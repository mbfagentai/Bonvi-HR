using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>Tabel (SPEC §6.6, <c>GET /timesheet</c>).</summary>
/// <param name="Year">Yil — majburiy.</param>
/// <param name="Month">Oy (1–12) — majburiy.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="EmployeeId">Faqat shu xodim.</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifadagi xodimlar (sukut 25, eng ko'pi 200).</param>
public sealed record GetTimesheetQuery(
    int Year,
    int Month,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? EmployeeId = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

/// <summary><see cref="GetTimesheetQuery"/> ni bajaradi.</summary>
public sealed class GetTimesheetQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public GetTimesheetQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<AttendanceTimesheetDto>> HandleAsync(
        GetTimesheetQuery query,
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

        var found = await _readModel.GetTimesheetAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        return ToDto(filter.Value, found, page, pageSize);
    }

    /// <summary>So'rovni filtrga aylantiradi — ekran va eksport uchun bitta qoida.</summary>
    internal static Result<TimesheetFilter> BuildFilter(GetTimesheetQuery query, int page, int pageSize)
    {
        var month = AttendanceQueryParsing.Month(query.Year, query.Month);
        if (month.IsFailure)
        {
            return month.Error;
        }

        return new TimesheetFilter(
            query.Year,
            query.Month,
            query.LegalEntityId,
            query.DepartmentId,
            query.PositionId,
            query.EmployeeId,
            AttendanceQueryParsing.Normalize(query.Search),
            page,
            pageSize);
    }

    internal static AttendanceTimesheetDto ToDto(TimesheetFilter filter, TimesheetPage found, int page, int pageSize)
        => new(
            filter.Year,
            filter.Month,
            found.Days,
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize),
            found.LastComputedAt);
}
