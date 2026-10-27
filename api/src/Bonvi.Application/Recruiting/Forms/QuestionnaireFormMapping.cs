using System.Globalization;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>
/// Onlayn anketadan tizimdagi MAVJUD maydonlarga ko'chirish (SPEC §5).
/// </summary>
/// <remarks>
/// <para>
/// Nega kerak: HR nomzodlarni ma'lumoti, maoshi va manbasi bo'yicha
/// <see cref="CandidateQuestionnaire"/> orqali guruhlaydi va filtrlaydi.
/// Onlayn anketa o'sha ustunlarni ham to'ldirmasa, botdan kelgan nomzod
/// bilan ofisga kelgan nomzod ro'yxatda ikki xil ko'rinardi.
/// </para>
/// <para>
/// Matnlar o'zbek lotinida (xodim kartasiga ko'chiriladigan
/// <c>CandidateHandover</c> matnlari bilan bir xil uslubda).
/// </para>
/// <para>
/// «Qayerdan bilgan» bu yerda YO'Q — u anketaga emas, nomzodning o'ziga yoziladi
/// (<c>Candidate.SetReferral</c>, <see cref="SubmitQuestionnaireFormCommandHandler"/>).
/// </para>
/// </remarks>
public static class QuestionnaireFormMapping
{
    /// <summary>Aniq summani eski oraliqqa o'giradi (SPEC §5 jadvali).</summary>
    /// <remarks>
    /// 1 mln dan kam summa ham <see cref="SalaryExpectation.From1To2Million"/>:
    /// undan pastki oraliq yo'q, eng yaqini shu. Aniq summa baribir anketaning
    /// o'zida (PDF'da) turadi.
    /// </remarks>
    public static SalaryExpectation SalaryRange(long amount) => amount switch
    {
        < 2_000_000 => SalaryExpectation.From1To2Million,
        < 3_000_000 => SalaryExpectation.From2To3Million,
        < 4_000_000 => SalaryExpectation.From3To4Million,
        < 5_000_000 => SalaryExpectation.From4To5Million,
        < 7_000_000 => SalaryExpectation.From5To7Million,
        _ => SalaryExpectation.From7Million,
    };

    /// <summary>Rus tili so'zlashuv darajasi (0–5) → xodimning til darajasi.</summary>
    /// <remarks>
    /// 0 («bilmayman») ham <see cref="LanguageLevel.Elementary"/>: enumda
    /// «bilmaydi» qiymati yo'q, eng past daraja shu. Aniq javob anketada turadi.
    /// </remarks>
    public static LanguageLevel RussianLevel(int? speaking) => speaking switch
    {
        null or <= 1 => LanguageLevel.Elementary,
        2 => LanguageLevel.Intermediate,
        3 => LanguageLevel.UpperIntermediate,
        4 => LanguageLevel.Advanced,
        _ => LanguageLevel.Native,
    };

    /// <summary>Anketadan <see cref="CandidateQuestionnaire"/> yasaydi.</summary>
    /// <param name="candidateId">Nomzod.</param>
    /// <param name="answers">TEKSHIRILGAN javoblar.</param>
    /// <param name="consentAt">Shaxsiy ma'lumotga rozilik lahzasi — yuborilgan vaqt.</param>
    public static Result<CandidateQuestionnaire> ToQuestionnaire(
        Guid candidateId,
        QuestionnaireFormAnswers answers,
        DateTimeOffset consentAt)
    {
        ArgumentNullException.ThrowIfNull(answers);

        var russian = answers.Languages?.FirstOrDefault(row => row.Language == FormLanguage.Russian);

        return CandidateQuestionnaire.Create(
            candidateId,
            answers.EducationLevel ?? EducationLevel.SecondarySchool,
            answers.MaritalStatus ?? MaritalStatus.Unmarried,
            RussianLevel(russian?.Speaking),
            SalaryRange(answers.ExpectedSalary ?? 0),
            consentAt.ToUniversalTime(),
            workExperience: WorkExperience(answers),
            otherLanguages: OtherLanguages(answers),
            officeSkills: OfficeSkills(answers),
            lastSalary: answers.LastSalary is { } last ? Money(last) : null,
            extraNote: answers.ExtraNote,
            hasDriverLicense: answers.HasDriverLicense == true,
            driverLicenseCategories: DriverLicenseCategories(answers));
    }

