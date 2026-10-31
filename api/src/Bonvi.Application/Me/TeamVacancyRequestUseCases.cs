using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.VacancyRequests;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/*
  «Jamoa» tabidan vakansiya so'rovi (egasi, 2026-10-31). Qamrov — boshliqlik (`TeamAccess`),
  so'rovchi — boshliqning o'zi. Mantiq `TeamVacancyRequests` da, bot yuzasi bilan umumiy.
*/

/// <summary><c>GET /api/me/team/vacancy-requests</c> — boshliq qamrovidagi so'rovlar.</summary>
public sealed class ListMyTeamVacancyRequestsQueryHandler
{
    private readonly TeamAccess _access;
    private readonly TeamVacancyRequests _requests;

    public ListMyTeamVacancyRequestsQueryHandler(TeamAccess access, TeamVacancyRequests requests)
    {
        _access = access;
        _requests = requests;
    }

    public async Task<Result<PagedResult<VacancyRequestDto>>> HandleAsync(
        ListTeamVacancyRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        return await _requests.ListAsync(caller.Value.Scope, query, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary><c>POST /api/me/team/vacancy-requests</c> — boshliq so'rov yuboradi.</summary>
public sealed class SubmitMyTeamVacancyRequestCommandHandler
{
    private readonly TeamAccess _access;
    private readonly TeamVacancyRequests _requests;

    public SubmitMyTeamVacancyRequestCommandHandler(TeamAccess access, TeamVacancyRequests requests)
    {
        _access = access;
        _requests = requests;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        SubmitTeamVacancyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        return await _requests
            .SubmitAsync(
                caller.Value.Scope,
                caller.Value.Ids.EmployeeId,
                command,
                TeamErrors.DepartmentNotFound,
                TeamErrors.NotTeamLead,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary><c>POST /api/me/team/vacancy-requests/{id}/cancel</c> — qaror qilinmagan so'rovni qaytarib olish.</summary>
public sealed class CancelMyTeamVacancyRequestCommandHandler
{
    private readonly TeamAccess _access;
    private readonly TeamVacancyRequests _requests;

    public CancelMyTeamVacancyRequestCommandHandler(TeamAccess access, TeamVacancyRequests requests)
    {
        _access = access;
        _requests = requests;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        return await _requests.CancelAsync(caller.Value.Scope, requestId, cancellationToken).ConfigureAwait(false);
    }
}
