namespace Bonvi.Bot.Conversation;

/// <summary>Nomzodning ma'lumot darajasi — 7-qadamning tugmalari.</summary>
/// <remarks>
/// ⚠️ <b>A'zolarning NOMI serverga ketadigan qiymatning o'zi</b>: so'rov
/// tanasida u satr bo'lib yoziladi («SecondarySchool») va server uni shu
/// nom bo'yicha tanib oladi. Ya'ni nomni o'zgartirish — bu bazadagi
/// qiymatni o'zgartirish, oddiy qayta nomlash emas
/// (sabab <see cref="Answers"/> izohida).
/// </remarks>
public enum EducationAnswer
{
    /// <summary>O'rta — maktab.</summary>
    SecondarySchool = 1,

    /// <summary>O'rta maxsus — kollej yoki litsey.</summary>
    VocationalSchool = 2,

    /// <summary>Oliy — bakalavriat tugallangan.</summary>
    HigherComplete = 3,

    /// <summary>Oliy — tugallanmagan.</summary>
    HigherIncomplete = 4,
}

/// <summary>Oilaviy holat — 10-qadamning tugmalari.</summary>
public enum MaritalAnswer
{
    /// <summary>Oilali.</summary>
    Married = 1,

    /// <summary>Turmush qurmagan.</summary>
    /// <remarks>
    /// Nomi <c>Single</c> EMAS: u <c>System.Single</c> turi bilan
    /// to'qnashadi va analizator (CA1720) uni rad etadi. Serverdagi
    /// qiymat ham aynan shu sababdan <c>Unmarried</c>.
    /// </remarks>
    Unmarried = 2,

    /// <summary>Ajrashgan.</summary>
    Divorced = 3,
}

/// <summary>Rus tilini bilish darajasi — 11-qadamning tugmalari.</summary>
/// <remarks>
/// ⚠️ <b>Uchta tugma, lekin sanoq beshta darajali.</b> Serverda bu
/// xodimning til darajasi bilan AYNAN bir xil sanoq
/// (<c>LanguageLevel</c>): <c>UpperIntermediate</c> (3) va
/// <c>Native</c> (5) ham bor. Botda ular so'ralmaydi — nomzod o'zini
/// beshta pog'onadan birortasiga qo'ya olmaydi va uchta savol
/// yetarli aniqlik beradi; qolganini HR suhbatda aniqlashtiradi.
/// Shu sababli raqamlar 1, 2, 4 — ular serverdagi qiymatlar bilan bir
/// xil bo'lib qolishi kerak.
/// </remarks>
public enum RussianLevelAnswer
{
    /// <summary>Boshlang'ich — tushunmaydi.</summary>
    Elementary = 1,

    /// <summary>O'rta — tushunadi, qiynalib gapiradi.</summary>
    Intermediate = 2,

    /// <summary>Yaxshi — erkin gaplashadi.</summary>
    Advanced = 4,
}

/// <summary>Kutilayotgan maosh oralig'i — 19-qadamning tugmalari.</summary>
/// <remarks>
/// Oraliq, aniq son emas: aniq son so'ralganda javoblar «3 mln»,
/// «3000000», «kelishamiz» bo'lib ketadi va ularni guruhlab bo'lmaydi.
/// Oraliqlar eski botdan (@MBF_jbot) olingan.
/// </remarks>
public enum SalaryAnswer
{
    /// <summary>1–2 million so'm.</summary>
    From1To2Million = 1,

    /// <summary>2–3 million so'm.</summary>
    From2To3Million = 2,

    /// <summary>3–4 million so'm.</summary>
    From3To4Million = 3,

    /// <summary>5–7 million so'm.</summary>
    From5To7Million = 4,
}

/// <summary>Ish haqida qayerdan bilgan — 20-qadamning tugmalari.</summary>
/// <remarks>
/// Bu — reklama byudjeti haqidagi yagona o'lchov: qaysi kanal nomzod
/// olib kelayotganini boshqa hech qayerdan bilib bo'lmaydi.
/// </remarks>
public enum ReferralAnswer
{
    /// <summary>Tanishlar orqali.</summary>
    Acquaintance = 1,

    /// <summary>Instagram yoki Facebook.</summary>
    SocialNetwork = 2,

    /// <summary>OLX.uz.</summary>
    Olx = 3,

    /// <summary>hh.uz.</summary>
    HeadHunter = 4,

    /// <summary>Telegram.</summary>
    Telegram = 5,

    /// <summary>Boshqa manba.</summary>
    Other = 6,
}

/// <summary>
/// Anketa javoblarining umumiy qoidalari: chegaralar va toifalar.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bot serverning sanoqlarini IMPORT qilmaydi.</b> Bot boshqa
/// loyihalarga umuman havola qilmaydi (<c>Bonvi.Bot.csproj</c> dagi
/// qoida) — u domenni ham, bazani ham ko'rmaydi. Shu sababli sanoqlar
/// shu yerda qaytadan e'lon qilingan va ular server bilan faqat A'ZO
/// NOMI orqali bog'langan: so'rov tanasida qiymat satr bo'lib ketadi.
/// </para>
/// <para>
/// <b>Matn chegaralari serverdagi ustunlar bilan bir xil.</b> Server
/// uzun matnni o'zi qirqadi (<c>CandidateQuestionnaire.Cut</c>), lekin
/// bot ham qirqadi: aks holda nomzod yakuniy xulosada YUBORILMAYDIGAN
/// matnni ko'rardi. Mutaxassislik esa umuman anketada emas, nomzod
/// kartasida (<c>candidates.speciality</c>, 500 belgi) — u yerda
/// qirqish YO'Q va chegaradan oshgan qiymat bazada xatoga aylanardi.
/// </para>
/// </remarks>
public static class Answers
{
    /// <summary>Mutaxassislik — <c>candidates.speciality</c> ustuni.</summary>
    public const int MaxSpecialityLength = 500;

    /// <summary>Ish tajribasi.</summary>
    public const int MaxWorkExperienceLength = 2000;

    /// <summary>Boshqa tillar.</summary>
    public const int MaxOtherLanguagesLength = 500;

    /// <summary>Office dasturlari.</summary>
    public const int MaxOfficeSkillsLength = 1000;

    /// <summary>Oxirgi maosh.</summary>
    public const int MaxLastSalaryLength = 200;

    /// <summary>Qo'shimcha ma'lumot.</summary>
    public const int MaxExtraNoteLength = 2000;

    /// <summary>
    /// Haydovchilik guvohnomasi toifalari — 17-qadamning tugmalari.
    /// </summary>
    /// <remarks>
    /// TARJIMA QILINMAYDI: toifa belgisi hujjatda lotin harfi bilan
    /// yozilgan va uni kirillga o'girish («В») butunlay boshqa toifani
    /// bildirib qo'yardi.
    /// </remarks>
    public static readonly string[] DriverLicenseCategories = ["B", "BC", "BCE", "BCED"];

    /// <summary>
    /// Nomzod yozgan matnni tozalaydi va chegaraga sig'diradi.
    /// </summary>
    /// <remarks>
    /// Bo'sh matn <c>null</c> ga aylanadi: «bo'sh satr» va «javob
    /// berilmagan» serverda bir xil ma'noga ega bo'lishi kerak, aks
    /// holda HR kartada bo'sh qator ko'rardi.
    /// </remarks>
    public static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
