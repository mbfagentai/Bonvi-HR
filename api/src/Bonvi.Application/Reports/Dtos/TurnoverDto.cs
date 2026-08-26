namespace Bonvi.Application.Reports.Dtos;

/// <summary>Kadrlar oqimi hisoboti — API javobi.</summary>
/// <param name="From">So'ralgan davrning boshi (oy boshidan).</param>
/// <param name="To">So'ralgan davrning oxiri (odatda bugun).</param>
/// <param name="Months">Oylik qatorlar — grafik uchun, xronologik tartibda.</param>
/// <param name="Reasons">Shu davrdagi ketish sabablari taqsimoti, ko'pdan kamga.</param>
public sealed record TurnoverDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<TurnoverMonthDto> Months,
    IReadOnlyList<TurnoverReasonDto> Reasons);

/// <summary>Kadrlar oqimining bitta oylik nuqtasi.</summary>
/// <param name="MonthStart">Oy boshi.</param>
/// <param name="MonthEnd">Oy oxiri (joriy oy uchun — so'ralgan davr oxiri).</param>
/// <param name="Hired">Shu oyda ishga qabul qilingan hodisalar soni.</param>
/// <param name="Left">Shu oyda ishdan bo'shatilgan hodisalar soni.</param>
/// <param name="HeadcountStart">Oy boshidagi shtat soni (odam bo'yicha).</param>
/// <param name="HeadcountEnd">Oy oxiridagi shtat soni (odam bo'yicha).</param>
public sealed record TurnoverMonthDto(
    DateOnly MonthStart,
    DateOnly MonthEnd,
    int Hired,
    int Left,
    int HeadcountStart,
    int HeadcountEnd);

/// <summary>Ketish sababi bo'yicha bitta bo'lak.</summary>
/// <param name="ReasonId">Sabab identifikatori. <c>null</c> — ko'rsatilmagan.</param>
/// <param name="ReasonName">Ko'rsatiladigan nom.</param>
/// <param name="IsVoluntary">Xodimning o'z tashabbusi bilanmi. Noma'lum bo'lsa — <c>null</c>.</param>
/// <param name="Count">Shu sabab bilan ketganlar soni.</param>
public sealed record TurnoverReasonDto(Guid? ReasonId, string ReasonName, bool? IsVoluntary, int Count);
