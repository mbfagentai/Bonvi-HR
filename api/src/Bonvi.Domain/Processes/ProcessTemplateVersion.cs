using System.Text;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>
/// Process template version — the JSON definition of the steps (§3.1), docs/SPEC-mening-ishlarim.md §6.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>A published version never changes.</b> A case keeps the version it started with for life;
/// there is deliberately no method that edits the definition — a change is a new version.
/// </para>
/// <para>
/// At most one <see cref="ProcessTemplateVersionStatus.Published"/> version per template (partial
/// unique index). The seeder saves the <see cref="Retire"/> of the old one FIRST, then
/// <see cref="Publish"/>es the new one — PostgreSQL checks the unique index per statement and EF does
/// not guarantee statement order inside one save (same rule as blank template versions).
/// </para>
/// </remarks>
public sealed class ProcessTemplateVersion : AggregateRoot, IAuditable
{
    /// <summary>Version label length (<c>1.0</c>).</summary>
    public const int MaxVersionLength = 8;

    /// <summary>Largest definition (UTF-8 bytes) — 256 KB.</summary>
    public const int MaxDefinitionBytes = 256 * 1024;

    /// <summary>Hex length of a SHA-256.</summary>
    public const int Sha256HexLength = 64;

    private ProcessTemplateVersion(Guid id, Guid templateId, string version, string definition, string definitionSha256)
        : base(id)
    {
        TemplateId = templateId;
        Version = version;
        Definition = definition;
        DefinitionSha256 = definitionSha256;
        Status = ProcessTemplateVersionStatus.Draft;
    }

    private ProcessTemplateVersion() { }

    public Guid TemplateId { get; private set; }

    /// <summary>Version label (<c>1.0</c>) — unique within the template.</summary>
    public string Version { get; private set; } = string.Empty;

    public ProcessTemplateVersionStatus Status { get; private set; }

    /// <summary>The definition JSON (<c>jsonb</c>), §3.1. Validated by the Application layer.</summary>
    public string Definition { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the canonical JSON (lower-case hex) — how the seeder detects a change.</summary>
    public string DefinitionSha256 { get; private set; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>New version in Draft.</summary>
    public static Result<ProcessTemplateVersion> Create(
        Guid templateId,
        string? version,
        string? definition,
        string? definitionSha256)
    {
        if (templateId == Guid.Empty)
        {
            return ProcessErrors.NotFound;
        }

        var cleanVersion = version?.Trim();
        if (string.IsNullOrEmpty(cleanVersion) || cleanVersion.Length > MaxVersionLength)
        {
            return ProcessErrors.Invalid;
        }

        if (string.IsNullOrWhiteSpace(definition) || Encoding.UTF8.GetByteCount(definition) > MaxDefinitionBytes)
        {
            return ProcessErrors.Invalid;
        }

        if (definitionSha256 is not { Length: Sha256HexLength } || !definitionSha256.All(char.IsAsciiHexDigitLower))
        {
            return ProcessErrors.Invalid;
        }

        return new ProcessTemplateVersion(NewId(), templateId, cleanVersion, definition, definitionSha256);
    }

    /// <summary>Publishes the version — only from Draft.</summary>
    public Result Publish(DateTimeOffset at)
    {
        if (Status != ProcessTemplateVersionStatus.Draft)
        {
            return ProcessErrors.InvalidStatus;
        }

        Status = ProcessTemplateVersionStatus.Published;
        PublishedAt = at;
        return Result.Success();
    }

    /// <summary>Retires the version — only from Published (a newer one is coming).</summary>
    public Result Retire()
    {
        if (Status != ProcessTemplateVersionStatus.Published)
        {
            return ProcessErrors.InvalidStatus;
        }

        Status = ProcessTemplateVersionStatus.Retired;
        return Result.Success();
    }
}
