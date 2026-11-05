namespace Bonvi.Application.Processes.Calendar;

/// <summary>
/// Builds the working-day calendar from <c>process_holidays</c> (SPEC-mening-ishlarim §6.10).
/// </summary>
/// <remarks>
/// Every call reads the table: a caller loads once per unit of work (one reconcile pass, one request) and
/// keeps the returned calendar for its length, so a long pass sees one consistent set of days and an HR edit
/// counts from the next pass.
/// </remarks>
public interface IWorkingDayCalendarSource
{
    Task<IWorkingDayCalendar> LoadAsync(CancellationToken cancellationToken = default);
}
