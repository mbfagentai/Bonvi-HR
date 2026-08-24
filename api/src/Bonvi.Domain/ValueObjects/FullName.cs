using Bonvi.Domain.Common;

namespace Bonvi.Domain.ValueObjects;

/// <summary>
/// Xodimning to'liq ismi: familiya, ism, otasining ismi.
/// </summary>
/// <remarks>
/// Excel'da uchalasi bitta katakda: «Мамажонов Бобуржон Фазлитдин ўғли».
/// Bu yerda ular ajratiladi — shunda familiya bo'yicha saralash va
/// qidiruv to'g'ri ishlaydi.
/// </remarks>
public sealed class FullName : ValueObject
{
    /// <summary>Otasining ismiga qo'shiladigan qo'shimchalar (ajratishda hisobga olinadi).</summary>
    private static readonly string[] PatronymicSuffixes =
    [
        "ўғли", "o'g'li", "ogli", "угли", "qizi", "қизи", "kizi",
    ];

    private FullName(string lastName, string firstName, string? middleName)
    {
        LastName = lastName;
        FirstName = firstName;
        MiddleName = middleName;
    }

    /// <summary>Familiya.</summary>
    public string LastName { get; }

    /// <summary>Ism.</summary>
    public string FirstName { get; }

    /// <summary>Otasining ismi (bo'lmasligi mumkin).</summary>
    public string? MiddleName { get; }

    /// <summary>To'liq ko'rinish: «Мамажонов Бобуржон Фазлитдин ўғли».</summary>
    public string Full => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{LastName} {FirstName}"
        : $"{LastName} {FirstName} {MiddleName}";

    /// <summary>Qisqa ko'rinish: «Мамажонов Б.Ф.».</summary>
    public string ShortForm
    {
        get
        {
            var initials = FirstName.Length > 0 ? $" {FirstName[0]}." : string.Empty;
            var middle = !string.IsNullOrWhiteSpace(MiddleName) ? $"{MiddleName[0]}." : string.Empty;
            return $"{LastName}{initials}{middle}";
        }
    }

    public static Result<FullName> Create(string? lastName, string? firstName, string? middleName)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Error.Validation("ism.familiya_bosh", "Familiya kiritilishi shart.");
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Error.Validation("ism.ism_bosh", "Ism kiritilishi shart.");
        }

        return new FullName(
            lastName.Trim(),
            firstName.Trim(),
            string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim());
    }

    /// <summary>Tekshiruvsiz yaratadi. FAQAT bazadan o'qishda ishlatiladi.</summary>
    public static FullName FromTrusted(string lastName, string firstName, string? middleName)
        => new(lastName, firstName, middleName);

    /// <summary>
    /// Bitta satrni familiya / ism / otasining ismiga ajratadi.
    /// Excel migratsiyasida ishlatiladi.
    /// </summary>
    /// <example>
    /// «Мамажонов Бобуржон Фазлитдин ўғли» →
    /// Familiya: Мамажонов, Ism: Бобуржон, Otasining ismi: Фазлитдин ўғли
    /// </example>
    public static Result<FullName> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("ism.bosh", "Ism-familiya kiritilmagan.");
        }

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => Error.Validation("ism.bosh", "Ism-familiya kiritilmagan."),
            1 => Error.Validation("ism.tolik_emas", $"Kamida familiya va ism kerak. Kiritildi: «{input}»."),
            2 => Create(parts[0], parts[1], null),
            _ => Create(parts[0], parts[1], string.Join(' ', parts[2..])),
        };
    }

    /// <summary>Berilgan so'z otasining ismiga qo'shiladigan qo'shimchami.</summary>
    public static bool IsPatronymicSuffix(string word)
        => PatronymicSuffixes.Contains(word, StringComparer.OrdinalIgnoreCase);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LastName;
        yield return FirstName;
        yield return MiddleName;
    }

    public override string ToString() => Full;
}
