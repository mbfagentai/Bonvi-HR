using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>
/// A working-day calendar exception (<c>process_holidays</c>), docs/SPEC-mening-ishlarim.md §6.10:
/// a holiday on a weekday, or a working Saturday.
/// </summary>
/// <remarks>
/// Working day = Mon–Fri that is not a <see cref="ProcessHolidayKind.Holiday"/>, plus
/// <see cref="ProcessHolidayKind.WorkingDay"/> rows. The SPEC's natural key is <see cref="Date"/>; the
/// row still has a <see cref="Entity.Id"/> because every audited entity is keyed by one Guid
/// (<c>AuditCatalogCoverageTests</c>) — <c>date</c> is UNIQUE.
/// </remarks>
public sealed class ProcessHoliday : AggregateRoot
{
    /// <summary>Title length.</summary>
    public const int MaxTitleLength = 200;

    private ProcessHoliday(Guid id, DateOnly date, ProcessHolidayKind kind, string title)
        : base(id)
    {
        Date = date;
        Kind = kind;
        Title = title;
    }

    private ProcessHoliday() { }

    /// <summary>The calendar date — unique.</summary>
    public DateOnly Date { get; private set; }

    public ProcessHolidayKind Kind { get; private set; }

    /// <summary>«Mustaqillik kuni», «Ish kuni (ko'chirilgan)».</summary>
    public string Title { get; private set; } = string.Empty;

    public static Result<ProcessHoliday> Create(DateOnly date, ProcessHolidayKind kind, string? title)
    {
        var cleanTitle = Clean(title);
        if (!Enum.IsDefined(kind) || cleanTitle is null || cleanTitle.Length > MaxTitleLength)
        {
            return ProcessErrors.Invalid;
        }

        return new ProcessHoliday(NewId(), date, kind, cleanTitle);
    }

    /// <summary>Changes kind and title; the date is the identity and does not change.</summary>
    public Result Update(ProcessHolidayKind kind, string? title)
    {
        var cleanTitle = Clean(title);
        if (!Enum.IsDefined(kind) || cleanTitle is null || cleanTitle.Length > MaxTitleLength)
        {
            return ProcessErrors.Invalid;
        }

        Kind = kind;
        Title = cleanTitle;
        return Result.Success();
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
