using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Dashboard.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Dashboard.Queries;

/// <summary>
/// Bosh sahifaning ko'rsatkichlarini so'rash.
/// </summary>
/// <param name="Period">
/// Davr almashtirgichining qiymati: <c>day</c> | <c>week</c> | <c>days15</c> |
/// <c>month</c>. Berilmasa — <c>month</c>: bir kunlik oynada odatda nol
/// chiqadi va HR bo'sh ekran ko'rib qolardi.
/// </param>
/// <remarks>
/// <para>
/// <b>Nega qiymat SATR, sanoq (enum) emas.</b> Minimal API so'rov satridagi
/// sanoqni REGISTRGA SEZGIR bog'laydi: <c>?period=Day</c> ishlaydi,
/// <c>?period=day</c> esa 400 beradi. Ekrandagi almashtirgich qiymatlari
/// esa kichik harfda va shunday bo'lib qolishi kerak — interfeys C#
/// nomlash uslubini takrorlashi shart emas. Shu sababli qiymat satr bo'lib
/// keladi va shu yerda, ikkala yozuvni ham qabul qilib, sanoqqa aylanadi.
/// </para>
/// </remarks>
public sealed record GetDashboardStatsQuery(string? Period = null);

/// <summary>
/// <see cref="GetDashboardStatsQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu handler'ning butun mas'uliyati — davrni sanaga aylantirish.</b>
/// Sanoqning o'zi <see cref="IDashboardReadModel"/> da, bazada bajariladi.
/// Ikkisi ataylab ajratilgan: «1 oy qayerdan boshlanadi» degan savol
/// biznes qoidasi va u o'zgarishi mumkin, <c>COUNT</c> so'rovlari esa
/// o'zgarmaydi.
/// </para>
/// <para>
/// <b>Sana <see cref="IClock"/> dan olinadi, <c>DateTime.Now</c> dan emas.</b>
/// Ikki sabab: server UTC'da yuradi, HR esa Toshkent kunini ko'radi
/// (soat 03:00 da «bugun» ikki xil kun bo'lib qolardi); va vaqtni
/// boshqara olmasdan «oxirgi 15 kun» qoidasini test qilib bo'lmaydi.
/// </para>
/// </remarks>
public sealed class GetDashboardStatsQueryHandler
{
    private readonly IDashboardReadModel _dashboard;
    private readonly IClock _clock;

    public GetDashboardStatsQueryHandler(IDashboardReadModel dashboard, IClock clock)
    {
        _dashboard = dashboard;
        _clock = clock;
    }

    public async Task<Result<DashboardStatsDto>> HandleAsync(
        GetDashboardStatsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        DashboardPeriod period;

        if (string.IsNullOrWhiteSpace(query.Period))
        {
            period = DashboardPeriod.Month;
        }
        else if (!Enum.TryParse(query.Period, ignoreCase: true, out period)
            || !Enum.IsDefined(period))
        {
            // `Enum.TryParse` RAQAMNI ham qabul qiladi: «99» muvaffaqiyatli
            // «bog'lanadi» va sanoqda mavjud bo'lmagan qiymat hosil bo'ladi.
            // U sana arifmetikasiga tushsa, xato jimgina, noto'g'ri oraliq
            // ko'rinishida chiqardi — shu sababli `IsDefined` ham kerak.
            return Error.Validation(
                "dashboard.davr_notogri",
                "Davr noto'g'ri. Ruxsat etilgan qiymatlar: day, week, days15, month.");
        }

        var to = _clock.Today;
        var from = PeriodStart(period, to);

        // Oldingi davr — joriysiga TEGIB turadi va aynan shu qoida bilan
        // hisoblanadi. Shu sababli «bir oy» ikkalasida ham bir xil uzunlikda
        // bo'ladi va taqqoslash halol chiqadi.
        var previousTo = from.AddDays(-1);
        var previousFrom = PeriodStart(period, previousTo);

        var counts = await _dashboard
            .GetCountsAsync(
                new DashboardCountsRange(to, from, to, previousFrom, previousTo),
                cancellationToken)
            .ConfigureAwait(false);

        return new DashboardStatsDto(
            period,
            from,
            to,
            previousFrom,
            previousTo,
            counts.ActiveEmployees,
            counts.OnProbation,
            counts.FormerEmployees,
            counts.Hired,
            counts.Left,
            counts.HiredPrevious,
            counts.LeftPrevious,
            counts.BirthdaysToday,
            new DashboardCatalogSizesDto(
                counts.Departments,
                counts.Positions,
                counts.Directions,
                counts.LegalEntities));
    }

    /// <summary>
    /// Davrning boshlanish sanasi — oxiridan orqaga sanaladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Oraliq ikki tomondan yopiq, shu sababli «7 kun» uchun 6 kun orqaga
    /// qaytiladi: 18–24 avgust — bu yetti kun, sakkiz emas. Klassik
    /// «bir kun ko'p» xatosi aynan shu yerda tug'iladi.
    /// </para>
    /// <para>
    /// «1 oy» kunlarda emas, <c>AddMonths</c> bilan sanaladi: fevralda
    /// 28 kun, avgustda 31 kun va HR uchun «bir oy» — kalendar tushunchasi.
    /// </para>
    /// </remarks>
    private static DateOnly PeriodStart(DashboardPeriod period, DateOnly end) => period switch
    {
        DashboardPeriod.Day => end,
        DashboardPeriod.Week => end.AddDays(-6),
        DashboardPeriod.Days15 => end.AddDays(-14),

        // Oy — sukut. Noma'lum qiymat bu yerga yeta olmaydi: u yuqorida
        // `Enum.IsDefined` bilan rad etilgan.
        _ => end.AddMonths(-1).AddDays(1),
    };
}
