namespace Bonvi.Application.Processes.Calendar;

/// <summary>
/// The process working-day calendar (SPEC-mening-ishlarim §6.10): Monday–Friday, minus Holiday rows, plus
/// WorkingDay rows (a working Saturday). The single implementation is <see cref="WorkingDayCalendar"/>; the
/// blanks engine keeps its own Mon–Fri <c>addWorkingDays</c> in Phase 1 (C10).
/// </summary>
public interface IWorkingDayCalendar
{
    bool IsWorkingDay(DateOnly date);

    /// <summary>
    /// Moves <paramref name="workingDays"/> working days from <paramref name="from"/> (the start day itself is
    /// not counted); negative moves back. 0 returns <paramref name="from"/> unchanged, even on a day off.
    /// </summary>
    DateOnly AddWorkingDays(DateOnly from, int workingDays);

    /// <summary>
    /// The first date strictly after <paramref name="anchor"/> that falls on <paramref name="councilDay"/>;
    /// when that date is not a working day, the next working day after it (§3.1 <c>nextCouncilDay</c>).
    /// </summary>
    DateOnly NextCouncilDay(DateOnly anchor, DayOfWeek councilDay);
}

/// <summary><c>process_holidays.kind</c>.</summary>
public enum ProcessCalendarDayKind
{
    /// <summary>A day off on a weekday.</summary>
    Holiday = 1,

    /// <summary>A working Saturday / Sunday (government transfer).</summary>
    WorkingDay = 2,
}

/// <summary>One <c>process_holidays</c> row as the calendar needs it.</summary>
public sealed record ProcessCalendarDay(DateOnly Date, ProcessCalendarDayKind Kind);
