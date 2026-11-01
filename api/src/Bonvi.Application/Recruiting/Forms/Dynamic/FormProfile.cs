using System.Text.Json.Nodes;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// Bog'lanish — anketa javobining hr.bonvi.uz ning o'z ma'lumotiga (nomzod kartasi, anketa
/// kartasi) yo'li (SPEC-anketa-konstruktori §3).
/// </summary>
/// <param name="Key">Kalit (<c>candidate.lastName</c>).</param>
/// <param name="Name">HR konstruktorda ko'radigan nom.</param>
/// <param name="Kinds">Bog'lanadigan maydon turlari.</param>
/// <param name="Required">E'lon qilinadigan anketada bo'lishi shart (shartsiz va majburiy maydonda).</param>
/// <param name="Values">Yopiq variantlar to'plami (choice / multi_choice / levels qatorlari).</param>
/// <param name="Rows">levels — o'qiladigan qatorlar (ma'lumot uchun).</param>
/// <param name="Scale">levels — qat'iy shkala.</param>
/// <param name="Note">HR ga izoh — bu javob qayerga boradi.</param>
public sealed record FormBinding(
    string Key,
    string Name,
    IReadOnlyList<FieldKind> Kinds,
    bool Required,
    IReadOnlyList<string>? Values = null,
    IReadOnlyList<string>? Rows = null,
    (int Min, int Max)? Scale = null,
    string? Note = null);

/// <summary>
/// HR-F02 profili — bog'lanishlar jadvali (SPEC §3) MA'LUMOT sifatida: profil yuborish, kelgan
/// ta'rifni tekshirish (§5.2) va javobni nomzodga yozish (§6.2) shu bitta ro'yxatdan o'qiydi.
/// </summary>
public static class FormProfile
{
    public const int Schema = 2;
    public const string Code = Bonvi.Domain.Recruiting.CandidateForm.HrF02;

    public const string Vacancy = "vacancy";
    public const string Photo = "photo";
    public const string LastName = "candidate.lastName";
    public const string FirstName = "candidate.firstName";
    public const string MiddleName = "candidate.middleName";
    public const string BirthDate = "candidate.birthDate";
    public const string Phone = "candidate.phone";
    public const string Address = "candidate.address";
    public const string Referral = "candidate.referral";
    public const string Signature = "signature";
    public const string PersonalDataConsent = "consent.personalData";
    public const string Education = "questionnaire.education";
    public const string MaritalStatus = "questionnaire.maritalStatus";
    public const string Languages = "questionnaire.languages";
    public const string ComputerSkills = "questionnaire.computerSkills";
    public const string ExpectedSalary = "questionnaire.expectedSalary";
    public const string LastSalary = "questionnaire.lastSalary";
    public const string HasWorkExperience = "questionnaire.hasWorkExperience";
    public const string WorkExperience = "questionnaire.workExperience";
    public const string DriverLicense = "questionnaire.driverLicense";
    public const string DriverLicenseCategories = "questionnaire.driverLicenseCategories";
    public const string ExtraNote = "questionnaire.extraNote";

    /// <summary>Imzodagi ism shu ikkisini o'z ichiga olishi shart.</summary>
    public static IReadOnlyList<string> NameBindings { get; } = [LastName, FirstName];

    /// <summary>Tug'ilgan sana — maydon nima desa ham, BonviHR shu yosh oralig'ini talab qiladi.</summary>
    public const int MinAge = 14;
    public const int MaxAge = 100;

