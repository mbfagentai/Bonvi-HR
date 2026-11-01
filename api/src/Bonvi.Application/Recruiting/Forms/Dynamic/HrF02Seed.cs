using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Forms.Texts;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// O'rnatilgan HR-F02 v3.1 — ta'rif ko'rinishida (SPEC-anketa-konstruktori §4). HRMentor ga
/// profil bilan birga yuboriladi: konstruktor shundan boshlanadi va HR uni o'zgartirmasdan
/// «4.0» qilib e'lon qilsa, nomzodlar tanish anketani dinamik dvigatelda ko'radi.
/// </summary>
/// <remarks>
/// <para>
/// So'zlar bugungi manbalardan: anketa matnlari — <c>kiosk-texts.json</c> (uch til, HR o'zgartirmagan
/// asl matn), qog'ozdagi so'zlar — <see cref="QuestionnairePdfTexts"/>, ro'yxatlar — katalog.
/// Maydon identifikatorlari — o'rnatilgan anketaning JSON kalitlari (<c>lastName</c>,
/// <c>jobs</c>…): javoblar va nomzod kartasiga yozish ikki yo'lda bir xil chiqadi (AC-AK9).
/// </para>
/// <para>
/// O'rnatilgan tekshiruvning qoidalari umumiy kalitlar bilan: yosh 16–70, «Rus tili» majburiy
/// qator, ≤ 3 boshqa til, <c>hasWorkExperience</c> shartlari, «o'zingiz haqingizda» javoblarida
/// 2 harf, kredit 1…1 000 000 000, toifalar, «manzilim shu» belgisi.
/// </para>
/// <para>
/// Barqarorlik: bir xil manbadan bir xil JSON — profil xeshi o'zgarmaydi va qayta yuborilmaydi.
/// </para>
/// </remarks>
public static partial class HrF02Seed
{
    /// <summary>Urug'ning bosma versiyasi — o'rnatilgan anketa.</summary>
    public const string Label = Bonvi.Domain.Recruiting.CandidateForm.HrF02Version;

    public static JsonObject Build() => Build(HrF02TextCatalog.Current);

    internal static JsonObject Build(HrF02TextCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var seed = new Builder(catalog);
        return seed.Definition();
    }

