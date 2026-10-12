using Bonvi.Domain.Common;

namespace Bonvi.Application.Audit;

/// <summary>Audit jurnalini o'qish xatolari (SPEC-rollar-va-audit §8.4).</summary>
public static class AuditErrors
{
    /// <summary>Yozuv yo'q YOKI so'rovchiga ko'rinmaydi.</summary>
    /// <remarks>
    /// Ikki holat ATAYLAB bitta javob: «ko'rish huquqi yo'q» (403) deyilsa, bunday
    /// identifikatorli yozuv BORLIGI ma'lum bo'lib qolardi.
    /// </remarks>
    public static Error NotFound { get; } =
        Error.NotFound("audit.topilmadi", "Audit yozuvi topilmadi.");

    /// <summary>Davrning boshi oxiridan keyin.</summary>
    public static Error InvalidPeriod { get; } =
        Error.Validation("audit.davr_notogri", "Boshlanish sanasi tugash sanasidan keyin bo'lmasin.");

    /// <summary>
    /// Filtr qiymati notanish: bo'lim, amal, yozuv turi, ko'rinish yoki bajaruvchi
    /// (foydalanuvchi identifikatori ham, <c>system</c> ham emas).
    /// </summary>
    /// <remarks>
    /// Notanish qiymat jimgina e'tiborsiz qoldirilsa, filtrsiz to'liq ro'yxat chiqib,
    /// «filtr ishlayapti» degan yolg'on taassurot berardi.
    /// </remarks>
    /// <param name="parameter">So'rov parametrining nomi (<c>module</c>, <c>actor</c>…).</param>
    public static Error InvalidFilter(string parameter)
        => Error.Validation("audit.filtr_notogri", $"«{parameter}» filtrining qiymati noto'g'ri.")
            .WithDetails(new Dictionary<string, object> { ["parameter"] = parameter });
}
