using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Blanks;

/// <summary>Seeder uchun bitta shablon fayli: kod (fayl nomi) va JSON matni.</summary>
/// <param name="Code">Fayl nomidagi kod (<c>HR-F06</c>) — ta'rifdagi <c>code</c> ga teng bo'lishi shart.</param>
/// <param name="Json">Ta'rif JSON.</param>
public sealed record BlankTemplateFile(string Code, string Json);

/// <summary>Seeder yurishining natijasi (log va test uchun).</summary>
/// <param name="TemplatesCreated">Yangi shablon qatorlari.</param>
/// <param name="VersionsPublished">E'lon qilingan yangi versiyalar.</param>
/// <param name="Unchanged">O'sha versiya, o'sha xesh — hech narsa qilinmadi.</param>
/// <param name="Conflicts">O'sha versiya, BOSHQA xesh yoki eskiroq versiya — qayta yozilmadi (xato logda).</param>
/// <param name="Invalid">Tekshiruvdan o'tmagan fayllar (xato logda).</param>
public sealed record BlankTemplateSeedResult(int TemplatesCreated, int VersionsPublished, int Unchanged, int Conflicts, int Invalid);

/// <summary>
/// Ichiga joylangan shablon fayllarini (<c>Blanks/Templates/*.json</c>) bazaga yozadi — SPEC-blankalar §3.1–3.2, §14 S3.
/// </summary>
/// <remarks>
/// <para>
/// Har bir fayl uchun: shablon yo'q → yaratiladi; versiya yangi → qo'shiladi va e'lon qilinadi, avvalgi
/// e'lon qilingani eskiradi (Retired); o'sha versiya bazada bor va xeshi teng → hech narsa; o'sha versiya,
/// lekin xesh BOSHQA → xato logga yoziladi va bazadagisi QAYTA YOZILMAYDI. E'lon qilingan ta'rif o'zgarmaydi:
/// unga bog'langan blankalar umrbod shu ta'rif bilan tekshiriladi va chop etiladi. O'zgarish — versiyani
/// oshirish.
/// </para>
/// <para>
/// Xesh — kanonik JSON'dan (kalitlar tartiblangan, bo'shliqsiz): fayldagi formatlash yoki kalit tartibi
/// o'zgarsa «ziddiyat» chiqmaydi. Bazaga ham aynan shu kanonik matn yoziladi.
/// </para>
/// <para>
/// Ishga tushishni yiqitmaydi: yaroqsiz fayl yoki ziddiyat — <c>Error</c> log, qolgan shablonlar
/// davom etadi. Fayllarning yaroqliligini <c>WaveOneTemplatesTests</c> build vaqtida kafolatlaydi.
/// </para>
/// </remarks>
public sealed partial class BlankTemplateSeeder
{
    private const string ResourceMarker = ".Blanks.Templates.";

    private static readonly Assembly ResourceAssembly = typeof(BlankTemplateSeeder).Assembly;

    private readonly AppDbContext _context;
    private readonly IClock _clock;
    private readonly ILogger<BlankTemplateSeeder> _logger;

