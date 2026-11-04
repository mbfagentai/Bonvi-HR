using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>
/// Process template — one row per process code (<c>hire-request</c>), docs/SPEC-mening-ishlarim.md §6.1.
/// </summary>
/// <remarks>
/// The template row carries no steps: they live in the version (<see cref="ProcessTemplateVersion"/>).
/// A changed definition is a new version and running cases keep the version they started with.
/// Templates are system logic seeded from embedded JSON (<c>ProcessTemplateSeeder</c>), never deleted.
/// </remarks>
public sealed class ProcessTemplate : AggregateRoot, IAuditable
{
    /// <summary>Process code length (<c>hire-formalise</c>).</summary>
    public const int MaxCodeLength = 32;

    /// <summary>Title length.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Regulation reference length (<c>HR-R01 §2.4 + HR-R06 §4</c>).</summary>
    public const int MaxSourceLength = 100;

    private ProcessTemplate(Guid id, string code, string title, string source, ProcessSubjectKind subjectKind)
        : base(id)
    {
        Code = code;
        Title = title;
        Source = source;
        SubjectKind = subjectKind;
        IsActive = true;
    }

    private ProcessTemplate() { }

    /// <summary>Process code (§3.2) — unique, immutable: cases and subject refs depend on it.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Title shown in the UI.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The regulation the process implements (<c>HR-R02</c>).</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>What a case of this process is about — immutable.</summary>
    public ProcessSubjectKind SubjectKind { get; private set; }

    /// <summary>An inactive template starts no new cases; running ones continue.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>New template (seeder).</summary>
    public static Result<ProcessTemplate> Create(string? code, string? title, string? source, ProcessSubjectKind subjectKind)
    {
        var cleanCode = Clean(code);
        if (cleanCode is null || cleanCode.Length > MaxCodeLength)
        {
            return ProcessErrors.Invalid;
        }

        var cleanTitle = Clean(title);
        if (cleanTitle is null || cleanTitle.Length > MaxTitleLength)
        {
            return ProcessErrors.Invalid;
        }

        var cleanSource = Clean(source) ?? string.Empty;
        if (cleanSource.Length > MaxSourceLength)
        {
            return ProcessErrors.Invalid;
        }

        if (!Enum.IsDefined(subjectKind))
        {
            return ProcessErrors.Invalid;
        }

        return new ProcessTemplate(NewId(), cleanCode, cleanTitle, cleanSource, subjectKind);
    }

    /// <summary>Updates title and source (comes with a new version).</summary>
    public Result Update(string? title, string? source)
    {
        var cleanTitle = Clean(title);
        if (cleanTitle is null || cleanTitle.Length > MaxTitleLength)
        {
            return ProcessErrors.Invalid;
        }

        var cleanSource = Clean(source) ?? string.Empty;
        if (cleanSource.Length > MaxSourceLength)
        {
            return ProcessErrors.Invalid;
        }

        Title = cleanTitle;
        Source = cleanSource;
        return Result.Success();
    }

    /// <summary>Allows new cases.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Stops new cases; running ones continue.</summary>
    public void Deactivate() => IsActive = false;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
