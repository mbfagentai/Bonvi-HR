using System.Globalization;
using System.Text.Json.Nodes;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// Konstruktor anketasining bog'langan javoblarini nomzod kartasi va anketa kartasiga o'tkazadi
/// (SPEC-anketa-konstruktori §3 «Goes to», §6.2).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bitta joy.</b> Nomzodni topish, ro'yxatga olish, bo'sh maydonlarni to'ldirish va
/// «qayerdan bilgan» — o'rnatilgan anketa bilan UMUMIY yo'l: u <see cref="Person"/> bergan
/// <see cref="QuestionnaireFormAnswers"/> ning shaxsiy qismini o'qiydi. Anketa kartasi
/// (<see cref="ToQuestionnaire"/>) urug' (seed) uchun o'rnatilgan
/// <see cref="QuestionnaireFormMapping"/> bilan AYNAN bir xil qiymat beradi (AC-AK9).
/// </para>
/// <para>
/// Bog'lanmagan javoblar faqat anketaning o'zida va PDF da turadi.
/// </para>
/// </remarks>
public static class DynamicFormMapping
{
    /// <summary>Nomzod kartasidagi manzil ustuni (<c>candidates.address</c>, 500).</summary>
    public const int CandidateAddressLimit = 500;

    /// <summary>«Ofis dasturlari» guruhi — kartaning «Ofis dasturlari» qatori shu guruhdan.</summary>
    public const string OfficeGroup = "office";

    /// <summary>Ish joyi qatorining «ketish sababi» ustuni — kartada «, sabab: …».</summary>
    public const string LeaveReasonColumn = "leaveReason";

    /// <summary>Rus tili qatori (<see cref="FormProfile.Languages"/>).</summary>
    public const string RussianRow = nameof(FormLanguage.Russian);

    /// <summary>
    /// Umumiy yo'l o'qiydigan shaxsiy qism: vakansiya, F.I.Sh., tug'ilgan sana, telefon, manzil,
    /// «qayerdan bilgan», imzodagi ism.
    /// </summary>
    public static QuestionnaireFormAnswers Person(DynamicFormAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        var referral = answers.Bound(FormProfile.Referral) as JsonObject;
        var signature = answers.Bound(FormProfile.Signature) as JsonObject;

        return new QuestionnaireFormAnswers
        {
            VacancyId = Guid.TryParse(answers.BoundText(FormProfile.Vacancy), out var vacancy) ? vacancy : null,
            LastName = answers.BoundText(FormProfile.LastName),
            FirstName = answers.BoundText(FormProfile.FirstName),
            MiddleName = answers.BoundText(FormProfile.MiddleName),
            BirthDate = Day(answers.BoundText(FormProfile.BirthDate)),
            Phone = answers.BoundText(FormProfile.Phone),
            ActualAddress = Cut(answers.BoundText(FormProfile.Address), CandidateAddressLimit),
            ReferralChannelId = Guid.TryParse(DynamicFormAnswers.Text(referral?["channel"]), out var channel) ? channel : null,
            ReferralDetail = DynamicFormAnswers.Text(referral?["detail"]),
            SignatureName = DynamicFormAnswers.Text(signature?["name"]),
        };
    }

    /// <summary>
    /// Umumiy yo'l xatosidagi o'rnatilgan anketa yo'lini (<c>vacancyId</c>, <c>lastName</c>,
    /// <c>referralDetail</c>) shu ta'rifning maydoniga o'giradi — forma xatoni o'z qadamida ochadi.
    /// </summary>
    public static Error Path(Error error, FormDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(definition);

        if (!error.Details.TryGetValue("field", out var field) || field is not string path)
        {
            return error;
        }

        var mapped = path switch
        {
            "vacancyId" => definition.Bound(FormProfile.Vacancy)?.Id,
            "lastName" => definition.Bound(FormProfile.LastName)?.Id,
            "photo" => definition.Bound(FormProfile.Photo)?.Id,
            "signature" => definition.Bound(FormProfile.Signature)?.Id,
            "referralDetail" => definition.Bound(FormProfile.Referral) is { } referral ? $"{referral.Id}.detail" : null,
            _ => null,
        };

        return mapped is null ? error : error.At(mapped);
    }

