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

        if (await EmployeesInScopeAsync(cancellationToken).ConfigureAwait(false) is { } inScope)
        {
            query = query.Where(request => inScope.Contains(request.EmployeeId));
        }

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

        // Davr — so'rov KUNLARI bo'yicha: birorta kuni oraliqqa tushsa kiradi
        // (almashtirishda ikkala sana ham kun bo'lib saqlanadi).
        //
        // ⚠️ Ikkala chegara BITTA `Any` ichida. Ilgari ular ikki alohida `Any`
        // edi va 1- va 30-oktabr kunli so'rov 10–20-oktabr oralig'ida ham
        // chiqardi: 30 ≥ 10 va 1 ≤ 20 — har biri o'zicha rost, oraliqda esa
        // birorta ham kun yo'q (egasi, 2026-10-20).
        if (filter.From is { } from && filter.To is { } to)
        {
            query = query.Where(request => request.Days.Any(day => day.Date >= from && day.Date <= to));
        }
        else if (filter.From is { } onlyFrom)
        {
            query = query.Where(request => request.Days.Any(day => day.Date >= onlyFrom));
        }
        else if (filter.To is { } onlyTo)
        {
            query = query.Where(request => request.Days.Any(day => day.Date <= onlyTo));
        }

        if (filter.Search is { } search)
        {
            var matching = EmployeesMatching(search).Select(employee => employee.Id);
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
        var requests = _context.ScheduleChangeRequests.AsNoTracking();

        if (await EmployeesInScopeAsync(cancellationToken).ConfigureAwait(false) is { } inScope)
        {
            requests = requests.Where(item => inScope.Contains(item.EmployeeId));
        }

        var request = await requests
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
