using System.Globalization;
using System.Security.Cryptography;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>
/// Xodim ilovasi parolining QOIDALARI — bitta joyda: avtomatik yasash va HR yozgan parolni tekshirish.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega 6 raqam</b> (egasining qarori, 2026-09-27). Parolni HR beradi,
/// og'zaki aytadi yoki qog'ozga yozib beradi; xodimlarning katta qismi
/// telefonda harf terishga o'rganmagan. Olti raqam — PIN-kod kabi tanish.
/// Taxmin qilishga qarshi himoya parolning uzunligida EMAS, kirish
/// yo'lidagi blokda: (login, IP) juftligiga 15 daqiqada 5 urinish
/// (<c>EmployeeLoginThrottle</c>) va IP bo'yicha tezlik cheklovi.
/// </para>
/// <para>
/// ⚠️ <b>Arzimas birikmalar RAD etiladi</b>: bir xil raqamlar (000000),
/// ketma-ket o'suvchi yoki kamayuvchi qator (123456, 654321) va takrorlanuvchi
/// bo'lak (121212, 123123). Ular birinchi navbatda terib ko'riladi va
/// tasodifiy yasalgan parolda ham chiqib qolishi mumkin (millionda bir
/// necha o'nta).
/// </para>
/// <para>
/// HR O'ZI yozgan parol ham shu arzimaslik tekshiruvidan o'tadi (2026-10-12,
/// ilova internetga ochilgach; 2026-09-30 xavfsizlik ko'rigining bandi). Ilgari
/// faqat uzunlik tekshirilardi — «xodimning o'zi so'ragan parol» deb. Lekin
/// 000000 yoki 123456 hujumchi birinchi terib ko'radigan qiymatlar va ular
/// 15 daqiqada 5 urinishli blokdan ham oldin topiladi. Harfli parol esa
/// tekshirilmaydi — u raqamli PIN emas.
/// </para>
/// </remarks>
public static class EmployeePassword
{
    /// <summary>Avtomatik parolning uzunligi — raqam.</summary>
    public const int GeneratedLength = 6;

    /// <summary>HR yozgan parolning eng qisqa uzunligi.</summary>
    public const int MinLength = 6;

    /// <summary>HR yozgan parolning eng uzun uzunligi.</summary>
    /// <remarks>
    /// Chegara bo'lishi SHART: cheklanmagan satr xeshlagichga bir necha
    /// megabaytlik ish berib, bitta so'rov bilan serverni band qilardi.
    /// </remarks>
    public const int MaxLength = 64;

    /// <summary>Yangi 6 xonali parol — kriptografik tasodif bilan, arzimaslari tashlab.</summary>
    /// <remarks>
    /// <c>RandomNumberGenerator.GetInt32</c> — bir tekis taqsimotni o'zi
    /// kafolatlaydi (qo'lda yozilgan <c>% 1000000</c> siljish berardi).
    /// Rad etilgan son qayta tortiladi — ehtimol millionda bir necha o'nta.
    /// </remarks>
    public static string Generate()
    {
        while (true)
        {
            var candidate = RandomNumberGenerator
                .GetInt32(0, 1_000_000)
                .ToString("D6", CultureInfo.InvariantCulture);

            if (!IsTriviallyWeak(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Raqamli parol arzimas birikmami (sinf izohidagi uchta naqsh).</summary>
    /// <param name="digits">Faqat raqamlardan iborat satr.</param>
    public static bool IsTriviallyWeak(string digits)
    {
        ArgumentException.ThrowIfNullOrEmpty(digits);

        if (digits.Any(ch => !char.IsAsciiDigit(ch)))
        {
            return false;
        }

        return IsRun(digits, step: 0)
            || IsRun(digits, step: 1)
            || IsRun(digits, step: -1)
            || IsRepeatedBlock(digits, 2)
            || IsRepeatedBlock(digits, 3);
    }

    /// <summary>
    /// HR yozgan parolni tekshiradi va saqlanadigan ko'rinishini qaytaradi.
    /// </summary>
    /// <remarks>
    /// Chetdagi bo'shliqlar OLIB TASHLANADI: nusxalashda tasodifan qo'shilgan
    /// bo'shliq bilan saqlangan parolni xodim hech qachon to'g'ri terolmasdi.
    /// HR esa saqlangan parolni istalgan payt ko'radi — ya'ni ko'rgani AYNAN
    /// ishlaydigan qiymat.
    /// </remarks>
    /// <param name="password">HR kiritgan matn.</param>
    public static Result<string> Validate(string? password)
    {
        var value = password?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            return EmployeeAccountErrors.PasswordEmpty;
        }

        if (value.Length < MinLength)
        {
            return EmployeeAccountErrors.PasswordTooShort;
        }

        if (value.Length > MaxLength)
        {
            return EmployeeAccountErrors.PasswordTooLong;
        }

        if (IsTriviallyWeak(value))
        {
            return EmployeeAccountErrors.PasswordTooSimple;
        }

        return value;
    }

    /// <summary>Har bir raqam oldingisidan <paramref name="step"/> ga farq qiladi.</summary>
    private static bool IsRun(string digits, int step)
    {
        for (var i = 1; i < digits.Length; i++)
        {
            if (digits[i] - digits[i - 1] != step)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Satr <paramref name="size"/> uzunlikdagi bo'lakning takroridan iborat.</summary>
    private static bool IsRepeatedBlock(string digits, int size)
    {
        if (digits.Length % size != 0 || digits.Length == size)
        {
            return false;
        }

        for (var i = size; i < digits.Length; i++)
        {
            if (digits[i] != digits[i - size])
            {
                return false;
            }
        }

        return true;
    }
}
