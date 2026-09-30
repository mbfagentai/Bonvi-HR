using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Recompute;

/// <summary>Davrni qo'lda qayta hisoblash (<c>POST /recompute</c>, SPEC §6.7).</summary>
/// <param name="From">Davr boshi (o'zi ham kiradi).</param>
/// <param name="To">Davr oxiri (o'zi ham kiradi).</param>
/// <param name="EmployeeId">Faqat shu xodim; <c>null</c> — davrga tegishli hamma.</param>
public sealed record RecomputeAttendanceCommand(DateOnly From, DateOnly To, Guid? EmployeeId);

/// <summary><see cref="RecomputeAttendanceCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// Bu — «to'rtta qonuniy tuzatish»ning ikkinchisi (SPEC §5.8): jadval yoki
/// biriktirma tuzatilgach, HR aniq davrni qayta hisoblaydi. Kunni qo'lda
/// tahrirlash yo'li YO'Q (§1.2 #5).
/// </para>
/// <para>
/// Davr chegarasi hisobotlar bilan bir xil — <see cref="Bonvi.Domain.Attendance.AttendanceRules.MaxRecomputeDays"/>
/// (bir chorak), xuddi o'sha kodlar bilan (<c>davomat.davr_uzun</c>,
/// <c>davomat.davr_notogri</c>).
/// </para>
/// <para>
/// Javobdagi <c>employeeCount</c> shu lahzadagi baho: vazifa navbatda
/// kutganda ro'yxatni qaytadan aniqlaydi. Sinxron navbatda (sinov muhiti)
/// ikkalasi bir xil.
/// </para>
/// </remarks>
public sealed class RecomputeAttendanceCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceComputationSource _source;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IClock _clock;

    public RecomputeAttendanceCommandHandler(
        IEmployeeRepository employees,
        IAttendanceComputationSource source,
        IBackgroundJobQueue jobs,
        IClock clock)
    {
        _employees = employees;
        _source = source;
        _jobs = jobs;
        _clock = clock;
    }

    public async Task<Result<AttendanceRecomputeAcceptedDto>> HandleAsync(
        RecomputeAttendanceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var period = AttendanceQueryParsing.Period(command.From, command.To, _clock.Today, PeriodDefault.SingleDay);
        if (period.IsFailure)
        {
            return period.Error;
        }

        var (from, to) = period.Value;

        int employeeCount;

        if (command.EmployeeId is { } employeeId)
        {
            if (!await _employees.ExistsAsync(employeeId, cancellationToken).ConfigureAwait(false))
            {
                return AttendanceErrors.EmployeeNotFound;
            }

            employeeCount = 1;
        }
        else
        {
            var employees = await _source.ListEmployeesAsync(from, to, cancellationToken).ConfigureAwait(false);
            employeeCount = employees.Count;
        }

        var jobId = await _jobs
            .EnqueueAsync<AttendanceRecomputeRangeJob>(job => job.RunAsync(
                from,
                to,
                command.EmployeeId,
                CancellationToken.None))
            .ConfigureAwait(false);

        return new AttendanceRecomputeAcceptedDto(jobId, employeeCount, to.DayNumber - from.DayNumber + 1);
    }
}
