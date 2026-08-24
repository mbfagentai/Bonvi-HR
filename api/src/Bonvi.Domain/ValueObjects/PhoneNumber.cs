using Bonvi.Domain.Common;

namespace Bonvi.Domain.ValueObjects;

/// <summary>
/// O'zbekiston telefon raqami. Doim <c>+998XXXXXXXXX</c> ko'rinishida saqlanadi.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da raqamlar turli ko'rinishda: <c>905088888</c>, <c>+998 90 508 88 88</c>,
/// <c>90-508-88-88</c>. Qidiruv ishlashi uchun bitta ko'rinishga keltiriladi.
/// </para>
/// <para>
/// Bonvi ma'lumotidagi operator kodlari: 90, 91, 93, 94, 95, 97, 98, 99,
/// 88, 77, 50, 33, 70, 20. Ro'yxat "oq ro'yxat" sifatida ishlatilmaydi —
/// yangi kodlar chiqadi, shu sababli faqat uzunlik va boshlanish tekshiriladi.
/// </para>
/// </remarks>
public sealed class PhoneNumber : ValueObject
{
    private const string CountryCode = "998";

    private PhoneNumber(string value) => Value = value;

    /// <summary>Normallashtirilgan qiymat: <c>+998905088888</c>.</summary>
    public string Value { get; }

    /// <summary>Operator kodi (masalan <c>90</c>).</summary>
    public string OperatorCode => Value.Substring(4, 2);

    /// <summary>Ko'rsatish uchun: <c>+998 90 508 88 88</c>.</summary>
    public string Formatted =>
        $"+{CountryCode} {Value.Substring(4, 2)} {Value.Substring(6, 3)} {Value.Substring(9, 2)} {Value.Substring(11, 2)}";

    public static Result<PhoneNumber> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("telefon.bosh", "Telefon raqami kiritilmagan.");
        }

        var digits = new string([.. input.Where(char.IsAsciiDigit)]);

        // 9 xonali — mamlakat kodisiz kiritilgan (Excel'dagi eng keng tarqalgan ko'rinish)
        if (digits.Length == 9)
        {
            digits = CountryCode + digits;
        }

        // To'liq uzunlikdagi, lekin chet el raqami (masalan +7 916 123 45 67 — Rossiya).
        // Bu holatda "uzunlik noto'g'ri" emas, "boshqa davlat raqami" deb aytish kerak —
        // aks holda foydalanuvchi raqamni qayta-qayta sanab chiqadi.
        if (digits.Length is >= 10 and <= 15 && !digits.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            return Error.Validation(
                "telefon.mamlakat_kodi",
                $"Faqat O'zbekiston raqamlari qabul qilinadi (+998). Kiritildi: «{input}».");
        }

        if (digits.Length != 12)
        {
            return Error.Validation(
                "telefon.uzunlik",
                $"Telefon raqami 9 xonadan iborat bo'lishi kerak (masalan 905088888). Kiritildi: «{input}».");
        }

        if (digits[3] is '0' or '1')
        {
            return Error.Validation(
                "telefon.operator",
                "Operator kodi noto'g'ri — 0 yoki 1 bilan boshlanishi mumkin emas.");
        }

        return new PhoneNumber("+" + digits);
    }

    /// <summary>Tekshiruvsiz yaratadi. FAQAT bazadan o'qishda ishlatiladi.</summary>
    public static PhoneNumber FromTrusted(string value) => new(value);

    /// <summary>
    /// Bitta katakda bir nechta raqam bo'lsa ajratadi.
    /// Excel'da 13 ta yozuvda 18 xona (ikki raqam), bittasida 36 xona (to'rt raqam) bor.
    /// </summary>
    public static IReadOnlyList<string> SplitMultiple(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var digits = new string([.. input.Where(char.IsAsciiDigit)]);

        // Ajratgichli ko'rinish (vergul, slash, nuqta-vergul) — avval shuni sinaymiz
        var parts = input.Split([',', ';', '/', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
        {
            return parts;
        }

        // Ajratgichsiz yopishib qolgan raqamlar: 9 yoki 12 xonalik bo'laklarga bo'linadi
        if (digits.Length is 18 or 27 or 36 && digits.Length % 9 == 0)
        {
            return [.. Enumerable.Range(0, digits.Length / 9).Select(i => digits.Substring(i * 9, 9))];
        }

        if (digits.Length is 24 or 36 && digits.Length % 12 == 0)
        {
            return [.. Enumerable.Range(0, digits.Length / 12).Select(i => digits.Substring(i * 12, 12))];
        }

        return [input];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
