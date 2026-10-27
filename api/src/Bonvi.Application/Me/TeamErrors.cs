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
}
