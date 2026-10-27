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
/// <param name="AbsenceTypeId">Yo'qlik turi bo'yicha (SPEC-jadval-so'rovi §B).</param>
/// <param name="Mode">Shakl: qism kun / butun kun / bir necha kun.</param>
public sealed record SearchAbsenceRequestsQuery(
    Guid? EmployeeId = null,
    AbsenceRequestStatus? Status = null,
    bool? Decided = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null,
    Guid? AbsenceTypeId = null,
    AbsenceRequestMode? Mode = null);

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

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var filter = BuildFilter(query, page, pageSize);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel
            .SearchAbsenceRequestsAsync(filter.Value, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AbsenceRequestDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }

    /// <summary>
    /// So'rovni filtrga aylantiradi — ekran ham, Excel eksporti ham AYNAN shu metodni chaqiradi.
    /// </summary>
    /// <remarks>
    /// <c>internal</c>: <see cref="Reports.Queries.ExportAbsenceRequestsQueryHandler"/> o'z
    /// nusxasini yozmaydi (<see cref="Queries.SearchPassesQueryHandler"/> precedenti) — aks
    /// holda yangi filtr faqat ekranga qo'shilib, Excel eskirib qolardi. Sahifa hajmi
    /// tashqaridan: ekran uni 200 bilan qisadi, eksport esa chegara + 1 so'raydi.
    /// </remarks>
    internal static Result<AbsenceRequestFilter> BuildFilter(SearchAbsenceRequestsQuery query, int page, int pageSize)
    {
        if (query.From is { } from && query.To is { } to && from > to)
        {
            return AttendanceErrors.PeriodReversed;
        }

        return new AbsenceRequestFilter(
            query.EmployeeId,
            query.Status,
            query.Decided,
            query.From,
            query.To,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            page,
            pageSize,
            query.AbsenceTypeId,
            query.Mode);
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