    /// <summary>Anketa kartasi (<see cref="CandidateQuestionnaire"/>) — bog'langan javoblardan.</summary>
    /// <param name="candidateId">Nomzod.</param>
    /// <param name="answers">TEKSHIRILGAN javoblar.</param>
    /// <param name="consentAt">Shaxsiy ma'lumotga rozilik lahzasi — yuborilgan vaqt.</param>
    public static Result<CandidateQuestionnaire> ToQuestionnaire(
        Guid candidateId,
        DynamicFormAnswers answers,
        DateTimeOffset consentAt)
    {
        ArgumentNullException.ThrowIfNull(answers);

        var russian = Levels(answers, FormProfile.Languages)?["rows"]?[RussianRow] as JsonArray;
        var categories = answers.Bound(FormProfile.DriverLicenseCategories) as JsonArray;

        return CandidateQuestionnaire.Create(
            candidateId,
            Parse<EducationLevel>(answers.BoundText(FormProfile.Education)) ?? EducationLevel.SecondarySchool,
            Parse<MaritalStatus>(answers.BoundText(FormProfile.MaritalStatus)) ?? MaritalStatus.Unmarried,
            QuestionnaireFormMapping.RussianLevel(russian is { Count: > 0 } ? (int?)DynamicFormAnswers.Number(russian[0]) : null),
            QuestionnaireFormMapping.SalaryRange(answers.BoundNumber(FormProfile.ExpectedSalary) ?? 0),
            consentAt.ToUniversalTime(),
            workExperience: WorkExperience(answers),
            otherLanguages: OtherLanguages(answers),
            officeSkills: OfficeSkills(answers),
            lastSalary: answers.BoundNumber(FormProfile.LastSalary) is { } last ? Money(last) : null,
            extraNote: answers.BoundText(FormProfile.ExtraNote),
            hasDriverLicense: answers.BoundFlag(FormProfile.DriverLicense) == true,
            driverLicenseCategories: categories is { Count: > 0 }
                ? string.Join(", ", categories.Select(DynamicFormAnswers.Text))
                : null);
    }

    // ════════════════════════════════════════════════ ko'rinish (karta va PDF uchun umumiy)

    /// <summary>Javobning inson o'qiydigan ko'rinishi (lotin): «3 000 000 so'm», «24.10.2026», «Ha».</summary>
    public static string? Display(FormField field, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (value is null)
        {
            return null;
        }

        return field.Kind switch
        {
            FieldKind.Money => DynamicFormAnswers.Number(value) is { } amount ? Money(amount) : null,
            FieldKind.Number or FieldKind.Year => DynamicFormAnswers.Number(value)?.ToString(CultureInfo.InvariantCulture),
            FieldKind.Date => DynamicFormAnswers.Text(value) is { } day ? Date(day) : null,
            FieldKind.Month => DynamicFormAnswers.Text(value) is { } month ? QuestionnaireFormMapping.Month(month) : null,
            FieldKind.YesNo => DynamicFormAnswers.Flag(value) switch
            {
                true => field.Yes?.Latin ?? "Ha",
                false => field.No?.Latin ?? "Yo‘q",
                null => null,
            },
            FieldKind.Choice => DynamicFormAnswers.Text(value) is { } chosen ? field.Option(chosen)?.Printed ?? chosen : null,
            FieldKind.MultiChoice => value is JsonArray items
                ? string.Join(", ", items.Select(DynamicFormAnswers.Text).OfType<string>().Select(item => field.Option(item)?.Printed ?? item))
                : null,
            FieldKind.Period => value is JsonObject period ? Period(period) : null,
            FieldKind.YearRange => value is JsonObject range ? YearRange(range) : null,
            FieldKind.Consent => DynamicFormAnswers.Flag(value) == true ? "Ha" : null,
            _ => DynamicFormAnswers.Text(value),
        };
    }

    /// <summary>«3 000 000 so'm».</summary>
    public static string Money(long amount) => QuestionnaireFormMapping.Money(amount);

    /// <summary>«24.10.2026» — <c>yyyy-MM-dd</c> dan.</summary>
    public static string Date(string value)
        => Day(value) is { } day ? day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : value;

    /// <summary>Ish davri — o'rnatilgan anketa kartasidagidek: «03.2017–hozir», «03.2017–05.2020», «hozir ham».</summary>
    public static string? Period(JsonObject period)
    {
        ArgumentNullException.ThrowIfNull(period);

        var from = DynamicFormAnswers.Text(period["from"]);
        var to = DynamicFormAnswers.Text(period["to"]);
        var current = DynamicFormAnswers.Flag(period["current"]) == true;

        return (from, to, current) switch
        {
            (null, _, true) => "hozir ham",
            (_, _, true) => $"{QuestionnaireFormMapping.Month(from)}–hozir",
            (null, null, _) => null,
            _ => $"{QuestionnaireFormMapping.Month(from)}–{QuestionnaireFormMapping.Month(to)}",
        };
    }

