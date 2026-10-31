using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Common;

/// <summary>
/// Ma'lumotnomalar bilan ishlashda uchraydigan xatolar.
/// </summary>
/// <remarks>
/// Xato kodlari BIR joyda to'plangan: frontend ularni tarjima qiladi,
/// shu sababli kod tasodifan o'zgarib ketmasligi kerak. Xabar esa
/// zaxira variant — mijoz kodni tanimasa shu ko'rsatiladi.
/// </remarks>
public static class CatalogErrors
{
    /// <summary>Ma'lumotnoma yozuvi topilmadi.</summary>
    public static Error NotFound { get; } =
        Error.NotFound("malumotnoma.topilmadi", "Ma'lumotnoma yozuvi topilmadi.");

    /// <summary>Bunday nom allaqachon band.</summary>
    /// <remarks>
    /// <see cref="ErrorType.Conflict"/> (409), <see cref="ErrorType.Validation"/> emas:
    /// kiritilgan qiymatning o'zi to'g'ri, faqat bazaning JORIY holati bilan
    /// to'qnashadi. Boshqa foydalanuvchi o'sha nomni o'chirsa, xuddi shu
    /// so'rov muvaffaqiyatli bo'ladi.
    /// </remarks>
    public static Error NameTaken { get; } =
        Error.Conflict("malumotnoma.nom_band", "Bunday nom allaqachon mavjud.");

    /// <summary>Bo'lim topilmadi.</summary>
    public static Error DepartmentNotFound { get; } =
        Error.NotFound("bolim.topilmadi", "Bo'lim topilmadi.");

    /// <summary>Ko'rsatilgan ota bo'lim topilmadi.</summary>
    public static Error ParentDepartmentNotFound { get; } =
        Error.NotFound("bolim.ota_topilmadi", "Ko'rsatilgan ota bo'lim topilmadi.");

    /// <summary>Bo'lim boshlig'i qilib berilgan xodim yo'q yoki o'chirilgan (SPEC-jamoam §E).</summary>
    /// <remarks>
    /// <see cref="ErrorType.Validation"/> (400): boshliq — bo'lim formasining maydoni, va noto'g'ri
    /// qiymat formaning o'zida ko'rsatiladi. Usiz yo'q xodim boshliq bo'lib qolar va «Jamoam»
    /// eshigini hech kimga ochmasdan jimgina turaverardi.
    /// </remarks>
    public static Error HeadEmployeeNotFound { get; } =
        Error.Validation("bolim.boshliq_topilmadi", "Bo'lim boshlig'i qilib tanlangan xodim topilmadi.");

    /// <summary>
    /// Bo'lim boshlig'ini o'zgartirish — <c>catalog.manage</c> ustiga <c>attendance.view</c> ham kerak.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boshliq «Jamoam» orqali bo'limidagi odamlarning davomatini ko'radi, ya'ni boshliq tayinlash —
    /// davomatni ko'rish huquqini BERISH. O'zida bo'lmagan huquqni boshqaga berib bo'lmasin: aks holda
    /// faqat ma'lumotnoma yurituvchi o'zini yoki tanishini boshliq qilib, davomatni aylanma yo'l bilan
    /// ko'rardi.
    /// </para>
    /// <para>
    /// Kod ATAYLAB umumiy <c>auth.ruxsat_yoq</c> (403) — <c>EmployeeErrors.SalaryPermissionRequired</c>
    /// bilan bir xil sabab: tekshiruv so'rov TANASIGA bog'liq, <c>RequireAuthorization</c> uni ko'rmaydi.
    /// </para>
    /// </remarks>
    public static Error HeadPermissionRequired { get; } =
        Error.Forbidden(
            "auth.ruxsat_yoq",
            "Bo'lim boshlig'ini o'zgartirish uchun «attendance.view» ruxsati ham kerak.");

    /// <summary>
    /// Bo'limni o'z avlodiga ko'chirishga urinish — halqa.
    /// </summary>
    /// <remarks>
    /// Bu eng xavfli xato: agar o'tkazib yuborilsa, daraxtda yopiq halqa
    /// hosil bo'ladi va uni aylanib chiquvchi har qanday kod
    /// (yo'lni hisoblash, ro'yxat qurish) cheksiz rekursiyaga tushadi.
    /// </remarks>
    public static Error DepartmentCycle { get; } =
        Error.Conflict("bolim.halqa", "Bo'limni o'zining ichidagi bo'limga ko'chirib bo'lmaydi.");

    /// <summary>Lavozim topilmadi.</summary>
    public static Error PositionNotFound { get; } =
        Error.NotFound("lavozim.topilmadi", "Lavozim topilmadi.");

    /// <summary>
    /// Yozuvda hozir ishlayotgan xodimlar bor — avval ular boshqasiga o'tkaziladi (egasi, 2026-10-31).
    /// </summary>
    /// <remarks>
    /// Usiz faol emas bo'lim yoki lavozimda odamlar qolardi: yangi formalarda u tanlanmaydi, lekin
    /// xodim kartasi, davomat va hisobotlar uni ko'rsatishda davom etardi.
    /// </remarks>
    public static Error HasActiveEmployees { get; } =
        Error.Conflict(
            "malumotnoma.faol_xodim_bor",
            "Bu yozuvga ishlayotgan xodimlar biriktirilgan — avval ularni boshqasiga o'tkazing.");

    /// <summary>O'tkaziladigan yozuv yaroqsiz: o'zi, uning ichidagi bo'lim, yo'q yoki faol emas.</summary>
    public static Error ReassignTargetInvalid { get; } =
        Error.Validation(
            "malumotnoma.otkazish_joyi_notogri",
            "Xodimlarni faqat boshqa, faol yozuvga o'tkazish mumkin.");

    /// <summary>Ota bo'limi faol emas bo'limni yoqib bo'lmaydi — avval ota bo'lim yoqiladi.</summary>
    public static Error ParentDepartmentInactive { get; } =
        Error.Conflict(
            "bolim.ota_faol_emas",
            "Yuqori bo'lim faol emas — avval uni faol qiling.");

    /// <summary>Xodimlarni o'tkazish uchun xodim kartasidagi o'tkazish huquqi ham kerak.</summary>
    /// <remarks>Kod umumiy <c>auth.ruxsat_yoq</c> — <see cref="HeadPermissionRequired"/> bilan bir xil sabab.</remarks>
    public static Error ReassignPermissionRequired { get; } =
        Error.Forbidden(
            "auth.ruxsat_yoq",
            "Xodimlarni o'tkazish uchun ularni o'tkazish ruxsati ham kerak.");
}
