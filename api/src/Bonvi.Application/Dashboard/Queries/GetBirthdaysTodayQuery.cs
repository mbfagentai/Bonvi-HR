using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Dashboard.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Dashboard.Queries;

/// <summary>
/// Bugun tug'ilgan kuni bo'lgan xodimlar ro'yxatini so'rash — parametrsiz.
/// </summary>
/// <remarks>
/// «Bugun» qachonligini mijoz AYTMAYDI va ayta olmaydi ham: brauzerdagi
/// sana foydalanuvchining qurilmasi sozlamasiga bog'liq va u xohlagan
/// kuniga qo'yilishi mumkin. Sana serverda, <see cref="IClock"/> orqali
/// olinadi — bosh sahifadagi son bilan AYNAN bir manbadan.
/// </remarks>
public sealed record GetBirthdaysTodayQuery;

/// <summary>
/// <see cref="GetBirthdaysTodayQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Handler'ning butun mas'uliyati — «bugun» ni olish va YOSHNI
/// hisoblash.</b> Kimni ro'yxatga qo'shish sharti o'qish modelida
/// (<c>BirthdayQuery</c>) va u bosh sahifadagi son bilan bitta nusxada.
/// </para>
/// <para>
/// <b>Sana <see cref="IClock"/> dan, <c>DateTime.Now</c> dan emas.</b>
/// Server UTC'da yuradi, HR esa Toshkent kunini ko'radi: soat 03:00 da
/// «bugun» ikki xil kun bo'lib qolardi va ro'yxat bosh sahifadagi son
/// bilan mos kelmasdi (<c>GetDashboardStatsQueryHandler</c> dagi bilan
/// bir xil sabab va bir xil manba).
/// </para>
/// <para>
/// <b>Ruxsat bu yerda tekshirilmaydi.</b> Endpoint <c>employees.view</c>
/// bilan yopilgan va javobda maskalanadigan maydon yo'q — ism, rasm,
/// lavozim va bo'lim xodimlar ro'yxatida allaqachon ochiq. Rekruting
/// sonlaridan farqi shu: u yerda javob MAZMUNI ruxsatga qarab torayadi
/// va shu sababli tekshiruv handler'da turadi.
/// </para>
/// </remarks>
public sealed class GetBirthdaysTodayQueryHandler
{
    /// <summary>
    /// Ishonchli deb qabul qilinadigan yoshning yuqori chegarasi.
    /// </summary>
    /// <remarks>
    /// Qiymat <c>NotificationCollector</c> dagi tug'ilgan kun
    /// bildirishnomasi bilan AYNAN bir xil (<c>age is &gt; 0 and &lt; 120</c>):
    /// bitta xodim ikkita ekranda («qo'ng'iroq» va bu ro'yxat) bir xil
    /// yoshda ko'rinishi kerak. 120 dan oshgan yoki musbat bo'lmagan yosh —
    /// Excel'dan ko'chirilgan yozuvdagi xato, tug'ilgan kun emas.
    /// </remarks>
    private const int MaxTrustedAge = 120;

    private readonly IDashboardReadModel _dashboard;
    private readonly IClock _clock;

    public GetBirthdaysTodayQueryHandler(IDashboardReadModel dashboard, IClock clock)
    {
        _dashboard = dashboard;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<DashboardBirthdayDto>>> HandleAsync(
        GetBirthdaysTodayQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = _clock.Today;

        var rows = await _dashboard
            .ListBirthdaysTodayAsync(today, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<DashboardBirthdayDto> items =
        [
            .. rows.Select(row => new DashboardBirthdayDto(
                row.EmployeeId,
                row.FullName,
                row.PhotoKey,
                row.PositionName,
                row.DepartmentName,
                row.BirthDate,
                Age(today, row.BirthDate))),
        ];

        return Result.Success(items);
    }

    /// <summary>Bugun to'lgan yosh — sana ishonchsiz bo'lsa <c>null</c>.</summary>
    /// <remarks>
    /// <para>
    /// Oy va kun allaqachon bugunga to'g'ri kelgani uchun oddiy yillar
    /// ayirmasi yetarli — «tug'ilgan kuni hali bo'lmagan» holati bu
    /// ro'yxatda ta'rifi bo'yicha yo'q.
    /// </para>
    /// <para>
    /// <b>Nega qoida bildirishnomalar bilan UMUMIY joyga chiqarilmadi.</b>
    /// Tug'ilgan kun SHARTI chiqarildi, chunki u ikki joyda ajralib ketsa
    /// jimgina noto'g'ri natija beradi: son bilan ro'yxat mos kelmay
    /// qoladi va buni hech kim sezmaydi. Yosh esa shunday emas — u
    /// ko'rsatish tafsiloti va u yo'qolsa ekranda darhol ko'rinadi.
    /// Uni umumiylashtirish uchun bu handler <c>Notifications</c>
    /// modulining ichki qoidasiga bog'lanishi kerak bo'lardi (yoki
    /// teskarisi), ya'ni ikki mustaqil modul o'rtasida bir juft satr
    /// uchun bog'liqlik paydo bo'lardi.
    /// </para>
    /// </remarks>
    private static int? Age(DateOnly today, DateOnly birthDate)
    {
        var age = today.Year - birthDate.Year;

        return age is > 0 and < MaxTrustedAge ? age : null;
    }
}
