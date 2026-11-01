using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>
/// Onlayn anketaning SERVER tomonidagi yagona tekshiruvi (SPEC §3).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Birinchi xato qaytadi, hammasi emas.</b> Forma bir xil qoidalarni
/// o'zi ham tekshiradi va xatoni qadam ichida ko'rsatadi — server xatosi
/// faqat ikki tomon kelishmay qolgan nodir holat. O'shanda nomzodga bitta
/// aniq «qaysi maydon» yetarli: xato tafsilotida <c>field</c> — maydon yo'li
/// (<c>familyMembers[0].fullName</c>), forma shunga qarab tegishli qadamni
/// ochadi. Tekshiruv tartibi blanka tartibida — birinchi xato eng oldingi
/// qadamga tushadi.
/// </para>
/// <para>
/// <b>Uzun matn QIRQILMAYDI</b> (<see cref="CandidateQuestionnaire"/> dan
/// farqi): u yerda matn botdan keladi va qirqish anketani yo'qotishdan yaxshi
/// edi; bu yerda esa forma chegarani maydonning o'zida ushlaydi, ya'ni
/// chegaradan oshgan matn — ikki tomon kelishmaganining belgisi va jim
/// qirqish imzolangan hujjatdan so'zlarni o'chirib yuborardi.
/// </para>
/// <para>
/// Natija — TOZALANGAN nusxa: bo'sh matn <c>null</c>, telefon kanonik, toifa va
/// tillar qog'ozdagi tartibda, shartli maydonlar sharti bo'lmasa tashlangan.
/// Bazaga ham, PDF'ga ham faqat shu boradi.
/// </para>
/// </remarks>
public static class QuestionnaireFormValidator
{
    /// <summary>Ism, familiya, otasining ismi.</summary>
    public const int MaxNameLength = 60;

    /// <summary>Qisqa matn.</summary>
    public const int MaxShortLength = 200;

    /// <summary>Manzil.</summary>
    public const int MaxAddressLength = 300;

    /// <summary>Uzun matn («O'zingiz haqingizda» va boshqalar).</summary>
    public const int MaxLongLength = 1000;

    /// <summary>«Qo'shimcha ma'lumot» — egasining ro'yxatida alohida chegara.</summary>
    public const int MaxExtraNoteLength = 2000;

    /// <summary>«O'zingiz haqingizda» javobidagi eng kam harf soni («Ha» o'tadi, «.» o'tmaydi).</summary>
    public const int MinAnswerLetters = 2;

    /// <summary>Pul summasining yuqori chegarasi (so'm).</summary>
    public const long MaxMoney = 1_000_000_000;

    public const int MaxEducations = 5;

    public const int MaxJobs = 5;

    public const int MaxReferences = 3;

    public const int MaxFamilyMembers = 8;

    public const int MaxOtherLanguages = 3;

    private static readonly Regex TelegramPattern = new("^[A-Za-z0-9_]{5,32}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex MonthPattern = new("^[0-9]{4}-[0-9]{2}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// 3.0 formadan kelgan anketami — deploy paytida brauzerda OCHIQ qolgan eski sahifa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Forma sahifasi o'zini o'zi yangilamaydi: ofis plansheti yoki nomzodning telefoni 3.1 chiqqandan
    /// keyin ham eski kod bilan anketa yuboradi. Uni 3.1 qoidasi bilan tekshirilsa, eski forma bilmaydigan
    /// maydonlar («hasWorkExperience», «startAfter») majburiy bo'lib qaytadi va nomzod yigirma daqiqalik
    /// anketasini yubora olmay qoladi. Shuning uchun bunday anketa 3.0 qoidasi bilan qabul qilinadi va
    /// «3.0» versiyasi bilan saqlanadi (egasi: «serverga chiqarishda uzilish bo'lmasin», 2026-10-27).
    /// </para>
    /// <para>
    /// Belgisi: 3.0 forma «availableFrom» sanasini HAR DOIM yuboradi (u yerda majburiy edi) va yangi
    /// ikki savolni bilmaydi; 3.1 forma esa sanani umuman yubormaydi.
    /// </para>
    /// </remarks>
    public static bool IsLegacy(QuestionnaireFormAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        return answers.HasWorkExperience is null && answers.StartAfter is null && answers.AvailableFrom is not null;
    }

    /// <summary>Javoblarni tekshiradi va tozalangan nusxasini qaytaradi.</summary>
    /// <param name="answers">Formadan kelgan javoblar.</param>
    /// <param name="today">Toshkent bo'yicha bugun (yosh va sana chegaralari uchun).</param>
    /// <param name="referralChannels">
    /// «Qayerdan bilgan» ning FAOL kanallari — seansda formaga berilgan ro'yxat. Tekshiruvchi
    /// bazani ko'rmaydi: ro'yxatni chaqiruvchi beradi, qoida esa shu yerda qoladi.
    /// </param>
    /// <param name="extraComputerKeys">
    /// Koddagi 11 tadan tashqari qabul qilinadigan dastur kalitlari — HR ning HRMentor dagi
    /// kompyuter ro'yxati (SPEC-blankalar-hrmentor §5.2), yashirilganlari ham. Boshqa kalit — 400.
    /// </param>
    public static Result<QuestionnaireFormAnswers> Validate(
        QuestionnaireFormAnswers answers,
        DateOnly today,
        IReadOnlyCollection<ReferralChannelOptionDto> referralChannels,
        IReadOnlySet<string>? extraComputerKeys = null)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(referralChannels);

        var check = new Check(today, referralChannels, extraComputerKeys ?? new HashSet<string>(StringComparer.Ordinal));

        try
        {
            return check.Run(answers);
        }
        catch (FormFieldException failure)
        {
            return failure.Error;
        }
    }

