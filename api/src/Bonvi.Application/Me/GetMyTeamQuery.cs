using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Boshliqning qamrovi — <c>GET /api/me/team</c>.</summary>
public sealed record GetMyTeamQuery;

/// <summary>
/// <see cref="GetMyTeamQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Ilova bo'lim tanlagichini shundan quradi. Ro'yxat SERVER hisoblagan qamrovning o'zi
/// (<see cref="TeamScope"/>): mijoz bu ro'yxatdan boshqa bo'limni yuborsa ham, panel uni
/// «jamoa.bolim_topilmadi» bilan rad etadi — ro'yxat qulaylik, himoya emas.
/// </remarks>
public sealed class GetMyTeamQueryHandler
{
    private readonly TeamAccess _access;

    public GetMyTeamQueryHandler(TeamAccess access) => _access = access;

    public async Task<Result<TeamScopeDto>> HandleAsync(
        GetMyTeamQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        IReadOnlyList<TeamDepartmentDto> departments =
        [
            .. caller.Value.Scope.Departments.Select(department => new TeamDepartmentDto(
                department.Id,
                department.Name,
                department.Path,
                department.Level,
                department.ParentId)),
        ];

        return new TeamScopeDto(departments);
    }
}
