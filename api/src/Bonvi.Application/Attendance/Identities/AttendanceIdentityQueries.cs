using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Identities;

/// <summary>Mosliklar ro'yxati (<c>GET /identities</c>).</summary>
/// <param name="Search">Qurilmadagi shaxs raqami yoki xodim.</param>
/// <param name="EmployeeId">Faqat shu xodimning mosliklari.</param>
/// <param name="Provider">Faqat shu raqamlar fazosi.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (eng ko'pi 200).</param>
public sealed record SearchIdentitiesQuery(
    string? Search = null,
    Guid? EmployeeId = null,
    AttendanceIdentityProvider? Provider = null,
    int? Page = null,
    int? PageSize = null);

/// <summary><see cref="SearchIdentitiesQuery"/> ni bajaradi.</summary>
public sealed class SearchIdentitiesQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public SearchIdentitiesQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<AttendanceIdentityDto>>> HandleAsync(
        SearchIdentitiesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var found = await _readModel
            .SearchIdentitiesAsync(
                new IdentitySearchFilter(
                    AttendanceQueryParsing.Normalize(query.Search),
                    query.EmployeeId,
                    query.Provider,
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AttendanceIdentityDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }
}

/// <summary>Moslanmagan shaxs raqamlari (<c>GET /identities/unmatched</c>, SPEC §4.2).</summary>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (eng ko'pi 200).</param>
public sealed record ListUnmatchedQuery(int? Page = null, int? PageSize = null);

/// <summary><see cref="ListUnmatchedQuery"/> ni bajaradi.</summary>
public sealed class ListUnmatchedQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public ListUnmatchedQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<UnmatchedGroupDto>>> HandleAsync(
        ListUnmatchedQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var found = await _readModel
            .ListUnmatchedAsync(new UnmatchedFilter(page, pageSize), cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<UnmatchedGroupDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }
}
