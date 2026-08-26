using Bonvi.Domain.Common;

namespace Bonvi.Application.Feedback.Common;

/// <summary>«Qayta aloqa» (exit interview, M10) bo'yicha xatolar.</summary>
public static class FeedbackErrors
{
    public static Error NotFound { get; } =
        Error.NotFound("qayta_aloqa.topilmadi", "Qayta aloqa yozuvi topilmadi.");

    public static Error EmploymentNotFound { get; } =
        Error.NotFound("qayta_aloqa.mehnat_topilmadi", "Mehnat munosabati topilmadi.");

    /// <summary>
    /// Suhbat faqat YAKUNLANGAN mehnat munosabati uchun yoziladi.
    /// </summary>
    /// <remarks>
    /// «Exit interview» — ATAMASINING o'zi ishdan ketishni nazarda tutadi.
    /// Hali ishlab turgan xodimga bu yozuv ochilsa, u tizimda «ketayotgan»
    /// bo'lib ko'rinib, HR ni chalg'itardi.
    /// </remarks>
    public static Error EmploymentNotTerminated { get; } =
        Error.Conflict(
            "qayta_aloqa.mehnat_tugamagan",
            "Qayta aloqa faqat ishdan bo'shatilgan xodim uchun yoziladi.");

    /// <summary>Bitta mehnat munosabatiga bittadan ortiq suhbat yozilmaydi.</summary>
    public static Error AlreadyRecorded { get; } =
        Error.Conflict(
            "qayta_aloqa.allaqachon_bor",
            "Bu mehnat munosabati uchun qayta aloqa yozuvi allaqachon mavjud.");
}
