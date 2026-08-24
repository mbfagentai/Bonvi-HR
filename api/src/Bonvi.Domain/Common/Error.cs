namespace Bonvi.Domain.Common;

/// <summary>Xato turi — API qatlamida HTTP statusiga aylantiriladi.</summary>
public enum ErrorType
{
    /// <summary>Kutilmagan ichki xato → 500</summary>
    Failure = 0,

    /// <summary>Kirish ma'lumoti noto'g'ri → 400</summary>
    Validation = 1,

    /// <summary>Topilmadi → 404</summary>
    NotFound = 2,

    /// <summary>Joriy holatda bu amal mumkin emas → 409</summary>
    Conflict = 3,

    /// <summary>Ruxsat yo'q → 403</summary>
    Forbidden = 4,
}

/// <summary>
/// Biznes xatosi. Kod — mashina uchun, xabar — foydalanuvchi uchun (o'zbekcha).
/// </summary>
/// <remarks>
/// Kutilgan xatolar uchun istisno (exception) TASHLANMAYDI.
/// Sabab: "xodim topilmadi" — bu dastur xatosi emas, oddiy natija.
/// Istisno faqat haqiqiy nosozlikda ishlatiladi.
/// </remarks>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public override string ToString() => $"{Code}: {Message}";
}
