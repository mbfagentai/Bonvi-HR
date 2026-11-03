using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>
/// Javoblar tekshiruvining xatolari (SPEC-blankalar §7). Qaysi maydon ekani — xato tafsilotidagi
/// <c>field</c> va <c>detail</c> da (<c>questions[0].date</c>), kodlar kam va umumiy (anketadagi kabi).
/// </summary>
/// <remarks>
/// Kodlar Domain <c>BlankErrors</c> dagi bilan bir xil (S1 yaratadi); Application dvigateli Domain
/// blanka turlariga bog'lanmasligi uchun shu yerda ham e'lon qilingan.
/// </remarks>
public static class BlankAnswerErrors
{
    public static Error Unreadable { get; } =
        Error.Validation("blanka.json", "Blanka javoblari o'qilmadi.");

    public static Error Required { get; } =
        Error.Validation("blanka.majburiy", "Majburiy maydon to'ldirilmagan.");

    public static Error Invalid { get; } =
        Error.Validation("blanka.notogri", "Maydon qiymati noto'g'ri.");
}
