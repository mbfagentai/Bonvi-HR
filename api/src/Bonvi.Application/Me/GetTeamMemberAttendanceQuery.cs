using Bonvi.Application.Attendance.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

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
/// (<see cref="GetMyAttendanceQueryHandler.BuildAsync"/>), xodim identifikatori bilan: a'zoning
/// oyi uning o'z ilovasidagidek ko'rinadi (SPEC-jamoam §C3). Turniket suratlari bu yerda ham yo'q.
/// </para>
/// <para>
/// <b>Tekshiruv tartibi:</b> avval «boshliqmi» (403), keyin oy (400), keyin a'zo (404).
/// Boshliq bo'lmagan odam oy qoidalaridan ham hech narsa bilib olmasin.
/// </para>
/// <para>
/// ⚠️ A'zo — shu OYGA tegadigan, bo'limi qamrovda bo'lgan munosabati bor odam
/// (<see cref="TeamScope.Covers"/>). Bo'lmasa — «jamoa.xodim_topilmadi», xuddi umuman
/// yo'q odamdagidek.
/// </para>
/// </remarks>
public sealed class GetTeamMemberAttendanceQueryHandler
{
    private readonly TeamAccess _access;
    private readonly IEmploymentRepository _employments;
    private readonly IEmployeeAppReadModel _profiles;
    private readonly ITeamReadModel _team;
    private readonly GetMyAttendanceQueryHandler _month;
    private readonly TeamViewAudit _audit;
    private readonly IClock _clock;

    public GetTeamMemberAttendanceQueryHandler(
        TeamAccess access,
        IEmploymentRepository employments,
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

        var monthStart = new DateOnly(query.Year, query.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var employments = await _employments
            .GetByEmployeeAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (!caller.Value.Scope.Covers(employments, monthStart, monthEnd))
        {
            return TeamErrors.MemberNotFound;
        }

        // Lavozim va bo'lim — bugungi; o'tgan oy so'ralsa, o'sha oyning oxirgi kunidagi:
        // oy ekranining sarlavhasi o'sha oyda u qayerda ishlaganini aytadi.
        var today = _clock.Today;
        var profile = await _profiles
            .ProfileAsync(query.EmployeeId, today < monthEnd ? today : monthEnd, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            // Xodim yumshoq o'chirilgan — munosabatlari qolgan bo'lsa ham, panelda u yo'q.
            return TeamErrors.MemberNotFound;
        }

        var phones = await _team.PhonesAsync([query.EmployeeId], cancellationToken).ConfigureAwait(false);

        var attendance = await _month
            .BuildAsync(query.EmployeeId, query.Year, query.Month, cancellationToken)
            .ConfigureAwait(false);

        await _audit
            .MemberViewedAsync(caller.Value.Ids, query.EmployeeId, query.Year, query.Month)
            .ConfigureAwait(false);

        return new TeamMemberAttendanceDto(
            new TeamMemberDto(
                query.EmployeeId,
                profile.FullName,
                profile.PhotoKey is not null,
                profile.Position,
                profile.Department,
                phones.TryGetValue(query.EmployeeId, out var phone) ? phone.Formatted : null),
            attendance);
    }
}
