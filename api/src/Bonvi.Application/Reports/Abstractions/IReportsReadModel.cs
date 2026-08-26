namespace Bonvi.Application.Reports.Abstractions;

/// <summary>
/// Hisobotlar uchun <b>o'qish</b> porti — dastlab kadrlar oqimi (turnover).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>IDashboardReadModel</c> ga qo'shilmagan.</b> Bosh sahifa —
/// har kirishda ochiladigan, davrga (allaqachon tor: kun/hafta/15 kun/oy)
/// bog'liq bir martalik sonlar. Kadrlar oqimi esa OYLAR bo'yicha SERIYA
/// (grafik uchun) va alohida <c>reports.view</c> ruxsati ostida — ular
/// boshqa ekran, boshqa ruxsat, boshqa hayot davri. Bitta portga
/// qo'shilsa, bosh sahifa endi hisobot ruxsatini ham bilib qolardi.
/// </para>
/// <para>Amalga oshirilishi Infrastructure qatlamida.</para>
/// </remarks>
public interface IReportsReadModel
{
    /// <summary>Kadrlar oqimi: oylik kelgan/ketgan dinamikasi va ketish sabablari.</summary>
    Task<TurnoverReport> GetTurnoverAsync(TurnoverRange range, CancellationToken cancellationToken = default);
}

/// <summary>So'ralgan davr — ikki tomondan yopiq (<c>From</c> va <c>To</c> ham kiradi).</summary>
public sealed record TurnoverRange(DateOnly From, DateOnly To);

/// <summary>Kadrlar oqimi hisobotining butun natijasi.</summary>
/// <param name="Months">Oylik qatorlar, xronologik tartibda (eskisi birinchi — grafik chapdan o'ngga o'sadi).</param>
/// <param name="Reasons">Shu davrda ketganlarning sabab bo'yicha taqsimoti, ko'pdan kamga.</param>
public sealed record TurnoverReport(
    IReadOnlyList<TurnoverMonth> Months,
    IReadOnlyList<TurnoverReasonBucket> Reasons);

/// <summary>
/// Bitta oylik qator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Foiz YO'Q — <c>DashboardStatsDto</c> bilan bir xil qaror.</b> Bu yerda
/// faqat sanalgan faktlar: kelgan, ketgan va ikkala chegaradagi soni (odam
/// bo'yicha, boshqa hodisalar orasida ikki marta sanalmasin uchun). Agar
/// frontend foizni ko'rsatmoqchi bo'lsa, u <c>Left / ((HeadcountStart +
/// HeadcountEnd) / 2)</c> formulasi bilan o'zi hisoblaydi — bu standart
/// «o'rtacha shtat soniga nisbatan» ta'rif. Uni serverda tayyor qilib
/// berish 0 ga bo'linish va yaxlitlashni backend'ga yuklardi, buni esa
/// bu loyihada har doim ko'rinish qatlami hal qiladi.
/// </para>
/// <para>
/// <b><c>Hired</c>/<c>Left</c> — HODISA bo'yicha, ODAM bo'yicha emas.</b>
/// Bu bosh sahifadagi kartalardan ATAYLAB farq qiladi: u yerda («necha
/// KISHI hozir ishlayapti») bitta shaxs ikki marta sanalmasligi kerak.
/// Bu yerda esa savol boshqa — «bu oyda necha marta ishga qabul/bo'shatish
/// VOQEASI bo'ldi». Bitta xodim shu oy ichida bir firmadan ketib,
/// ikkinchisiga (yoki hatto o'shanga qayta) kirsa — bu ikkita alohida HR
/// voqeasi va ikkalasi ham trendda ko'rinishi kerak, aks holda «bu oy
/// qancha harakat bo'ldi» degan savolga kam ko'rsatilgan javob berilardi.
/// </para>
/// </remarks>
/// <param name="MonthStart">Oyning boshi (har doim 1-sana).</param>
/// <param name="MonthEnd">
/// Oyning oxiri — odatda oyning oxirgi kuni, lekin JORIY (hali tugamagan)
/// oy uchun so'ralgan davrning oxiri (odatda bugun).
/// </param>
/// <param name="Hired">Shu oy ichida ishga qabul qilingan MUNOSABATLAR soni.</param>
/// <param name="Left">Shu oy ichida ishdan bo'shatilgan MUNOSABATLAR soni.</param>
/// <param name="HeadcountStart">Oy BOSHIDA ishlayotgan (hali ketmagan) xodimlar soni — odam bo'yicha.</param>
/// <param name="HeadcountEnd">Oy OXIRIDA ishlayotgan xodimlar soni — odam bo'yicha.</param>
public sealed record TurnoverMonth(
    DateOnly MonthStart,
    DateOnly MonthEnd,
    int Hired,
    int Left,
    int HeadcountStart,
    int HeadcountEnd);

/// <summary>Bitta ketish sababi bo'yicha to'plangan son.</summary>
/// <param name="ReasonId">Sabab identifikatori. <c>null</c> — ko'rsatilmagan.</param>
/// <param name="ReasonName">Ko'rsatiladigan nom.</param>
/// <param name="IsVoluntary">
/// Xodimning o'z tashabbusi bilanmi. Sabab ko'rsatilmagan bo'lakda — <c>null</c>
/// (na ixtiyoriy, na majburiy deb aytib bo'lmaydi, chunki umuman ma'lum emas).
/// </param>
/// <param name="Count">Shu sabab bilan ketganlar soni (munosabat bo'yicha).</param>
public sealed record TurnoverReasonBucket(Guid? ReasonId, string ReasonName, bool? IsVoluntary, int Count);
