using Bonvi.Domain.Common;

namespace Bonvi.Application.Safety.Common;

/// <summary>Baxtsiz hodisalar jurnali (M11) bo'yicha xatolar.</summary>
public static class SafetyErrors
{
    public static Error NotFound { get; } =
        Error.NotFound("baxtsiz_hodisa.topilmadi", "Baxtsiz hodisa yozuvi topilmadi.");

    public static Error EmployeeNotFound { get; } =
        Error.NotFound("baxtsiz_hodisa.xodim_topilmadi", "Xodim topilmadi.");

    public static Error PositionNotFound { get; } =
        Error.NotFound("baxtsiz_hodisa.lavozim_topilmadi", "Lavozim topilmadi.");

    public static Error ResponsibleNotFound { get; } =
        Error.NotFound("baxtsiz_hodisa.masul_topilmadi", "Mas'ul xodim topilmadi.");
}