    /// <summary>
    /// Imzo o'rnidagi ism anketadagi familiya va ism bilan mosmi.
    /// </summary>
    /// <remarks>
    /// Lotin va kirill yozuvi farqsiz (kirill lotinga o'giriladi), tutuq
    /// belgilarining hamma ko'rinishi (<c>' ʻ ʼ ‘ ’ `</c>) bitta belgi deb —
    /// aniqrog'i umuman hisobga olinmaydi: «G‘ulomov», «G'ulomov» va
    /// «Ғуломов» bir xil. Registr ahamiyatsiz.
    /// </remarks>
    public static bool SignatureMatches(string? signature, string? lastName, string? firstName)
    {
        var signed = Fold(signature);
        var last = Fold(lastName);
        var first = Fold(firstName);

        return signed.Length > 0
            && last.Length > 0
            && first.Length > 0
            && signed.Contains(last, StringComparison.Ordinal)
            && signed.Contains(first, StringComparison.Ordinal);
    }

    /// <summary>
    /// Matnda ruxsat etilmagan belgi bormi (SPEC §3, xavfsizlik ko'rigi 2026-10-24).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rad etiladi: boshqarish belgilari (NUL va boshqalar — PostgreSQL ularni <c>jsonb</c> ga
    /// yozmaydi va so'rov PDF chizilgandan KEYIN 500 bo'lardi), yo'nalishni almashtiruvchi
    /// belgilar (U+202A–U+202E, U+2066–U+2069 — «Каримов» ni ekranda boshqa ism qilib
    /// ko'rsatadi), ko'rinmas belgilar (U+200B–U+200D, U+2060, U+FEFF), shaxsiy foydalanish
    /// va tayinlanmagan kod nuqtalari.
    /// </para>
    /// <para>
    /// Yangi qator va tab faqat UZUN matnda (<paramref name="multiline"/>) — u yerda nomzod
    /// xatboshi qiladi. Ism, telefon, manzil kabi bir qatorli maydonda ular rad etiladi.
    /// </para>
    /// </remarks>
    public static bool HasForbiddenCharacter(string text, bool multiline)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;

            if (value is '\n' or '\r' or '\t')
            {
                if (!multiline)
                {
                    return true;
                }

                continue;
            }

            if (value is (>= 0x202A and <= 0x202E) or (>= 0x2066 and <= 0x2069)
                or (>= 0x200B and <= 0x200D) or 0x2060 or 0xFEFF)
            {
                return true;
            }

            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned
                or UnicodeCategory.Surrogate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Taqqoslash uchun yig'ish: kichik harf, kirill → lotin, tutuqlarsiz, bitta bo'shliq.</summary>
    internal static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;

