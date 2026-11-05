using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Devices;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>Asosiy sahifa — davr grafigi (<c>GET /dashboard/trend</c>).</summary>
/// <remarks>
/// Ekranda bu «bir necha kunlik davr tanlandi» degani: panel jadvalni
/// yashiradi va kunlik chiziqli grafikni ko'rsatadi. Shu sababli qidiruv,
/// holat filtri, saralash va sahifa YO'Q — ular jadvalning parametrlari.
/// </remarks>
/// <param name="From">Davr boshi (Toshkent sanasi) — majburiy.</param>
/// <param name="To">Davr oxiri — majburiy, ikkala chegara ham kiradi.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim (ichki bo'limlari bilan).</param>
/// <param name="PositionId">Lavozim.</param>
public sealed record GetAttendanceTrendQuery(
    DateOnly From,
    DateOnly To,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null);

/// <summary><see cref="GetAttendanceTrendQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// Davr tekshiruvi <see cref="AttendanceQueryParsing.Period"/> orqali —
/// hisobotlar va jurnal bilan AYNAN bir xil qoida va ayni o'sha ikki kod
/// (<c>davomat.davr_notogri</c>, <c>davomat.davr_uzun</c>). Grafik uchun
/// uchinchi chegara yozilmadi: «hisobotda bir chorak ochiladi, grafikda
/// boshqacha» degan farqni HR ga tushuntirib bo'lmaydi.
/// </para>
/// <para>
/// Ikkala sana ham MAJBURIY — panelning o'zidan farqli. Sabab: sanasiz
/// so'rov «bugun» degan bitta kunni bildiradi va u uchun allaqachon
/// <see cref="GetAttendanceDashboardQuery"/> bor; grafik esa ekranda faqat
/// foydalanuvchi oraliqni TANLAGANDA so'raladi.
/// </para>
/// </remarks>
public sealed class GetAttendanceTrendQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceDeviceRepository _devices;
    private readonly IClock _clock;
    private readonly IAttendanceScope _scope;

    public GetAttendanceTrendQueryHandler(
        IAttendanceReadModel readModel,
        IAttendanceDeviceRepository devices,
        IClock clock,
        IAttendanceScope scope)
    {
        _readModel = readModel;
        _devices = devices;
        _clock = clock;
        _scope = scope;
    }

    public async Task<Result<AttendanceTrendDto>> HandleAsync(
        GetAttendanceTrendQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var period = AttendanceQueryParsing.Period(
            query.From,
            query.To,
            _clock.Today,
            PeriodDefault.SingleDay);

        if (period.IsFailure)
        {
            return period.Error;
        }

        var (from, to) = period.Value;

        var trend = await _readModel
            .GetDashboardTrendAsync(
                new DashboardTrendFilter(from, to, query.LegalEntityId, query.DepartmentId, query.PositionId),
                cancellationToken)
            .ConfigureAwait(false);

        // Devices are not a scoped user's business: names and outages are the whole company's.
        if (_scope.IsScoped)
        {
            return trend;
        }

        // Davr rejimida ham qurilma ogohlantirishlari ko'rinsin — Statistika
        // sahifasi shu rejimda `/dashboard` ni so'ramaydi.
        var devices = await _devices.ListAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);

        return trend with { DeviceHealth = AttendanceDeviceHealth.Of(devices, _clock.UtcNow) };
    }
}