    /// <summary>«2010–2014», «2010–», «–2014».</summary>
    public static string? YearRange(JsonObject range)
    {
        ArgumentNullException.ThrowIfNull(range);

        var from = DynamicFormAnswers.Number(range["from"]);
        var to = DynamicFormAnswers.Number(range["to"]);

        return from is null && to is null
            ? null
            : $"{from?.ToString(CultureInfo.InvariantCulture)}–{to?.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Daraja yoki «—».</summary>
    public static string Level(JsonNode? level)
        => DynamicFormAnswers.Number(level)?.ToString(CultureInfo.InvariantCulture) ?? "—";

    // ════════════════════════════════════════════════ anketa kartasi qatorlari

    /// <summary>
    /// «Ish tajribasi»: har ish joyi bir qator. Ustunlar tartibida qiymatlar « — » bilan, davr qavsda,
    /// «ketish sababi» — «, sabab: …» (o'rnatilgan anketa kartasidagi shakl); «Yo'q» — «Avval ishlamagan».
    /// </summary>
    private static string? WorkExperience(DynamicFormAnswers answers)
    {
        if (answers.BoundFlag(FormProfile.HasWorkExperience) == false)
        {
            return "Avval ishlamagan";
        }

        if (answers.Definition.Bound(FormProfile.WorkExperience) is not { } field
            || answers.Value(field.Id) is not JsonArray rows)
        {
            return null;
        }

        var lines = new List<string>();

        foreach (var row in rows.OfType<JsonObject>())
        {
            var parts = new List<string>();
            string? period = null;
            string? reason = null;

            foreach (var column in field.Columns)
            {
                if (Display(column, row[column.Id]) is not { Length: > 0 } text)
                {
                    continue;
                }

                if (column.Kind == FieldKind.Period)
                {
                    period ??= text;
                }
                else if (column.Id == LeaveReasonColumn)
                {
                    reason = text;
                }
                else
                {
                    parts.Add(text);
                }
            }

            var line = string.Join(" — ", parts);

            if (period is not null)
            {
                line += $" ({period})";
            }

            if (reason is not null)
            {
                line += ", sabab: " + reason;
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    /// <summary>«Ingliz tili: so'zlashuv 2, o'qish 3, yozish 2» — rus tilidan boshqa har qator.</summary>
    private static string? OtherLanguages(DynamicFormAnswers answers)
    {
        if (answers.Definition.Bound(FormProfile.Languages) is not { } field
            || Levels(answers, FormProfile.Languages) is not { } levels)
        {
            return null;
        }

        var lines = new List<string>();
        var rows = levels["rows"] as JsonObject;

        foreach (var option in field.Options.Where(option => option.Value != RussianRow))
        {
            if (rows?[option.Value] is JsonArray row)
            {
                lines.Add(LanguageLine(field, option.Label.Latin, row));
            }
        }

        foreach (var other in (levels["other"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            var name = DynamicFormAnswers.Text(other["name"]) ?? "Boshqa";
            lines.Add(LanguageLine(field, name, other["levels"] as JsonArray ?? new JsonArray()));
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    private static string LanguageLine(FormField field, string name, JsonArray levels)
        => $"{name}: " + string.Join(", ", field.Aspects.Select((aspect, index) =>
            $"{Lowercase(aspect.Label.Latin)} {Level(index < levels.Count ? levels[index] : null)}"));

    /// <summary>«MS Word — 3, MS Excel — 2» — «office» guruhidan, guruh yo'q bo'lsa — hamma qatordan.</summary>
    private static string? OfficeSkills(DynamicFormAnswers answers)
    {
        if (answers.Definition.Bound(FormProfile.ComputerSkills) is not { } field
            || Levels(answers, FormProfile.ComputerSkills)?["rows"] is not JsonObject rows)
        {
            return null;
        }

        var options = field.Groups.Any(group => group.Id == OfficeGroup)
            ? field.Options.Where(option => option.Group == OfficeGroup)
            : field.Options;

        var parts = options
            .Where(option => rows[option.Value] is JsonArray)
            .Select(option => $"{option.Label.Latin} — {string.Join("/", ((JsonArray)rows[option.Value]!).Where(level => level is not null).Select(Level))}")
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static JsonObject? Levels(DynamicFormAnswers answers, string binding) => answers.Bound(binding) as JsonObject;

    /// <summary>Variant qiymati → enum (raqam ko'rinishidagi qiymat qabul qilinmaydi).</summary>
    private static TEnum? Parse<TEnum>(string? value)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed)
            && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? parsed
            : null;

    private static DateOnly? Day(string? value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static string? Cut(string? value, int limit)
        => value is { Length: > 0 } text && text.Length > limit ? text[..limit].TrimEnd() : value;

    /// <summary>«So'zlashuv» → «so'zlashuv»; qisqartma («MS») o'zgarmaydi.</summary>
    private static string Lowercase(string text)
        => text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1])
            ? char.ToLowerInvariant(text[0]) + text[1..]
            : text;
}
