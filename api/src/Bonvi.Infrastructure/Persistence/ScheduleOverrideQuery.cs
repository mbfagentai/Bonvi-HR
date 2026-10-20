using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// «Qaysi xodim-sanalarning rejasi tasdiqlangan jadval so'rovi bilan almashgan» — YAGONA nusxa
/// (SPEC-verifikatsiya-jadval-sorovi §C2).
/// </summary>
/// <remarks>
/// Ikki yuklovchi o'qiydi: qayta hisoblash katalogi (<c>AttendanceComputationSource</c>) va o'qish
/// modelining hal qiluvchisi (<c>AttendanceReadModel</c>) — <see cref="AbsenceQuery"/> bilan bir xil
/// sabab: ikki nusxa ajralib ketsa, saqlangan kun va panel bir kunni ikki xil rejada o'qirdi.
/// </remarks>
internal static class ScheduleOverrideQuery
{
    /// <summary>[from, to] dagi tasdiqlangan kunlar.</summary>
    /// <param name="context">Ma'lumotlar bazasi konteksti.</param>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public static async Task<List<EmployeeDayOverride>> ApprovedAsync(
        AppDbContext context,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await context.ScheduleChangeRequests
            .AsNoTracking()
            .Where(request => request.Status == AbsenceRequestStatus.Approved)
            .SelectMany(
                request => request.Days.Where(day => day.Date >= from && day.Date <= to),
                (request, day) => new EmployeeDayOverride(
                    request.EmployeeId,
                    day.Date,
                    day.IsWorking,
                    day.StartTime,
                    day.EndTime,
                    day.BreakStart,
                    day.BreakEnd,
                    day.BreakMinutes,
                    request.Id,
                    request.DecidedAt,
                    day.IsFlexible,
                    day.RequiredMinutes))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
