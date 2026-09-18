using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>Asosiy sahifa — kunlik davomat paneli (<c>GET /dashboard</c>).</summary>
/// <param name="Date">Toshkent sanasi; berilmasa — bugun.</param>
/// <param name="Search">Xodim bo'yicha qidiruv — faqat jadvalga.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="Status">Faqat shu holatdagilar — faqat jadvalga.</param>
/// <param name="Sort"><c>name</c> (sukut) yoki <c>-name</c>.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (sukut 50, eng ko'pi 200).</param>
public sealed record GetAttendanceDashboardQuery(
    DateOnly? Date = null,
    string? Search = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    AttendanceDashboardStatus? Status = null,
    string? Sort = null,
    int? Page = null,
    int? PageSize = null);

/// <summary><see cref="GetAttendanceDashboardQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// Rad etadigan parametr yo'q: har qanday sana mazmunli (o'tgan — o'sha kun
/// qanday o'tgani, kelajak — kim kutilmoqda), sahifa esa boshqa ro'yxatlardagi
/// bilan bir xil qisiladi. Shu sababli yangi xato kodi ham yo'q; noto'g'ri
/// formatdagi sana yoki holat nomini ASP.NET o'zi 400 bilan qaytaradi.
/// </para>
/// <para>
/// «Bugun» shu yerda, <see cref="IClock"/> dan olinadi — jurnal handleri bilan
/// bir xil: Toshkent sanasining ikkinchi ta'rifi yozilmaydi.
/// </para>
/// </remarks>
public sealed class GetAttendanceDashboardQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IClock _clock;

    public GetAttendanceDashboardQueryHandler(IAttendanceReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<AttendanceDashboardDto>> HandleAsync(
        GetAttendanceDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize ?? AttendanceQueryParsing.DashboardPageSize);

        var filter = new DashboardFilter(
            query.Date ?? _clock.Today,
            query.LegalEntityId,
            query.DepartmentId,
            query.PositionId,
            AttendanceQueryParsing.Normalize(query.Search),
            query.Status,
            AttendanceQueryParsing.DashboardDescending(query.Sort),
            page,
            pageSize);

        var found = await _readModel.GetDashboardAsync(filter, cancellationToken).ConfigureAwait(false);

        return new AttendanceDashboardDto(
            found.Date,
            found.IsToday,
            found.AsOf,
            found.Total,
            found.Counts,
            found.Items,
            page,
            pageSize,
            found.TotalItems,
            AttendanceQueryParsing.PageCount(found.TotalItems, pageSize));
    }
}