    public static IReadOnlyList<FormBinding> Bindings { get; } =
    [
        new(Vacancy, "Vakansiya", [FieldKind.Vacancy], Required: true,
            Note: "Nomzod qaysi vakansiyaga yoziladi; tanlanmasa — umumiy baza"),
        new(Photo, "Rasm", [FieldKind.Photo], Required: true,
            Note: "Anketa rasmi va nomzod kartasining rasmi"),
        new(LastName, "Familiya", [FieldKind.ShortText], Required: true,
            Note: "Nomzod kartasiga yoziladi; takroriy nomzodni topishda ishlatiladi"),
        new(FirstName, "Ism", [FieldKind.ShortText], Required: true,
            Note: "Nomzod kartasiga yoziladi; takroriy nomzodni topishda ishlatiladi"),
        new(MiddleName, "Otasining ismi", [FieldKind.ShortText], Required: false,
            Note: "Nomzod kartasidagi F.I.Sh."),
        new(BirthDate, "Tug‘ilgan sana", [FieldKind.Date], Required: true,
            Note: "Nomzod kartasiga (bo‘sh bo‘lsa); takroriy nomzodni topishda; yosh 14–100"),
        new(Phone, "Telefon", [FieldKind.Phone], Required: true,
            Note: "Nomzod kartasidagi telefon (O‘zbekiston mobil raqami); takroriy nomzodni topishda"),
        new(Address, "Yashash manzili", [FieldKind.ShortText, FieldKind.LongText], Required: false,
            Note: "Nomzod kartasidagi manzil (bo‘sh bo‘lsa)"),
        new(Referral, "Qayerdan bilgan", [FieldKind.Referral], Required: false,
            Note: "Nomzod kartasidagi «Qayerdan bilgan» (bo‘sh bo‘lsa)"),
        new(Signature, "Imzo", [FieldKind.Signature], Required: true,
            Note: "Imzo rasmi va imzo o‘rnidagi ism"),
        new(PersonalDataConsent, "Shaxsiy ma’lumotlarga rozilik", [FieldKind.Consent], Required: true,
            Note: "Anketa kartasidagi rozilik vaqti"),
        new(Education, "Ma’lumoti", [FieldKind.Choice], Required: false,
            Values: ["SecondarySchool", "VocationalSchool", "HigherIncomplete", "HigherComplete", "HigherMaster"],
            Note: "Anketa kartasidagi ma’lumot darajasi"),
        new(MaritalStatus, "Oilaviy holati", [FieldKind.Choice], Required: false,
            Values: ["Married", "Unmarried", "Divorced", "Widowed"],
            Note: "Anketa kartasidagi oilaviy holat"),
        new(Languages, "Tillar", [FieldKind.Levels], Required: false,
            Rows: ["Russian"], Scale: (0, 5),
            Note: "Rus tili darajasi va boshqa tillar anketa kartasiga"),
        new(ComputerSkills, "Kompyuter", [FieldKind.Levels], Required: false,
            Note: "Ofis dasturlari (guruh «office») anketa kartasiga"),
        new(ExpectedSalary, "Kutilayotgan maosh", [FieldKind.Money], Required: false,
            Note: "Anketa kartasidagi maosh oralig‘i"),
        new(LastSalary, "Oxirgi maosh", [FieldKind.Money], Required: false,
            Note: "Anketa kartasiga"),
        new(HasWorkExperience, "Avval ishlaganmi", [FieldKind.YesNo], Required: false,
            Note: "«Yo‘q» bo‘lsa anketa kartasida «Avval ishlamagan»"),
        new(WorkExperience, "Ish tajribasi", [FieldKind.Repeat], Required: false,
            Note: "Har ish joyi bir qator bo‘lib anketa kartasiga"),
        new(DriverLicense, "Haydovchilik guvohnomasi", [FieldKind.YesNo], Required: false,
            Note: "Anketa kartasiga"),
        new(DriverLicenseCategories, "Guvohnoma toifalari", [FieldKind.MultiChoice], Required: false,
            Values: ["A", "B", "C", "D", "BC", "CE", "DE"],
            Note: "Anketa kartasiga («B, C»)"),
        new(ExtraNote, "Qo‘shimcha ma’lumot", [FieldKind.LongText], Required: false,
            Note: "Anketa kartasidagi izoh"),
    ];

    public static IReadOnlyDictionary<string, FormBinding> ByKey { get; } =
        Bindings.ToDictionary(binding => binding.Key, StringComparer.Ordinal);

    /// <summary>HRMentor ga yuboriladigan profil (§19.4): bog'lanishlar va urug' (seed).</summary>
    public static JsonObject Json(JsonObject seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var bindings = new JsonArray();
        foreach (var binding in Bindings)
        {
            var item = new JsonObject
            {
                ["key"] = binding.Key,
                ["name"] = binding.Name,
                ["kinds"] = new JsonArray([.. binding.Kinds.Select(kind => (JsonNode)kind.Wire())]),
                ["required"] = binding.Required,
            };

            if (binding.Values is { } values)
            {
                item["values"] = new JsonArray([.. values.Select(value => (JsonNode)value)]);
            }

            if (binding.Rows is { } rows)
            {
                item["rows"] = new JsonArray([.. rows.Select(row => (JsonNode)row)]);
            }

            if (binding.Scale is { } scale)
            {
                item["scale"] = new JsonObject { ["min"] = scale.Min, ["max"] = scale.Max };
            }

            if (binding.Note is { } note)
            {
                item["note"] = note;
            }

            bindings.Add(item);
        }

        return new JsonObject
        {
            ["schema"] = Schema,
            ["code"] = Code,
            ["bindings"] = bindings,
            ["seed"] = seed,
        };
    }
}
