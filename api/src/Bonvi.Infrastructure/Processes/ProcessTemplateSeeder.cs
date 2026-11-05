using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Processes;

/// <summary>One template file: the code (from the file name) and its JSON.</summary>
/// <param name="Code">File name without extension; must equal the definition's <c>code</c>.</param>
/// <param name="Json">Definition JSON.</param>
public sealed record ProcessTemplateFile(string Code, string Json);

/// <summary>What the host environment decides for one seed run (SPEC-mening-ishlarim §3.1, §6.6).</summary>
/// <param name="FailOnInvalid">An invalid file stops startup (Development) instead of being logged and skipped.</param>
/// <param name="WriteStartedOn">Writes <c>processes.startedOn</c> if absent — never in the Testing environment.</param>
public sealed record ProcessTemplateSeedOptions(bool FailOnInvalid, bool WriteStartedOn);

/// <summary>Result of one seed run (log and tests).</summary>
/// <param name="TemplatesCreated">New template rows.</param>
/// <param name="VersionsPublished">Newly published versions.</param>
/// <param name="Unchanged">Same version, same hash: nothing done.</param>
/// <param name="Conflicts">Same version with another hash, or a version older than the Published one: not written.</param>
/// <param name="Invalid">Files that failed validation: not written.</param>
public sealed record ProcessTemplateSeedResult(int TemplatesCreated, int VersionsPublished, int Unchanged, int Conflicts, int Invalid);

/// <summary>
/// Writes the embedded templates (<c>Processes/Templates/*.json</c>) to the database, SPEC-mening-ishlarim §3.1 and
/// §6.1. Same rules as <c>BlankTemplateSeeder</c>.
/// </summary>
/// <remarks>
/// <para>
/// Per file: no template → created; new version → added and published, the previous Published one retired; same
/// version and hash → nothing; same version with ANOTHER hash → error log, the stored row is NOT overwritten,
/// because running cases read that definition for life. A change means a higher version.
/// </para>
/// <para>
/// On an ordinary restart this writes nothing, and it never touches <see cref="ProcessTemplate.IsActive"/>: a
/// template somebody deactivated stays inactive through restarts and new versions.
/// </para>
/// <para>
/// The hash is taken over canonical JSON (sorted keys, no whitespace), so formatting never reads as a conflict.
/// This format is persisted in <c>definition_sha256</c>, hence its own copy rather than a shared helper with the
/// blanks engine, whose seeder is tracked as document debt (D6).
/// </para>
/// </remarks>
public sealed partial class ProcessTemplateSeeder
{
    private const string ResourceMarker = ".Processes.Templates.";

    private static readonly Assembly ResourceAssembly = typeof(ProcessTemplateSeeder).Assembly;

    private readonly AppDbContext _context;
    private readonly IClock _clock;
    private readonly ILogger<ProcessTemplateSeeder> _logger;

