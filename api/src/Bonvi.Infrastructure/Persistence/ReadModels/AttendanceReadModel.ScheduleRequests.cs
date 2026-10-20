using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>Jadval so'rovlari — o'qish (SPEC-verifikatsiya-jadval-sorovi §C3).</summary>
/// <remarks>
/// Alohida qism, lekin o'sha sinf: xodim kartasi, bugungi bo'lim/lavozim va HR nomini
/// yo'qlik so'rovlari bilan AYNAN bir xil yordamchilar beradi — ikki navbatda bitta odam
/// ikki xil ko'rinmasligi kerak.
/// </remarks>
public sealed partial class AttendanceReadModel : IScheduleChangeRequestReadModel
{
    /// <inheritdoc />
    public async Task<AttendancePage<ScheduleChangeRequestDto>> SearchScheduleChangeRequestsAsync(
        ScheduleChangeRequestFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.ScheduleChangeRequests.AsNoTracking();

        if (filter.EmployeeId is { } employeeId)
        {
            query = query.Where(request => request.EmployeeId == employeeId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(request => request.Status == status);
        }

        if (filter.Decided is { } decided)
        {
            query = decided
                ? query.Where(request => request.Status != AbsenceRequestStatus.Pending)
                : query.Where(request => request.Status == AbsenceRequestStatus.Pending);
        }

        // Davr — so'rov KUNLARI bo'yicha: birorta kuni oraliqqa tushsa kiradi.
        if (filter.From is { } from)
        {
            query = query.Where(request => request.Days.Any(day => day.Date >= from));
        }

        if (filter.To is { } to)
        {
            query = query.Where(request => request.Days.Any(day => day.Date <= to));
        }

        if (filter.Search is { } search)
        {
            var matching = EmployeeSearchQuery.Matching(_context, search).Select(employee => employee.Id);
            query = query.Where(request => matching.Contains(request.EmployeeId));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .Include(request => request.Days)
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AttendancePage<ScheduleChangeRequestDto>(
            await ToScheduleRequestDtosAsync(rows, cancellationToken).ConfigureAwait(false),
            total);
    }

    /// <inheritdoc />
    public async Task<ScheduleChangeRequestDto?> FindScheduleChangeRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ScheduleChangeRequests
            .AsNoTracking()
            .Include(item => item.Days)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (request is null)
        {
            return null;
        }

        return (await ToScheduleRequestDtosAsync([request], cancellationToken).ConfigureAwait(false))[0];
    }

    private async Task<IReadOnlyList<ScheduleChangeRequestDto>> ToScheduleRequestDtosAsync(
        List<ScheduleChangeRequest> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var employeeIds = rows.Select(row => row.EmployeeId).Distinct().ToList();

        var cards = await EmployeeCardsAsync(employeeIds, cancellationToken).ConfigureAwait(false);
        var employments = await EmploymentsAsync(employeeIds, cancellationToken).ConfigureAwait(false);

        var today = _clock.Today;

        var primaries = employeeIds.ToDictionary(
            id => id,
            id => PrimaryEmployment.Select(employments.GetValueOrDefault(id) ?? [], today));

        var names = await CatalogNamesAsync(
                primaries.Values.Select(employment => employment?.DepartmentId),
                primaries.Values.Select(employment => employment?.PositionId),
                [],
                cancellationToken)
            .ConfigureAwait(false);

        var deciderIds = rows
            .Where(row => row.DecidedBy is not null)
            .Select(row => row.DecidedBy!.Value)
            .Distinct()
            .ToList();

        var deciders = deciderIds.Count == 0
            ? []
            : await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => deciderIds.Contains(user.Id))
                .Select(user => new { user.Id, Name = user.FullName.Full })
                .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken)
                .ConfigureAwait(false);

        return
        [
            .. rows.Select(row =>
            {
                var employment = primaries.GetValueOrDefault(row.EmployeeId);
                var card = cards.GetValueOrDefault(row.EmployeeId);

                return new ScheduleChangeRequestDto(
                    row.Id,
                    row.EmployeeId,
                    card?.FullName ?? string.Empty,
                    card?.PhotoKey,
                    names.Position(employment?.PositionId),
                    names.Department(employment?.DepartmentId),
                    row.Kind,
                    [
                        .. row.Days
                            .OrderBy(day => day.Date)
                            .Select(day => new ScheduleChangeDayDto(
                                day.Date,
                                day.IsWorking,
                                day.StartTime,
                                day.EndTime,
                                day.BreakStart,
                                day.BreakEnd,
                                day.SwapWith,
                                day.BreakMinutes,
                                day.IsFlexible,
                                day.RequiredMinutes)),
                    ],
                    row.Note,
                    row.Status,
                    row.DecisionNote,
                    row.DecidedBy is { } decidedBy ? deciders.GetValueOrDefault(decidedBy) : null,
                    row.DecidedAt,
                    row.CreatedAt,
                    row.SubmittedByAccountId is not null,
                    row.CancelledByAccountId is not null,
                    row.Version,
                    row.EditedAt,
                    row.EditedByAccountId is not null);
            }),
        ];
    }
}
