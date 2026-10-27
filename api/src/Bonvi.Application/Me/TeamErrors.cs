using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>«Jamoam» endpointlarining xatolari (SPEC-jamoam §C).</summary>
/// <remarks>
/// Kodlar ilovada tarjima qilinadi — matn faqat zaxira (<c>CatalogErrors</c> bilan bir xil qoida).
/// </remarks>
public static class TeamErrors
{
    /// <summary>So'rovchi hech qaysi o'chirilmagan bo'limning boshlig'i emas.</summary>
    /// <remarks>
    /// Faolsiz bo'lim HAM hisoblanadi (§B): huquqni HR boshliqni olib tashlab yopadi,
    /// bo'limni faolsizlantirib emas.
    /// </remarks>
    public static Error NotTeamLead { get; } =
        Error.Forbidden("jamoa.boshliq_emas", "Siz hech qaysi bo'limning boshlig'i emassiz.");

    /// <summary>Xodim qamrovdan tashqarida — yoki umuman yo'q.</summary>
    /// <remarks>
    /// ⚠️ «Yo'q» va «sizniki emas» ATAYLAB bitta javob: aks holda boshliq identifikatorlarni
    /// sinab, qaysi biri korxonada borligini bilib olardi.
    /// </remarks>
    public static Error MemberNotFound { get; } =
        Error.NotFound("jamoa.xodim_topilmadi", "Xodim topilmadi.");

    /// <summary>Berilgan bo'lim qamrovdan tashqarida (yoki yo'q) — xuddi shu sababdan bitta javob.</summary>
    public static Error DepartmentNotFound { get; } =
        Error.NotFound("jamoa.bolim_topilmadi", "Bo'lim topilmadi.");

    /// <summary>Panel sanasi [2000-01-01, bugun (Toshkent)] dan tashqarida.</summary>
    /// <remarks>
    /// Kelajak sanasida panel hech narsa bilmaydi, 2000-yildan oldingisi esa haqiqatga to'g'ri
    /// kelmaydi (<c>AttendanceQueryParsing.MinYear</c>) — ikkalasi ham bo'sh panel qaytarib,
    /// xatoni yashirardi. <c>9999-12-31</c> esa sana arifmetikasida 500 berardi.
    /// </remarks>
    public static Error DateInvalid { get; } =
        Error.Validation("jamoa.sana_notogri", "Sana 2000-01-01 dan bugungacha bo'lishi kerak.");

    /// <summary>So'ralgan oy joriy Toshkent oyidan keyin.</summary>
    /// <remarks>
    /// Kod tabel oyi xatosi bilan BIR XIL (<c>davomat.oy_notogri</c>) — ilova uni allaqachon
    /// tarjima qiladi; matn esa aniqroq.
    /// </remarks>
    public static Error FutureMonth { get; } =
        Error.Validation("davomat.oy_notogri", "Kelgusi oyni ko'rib bo'lmaydi.");
}
