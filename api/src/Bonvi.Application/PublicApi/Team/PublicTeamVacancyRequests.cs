using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.PublicApi.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.VacancyRequests;
using Bonvi.Domain.Common;

namespace Bonvi.Application.PublicApi.Team;

/*
  Rahbar botidan vakansiya so'rovi (egasi, 2026-10-31) — bo'lim kaliti bilan. Qamrov — kalitning
  bo'limi (`PublicTeamAccess`); kalit odam emas, shuning uchun so'rovchi — bo'limning boshlig'i
  (bo'lmasa qamrov ichidagi eng yaqin ustki bo'limniki). Mantiq `TeamVacancyRequests` da.
*/

/// <summary><c>GET /team/vacancy-requests</c> — kalit qamrovidagi so'rovlar.</summary>
public sealed class ListPublicTeamVacancyRequestsQueryHandler
{
    private readonly PublicTeamAccess _access;
    private readonly TeamVacancyRequests _requests;

    public ListPublicTeamVacancyRequestsQueryHandler(PublicTeamAccess access, TeamVacancyRequests requests)
    {
        _access = access;
        _requests = requests;
    }

    public async Task<Result<PagedResult<VacancyRequestDto>>> HandleAsync(
        PublicTeamKey key,
        ListTeamVacancyRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        var scope = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope.Error;
        }

        return await _requests.ListAsync(scope.Value, query, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary><c>POST /team/vacancy-requests</c> — bot so'rov yuboradi.</summary>
public sealed class SubmitPublicTeamVacancyRequestCommandHandler
{
    private readonly PublicTeamAccess _access;
    private readonly TeamVacancyRequests _requests;

    public SubmitPublicTeamVacancyRequestCommandHandler(PublicTeamAccess access, TeamVacancyRequests requests)
    {
        _access = access;
        _requests = requests;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        PublicTeamKey key,
        SubmitTeamVacancyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var scope = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope.Error;
        }

        return await _requests
            .SubmitAsync(
                scope.Value,
                requesterId: null,
                command,
                PublicApiErrors.DepartmentNotFound,
                PublicApiErrors.DepartmentHeadMissing,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary><c>POST /team/vacancy-requests/{id}/cancel</c> — qaror qilinmagan so'rovni qaytarib olish.</summary>
public sealed class CancelPublicTeamVacancyRequestCommandHandler
{
    private readonly PublicTeamAccess _access;
    private readonly TeamVacancyRequests _requests;

    public CancelPublicTeamVacancyRequestCommandHandler(PublicTeamAccess access, TeamVacancyRequests requests)
    {
        _access = access;
        _requests = requests;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        PublicTeamKey key,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var scope = await _access.ResolveAsync(key, cancellationToken).ConfigureAwait(false);
        if (scope.IsFailure)
        {
            return scope.Error;
        }

        return await _requests.CancelAsync(scope.Value, requestId, cancellationToken).ConfigureAwait(false);
    }
}
