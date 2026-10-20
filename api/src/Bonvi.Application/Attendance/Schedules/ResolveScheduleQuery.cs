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
                MatchedDepartmentId: null, MatchedDepartmentName: null, Day: null, DaySource: null);
        }

        var catalog = await _source
            .LoadScheduleCatalogAsync(query.Date, query.Date, cancellationToken)
            .ConfigureAwait(false);

        var resolver = new ScheduleResolver(
            catalog.Assignments,
            catalog.Schedules,
            new DepartmentTree(catalog.Departments),
            catalog.Dates,
            catalog.Overrides);
        var resolution = resolver.Resolve(primary, query.Date);

        var matchedDepartmentId = resolution?.Tier == ScheduleScope.Department ? resolution.Assignment.DepartmentId : null;

        // Pog'onalarning kirishi — SHU SANADAGI bo'lim va lavozim: hal qiluvchi
        // aynan shular bilan qidirgan (ko'chirishdan oldingi sana — eski joy).
        var placement = EmploymentPlacement.On(primary, query.Date);

        var legalEntityName = await _names.LegalEntityAsync(primary.LegalEntityId, cancellationToken).ConfigureAwait(false);
        var departmentName = await _names.DepartmentAsync(placement.DepartmentId, cancellationToken).ConfigureAwait(false);
        var positionName = await _names.PositionAsync(placement.PositionId, cancellationToken).ConfigureAwait(false);
        var matchedDepartmentName = await _names.DepartmentAsync(matchedDepartmentId, cancellationToken).ConfigureAwait(false);

        var (day, daySource) = ResolvedDay(resolution, query.Date);

        return new ScheduleResolutionDto(
            employee.Id,
            employee.FullName.Full,
            query.Date,
            primary.Id,
            primary.LegalEntityId,
            legalEntityName,
            placement.DepartmentId,
            departmentName,
            placement.PositionId,
            positionName,
            resolution?.Tier,
            resolution?.Assignment.Id,
            resolution?.Assignment.EffectiveFrom,
            resolution?.Assignment.EffectiveTo,
            resolution?.Schedule.Id,
            resolution?.Schedule.Name,
            matchedDepartmentId,
            matchedDepartmentName,
            day,
            daySource);
    }

    /// <summary>Shu SANANING kuni — tabel hisoblagan manbadan.</summary>
    /// <remarks>
    /// ⚠️ Yillik to'rning katagi haftalik naqshdan USTUN (SPEC-jadval-to'ri §B2) —
    /// hisoblash ham shunday o'qiydi (<see cref="ScheduleResolution.DateOverride"/>).
    /// Ilgari javob faqat hafta kunining naqshini berardi: bayram kuni dam olishga
    /// aylantirilgan odamda ekran «09:00–18:00» der, tabel esa «Dam olish» ko'rsatardi.
    /// </remarks>
    private static (WorkScheduleDayDto? Day, ScheduleDaySource? Source) ResolvedDay(
        ScheduleResolution? resolution,
        DateOnly date)
    {
        if (resolution is null)
        {
            return (null, null);
        }

        if (resolution.RequestOverride is { } requested)
        {
            return (
                new WorkScheduleDayDto(
                    date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek,
                    requested.IsWorking,
                    requested.StartTime,
                    requested.EndTime,
                    requested.BreakMinutes,
                    requested.BreakStart,
                    requested.BreakEnd,
                    requested.BreakStart is { } breakStart && requested.BreakEnd is { } breakEnd && breakEnd < breakStart,
                    requested.IsWorking && requested.IsFlexible,
                    requested.IsWorking && requested.IsFlexible ? requested.RequiredMinutes : null),
                ScheduleDaySource.Request);
        }

        if (resolution.DateOverride is { } exact)
        {
            return (WorkScheduleMapper.ToDto(exact), ScheduleDaySource.Date);
        }

        var isoWeekday = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        var day = resolution.Schedule.Days.FirstOrDefault(item => item.DayIndex == isoWeekday);

        return day is null ? (null, null) : (WorkScheduleMapper.ToDto(day), ScheduleDaySource.Pattern);
    }
}
