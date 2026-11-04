using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Blankalar xatolari (SPEC-blankalar §7) — kodlari tarjimada (<c>web</c>: <c>errors.blanka.*</c>).
/// </summary>
/// <remarks>
/// Maydon darajasidagi xatolarda (<see cref="Required"/>, <see cref="Invalid"/>) maydon yo'li
/// <c>detail</c> sifatida qo'shiladi — buni Application qatlami qiladi, shu sababli bu yerda
/// faqat kod va umumiy matn.
/// </remarks>
public static class BlankErrors
{
    // ---------- Topilmadi ----------

    public static readonly Error NotFound =
        Error.NotFound("blanka.topilmadi", "Blanka topilmadi.");

    public static readonly Error TemplateNotFound =
        Error.NotFound("blanka.shablon_topilmadi", "Blanka shabloni topilmadi.");

    public static readonly Error StepNotFound =
        Error.NotFound("blanka.qadam_topilmadi", "Imzo qadami topilmadi.");

    public static readonly Error FileNotFound =
        Error.NotFound("blanka.fayl_topilmadi", "Fayl topilmadi.");

    public static readonly Error EmployeeNotFound =
        Error.NotFound("blanka.xodim_topilmadi", "Xodim topilmadi.");

    public static readonly Error CandidateNotFound =
        Error.NotFound("blanka.nomzod_topilmadi", "Nomzod topilmadi.");

    public static readonly Error UserNotFound =
        Error.NotFound("blanka.foydalanuvchi_topilmadi", "Foydalanuvchi topilmadi.");

    // ---------- Holat ----------

    public static readonly Error TemplateInactive =
        Error.Conflict("blanka.shablon_faol_emas", "Bu blanka shabloni faol emas — yangi blanka ochilmaydi.");

    public static readonly Error InvalidStatus =
        Error.Conflict("blanka.holat_notogri", "Blankaning joriy holatida bu amal mumkin emas.");

    public static readonly Error Stale =
        Error.Conflict("blanka.ozgargan", "Blankani boshqa foydalanuvchi o'zgartirdi — sahifani yangilang.");

    public static readonly Error FieldNotEditable =
        Error.Conflict("blanka.maydon_tahrirlanmaydi", "Bu maydonni hozir tahrirlab bo'lmaydi.");

    public static readonly Error StepNotOpen =
        Error.Conflict("blanka.qadam_ochiq_emas", "Bu qadam hozir imzo uchun ochiq emas.");

    public static readonly Error RejectNotAllowed =
        Error.Conflict("blanka.rad_mumkin_emas", "Bu qadamda rad etib bo'lmaydi.");

    public static readonly Error ReturnNotAllowed =
        Error.Conflict("blanka.qaytarish_mumkin_emas", "Bu qadamda qaytarib bo'lmaydi.");

    // ---------- Ruxsat ----------

    public static readonly Error StepForbidden =
        Error.Forbidden("blanka.qadam_ruxsat_yoq", "Bu qadamni siz imzolay olmaysiz.");

    public static readonly Error Forbidden =
        Error.Forbidden("blanka.ruxsat_yoq", "Bu amal uchun ruxsat yo'q.");

    // ---------- Kirish ma'lumoti ----------

    public static readonly Error SubjectMismatch =
        Error.Validation("blanka.subyekt_notogri", "Blanka kim haqida ekani shablonga mos emas.");

    public static readonly Error InvalidRole =
        Error.Validation("blanka.rol_notogri", "Bunday blanka roli yo'q.");

    public static readonly Error Required =
        Error.Validation("blanka.majburiy", "Majburiy maydon to'ldirilmagan.");

    public static readonly Error Invalid =
        Error.Validation("blanka.notogri", "Qiymat noto'g'ri.");

    public static readonly Error InvalidJson =
        Error.Validation("blanka.json", "Javoblarni o'qib bo'lmadi.");

    public static readonly Error TooLarge =
        Error.Validation("blanka.juda_katta", "Blanka hajmi chegaradan katta.");

    public static readonly Error DecisionRequired =
        Error.Validation("blanka.qaror_kerak", "Qaror tanlanishi shart.");

    public static readonly Error ReasonRequired =
        Error.Validation("blanka.sabab_kerak", "Sabab yoki izoh ko'rsatilishi shart.");

    public static readonly Error SignatureRequired =
        Error.Validation("blanka.imzo_kerak", "Imzo chizilishi shart.");

    public static readonly Error SignatureInvalid =
        Error.Validation("blanka.imzo_yaroqsiz", "Imzo rasmi yaroqsiz.");

    public static readonly Error SignerRequired =
        Error.Validation("blanka.imzochi_kerak", "Imzolovchining ismi ko'rsatilishi shart.");

    // ---------- Fayllar (SPEC §8) ----------

    public static readonly Error FileKind =
        Error.Validation("blanka.fayl_turi", "Bu turdagi fayl bu maydonga qabul qilinmaydi.");

    public static readonly Error FileLegacyFormat =
        Error.Validation("blanka.fayl_eski_format", "Eski formatdagi fayl (.doc, .xls) qabul qilinmaydi — yangi formatda saqlang.");

    public static readonly Error FileMacro =
        Error.Validation("blanka.fayl_makro", "Makrosli yoki shifrlangan fayl qabul qilinmaydi.");

    public static readonly Error FileTooLarge =
        Error.Validation("blanka.fayl_katta", "Fayl hajmi chegaradan katta.");

    public static readonly Error FileCount =
        Error.Validation("blanka.fayl_soni", "Bu maydonga boshqa fayl biriktirib bo'lmaydi — chegara to'lgan.");

    public static readonly Error FileEmpty =
        Error.Validation("blanka.fayl_bosh", "Fayl bo'sh.");
}
