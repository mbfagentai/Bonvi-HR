using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Calendar;

/// <inheritdoc />
public sealed class HolidayWorkingDayCalendarSource : IWorkingDayCalendarSource
{
    private readonly IProcessHolidayRepository _holidays;

    public HolidayWorkingDayCalendarSource(IProcessHolidayRepository holidays) => _holidays = holidays;

    /// <inheritdoc />
    public async Task<IWorkingDayCalendar> LoadAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _holidays.ListAllAsync(cancellationToken).ConfigureAwait(false);

        return new WorkingDayCalendar(rows.Select(row => new ProcessCalendarDay(row.Date, KindOf(row.Kind))));
    }

    private static ProcessCalendarDayKind KindOf(ProcessHolidayKind kind) => kind switch
    {
        ProcessHolidayKind.Holiday => ProcessCalendarDayKind.Holiday,
        ProcessHolidayKind.WorkingDay => ProcessCalendarDayKind.WorkingDay,
        _ => throw new InvalidOperationException($"Unknown process_holidays.kind: {kind}."),
    };
}
