namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// Vaqt oralig'ini BUTUN daqiqaga aylantirish — butun hisoblash uchun yagona joy.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Kesiladi, yuvarlanmaydi.</b> 7 soat 59 daqiqa 30 soniya — 479
/// daqiqa, 480 emas: yuqoriga yuvarlash hech kim ishlamagan yarim
/// daqiqani «ishlangan» deb yozardi (SPEC §5.5, 5-qadam). Tabel — HR
/// odamlar bilan tortishadigan son, unda to'qib chiqarilgan soniya
/// bo'lmasligi kerak.
/// </para>
/// <para>
/// <b>Nega alohida sinf.</b> Daqiqaga aylantirish hisoblashning oltita
/// joyida uchraydi: ishlangan vaqt, olingan tanaffus, oynadagi borliq,
/// kechikish, erta ketish va reja. Har biri o'zicha aylantirsa, bir kun
/// bittasi <c>Math.Round</c> ga almashadi va sonlar bir daqiqaga
/// ajralib ketadi — bunday xatoni hech kim ko'rmaydi, faqat hisobot
/// «negadir bir daqiqaga» mos kelmay qoladi.
/// </para>
/// </remarks>
internal static class WholeMinutes
{
    /// <summary>Oraliqdagi butun daqiqalar; manfiy yoki nol oraliq — 0.</summary>
    /// <remarks>
    /// Manfiy qiymat bu yerda hech qachon ma'no bermaydi: «kechikish −3
    /// daqiqa» — bu kechikmagan degani. Pastki chegara shu yerda turadi,
    /// har bir chaqiruv joyida alohida <c>Math.Max</c> unutilmasligi uchun.
    /// Butun sonli bo'lish (<c>Ticks / TicksPerMinute</c>) ATAYLAB:
    /// <c>TotalMinutes</c> — <c>double</c>, va uning kasr qismi katta
    /// yig'indida bitta tickka adashishi mumkin.
    /// </remarks>
    /// <param name="span">Aylantiriladigan oraliq.</param>
    public static int AtLeastZero(TimeSpan span)
        => span <= TimeSpan.Zero ? 0 : (int)(span.Ticks / TimeSpan.TicksPerMinute);
}
