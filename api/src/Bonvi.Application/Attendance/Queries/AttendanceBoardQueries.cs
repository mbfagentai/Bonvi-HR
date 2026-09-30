using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>«Hozir kim ishda» taxtasi (SPEC §6.5, <c>GET /board</c>).</summary>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
public sealed record GetBoardQuery(
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    string? Search = null);

/// <summary><see cref="GetBoardQuery"/> ni bajaradi.</summary>
/// <remarks>
/// Tekshiradigan parametr yo'q — ham filtrsiz, ham har qanday filtr bilan
/// taxta mazmunli. Handler baribir bor: endpoint read model'ni to'g'ridan-
/// to'g'ri chaqirmaydi (qatlamlar qoidasi), va qidiruv matnini bo'sh
/// qiymatga aylantirish barcha ro'yxatlarda bir joyda bajariladi.
/// </remarks>
public sealed class GetBoardQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public GetBoardQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<AttendanceBoardDto>> HandleAsync(
        GetBoardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var board = await _readModel
            .GetBoardAsync(
                new BoardFilter(
                    query.LegalEntityId,
                    query.DepartmentId,
                    AttendanceQueryParsing.Normalize(query.Search)),
                cancellationToken)
            .ConfigureAwait(false);

        return board;
    }
}

/// <summary>
/// Faqat ishdan ketganlar ogohlantirishi — nav nishoni va qurilmalar sahifasi uchun.
/// </summary>
/// <remarks>
/// <c>GET /former-employee-alerts</c>. Parametr YO'Q va bu ataylab:
/// ogohlantirishni hech qanday filtr yashirmasligi kerak (SPEC §6.5).
/// </remarks>
public sealed record GetFormerEmployeeAlertsQuery;

/// <summary><see cref="GetFormerEmployeeAlertsQuery"/> ni bajaradi.</summary>
public sealed class GetFormerEmployeeAlertsQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public GetFormerEmployeeAlertsQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<FormerEmployeeAlertsDto>> HandleAsync(
        GetFormerEmployeeAlertsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var alerts = await _readModel.GetFormerEmployeeAlertsAsync(cancellationToken).ConfigureAwait(false);

        return new FormerEmployeeAlertsDto(alerts.Count, alerts);
    }
}
