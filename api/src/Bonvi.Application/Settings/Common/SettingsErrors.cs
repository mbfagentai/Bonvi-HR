using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Settings.Common;

/// <summary>
/// Sozlamalar SO'ROVI bo'yicha xatolar.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda faqat so'rovning SHAKLIGA tegishli xatolar turadi. Kalit va
/// qiymatning o'zi haqidagi xatolar (noma'lum kalit, chegaradan
/// chiqqan son, uzun matn) domenda —
/// <c>Bonvi.Domain.Settings.SystemSettingErrors</c> da: ularni domen
/// agregatining O'ZI tashlaydi va use-case ularni faqat uzatib beradi.
/// Nusxasi bu yerda saqlansa, ikkita ta'rif bir kun ajralib ketardi.
/// </para>
/// <para>
/// «Topilmadi» xatosi ATAYLAB yo'q: yozilmagan sozlama — bu «mavjud
/// emas» degani emas, u sukut qiymat bilan ishlab turibdi
/// (<c>SystemSetting</c> izohi).
/// </para>
/// </remarks>
public static class SettingsErrors
{
    /// <summary>So'rovda bironta sozlama ko'rsatilmagan.</summary>
    /// <remarks>
    /// Bo'sh so'rov jimgina «muvaffaqiyat» qaytarsa, klientdagi xato
    /// (masalan formadan hech narsa yig'ilmagani) sezilmasdan qolardi:
    /// foydalanuvchi «saqlandi» degan xabarni ko'rib, hech narsa
    /// saqlanmagan bo'lardi.
    /// </remarks>
    public static Error NothingToUpdate { get; } =
        Error.Validation(
            "sozlama.royxat_bosh",
            "So'rovda bironta sozlama ko'rsatilmagan.");

    /// <summary>Bitta kalit so'rovda ikki marta kelgan.</summary>
    /// <remarks>
    /// «Oxirgisi g'olib» qoidasini qo'llash mumkin edi, lekin u
    /// yashirin xato bo'lardi: qaysi qiymat yozilgani so'rovdagi
    /// TARTIBGA bog'liq bo'lib qolardi va klient buni bilmasdi.
    /// </remarks>
    public static Error DuplicateKey { get; } =
        Error.Validation(
            "sozlama.takror_kalit",
            "Bitta sozlama so'rovda ikki marta ko'rsatilgan.");

    /// <summary>O'tish yozuvining saqlash muddati suratnikidan qisqa.</summary>
    /// <remarks>
    /// Prefiksi <c>davomat.</c> — kod SPEC §6.9 ro'yxatida va tarjimasi davomat
    /// bo'limida turadi; qoida esa sozlamalar ekranida buziladi, shu sababli shu yerda.
    /// </remarks>
    public static Error RetentionOrder { get; } =
        Error.Validation(
            "davomat.saqlash_muddati_nomuvofiq",
            "O'tish yozuvlarining saqlash muddati suratlarnikidan qisqa bo'lishi mumkin emas (0 — hech qachon o'chirilmaydi).");
}

/// <summary>
/// Kompaniya logotipining xatolari.
/// </summary>
/// <remarks>
/// Kodlar ALOHIDA, hisob rasmi bilan bir xil emas (<c>hisob.rasm_*</c>):
/// ikkalasi bir xil quvurdan o'tsa ham, foydalanuvchi uchun bular
/// butunlay boshqa narsalar va xabar matni ham boshqacha. Bitta kodni
/// ikki joyda ishlatish esa frontendni «bu qaysi rasm haqida?» degan
/// savol oldida qoldirardi.
/// </remarks>
public static class BotLogoErrors
{
    public static Error NotFound { get; } =
        Error.NotFound("bot.logo_yoq", "Bot uchun logotip qo'yilmagan.");

    public static Error TooLarge { get; } =
        Error.Validation("bot.logo_katta", "Logotip hajmi 10 MB dan oshmasligi kerak.");

    public static Error TypeNotSupported { get; } =
        Error.Validation("bot.logo_turi", "Faqat JPEG, PNG va WebP rasmlar qabul qilinadi.");

    public static Error Invalid { get; } =
        Error.Validation("bot.logo_yaroqsiz", "Fayl rasm emas yoki buzilgan.");

    public static Error Rejected(PhotoRejection rejection) => rejection switch
    {
        PhotoRejection.TooLarge => TooLarge,
        PhotoRejection.UnsupportedType => TypeNotSupported,
        _ => Invalid,
    };
}
