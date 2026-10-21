using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Common.Lists;

/// <summary>
/// HR ro'yxatlaridagi «dan–gacha» sana oralig'i (egasi, 2026-10-20) — tekshiruv
/// va Toshkent kuni chegaralari BITTA joyda.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qoida.</b> So'rovda <c>from</c> va <c>to</c> (<c>yyyy-MM-dd</c>), ikkalasi
/// ham KIRADI va istalgani berilmasligi mumkin — ochiq chegara. Boshi oxiridan
/// keyin bo'lsa 400 <c>royxat.sana_oraligi_notogri</c>. Qaysi sana bilan
/// solishtirilishi — har bir ro'yxatning o'z qarori (nomzod — ariza sanasi,
/// ishdan ketgan — bo'shatish sanasi va h.k.), u shu yerda emas, ro'yxat
/// handler'ida.
/// </para>
/// <para>
/// <b>Nega bitta kod, har modulga alohida emas.</b> Xato ro'yxatning o'ziga
/// emas, FILTR shakliga tegishli va matni hamma joyda bir xil. Beshta modulda
/// beshta kod — beshta bir xil tarjima va web uchun beshta holat bo'lardi.
/// Davomat so'rovlari ATAYLAB o'zining mavjud kodini saqlaydi
/// (<c>davomat.davr_notogri</c>): u yerda oraliq oldindan bor edi va web uni
/// allaqachon biladi.
/// </para>
/// <para>
/// <b>Lahza ustunlari Toshkent sanasi bo'yicha.</b> <c>timestamptz</c> ustun
/// (masalan nomzod yaratilgan lahza) ustunni o'girib emas, chegaralarni
/// UTC ga o'girib solishtiriladi: <c>[from 00:00 +05, to+1 00:00 +05)</c>.
/// Shart shu tufayli indeksga tushadi va «ekranda 10-oktabr» deb ko'ringan
/// yozuv aynan 10-oktabr filtrida chiqadi (tizim sanani +5 da ko'rsatadi).
/// <c>DateOnly</c> ustunlar (ishga kirish, bo'shatish, sinov tugashi, belgi
/// boshlanishi) HECH QACHON siljitilmaydi — kun qiymatiga mintaqa tegishli emas.
/// </para>
/// </remarks>
public static class ListDateRange
{
    /// <summary>Oraliq boshi oxiridan keyin.</summary>
    public static Error Reversed { get; } = Error.Validation(
        "royxat.sana_oraligi_notogri",
        "Sana oralig'ining boshi oxiridan keyin bo'lishi mumkin emas.");

    /// <summary>Ikkala chegara berilgan bo'lsa, boshi oxiridan keyin emasligini tekshiradi.</summary>
    /// <param name="from">Boshi (kiradi), <c>null</c> — ochiq.</param>
    /// <param name="to">Oxiri (kiradi), <c>null</c> — ochiq.</param>
    public static Result Validate(DateOnly? from, DateOnly? to)
        => from is { } start && to is { } end && start > end ? Reversed : Result.Success();

    /// <summary>Toshkent kunining boshi — UTC lahza (kiradi).</summary>
    /// <remarks>
    /// <c>0001-01-01</c> da <c>null</c> (chegara yo'q): uning Toshkent yarim tuni
    /// UTC da birinchi yildan oldin va <see cref="DateTimeOffset"/> uni ifodalay
    /// olmaydi — usiz bunday so'rov 400 emas, 500 berardi. Ma'nosi baribir bir xil:
    /// «eng boshidan».
    /// </remarks>
    /// <param name="from">Oraliq boshi.</param>
    public static DateTimeOffset? StartUtc(DateOnly? from)
        => from is { } date && date > DateOnly.MinValue ? TashkentMidnightUtc(date) : null;

    /// <summary>Oraliq oxiridan KEYINGI Toshkent kunining boshi — UTC lahza (kirmaydi).</summary>
    /// <remarks>
    /// <c>9999-12-31</c> da <c>null</c> (chegara yo'q): keyingi kun mavjud emas
    /// (<see cref="DateOnly.AddDays"/> istisno otardi). Ma'nosi — «oxirigacha».
    /// </remarks>
    /// <param name="to">Oraliq oxiri.</param>
    public static DateTimeOffset? EndUtcExclusive(DateOnly? to)
        => to is { } date && date < DateOnly.MaxValue ? TashkentMidnightUtc(date.AddDays(1)) : null;

    /// <summary>Npgsql <c>timestamptz</c> ga faqat UTC yozadi — shu sababli <c>ToUniversalTime</c>.</summary>
    private static DateTimeOffset TashkentMidnightUtc(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();
}
