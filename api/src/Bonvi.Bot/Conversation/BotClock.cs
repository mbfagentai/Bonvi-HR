namespace Bonvi.Bot.Conversation;

/// <summary>
/// Botning vaqt mintaqasi — nomzod ko'radigan HAR QANDAY sana shu yerdan
/// o'tadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida konstanta va nega faqat BITTA.</b> API vaqtni UTC
/// da qaytaradi, Toshkent esa undan besh soat OLDINDA. Ya'ni Toshkent
/// yarim tunidan ertalab soat 5 gacha UTC hali KECHAGI kunda turadi:
/// 7-sentabr soat 01:30 da topshirilgan ariza siljitilmagan holda
/// nomzodga «сиз 6 сентябрда ариза топширгансиз» bo'lib ko'rinardi.
/// Bu haqiqiy xato edi (<c>BotTexts.AlreadyApplied</c>).
/// </para>
/// <para>
/// Siljish IKKINCHI marta yozilmasligi kerak: ikkita nusxa bir kun
/// ajralib ketadi va o'shanda bir joyda «bugun», ikkinchisida «kecha»
/// chiqadi. Backend'da ham aynan shu qoida bor
/// (<c>SystemClock.TashkentOffset</c>) — bot unga havola qila olmaydi
/// (<c>Bonvi.Bot.csproj</c> izohi), shu sababli qiymat shu yerda
/// takrorlanadi, lekin bot ichida YAGONA nusxada.
/// </para>
/// <para>
/// ⚠️ <b>Tug'ilgan sana bu yerdan O'TMAYDI.</b> U <c>DateOnly</c> —
/// kun qiymatiga vaqt mintaqasi umuman tegishli emas va uni siljitish
/// nomzodning tug'ilgan kunini bir kunga surib yuborardi.
/// </para>
/// </remarks>
public static class BotClock
{
    /// <summary>Toshkent vaqti (UTC+5, yozgi vaqt yo'q).</summary>
    public static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);

    /// <summary>Hozirgi Toshkent vaqti.</summary>
    /// <remarks>
    /// Ommaviy oferta bilan rozilik VAQTI shu yerdan olinadi. Server uni
    /// o'zi UTC ga keltiradi — siljish qanday bo'lishidan qat'i nazar
    /// bir xil lahza yoziladi.
    /// </remarks>
    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(TashkentOffset);

    /// <summary>Istalgan lahzani Toshkent vaqtiga keltiradi.</summary>
    public static DateTimeOffset Local(DateTimeOffset moment) => moment.ToOffset(TashkentOffset);
}
