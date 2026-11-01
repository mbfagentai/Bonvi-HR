using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Forms;

/*
  ═══════════════════════════════════════════════════════════════════
   PLANSHET ANKETASI (HR-F02 v3.1) — JAVOBLAR SXEMASI

   Manba: egasining maydonlar ro'yxati (HR-F02_Online_forma_maydonlari.xlsx)
   va SPEC-planshet-anketa §3. Kalit nomlari ro'yxatdagidek (camelCase JSON).

   Majburiy maydonlar ham shu yerda NULLABLE — ataylab: «yuborilmagan»
   holatini sukut qiymatdan (0, false) ajratib, «to'ldirilmagan» degan aniq
   xatoni maydon nomi bilan qaytarish uchun. Tekshiruvchi
   (`QuestionnaireFormValidator`) ularni to'ldirilganini kafolatlaydi va
   TOZALANGAN nusxani qaytaradi — bazaga va PDF'ga faqat o'sha boradi.

   Domen enumlari (ma'lumot darajasi, oilaviy holat) QAYTA ishlatiladi:
   nomlari SPEC dagi satrlar bilan aynan bir xil va anketaning o'sha
   javoblari `CandidateQuestionnaire` ga to'g'ridan-to'g'ri yoziladi.
   «Qayerdan bilgan» esa enum emas — «Qayerdan bilgan» ma'lumotnomasidagi
   kanalning identifikatori (SPEC-qayerdan-bilgan §5).
   Faqat shu blankaga xos qiymatlar (uy turi, harbiy xizmat, til,
   qarindoshlik) quyidagi alohida enumlarda.

   Jinsi, pasport (seriya-raqam, kim va qachon bergan) va JSHSHIR
   SO'RALMAYDI (egasi, 2026-10-26): pasport kengash suhbatidan o'tgandan
   keyin olinadi, anketada esa bu juda shaxsiy ma'lumot. Bu kalitlar
   kelsa ham boshqa noma'lum kalitlar kabi e'tiborsiz qoladi va
   saqlanmaydi (`QuestionnaireFormJson` izohi).

   3.1 (HR rahbari, egasi tasdiqlagan, 2026-10-27): «Avval ishlaganmisiz?»
   (`hasWorkExperience`) qo'shildi — «Ha» bo'lsa ish joyi, tavsiya beruvchi
   va ishga oid ikki savol majburiy, «Yo'q» bo'lsa ular saqlanmaydi;
   «Umumiy ish tajribasi» (yil/oy) olib tashlandi; ishga chiqish sanasi
   o'rniga to'rt variantli tanlov (`startAfter`); kredit savoli majburiy.
  ═══════════════════════════════════════════════════════════════════
*/

/// <summary>Uy turi.</summary>
public enum FormHousingType
{
    /// <summary>Ko'p qavatli uy.</summary>
    Apartment = 1,

    /// <summary>Hovli.</summary>
    House = 2,
}

/// <summary>Harbiy xizmat — ixtiyoriy, hammaga (jins so'ralmaydi; yorliq qog'ozdagidek «erkaklar uchun»).</summary>
public enum FormMilitaryService
{
    /// <summary>O'tagan.</summary>
    Served = 1,

    /// <summary>Zaxirada.</summary>
    Reserve = 2,

    /// <summary>Kechiktirilgan.</summary>
    Deferred = 3,

    /// <summary>O'tamagan.</summary>
    NotServed = 4,
}

/// <summary>Til jadvalining qatori.</summary>
public enum FormLanguage
{
    /// <summary>O'zbek tili.</summary>
    Uzbek = 1,

    /// <summary>Rus tili — majburiy qator.</summary>
    Russian = 2,

    /// <summary>Ingliz tili.</summary>
    English = 3,

    /// <summary>Xitoy tili.</summary>
    Chinese = 4,

    /// <summary>Boshqa — nomi <see cref="FormLanguageSkill.OtherName"/> da.</summary>
    Other = 5,
}

/// <summary>Oila a'zosining qarindoshligi (egasining ro'yxati).</summary>
public enum FormFamilyRelation
{
    /// <summary>Otasi.</summary>
    Father = 1,

