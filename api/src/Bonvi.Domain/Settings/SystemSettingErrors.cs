using System.Globalization;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Settings;

/// <summary>
/// Tizim sozlamalari bo'yicha xatolar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega Domain qatlamida, Application'da emas</b> (bildirishnomalarda
/// esa <c>NotificationErrors</c> Application'da turadi). Bu yerdagi
/// xatolarni birinchi bo'lib DOMENNING O'ZI tashlaydi:
/// <see cref="SystemSetting.Create"/> va <see cref="SystemSetting.SetValue"/>
/// qiymatni <see cref="SystemSettingKeys"/> ta'rifi bo'yicha tekshiradi.
/// Xatolar Application'da tursa, domen ularga yeta olmasdi va tekshiruv
/// ikki joyda — agregatda va use-case'da — takrorlanardi. Takrorlangan
/// tekshiruvning taqdiri esa ma'lum: bir kun ikkisi ajralib ketadi.
/// </para>
/// <para>
/// <b>Kodlar o'zbekcha va BARQAROR.</b> Ular frontend uchun shartnoma:
/// «sozlama.chegara» kelganda ekran maydonni qizartiradi va min/max
/// ni ko'rsatadi. Matn esa faqat zaxira — foydalanuvchi tiliga frontend
/// o'zi tarjima qiladi (tizim 4 tilli).
/// </para>
/// </remarks>
public static class SystemSettingErrors
{
    /// <summary>Kalit umuman berilmagan.</summary>
    public static Error EmptyKey { get; } =
        Error.Validation("sozlama.kalit_bosh", "Sozlama kaliti ko'rsatilmagan.");

    /// <summary>
    /// Kalit <see cref="SystemSettingKeys"/> ro'yxatida yo'q.
    /// </summary>
    /// <remarks>
    /// Bu — bazaga tushishdan oldingi OXIRGI to'siq. Noma'lum kalit
    /// yozib qo'yilsa, u hech qachon o'qilmasdi (o'qish faqat ro'yxatdagi
    /// kalitlar bo'yicha ketadi) va jadvalda jimgina axlat bo'lib
    /// yotardi — xuddi bildirishnoma holatlaridagi kabi.
    /// </remarks>
    public static Error UnknownKey { get; } =
        Error.Validation(
            "sozlama.notogri_kalit",
            "Bunday sozlama yo'q: kalit noma'lum.");

    /// <summary>Butun son kutilgan joyda son emas.</summary>
    public static Error NotANumber { get; } =
        Error.Validation(
            "sozlama.notogri_son",
            "Bu sozlama butun son bo'lishi kerak.");

    /// <summary>
    /// Son ruxsat etilgan oraliqdan tashqarida.
    /// </summary>
    /// <remarks>
    /// Chegaralar xabar MATNIGA qo'shiladi (masalan «1 dan 1440 gacha»),
    /// chunki ular kalitdan kalitga farq qiladi va foydalanuvchi
    /// nimadan chiqib ketganini bilishi kerak. Kod esa har doim bitta —
    /// frontend uni tanib, maydonni ko'rsatib beradi.
    /// </remarks>
    public static Error OutOfRange(int min, int max) =>
        Error.Validation(
            "sozlama.chegara",
            string.Format(
                CultureInfo.InvariantCulture,
                "Qiymat {0} dan {1} gacha bo'lishi kerak.",
                min,
                max));

    /// <summary>Matn juda uzun.</summary>
    /// <remarks>
    /// Chegara xavfsizlik uchun ham kerak: bot matni tashqi ekranga
    /// chiqadi va uzunligi cheklanmagan maydon so'rov tanasini cheksiz
    /// o'stirishga imkon berardi.
    /// </remarks>
    public static Error TextTooLong(int maxLength) =>
        Error.Validation(
            "sozlama.matn_uzun",
            string.Format(
                CultureInfo.InvariantCulture,
                "Matn {0} belgidan oshmasligi kerak.",
                maxLength));
}
