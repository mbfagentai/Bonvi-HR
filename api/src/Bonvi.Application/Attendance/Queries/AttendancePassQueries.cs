using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>O'tishlar jurnali (SPEC §6.5, <c>GET /passes</c>).</summary>
/// <param name="From">Davr boshi (Toshkent sanasi, o'zi ham kiradi). Berilmasa — <paramref name="To"/>.</param>
/// <param name="To">Davr oxiri (o'zi ham kiradi). Berilmasa — bugun.</param>
/// <param name="EmployeeId">Faqat shu xodim.</param>
/// <param name="DeviceId">Faqat shu qurilma.</param>
/// <param name="Direction">Faqat shu yo'nalish.</param>
/// <param name="Method">Faqat shu tasdiqlash usuli.</param>
/// <param name="Matched"><c>true</c> — moslanganlar, <c>false</c> — moslanmaganlar, bo'sh — hammasi.</param>
/// <param name="LegalEntityId">Davrda shu firmada ishlaganlar.</param>
/// <param name="DepartmentId">Davrda shu bo'limda (ichki bo'limlari bilan) ishlaganlar.</param>
/// <param name="Search">Xodim (F.I.Sh., JSHSHIR, pasport, telefon) yoki qurilmadagi shaxs raqami.</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (eng ko'pi 200).</param>
/// <param name="Sort"><c>occurredAt</c> yoki <c>-occurredAt</c> (sukut — eng yangisi birinchi).</param>
public sealed record SearchPassesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? EmployeeId = null,
    Guid? DeviceId = null,
    PassDirection? Direction = null,
    PassMethod? Method = null,
    bool? Matched = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

/// <summary><see cref="SearchPassesQuery"/> ni bajaradi.</summary>
public sealed class SearchPassesQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IClock _clock;

    public SearchPassesQueryHandler(IAttendanceReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<PagedResult<AttendancePassDto>>> HandleAsync(
        SearchPassesQuery query,
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

        var found = await _readModel.SearchPassesAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        return new PagedResult<AttendancePassDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }

    /// <summary>
    /// So'rovni filtrga aylantiradi — ekran ham, eksport ham AYNAN shu metodni chaqiradi.
    /// </summary>
    /// <remarks>
    /// <c>internal</c>: <see cref="ExportPassesQueryHandler"/> o'z nusxasini
    /// yozmaydi (<c>SearchEmployeesQueryHandler.BuildFilter</c> precedenti) —
    /// aks holda yangi filtr faqat ekranga qo'shilib, Excel eskirib qolardi.
    /// </remarks>
    internal static Result<PassSearchFilter> BuildFilter(
        SearchPassesQuery query,
        DateOnly today,
        int page,
        int pageSize)
    {
        var period = AttendanceQueryParsing.Period(query.From, query.To, today, PeriodDefault.SingleDay);
        if (period.IsFailure)
        {
            return period.Error;
        }

        return new PassSearchFilter(
            period.Value.From,
            period.Value.To,
            query.EmployeeId,
            query.DeviceId,
            query.Direction,
            query.Method,
            query.Matched,
            query.LegalEntityId,
            query.DepartmentId,
            AttendanceQueryParsing.Normalize(query.Search),
            AttendanceQueryParsing.PassesDescending(query.Sort),
            page,
            pageSize);
    }
}

/// <summary>Bitta o'tish (<c>GET /passes/{id}</c>).</summary>
/// <remarks>
/// Taxtadagi ishdan ketganlar ogohlantirishi <c>?passId=</c> bilan jurnalga
/// havola beradi va o'sha o'tish jurnalning ochiq sahifasida bo'lmasligi
/// mumkin. Qator shakli ro'yxatdagi bilan bir xil.
/// </remarks>
/// <param name="PassId">O'tish identifikatori.</param>
public sealed record GetPassQuery(Guid PassId);

/// <summary><see cref="GetPassQuery"/> ni bajaradi.</summary>
public sealed class GetPassQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public GetPassQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<AttendancePassDto>> HandleAsync(
        GetPassQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pass = await _readModel.FindPassAsync(query.PassId, cancellationToken).ConfigureAwait(false);

        // Topilmadi — 404, bo'sh qator EMAS: havola eskirgan (o'tish saqlash
        // muddati tugab o'chirilgan) bo'lsa, ekran «ma'lumot yo'q» deb aniq
        // aytishi kerak, bo'sh kartani ko'rsatmasligi kerak.
        return pass is null ? AttendanceErrors.PassNotFound : pass;
    }
}