    /// <summary>Onasi.</summary>
    Mother = 2,

    /// <summary>Turmush o'rtog'i.</summary>
    Spouse = 3,

    /// <summary>Akasi / ukasi.</summary>
    Brother = 4,

    /// <summary>Opasi / singlisi.</summary>
    Sister = 5,

    /// <summary>Farzandi.</summary>
    Child = 6,

    /// <summary>Boshqa.</summary>
    Other = 7,
}

/// <summary>«Ishga qachondan chiqa olasiz?» — qabul qilingan kunga nisbatan (3.1 dan, sana o'rniga).</summary>
public enum FormStartAfter
{
    /// <summary>Qabul qilingan kundan.</summary>
    OnHireDay = 1,

    /// <summary>Qabul qilingandan 1 hafta keyin.</summary>
    OneWeek = 2,

    /// <summary>Qabul qilingandan 10 kun keyin.</summary>
    TenDays = 3,

    /// <summary>Qabul qilingandan 15 kun keyin.</summary>
    FifteenDays = 4,
}

/// <summary>O'qigan joyi — «Ma'lumoti» jadvalining qatori.</summary>
public sealed record FormEducation(
    string? Institution,
    string? Faculty = null,
    int? StartYear = null,
    int? EndYear = null,
    string? DiplomaNumber = null);

/// <summary>Til bilish darajasi — 0 (bilmayman) dan 5 (ona tili) gacha.</summary>
public sealed record FormLanguageSkill(
    FormLanguage? Language,
    string? OtherName = null,
    int? Speaking = null,
    int? Reading = null,
    int? Writing = null);

/// <summary>Ish joyi — «Mehnat faoliyati» jadvalining qatori. Oylar <c>yyyy-MM</c>.</summary>
public sealed record FormJob(
    string? Organization,
    string? Position = null,
    string? From = null,
    string? To = null,
    bool Current = false,
    string? LeaveReason = null);

/// <summary>Tavsiya beruvchi.</summary>
public sealed record FormReference(
    string? FullName,
    string? OrganizationPosition = null,
    string? Phone = null);

/// <summary>Oila a'zosi.</summary>
public sealed record FormFamilyMember(
    FormFamilyRelation? Relation,
    string? FullName,
    int? BirthYear = null,
    string? Workplace = null,
    string? Phone = null,
    string? Address = null);

/// <summary>
/// HR-F02 anketasining barcha javoblari (A qism, 0–11 bo'limlar).
/// </summary>
/// <remarks>
/// Nomlar SPEC §3 dagi kalitlar — forma aynan shu nomlar bilan yuboradi.
/// </remarks>
public sealed record QuestionnaireFormAnswers
{
    // ── 0. Vakansiya ──

    /// <summary>Qaysi vakansiyaga; <c>null</c> — «Umumiy (istalgan lavozim)».</summary>
    public Guid? VacancyId { get; init; }

    // ── 1. Shaxsiy ma'lumotlar ──
    public string? LastName { get; init; }

    public string? FirstName { get; init; }

    public string? MiddleName { get; init; }

    public DateOnly? BirthDate { get; init; }

    public string? RegisteredAddress { get; init; }

    public string? ActualAddress { get; init; }

    public FormHousingType? HousingType { get; init; }

    public string? Mahalla { get; init; }

    public string? Phone { get; init; }

    public string? PhoneExtra { get; init; }

    public string? Telegram { get; init; }

    public string? Email { get; init; }

    public MaritalStatus? MaritalStatus { get; init; }

    public int? ChildrenCount { get; init; }

    public FormMilitaryService? MilitaryService { get; init; }

    public int? MilitaryServiceMonths { get; init; }

    public bool? HasDriverLicense { get; init; }

    public IReadOnlyList<string>? DriverLicenseCategories { get; init; }

    public bool? HasCar { get; init; }

    public string? CarModel { get; init; }

    // ── 2. Ma'lumoti ──
    public EducationLevel? EducationLevel { get; init; }

