using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Schedules;

/// <summary>«Nega bu odamda shu jadval?» (<c>GET /schedules/resolve</c>).</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Date">Sana.</param>
public sealed record ResolveScheduleQuery(Guid EmployeeId, DateOnly Date);

/// <summary><see cref="ResolveScheduleQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Hisoblash bilan AYNAN bir xil kod</b>: asosiy munosabat
/// <see cref="PrimaryEmployment.Select"/>, jadval <see cref="ScheduleResolver"/>,
/// katalog — qayta hisoblash yuklaydigan o'sha <see cref="IAttendanceComputationSource"/>.
/// Bu ekran «tabel nega shunday» deganda javob beradi; alohida qoida bilan
/// yozilsa, bir kun tabel bilan ajralib ketib, HR ni adashtirardi.
/// </para>
/// <para>
/// Bu savol qo'llab-quvvatlash so'rovini dasturchiga yetkazmaslik uchun bor:
/// yutgan pog'ona va (bo'lim pog'onasida) aynan qaysi OTA bo'lim yutgani
/// ko'rinib turadi.
/// </para>
/// </remarks>
public sealed class ResolveScheduleQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceComputationSource _source;
    private readonly CatalogNameResolver _names;

    public ResolveScheduleQueryHandler(
        IEmployeeRepository employees,
        IAttendanceComputationSource source,
        CatalogNameResolver names)
    {
        _employees = employees;
        _source = source;
        _names = names;
    }

    public async Task<Result<ScheduleResolutionDto>> HandleAsync(
        ResolveScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employee = await _employees.GetByIdAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var employments = await _source.LoadEmploymentsAsync([employee.Id], cancellationToken).ConfigureAwait(false);
        var primary = PrimaryEmployment.Select(employments.GetValueOrDefault(employee.Id) ?? [], query.Date);

        if (primary is null)
        {
            return new ScheduleResolutionDto(
                employee.Id, employee.FullName.Full, query.Date,
                EmploymentId: null, LegalEntityId: null, LegalEntityName: null,
                DepartmentId: null, DepartmentName: null, PositionId: null, PositionName: null,
                Scope: null, AssignmentId: null, EffectiveFrom: null, EffectiveTo: null,
                WorkScheduleId: null, WorkScheduleName: null,
                MatchedDepartmentId: null, MatchedDepartmentName: null, Day: null);
        }

        var catalog = await _source
            .LoadScheduleCatalogAsync(query.Date, query.Date, cancellationToken)
            .ConfigureAwait(false);

        var resolver = new ScheduleResolver(
            catalog.Assignments,
            catalog.Schedules,
            new DepartmentTree(catalog.Departments),
            catalog.Dates);
        var resolution = resolver.Resolve(primary, query.Date);

        var matchedDepartmentId = resolution?.Tier == ScheduleScope.Department ? resolution.Assignment.DepartmentId : null;

        var legalEntityName = await _names.LegalEntityAsync(primary.LegalEntityId, cancellationToken).ConfigureAwait(false);
        var departmentName = await _names.DepartmentAsync(primary.DepartmentId, cancellationToken).ConfigureAwait(false);
        var positionName = await _names.PositionAsync(primary.PositionId, cancellationToken).ConfigureAwait(false);
        var matchedDepartmentName = await _names.DepartmentAsync(matchedDepartmentId, cancellationToken).ConfigureAwait(false);

        var isoWeekday = query.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)query.Date.DayOfWeek;
        var day = resolution?.Schedule.Days.FirstOrDefault(item => item.DayIndex == isoWeekday);

        return new ScheduleResolutionDto(
            employee.Id,
            employee.FullName.Full,
            query.Date,
            primary.Id,
            primary.LegalEntityId,
            legalEntityName,
            primary.DepartmentId,
            departmentName,
            primary.PositionId,
            positionName,
            resolution?.Tier,
            resolution?.Assignment.Id,
            resolution?.Assignment.EffectiveFrom,
            resolution?.Assignment.EffectiveTo,
            resolution?.Schedule.Id,
            resolution?.Schedule.Name,
            matchedDepartmentId,
            matchedDepartmentName,
            day is null ? null : WorkScheduleMapper.ToDto(day));
    }
}