        foreach (var symbol in value.Trim().ToLowerInvariant())
        {
            if (symbol is '\'' or '`' or 'ʻ' or 'ʼ' or '‘' or '’' or 'ъ' or 'ь')
            {
                continue;
            }

            if (char.IsWhiteSpace(symbol) || symbol == '-')
            {
                if (!lastWasSpace)
                {
                    _ = builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            lastWasSpace = false;
            _ = builder.Append(Latin(symbol));
        }

        return builder.ToString().Trim();
    }

    private static string Latin(char symbol) => symbol switch
    {
        'а' => "a",
        'б' => "b",
        'в' => "v",
        'г' or 'ғ' => "g",
        'д' => "d",
        'е' or 'э' => "e",
        'ё' => "yo",
        'ж' => "j",
        'з' => "z",
        'и' or 'ы' => "i",
        'й' => "y",
        'к' => "k",
        'қ' => "q",
        'л' => "l",
        'м' => "m",
        'н' => "n",
        'о' or 'ў' => "o",
        'п' => "p",
        'р' => "r",
        'с' or 'ц' => "s",
        'т' => "t",
        'у' => "u",
        'ф' => "f",
        'х' => "x",
        'ҳ' => "h",
        'ч' => "ch",
        'ш' or 'щ' => "sh",
        'ю' => "yu",
        'я' => "ya",
        _ => symbol.ToString(),
    };

    /// <summary>Tekshiruvni to'xtatadigan birinchi xato — faqat shu sinf ichida ushlanadi.</summary>
    private sealed class FormFieldException(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }

    /// <summary>Bitta tekshiruv yurishi.</summary>
    /// <remarks>
    /// Istisno bilan to'xtatish ATAYLAB: yuzga yaqin maydonning har birida
    /// «xato bo'lsa qaytar» shartini yozish kodni uch barobar uzaytirardi va
    /// bittasini unutish oson edi. Istisno sinf tashqarisiga chiqmaydi.
    /// </remarks>
    private sealed class Check(
        DateOnly today,
        IReadOnlyCollection<ReferralChannelOptionDto> referralChannels,
        IReadOnlySet<string> extraComputerKeys)
    {
        public Result<QuestionnaireFormAnswers> Run(QuestionnaireFormAnswers a)
        {
            // Eski (3.0) sahifadan kelganmi — 5, 8 va 10-bo'limlarda o'sha qoidalar (IsLegacy izohi)
            var legacy = IsLegacy(a);

            // ── 1. Shaxsiy ma'lumotlar ──
            var lastName = Required(a.LastName, "lastName", MaxNameLength);
            var firstName = Required(a.FirstName, "firstName", MaxNameLength);
            var middleName = Optional(a.MiddleName, "middleName", MaxNameLength);

            var birthDate = Need(a.BirthDate, "birthDate");
            var age = Age(birthDate);
            Ensure(age is >= 16 and <= 70, "birthDate");

            var registered = Required(a.RegisteredAddress, "registeredAddress", MaxAddressLength);
            var actual = Required(a.ActualAddress, "actualAddress", MaxAddressLength);

            if (a.HousingType is { } housing)
            {
                EnsureDefined(housing, "housingType");
            }

            var mahalla = Optional(a.Mahalla, "mahalla", MaxShortLength);

            var phone = Phone(a.Phone, "phone", required: true)!;
            var phoneExtra = Phone(a.PhoneExtra, "phoneExtra", required: false);
            var telegram = Telegram(a.Telegram);
            var email = Email(a.Email);

            var marital = Need(a.MaritalStatus, "maritalStatus");
            EnsureDefined(marital, "maritalStatus");

            Ensure(a.ChildrenCount is null or (>= 0 and <= 20), "childrenCount");

            // Harbiy xizmat — ixtiyoriy, hammaga (2026-10-26): jins endi so'ralmaydi.
            // Muddat faqat «O'tagan» da, boshqa javobda tashlanadi.
            var military = a.MilitaryService;
            int? militaryMonths = null;

            if (military is { } service)
            {
                EnsureDefined(service, "militaryService");

                if (service == FormMilitaryService.Served && a.MilitaryServiceMonths is { } months)
                {
                    Ensure(months is >= 1 and <= 60, "militaryServiceMonths");
                    militaryMonths = months;
                }
            }

            var hasLicense = Need(a.HasDriverLicense, "hasDriverLicense");
            IReadOnlyList<string> categories = [];

            if (hasLicense)
            {
                var chosen = (a.DriverLicenseCategories ?? [])
                    .Select(category => category?.Trim().ToUpperInvariant() ?? string.Empty)
                    .ToHashSet(StringComparer.Ordinal);

                var catalog = legacy
                    ? QuestionnaireFormCatalog.LegacyDriverLicenseCategories
                    : QuestionnaireFormCatalog.DriverLicenseCategories;

                Ensure(chosen.Count > 0, "driverLicenseCategories", missing: true);
                Ensure(chosen.All(catalog.Contains), "driverLicenseCategories");

                categories = [.. catalog.Where(chosen.Contains)];
            }

            var carModel = a.HasCar == true ? Optional(a.CarModel, "carModel", MaxShortLength) : null;

            // ── 2. Ma'lumoti ──
            var education = Need(a.EducationLevel, "educationLevel");
            EnsureDefined(education, "educationLevel");

            var educations = Rows(a.Educations, "educations", MaxEducations, (row, path) =>
            {
                var startYear = Year(row.StartYear, $"{path}.startYear", 1950);
                var endYear = Year(row.EndYear, $"{path}.endYear", 1950);
                Ensure(startYear is null || endYear is null || startYear <= endYear, $"{path}.endYear");

                return new FormEducation(
                    Required(row.Institution, $"{path}.institution", MaxShortLength),
                    Optional(row.Faculty, $"{path}.faculty", MaxShortLength),
                    startYear,
                    endYear,
                    Optional(row.DiplomaNumber, $"{path}.diplomaNumber", MaxShortLength));
            });

            var schoolName = Optional(a.SchoolName, "schoolName", MaxShortLength);
            var strongSubjects = Optional(a.StrongSubjects, "strongSubjects", MaxShortLength);
            var courses = Optional(a.Courses, "courses", MaxLongLength);

            // ── 3. Tillar ──
            var languages = Languages(a.Languages);

            // ── 4. Kompyuter ──
            var computer = Computer(a.ComputerSkills);
            var computerOther = Optional(a.ComputerSkillsOther, "computerSkillsOther", MaxShortLength);

            // ── 5. Mehnat faoliyati ──

            // «Avval ishlaganmisiz?» (3.1, egasi tasdiqlagan): «Ha» — kamida bitta ish joyi va
            // bitta tavsiya beruvchi; «Yo'q» — kelgan qatorlar TEKSHIRILMAYDI va saqlanmaydi
            // (forma ularni ko'rsatmaydi; ortda qolgan qoralama imzolangan hujjatga tushmasin).
            // 3.0 sahifada bu savol yo'q edi — javobsiz qoladi, qatorlar esa avvalgidek ixtiyoriy.
            bool? hasWorkExperience = legacy ? null : Need(a.HasWorkExperience, "hasWorkExperience");
            List<FormJob> jobs = [];
            List<FormReference> references = [];

            if (hasWorkExperience is not false)
            {
                Ensure(legacy || a.Jobs is { Count: > 0 }, "jobs", missing: true);

                jobs = Rows(a.Jobs, "jobs", MaxJobs, (row, path) =>
                {
                    var from = Month(row.From, $"{path}.from");
                    var to = row.Current ? null : Month(row.To, $"{path}.to");
                    Ensure(from is null || to is null || string.CompareOrdinal(from, to) <= 0, $"{path}.to");

                    return new FormJob(
                        Required(row.Organization, $"{path}.organization", MaxShortLength),
                        Optional(row.Position, $"{path}.position", MaxShortLength),
                        from,
                        to,
                        row.Current,
                        Optional(row.LeaveReason, $"{path}.leaveReason", MaxShortLength));
                });

                // ── 6. Tavsiya beruvchilar — ishlagan bo'lsa kamida bitta ──
                Ensure(legacy || a.References is { Count: > 0 }, "references", missing: true);

                references = Rows(a.References, "references", MaxReferences, (row, path) => new FormReference(
                    Required(row.FullName, $"{path}.fullName", MaxShortLength),
                    Optional(row.OrganizationPosition, $"{path}.organizationPosition", MaxShortLength),
                    LoosePhone(row.Phone, $"{path}.phone")));
            }

            // ── 7. Oila a'zolari — kamida bitta ──
            Ensure(a.FamilyMembers is { Count: > 0 }, "familyMembers", missing: true);

            var family = Rows(a.FamilyMembers, "familyMembers", MaxFamilyMembers, (row, path) =>
            {
                var relation = Need(row.Relation, $"{path}.relation");
                EnsureDefined(relation, $"{path}.relation");

                return new FormFamilyMember(
                    relation,
                    Required(row.FullName, $"{path}.fullName", MaxShortLength),
                    Year(row.BirthYear, $"{path}.birthYear", 1920),
                    Optional(row.Workplace, $"{path}.workplace", MaxShortLength),
                    LoosePhone(row.Phone, $"{path}.phone"),
                    Optional(row.Address, $"{path}.address", MaxAddressLength));
            });

            // ── 8. Ish sharoitlari va kutilmalar ──

            // Kanal — FAOL ro'yxatdan (SPEC-qayerdan-bilgan §5). Aniqlik faqat kanal so'rasa:
            // majburiy, aks holda saqlanmaydi (forma maydonni o'shandagina ko'rsatadi).
            var referralChannelId = Need(a.ReferralChannelId, "referralChannelId");
            var referralChannel = referralChannels.FirstOrDefault(channel => channel.Id == referralChannelId);

            if (referralChannel is null)
            {
                Fail(RecruitingErrors.ReferralChannelNotFound, "referralChannelId");
            }

            var referralDetail = referralChannel!.AsksDetail
                ? Required(a.ReferralDetail, "referralDetail", MaxShortLength)
                : null;

            var hasAcquaintances = Need(a.HasAcquaintances, "hasAcquaintances");
            var acquaintances = hasAcquaintances
                ? Required(a.AcquaintancesDetail, "acquaintancesDetail", MaxShortLength)
                : null;

            var lastSalary = Money(a.LastSalary, "lastSalary");
            var expectedSalary = Money(Need(a.ExpectedSalary, "expectedSalary"), "expectedSalary");

            // Sana emas, tanlov (3.1). Eski `availableFrom` talab qilinmaydi va saqlanmaydi —
            // faqat 3.0 sahifadan kelgan anketada (o'shandagi qoida: bugundan oldin emas).
            FormStartAfter? startAfter = null;
            DateOnly? availableFrom = null;

            if (legacy)
            {
                availableFrom = a.AvailableFrom;
                Ensure(availableFrom >= today, "availableFrom");
            }
            else
            {
                startAfter = Need(a.StartAfter, "startAfter");
                EnsureDefined(startAfter.Value, "startAfter");
            }

            var shifts = Need(a.CanWorkShifts, "canWorkShifts");
            var overtime = Need(a.CanWorkOvertime, "canWorkOvertime");
            var travel = Need(a.CanTravel, "canTravel");

            var hasOtherJob = Need(a.HasOtherJob, "hasOtherJob");
            var otherJob = hasOtherJob ? Required(a.OtherJobDetail, "otherJobDetail", MaxShortLength) : null;

            // Kredit — majburiy savol (3.1); «Ha» bo'lsa oylik to'lov ham majburiy va noldan katta
            // 3.0 sahifada ixtiyoriy edi — o'shandagidek
            bool? hasLoan = legacy ? a.HasLoan : Need(a.HasLoan, "hasLoan");
            long? loanPayment = null;

            if (hasLoan == true)
            {
                loanPayment = legacy
                    ? Money(a.LoanMonthlyPayment, "loanMonthlyPayment")
                    : Money(Need(a.LoanMonthlyPayment, "loanMonthlyPayment"), "loanMonthlyPayment");
                Ensure(legacy || loanPayment > 0, "loanMonthlyPayment");
            }

            var trial = Need(a.AgreesToTrial, "agreesToTrial");
            var startConditions = Optional(a.StartConditions, "startConditions", MaxLongLength);

            // ── 9. Sog'liq va qo'shimcha ──
            var health = Need(a.HasHealthRestriction, "hasHealthRestriction");
            var healthDetail = health ? Required(a.HealthRestrictionDetail, "healthRestrictionDetail", MaxShortLength) : null;

            var conviction = Need(a.HasConviction, "hasConviction");
            var convictionDetail = conviction ? Required(a.ConvictionDetail, "convictionDetail", MaxShortLength) : null;

            var relativesDetail = a.RelativesConviction == true
                ? Optional(a.RelativesConvictionDetail, "relativesConvictionDetail", MaxShortLength)
                : null;

            var abroad = Optional(a.AbroadTrips, "abroadTrips", MaxLongLength);

            // ── 10. O'zingiz haqingizda ──
            // 2026-10-29 (egasi): HAMMA savol majburiy va javobda harf bo'lishi shart — nomzodlar
            // bo'sh qoldirib yoki «.» yozib o'tib ketardi (Answer). Avvalgi ish haqidagi uchtasi — faqat
            // avval ishlaganga (ishlamaganda kelgan matn saqlanmaydi, 5-bo'limdagi qoida).
            // 3.0 sahifadan kelganda — avvalgi qoidalar: birinchi to'rttasi majburiy, qolgani ixtiyoriy.
            string? Open(string? value, string path, int limit = MaxLongLength)
                => legacy ? Optional(value, path, limit) : Answer(value, path, limit);

            string? AboutWork(string? value, string path) => hasWorkExperience switch
            {
                true => Answer(value, path, MaxLongLength),
                null => Optional(value, path, MaxLongLength),
                false => null,
            };

            string First(string? value, string path)
                => legacy ? Required(value, path, MaxLongLength) : Answer(value, path, MaxLongLength);

            var motivation = First(a.Motivation, "motivation");
            var whyUs = First(a.WhyUs, "whyUs");
            var strengths = First(a.Strengths, "strengths");
            var weaknesses = First(a.Weaknesses, "weaknesses");
            var motivators = Open(a.Motivators, "motivators");
            var successDefinition = Open(a.SuccessDefinition, "successDefinition");
            var lastJobAchievement = AboutWork(a.LastJobAchievement, "lastJobAchievement");
            var bestIdea = AboutWork(a.BestIdea, "bestIdea");
            var biggestChallenge = Open(a.BiggestChallenge, "biggestChallenge");
            var stressHandling = Open(a.StressHandling, "stressHandling");
            var unfairnessToYou = Open(a.UnfairnessToYou, "unfairnessToYou");
            var unfairnessToCompany = Open(a.UnfairnessToCompany, "unfairnessToCompany");
            var teamwork = Open(a.Teamwork, "teamwork");
            var previousTeamOpinion = AboutWork(a.PreviousTeamOpinion, "previousTeamOpinion");
            var studentWorkExperience = Open(a.StudentWorkExperience, "studentWorkExperience");
            var fiveYearPlan = Open(a.FiveYearPlan, "fiveYearPlan");
            var extraNote = Open(a.ExtraNote, "extraNote", MaxExtraNoteLength);

            // ── 11. Tilxat ──
            Consent(a.ConsentAccuracy, "consentAccuracy");
            Consent(a.ConsentPersonalData, "consentPersonalData");
            Consent(a.ConsentReferenceCheck, "consentReferenceCheck");
            Consent(a.ConsentNotifyChanges, "consentNotifyChanges");
            Consent(a.ConsentConfidentiality, "consentConfidentiality");

            var signature = Required(a.SignatureName, "signatureName", MaxShortLength);

            if (!SignatureMatches(signature, lastName, firstName))
            {
                throw new FormFieldException(QuestionnaireFormErrors.SignatureNameMismatch.At("signatureName"));
            }

            return a with
            {
                LastName = lastName,
                FirstName = firstName,
                MiddleName = middleName,
                BirthDate = birthDate,
                RegisteredAddress = registered,
                ActualAddress = actual,
                Mahalla = mahalla,
                Phone = phone,
                PhoneExtra = phoneExtra,
                Telegram = telegram,
                Email = email,
                MaritalStatus = marital,
                MilitaryService = military,
                MilitaryServiceMonths = militaryMonths,
                HasDriverLicense = hasLicense,
                DriverLicenseCategories = categories,
                CarModel = carModel,
                EducationLevel = education,
                Educations = educations,
                SchoolName = schoolName,
                StrongSubjects = strongSubjects,
                Courses = courses,
                Languages = languages,
                ComputerSkills = computer,
                ComputerSkillsOther = computerOther,
                HasWorkExperience = hasWorkExperience,
                Jobs = jobs,
                References = references,
                FamilyMembers = family,
                ReferralChannelId = referralChannelId,
                ReferralDetail = referralDetail,
                HasAcquaintances = hasAcquaintances,
                AcquaintancesDetail = acquaintances,
                LastSalary = lastSalary,
                ExpectedSalary = expectedSalary,
                StartAfter = startAfter,

                // ⚠️ `with` qolgan hamma narsani kirishdan ko'chiradi — eski sana ATAYLAB qayta yoziladi
                // (3.1 formada doim null, faqat 3.0 sahifadan kelganda tekshirilgan sana)
                AvailableFrom = availableFrom,
                CanWorkShifts = shifts,
                CanWorkOvertime = overtime,
                CanTravel = travel,
                HasOtherJob = hasOtherJob,
                OtherJobDetail = otherJob,
                HasLoan = hasLoan,
                LoanMonthlyPayment = loanPayment,
                AgreesToTrial = trial,
                StartConditions = startConditions,
                HasHealthRestriction = health,
                HealthRestrictionDetail = healthDetail,
                HasConviction = conviction,
                ConvictionDetail = convictionDetail,
                RelativesConvictionDetail = relativesDetail,
                AbroadTrips = abroad,
                Motivation = motivation,
                WhyUs = whyUs,
                Strengths = strengths,
                Weaknesses = weaknesses,
                Motivators = motivators,
                SuccessDefinition = successDefinition,
                LastJobAchievement = lastJobAchievement,
                BestIdea = bestIdea,
                BiggestChallenge = biggestChallenge,
                StressHandling = stressHandling,
                UnfairnessToYou = unfairnessToYou,
                UnfairnessToCompany = unfairnessToCompany,
                Teamwork = teamwork,
                PreviousTeamOpinion = previousTeamOpinion,
                StudentWorkExperience = studentWorkExperience,
                FiveYearPlan = fiveYearPlan,
                ExtraNote = extraNote,
                SignatureName = signature,
            };
        }

        // ════════════════════════════════════════════════ yordamchilar

        private static void Fail(Error error, string path)
            => throw new FormFieldException(error.At(path));

        private static void Missing(string path)
            => Fail(QuestionnaireFormErrors.Required, path);

        private static void Ensure(bool condition, string path, bool missing = false)
        {
            if (condition)
            {
                return;
            }

            if (missing)
            {
                Missing(path);
            }

            Fail(QuestionnaireFormErrors.Invalid, path);
        }

        private static T Need<T>(T? value, string path)
            where T : struct
        {
            if (value is null)
            {
                Missing(path);
            }

            return value!.Value;
        }

        private static void EnsureDefined<TEnum>(TEnum value, string path)
            where TEnum : struct, Enum
            => Ensure(Enum.IsDefined(value), path);

        private static string? Optional(string? value, string path, int limit)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = value.Trim();

            if (text.Length > limit)
            {
                throw new FormFieldException(QuestionnaireFormErrors.TooLong.At(path, limit));
            }

            // Uzun matn chegarasidagi maydonlar — ko'p qatorli (textarea), qolgani bir qatorli
            if (HasForbiddenCharacter(text, multiline: limit >= MaxLongLength))
            {
                Fail(QuestionnaireFormErrors.ForbiddenCharacter, path);
            }

            return text;
        }

        private static string Required(string? value, string path, int limit)
        {
            var text = Optional(value, path, limit);

            if (text is null)
            {
                Missing(path);
            }

            return text!;
        }

        /// <summary>
        /// «O'zingiz haqingizda» javobi: majburiy va kamida <see cref="MinAnswerLetters"/> ta harf —
        /// «.», «-» yoki «1» javob emas (nomzodlar shunday yozib o'tib ketardi, 2026-10-29). Kod —
        /// «majburiy»: yangilanmagan ochiq sahifa ham uni taniydi va savolni ochib beradi.
        /// </summary>
        private static string Answer(string? value, string path, int limit)
        {
            var text = Required(value, path, limit);

            if (text.Count(char.IsLetter) < MinAnswerLetters)
            {
                Missing(path);
            }

            return text;
        }

        private static void Consent(bool? value, string path)
        {
            if (value != true)
            {
                Fail(QuestionnaireFormErrors.ConsentMissing, path);
            }
        }

        private int Age(DateOnly birthDate)
        {
            var age = today.Year - birthDate.Year;

            return birthDate > today.AddYears(-age) ? age - 1 : age;
        }

        private int? Year(int? value, string path, int from)
        {
            Ensure(value is null || (value >= from && value <= today.Year), path);
            return value;
        }

        private string? Month(string? value, string path)
        {
            var text = Optional(value, path, 7);
            if (text is null)
            {
                return null;
            }

            var valid = MonthPattern.IsMatch(text)
                && DateOnly.TryParseExact(text + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
                && month.Year >= 1950
                && month <= today;

            Ensure(valid, path);
            return text;
        }

        private static long? Money(long? value, string path)
        {
            Ensure(value is null or (>= 0 and <= MaxMoney), path);
            return value;
        }

        private static string? Phone(string? value, string path, bool required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    Missing(path);
                }

                return null;
            }

            var parsed = PhoneNumber.Create(value);

            if (parsed.IsFailure)
            {
                // Telefonning o'z xato kodi (`telefon.*`) — u allaqachon to'rt tilga tarjima qilingan
                throw new FormFieldException(parsed.Error.At(path));
            }

            return parsed.Value.Value;
        }