    /// <summary>Toifalar matni — qog'ozdagi tartibda, vergul bilan: «B, C», «BC, CE».</summary>
    /// <remarks>
    /// <para>
    /// Ajratgichsiz yozilmaydi (3.1 gacha «BC» edi): endi «BC» o'zi alohida toifa va «B» + «C»
    /// bilan bir xil satr bo'lib qolardi. PDF'dagi «Bor, toifasi: B, C» bilan bir xil.
    /// </para>
    /// <para>
    /// Bot o'z tugmalarini («B», «BC», «BCE», «BCED» — <c>Bonvi.Bot</c>) shu maydonga o'zgarishsiz
    /// yozishda davom etadi: u to'plam emas, bitta tugma qiymati. MCP filtri satr ichidan
    /// qidiradi (<c>ILIKE %C%</c>), ya'ni bitta toifa bo'yicha ikkala yozuvni ham topadi.
    /// </para>
    /// </remarks>
    private static string? DriverLicenseCategories(QuestionnaireFormAnswers answers)
        => answers.DriverLicenseCategories is { Count: > 0 } categories
            ? string.Join(", ", categories)
            : null;

    /// <summary>«3 000 000 so'm» — bo'shliq bilan guruhlangan, PDF'dagi bilan bir xil.</summary>
    public static string Money(long amount)
        => amount.ToString("#,0", Grouping) + " so‘m";

    /// <summary>«03.2021» — <c>yyyy-MM</c> dan.</summary>
    public static string Month(string? value)
        => value is { Length: 7 } ? $"{value[5..]}.{value[..4]}" : value ?? string.Empty;

    private static readonly NumberFormatInfo Grouping = new() { NumberGroupSeparator = " ", NumberGroupSizes = [3] };

    private static string? WorkExperience(QuestionnaireFormAnswers answers)
    {
        // «Avval ishlaganmisiz?» — «Yo'q» (3.1): HR «Anketa» kartasida bo'sh joy emas, javobni ko'rsin
        if (answers.HasWorkExperience == false)
        {
            return "Avval ishlamagan";
        }

        var lines = new List<string>();

        foreach (var job in answers.Jobs ?? [])
        {
            var line = job.Organization ?? string.Empty;

            if (job.Position is { } position)
            {
                line += " — " + position;
            }

            var period = (job.From, job.To, job.Current) switch
            {
                (null, _, true) => "hozir ham",
                (_, _, true) => $"{Month(job.From)}–hozir",
                (null, null, _) => null,
                _ => $"{Month(job.From)}–{Month(job.To)}",
            };

            if (period is not null)
            {
                line += $" ({period})";
            }

            if (job.LeaveReason is { } reason)
            {
                line += ", sabab: " + reason;
            }

            lines.Add(line);
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    private static string? OtherLanguages(QuestionnaireFormAnswers answers)
    {
        var lines = (answers.Languages ?? [])
            .Where(row => row.Language != FormLanguage.Russian)
            .Select(row =>
            {
                var name = row.Language == FormLanguage.Other
                    ? row.OtherName ?? "Boshqa"
                    : QuestionnaireFormCatalog.LanguageLabel(row.Language!.Value);

                return $"{name}: so‘zlashuv {Level(row.Speaking)}, o‘qish {Level(row.Reading)}, yozish {Level(row.Writing)}";
            })
            .ToList();

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    private static string? OfficeSkills(QuestionnaireFormAnswers answers)
    {
        var skills = answers.ComputerSkills;
        if (skills is null || skills.Count == 0)
        {
            return null;
        }

        var parts = QuestionnaireFormCatalog.OfficeGroup.Items
            .Where(item => skills.ContainsKey(item.Key))
            .Select(item => $"{item.Label} — {skills[item.Key]}")
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string Level(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "—";
}
