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
}
