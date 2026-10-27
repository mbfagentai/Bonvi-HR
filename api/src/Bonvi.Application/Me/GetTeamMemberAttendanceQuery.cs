using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Me;

/// <summary>Jamoa a'zosining bir oylik davomati.</summary>
/// <param name="EmployeeId">A'zo.</param>
/// <param name="Year">Yil.</param>
/// <param name="Month">Oy (1–12).</param>
public sealed record GetTeamMemberAttendanceQuery(Guid EmployeeId, int Year, int Month);

/// <summary>
/// <see cref="GetTeamMemberAttendanceQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Oy — <c>/api/me/attendance</c> ning O'ZI</b>
/// (<see cref="GetMyAttendanceQueryHandler.MonthAsync"/>), xodim identifikatori bilan: a'zoning
/// oyi uning o'z ilovasidagidek ko'rinadi (SPEC-jamoam §C3). Turniket suratlari bu yerda ham yo'q,
/// «hozir» (<c>current</c>) ham yo'q (<see cref="TeamMemberMonthDto"/> izohi).
/// </para>
/// <para>
/// <b>Tekshiruv tartibi:</b> avval «boshliqmi» (403), keyin oy (400), keyin a'zo (404).
/// Boshliq bo'lmagan odam oy qoidalaridan ham hech narsa bilib olmasin.
/// </para>
/// <para>
/// ⚠️ <b>Qamrov — HAR KUNGA alohida</b> (<see cref="TeamScope.DaysCovered"/>): odam o'sha kuni
/// qaysi bo'limda bo'lgan bo'lsa, o'sha bo'yicha. Bugungacha bitta ham qamrovdagi kuni yo'q oy —
/// «jamoa.xodim_topilmadi», xuddi umuman yo'q odamdagidek. Qamrovdan tashqaridagi kunlar
/// «ishlamagan» kun bo'lib qaytadi va jamiga kirmaydi: jamoaga 15-sanada kelgan odamning 1–14
/// kunlari yangi boshliqqa ko'rinmaydi, ketgan odamning esa eski boshlig'iga faqat o'zida
/// ishlagan kunlari ko'rinadi.
/// </para>
/// <para>
/// Telefon va rasm — faqat BUGUN qamrovdagi a'zoda: ketgan odamning eski oyini ko'rish uning
/// bugungi raqamini olish huquqi emas.
/// </para>
/// </remarks>
public sealed class GetTeamMemberAttendanceQueryHandler
{
    private readonly TeamAccess _access;
    private readonly IAttendanceComputationSource _employments;
    private readonly IEmployeeAppReadModel _profiles;
    private readonly ITeamReadModel _team;
    private readonly GetMyAttendanceQueryHandler _month;
    private readonly TeamViewAudit _audit;
    private readonly IClock _clock;

    public GetTeamMemberAttendanceQueryHandler(
        TeamAccess access,
        IAttendanceComputationSource employments,
        IEmployeeAppReadModel profiles,
        ITeamReadModel team,
        GetMyAttendanceQueryHandler month,
        TeamViewAudit audit,
        IClock clock)
    {
        _access = access;
        _employments = employments;
        _profiles = profiles;
        _team = team;
        _month = month;
        _audit = audit;
        _clock = clock;
    }

    public async Task<Result<TeamMemberAttendanceDto>> HandleAsync(
        GetTeamMemberAttendanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var valid = AttendanceQueryParsing.Month(query.Year, query.Month);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        var today = _clock.Today;
        var monthStart = new DateOnly(query.Year, query.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // Kelgusi oyda hech narsa yo'q — bo'sh oy qaytarish esa «bu odam keyingi oy ham
        // jamoada» degan ko'chirish rejasini aytib qo'yardi.
        if (monthStart > today)
        {
            return TeamErrors.FutureMonth;
        }

        var scope = caller.Value.Scope;
        var loaded = await _employments
            .LoadEmploymentsAsync([query.EmployeeId], cancellationToken)
            .ConfigureAwait(false);
        var employments = loaded.GetValueOrDefault(query.EmployeeId) ?? [];

        var covered = scope.DaysCovered(employments, monthStart, monthEnd);
        var pastCovered = covered.Where(date => date <= today).ToList();

        if (pastCovered.Count == 0)
        {
            return TeamErrors.MemberNotFound;
        }

        var profile = await _profiles.ProfileAsync(query.EmployeeId, today, cancellationToken).ConfigureAwait(false);

        if (profile is null)
        {
            // Xodim yumshoq o'chirilgan — munosabatlari qolgan bo'lsa ham, panelda u yo'q.
            return TeamErrors.MemberNotFound;
        }

        // Sarlavhadagi bo'lim va lavozim — oyning qamrovdagi OXIRGI kunidagi joy. Profilning
        // o'zi joriy maydonlardan o'qiydi va ketgan odamning yangi bo'limini aytib qo'yardi.
        var headerDay = pastCovered.Max();
        var placement = PrimaryEmployment.Select(employments, headerDay) is { } primary
            ? EmploymentPlacement.On(primary, headerDay)
            : default;
        var names = await _team
            .PlacementNamesAsync(placement.DepartmentId, placement.PositionId, cancellationToken)
            .ConfigureAwait(false);

        var inScopeToday = scope.CoversOn(employments, today);
        var phones = inScopeToday
            ? await _team.PhonesAsync([query.EmployeeId], cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, PhoneNumber>();

        var (days, _) = await _month
            .MonthAsync(query.EmployeeId, query.Year, query.Month, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MyAttendanceDayDto> visible =
        [
            .. days.Select(day => covered.Contains(day.Day.Date) ? day : OutsideScope(day.Day.Date)),
        ];

        await _audit
            .MemberViewedAsync(caller.Value.Ids, query.EmployeeId, query.Year, query.Month)
            .ConfigureAwait(false);

        return new TeamMemberAttendanceDto(
            new TeamMemberDto(
                query.EmployeeId,
                profile.FullName,
                inScopeToday && profile.PhotoKey is not null,
                names.Position,
                names.Department,
                phones.TryGetValue(query.EmployeeId, out var phone) ? phone.Formatted : null),
            new TeamMemberMonthDto(
                query.Year,
                query.Month,
                visible,
                TimesheetTotalsDto.Of([.. visible.Select(day => day.Day)])));
    }

    /// <summary>
    /// Qamrovdan tashqaridagi kun — tabel munosabatsiz kunni qanday hosil qilsa, aynan shunday:
    /// <c>NotEmployed</c>, sonlar 0, vaqtlar va reja yo'q, belgilar yo'q.
    /// </summary>
    private static MyAttendanceDayDto OutsideScope(DateOnly date)
        => new(
            new TimesheetCellDto(
                date,
                AttendanceDayStatus.NotEmployed,
                WorkedMinutes: 0,
                LateMinutes: 0,
                EarlyLeaveMinutes: 0,
                FirstInAt: null,
                LastOutAt: null,
                BreakTakenMinutes: 0,
                BreakDeductionMinutes: 0,
                PlanStartAt: null,
                PlanEndAt: null),
            []);
}
