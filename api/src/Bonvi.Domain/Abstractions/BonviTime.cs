namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Korxonaning vaqt mintaqasi — tizimdagi YAGONA manba.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu Domain qatlamida turibdi.</b> Ilgari siljish
/// <c>SystemClock</c> ichida, Infrastructure'da edi va u yerda
/// <c>internal</c>. Application undan foydalana olmaydi (qatlamlar
/// yo'nalishi <c>Api → Infrastructure → Application → Domain</c> va
/// buni arxitektura testi tekshiradi), shu sababli
/// <c>GetSystemInfoQuery</c> da <c>TimeSpan.FromHours(5)</c> qo'lda
/// IKKINCHI marta yozilgan edi. Aynan shu holatdan <c>SystemClock</c>
/// ning o'z izohi ogohlantirgan: ikki nusxa bir kun ajralib ketadi va
/// yarim tunda hisoblangan «bugun» ikki xil chiqadi.
/// </para>
/// <para>
/// <b>Nega <c>TimeZoneInfo</c> emas, oddiy siljish.</b> O'zbekistonda
/// yozgi vaqt yo'q va 1992 yildan beri siljish o'zgarmagan — ya'ni
/// qoida bitta son. <c>TimeZoneInfo.FindSystemTimeZoneById</c> esa
/// operatsion tizimdagi mintaqa bazasiga tayanadi: u konteynerda
/// bo'lmasligi ham mumkin (<c>alpine</c> da <c>tzdata</c> alohida
/// paket) va o'shanda tizim ishga tushishda yiqilardi.
/// </para>
/// <para>
/// ⚠️ <b>Bu siljish faqat KO'RSATISH va «bugun» hisobi uchun.</b>
/// Bazada vaqt har doim UTC bo'lib yotadi: <c>timestamptz</c> ustuni
/// siljishni umuman saqlamaydi, u lahzani saqlaydi. «Bazaga +5
/// yozamiz» degan urinish bir xil lahzani ikki xil o'qiladigan qilib
/// qo'yardi va eski yozuvlar yangilaridan besh soatga ajralardi.
/// </para>
/// </remarks>
public static class BonviTime
{
    /// <summary>Toshkent vaqti — UTC+5, yozgi vaqt yo'q.</summary>
    public static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);

    /// <summary>Lahzani Toshkent vaqtiga keltiradi.</summary>
    /// <remarks>
    /// Nomi bilan chaqirilgani <c>.ToOffset(BonviTime.TashkentOffset)</c>
    /// dan qisqa emas, lekin ANIQROQ: chaqiruv joyida «bu yerda nega
    /// siljish bor?» degan savol tug'ilmaydi — javob nomning o'zida.
    /// </remarks>
    public static DateTimeOffset ToTashkent(this DateTimeOffset instant)
        => instant.ToOffset(TashkentOffset);

    /// <summary>Lahza Toshkent vaqti bo'yicha qaysi KUNGA tushadi.</summary>
    public static DateOnly TashkentDate(this DateTimeOffset instant)
        => DateOnly.FromDateTime(instant.ToOffset(TashkentOffset).Date);
}
