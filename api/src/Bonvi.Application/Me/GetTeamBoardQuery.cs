using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Jamoa paneli — <c>GET /api/me/team/board</c>.</summary>
/// <param name="Date">Toshkent sanasi; berilmasa — bugun.</param>
/// <param name="DepartmentId">Qamrovdagi bo'lim (ichki bo'limlari bilan); berilmasa — butun qamrov.</param>
/// <param name="Status">Faqat shu holatdagilar — faqat jadvalga.</param>
/// <param name="Search">Xodim bo'yicha qidiruv — faqat jadvalga.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (sukut 50, eng ko'pi 200).</param>
public sealed record GetTeamBoardQuery(
    DateOnly? Date = null,
    Guid? DepartmentId = null,
    AttendanceDashboardStatus? Status = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>
/// <see cref="GetTeamBoardQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>HR panelining O'ZI</b> — <see cref="IAttendanceReadModel.GetDashboardAsync"/>, filtri
/// esa <see cref="GetAttendanceDashboardQueryHandler.BuildFilter"/> dan (Excel eksporti kabi):
/// holat qoidasi, sanoqlar, qidiruv, sahifa va «bugun» ikkala ekranda bitta. Ikkinchi hisob
/// yozilsa, boshliq va HR bitta kunni ikki xil o'qiydigan kun albatta kelardi.
/// </para>
/// <para>
/// Qamrov filtrga SERVERDA qo'yiladi (<c>DepartmentRoots</c>); mijozdan kelgan bo'lim faqat
/// qamrov ICHIDA toraytiradi. Qatorga faqat telefon qo'shiladi — bitta so'rov, sahifadagilar
/// uchun.
/// </para>
/// </remarks>
public sealed class GetTeamBoardQueryHandler
{
    private readonly TeamAccess _access;
    private readonly IAttendanceReadModel _readModel;
    private readonly ITeamReadModel _team;
    private readonly TeamViewAudit _audit;
    private readonly IClock _clock;

    public GetTeamBoardQueryHandler(
        TeamAccess access,
        IAttendanceReadModel readModel,
        ITeamReadModel team,
        TeamViewAudit audit,
        IClock clock)
    {
        _access = access;
        _readModel = readModel;
        _team = team;
        _audit = audit;
        _clock = clock;
    }

    public async Task<Result<TeamBoardDto>> HandleAsync(
        GetTeamBoardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var scope = caller.Value.Scope;

        if (query.DepartmentId is { } departmentId && !scope.Contains(departmentId))
        {
            return TeamErrors.DepartmentNotFound;
        }

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize ?? AttendanceQueryParsing.DashboardPageSize);

        // Firma va lavozim ATAYLAB yo'q — boshliqqa faqat bo'lim tanlagichi bor (§C2).
        var dashboard = new GetAttendanceDashboardQuery(
            Date: query.Date,
            Search: query.Search,
            Status: query.Status);

        var filter = GetAttendanceDashboardQueryHandler.BuildFilter(dashboard, _clock.Today, page, pageSize) with
        {
            DepartmentRoots = query.DepartmentId is { } narrowed ? [narrowed] : scope.Roots,
            SearchNameAndPhoneOnly = true,
        };

        var found = await _readModel.GetDashboardAsync(filter, cancellationToken).ConfigureAwait(false);

        var phones = await _team
            .PhonesAsync([.. found.Items.Select(item => item.EmployeeId)], cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<TeamBoardItemDto> items =
        [
            .. found.Items.Select(item => new TeamBoardItemDto(
                item.EmployeeId,
                item.FullName,
                item.PhotoKey is not null,
                item.PositionName,
                item.DepartmentName,
                item.ArrivedAt,
                item.LeftAt,
                item.Status,
                item.LateMinutes,
                item.LeftAuto,
                phones.TryGetValue(item.EmployeeId, out var phone) ? phone.Formatted : null)),
        ];

        await _audit.BoardViewedAsync(caller.Value.Ids).ConfigureAwait(false);

        return new TeamBoardDto(
            found.Date,
            found.IsToday,
            found.AsOf,
            found.Total,
            found.Counts,
            items,
            page,
            pageSize,
            found.TotalItems,
            AttendanceQueryParsing.PageCount(found.TotalItems, pageSize));
    }
}
