using System.Text;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.ValueObjects;

/// <summary>
/// Pasport seriyasi va raqami: 2 ta lotin harfi + 7 ta raqam (masalan <c>AB5725936</c>).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Bu yerdagi asosiy muammo — <b>gomoglif</b>lar.
/// Bonvi Excel'idagi 538 ta pasportning ko'pchiligi <b>kirill</b> harflarida yozilgan:
/// <c>АВ5725936</c> dagi «А» — bu U+0410 (kirill), lotin «A» (U+0041) emas.
/// Ko'zga bir xil ko'rinadi, lekin kompyuter uchun butunlay boshqa belgi.
/// </para>
/// <para>
/// Natijada Excel'da lotin «AB» bo'yicha qidirilsa — hech narsa topilmaydi,
/// garchi yozuv ko'z oldida turgan bo'lsa ham. Excel'da 607 ta kirill va
/// 495 ta lotin harf aralashib ketgan.
/// </para>
/// <para>
/// Yechim: har bir pasport saqlashdan oldin lotinga normallashtiriladi.
/// Foydalanuvchi kirill klaviaturasida yozsa ham, tizim to'g'ri topadi.
/// </para>
/// </remarks>
public sealed class PassportSeries : ValueObject
{
    /// <summary>
    /// Kirill → lotin gomoglif jadvali.
    /// Faqat ko'rinishi bir xil bo'lgan harflar kiritilgan.
    /// </summary>
    private static readonly Dictionary<char, char> Homoglyphs = new()
    {
        ['А'] = 'A', ['В'] = 'B', ['Е'] = 'E', ['К'] = 'K', ['М'] = 'M',
        ['Н'] = 'H', ['О'] = 'O', ['Р'] = 'P', ['С'] = 'C', ['Т'] = 'T',
        ['У'] = 'Y', ['Х'] = 'X', ['І'] = 'I', ['Ј'] = 'J', ['Ѕ'] = 'S',
        ['а'] = 'A', ['в'] = 'B', ['е'] = 'E', ['к'] = 'K', ['м'] = 'M',
        ['н'] = 'H', ['о'] = 'O', ['р'] = 'P', ['с'] = 'C', ['т'] = 'T',
        ['у'] = 'Y', ['х'] = 'X',
    };

    private PassportSeries(string value) => Value = value;

    /// <summary>Normallashtirilgan qiymat: <c>AB5725936</c> (doim lotin, doim katta harf).</summary>
    public string Value { get; }

    /// <summary>Seriya — dastlabki 2 harf.</summary>
    public string Series => Value[..2];

    /// <summary>Raqam — keyingi 7 xona.</summary>
    public string Number => Value[2..];

    public static Result<PassportSeries> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("pasport.bosh", "Pasport ma'lumoti kiritilmagan.");
        }

        var normalized = Normalize(input);

        if (normalized.Length != 9)
        {
            return Error.Validation(
                "pasport.uzunlik",
                $"Pasport 2 harf va 7 raqamdan iborat bo'lishi kerak (masalan AB1234567). Kiritildi: «{input}».");
        }

        if (!char.IsAsciiLetterUpper(normalized[0]) || !char.IsAsciiLetterUpper(normalized[1]))
        {
            return Error.Validation(
                "pasport.seriya",
                "Pasport seriyasi 2 ta harfdan iborat bo'lishi kerak.");
        }

        for (var i = 2; i < 9; i++)
        {
            if (!char.IsAsciiDigit(normalized[i]))
            {
                return Error.Validation(
                    "pasport.raqam",
                    "Pasport raqami 7 ta raqamdan iborat bo'lishi kerak.");
            }
        }

        return new PassportSeries(normalized);
    }

    /// <summary>Tekshiruvsiz yaratadi. FAQAT bazadan o'qishda ishlatiladi.</summary>
    public static PassportSeries FromTrusted(string value) => new(value);

    /// <summary>
    /// Kirill gomogliflarini lotinga aylantiradi, bo'shliq va tirelarni olib tashlaydi,
    /// katta harfga keltiradi.
    /// </summary>
    public static string Normalize(string input)
    {
        var builder = new StringBuilder(input.Length);

        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '_' or '№')
            {
                continue;
            }

            if (Homoglyphs.TryGetValue(ch, out var latin))
            {
                builder.Append(latin);
            }
            else if (char.IsAsciiLetter(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
            else if (char.IsAsciiDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
