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
}
