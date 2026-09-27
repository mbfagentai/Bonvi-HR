using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Documents;
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

    /// <summary>Tekshiruv materiali topilmadi — yoki u boshqa xodimniki.</summary>
    /// <remarks>
    /// Boshqa xodimning materiali ham AYNAN shu javobni oladi: «bor, lekin
    /// sizniki emas» degan farq marshrutdagi xodim orqali begona faylni
    /// qidirish yo'lini ochardi.
    /// </remarks>
    public static Error CheckFileNotFound { get; } =
        Error.NotFound("tekshiruv_fayli.topilmadi", "Tekshiruv materiali topilmadi.");

    public static Error EmploymentNotFound { get; } =
        Error.NotFound("mehnat.topilmadi", "Mehnat munosabati topilmadi.");

    public static Error EmploymentClosed { get; } =
        Error.Conflict("mehnat.tugagan", "Mehnat munosabati allaqachon yakunlangan.");

    public static Error ProbationNotFound { get; } =
        Error.NotFound("sinov.topilmadi", "Sinov muddati topilmadi.");

    /// <summary>
    /// Amal ichida summa yuborildi, lekin <c>employment.salary</c> ruxsati yo'q.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kod ATAYLAB umumiy <c>auth.ruxsat_yoq</c> — endpoint'dagi
    /// <c>RequireAuthorization</c> rad etgandagi bilan bir xil: foydalanuvchi
    /// uchun ikkalasining ma'nosi bitta («bu amalga ruxsatingiz yo'q»),
    /// interfeys esa oklad maydonini ruxsatsiz odamga umuman ko'rsatmaydi.
    /// </para>
    /// <para>
    /// Tekshiruv endpoint'da emas, handler'da, chunki u so'rov TANASIGA
    /// bog'liq (summa bormi-yo'qmi) — <c>RequireAuthorization</c> tanani
    /// ko'rmaydi. Bu xato har qanday o'zgarishdan OLDIN qaytadi.
    /// </para>
    /// </remarks>
    public static Error SalaryPermissionRequired { get; } =
        Error.Forbidden(
            "auth.ruxsat_yoq",
            "Oklad summasini yuborish uchun «employment.salary» ruxsati kerak.");

    /// <summary>
    /// Qo'shimcha vazifa haqi yuborildi, lekin <c>employment.salary</c> yoki
    /// <c>salary.extra.view</c> yo'q.
    /// </summary>
    /// <remarks>Kod umumiy <c>auth.ruxsat_yoq</c> — <see cref="SalaryPermissionRequired"/> bilan bir xil sabab.</remarks>
    public static Error ExtraPayPermissionRequired { get; } =
        Error.Forbidden(
            "auth.ruxsat_yoq",
            "Qo'shimcha haq uchun «employment.salary» va «salary.extra.view» ruxsatlari kerak.");

    /// <summary>
    /// Sinov yakunida yangi lavozim yoki bo'lim yuborildi, lekin
    /// <c>employment.transfer</c> ruxsati yo'q.
    /// </summary>
    /// <remarks>
    /// Sabab <see cref="SalaryPermissionRequired"/> bilan bir xil: amal
    /// ichida kelgan ko'chirish baribir ko'chirish. Usiz «sinovni yakunlash»
    /// ruxsati «ko'chirish» ruxsatini aylanib o'tish yo'li bo'lib qolardi.
    /// </remarks>
    public static Error TransferPermissionRequired { get; } =
        Error.Forbidden(
            "auth.ruxsat_yoq",
            "Yangi lavozim yoki bo'lim tayinlash uchun «employment.transfer» ruxsati kerak.");

    /// <summary>
    /// Ishga qabul YANGI xodim kartasini yaratmoqchi, lekin foydalanuvchida
    /// «employees.create» yo'q — «employment.hire» karta yaratishga huquq bermaydi.
    /// </summary>
    public static Error EmployeeCreatePermissionRequired { get; } =
        Error.Forbidden(
            "auth.ruxsat_yoq",
            "Yangi xodim kartasini yaratish uchun «employees.create» ruxsati kerak.");

    public static Error FlagNotFound { get; } =
        Error.NotFound("belgi.topilmadi", "Belgi topilmadi.");

    /// <summary>
    /// Xodimda allaqachon kuchdagi belgi bor — yangisi uni ALMASHTIRADI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Foydalanuvchi qarori: bir vaqtda atigi bitta kuchdagi belgi
    /// (sababi <c>EmployeeFlag</c> izohida). Amal butunlay
    /// to'silmaydi — HR tasdiqlasa, eskisi yopilib yangisi ochiladi.
    /// Shu sababli bu <see cref="Blacklisted"/> emas, balki
    /// <see cref="UnderMonitoring"/> bilan bir toifadagi xato: 409 va
    /// «tasdiqlang» taklifi.
    /// </para>
    /// <para>
    /// Bu — ASOSIY himoya. Interfeysdagi tasdiqlash oynasi faqat qulaylik:
    /// so'rov to'g'ridan-to'g'ri yuborilsa ham qoida shu yerda ushlab
    /// qoladi.
    /// </para>
    /// </remarks>
    public static Error FlagAlreadyInForce { get; } =
        Error.Conflict(
            "belgi.boshqa_belgi_bor",
            "Xodimda allaqachon kuchdagi belgi bor. Bir vaqtda atigi bitta belgi bo'ladi: "
                + "davom ettirilsa eskisi yopiladi. Tasdiqlash uchun «confirm=true» bilan qayta yuboring.");

    /// <summary>
    /// Ketish belgisi ishlab turgan xodimga qo'yilmoqchi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rezerv va qora ro'yxat — «bu odamni KEYIN qaytarib olamizmi?»
    /// degan savolga javob, ya'ni ular ketish paytida tanlanadi
    /// (<c>EmployeeFlagStage</c> izohi). Ishlab turgan odamni «rezerv»
    /// deb belgilash ma'nosiz: u bizda ishlayapti, chaqirishning hojati
    /// yo'q.
    /// </para>
    /// <para>
    /// <b>Nega TAQIQ, tasdiq emas</b> (<see cref="FlagAlreadyInForce"/>
    /// dan farqli): u yerda ikkala holat ham ma'noli edi va HR qaysi
    /// birini saqlashni tanlaydi. Bu yerda esa saqlanadigan ma'no umuman
    /// yo'q — belgi noto'g'ri joyda turadi va uni tasdiqlash bilan
    /// to'g'rilab bo'lmaydi. Kerak bo'lsa, avval xodim ishdan bo'shatiladi
    /// va belgi o'sha oqimda tanlanadi.
    /// </para>
    /// </remarks>
    public static Error FlagNeedsDeparture { get; } =
        Error.Conflict(
            "belgi.xodim_ishlayapti",
            "Rezerv va qora ro'yxat ishdan ketishda tanlanadi. Xodim hozir ishlayapti — "
                + "unga faqat «nazoratda» yoki «qobiliyatli» belgisi qo'yiladi.");

    /// <summary>
    /// Ishlab turgan xodim belgisi ketgan odamga qo'yilmoqchi.
    /// </summary>
    /// <remarks>
    /// Nazorat va «qobiliyatli» — «bu odam bilan HOZIR nima qilish
    /// kerak?» degan savolga javob. Ketgan odamda bunday vazifa yo'q:
    /// uni hech kim kuzatmaydi va unga hech kim yuklama bermaydi.
    /// Sabab <see cref="FlagNeedsDeparture"/> dagi bilan bir xil.
    /// </remarks>
    public static Error FlagNeedsEmployment { get; } =
        Error.Conflict(
            "belgi.xodim_ketgan",
            "«Nazoratda» va «qobiliyatli» belgilari ishlab turgan xodimga qo'yiladi. "
                + "Xodim ishdan ketgan — unga rezerv yoki qora ro'yxat belgisi qo'yiladi.");

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

    public static Error IncentiveNotFound { get; } =
        Error.NotFound("ragbat_jarima.topilmadi", "Rag'bat/jarima yozuvi topilmadi.");

    public static Error AwardTypeNotFound { get; } =
        Error.NotFound("ragbat_jarima.ragbat_turi_topilmadi", "Rag'bat turi topilmadi.");

    public static Error PenaltyTypeNotFound { get; } =
        Error.NotFound("ragbat_jarima.jarima_turi_topilmadi", "Jarima turi topilmadi.");

    public static Error DocumentNotFound { get; } =
        Error.NotFound("hujjat.topilmadi", "Hujjat topilmadi.");

    public static Error DocumentTypeNotFound { get; } =
        Error.NotFound("hujjat.turi_topilmadi", "Hujjat turi topilmadi.");

    public static Error DocumentTooLarge { get; } =
        Error.Validation(
            "hujjat.katta",
            $"Fayl hajmi {DocumentLimits.MaxBytes / (1024 * 1024)} MB dan oshmasligi kerak.");

    /// <summary>
    /// Fayl mazmuni tanilmadi.
    /// </summary>
    /// <remarks>
    /// <see cref="PhotoInvalid"/> bilan bir xil mantiq: kengaytma emas,
    /// MAZMUN tekshiriladi (<see cref="IFileContentInspector"/>).
    /// </remarks>
    public static Error DocumentInvalid { get; } =
        Error.Validation("hujjat.yaroqsiz", "Fayl PDF, JPEG, PNG yoki WebP emas (yoki buzilgan).");

    /// <summary>Umumiy hujjat quvurining rad etish sababini XODIM kodiga o'giradi.</summary>
    public static Error DocumentRejected(DocumentRejection rejection) => rejection switch
    {
        DocumentRejection.TooLarge => DocumentTooLarge,
        DocumentRejection.Invalid => DocumentInvalid,
        _ => DocumentInvalid,
    };
}
