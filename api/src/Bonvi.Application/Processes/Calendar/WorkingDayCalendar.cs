namespace Bonvi.Application.Processes.Calendar;

/// <summary>
/// Pure <see cref="IWorkingDayCalendar"/> over a fixed list of exception days. <see cref="IWorkingDayCalendarSource"/>
/// loads the <c>process_holidays</c> rows and builds one of these.
/// </summary>
/// <remarks>
/// A Holiday row on a weekend and a WorkingDay row on a weekday change nothing. When one date has both kinds
/// the last row wins — the table has <c>date</c> as its key, so this only matters for hand-built lists.
/// </remarks>
public sealed class WorkingDayCalendar : IWorkingDayCalendar
{
    /// <summary>
    /// Walking further than this many calendar days for one call means a broken holiday list (e.g. every day
    /// marked Holiday) — fail loudly instead of spinning.
    /// </summary>
    private const int MaxCalendarDaysWalked = 3660;

    private readonly Dictionary<DateOnly, ProcessCalendarDayKind> exceptions;

    public WorkingDayCalendar(IEnumerable<ProcessCalendarDay> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        exceptions = [];

        foreach (var day in days)
        {
            exceptions[day.Date] = day.Kind;
        }
    }

    /// <summary>Plain Monday–Friday, no exceptions.</summary>
    public static WorkingDayCalendar MondayToFriday { get; } = new([]);

    /// <summary>ISO weekday (1 Monday … 7 Sunday, setting <c>processes.councilWeekday</c>) → <see cref="DayOfWeek"/>.</summary>
    public static DayOfWeek FromIsoWeekday(int isoWeekday)
        => isoWeekday is >= 1 and <= 7
            ? (DayOfWeek)(isoWeekday % 7)
            : throw new ArgumentOutOfRangeException(nameof(isoWeekday), isoWeekday, "ISO weekday is 1 (Monday) … 7 (Sunday).");

    public bool IsWorkingDay(DateOnly date)
        => exceptions.TryGetValue(date, out var kind)
            ? kind == ProcessCalendarDayKind.WorkingDay
            : date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public DateOnly AddWorkingDays(DateOnly from, int workingDays)
    {
        var step = Math.Sign(workingDays);
        var remaining = Math.Abs(workingDays);
        var date = from;
        var walked = 0;

        while (remaining > 0)
        {
            date = date.AddDays(step);
            Guard(ref walked, from);

            if (IsWorkingDay(date))
            {
                remaining--;
            }
        }

        return date;
    }

    public DateOnly NextCouncilDay(DateOnly anchor, DayOfWeek councilDay)
    {
        var daysAhead = ((int)councilDay - (int)anchor.DayOfWeek + 7) % 7;
        var date = anchor.AddDays(daysAhead == 0 ? 7 : daysAhead);
        var walked = 0;

        // Council day on a holiday → the next working day (not the next council weekday)
        while (!IsWorkingDay(date))
        {
            date = date.AddDays(1);
            Guard(ref walked, anchor);
        }

        return date;
    }

    private static void Guard(ref int walked, DateOnly from)
    {
        if (++walked > MaxCalendarDaysWalked)
        {
            throw new InvalidOperationException(
                $"No working day within {MaxCalendarDaysWalked} days of {from:yyyy-MM-dd} — the holiday list is broken.");
        }
    }
}