        /// <summary>
        /// Qarindosh va tavsiya beruvchining telefoni — qat'iy emas: shahar yoki
        /// chet el raqami ham bo'lishi mumkin. Faqat «raqamga o'xshashi» tekshiriladi.
        /// </summary>
        private static string? LoosePhone(string? value, string path)
        {
            var text = Optional(value, path, 30);
            if (text is null)
            {
                return null;
            }

            var digits = text.Count(char.IsAsciiDigit);
            var allowed = text.All(symbol => char.IsAsciiDigit(symbol) || symbol is '+' or ' ' or '-' or '(' or ')');

            Ensure(allowed && digits is >= 7 and <= 15, path);

            return PhoneNumber.Create(text) is { IsSuccess: true } uzbek ? uzbek.Value.Value : text;
        }

        private static string? Telegram(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var name = value.Trim().TrimStart('@');
            Ensure(TelegramPattern.IsMatch(name), "telegram");

            return name;
        }

        private static string? Email(string? value)
        {
            var text = Optional(value, "email", MaxShortLength);
            if (text is null)
            {
                return null;
            }

            Ensure(EmailPattern.IsMatch(text), "email");
            return text;
        }

        private static List<TRow> Rows<TRow>(
            IReadOnlyList<TRow>? rows,
            string path,
            int limit,
            Func<TRow, string, TRow> check)
        {
            if (rows is null)
            {
                return [];
            }

            Ensure(rows.Count <= limit, path);

            var result = new List<TRow>(rows.Count);

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var rowPath = $"{path}[{index}]";

                if (row is null)
                {
                    Missing(rowPath);
                }

                result.Add(check(row!, rowPath));
            }

