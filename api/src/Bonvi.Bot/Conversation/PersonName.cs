namespace Bonvi.Bot.Conversation;

/// <summary>Uch bo'lakka ajratilgan F.I.Sh.</summary>
public sealed record PersonName(string LastName, string FirstName, string? MiddleName);

/// <summary>
/// Nomzod yozgan F.I.Sh. ni bo'laklarga ajratadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega botda ajratiladi.</b> API nomzodni <c>lastName</c>,
/// <c>firstName</c>, <c>middleName</c> bo'lib qabul qiladi, odam esa
/// yagona qatorda yozadi. Ajratish qoidasi domendagi
/// <c>FullName.Parse</c> bilan AYNAN bir xil: birinchi so'z — familiya,
/// ikkinchisi — ism, qolgani — otasining ismi. Ikkalasi ajralib ketsa,
/// botdan kelgan nomzod qo'lda kiritilganidan boshqacha yozilardi.
/// </para>
/// <para>
/// <b>Tekshiruv ATAYLAB yumshoq.</b> Bu yerda faqat aniq xatolar
/// to'siladi: bitta so'z, juda qisqa yoki juda uzun matn. Alifbo yoki
/// imlo tekshirilmaydi — odam o'z ismini qanday yozsa, shunday tushadi
/// va uni «to'g'rilashga» urinish begona ismlarni buzardi. Haqiqiy
/// tekshiruvni server qiladi.
/// </para>
/// </remarks>
public static class NameParser
{
    /// <summary>Eng qisqa qabul qilinadigan matn.</summary>
    private const int MinLength = 5;

    /// <summary>Eng uzun matn — server chegarasidan pastroq.</summary>
    private const int MaxLength = 200;

    /// <summary>
    /// Matnni bo'laklarga ajratadi.
    /// </summary>
    /// <returns>
    /// Ajratib bo'lmasa <c>null</c> — chaqiruvchi foydalanuvchidan
    /// qaytadan so'raydi.
    /// </returns>
    public static PersonName? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();

        if (trimmed.Length is < MinLength or > MaxLength)
        {
            return null;
        }

        // Bo'sh joylarning har qanday ko'rinishi (ikkita probel, tab)
        // bitta ajratgich sifatida ishlaydi — odam yozganda bu odatiy hol.
        var parts = trimmed.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            < 2 => null,
            2 => new PersonName(parts[0], parts[1], null),
            _ => new PersonName(parts[0], parts[1], string.Join(' ', parts[2..])),
        };
    }
}
