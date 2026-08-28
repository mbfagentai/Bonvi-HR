using Bonvi.Domain.Common;

namespace Bonvi.Application.Recruiting.Common;

/// <summary>
/// Rekruting (2-bosqich) bo'yicha use-case xatolari.
/// </summary>
/// <remarks>
/// Domen xatolari (masalan «nomzod.jarayonda_emas») bu yerda
/// TAKRORLANMAYDI: ular agregatning o'zida yashaydi va shu yerda qayta
/// e'lon qilinsa, ikkita haqiqat paydo bo'lardi. Bu yerda faqat
/// use-case ko'radigan holatlar — «topilmadi» va boshqa agregatlar bilan
/// bog'liq ziddiyatlar.
/// </remarks>
public static class RecruitingErrors
{
    // ---------- Vakansiya ----------

    public static Error VacancyNotFound { get; } =
        Error.NotFound("vakansiya.topilmadi", "Vakansiya topilmadi.");

    public static Error PositionNotFound { get; } =
        Error.NotFound("vakansiya.lavozim_topilmadi", "Lavozim topilmadi.");

    public static Error LegalEntityNotFound { get; } =
        Error.NotFound("vakansiya.yuridik_shaxs_topilmadi", "Yuridik shaxs topilmadi.");

    public static Error DepartmentNotFound { get; } =
        Error.NotFound("vakansiya.bolim_topilmadi", "Bo'lim topilmadi.");

    public static Error ResponsibleNotFound { get; } =
        Error.NotFound("vakansiya.masul_topilmadi", "Mas'ul xodim topilmadi.");

    /// <summary>Yopilgan yoki to'xtatilgan vakansiyaga nomzod kiritilmaydi.</summary>
    public static Error VacancyNotAccepting { get; } = Error.Conflict(
        "vakansiya.qabul_qilmaydi",
        "Vakansiya ochiq emas: unga yangi nomzod kiritib bo'lmaydi.");

    // ---------- Bosqich ----------

    public static Error StageNotFound { get; } =
        Error.NotFound("bosqich.topilmadi", "Voronka bosqichi topilmadi.");

    /// <summary>Faolsizlantirilgan bosqichga nomzod o'tkazilmaydi.</summary>
    /// <remarks>
    /// Bosqich ma'lumotnoma yozuvi va u hech qachon o'chirilmaydi, faqat
    /// faolsizlantiriladi. Unda TURGAN nomzodlar joyida qoladi va bosqich
    /// nomi ularning kartasida ko'rinib turadi — aks holda tarix
    /// «noma'lum bosqich» bo'lib qolardi. Lekin YANGI nomzodni u yerga
    /// o'tkazib bo'lmaydi: faolsizlantirishning butun ma'nosi shu.
    /// </remarks>
    public static Error StageInactive { get; } = Error.Conflict(
        "bosqich.faol_emas",
        "Bosqich faol emas: nomzodni unga o'tkazib bo'lmaydi.");

    /// <summary>Voronkada bironta ham faol bosqich yo'q.</summary>
    /// <remarks>
    /// Nomzodni voronkasiz kiritish mumkin emas: u qayerda turgani
    /// noma'lum bo'lardi va hech qaysi ro'yxatga tushmasdi. Urug' fayli
    /// oltita bosqichni beradi, ya'ni bu holat faqat HR hammasini
    /// faolsizlantirsa yuzaga keladi.
    /// </remarks>
    public static Error NoActiveStages { get; } = Error.Conflict(
        "bosqich.faol_yoq",
        "Voronkada bironta ham faol bosqich yo'q — avval bosqich qo'shing.");

    public static Error StageNameTaken { get; } =
        Error.Conflict("bosqich.nom_band", "Bunday nomli bosqich allaqachon bor.");

    /// <summary>Qayta tartiblashda ro'yxat to'liq emas.</summary>
    public static Error ReorderIncomplete { get; } = Error.Validation(
        "bosqich.tartib_royxati",
        "Qayta tartiblashda BARCHA bosqichlar ro'yxatda bo'lishi kerak.");

    // ---------- Nomzod ----------

    public static Error CandidateNotFound { get; } =
        Error.NotFound("nomzod.topilmadi", "Nomzod topilmadi.");

    public static Error ResumeNotFound { get; } =
        Error.NotFound("nomzod.rezyume_topilmadi", "Nomzodning rezyumesi yo'q.");

    /// <summary>Fayl chegaradan katta.</summary>
    public static Error ResumeTooLarge { get; } = Error.Validation(
        "nomzod.rezyume_katta",
        "Rezyume fayli juda katta (eng ko'pi 20 MB).");

    /// <summary>Fayl mazmuni tanilmadi.</summary>
    public static Error ResumeInvalid { get; } = Error.Validation(
        "nomzod.rezyume_yaroqsiz",
        "Rezyume PDF, JPEG, PNG yoki WebP emas (yoki buzilgan).");

    // ---------- Taklif ----------

    public static Error OfferNotFound { get; } =
        Error.NotFound("taklif.topilmadi", "Ish taklifi topilmadi.");

    /// <summary>Nomzodda javob kutayotgan taklif allaqachon bor.</summary>
    public static Error OfferAlreadyPending { get; } = Error.Conflict(
        "taklif.faol_mavjud",
        "Nomzodga yuborilgan va javobi kelmagan taklif bor — avval unga javob yozing.");

    /// <summary>Nomzod allaqachon ishga olingan (takroriy qabul).</summary>
    /// <remarks>
    /// Ikki xil yo'l bilan yuzaga chiqadi: nomzodning holati allaqachon
    /// «ishga olingan» yoki uning nomiga ishga qabul hodisasi topilgan.
    /// Ikkinchisi — birinchi urinish yarim yo'lda uzilgan holat va
    /// takroriy urinish IKKINCHI xodim kartasini yaratmasligi kerak
    /// (batafsil <c>AcceptJobOfferCommandHandler</c> izohida).
    /// </remarks>
    public static Error AlreadyHired { get; } = Error.Conflict(
        "nomzod.allaqachon_ishga_olingan",
        "Nomzod allaqachon ishga olingan.");
}