    public IReadOnlyList<FormEducation>? Educations { get; init; }

    public string? SchoolName { get; init; }

    public string? StrongSubjects { get; init; }

    public string? Courses { get; init; }

    // ── 3. Tillar ──
    public IReadOnlyList<FormLanguageSkill>? Languages { get; init; }

    // ── 4. Kompyuter ──

    /// <summary>
    /// Dastur kaliti → daraja (1–4). Kalit yo'q — bilmaydi. Kalitlar faqat
    /// <see cref="QuestionnaireFormCatalog.ComputerGroups"/> dagi 11 ta va HR ning HRMentor dagi
    /// kompyuter ro'yxatidagilar (<c>x-…</c>, SPEC-blankalar-hrmentor §5.2), boshqasi — 400.
    /// </summary>
    public IReadOnlyDictionary<string, int>? ComputerSkills { get; init; }

    /// <summary>«Boshqa dasturlar va dasturlash tillari» — ro'yxatda yo'q hamma narsa shu matnga.</summary>
    public string? ComputerSkillsOther { get; init; }

    // ── 5. Mehnat faoliyati ──

    /// <summary>
    /// «Avval biror joyda ishlaganmisiz?» — majburiy (3.1). <c>true</c>: kamida bitta ish joyi va
    /// bitta tavsiya beruvchi; <c>false</c>: ish joylari, tavsiya beruvchilar va ishga oid ikki
    /// savol (<see cref="LastJobAchievement"/>, <see cref="BestIdea"/>) saqlanmaydi.
    /// </summary>
    /// <remarks>
    /// «Umumiy ish tajribasi» (<c>totalExperienceYears</c>, <c>totalExperienceMonths</c>) 3.1 da
    /// olib tashlandi: eski 3.0 yozuvidagi bu kalitlar boshqa noma'lum kalitlar kabi o'qilmaydi.
    /// </remarks>
    public bool? HasWorkExperience { get; init; }

    public IReadOnlyList<FormJob>? Jobs { get; init; }

    // ── 6. Tavsiya beruvchilar ──
    public IReadOnlyList<FormReference>? References { get; init; }

    // ── 7. Oila a'zolari ──
    public IReadOnlyList<FormFamilyMember>? FamilyMembers { get; init; }

    // ── 8. Ish sharoitlari va kutilmalar ──

    /// <summary>
    /// «Bu vakansiya haqida qayerdan bildingiz?» — FAOL kanal (seansdagi <c>referralChannels</c>
    /// ro'yxatidan). Majburiy. Eski <c>referralSource</c> (sanoq) kaliti endi tanilmaydi.
    /// </summary>
    public Guid? ReferralChannelId { get; init; }

    /// <summary>Aniqlik («kim / qaysi manba») — kanal so'rasa majburiy, aks holda saqlanmaydi.</summary>
    public string? ReferralDetail { get; init; }

    public bool? HasAcquaintances { get; init; }

    public string? AcquaintancesDetail { get; init; }

    public long? LastSalary { get; init; }

    public long? ExpectedSalary { get; init; }

    /// <summary>«Ishga qachondan chiqa olasiz?» — majburiy tanlov (3.1).</summary>
    public FormStartAfter? StartAfter { get; init; }

    /// <summary>
    /// ESKI (3.0) javob — aniq sana. Faqat bazadagi 3.0 anketalari PDF'da qayta chizilishi uchun.
    /// </summary>
    /// <remarks>
    /// Yangi anketada so'ralmaydi: tekshiruvchi uni talab qilmaydi va tozalangan nusxaga
    /// HECH QACHON o'tkazmaydi (formadan kelsa ham tashlanadi). PDF sanani faqat
    /// <see cref="StartAfter"/> yo'q bo'lganda chiqaradi.
    /// </remarks>
    public DateOnly? AvailableFrom { get; init; }

    public bool? CanWorkShifts { get; init; }

    public bool? CanWorkOvertime { get; init; }

    public bool? CanTravel { get; init; }

    public bool? HasOtherJob { get; init; }

    public string? OtherJobDetail { get; init; }