            return result;
        }

        private static List<FormLanguageSkill> Languages(IReadOnlyList<FormLanguageSkill>? rows)
        {
            var result = new List<FormLanguageSkill>();
            var seen = new HashSet<FormLanguage>();
            var others = 0;

            for (var index = 0; index < (rows?.Count ?? 0); index++)
            {
                var row = rows![index];
                var path = $"languages[{index}]";

                if (row is null)
                {
                    Missing(path);
                }

                var language = Need(row!.Language, $"{path}.language");
                EnsureDefined(language, $"{path}.language");

                string? otherName = null;

                if (language == FormLanguage.Other)
                {
                    otherName = Required(row.OtherName, $"{path}.otherName", MaxNameLength);
                    others++;
                    Ensure(others <= MaxOtherLanguages, "languages");
                }
                else
                {
                    // Bitta til ikki qatorda — qaysi biri to'g'ri ekanini bilib bo'lmaydi
                    Ensure(seen.Add(language), $"{path}.language");
                }

                result.Add(new FormLanguageSkill(
                    language,
                    otherName,
                    Level(row.Speaking, $"{path}.speaking"),
                    Level(row.Reading, $"{path}.reading"),
                    Level(row.Writing, $"{path}.writing")));
            }

            // Rus tili — egasining ro'yxatida majburiy qator, uchala daraja bilan
            var russian = result.FindIndex(row => row.Language == FormLanguage.Russian);

            if (russian < 0)
            {
                Missing("languages");
            }

            var russianRow = result[russian];
            Ensure(russianRow.Speaking is not null, $"languages[{russian}].speaking", missing: true);
            Ensure(russianRow.Reading is not null, $"languages[{russian}].reading", missing: true);
            Ensure(russianRow.Writing is not null, $"languages[{russian}].writing", missing: true);

            // Qog'ozdagi tartib: o'zbek, rus, ingliz, xitoy, keyin «boshqa»lar kiritilgan tartibda
            return [.. result.OrderBy(row => (int)row.Language!.Value)];
        }

        private static int? Level(int? value, string path)
        {
            Ensure(value is null or (>= 0 and <= 5), path);
            return value;
        }

        private Dictionary<string, int> Computer(IReadOnlyDictionary<string, int>? skills)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);

            if (skills is null)
            {
                return result;
            }

            foreach (var (key, level) in skills)
            {
                var path = $"computerSkills.{key}";

                Ensure(QuestionnaireFormCatalog.ComputerKeys.Contains(key) || extraComputerKeys.Contains(key), path);
                Ensure(level is >= 1 and <= 4, path);

                result[key] = level;
            }

            return result;
        }
    }
}
