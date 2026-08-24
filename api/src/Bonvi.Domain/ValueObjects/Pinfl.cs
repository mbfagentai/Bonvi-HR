using System.Globalization;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.ValueObjects;

/// <summary>Xodimning jinsi (JSHSHIR birinchi raqamidan aniqlanadi).</summary>
public enum Gender
{
    Male = 1,
    Female = 2,
}

/// <summary>
/// JSHSHIR — Jismoniy Shaxsning Shaxsiy Identifikatsiya Raqami (14 xona).
/// Tizimning asosiy identifikatori: pasport almashadi, JSHSHIR — hech qachon.
/// </summary>
/// <remarks>
/// <para>Tuzilishi:</para>
/// <list type="bullet">
///   <item>[0]     — jins va tug'ilgan asr: 1/2 → 1800-lar, 3/4 → 1900-lar, 5/6 → 2000-lar
///                   (toq — erkak, juft — ayol)</item>
///   <item>[1..6]  — tug'ilgan sana: KKOOYY</item>
///   <item>[7..12] — hudud kodi va tartib raqami</item>
///   <item>[13]    — nazorat raqami</item>
/// </list>
/// <para>
/// Nazorat raqami algoritmi Bonvi'ning 535 ta haqiqiy yozuvida tekshirilgan:
/// 98% mos keldi. Mos kelmagan 2% — Excel'dagi haqiqiy kiritish xatolari
/// (masalan <c>21707764200043</c> — "1876-yilda tug'ilgan" deb chiqadi).
/// </para>
/// </remarks>
public sealed class Pinfl : ValueObject
{
    /// <summary>Nazorat raqamini hisoblash og'irliklari.</summary>
    private static readonly int[] Weights = [7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1, 7];

    private Pinfl(string value) => Value = value;

    /// <summary>14 xonali raqam, faqat raqamlardan iborat.</summary>
    public string Value { get; }

    /// <summary>JSHSHIR ichiga kodlangan jins.</summary>
    public Gender Gender => (Value[0] - '0') % 2 == 1 ? Gender.Male : Gender.Female;

    /// <summary>JSHSHIR ichiga kodlangan tug'ilgan sana.</summary>
    public DateOnly BirthDate
    {
        get
        {
            var century = (Value[0] - '0') switch
            {
                1 or 2 => 1800,
                3 or 4 => 1900,
                _ => 2000,
            };

            var day = int.Parse(Value.AsSpan(1, 2), CultureInfo.InvariantCulture);
            var month = int.Parse(Value.AsSpan(3, 2), CultureInfo.InvariantCulture);
            var year = int.Parse(Value.AsSpan(5, 2), CultureInfo.InvariantCulture);

            return new DateOnly(century + year, month, day);
        }
    }

    /// <summary>
    /// JSHSHIR yaratadi. Format, nazorat raqami va sana haqiqiyligi tekshiriladi.
    /// </summary>
    public static Result<Pinfl> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("pinfl.bosh", "JSHSHIR kiritilmagan.");
        }

        var digits = new string([.. input.Where(char.IsAsciiDigit)]);

        if (digits.Length != 14)
        {
            return Error.Validation(
                "pinfl.uzunlik",
                $"JSHSHIR 14 xonadan iborat bo'lishi kerak, kiritildi: {digits.Length} xona.");
        }

        if (digits[0] is < '1' or > '6')
        {
            return Error.Validation(
                "pinfl.birinchi_raqam",
                "JSHSHIR birinchi raqami 1 dan 6 gacha bo'lishi kerak (jins va tug'ilgan asr).");
        }

        if (!TryReadBirthDate(digits, out _))
        {
            return Error.Validation(
                "pinfl.sana",
                "JSHSHIR ichidagi tug'ilgan sana mavjud emas.");
        }

        if (!IsChecksumValid(digits))
        {
            return Error.Validation(
                "pinfl.nazorat_raqami",
                "JSHSHIR nazorat raqami mos kelmadi — raqam noto'g'ri kiritilgan.");
        }

        return new Pinfl(digits);
    }

    /// <summary>
    /// Tekshiruvsiz yaratadi. FAQAT bazadan o'qishda (EF Core) ishlatiladi —
    /// u yerdagi qiymat allaqachon tekshirilgan.
    /// </summary>
    public static Pinfl FromTrusted(string value) => new(value);

    /// <summary>Nazorat raqami to'g'riligini tekshiradi.</summary>
    public static bool IsChecksumValid(string digits)
    {
        if (digits.Length != 14)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (digits[i] - '0') * Weights[i];
        }

        return sum % 10 == digits[13] - '0';
    }

    /// <summary>
    /// JSHSHIR'dagi tug'ilgan sana kiritilgan sana bilan mos kelishini tekshiradi.
    /// Excel'da 527 ta yozuvdan 32 tasi mos kelmagan — bu kiritish xatolarini ochadi.
    /// </summary>
    public bool MatchesBirthDate(DateOnly birthDate) => BirthDate == birthDate;

    private static bool TryReadBirthDate(string digits, out DateOnly date)
    {
        date = default;

        var century = (digits[0] - '0') switch
        {
            1 or 2 => 1800,
            3 or 4 => 1900,
            _ => 2000,
        };

        var day = int.Parse(digits.AsSpan(1, 2), CultureInfo.InvariantCulture);
        var month = int.Parse(digits.AsSpan(3, 2), CultureInfo.InvariantCulture);
        var year = int.Parse(digits.AsSpan(5, 2), CultureInfo.InvariantCulture);

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(century + year, month))
        {
            return false;
        }

        date = new DateOnly(century + year, month, day);
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