    [GeneratedRegex(@"^\d+\.\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SectionNumber();

    private sealed class Builder(HrF02TextCatalog catalog)
    {
        private static readonly (string Key, JsonNode? Value)[] None = [];

        public JsonObject Definition() => new()
        {
            ["schema"] = FormProfile.Schema,
            ["code"] = FormProfile.Code,
            ["label"] = Label,
            ["title"] = T("kiosk.start.title"),
            ["pdf"] = new JsonObject
            {
                ["printed_code"] = FormProfile.Code,
                ["title"] = P("pdf.head.title"),
                ["subtitle"] = P("pdf.head.subtitle"),
                ["instruction"] = P("pdf.head.partAHint"),
                ["candidate_part"] = $"{P("pdf.head.partA")}  {P("pdf.head.partAText")}",
                ["office_part"] = $"{P("pdf.hr.part")}  {P("pdf.hr.partText")}",
                ["footer"] = P("pdf.hr.storage"),
            },
            ["sections"] = new JsonArray(
                Personal(),
                Education(),
                Languages(),
                Computer(),
                Work(),
                References(),
                Family(),
                Conditions(),
                Health(),
                About(),
                Consent(),
                Documents(),
                Tests(),
                Interview()),
        };

        // ── 1. Shaxsiy ma'lumotlar ──

        private JsonObject Personal() => Section("personal", T("kiosk.screen.personal"), "pdf.personal.title",
            Screen("vacancy-page", "kiosk.screen.vacancy",
                Field("vacancy", "vacancy", T("kiosk.vacancy.label"), required: true, hint: T("kiosk.vacancy.hint"),
                    extra: [("bind", FormProfile.Vacancy), ("pdf_hidden", true)]),
                Field("photo", "photo", T("kiosk.photo.label"), required: true, hint: T("kiosk.photo.hint"),
                    extra: [("bind", FormProfile.Photo), ("pdf_hidden", true)])),
            Screen("personal-main", "kiosk.screen.personal",
                Name("lastName", FormProfile.LastName, required: true, "pdf.personal.lastName"),
                Name("firstName", FormProfile.FirstName, required: true, "pdf.personal.firstName"),
                Name("middleName", FormProfile.MiddleName, required: false, "pdf.personal.middleName"),
                Field("birthDate", "date", T("kiosk.field.birthDate"), required: true, pdf: "pdf.personal.birthDate",
                    extra: [("bind", FormProfile.BirthDate), ("min_age", 16), ("max_age", 70)])),
            Screen("address", "kiosk.screen.address",
                Field("registeredAddress", "short_text", T("kiosk.field.registeredAddress"), required: true,
                    pdf: "pdf.personal.registeredAddress", extra: [("max_length", QuestionnaireFormValidator.MaxAddressLength)]),
                Field("actualAddress", "short_text", T("kiosk.field.actualAddress"), required: true,
                    pdf: "pdf.personal.actualAddress",
                    extra:
                    [
                        ("bind", FormProfile.Address),
                        ("max_length", QuestionnaireFormValidator.MaxAddressLength),
                        ("same_as", new JsonObject { ["field"] = "registeredAddress", ["label"] = T("kiosk.field.sameAddress") }),
                    ]),
                Field("housingType", "choice", T("kiosk.field.housingType"), pdf: "pdf.personal.housing",
                    extra: [("options", Options(HrF02TextCatalog.HousingList)), ("style", "tiles")]),
                Field("mahalla", "short_text", T("kiosk.field.mahalla"), pdf: "pdf.personal.mahalla")),
            Screen("contacts", "kiosk.screen.contacts",
                Field("phone", "phone", T("kiosk.field.phone"), required: true, pdf: "pdf.personal.phone",
                    extra: [("bind", FormProfile.Phone)]),
                Field("phoneExtra", "phone", T("kiosk.field.phoneExtra"), pdf: "pdf.personal.phoneExtra"),
                Field("telegram", "telegram", T("kiosk.field.telegram"), pdf: "pdf.personal.telegram"),
                Field("email", "email", T("kiosk.field.email"), pdf: "pdf.personal.email")),
            Screen("marital", "kiosk.screen.marital",
                Field("maritalStatus", "choice", T("kiosk.field.maritalStatus"), required: true, pdf: "pdf.personal.marital",
                    extra: [("bind", FormProfile.MaritalStatus), ("options", Options(HrF02TextCatalog.MaritalList)), ("style", "list")]),
                Field("childrenCount", "number", T("kiosk.field.childrenCount"), pdf: "pdf.personal.children",
                    extra: [("min", 0), ("max", 20)]),
                Field("militaryService", "choice", T("kiosk.field.militaryService"), pdf: "pdf.personal.military",
                    extra: [("options", Options(HrF02TextCatalog.MilitaryList)), ("style", "list")]),
                Field("militaryServiceMonths", "number", T("kiosk.field.militaryServiceMonths"),
                    extra: [("min", 1), ("max", 60), ("visible_if", In("militaryService", "Served"))])),
            Screen("license", "kiosk.screen.license",
                Field("hasDriverLicense", "yes_no", T("kiosk.field.hasDriverLicense"), required: true, pdf: "pdf.personal.license",
                    extra: [("bind", FormProfile.DriverLicense)]),
                Field("driverLicenseCategories", "multi_choice", T("kiosk.field.driverLicenseCategories"), required: true,
                    extra:
                    [
                        ("bind", FormProfile.DriverLicenseCategories),
                        ("options", Options(HrF02TextCatalog.DriverLicenseList)),
                        ("min_selected", 1),
                        ("visible_if", Is("hasDriverLicense", true)),
                    ]),
                Field("hasCar", "yes_no", T("kiosk.field.hasCar"), pdf: "pdf.personal.car"),
                Field("carModel", "short_text", T("kiosk.field.carModel"), extra: [("visible_if", Is("hasCar", true))])));

        // ── 2. Ma'lumoti ──

        private JsonObject Education() => Section("education", T("kiosk.screen.education"), "pdf.education.title",
            Screen("education-page", "kiosk.screen.education",
                Field("educationLevel", "choice", T("kiosk.field.educationLevel"), required: true, pdf: "pdf.education.level",
                    extra: [("bind", FormProfile.Education), ("options", Options(HrF02TextCatalog.EducationList)), ("style", "list")]),
                Field("educations", "repeat", T("kiosk.field.educations"),
                    extra:
                    [
                        ("columns", new JsonArray(
                            Field("institution", "short_text", T("kiosk.field.institution"), required: true, pdf: "pdf.education.institution"),
                            Field("faculty", "short_text", T("kiosk.field.faculty"), pdf: "pdf.education.faculty"),
                            Field("years", "year_range", L("O‘qigan yillari", "Ўқиган йиллари", "Годы учёбы"),
                                pdf: "pdf.education.years", extra: [("min_year", 1950), ("max_year", "today")]),
                            Field("diplomaNumber", "short_text", T("kiosk.field.diplomaNumber"), pdf: "pdf.education.diploma"))),
                        ("max_rows", QuestionnaireFormValidator.MaxEducations),
                        ("summary", "{institution}"),
                        ("print_rows", 2),
                    ])),
            Screen("school", "kiosk.screen.school",
                Field("schoolName", "short_text", T("kiosk.field.schoolName"), pdf: "pdf.education.school"),
                Field("strongSubjects", "short_text", T("kiosk.field.strongSubjects"), pdf: "pdf.education.subjects"),
                Field("courses", "long_text", T("kiosk.field.courses"), pdf: "pdf.education.courses")));

        // ── 3. Tillar ──

        private JsonObject Languages() => Section("language-skills", T("kiosk.screen.languages"), "pdf.languages.title",
            Screen("languages-page", "kiosk.screen.languages",
                Field("languages", "levels", T("kiosk.screen.languages"),
                    hint: Joined("kiosk.languages.legend", "kiosk.languages.scale"),
                    extra:
                    [
                        ("bind", FormProfile.Languages),
                        ("options", Options(HrF02TextCatalog.LanguageList, value => value != nameof(FormLanguage.Other))),
                        ("aspects", new JsonArray(
                            Aspect("speaking", "kiosk.languages.speaking"),
                            Aspect("reading", "kiosk.languages.reading"),
                            Aspect("writing", "kiosk.languages.writing"))),
                        ("scale", new JsonObject { ["min"] = 0, ["max"] = 5 }),
                        ("required_rows", new JsonArray(nameof(FormLanguage.Russian))),
                        ("other", new JsonObject
                        {
                            ["max"] = QuestionnaireFormValidator.MaxOtherLanguages,
                            ["label"] = Languages(catalog.DefaultOptions(HrF02TextCatalog.LanguageList)
                                .First(option => option.Value == nameof(FormLanguage.Other)).Label),
                        }),
                        ("print_rows", new JsonArray(
                            nameof(FormLanguage.Uzbek),
                            nameof(FormLanguage.Russian),
                            nameof(FormLanguage.English),
                            nameof(FormLanguage.Chinese))),
                    ])));

        // ── 4. Kompyuter ──

        private JsonObject Computer()
        {
            var options = Options(HrF02TextCatalog.ComputerList);
            return Section("computer", T("kiosk.screen.computer"), "pdf.computer.title",
                Screen("computer-page", "kiosk.screen.computer",
                    Field("computerSkills", "levels", T("kiosk.screen.computer"),
                        hint: Joined("kiosk.computer.legend", "kiosk.computer.scale"),
                        extra:
                        [
                            ("bind", FormProfile.ComputerSkills),
                            ("groups", new JsonArray([.. HrF02TextCatalog.ComputerGroupIds.Select(id => (JsonNode)new JsonObject
                            {
                                ["id"] = id,
                                ["label"] = L(
                                    catalog.DefaultGroupLabel(HrF02TextCatalog.ComputerList, id, FormTexts.Latin)!,
                                    catalog.DefaultGroupLabel(HrF02TextCatalog.ComputerList, id, FormTexts.Cyrillic),
                                    catalog.DefaultGroupLabel(HrF02TextCatalog.ComputerList, id, FormTexts.Russian)),
                            })])),
                            ("options", options),
                            ("aspects", new JsonArray(new JsonObject
                            {
                                ["id"] = "level",
                                ["label"] = L("Daraja", "Даража", "Уровень"),
                            })),
                            ("scale", new JsonObject { ["min"] = 1, ["max"] = 4 }),
                            ("print_rows", new JsonArray([.. options.Select(option => (JsonNode)option!["value"]!.GetValue<string>())])),
                        ]),
                    Field("computerSkillsOther", "short_text", T("kiosk.field.computerSkillsOther"),
                        hint: T("kiosk.computer.otherPlaceholder"), pdf: "pdf.computer.other")));
        }

        // ── 5. Mehnat faoliyati ──

        private JsonObject Work() => Section("work", T("kiosk.screen.work"), "pdf.work.title",
            Screen("work-page", "kiosk.screen.work",
                Field("hasWorkExperience", "yes_no", T("kiosk.field.hasWorkExperience"), required: true, pdf: "pdf.work.experience",
                    extra: [("bind", FormProfile.HasWorkExperience)]),
                Field("jobs", "repeat", T("kiosk.sheet.job"), required: true, hint: T("kiosk.work.hint"),
                    extra:
                    [
                        ("bind", FormProfile.WorkExperience),
                        ("visible_if", Is("hasWorkExperience", true)),
                        ("columns", new JsonArray(
                            Field("organization", "short_text", T("kiosk.field.organization"), required: true, pdf: "pdf.work.organization"),
                            Field("position", "short_text", T("kiosk.field.position"), pdf: "pdf.work.position"),
                            Field("period", "period", L("Ishlagan davri", "Ишлаган даври", "Период работы"),
                                pdf: "pdf.work.period", extra: [("allow_current", true)]),
                            Field("leaveReason", "short_text", T("kiosk.field.leaveReason"), pdf: "pdf.work.leaveReason"))),
                        ("min_rows", 1),
                        ("max_rows", QuestionnaireFormValidator.MaxJobs),
                        ("summary", "{organization} — {position}"),
                        ("print_rows", 3),
                    ])));

        // ── 6. Tavsiya beruvchilar ──

        private JsonObject References() => Section("referees", T("kiosk.screen.references"), "pdf.references.title",
            Screen("references-page", "kiosk.screen.references",
                Field("references", "repeat", T("kiosk.sheet.reference"), required: true, hint: T("kiosk.references.hint"),
                    extra:
                    [
                        ("visible_if", Is("hasWorkExperience", true)),
                        ("columns", new JsonArray(
                            Field("fullName", "short_text", T("kiosk.field.fullName"), required: true, pdf: "pdf.references.fullName"),
                            Field("organizationPosition", "short_text", T("kiosk.field.organizationPosition"),
                                pdf: "pdf.references.organizationPosition"),
                            Field("phone", "phone", T("kiosk.field.contactPhone"), pdf: "pdf.references.phone",
                                extra: [("mobile_only", false)]))),
                        ("min_rows", 1),
                        ("max_rows", QuestionnaireFormValidator.MaxReferences),
                        ("summary", "{fullName}"),
                        ("print_rows", 2),
                    ])));

        // ── 7. Oila a'zolari ──

        private JsonObject Family() => Section("family", T("kiosk.screen.family"), "pdf.family.title",
            Screen("family-page", "kiosk.screen.family",
                Field("familyMembers", "repeat", T("kiosk.sheet.family"), required: true, hint: T("kiosk.family.hint"),
                    extra:
                    [
                        ("columns", new JsonArray(
                            Field("relation", "choice", T("kiosk.field.relation"), required: true, pdf: "pdf.family.relation",
                                extra: [("options", Options(HrF02TextCatalog.RelationList))]),
                            Field("fullName", "short_text", T("kiosk.field.fullName"), required: true, pdf: "pdf.family.fullName"),
                            Field("birthYear", "year", T("kiosk.field.birthYear"), pdf: "pdf.family.birthYear",
                                extra: [("min_year", 1920), ("max_year", "today")]),
                            Field("workplace", "short_text", T("kiosk.field.workplace"), pdf: "pdf.family.workplace"),
                            Field("phone", "phone", T("kiosk.field.contactPhone"), pdf: "pdf.family.phone",
                                extra: [("mobile_only", false)]),
                            Field("address", "short_text", T("kiosk.field.address"), pdf: "pdf.family.address",
                                extra: [("max_length", QuestionnaireFormValidator.MaxAddressLength)]))),
                        ("min_rows", 1),
                        ("max_rows", QuestionnaireFormValidator.MaxFamilyMembers),
                        ("summary", "{fullName}"),
                        ("print_rows", 4),
                    ])));

        // ── 8. Ish sharoitlari va kutilmalar ──

        private JsonObject Conditions() => Section("conditions",
            L("Ish sharoitlari va kutilmalar", "Иш шароитлари ва кутилмалар", "Условия работы и ожидания"), "pdf.conditions.title",
            Screen("referral-page", "kiosk.screen.referral",
                Field("referral", "referral", T("kiosk.field.referralSource"), required: true, pdf: "pdf.conditions.referral",
                    extra: [("bind", FormProfile.Referral)]),
                Field("hasAcquaintances", "yes_no", T("kiosk.field.hasAcquaintances"), required: true,
                    pdf: "pdf.conditions.acquaintances"),
                Field("acquaintancesDetail", "short_text", T("kiosk.field.acquaintancesDetail"), required: true,
                    extra: [("visible_if", Is("hasAcquaintances", true))])),
            Screen("salary", "kiosk.screen.salary",
                Field("lastSalary", "money", T("kiosk.field.lastSalary"), pdf: "pdf.conditions.lastSalary",
                    extra: [("bind", FormProfile.LastSalary)]),
                Field("expectedSalary", "money", T("kiosk.field.expectedSalary"), required: true, pdf: "pdf.conditions.expectedSalary",
                    extra: [("bind", FormProfile.ExpectedSalary)]),
                Field("startAfter", "choice", T("kiosk.field.startAfter"), required: true, pdf: "pdf.conditions.startAfter",
                    extra: [("options", Options(HrF02TextCatalog.StartAfterList)), ("style", "list")]),
                Field("hasLoan", "yes_no", T("kiosk.field.hasLoan"), required: true, pdf: "pdf.conditions.loan"),
                Field("loanMonthlyPayment", "money", T("kiosk.field.loanMonthlyPayment"), required: true,
                    extra: [("min", 1), ("max", QuestionnaireFormValidator.MaxMoney), ("visible_if", Is("hasLoan", true))])),
            Screen("conditions-main", "kiosk.screen.conditions",
                Field("canWorkShifts", "yes_no", T("kiosk.field.canWorkShifts"), required: true, pdf: "pdf.conditions.shifts"),
                Field("canWorkOvertime", "yes_no", T("kiosk.field.canWorkOvertime"), required: true, pdf: "pdf.conditions.overtime"),
                Field("canTravel", "yes_no", T("kiosk.field.canTravel"), required: true, pdf: "pdf.conditions.travel"),
                Field("agreesToTrial", "yes_no", T("kiosk.field.agreesToTrial"), required: true, pdf: "pdf.conditions.trial")),
            Screen("terms", "kiosk.screen.terms",
                Field("hasOtherJob", "yes_no", T("kiosk.field.hasOtherJob"), required: true, pdf: "pdf.conditions.otherJob"),
                Field("otherJobDetail", "short_text", T("kiosk.field.otherJobDetail"), required: true,
                    extra: [("visible_if", Is("hasOtherJob", true))]),
                Field("agreesParentsInvite", "yes_no", T("kiosk.field.agreesParentsInvite"), pdf: "pdf.conditions.parents"),
                Field("startConditions", "long_text", T("kiosk.field.startConditions"), pdf: "pdf.conditions.startConditions")));

        // ── 9. Sog'liq va qo'shimcha ma'lumotlar ──

        private JsonObject Health() => Section("health",
            L("Sog‘liq va qo‘shimcha ma’lumotlar", "Соғлиқ ва қўшимча маълумотлар", "Здоровье и дополнительные сведения"),
            "pdf.health.title",
            Screen("health-page", "kiosk.screen.health",
                Field("hasHealthRestriction", "yes_no", T("kiosk.field.hasHealthRestriction"), required: true,
                    hint: T("kiosk.field.healthHint")),
                Field("healthRestrictionDetail", "short_text", T("kiosk.field.healthRestrictionDetail"), required: true,
                    extra: [("visible_if", Is("hasHealthRestriction", true))]),
                Field("hasFamilyPatient", "yes_no", T("kiosk.field.hasFamilyPatient"), pdf: "pdf.health.patient")),
            Screen("background", "kiosk.screen.background",
                Field("hasConviction", "yes_no", T("kiosk.field.hasConviction"), required: true, pdf: "pdf.health.conviction"),
                Field("convictionDetail", "short_text", T("kiosk.field.convictionDetail"), required: true,
                    extra: [("visible_if", Is("hasConviction", true))]),
                Field("relativesConviction", "yes_no", T("kiosk.field.relativesConviction"), pdf: "pdf.health.relativesConviction"),
                Field("relativesConvictionDetail", "short_text", T("kiosk.field.relativesConvictionDetail"),
                    extra: [("visible_if", Is("relativesConviction", true))]),
                Field("abroadTrips", "long_text", T("kiosk.field.abroadTrips"), pdf: "pdf.health.abroad")));

        // ── 10. O'zingiz haqingizda ──

        private JsonObject About()
        {
            var worked = Is("hasWorkExperience", true);
            return Section("about", T("kiosk.screen.about"), "pdf.about.title",
                HintedScreen("about-page", "kiosk.screen.about", T("kiosk.about.hint"),
                    Answer("motivation"), Answer("whyUs"), Answer("strengths"), Answer("weaknesses")),
                HintedScreen("aboutSuccess", "kiosk.screen.aboutSuccess", null,
                    Answer("motivators"), Answer("successDefinition"),
                    Answer("lastJobAchievement", worked), Answer("bestIdea", worked.DeepClone())),
                HintedScreen("aboutHard", "kiosk.screen.aboutHard", null,
                    Answer("biggestChallenge"), Answer("stressHandling"), Answer("unfairnessToYou"), Answer("unfairnessToCompany")),
                HintedScreen("aboutTeam", "kiosk.screen.aboutTeam", null,
                    Answer("teamwork"), Answer("previousTeamOpinion", worked.DeepClone()), Answer("studentWorkExperience"),
                    Answer("fiveYearPlan"), Answer("extraNote", limit: QuestionnaireFormValidator.MaxExtraNoteLength)));
        }

        private JsonObject Answer(string id, JsonNode? visibleIf = null, int limit = QuestionnaireFormValidator.MaxLongLength)
        {
            List<(string, JsonNode?)> extra = [("max_length", limit), ("min_letters", QuestionnaireFormValidator.MinAnswerLetters)];
            if (visibleIf is not null)
            {
                extra.Add(("visible_if", visibleIf));
            }

            if (id == "extraNote")
            {
                extra.Add(("bind", FormProfile.ExtraNote));
            }

            return Field(id, "long_text", T($"kiosk.about.q.{id}"), required: true, pdf: $"pdf.about.{id}", extra: [.. extra]);
        }

        // ── 11. Tilxat ──

        private JsonObject Consent() => Section("consent", L("Tilxat", "Тилхат", "Расписка"), "pdf.consent.title",
            Screen("consent-page", "kiosk.screen.consent",
                Agreement("consentAccuracy", L("Ma’lumotlarim to‘g‘ri", "Маълумотларим тўғри", "Сведения достоверны"), intro: true),
                Agreement("consentPersonalData", L("Shaxsga doir ma’lumotlar", "Шахсга доир маълумотлар", "Персональные данные"),
                    bind: FormProfile.PersonalDataConsent),
                Agreement("consentReferenceCheck", L("Surishtiruv", "Суриштирув", "Проверка рекомендаций")),
                Agreement("consentNotifyChanges", L("O‘zgarishlarni xabar qilish", "Ўзгаришларни хабар қилиш", "Сообщать об изменениях")),
                Agreement("consentConfidentiality", L("Korxona sirlari", "Корхона сирлари", "Коммерческая тайна"))),
            Screen("signature-page", "kiosk.screen.signature",
                Field("signature", "signature", T("kiosk.field.signatureName"), required: true, hint: T("kiosk.field.signatureNameHint"),
                    extra: [("bind", FormProfile.Signature)])));

        private JsonObject Agreement(string id, JsonObject label, bool intro = false, string? bind = null)
        {
            var text = T($"kiosk.consent.{id}");
            if (intro)
            {
                var opening = T("kiosk.consent.intro");
                foreach (var language in FormTexts.Languages)
                {
                    var first = opening[language]!.GetValue<string>().Replace("{{name}}", "{{fullName}}", StringComparison.Ordinal);
                    text[language] = first + "\n" + text[language]!.GetValue<string>();
                }
            }

            List<(string, JsonNode?)> extra = [("text", text)];
            if (bind is not null)
            {
                extra.Add(("bind", bind));
            }

            return Field(id, "consent", label, required: true, extra: [.. extra]);
        }

        // ── B qism (12–14) — faqat PDF da, HR qo'lda to'ldiradi ──

        private static JsonObject Documents() => Office("documents", "pdf.hr.documentsTitle",
            Field("documents", "checklist", L(P("pdf.hr.document")),
                extra:
                [
                    ("items", new JsonArray(
                        Item("pdf.hr.photo", "photo", P("pdf.hr.photoNote")),
                        Item("pdf.hr.passport"),
                        Item("pdf.hr.diploma"),
                        Item("pdf.hr.license"),
                        Item("pdf.hr.noConviction"),
                        Item("pdf.hr.military"),
                        Item("pdf.hr.signedForm", "signature", P("pdf.hr.signedNote")))),
                ]));

        private static JsonObject Tests() => Office("tests", "pdf.hr.testsTitle",
            Blank("professionalTest", "pdf.hr.professional", "pdf.hr.points"),
            Blank("discTest", "pdf.hr.disc"),
            Blank("productivityTest", "pdf.hr.productivity"),
            Blank("russianCheck", "pdf.hr.russian", "pdf.hr.russianLevel"),
            Blank("excelCheck", "pdf.hr.excel", "pdf.hr.excelLevel"),
            Blank("practicalTest", "pdf.hr.practical"));

        private static JsonObject Interview() => Office("interview", "pdf.hr.interviewTitle",
            Score("fitScore", "pdf.hr.fit"),
            Score("experienceScore", "pdf.hr.experience"),
            Score("communicationScore", "pdf.hr.communication"),
            Score("practicalScore", "pdf.hr.practicalResult"),
            Field("decision", "choice", L(P("pdf.hr.decision")),
                extra:
                [
                    ("options", new JsonArray(
                        Choice("council", P("pdf.hr.council")),
                        Choice("reserve", P("pdf.hr.reserve")),
                        Choice("rejected", P("pdf.hr.rejected")))),
                ]),
            Blank("rejectReason", "pdf.hr.rejectReason"),
            Blank("hrSpecialist", "pdf.hr.specialist"),
            Blank("hrSignature", "pdf.hr.sign"),
            Blank("hrDate", "pdf.hr.date"));

        private static JsonObject Office(string id, string titleKey, params JsonObject[] fields)
        {
            var title = SectionNumber().Replace(P(titleKey), string.Empty);
            return Section($"hr-{id}", L(Sentence(title)), titleKey, new JsonObject
            {
                ["id"] = $"hr-{id}-page",
                ["fields"] = new JsonArray([.. fields]),
            }, office: true);
        }

        /// <summary>B qism satri: «Rad etish sababi: ____» — so'z yorliqqa, chiziq izohga.</summary>
        private static JsonObject Blank(string id, string labelKey, string? hintKey = null)
        {
            var (label, line) = Split(P(labelKey));
            var hint = hintKey is not null ? P(hintKey) : line;
            return Field(id, "short_text", L(label), hint: hint is null ? null : L(hint));
        }

        private static JsonObject Score(string id, string labelKey)
            => Field(id, "number", L(P(labelKey)), hint: L(P("pdf.hr.score")), extra: [("min", 1), ("max", 5)]);

        private static (string Label, string? Line) Split(string text)
        {
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            return colon > 0 && text[(colon + 1)..].Contains('_', StringComparison.Ordinal)
                ? (text[..colon].Trim(), text[(colon + 1)..].Trim())
                : (text, null);
        }

        private static JsonObject Item(string labelKey, string? auto = null, string? note = null)
        {
            var item = new JsonObject { ["label"] = P(labelKey) };
            if (auto is not null)
            {
                item["auto"] = auto;
            }

            if (note is not null)
            {
                item["note"] = note;
            }

            return item;
        }

        private static JsonObject Choice(string value, string label) => new() { ["value"] = value, ["label"] = L(label) };

        // ── qurilish bloklari ──

        private static JsonObject Section(string id, JsonObject title, string pdfTitleKey, params JsonObject[] screens)
            => Section(id, title, pdfTitleKey, office: false, screens);

        private static JsonObject Section(string id, JsonObject title, string pdfTitleKey, JsonObject screen, bool office)
            => Section(id, title, pdfTitleKey, office, [screen]);

        private static JsonObject Section(string id, JsonObject title, string pdfTitleKey, bool office, JsonObject[] screens) => new()
        {
            ["id"] = id,
            ["audience"] = office ? "office" : "candidate",
            ["title"] = title,
            ["pdf_title"] = SectionNumber().Replace(P(pdfTitleKey), string.Empty),
            ["screens"] = new JsonArray([.. screens]),
        };

        private JsonObject Screen(string id, string titleKey, params JsonObject[] fields)
            => HintedScreen(id, titleKey, null, fields);

        private JsonObject HintedScreen(string id, string titleKey, JsonObject? hint, params JsonObject[] fields)
        {
            var screen = new JsonObject { ["id"] = id, ["title"] = T(titleKey) };
            if (hint is not null)
            {
                screen["hint"] = hint;
            }

            screen["fields"] = new JsonArray([.. fields]);
            return screen;
        }

        private JsonObject Name(string id, string bind, bool required, string pdfKey)
            => Field(id, "short_text", T($"kiosk.field.{id}"), required, pdf: pdfKey,
                extra: [("bind", bind), ("max_length", QuestionnaireFormValidator.MaxNameLength)]);

        private static JsonObject Field(
            string id,
            string kind,
            JsonObject label,
            bool required = false,
            JsonObject? hint = null,
            string? pdf = null,
            (string Key, JsonNode? Value)[]? extra = null)
        {
            var field = new JsonObject
            {
                ["id"] = id,
                ["kind"] = kind,
                ["label"] = label,
                ["required"] = required,
            };

            if (hint is not null)
            {
                field["hint"] = hint;
            }

            // qog'ozdagi so'z boshqacha bo'lsa — pdf_label
            if (pdf is not null && P(pdf) is var printed && printed != label[FormTexts.Latin]!.GetValue<string>())
            {
                field["pdf_label"] = printed;
            }

            foreach (var (key, value) in extra ?? None)
            {
                field[key] = value;
            }

            return field;
        }

        private static JsonObject Is(string field, bool value) => new() { ["field"] = field, ["is"] = value };

        private static JsonObject In(string field, params string[] values)
            => new() { ["field"] = field, ["in"] = new JsonArray([.. values.Select(value => (JsonNode)value)]) };

        private JsonObject Aspect(string id, string labelKey) => new() { ["id"] = id, ["label"] = T(labelKey) };

        private JsonArray Options(string listId, Func<string, bool>? keep = null)
        {
            var options = new JsonArray();
            foreach (var option in catalog.DefaultOptions(listId).Where(option => keep?.Invoke(option.Value) ?? true))
            {
                var item = new JsonObject
                {
                    ["value"] = option.Value,
                    ["label"] = Languages(option.Label),
                };

                if (option.Group is { } group)
                {
                    item["group"] = group;
                }

                options.Add(item);
            }

            return options;
        }

        /// <summary>Kiosk matni — uch til.</summary>
        private JsonObject T(string key) => Languages(catalog.Items[key].Default);

        /// <summary>Ikki kiosk matni bir izohda (masalan, ko'rsatma va shkala).</summary>
        private JsonObject Joined(string first, string second)
        {
            var text = T(first);
            var tail = T(second);
            foreach (var language in FormTexts.Languages)
            {
                text[language] = text[language]!.GetValue<string>() + "\n" + tail[language]!.GetValue<string>();
            }

            return text;
        }

        private static JsonObject Languages(IReadOnlyDictionary<string, string> texts)
        {
            var text = new JsonObject();
            foreach (var language in FormTexts.Languages)
            {
                if (texts.TryGetValue(language, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    text[language] = value;
                }
            }

            return text;
        }

        /// <summary>Qo'lda yozilgan matn; kirill va rus bo'lmasa HRMentor lotindan to'ldiradi.</summary>
        private static JsonObject L(string latin, string? cyrillic = null, string? russian = null)
        {
            var text = new JsonObject { [FormTexts.Latin] = latin };
            if (cyrillic is not null)
            {
                text[FormTexts.Cyrillic] = cyrillic;
            }

            if (russian is not null)
            {
                text[FormTexts.Russian] = russian;
            }

            return text;
        }

        /// <summary>PDF matni (asl, lotin).</summary>
        private static string P(string key) => QuestionnairePdfTexts.Default(key);

        /// <summary>«HUJJATLAR NAZORATI» → «Hujjatlar nazorati».</summary>
        private static string Sentence(string upper)
            => upper.Length == 0 ? upper : upper[..1] + upper[1..].ToLowerInvariant();
    }
}
