using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Blanks;

/// <summary>
/// HRMentor bilan bitta sinxronlash yurishi (SPEC-blankalar-hrmentor §3): katalog → profil →
/// matnlar → anketa ta'rifi → blankalar (profil va ta'rif — SPEC-anketa-konstruktori §3, §5.2). Har besh daqiqada <see cref="HrMentorSyncJob"/>, qo'lda esa «Hozir yangilash»
/// (<see cref="SyncHrMentorBlanksCommandHandler"/>) — ikkalasi ham SHU kod.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qadamlar mustaqil.</b> Yiqilgan qadam xato kodini qayd etadi va o'zining oxirgi yaxshi
/// ma'lumotini qoldiradi, qolganlari baribir yuradi. HRMentor o'chiq bo'lsa ham anketa, PDF va
/// «Blankalar» sahifasi oxirgi nusxa bilan ishlayveradi — bu yerda hech narsa HRMentor tufayli
/// yiqilmaydi.
/// </para>
/// <para>
/// <b>Bir vaqtda bittasi.</b> Application qatlami Hangfire ni bilmaydi
/// (<c>DisableConcurrentExecution</c> yo'q), API esa bitta jarayon — shu sababli darvoza
/// jarayon ichidagi semafor (<see cref="Gate"/>). Ikki yurish bir vaqtda bitta blankani
/// yuklasa, ikkinchisi birinchisining faylini «eski» deb o'chirib yuborardi.
/// </para>
/// </remarks>
public sealed class HrMentorSync
{
    /// <summary>Blanka faylining eng katta hajmi — 50 MB.</summary>
    public const long MaxBlankBytes = 50L * 1024 * 1024;

