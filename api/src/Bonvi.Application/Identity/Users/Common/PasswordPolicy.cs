using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// Administrator tomonidan qo'lda kiritiladigan parolga qo'yiladigan
/// minimal talab.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega FluentValidation emas.</b> Autentifikatsiya bo'limi (<c>Login</c>,
/// <c>User.Create</c>) allaqachon oddiy <see cref="Result"/> tekshiruvlari
/// bilan yozilgan — bu yerda ham xuddi shu uslub davom ettiriladi, yangi
/// bog'liqlik yo'q.
/// </para>
/// <para>
/// <b>Nega faqat uzunlik.</b> Tizimda bor-yo'g'i 4 ta hisob va ularning
/// barchasini BITTA administrator yaratadi/tiklaydi — murakkab qoidalar
/// (katta-kichik harf, raqam, maxsus belgi) bu yerda haqiqiy xavfsizlik
/// bermaydi, faqat administratorga ortiqcha to'siq bo'ladi. 8 belgi — umumiy
/// qabul qilingan minimal chegara.
/// </para>
/// </remarks>
public static class PasswordPolicy
{
    /// <summary>Eng kam uzunlik.</summary>
    public const int MinLength = 8;

    public static Result Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Error.Validation("foydalanuvchi.parol_bosh", "Parol kiritilishi shart.");
        }

        if (password.Length < MinLength)
        {
            return Error.Validation(
                "foydalanuvchi.parol_qisqa",
                $"Parol kamida {MinLength} belgidan iborat bo'lishi kerak.");
        }

        return Result.Success();
    }
}
