using Bonvi.Application.Common.Photos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Xodimlar va mehnat munosabatlari bo'yicha umumiy xatolar.
/// </summary>
/// <remarks>
/// Kodlar bir joyda turadi, chunki ular <b>shartnoma</b>: frontend ularga
/// qarab foydalanuvchiga nima ko'rsatishni hal qiladi (masalan «qora ro'yxat»
/// xatosida tasdiqlash oynasi chiqadi). Har bir handler'da qo'lda yozilsa,
/// bitta harf farqi interfeysni jimgina buzardi.
/// </remarks>
public static class EmployeeErrors
{
    public static Error NotFound { get; } =
        Error.NotFound("xodim.topilmadi", "Xodim topilmadi.");

    public static Error PinflTaken { get; } =
        Error.Conflict("xodim.jshshir_band", "Bunday JSHSHIR bilan xodim allaqachon mavjud.");

    /// <summary>Qora ro'yxat — amalni <b>to'xtatadi</b>.</summary>
    public static Error Blacklisted { get; } =
        Error.Conflict(
            "xodim.qora_royxatda",
            "Xodim qora ro'yxatda. Amalni davom ettirish uchun belgi yopilishi kerak.");

    /// <summary>
    /// Nazorat — ishga olishda faqat ogohlantiradi, bo'shatishda tasdiq so'raydi.
    /// </summary>
    public static Error UnderMonitoring { get; } =
        Error.Conflict(
            "xodim.nazoratda",
            "Xodim nazoratda. Davom ettirish uchun «confirm=true» bilan qayta yuboring.");

    /// <summary>Xodim bor, lekin rasmi yo'q (yoki fayli yo'qolgan).</summary>
    /// <remarks>
    /// «Xodim topilmadi» dan ATAYLAB ajratilgan: interfeys birinchi holatda
    /// xato ko'rsatadi, ikkinchisida esa jimgina bosh harflardan iborat
    /// o'rinbosar rasm chizadi.
    /// </remarks>
    public static Error PhotoNotFound { get; } =
        Error.NotFound("xodim.rasm_yoq", "Xodimning rasmi yo'q.");

    public static Error PhotoTooLarge { get; } =
        Error.Validation("xodim.rasm_katta", "Rasm hajmi 10 MB dan oshmasligi kerak.");

    public static Error PhotoTypeNotSupported { get; } =
        Error.Validation("xodim.rasm_turi", "Faqat JPEG, PNG va WebP rasmlar qabul qilinadi.");

    /// <summary>
    /// Fayl rasm sifatida ochilmadi.
    /// </summary>
    /// <remarks>
    /// <see cref="PhotoTypeNotSupported"/> dan farqi: u e'lon qilingan turga
    /// qaraydi, bu esa fayl MAZMUNIGA. Nomi «.jpg» bo'lgan skript yoki
    /// buzilgan surat aynan shu yerda to'xtaydi.
    /// </remarks>
    public static Error PhotoInvalid { get; } =
        Error.Validation("xodim.rasm_yaroqsiz", "Fayl rasm emas yoki buzilgan.");

    /// <summary>
    /// Umumiy rasm quvurining rad etish sababini XODIM kodiga o'giradi.
    /// </summary>
    /// <remarks>
    /// Quvur (<see cref="SquarePhotoStore"/>) barcha bo'limlar uchun bir xil,
    /// xato KODI esa bo'limga tegishli shartnoma. Xaritalash aynan shu yerda:
    /// yangi sabab qo'shilsa, kompilyator uni tarjimasiz qoldirmaydi.
    /// </remarks>
    public static Error PhotoRejected(PhotoRejection rejection) => rejection switch
    {
        PhotoRejection.TooLarge => PhotoTooLarge,
        PhotoRejection.UnsupportedType => PhotoTypeNotSupported,
        PhotoRejection.Invalid => PhotoInvalid,
        _ => PhotoInvalid,
    };

    public static Error EmploymentNotFound { get; } =
        Error.NotFound("mehnat.topilmadi", "Mehnat munosabati topilmadi.");

    public static Error EmploymentClosed { get; } =
        Error.Conflict("mehnat.tugagan", "Mehnat munosabati allaqachon yakunlangan.");

    public static Error ProbationNotFound { get; } =
        Error.NotFound("sinov.topilmadi", "Sinov muddati topilmadi.");

    public static Error FlagNotFound { get; } =
        Error.NotFound("belgi.topilmadi", "Belgi topilmadi.");

    /// <summary>
    /// Xodim shu firmada allaqachon ishlayapti.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BLOK, ogohlantirish emas. O'rindoshlik (bir odam ikki firmada)
    /// odatiy hol va u to'sib qo'yilmaydi — bu yerdagi shart ikkalasini
    /// ajratadi: BIR XIL yuridik shaxsda ikkinchi ochiq munosabat
    /// mumkin emas.
    /// </para>
    /// <para>
    /// Nega qat'iy: bunday ikkilanish jimgina yuradi. Xodim ikki qator
    /// bo'lib chiqmaydi (ro'yxat har shaxsni bir marta ko'rsatadi), lekin
    /// hisobotdagi stavka, oklad va xodimlar soni ikki barobar bo'lib
    /// ketadi. HR buni faqat oylik hisoblanganda sezardi.
    /// </para>
    /// </remarks>
    public static Error AlreadyEmployedHere { get; } =
        Error.Conflict(
            "mehnat.shu_firmada_bor",
            "Xodim bu yuridik shaxsda allaqachon ishlayapti. "
                + "Avval mavjud mehnat munosabatini yakunlang.");

    public static Error LegalEntityNotFound { get; } =
        Error.NotFound("yuridik_shaxs.topilmadi", "Yuridik shaxs topilmadi.");
}