    /// <summary>Hozircha HRMentor da tahrirlanadigan yagona blanka.</summary>
    public const string FormCode = CandidateForm.HrF02;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly Regex SafeExtension = new(
        @"^\.[A-Za-z0-9]{1,10}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex SafeContentType = new(
        @"^[a-z]+/[a-z0-9.+-]{1,100}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly SystemSettingsReader _settings;
    private readonly ISystemSettingRepository _settingRows;
    private readonly IHrMentorClient _client;
    private readonly IHrBlankRepository _blanks;
    private readonly IFormTextSnapshotRepository _snapshots;
    private readonly IFormDefinitionRepository _definitions;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrMentorSyncJournal _journal;
    private readonly IClock _clock;

    public HrMentorSync(
        SystemSettingsReader settings,
        ISystemSettingRepository settingRows,
        IHrMentorClient client,
        IHrBlankRepository blanks,
        IFormTextSnapshotRepository snapshots,
        IFormDefinitionRepository definitions,
        IFileStorage storage,
        IUnitOfWork unitOfWork,
        IHrMentorSyncJournal journal,
        IClock clock)
    {
        _settings = settings;
        _settingRows = settingRows;
        _client = client;
        _blanks = blanks;
        _snapshots = snapshots;
        _definitions = definitions;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _journal = journal;
        _clock = clock;
    }

    /// <summary>
    /// Bitta yurish. <paramref name="wait"/> — boshqa yurish tugashini shuncha kutadi
    /// (<see cref="TimeSpan.Zero"/> — band bo'lsa darhol voz kechadi).
    /// </summary>
    /// <returns>
    /// Natija; ulanish o'chiq bo'lsa <see cref="HrMentorErrors.NotConnected"/>, darvoza band
    /// bo'lsa <see cref="HrMentorErrors.Busy"/>.
    /// </returns>
    public async Task<Result<HrMentorSyncReport>> RunAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        if (!await Gate.WaitAsync(wait, cancellationToken).ConfigureAwait(false))
        {
            return HrMentorErrors.Busy;
        }

        try
        {
            return await RunLockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<Result<HrMentorSyncReport>> RunLockedAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var connection = HrMentorConnection.From(
            settings.Text(SystemSettingKeys.HrMentorBaseUrl),
            settings.Secret(SystemSettingKeys.HrMentorApiKey));

        if (connection is null)
        {
            return HrMentorErrors.NotConnected;
        }

        var startedAt = _clock.UtcNow;
        string? firstError = null;
        var passing = false;

        void Fail(HrMentorSyncStep step, Error error)
        {
            _journal.StepFailed(step, error.Code);
            firstError ??= error.Code;
            passing |= !HrMentorErrors.IsLasting(error.Code);
        }

        var catalog = HrF02TextCatalog.Current;
        var profile = FormProfile.Json(HrF02Seed.Build());
        var profileHash = CanonicalJson.Sha256(profile);

        async Task<bool> SendCatalogAsync()
        {
            var pushed = await _client
                .PushCatalogAsync(connection, FormCode, catalog.Json, cancellationToken)
                .ConfigureAwait(false);

            if (pushed.IsFailure)
            {
                Fail(HrMentorSyncStep.Catalog, pushed.Error);
                return false;
            }

            await WriteAsync(SystemSettingKeys.HrMentorCatalogHash, catalog.Hash, cancellationToken).ConfigureAwait(false);
            return true;
        }

        async Task<bool> SendProfileAsync()
        {
            var pushed = await _client
                .PushProfileAsync(connection, FormCode, profile.ToJsonString(), cancellationToken)
                .ConfigureAwait(false);

            if (pushed.IsFailure)
            {
                Fail(HrMentorSyncStep.Profile, pushed.Error);
                return false;
            }

            await WriteAsync(SystemSettingKeys.HrMentorProfileHash, profileHash, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // 1. Katalog — faqat xeshi o'zgarganda.
        var catalogCached = string.Equals(settings.Hidden(SystemSettingKeys.HrMentorCatalogHash), catalog.Hash, StringComparison.Ordinal);
        var catalogPushed = !catalogCached && await SendCatalogAsync().ConfigureAwait(false);

        // 1a. Profil — faqat xeshi o'zgarganda (katalogdan keyin: undan oldin HRMentor 404 beradi).
        var profilePushed = !string.Equals(settings.Hidden(SystemSettingKeys.HrMentorProfileHash), profileHash, StringComparison.Ordinal)
            && await SendProfileAsync().ConfigureAwait(false);

        // 2. HR matnlari — yangi reviziya bo'lsa.
        var texts = await PullTextsAsync(connection, catalog, cancellationToken).ConfigureAwait(false);

        // 2b. Xesh «yuborilgan» deydi, HRMentor esa formani bilmaydi (404): uning bazasi tiklangan
        // yoki forma qayta ulangan. Xeshlar eskirgan — katalog va profil shu yurishda qayta
        // yuboriladi, aks holda konstruktor HR-F02 ni hech qachon ko'rmaydi (2026-11-02, lokal
        // tiklashda topildi). Katalog shu yurishda yuborilgan bo'lsa qaytarilmaydi — aylanma yo'q.
        if (texts.IsSuccess && texts.Value.FormUnknown && catalogCached)
        {
            catalogPushed = await SendCatalogAsync().ConfigureAwait(false);
            if (catalogPushed)
            {
                profilePushed = await SendProfileAsync().ConfigureAwait(false) || profilePushed;
                texts = await PullTextsAsync(connection, catalog, cancellationToken).ConfigureAwait(false);
            }
        }

        if (texts.IsFailure)
        {
            Fail(HrMentorSyncStep.Texts, texts.Error);
        }

        // 2a. Konstruktor anketasi — yangi versiya, qaytarib olish yoki o'zgarishsiz.
        var definition = await PullDefinitionAsync(
                connection,
                settings.Hidden(SystemSettingKeys.HrMentorDefinitionVersion),
                cancellationToken)
            .ConfigureAwait(false);
        if (definition.IsFailure)
        {
            Fail(HrMentorSyncStep.Definition, definition.Error);
        }

        // 3. Blankalar.
        var blanks = await PullBlanksAsync(
                connection,
                settings.Hidden(SystemSettingKeys.HrMentorBlanksSource),
                cancellationToken)
            .ConfigureAwait(false);
        if (blanks.Error is { } blanksError)
        {
            Fail(HrMentorSyncStep.Blanks, blanksError);
        }

        await WriteAsync(SystemSettingKeys.HrMentorLastAttemptAt, Moment(startedAt), cancellationToken).ConfigureAwait(false);
        await WriteAsync(SystemSettingKeys.HrMentorLastError, firstError ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (firstError is null)
        {
            await WriteAsync(SystemSettingKeys.HrMentorLastSuccessAt, Moment(startedAt), cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var report = new HrMentorSyncReport(
            catalogPushed,
            texts.IsSuccess ? texts.Value.Saved : null,
            blanks.Added,
            blanks.Updated,
            blanks.Removed,
            firstError,
            profilePushed,
            definition.IsSuccess ? definition.Value : null,
            ErrorsLasting: firstError is not null && !passing);

        if (report.Changed)
        {
            _journal.Finished(report);
        }

        return report;
    }

    /// <summary>
    /// Yangi reviziya saqlansa — uning raqami (<see cref="TextsPull.Saved"/>); o'zgarish yo'q bo'lsa
    /// <c>null</c>. HRMentor formani bilmasa (404) — <see cref="TextsPull.FormUnknown"/>.
    /// </summary>
    private async Task<Result<TextsPull>> PullTextsAsync(
        HrMentorConnection connection,
        HrF02TextCatalog catalog,
        CancellationToken cancellationToken)
    {
        var served = await _client.GetServedTextsAsync(connection, FormCode, cancellationToken).ConfigureAwait(false);
        if (served.IsFailure)
        {
            return served.Error;
        }

        // 404 — katalog hali qabul qilinmagan: HR hech narsa o'zgartira olmagan.
        if (served.Value is not { } json)
        {
            return new TextsPull(null, FormUnknown: true);
        }

        var shaped = catalog.Shape(json);
        if (shaped.IsFailure)
        {
            return shaped.Error;
        }

        // Reviziya 0 — HR matnlarni hali saqlamagan: o'rnatilgan matnlar amalda qoladi.
        if (shaped.Value.Revision == 0)
        {
            return new TextsPull(null, FormUnknown: false);
        }

        var latest = await _snapshots.LatestAsync(FormCode, cancellationToken).ConfigureAwait(false);
        if (latest is not null && latest.Revision >= shaped.Value.Revision)
        {
            return new TextsPull(null, FormUnknown: false);
        }

        _snapshots.Add(FormTextSnapshot.Create(FormCode, shaped.Value.Revision, shaped.Value.PayloadJson, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TextsPull(shaped.Value.Revision, FormUnknown: false);
    }

    /// <summary>Matnlar qadamining natijasi: saqlangan reviziya va HRMentor formani bilmasligi.</summary>
    private readonly record struct TextsPull(int? Saved, bool FormUnknown);

    /// <summary>
    /// E'lon qilingan ta'rifni oladi (SPEC-anketa-konstruktori §5.2). Amaldagi anketa almashsa —
    /// qaysi versiyaga; o'zgarish yo'q bo'lsa <c>null</c>. Rad etilgan versiya saqlanmaydi va
    /// amaldagisi qoladi.
    /// </summary>
    private async Task<Result<HrMentorDefinitionSwitch?>> PullDefinitionAsync(
        HrMentorConnection connection,
        string? currentSetting,
        CancellationToken cancellationToken)
    {
        var served = await _client.GetPublishedDefinitionAsync(connection, FormCode, cancellationToken).ConfigureAwait(false);
        if (served.IsFailure)
        {
            return served.Error;
        }

        // 404 — katalog hali qabul qilinmagan: konstruktorda hech narsa yo'q.
        if (served.Value is not { } json)
        {
            return Result.Success<HrMentorDefinitionSwitch?>(null);
        }

        var published = HrMentorDefinitionPayload.Published(json, FormCode);
        if (published.IsFailure)
        {
            return published.Error;
        }

        int? current = int.TryParse(currentSetting, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        // E'lon qaytarib olindi — o'rnatilgan anketa. Saqlangan versiyalar qoladi (eski anketalar PDF i).
        if (published.Value is not { } offered)
        {
            if (current is null)
            {
                return Result.Success<HrMentorDefinitionSwitch?>(null);
            }

            await WriteAsync(SystemSettingKeys.HrMentorDefinitionVersion, string.Empty, cancellationToken).ConfigureAwait(false);
            return new HrMentorDefinitionSwitch(null);
        }

        var latest = await _definitions.LatestAsync(FormCode, cancellationToken).ConfigureAwait(false);
        if (latest is not null && latest.Version > offered.Version)
        {
            // Eskiroq versiya — e'tiborsiz (matnlar reviziyasi kabi): versiyalar faqat o'sadi.
            return Result.Success<HrMentorDefinitionSwitch?>(null);
        }

        var stored = latest?.Version == offered.Version ? latest : null;
        if (stored is not null)
        {
            // Bir versiya — bir mazmun: boshqa xesh bilan qayta kelgani qabul qilinmaydi.
            if (!string.Equals(stored.Sha256, offered.Sha256, StringComparison.Ordinal))
            {
                return HrMentorErrors.DefinitionInvalid;
            }
        }
        else
        {
            var read = FormDefinitionReader.Read(offered.Definition);
            if (read.IsFailure)
            {
                return read.Error;
            }

            _definitions.Add(FormDefinitionSnapshot.Create(
                FormCode,
                offered.Version,
                offered.Label,
                CanonicalJson.Write(offered.Definition),
                offered.Sha256,
                _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (current == offered.Version)
        {
            return Result.Success<HrMentorDefinitionSwitch?>(null);
        }

        await WriteAsync(
                SystemSettingKeys.HrMentorDefinitionVersion,
                offered.Version.ToString(CultureInfo.InvariantCulture),
                cancellationToken)
            .ConfigureAwait(false);
        return new HrMentorDefinitionSwitch(offered.Version);
    }

    private sealed record BlanksOutcome(int Added, int Updated, int Removed, Error? Error);

    /// <summary>
    /// Blankalar (SPEC-hujjat-manbai §2): HRMentor da SHU platformaga biriktirilgan hujjatlar;
    /// kalit hali platformaga bog'lanmagan bo'lsa — eski umumiy ro'yxat. Oxirida platformaga
    /// qaysi versiyalar bizda turgani aytiladi; bu yiqilsa — qadam xatosi, blankalar esa saqlangan.
    /// </summary>
    /// <param name="connection">HRMentor ulanishi.</param>
    /// <param name="previousSource">Oxirgi muvaffaqiyatli ro'yxat manbai (<c>hrmentor.blanksSource</c>).</param>
    /// <param name="cancellationToken">Bekor qilish.</param>
    private async Task<BlanksOutcome> PullBlanksAsync(
        HrMentorConnection connection,
        string previousSource,
        CancellationToken cancellationToken)
    {
        var fromPlatform = true;
        Result<IReadOnlyList<HrMentorBlankItem>> listed;
        var platform = await _client.ListPlatformDocumentsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (platform.IsSuccess
            && platform.Value.Documents.Count == 0
            && !string.Equals(previousSource, SystemSettingKeys.HrMentorBlanksFromPlatform, StringComparison.Ordinal))
        {
            // Platforma hali hech narsa bermagan: ruxsat papkadan oldin berilgan. Bo'sh ro'yxat bilan
            // birinchi o'tish bor blankalarni o'chirib yuborardi — platforma ishga tushmagan deb
            // hisoblanadi va eski ro'yxat olinadi (SPEC-hujjat-manbai §2).
            fromPlatform = false;
            listed = await _client.ListBlanksAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        else if (platform.IsSuccess)
        {
            listed = Result.Success(platform.Value.Documents);
        }
        else if (platform.Error.Code == HrMentorErrors.PlatformNotReady.Code)
        {
            // O'tish davri: HRMentor da kalit hali «hr.bonvi.uz» ga bog'lanmagan — eski ro'yxat.
            fromPlatform = false;
            listed = await _client.ListBlanksAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Boshqa har qanday xato (platforma o'chirilgan ham) — qadam xatosi, oxirgi nusxa qoladi.
            return new BlanksOutcome(0, 0, 0, platform.Error);
        }

        if (listed.IsFailure)
        {
            return new BlanksOutcome(0, 0, 0, listed.Error);
        }

        var stored = (await _blanks.ListTrackedAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(blank => blank.DocumentId);
        var listedIds = new HashSet<Guid>();
        int added = 0, updated = 0, removed = 0;
        Error? firstError = null;

        foreach (var item in listed.Value)
        {
            if (!listedIds.Add(item.DocumentId))
            {
                continue;
            }

            stored.TryGetValue(item.DocumentId, out var existing);
            var version = ToVersion(item);

            // O'sha versiya — fayl o'zgarmagan; faqat nom yoki kod yangilanishi mumkin.
            if (existing is not null && existing.VersionId == item.VersionId && existing.Sha256 == version.Sha256)
            {
                if (existing.Code != version.Code.Trim() || existing.Title != version.Title.Trim())
                {
                    existing.Rename(version.Code, version.Title);
                    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            var fileKey = await DownloadAsync(connection, item, version, fromPlatform, cancellationToken).ConfigureAwait(false);
            if (fileKey.IsFailure)
            {
                _journal.BlankSkipped(item.DocumentId, fileKey.Error.Code);
                firstError ??= fileKey.Error;
                continue;
            }

            if (existing is null)
            {
                _blanks.Add(HrBlank.Create(item.DocumentId, version, fileKey.Value, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                added++;
            }
            else
            {
                var oldKey = existing.Replace(version, fileKey.Value, _clock.UtcNow);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await _storage.DeleteAsync(oldKey, cancellationToken).ConfigureAwait(false);
                updated++;
            }
        }

        // Ro'yxatdan chiqqanlar — qator, keyin fayl.
        foreach (var gone in stored.Values.Where(blank => !listedIds.Contains(blank.DocumentId)))
        {
            _blanks.Remove(gone);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _storage.DeleteAsync(gone.FileKey, cancellationToken).ConfigureAwait(false);
            removed++;
        }

        await WriteAsync(
                SystemSettingKeys.HrMentorBlanksSource,
                fromPlatform ? SystemSettingKeys.HrMentorBlanksFromPlatform : SystemSettingKeys.HrMentorBlanksFromLegacy,
                cancellationToken)
            .ConfigureAwait(false);

        if (fromPlatform)
        {
            // To'liq to'plam: yuklanmagan (xato bergan) blanka eski versiyasi bilan yoki umuman yo'q.
            var held = await _blanks.ListTrackedAsync(cancellationToken).ConfigureAwait(false);
            var reported = await _client
                .ReportHoldingsAsync(
                    connection,
                    [.. held.Select(blank => new HrMentorHolding(blank.DocumentId, blank.VersionId))],
                    cancellationToken)
                .ConfigureAwait(false);
            if (reported.IsFailure)
            {
                firstError ??= reported.Error;
            }
        }

        return new BlanksOutcome(added, updated, removed, firstError);
    }

    /// <summary>Faylni yuklaydi, hajmi va SHA-256 ini tekshiradi, saqlaydi — kalitini qaytaradi.</summary>
    private async Task<Result<string>> DownloadAsync(
        HrMentorConnection connection,
        HrMentorBlankItem item,
        HrBlankVersion version,
        bool fromPlatform,
        CancellationToken cancellationToken)
    {
        if (item.SizeBytes > MaxBlankBytes)
        {
            return HrMentorErrors.BlankTooLarge;
        }

        var downloaded = fromPlatform
            ? await _client
                .DownloadPlatformDocumentAsync(connection, item.DocumentId, MaxBlankBytes, cancellationToken)
                .ConfigureAwait(false)
            : await _client
                .DownloadBlankAsync(connection, item.DocumentId, MaxBlankBytes, cancellationToken)
                .ConfigureAwait(false);
        if (downloaded.IsFailure)
        {
            return downloaded.Error;
        }

        var bytes = downloaded.Value;
        if (bytes.LongLength != item.SizeBytes
            || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), version.Sha256, StringComparison.Ordinal))
        {
            return HrMentorErrors.BlankChecksum;
        }

        using var content = new MemoryStream(bytes, writable: false);
        var saved = await _storage
            .SaveAsync(content, StorageName(item), version.ContentType, cancellationToken)
            .ConfigureAwait(false);

        return saved.Key;
    }

    private static HrBlankVersion ToVersion(HrMentorBlankItem item)
    {
        var contentType = (item.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!SafeContentType.IsMatch(contentType))
        {
            contentType = "application/octet-stream";
        }

        return new HrBlankVersion(
            item.Code?.Trim() ?? string.Empty,
            item.Title,
            item.VersionId,
            item.VersionNo,
            item.PublishedAt,
            item.FileName,
            contentType,
            item.SizeBytes,
            (item.Sha256 ?? string.Empty).Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Saqlashga beriladigan nom: faqat kengaytma ahamiyatli (<c>IFileStorage</c> kalitni o'zi
    /// yasaydi), va u xavfsiz shaklda bo'lishi shart.
    /// </summary>
    private static string StorageName(HrMentorBlankItem item)
    {
        var extension = Path.GetExtension(item.FileName ?? string.Empty);
        return SafeExtension.IsMatch(extension) ? "blank" + extension.ToLowerInvariant() : "blank";
    }

    private static string Moment(DateTimeOffset value)
        => value.ToUniversalTime().ToString(SystemSettingKeys.HrMentorMomentFormat, CultureInfo.InvariantCulture);

    /// <summary>Yashirin sozlamani yozadi (saqlash chaqiruvchida).</summary>
    private async Task WriteAsync(string key, string value, CancellationToken cancellationToken)
    {
        var row = await _settingRows.FindAsync(key, cancellationToken).ConfigureAwait(false);
        Result applied;

        if (row is null)
        {
            var created = SystemSetting.CreateServerValue(key, value);
            if (created.IsSuccess)
            {
                _settingRows.Add(created.Value);
            }

            applied = created.IsSuccess ? Result.Success() : created.Error;
        }
        else
        {
            applied = row.SetServerValue(value);
        }

        if (applied.IsFailure)
        {
            throw new InvalidOperationException($"«{key}» yozilmadi: {applied.Error.Code}");
        }
    }
}