    /// <summary>«Hozirda kredit to'lovlaringiz bormi?» — majburiy (3.1); <c>true</c> bo'lsa oylik to'lov ham.</summary>
    public bool? HasLoan { get; init; }

    public long? LoanMonthlyPayment { get; init; }

    public bool? AgreesToTrial { get; init; }

    public bool? AgreesParentsInvite { get; init; }

    public string? StartConditions { get; init; }

    // ── 9. Sog'liq va qo'shimcha ──
    public bool? HasHealthRestriction { get; init; }

    public string? HealthRestrictionDetail { get; init; }

    public bool? HasFamilyPatient { get; init; }

    public bool? HasConviction { get; init; }

    public string? ConvictionDetail { get; init; }

    public bool? RelativesConviction { get; init; }

    public string? RelativesConvictionDetail { get; init; }

    public string? AbroadTrips { get; init; }

    // ── 10. O'zingiz haqingizda ──
    public string? Motivation { get; init; }

    public string? WhyUs { get; init; }

    public string? Strengths { get; init; }

    public string? Weaknesses { get; init; }

    public string? Motivators { get; init; }

    public string? SuccessDefinition { get; init; }

    public string? LastJobAchievement { get; init; }

    public string? BestIdea { get; init; }

    public string? BiggestChallenge { get; init; }

    public string? StressHandling { get; init; }

    public string? UnfairnessToYou { get; init; }

    public string? UnfairnessToCompany { get; init; }

    public string? Teamwork { get; init; }

    public string? PreviousTeamOpinion { get; init; }

    public string? StudentWorkExperience { get; init; }

    public string? FiveYearPlan { get; init; }

    public string? ExtraNote { get; init; }

    // ── 11. Tilxat ──
    public bool? ConsentAccuracy { get; init; }

    public bool? ConsentPersonalData { get; init; }

    public bool? ConsentReferenceCheck { get; init; }

    public bool? ConsentNotifyChanges { get; init; }

    public bool? ConsentConfidentiality { get; init; }

    public string? SignatureName { get; init; }
}

/// <summary>Anketa JSON'ining yagona serializatsiya sozlamasi — o'qishda ham, yozishda ham.</summary>
/// <remarks>
/// <para>
/// Enumlar MATN (<c>"Married"</c>), son emas: bazadagi <c>jsonb</c> o'qiladigan
/// bo'lsin va enumga qiymat qo'shilganda eski yozuvlar siljimasin. Son bilan
/// kelgan enum RAD etiladi (<c>allowIntegerValues: false</c>) — forma
/// faqat nomlarni yuboradi.
/// </para>
/// <para>
/// Bo'sh qiymatlar yozilmaydi (<c>WhenWritingNull</c>): hujjat ixcham va
/// «javob berilmagan» bilan «bo'sh javob» farqi yo'qolmaydi.
/// </para>
/// <para>
/// Sxemada yo'q kalit (masalan, 2026-10-26 da olib tashlangan <c>passportNumber</c>,
/// <c>pinfl</c>, <c>gender</c> yoki 3.1 da olib tashlangan <c>totalExperienceYears</c>,
/// <c>totalExperienceMonths</c>) RAD ETILMAYDI — e'tiborsiz qoladi (freymvorkning
/// sukut qoidasi). U baribir hech qayerga yetmaydi: bazaga va PDF'ga tekshiruvchi
/// qaytargan yozuv boradi, unda bunday xususiyat yo'q. Bazadagi eski yozuv ham
/// shu sababli o'qiladi — faqat o'sha kalitlar PDF'da chiqmaydi.
/// </para>
/// </remarks>
public static class QuestionnaireFormJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Formadan kelgan matnni o'qiydi; buzuq JSON — <c>null</c>.</summary>
    public static QuestionnaireFormAnswers? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QuestionnaireFormAnswers>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Tozalangan javoblarni bazaga yoziladigan matnga aylantiradi.</summary>
    public static string Write(QuestionnaireFormAnswers answers)
        => JsonSerializer.Serialize(answers, Options);
}