    public BlankTemplateSeeder(AppDbContext context, IClock clock, ILogger<BlankTemplateSeeder> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    private enum Outcome
    {
        Unchanged,
        Created,
        Published,
        Conflict,
    }

    [LoggerMessage(
        EventId = 2400,
        Level = LogLevel.Error,
        Message = "Blanka shabloni {Code} yaroqsiz — urug'lanmadi: {Errors}")]
    private partial void LogInvalid(string code, string errors);

    [LoggerMessage(
        EventId = 2401,
        Level = LogLevel.Error,
        Message = "Blanka shabloni {Code} v{Version}: bazadagi e'lon qilingan ta'rif fayldagidan farq qiladi (xesh {Stored} ≠ {Embedded}) — QAYTA YOZILMADI. O'zgarish uchun versiyani oshiring.")]
    private partial void LogHashConflict(string code, string version, string stored, string embedded);

    [LoggerMessage(
        EventId = 2402,
        Level = LogLevel.Error,
        Message = "Blanka shabloni {Code}: fayldagi v{Version} bazadagi e'lon qilingan v{Published} dan eski — urug'lanmadi.")]
    private partial void LogOlderVersion(string code, string version, string published);

    [LoggerMessage(
        EventId = 2403,
        Level = LogLevel.Information,
        Message = "Blanka shabloni {Code} v{Version} e'lon qilindi (avvalgisi: {Previous})")]
    private partial void LogPublished(string code, string version, string previous);

    [LoggerMessage(
        EventId = 2404,
        Level = LogLevel.Information,
        Message = "Blanka shablonlari: {Created} yangi shablon, {Published} yangi versiya, {Unchanged} o'zgarmagan, {Conflicts} ziddiyat, {Invalid} yaroqsiz")]
    private partial void LogTotal(int created, int published, int unchanged, int conflicts, int invalid);

    /// <summary>Ichiga joylangan barcha shablon fayllarini urug'laydi.</summary>
    public Task<BlankTemplateSeedResult> SeedAsync(CancellationToken cancellationToken = default)
        => SeedAsync(ReadEmbeddedFiles(), cancellationToken);

    /// <summary>Berilgan fayllarni urug'laydi (testlar o'zgartirilgan faylni shu yerdan beradi).</summary>
    /// <param name="files">Shablon fayllari.</param>
    /// <param name="cancellationToken">Bekor qilish.</param>
    public async Task<BlankTemplateSeedResult> SeedAsync(IEnumerable<BlankTemplateFile> files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        int created = 0, published = 0, unchanged = 0, conflicts = 0, invalid = 0;

        foreach (var file in files.OrderBy(file => file.Code, StringComparer.Ordinal))
        {
            var validation = BlankDefinitionValidator.Validate(file.Json, file.Code);

            if (!validation.IsValid)
            {
                invalid++;
                LogInvalid(file.Code, string.Join("; ", validation.Errors.Select(error => $"{error.Path}: {error.Message}")));
                continue;
            }

            var canonical = Canonicalize(file.Json);
            var outcome = await _context.ExecuteInTransactionAsync(
                token => SeedOneAsync(validation.Definition!, canonical, Sha256Hex(canonical), token),
                cancellationToken).ConfigureAwait(false);

            switch (outcome)
            {
                case Outcome.Created:
                    created++;
                    published++;
                    break;
                case Outcome.Published:
                    published++;
                    break;
                case Outcome.Conflict:
                    conflicts++;
                    break;
                default:
                    unchanged++;
                    break;
            }
        }

        LogTotal(created, published, unchanged, conflicts, invalid);
        return new BlankTemplateSeedResult(created, published, unchanged, conflicts, invalid);
    }

    /// <summary>Bitta shablon — tranzaksiya ichida; takrorlanganda hammasini qayta o'qiydi.</summary>
    private async Task<Outcome> SeedOneAsync(BlankDefinition definition, string canonical, string hash, CancellationToken cancellationToken)
    {
        var template = await _context.BlankTemplates
            .SingleOrDefaultAsync(row => row.Code == definition.Code, cancellationToken)
            .ConfigureAwait(false);

        var isNewTemplate = template is null;

        if (template is null)
        {
            template = Ensure(BlankTemplate.Create(definition.Code, definition.Title, SubjectKindOf(definition.Subject)));
            _context.BlankTemplates.Add(template);
        }

        var versions = isNewTemplate
            ? []
            : await _context.BlankTemplateVersions
                .Where(row => row.TemplateId == template.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        if (versions.Find(row => row.Version == definition.Version) is { } existing)
        {
            if (existing.DefinitionSha256 == hash)
            {
                return Outcome.Unchanged;
            }

            LogHashConflict(definition.Code, definition.Version, existing.DefinitionSha256, hash);
            return Outcome.Conflict;
        }

        var current = versions.Find(row => row.Status == BlankTemplateVersionStatus.Published);

        if (current is not null && IsOlder(definition.Version, current.Version))
        {
            LogOlderVersion(definition.Code, definition.Version, current.Version);
            return Outcome.Conflict;
        }

        // Qisman unikal indeks (bitta Published) har buyruqda tekshiriladi: eskisi AVVAL saqlanadi
        if (current is not null)
        {
            Ensure(current.Retire());
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var version = Ensure(BlankTemplateVersion.Create(
            template.Id, definition.Version, canonical, hash, BlankTemplateSource.Seed));
        Ensure(version.Publish(null, _clock.UtcNow));
        _context.BlankTemplateVersions.Add(version);

        if (!isNewTemplate)
        {
            Ensure(template.Update(definition.Title, template.SortOrder));
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogPublished(definition.Code, definition.Version, current?.Version ?? "—");
        return isNewTemplate ? Outcome.Created : Outcome.Published;
    }

    /// <summary>Tekshirilgan ta'rifda domen rad etishi — dastur xatosi, jim o'tkazilmaydi.</summary>
    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Blanka shablonini urug'lab bo'lmadi: {result.Error.Code}");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        Ensure((Result)result);
        return result.Value;
    }

    private static BlankSubjectKind SubjectKindOf(string subject) => subject switch
    {
        "employee" => BlankSubjectKind.Employee,
        "candidate" => BlankSubjectKind.Candidate,
        _ => BlankSubjectKind.None,
    };

    /// <summary><c>1.10</c> &gt; <c>1.9</c>: raqam sifatida taqqoslanadi; tushunilmasa — eski emas deb olinadi.</summary>
    private static bool IsOlder(string candidate, string published)
        => Version.TryParse(candidate, out var a) && Version.TryParse(published, out var b) && a < b;

    /// <summary>Kanonik JSON: obyekt kalitlari ordinal tartibda, bo'shliqsiz.</summary>
    public static string Canonicalize(string json)
        => Sort(JsonNode.Parse(json))?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null";

    /// <summary>SHA-256 ning kichik harfli hex yozuvi (<c>definition_sha256</c>).</summary>
    public static string Sha256Hex(string canonical)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => KeyValuePair.Create(pair.Key, Sort(pair.Value)))),
        JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
        _ => node?.DeepClone(),
    };

    private static List<BlankTemplateFile> ReadEmbeddedFiles()
    {
        var files = new List<BlankTemplateFile>();

        foreach (var name in ResourceAssembly.GetManifestResourceNames())
        {
            var marker = name.IndexOf(ResourceMarker, StringComparison.Ordinal);

            if (marker < 0 || !name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = ResourceAssembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Resurs o'qilmadi: {name}");
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var code = name[(marker + ResourceMarker.Length)..^".json".Length];
            files.Add(new BlankTemplateFile(code, reader.ReadToEnd()));
        }

        return files;
    }
}
