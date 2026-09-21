using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Requests;

/// <summary>Yo'qlik so'rovlari ro'yxati (<c>GET /requests</c>).</summary>
/// <param name="EmployeeId">Faqat shu xodim — xodim kartasida.</param>
/// <param name="Status">Faqat shu holat.</param>
/// <param name="Decided">Qaror qabul qilinganlar (<c>true</c>) yoki kutayotganlar (<c>false</c>).</param>
/// <param name="From">Davr boshi.</param>
/// <param name="To">Davr oxiri.</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record SearchAbsenceRequestsQuery(
    Guid? EmployeeId = null,
    AbsenceRequestStatus? Status = null,
    bool? Decided = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

/// <summary><see cref="SearchAbsenceRequestsQuery"/> ni bajaradi.</summary>
/// <remarks>
/// Davr ATAYLAB majburiy emas (o'tishlar jurnalidan farqi): so'rovlar
/// jadvali o'nlab qatordan iborat va xodim kartasida odam butun tarixni
/// ko'rishi kerak. Sahifalash esa bor — umumiy navbat o'sib borishi mumkin.
/// </remarks>
public sealed class SearchAbsenceRequestsQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public SearchAbsenceRequestsQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<AbsenceRequestDto>>> HandleAsync(
        SearchAbsenceRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is { } from && query.To is { } to && from > to)
        {
            return AttendanceErrors.PeriodReversed;
        }

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var found = await _readModel
            .SearchAbsenceRequestsAsync(
                new AbsenceRequestFilter(
                    query.EmployeeId,
                    query.Status,
                    query.Decided,
                    query.From,
                    query.To,
                    string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AbsenceRequestDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }
}

/// <summary>Kutayotgan so'rovlar soni — nav nishoni uchun.</summary>
public sealed record GetPendingAbsenceRequestCountQuery;

/// <summary><see cref="GetPendingAbsenceRequestCountQuery"/> ni bajaradi.</summary>
public sealed class GetPendingAbsenceRequestCountQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public GetPendingAbsenceRequestCountQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<AbsenceRequestCountDto>> HandleAsync(
        GetPendingAbsenceRequestCountQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var count = await _readModel
            .CountPendingAbsenceRequestsAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AbsenceRequestCountDto(count);
    }
}
