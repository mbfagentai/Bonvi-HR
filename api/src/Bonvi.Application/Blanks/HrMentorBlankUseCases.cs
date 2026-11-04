using System.Globalization;
using Bonvi.Application.Settings;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Blanks;

/// <summary>
/// <c>hrmentor:sync</c> — har besh daqiqada HRMentor dan katalog, matnlar va blankalar
/// (SPEC-blankalar-hrmentor §3).
/// </summary>
/// <remarks>
/// Boshqa yurish ketayotgan bo'lsa (masalan, «Hozir yangilash») kutmaydi: keyingi besh
/// daqiqa baribir keladi. Ulanish o'chiq bo'lsa hech narsa qilmaydi.
/// </remarks>
public sealed class HrMentorSyncJob
{
    private readonly HrMentorSync _sync;

    public HrMentorSyncJob(HrMentorSync sync) => _sync = sync;

    /// <summary>Bitta yurish.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    public Task RunAsync(CancellationToken cancellationToken)
        => _sync.RunAsync(TimeSpan.Zero, cancellationToken);
}

/// <summary>Bitta blanka — «Blankalar» sahifasining qatori.</summary>
public sealed record HrMentorBlankDto(
    Guid Id,
    string Code,
    string Title,
    int VersionNo,
    DateTimeOffset? PublishedAt,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>Amaldagi konstruktor anketasi — «Anketa: HRMentor v4.0» qatori (SPEC-anketa-konstruktori §9).</summary>
/// <param name="Version">HRMentor dagi versiya raqami.</param>
/// <param name="Label">Yorliq.</param>
/// <param name="ReceivedAt">Olingan lahza.</param>
public sealed record HrMentorBlankFormDto(int Version, string Label, DateTimeOffset ReceivedAt);

/// <summary>«Blankalar» sahifasi: ulanish holati va blankalar (SPEC-blankalar-hrmentor §6).</summary>
/// <param name="Connected">Manzil va kalit ikkalasi qo'yilganmi.</param>
/// <param name="LastAttemptAt">Oxirgi urinish.</param>
/// <param name="LastSuccessAt">Oxirgi xatosiz yurish.</param>
/// <param name="LastError">Oxirgi urinishning xato kodi; bo'lmasa <c>null</c>.</param>
/// <param name="TextsRevision">Amaldagi HR matnlari reviziyasi; bo'lmasa <c>null</c>.</param>
/// <param name="Items">Blankalar — kod bo'yicha.</param>
/// <param name="Form">Amaldagi konstruktor anketasi; <c>null</c> — dasturdagi o'rnatilgan anketa.</param>
/// <param name="Source">Blankalar manbai: «platform», «legacy» (kalit hali platformaga bog'lanmagan) yoki <c>null</c>.</param>
public sealed record HrMentorBlanksDto(
    bool Connected,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    int? TextsRevision,
    IReadOnlyList<HrMentorBlankDto> Items,
    HrMentorBlankFormDto? Form = null,
    string? Source = null);

/// <summary>Sahifa so'rovi.</summary>
public sealed record GetHrMentorBlanksQuery;

/// <summary><c>GET /api/hrmentor/blanks</c>.</summary>
public sealed class GetHrMentorBlanksQueryHandler
{
    private readonly SystemSettingsReader _settings;
    private readonly IHrBlankRepository _blanks;
    private readonly IFormTextSnapshotRepository _snapshots;
    private readonly FormDefinitionsReader _definitions;

    public GetHrMentorBlanksQueryHandler(
        SystemSettingsReader settings,
        IHrBlankRepository blanks,
        IFormTextSnapshotRepository snapshots,
        FormDefinitionsReader definitions)
    {
        _settings = settings;
        _blanks = blanks;
        _snapshots = snapshots;
        _definitions = definitions;
    }

    public async Task<Result<HrMentorBlanksDto>> HandleAsync(GetHrMentorBlanksQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // «Hozir yangilash» dan keyin — yurish yozgan holat ko'rinsin.
        _settings.Forget();
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var connected = HrMentorConnection.From(
            settings.Text(SystemSettingKeys.HrMentorBaseUrl),
            settings.Secret(SystemSettingKeys.HrMentorApiKey)) is not null;

        var rows = await _blanks.ListAsync(cancellationToken).ConfigureAwait(false);
        var latest = await _snapshots.LatestAsync(CandidateForm.HrF02, cancellationToken).ConfigureAwait(false);
        var error = settings.Hidden(SystemSettingKeys.HrMentorLastError);
        var form = await _definitions.CurrentAsync(cancellationToken).ConfigureAwait(false);

        return new HrMentorBlanksDto(
            connected,
            Moment(settings.Hidden(SystemSettingKeys.HrMentorLastAttemptAt)),
            Moment(settings.Hidden(SystemSettingKeys.HrMentorLastSuccessAt)),
            string.IsNullOrWhiteSpace(error) ? null : error,
            latest?.Revision,
            [.. rows.Select(row => new HrMentorBlankDto(
                row.Id,
                row.Code,
                row.Title,
                row.VersionNo,
                row.PublishedAt,
                row.FileName,
                row.ContentType,
                row.SizeBytes))],
            form is null ? null : new HrMentorBlankFormDto(form.Version, form.Label, form.ReceivedAt),
            Source(settings.Hidden(SystemSettingKeys.HrMentorBlanksSource)));
    }

    private static DateTimeOffset? Moment(string value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment)
            ? moment
            : null;

    /// <summary>Blankalar manbai (SPEC-hujjat-manbai §2) — faqat ma'lum ikki qiymat, aks holda <c>null</c>.</summary>
    private static string? Source(string value) => value switch
    {
        SystemSettingKeys.HrMentorBlanksFromPlatform => SystemSettingKeys.HrMentorBlanksFromPlatform,
        SystemSettingKeys.HrMentorBlanksFromLegacy => SystemSettingKeys.HrMentorBlanksFromLegacy,
        _ => null,
    };
}

/// <summary>Bitta blankaning fayli.</summary>
public sealed record GetHrMentorBlankFileQuery(Guid Id);

/// <summary>Yuklab olinadigan fayl.</summary>
public sealed record HrMentorBlankFile(string FileName, string ContentType, Stream Content);

/// <summary><c>GET /api/hrmentor/blanks/{id}/file</c>.</summary>
public sealed class GetHrMentorBlankFileQueryHandler
{
    /// <summary>Blanka (yoki uning fayli) topilmadi.</summary>
    public static readonly Error NotFound = Error.NotFound("hrmentor.blank_not_found", "Blanka topilmadi.");

    private readonly IHrBlankRepository _blanks;
    private readonly IFileStorage _storage;

    public GetHrMentorBlankFileQueryHandler(IHrBlankRepository blanks, IFileStorage storage)
    {
        _blanks = blanks;
        _storage = storage;
    }

    public async Task<Result<HrMentorBlankFile>> HandleAsync(GetHrMentorBlankFileQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var blank = await _blanks.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (blank is null)
        {
            return NotFound;
        }

        var content = await _storage.OpenAsync(blank.FileKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return NotFound;
        }

        return new HrMentorBlankFile(blank.FileName, blank.ContentType, content);
    }
}

/// <summary>«Hozir yangilash».</summary>
public sealed record SyncHrMentorBlanksCommand;

/// <summary>
/// <c>POST /api/hrmentor/blanks/sync</c> — o'sha yurish darhol, keyin sahifa javobi. Boshqa yurish
/// ketayotgan bo'lsa uning tugashini kutadi (ko'pi bilan <see cref="Wait"/>).
/// </summary>
public sealed class SyncHrMentorBlanksCommandHandler
{
    /// <summary>Ketayotgan yurishni kutish chegarasi.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(120);

    private readonly HrMentorSync _sync;
    private readonly GetHrMentorBlanksQueryHandler _query;

    public SyncHrMentorBlanksCommandHandler(HrMentorSync sync, GetHrMentorBlanksQueryHandler query)
    {
        _sync = sync;
        _query = query;
    }

    public async Task<Result<HrMentorBlanksDto>> HandleAsync(SyncHrMentorBlanksCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var run = await _sync.RunAsync(Wait, cancellationToken).ConfigureAwait(false);
        if (run.IsFailure)
        {
            return run.Error;
        }

        return await _query.HandleAsync(new GetHrMentorBlanksQuery(), cancellationToken).ConfigureAwait(false);
    }
}
