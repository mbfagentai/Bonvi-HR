using System.Globalization;
using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Dynamic;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Konstruktor anketasining (urug' — HR-F02 4.0) namunaviy javoblari: o'rnatilgan anketa javobini
/// AYNAN o'sha ma'lumot bilan ta'rif shakliga o'giradi (SPEC-anketa-konstruktori AC-AK9 — bir odam,
/// ikki yo'l, bir natija).
/// </summary>
internal static class DynamicFormSamples
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Urug' ta'rifi — HRMentor e'lon qilgandek tekshirilgan.</summary>
    public static FormDefinition Seed { get; } = FormDefinitionReader.Read(HrF02Seed.Build()).Value;

    /// <summary>O'rnatilgan anketa javobi → ta'rifning <c>{ maydon: qiymat }</c> javobi.</summary>
    public static JsonObject From(QuestionnaireFormAnswers a)
    {
        ArgumentNullException.ThrowIfNull(a);

        var o = new JsonObject();

        Put(o, "vacancy", a.VacancyId?.ToString());
        Put(o, "lastName", a.LastName);
        Put(o, "firstName", a.FirstName);
        Put(o, "middleName", a.MiddleName);
        Put(o, "birthDate", a.BirthDate?.ToString("yyyy-MM-dd", Invariant));
        Put(o, "registeredAddress", a.RegisteredAddress);
        Put(o, "actualAddress", a.ActualAddress);
        Put(o, "housingType", a.HousingType?.ToString());
        Put(o, "mahalla", a.Mahalla);
        Put(o, "phone", a.Phone);
        Put(o, "phoneExtra", a.PhoneExtra);
        Put(o, "telegram", a.Telegram);
        Put(o, "email", a.Email);
        Put(o, "maritalStatus", a.MaritalStatus?.ToString());
        Put(o, "childrenCount", a.ChildrenCount);
        Put(o, "militaryService", a.MilitaryService?.ToString());
        Put(o, "militaryServiceMonths", a.MilitaryServiceMonths);
        Put(o, "hasDriverLicense", a.HasDriverLicense);
        Put(o, "driverLicenseCategories", a.DriverLicenseCategories is { } categories ? new JsonArray([.. categories.Select(item => (JsonNode)item)]) : null);
        Put(o, "hasCar", a.HasCar);
        Put(o, "carModel", a.CarModel);

        Put(o, "educationLevel", a.EducationLevel?.ToString());
        Put(o, "educations", a.Educations is { } educations
            ? new JsonArray([.. educations.Select(row => (JsonNode)Row(
                ("institution", row.Institution),
                ("faculty", row.Faculty),
                ("years", Range(row.StartYear, row.EndYear)),
                ("diplomaNumber", row.DiplomaNumber)))])
            : null);
        Put(o, "schoolName", a.SchoolName);
        Put(o, "strongSubjects", a.StrongSubjects);
        Put(o, "courses", a.Courses);

        Put(o, "languages", Languages(a.Languages));
        Put(o, "computerSkills", a.ComputerSkills is { } skills
            ? new JsonObject
            {
                ["rows"] = new JsonObject(skills.Select(skill => KeyValuePair.Create(skill.Key, (JsonNode?)new JsonArray(skill.Value)))),
            }
            : null);
        Put(o, "computerSkillsOther", a.ComputerSkillsOther);

        Put(o, "hasWorkExperience", a.HasWorkExperience);
        Put(o, "jobs", a.Jobs is { } jobs
            ? new JsonArray([.. jobs.Select(job => (JsonNode)Row(
                ("organization", job.Organization),
                ("position", job.Position),
                ("period", Row(("from", job.From), ("to", job.To), ("current", job.Current ? true : null))),
                ("leaveReason", job.LeaveReason)))])
            : null);
        Put(o, "references", a.References is { } references
            ? new JsonArray([.. references.Select(reference => (JsonNode)Row(
                ("fullName", reference.FullName),
                ("organizationPosition", reference.OrganizationPosition),
                ("phone", reference.Phone)))])
            : null);
        Put(o, "familyMembers", a.FamilyMembers is { } family
            ? new JsonArray([.. family.Select(member => (JsonNode)Row(
                ("relation", member.Relation?.ToString()),
                ("fullName", member.FullName),
                ("birthYear", member.BirthYear),
                ("workplace", member.Workplace),
                ("phone", member.Phone),
                ("address", member.Address)))])
            : null);

        Put(o, "referral", a.ReferralChannelId is { } channel
            ? Row(("channel", channel.ToString()), ("detail", a.ReferralDetail))
            : null);
        Put(o, "hasAcquaintances", a.HasAcquaintances);
        Put(o, "acquaintancesDetail", a.AcquaintancesDetail);
        Put(o, "lastSalary", a.LastSalary);
        Put(o, "expectedSalary", a.ExpectedSalary);
        Put(o, "startAfter", a.StartAfter?.ToString());
        Put(o, "hasLoan", a.HasLoan);
        Put(o, "loanMonthlyPayment", a.LoanMonthlyPayment);
        Put(o, "canWorkShifts", a.CanWorkShifts);
        Put(o, "canWorkOvertime", a.CanWorkOvertime);
        Put(o, "canTravel", a.CanTravel);
        Put(o, "agreesToTrial", a.AgreesToTrial);
        Put(o, "hasOtherJob", a.HasOtherJob);
        Put(o, "otherJobDetail", a.OtherJobDetail);
        Put(o, "agreesParentsInvite", a.AgreesParentsInvite);
        Put(o, "startConditions", a.StartConditions);

        Put(o, "hasHealthRestriction", a.HasHealthRestriction);
        Put(o, "healthRestrictionDetail", a.HealthRestrictionDetail);
        Put(o, "hasFamilyPatient", a.HasFamilyPatient);
        Put(o, "hasConviction", a.HasConviction);
        Put(o, "convictionDetail", a.ConvictionDetail);
        Put(o, "relativesConviction", a.RelativesConviction);
        Put(o, "relativesConvictionDetail", a.RelativesConvictionDetail);
        Put(o, "abroadTrips", a.AbroadTrips);

        Put(o, "motivation", a.Motivation);
        Put(o, "whyUs", a.WhyUs);
        Put(o, "strengths", a.Strengths);
        Put(o, "weaknesses", a.Weaknesses);
        Put(o, "motivators", a.Motivators);
        Put(o, "successDefinition", a.SuccessDefinition);
        Put(o, "lastJobAchievement", a.LastJobAchievement);
        Put(o, "bestIdea", a.BestIdea);
        Put(o, "biggestChallenge", a.BiggestChallenge);
        Put(o, "stressHandling", a.StressHandling);
        Put(o, "unfairnessToYou", a.UnfairnessToYou);
        Put(o, "unfairnessToCompany", a.UnfairnessToCompany);
        Put(o, "teamwork", a.Teamwork);
        Put(o, "previousTeamOpinion", a.PreviousTeamOpinion);
        Put(o, "studentWorkExperience", a.StudentWorkExperience);
        Put(o, "fiveYearPlan", a.FiveYearPlan);
        Put(o, "extraNote", a.ExtraNote);

        Put(o, "consentAccuracy", a.ConsentAccuracy);
        Put(o, "consentPersonalData", a.ConsentPersonalData);
        Put(o, "consentReferenceCheck", a.ConsentReferenceCheck);
        Put(o, "consentNotifyChanges", a.ConsentNotifyChanges);
        Put(o, "consentConfidentiality", a.ConsentConfidentiality);
        Put(o, "signature", a.SignatureName is { } name ? Row(("name", name)) : null);

        return o;
    }

    /// <summary>Tekshirilgan javoblar — chizuvchi va xaritalash testlari uchun.</summary>
    public static DynamicFormAnswers Checked(QuestionnaireFormAnswers answers, DateOnly today)
    {
        var result = DynamicFormValidator.Validate(From(answers), Seed, today, ReferralSeeds.Options);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Namuna tekshiruvdan o'tmadi: {result.Error.Code} — {result.Error.Message}");
    }

    private static JsonObject? Languages(IReadOnlyList<FormLanguageSkill>? languages)
    {
        if (languages is null)
        {
            return null;
        }

        var rows = new JsonObject();
        var other = new JsonArray();

        foreach (var skill in languages)
        {
            var levels = new JsonArray(Level(skill.Speaking), Level(skill.Reading), Level(skill.Writing));

            if (skill.Language == FormLanguage.Other)
            {
                other.Add(new JsonObject { ["name"] = skill.OtherName, ["levels"] = levels });
            }
            else if (skill.Language is { } language)
            {
                rows[language.ToString()] = levels;
            }
        }

        return new JsonObject { ["rows"] = rows, ["other"] = other };
    }

    private static JsonValue? Level(int? level) => level is { } value ? JsonValue.Create(value) : null;

    private static JsonObject? Range(int? from, int? to)
        => from is null && to is null ? null : Row(("from", from), ("to", to));

    /// <summary>Bo'sh qiymatlarsiz obyekt.</summary>
    private static JsonObject Row(params (string Key, object? Value)[] values)
    {
        var row = new JsonObject();

        foreach (var (key, value) in values)
        {
            Put(row, key, value);
        }

        return row;
    }

    private static void Put(JsonObject target, string key, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case JsonNode node:
                target[key] = node;
                break;
            case string text:
                target[key] = text;
                break;
            case bool flag:
                target[key] = flag;
                break;
            case int number:
                target[key] = number;
                break;
            case long number:
                target[key] = number;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.GetType().Name, "kutilmagan tur");
        }
    }
}
