using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>
/// Process engine errors (docs/SPEC-mening-ishlarim.md §10, gate codes §8.1). The codes are
/// translated by the web client (<c>errors.jarayon.*</c>, 4 locales).
/// </summary>
public static class ProcessErrors
{
    // ---------- Not found ----------

    public static readonly Error NotFound =
        Error.NotFound("jarayon.topilmadi", "Jarayon topilmadi.");

    // ---------- State ----------

    public static readonly Error InvalidStepState =
        Error.Conflict("jarayon.qadam_holati_notogri", "Qadamning joriy holatida bu amal mumkin emas.");

    public static readonly Error InvalidStatus =
        Error.Conflict("jarayon.holat_notogri", "Joriy holatda bu amal mumkin emas.");

    public static readonly Error Stale =
        Error.Conflict("jarayon.versiya", "Jarayonni boshqa foydalanuvchi o'zgartirdi — sahifani yangilang.");

    /// <summary>A manual start for an employment that already has its case: one subject, one case (§3.2).</summary>
    public static readonly Error CaseAlreadyStarted =
        Error.Conflict("jarayon.allaqachon_ochilgan", "Bu xodim uchun bu jarayon allaqachon ochilgan.");

    /// <summary>A pass holds the engine lock for its moment: nothing was written, the same click works in a few seconds.</summary>
    public static readonly Error EngineBusy =
        Error.Conflict("jarayon.band", "Jarayonlar hozir hisoblanmoqda — bir necha soniyadan keyin qayta urining.");

    /// <summary>Reopen refused: what ended the case still holds, the very next pass would end it again (§7.5).</summary>
    public static readonly Error EndFactStillHolds =
        Error.Conflict("jarayon.yakun_kuchda", "Jarayonni yakunlagan holat hali o'zgarmagan — qayta ochilsa yana yakunlanadi.");

    public static readonly Error OverrideNotUsable =
        Error.Conflict("jarayon.istisno_yaroqsiz", "Istisno ishlatilgan yoki muddati o'tgan.");

    // ---------- Permission ----------

    public static readonly Error Forbidden =
        Error.Forbidden("jarayon.ruxsat_yoq", "Bu amal uchun ruxsat yo'q.");

    // ---------- Input ----------

    public static readonly Error Invalid =
        Error.Validation("jarayon.kiritish_notogri", "Kiritilgan qiymat noto'g'ri.");

    // ---------- Gates (§8.1) — blocking codes ----------

    /// <summary>Q01: the candidate has no HR-F02 form.</summary>
    public static readonly Error FormMissing =
        Error.Conflict("jarayon.anketa_yoq", "Nomzodning anketasi (HR-F02) yo'q.");

    /// <summary>Q01: no council decision «accept» / «conditional».</summary>
    public static readonly Error CouncilDecisionMissing =
        Error.Conflict("jarayon.kengash_qarori_yoq", "Kengash qarori (qabul yoki shartli qabul) yo'q.");

    /// <summary>Q01: a conditional decision without an «ok» / «watch» background check.</summary>
    public static readonly Error BackgroundCheckMissing =
        Error.Conflict("jarayon.surishtiruv_yoq", "Surishtiruv natijasi yo'q.");

    /// <summary>Q01: a hire without a candidate.</summary>
    public static readonly Error HireWithoutCandidate =
        Error.Conflict("jarayon.nomzodsiz_qabul", "Nomzodsiz ishga qabul qilib bo'lmaydi.");

    /// <summary>Q03: the employee has no JSHSHIR or the payment input is missing.</summary>
    public static readonly Error PinflMissing =
        Error.Conflict("jarayon.jshshir_yoq", "Xodimning JSHSHIR raqami yoki to'lov ma'lumoti yo'q.");

    /// <summary>Q04: the safety briefing step of the formalise case is not Done.</summary>
    public static readonly Error SafetyBriefingMissing =
        Error.Conflict("jarayon.yoriqnoma_yoq", "Mehnat muhofazasi yo'riqnomasi tasdiqlanmagan.");

    /// <summary>Q12: no Approved HR-F09 handover act for the employment.</summary>
    public static readonly Error HandoverActOpen =
        Error.Conflict("jarayon.akt_yopilmagan", "Ishlarni topshirish akti (HR-F09) tasdiqlanmagan.");

    /// <summary>Q12: an override is honoured only on or after the termination date.</summary>
    public static readonly Error OverrideOnlyOnLastDay =
        Error.Conflict("jarayon.istisno_oxirgi_kunda", "Istisno faqat oxirgi ish kunida yoki undan keyin amal qiladi.");

    /// <summary>Q13: no HR-F08 exit interview with the main reason code.</summary>
    public static readonly Error ExitInterviewMissing =
        Error.Conflict("jarayon.chiqish_suhbati_yoq", "Chiqish suhbati (HR-F08) o'tkazilmagan.");

    // ---------- Gates — warning codes (HrWarning, never an error) ----------

    /// <summary>Q01 warning: the hire order number is missing.</summary>
    public const string HireOrderNumberMissingWarning = "jarayon.buyruq_raqami_yoq";
}
