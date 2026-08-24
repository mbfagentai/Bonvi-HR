using Bonvi.Domain.Common;
using FluentValidation.Results;

namespace Bonvi.Application.Catalog.Common;

/// <summary>
/// FluentValidation natijasini domen xatosiga aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// Mediator kutubxonasi yo'q, ya'ni validatsiya "pipeline behavior" orqali
/// avtomatik bajarilmaydi — har bir handler uni ataylab chaqiradi.
/// Bu ko'proq yozuv, lekin qaysi buyruq tekshirilishi kodda ko'rinib turadi.
/// </para>
/// <para>
/// Faqat BIRINCHI xato qaytariladi: <see cref="Error"/> bitta kod va bitta
/// xabar saqlaydi, forma esa baribir birinchi muammoni ko'rsatadi.
/// </para>
/// </remarks>
internal static class ValidationErrorExtensions
{
    /// <summary>Zaxira kod: validator kod ko'rsatishni unutgan bo'lsa.</summary>
    private const string FallbackCode = "malumotnoma.notogri";

    public static Error ToError(this ValidationResult result)
    {
        var failure = result.Errors[0];

        return Error.Validation(
            string.IsNullOrWhiteSpace(failure.ErrorCode) ? FallbackCode : failure.ErrorCode,
            failure.ErrorMessage);
    }
}
