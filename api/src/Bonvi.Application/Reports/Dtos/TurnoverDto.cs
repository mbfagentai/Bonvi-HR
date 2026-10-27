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

/// <summary>
/// «Nomzodlar qayerdan bilgan» hisoboti — API javobi (SPEC-qayerdan-bilgan §7).
/// </summary>
/// <remarks>
/// Foiz YO'Q — <see cref="TurnoverDto"/> bilan bir xil qaror: ikkala son ham javobda,
/// ulushni ko'rinish qatlami hisoblaydi (nolga bo'linish va yaxlitlash o'sha yerda).
/// </remarks>
/// <param name="Rows">Kanallar — nomzodlar soni bo'yicha kamayish tartibida; bo'sh kanal yo'q.</param>
/// <param name="TotalCandidates">Davrdagi barcha nomzodlar (qatorlar yig'indisi).</param>
/// <param name="TotalHired">Ulardan ishga olinganlar.</param>
public sealed record CandidateReferralReportDto(
    IReadOnlyList<CandidateReferralRowDto> Rows,
    int TotalCandidates,
    int TotalHired);

/// <summary>Bitta kanal qatori.</summary>
/// <param name="ChannelId">Kanal. <c>null</c> — «Ko'rsatilmagan» (kanalsiz eski nomzodlar).</param>
/// <param name="ChannelName">Kanal nomi; «Ko'rsatilmagan» qatorida <c>null</c> — yorliqni ekran qo'yadi.</param>
/// <param name="IsActive">Kanal faolmi; «Ko'rsatilmagan» qatorida <c>false</c> (kanal yo'q).</param>
/// <param name="Candidates">Davrda shu kanal orqali ariza bergan nomzodlar.</param>
/// <param name="Hired">Ulardan ishga olinganlar (holati «ishga olingan»).</param>
public sealed record CandidateReferralRowDto(
    Guid? ChannelId,
    string? ChannelName,
    bool IsActive,
    int Candidates,
    int Hired);
