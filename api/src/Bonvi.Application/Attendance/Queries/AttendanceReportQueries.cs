using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

// ════════════════════════════════════════════════════════════════════
//  Chetlanishlar
// ════════════════════════════════════════════════════════════════════

/// <summary>Chetlanishlar hisoboti (SPEC §6.6, <c>GET /reports/exceptions</c>).</summary>
/// <param name="Kind"><c>Late</c> | <c>EarlyLeave</c> | <c>Absent</c> | <c>Missing</c> — majburiy.</param>
/// <param name="From">Davr boshi. Berilmasa — <paramref name="To"/> oyining 1-sanasi.</param>
/// <param name="To">Davr oxiri. Berilmasa — bugun.</param>
/// <param name="LegalEntityId">Kun yozuvidagi firma.</param>
/// <param name="DepartmentId">Kun yozuvidagi bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Kun yozuvidagi lavozim.</param>
/// <param name="EmployeeId">Faqat shu xodim.</param>
/// <param name="MinMinutes">Kamida shuncha daqiqa — faqat <c>Late</c>/<c>EarlyLeave</c> uchun, boshqalarda e'tiborga olinmaydi.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (eng ko'pi 200).</param>
/// <param name="Sort"><c>date</c> (sukut, yangidan eskiga), <c>employee</c>, <c>minutes</c>; teskari uchun <c>-</c>.</param>
public sealed record GetAttendanceExceptionsQuery(
    string? Kind = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? EmployeeId = null,
    int? MinMinutes = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

/// <summary><see cref="GetAttendanceExceptionsQuery"/> ni bajaradi.</summary>
public sealed class GetAttendanceExceptionsQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IClock _clock;

    public GetAttendanceExceptionsQueryHandler(IAttendanceReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<PagedResult<AttendanceExceptionDto>>> HandleAsync(
        GetAttendanceExceptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var filter = BuildFilter(query, _clock.Today, page, pageSize);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.GetExceptionsAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        return new PagedResult<AttendanceExceptionDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }

    /// <summary>So'rovni filtrga aylantiradi — ekran va eksport uchun bitta qoida.</summary>
    internal static Result<ExceptionFilter> BuildFilter(
        GetAttendanceExceptionsQuery query,
        DateOnly today,
        int page,
        int pageSize)
    {
        var kind = AttendanceQueryParsing.ParseExceptionKind(query.Kind);
        if (kind.IsFailure)
        {
            return kind.Error;
        }

        var period = AttendanceQueryParsing.Period(query.From, query.To, today, PeriodDefault.MonthToDate);
        if (period.IsFailure)
        {
            return period.Error;
        }

        var (sort, descending) = AttendanceQueryParsing.ExceptionSort(query.Sort);

        // Manfiy yoki nol «kamida» — filtrsiz bilan bir xil; uni xato qilish
        // HR tozalagan maydonni 400 ga aylantirardi.
        var minMinutes = query.MinMinutes is > 0 ? query.MinMinutes : null;

        return new ExceptionFilter(
            kind.Value,
            period.Value.From,
            period.Value.To,
            query.LegalEntityId,
            query.DepartmentId,
            query.PositionId,
            query.EmployeeId,
            minMinutes,
            sort,
            descending,
            page,
            pageSize);
    }
}

// ════════════════════════════════════════════════════════════════════
//  Xulosa
// ════════════════════════════════════════════════════════════════════

/// <summary>Davr bo'yicha xulosa (SPEC §6.6, <c>GET /reports/summary</c>).</summary>
/// <param name="From">Davr boshi. Berilmasa — <paramref name="To"/> oyining 1-sanasi.</param>
/// <param name="To">Davr oxiri. Berilmasa — bugun.</param>
/// <param name="LegalEntityId">Kun yozuvidagi firma.</param>
/// <param name="DepartmentId">Kun yozuvidagi bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Kun yozuvidagi lavozim.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifadagi xodimlar (eng ko'pi 200).</param>
/// <param name="Sort"><c>employee</c> (sukut), <c>workedMinutes</c>, <c>lateMinutes</c>, <c>absentDays</c>, <c>problemDays</c>.</param>
public sealed record GetAttendanceSummaryQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

/// <summary><see cref="GetAttendanceSummaryQuery"/> ni bajaradi.</summary>
public sealed class GetAttendanceSummaryQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IClock _clock;

    public GetAttendanceSummaryQueryHandler(IAttendanceReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<PagedResult<AttendanceSummaryDto>>> HandleAsync(
        GetAttendanceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var filter = BuildFilter(query, _clock.Today, page, pageSize);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.GetSummaryAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        return new PagedResult<AttendanceSummaryDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }

    /// <summary>So'rovni filtrga aylantiradi — ekran va eksport uchun bitta qoida.</summary>
    internal static Result<SummaryFilter> BuildFilter(
        GetAttendanceSummaryQuery query,
        DateOnly today,
        int page,
        int pageSize)
    {
        var period = AttendanceQueryParsing.Period(query.From, query.To, today, PeriodDefault.MonthToDate);
        if (period.IsFailure)
        {
            return period.Error;
        }

        var (sort, descending) = AttendanceQueryParsing.SummarySort(query.Sort);

        return new SummaryFilter(
            period.Value.From,
            period.Value.To,
            query.LegalEntityId,
            query.DepartmentId,
            query.PositionId,
            sort,
            descending,
            page,
            pageSize);
    }
}