    public ProcessTemplateSeeder(AppDbContext context, IClock clock, ILogger<ProcessTemplateSeeder> logger)
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
        EventId = 2500,
        Level = LogLevel.Error,
        Message = "Process template {Code} is invalid, not seeded: {Errors}")]
    private partial void LogInvalid(string code, string errors);

    [LoggerMessage(
        EventId = 2501,
        Level = LogLevel.Error,
        Message = "Process template {Code} v{Version}: the stored definition differs from the file (hash {Stored} != {Embedded}), NOT overwritten. Raise the version to change it.")]
    private partial void LogHashConflict(string code, string version, string stored, string embedded);

    [LoggerMessage(
        EventId = 2502,
        Level = LogLevel.Error,
        Message = "Process template {Code}: file v{Version} is older than the published v{Published}, not seeded.")]
    private partial void LogOlderVersion(string code, string version, string published);

    [LoggerMessage(
        EventId = 2503,
        Level = LogLevel.Information,
        Message = "Process template {Code} v{Version} published (previous: {Previous})")]
    private partial void LogPublished(string code, string version, string previous);

    [LoggerMessage(
        EventId = 2504,
        Level = LogLevel.Information,
        Message = "Process templates: {Created} new, {Published} versions published, {Unchanged} unchanged, {Conflicts} conflicts, {Invalid} invalid")]
    private partial void LogTotal(int created, int published, int unchanged, int conflicts, int invalid);

    [LoggerMessage(
        EventId = 2505,
        Level = LogLevel.Information,
        Message = "Process engine start date written: {StartedOn}")]
    private partial void LogStartedOn(string startedOn);

    /// <summary>Seeds every embedded template.</summary>
    public Task<ProcessTemplateSeedResult> SeedAsync(ProcessTemplateSeedOptions options, CancellationToken cancellationToken = default)
        => SeedAsync(ReadEmbeddedFiles(), options, cancellationToken);

    /// <summary>Seeds the given files (tests pass altered ones here).</summary>
    /// <exception cref="InvalidOperationException">
    /// A file is invalid and <see cref="ProcessTemplateSeedOptions.FailOnInvalid"/> is set. Raised before any write,
    /// with every error of every file.
    /// </exception>
    public async Task<ProcessTemplateSeedResult> SeedAsync(
        IEnumerable<ProcessTemplateFile> files,
        ProcessTemplateSeedOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(options);

        var valid = new List<(ProcessDefinition Definition, string Canonical)>();
        var problems = new List<string>();

        foreach (var file in files.OrderBy(file => file.Code, StringComparer.Ordinal))
        {
            var errors = Check(file, out var definition, out var canonical);

            if (errors.Count > 0)
            {
                var joined = string.Join("; ", errors);
                LogInvalid(file.Code, joined);
                problems.Add($"{file.Code}: {joined}");
                continue;
            }

            valid.Add((definition!, canonical!));
        }

        if (problems.Count > 0 && options.FailOnInvalid)
        {
            throw new InvalidOperationException("Invalid process templates. " + string.Join(" | ", problems));
        }

        int created = 0, published = 0, unchanged = 0, conflicts = 0;

        foreach (var (definition, canonical) in valid)
        {
            var outcome = await _context.ExecuteInTransactionAsync(
                token => SeedOneAsync(definition, canonical, Sha256Hex(canonical), token),
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

        LogTotal(created, published, unchanged, conflicts, problems.Count);

        // A release with a broken template stays dormant (no cases, gates off) rather than half a process engine
        if (options.WriteStartedOn && problems.Count == 0)
        {
            await EnsureStartedOnAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ProcessTemplateSeedResult(created, published, unchanged, conflicts, problems.Count);
    }

    /// <summary>
    /// The validator, plus the column limits of the domain: both must reject here, because a rejection inside
    /// <see cref="SeedOneAsync"/> would stop startup in Production too.
    /// </summary>
    private static List<string> Check(ProcessTemplateFile file, out ProcessDefinition? definition, out string? canonical)
    {
        definition = null;
        canonical = null;

        var validation = ProcessDefinitionValidator.Validate(file.Json, file.Code);

        if (!validation.IsValid)
        {
            return validation.Errors.Select(error => error.ToString()).ToList();
        }

        var candidate = validation.Definition!;
        var text = Canonicalize(file.Json);

        var template = ProcessTemplate.Create(candidate.Code, candidate.Title, candidate.Source, SubjectKindOf(candidate.SubjectKind));

        if (template.IsFailure)
        {
            return [$"$: code, title or source is over the column limit ({template.Error.Code})"];
        }

        var version = ProcessTemplateVersion.Create(template.Value.Id, candidate.Version, text, Sha256Hex(text));

        if (version.IsFailure)
        {
            return [$"$: version or definition is over the column limit ({version.Error.Code})"];
        }

        definition = candidate;
        canonical = text;
        return [];
    }

    /// <summary>One template, inside a transaction; a retry reads everything again.</summary>
    private async Task<Outcome> SeedOneAsync(ProcessDefinition definition, string canonical, string hash, CancellationToken cancellationToken)
    {
        var template = await _context.ProcessTemplates
            .SingleOrDefaultAsync(row => row.Code == definition.Code, cancellationToken)
            .ConfigureAwait(false);

        var isNewTemplate = template is null;

        if (template is null)
        {
            template = Ensure(ProcessTemplate.Create(definition.Code, definition.Title, definition.Source, SubjectKindOf(definition.SubjectKind)));
            _context.ProcessTemplates.Add(template);
        }

        var versions = isNewTemplate
            ? []
            : await _context.ProcessTemplateVersions
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

        var current = versions.Find(row => row.Status == ProcessTemplateVersionStatus.Published);

        if (current is not null && IsOlder(definition.Version, current.Version))
        {
            LogOlderVersion(definition.Code, definition.Version, current.Version);
            return Outcome.Conflict;
        }

        // The partial unique index (one Published) is checked per statement: the old one is saved FIRST
        if (current is not null)
        {
            Ensure(current.Retire());
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var version = Ensure(ProcessTemplateVersion.Create(template.Id, definition.Version, canonical, hash));
        Ensure(version.Publish(_clock.UtcNow));
        _context.ProcessTemplateVersions.Add(version);

        if (!isNewTemplate)
        {
            Ensure(template.Update(definition.Title, definition.Source));
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogPublished(definition.Code, definition.Version, current?.Version ?? "-");
        return isNewTemplate ? Outcome.Created : Outcome.Published;
    }

    /// <summary>
    /// Written once: any existing row, even an empty one an operator left to keep the engine dormant, stays as it is.
    /// </summary>
    private async Task EnsureStartedOnAsync(CancellationToken cancellationToken)
    {
        var exists = await _context.SystemSettings
            .AnyAsync(row => row.Key == SystemSettingKeys.ProcessesStartedOn, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        var startedOn = _clock.Today.ToString(SystemSettingKeys.ProcessesStartedOnFormat, CultureInfo.InvariantCulture);

        _context.SystemSettings.Add(Ensure(SystemSetting.CreateServerValue(SystemSettingKeys.ProcessesStartedOn, startedOn)));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogStartedOn(startedOn);
    }

    /// <summary>The domain rejecting a checked definition is a programming error, never swallowed.</summary>
    private static void Ensure(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Process template could not be seeded: {result.Error.Code}");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        Ensure((Result)result);
        return result.Value;
    }

    /// <summary>The validator already limited the value to <see cref="ProcessDefinition.SubjectKinds"/>.</summary>
    private static ProcessSubjectKind SubjectKindOf(string subjectKind) => subjectKind switch
    {
        "vacancyRequest" => ProcessSubjectKind.VacancyRequest,
        "candidate" => ProcessSubjectKind.Candidate,
        "employment" => ProcessSubjectKind.Employment,
        _ => throw new InvalidOperationException($"Unknown process subject kind: {subjectKind}."),
    };

    /// <summary><c>1.10</c> &gt; <c>1.9</c>: compared as numbers.</summary>
    private static bool IsOlder(string candidate, string published)
        => Version.TryParse(candidate, out var a) && Version.TryParse(published, out var b) && a < b;

    /// <summary>Canonical JSON: object keys in ordinal order, no whitespace.</summary>
    public static string Canonicalize(string json)
        => Sort(JsonNode.Parse(json))?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null";

    /// <summary>Lower-case hex SHA-256 (<c>definition_sha256</c>).</summary>
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

    private static List<ProcessTemplateFile> ReadEmbeddedFiles()
    {
        var files = new List<ProcessTemplateFile>();

        foreach (var name in ResourceAssembly.GetManifestResourceNames())
        {
            var marker = name.IndexOf(ResourceMarker, StringComparison.Ordinal);

            if (marker < 0 || !name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = ResourceAssembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Resource not readable: {name}");
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var code = name[(marker + ResourceMarker.Length)..^".json".Length];
            files.Add(new ProcessTemplateFile(code, reader.ReadToEnd()));
        }

        return files;
    }
}
