namespace Bonvi.Application.Catalog.Common;

/// <summary>
/// Ma'lumotnoma yozuvini yaratish va tahrirlash uchun YAGONA buyruq.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta buyruq sakkizta ma'lumotnomaga yetadi.</b> Sakkizta
/// ma'lumotnomaning to'qqiz maydonining yettitasi bir xil (nom, kod, tartib,
/// izoh). Farq faqat bir-ikkita qo'shimcha maydonda. Har biriga alohida
/// buyruq yozilsa, o'sha yetti maydon sakkiz marta takrorlanadi va
/// birortasini o'zgartirish sakkizta faylga tegishni talab qiladi.
/// </para>
/// <para>
/// <b>Nega bu "ortiqcha maydonli" tuzilma zarar qilmaydi.</b> Ma'lum bir
/// ma'lumotnomaga tegishli bo'lmagan maydon shunchaki <b>e'tiborsiz
/// qoldiriladi</b>: har bir turning <c>IReferenceDefinition</c> ta'rifi
/// faqat o'ziga keraklisini o'qiydi. Natijada frontendda ham bitta
/// modal forma sakkizta ma'lumotnomaga xizmat qiladi.
/// </para>
/// </remarks>
/// <param name="Name">Nom. Barcha ma'lumotnomalar uchun majburiy.</param>
/// <param name="Note">Ixtiyoriy izoh.</param>
/// <param name="TaxId">STIR — faqat yuridik shaxs uchun.</param>
/// <param name="FullName">To'liq rasmiy nom — faqat yuridik shaxs uchun.</param>
/// <param name="Color">Rang <c>#RRGGBB</c> — faqat yo'nalish uchun.</param>
/// <param name="IsVoluntary">Ixtiyoriy ketishmi — faqat ishdan ketish sababi uchun.</param>
/// <param name="IsRehireable">Qayta ishga olish mumkinmi — faqat ishdan ketish sababi uchun.</param>
/// <param name="HasAmount">Pul bilan bog'liqmi — rag'bat va jarima turlari uchun.</param>
/// <param name="Severity">Og'irlik darajasi 1–3 — faqat jarima turi uchun.</param>
/// <param name="IsRequired">Majburiy hujjatmi — faqat hujjat turi uchun.</param>
/// <param name="ParentId">Ota bo'lim — faqat bo'lim YARATISHDA. Keyin «ko'chirish» amali bilan o'zgaradi.</param>
/// <param name="LegalEntityId">Bo'lim qaysi yuridik shaxsga tegishli.</param>
/// <param name="HeadEmployeeId">Bo'lim boshlig'i (xodim).</param>
/// <param name="DepartmentId">Lavozim qaysi bo'limga tegishli.</param>
/// <param name="Grade">Lavozim darajasi 1–5.</param>
/// <param name="ProbationDays">Standart sinov muddati (kun), 1–180. Bo'sh — 90 kun.</param>
/// <param name="CountsAsWorkTime">So'rov vaqti ish vaqti hisoblanadimi — faqat yo'qlik turi uchun.</param>
public sealed record SaveReferenceCommand(
    string? Name,
    string? Note = null,
    string? TaxId = null,
    string? FullName = null,
    string? Color = null,
    bool? IsVoluntary = null,
    bool? IsRehireable = null,
    bool? HasAmount = null,
    int? Severity = null,
    bool? IsRequired = null,
    Guid? ParentId = null,
    Guid? LegalEntityId = null,
    Guid? HeadEmployeeId = null,
    Guid? DepartmentId = null,
    int? Grade = null,
    int? ProbationDays = null,
    bool? CountsAsWorkTime = null)
{
    /// <summary>
    /// Tekshiruvdan o'tgan nom.
    /// </summary>
    /// <remarks>
    /// Validator <see cref="Name"/> bo'sh emasligini kafolatlaydi, lekin tip
    /// darajasida u baribir <c>null</c> bo'lishi mumkin (JSON'dan kelgan).
    /// Bu xossa null-forgiving operatorini (<c>!</c>) bitta joyda to'playdi.
    /// </remarks>
    public string SafeName => Name ?? string.Empty;
}

/// <summary>
/// Ma'lumotnoma yozuvlarini qayta tartiblash.
/// </summary>
/// <remarks>
/// Yangi tartib — ro'yxatdagi o'rin: birinchi identifikator 0, ikkinchisi 1…
/// Foydalanuvchi interfeysda sudrab tashlaydi, frontend esa yakuniy
/// ketma-ketlikni yuboradi. Har bir yozuv uchun alohida so'rov yuborilmaydi.
/// </remarks>
/// <param name="OrderedIds">Yangi ketma-ketlikdagi identifikatorlar.</param>
public sealed record ReorderReferenceCommand(IReadOnlyList<Guid> OrderedIds);
