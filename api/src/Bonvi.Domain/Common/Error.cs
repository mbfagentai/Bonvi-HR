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

    /// <summary>
    /// Shu bilan BIRGA kuchda bo'lgan boshqa sabablar kodlari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta amal bir nechta qoidaga urilishi mumkin: xodim bir vaqtda ham
    /// qora ro'yxatda, ham nazoratda bo'ladi. <see cref="Code"/> da eng
    /// jiddiysi qaytadi (interfeys unga qarab rang va matn tanlaydi),
    /// qolganlari esa shu yerda — HR to'liq rasmni ko'rishi kerak.
    /// </para>
    /// <para>
    /// Nega faqat KOD, matn emas: matnni foydalanuvchi tilida frontend
    /// hosil qiladi. Serverdan tayyor matn yuborilsa, u har doim
    /// o'zbekcha bo'lib qolardi — tanlangan tildan qat'i nazar.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Related { get; init; } = [];

    /// <summary>Yonma-yon kuchda bo'lgan sabablarni biriktiradi.</summary>
    /// <remarks>
    /// Asosiy xatoning o'zi ro'yxatga TUSHMAYDI: u allaqachon
    /// <see cref="Code"/> da va ikki marta ko'rsatilishi kerak emas.
    /// </remarks>
    public Error With(params string[] related)
    {
        ArgumentNullException.ThrowIfNull(related);

        return this with
        {
            Related = [.. related.Where(code => !string.IsNullOrWhiteSpace(code) && code != Code)],
        };
    }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public override string ToString() => $"{Code}: {Message}";
}
