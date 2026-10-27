using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Recruiting;

/// <summary>Nomzodning ma'lumot darajasi.</summary>
/// <remarks>
/// To'plam YOPIQ va u botdagi tugmalardan keladi. Erkin matn bo'lganida
/// «Олий», «Oliy», «Высшее» uchta har xil qiymat bo'lib qolardi va HR
/// nomzodlarni ma'lumoti bo'yicha umuman guruhlay olmasdi.
/// </remarks>
public enum EducationLevel
{
    /// <summary>O'rta — maktab.</summary>
    SecondarySchool = 1,

    /// <summary>O'rta maxsus — kollej yoki litsey.</summary>
    VocationalSchool = 2,

    /// <summary>Oliy — bakalavriat tugallangan.</summary>
    HigherComplete = 3,

    /// <summary>Oliy — tugallanmagan.</summary>
    HigherIncomplete = 4,

    /// <summary>Oliy — magistratura.</summary>
    /// <remarks>
    /// Onlayn anketadan (HR-F02, 2026-10-24) keladi; bot uni hech qachon
    /// yubormaydi. OXIRIGA qo'shildi — bazadagi eski qiymatlar siljimaydi.
    /// </remarks>
    HigherMaster = 5,
}

/// <summary>Nomzodning oilaviy holati.</summary>
public enum MaritalStatus
{
    /// <summary>Oilali.</summary>
    Married = 1,

    /// <summary>Turmush qurmagan / bo'ydoq.</summary>
    /// <remarks>
    /// Nomi <c>Single</c> emas: u <c>System.Single</c> turi bilan
    /// to'qnashadi va analizator (CA1720) uni rad etadi.
    /// </remarks>
    Unmarried = 2,

    /// <summary>Ajrashgan.</summary>
    Divorced = 3,

    /// <summary>Beva.</summary>
    /// <remarks>Onlayn anketadan (HR-F02); bot yubormaydi. OXIRIGA qo'shildi.</remarks>
    Widowed = 4,
}

/// <summary>
/// Nomzod kutayotgan oylik maosh — ORALIQ, aniq son emas.
/// </summary>
/// <remarks>
/// <para>
/// Oraliq tanlangani ataylab (foydalanuvchi qarori, 2026-09-06): aniq son
/// so'ralganda javoblar «3 mln», «3000000», «kelishamiz» bo'lib ketadi va
/// ularni guruhlab bo'lmaydi. Oraliq esa bitta tugma — nomzod uchun ham
/// tez, HR uchun ham taqqoslanadigan.
/// </para>
/// <para>
/// ⚠️ Oraliqlar VAQT O'TISHI BILAN ESKIRADI. Ular eski botdan olingan
/// (@MBF_jbot) va bir necha yildan keyin ma'nosini yo'qotadi. O'shanda
/// yangi qiymat QO'SHILADI, eskisi o'chirilmaydi: bazadagi yozuvlar
/// o'sha paytdagi javobni bildiradi va uni qayta talqin qilish tarixni
/// buzardi.
/// </para>
/// </remarks>
public enum SalaryExpectation
{
    /// <summary>1–2 million so'm.</summary>
    From1To2Million = 1,

    /// <summary>2–3 million so'm.</summary>
    From2To3Million = 2,

    /// <summary>3–4 million so'm.</summary>
    From3To4Million = 3,

    /// <summary>5–7 million so'm.</summary>
    From5To7Million = 4,

    /// <summary>4–5 million so'm.</summary>
    /// <remarks>
    /// Eski oraliqlardagi «teshik» (3–4 dan keyin darhol 5–7). Onlayn
    /// anketa ANIQ summa so'raydi va uni oraliqqa o'giradi — 4,5 mln
    /// so'ragan odam hech qaysi eski oraliqqa to'g'ri tushmasdi. Izohdagi
    /// qoida bo'yicha yangi qiymat QO'SHILDI, eskilari o'zgarmadi.
    /// </remarks>
    From4To5Million = 5,

    /// <summary>7 million so'm va undan yuqori.</summary>
    /// <remarks>Onlayn anketadan; bot yubormaydi.</remarks>
    From7Million = 6,
}

/// <summary>
/// Nomzodning to'ldirgan anketasi — botdagi savollarga javoblar.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida jadval, <see cref="Candidate"/> ga ustun
/// qo'shilmadi.</b> <c>candidates</c> — tizimning eng ko'p o'qiladigan
/// jadvali: voronka ekrani, qidiruv, hisobotlar va bosh sahifadagi
/// hisoblagichlar hammasi undan o'qiydi. Unga o'n uchta bo'sh turadigan
/// ustun qo'shilsa, har bir <c>SELECT</c> shu ustunlarni ham
/// ko'tarardi — holbuki ular FAQAT bitta joyda, nomzod kartasi
/// ochilganda kerak.
/// </para>
/// <para>
/// <b>Nega bu <see cref="Candidate"/> ning BOLASI, alohida agregat
/// ildizi emas.</b> Anketa nomzoddan ajralgan holda ma'nosiz: u
/// nomzodning o'zi haqidagi ma'lumot va u bilan birga yaratiladi, birga
/// o'chadi. Alohida ildiz bo'lganida «anketasi bor, lekin nomzodi yo'q»
/// degan holat mumkin bo'lib qolardi.
/// </para>
/// <para>
/// <b>Anketa BO'LMASLIGI normal.</b> HR qo'lda kiritgan nomzodda ham,
/// bu funksiya paydo bo'lgunicha botdan kelgan eski yozuvlarda ham u
/// yo'q. Shu sababli hech qanday joyda «anketa bor» deb faraz
/// qilinmaydi.
/// </para>
/// <para>
/// <b>Nega tahrirlanmaydi.</b> Bu — nomzodning O'Z javoblari, HR ning
/// qaydi emas. HR ularni tuzatsa, «nomzod nima deb aytdi» degan fakt
/// yo'qolardi. HR ning fikri uchun <c>Candidate.Note</c> bor.
/// </para>
/// </remarks>
public sealed class CandidateQuestionnaire : Entity
{
    /// <summary>Ish tajribasi matnining eng katta uzunligi.</summary>
    public const int MaxWorkExperienceLength = 2000;

    /// <summary>«Boshqa tillar» matnining eng katta uzunligi.</summary>
    public const int MaxOtherLanguagesLength = 500;

    /// <summary>Office dasturlar matnining eng katta uzunligi.</summary>
    public const int MaxOfficeSkillsLength = 1000;

    /// <summary>Oxirgi maosh matnining eng katta uzunligi.</summary>
    public const int MaxLastSalaryLength = 200;

    /// <summary>Qo'shimcha ma'lumot matnining eng katta uzunligi.</summary>
    public const int MaxExtraNoteLength = 2000;

    /// <summary>Guvohnoma toifalari matnining eng katta uzunligi.</summary>
    /// <remarks>Botdan «BCED» eng uzuni; onlayn anketadan hamma toifa birga —
    /// «A, B, C, D, BC, CE, DE» (22 belgi, HR-F02 3.1). Zaxira kelajakdagi
    /// «BCE + traktor» kabi qiymatlar uchun.</remarks>
    public const int MaxDriverLicenseCategoriesLength = 32;

    private CandidateQuestionnaire(Guid id, Guid candidateId) : base(id) => CandidateId = candidateId;

    /// <summary>EF Core uchun.</summary>
    private CandidateQuestionnaire() { }

    /// <summary>Qaysi nomzodning anketasi.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Ma'lumot darajasi.</summary>
    public EducationLevel Education { get; private set; }

    /// <summary>Ish tajribasi — erkin matn: kompaniya, lavozim, davr.</summary>
    /// <remarks>
    /// Strukturaviy ro'yxat (kompaniya + lavozim + sana) ATAYLAB
    /// qilinmadi: u botda uchta qo'shimcha qadam degani va har bir ish
    /// joyi uchun takrorlanardi. Nomzodlarning aksariyati bitta qatorda
    /// yozadi, HR esa uni telefon suhbatida baribir aniqlaydi.
    /// </remarks>
    public string? WorkExperience { get; private set; }

    /// <summary>Oilaviy holati.</summary>
    public MaritalStatus MaritalStatus { get; private set; }

    /// <summary>
    /// Rus tilini bilish darajasi.
    /// </summary>
    /// <remarks>
    /// ⭐ Xodimning til darajasi bilan AYNAN bir xil enum
    /// (<see cref="LanguageLevel"/>) — bu ataylab. Nomzod ishga qabul
    /// qilinganda uning javobi xodim kartasidagi til ko'nikmasiga
    /// to'g'ridan-to'g'ri ko'chadi; alohida enum bo'lganida ko'chirishda
    /// moslashtirish jadvali kerak bo'lardi va u eskirardi.
    /// Botda uchta tugma bor, enumda esa beshta daraja: qolgan ikkitasini
    /// HR keyin qo'lda aniqlashtira oladi.
    /// </remarks>
    public LanguageLevel RussianLevel { get; private set; }

    /// <summary>Boshqa tillar — erkin matn.</summary>
    public string? OtherLanguages { get; private set; }

    /// <summary>Office dasturlarini bilish — erkin matn (foizlarda).</summary>
    public string? OfficeSkills { get; private set; }

    /// <summary>Oxirgi ish joyidagi oylik maosh — erkin matn.</summary>
    /// <remarks>
    /// Son emas, MATN: javoblar «3 mln», «kelishilgan holda», «ishlamaganman»
    /// bo'lib keladi. Songa majburlash nomzodni to'xtatib qo'yardi, noto'g'ri
    /// tushunilgan son esa bo'sh maydondan yomonroq.
    /// </remarks>
    public string? LastSalary { get; private set; }

    /// <summary>Nomzodning o'zi haqida qo'shimcha aytgani.</summary>
    public string? ExtraNote { get; private set; }

    /// <summary>Haydovchilik guvohnomasi bormi.</summary>
    public bool HasDriverLicense { get; private set; }

    /// <summary>Guvohnoma toifasi — guvohnoma bo'lmasa <c>null</c>.</summary>
    public string? DriverLicenseCategories { get; private set; }

    /// <summary>Kutilayotgan maosh oralig'i.</summary>
    public SalaryExpectation ExpectedSalary { get; private set; }

    /*
      «Ish haqida qayerdan bilgan» ENDI BU YERDA EMAS — nomzodning o'zida
      (`Candidate.ReferralChannelId`, SPEC-qayerdan-bilgan §4): u HAR BIR
      nomzodga kerak, anketa esa faqat bot va onlayn anketada bor.
      Migratsiya `QayerdanBilgan` eski javoblarni o'sha yerga ko'chirgan.
    */

    /// <summary>
    /// Ommaviy oferta bilan rozilik VAQTI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bayroq (<c>bool</c>) emas, VAQT — bu huquqiy fakt: «qachon rozilik
    /// berildi» degan savolga javob kerak bo'lishi mumkin. Bayroq bilan
    /// javob «bir marta, qachondir» bo'lardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Nega <c>null</c> BO'LISHI SHART.</b> Ommaviy oferta havolasi
    /// sozlanmagan bo'lsa (<c>bot.offerUrl</c> bo'sh), bot 21-qadamni
    /// UMUMAN chizmaydi — nomzod hech qanday oferta ko'rmaydi va hech
    /// nimaga rozilik bermaydi. Ustun majburiy bo'lganida bu holatni
    /// ifodalab bo'lmasdi va bot arizani YUBORISH lahzasini «rozilik
    /// vaqti» deb yozardi: bazada ko'rinishi tekshirilgan rozilikdan
    /// farq qilmaydigan, aslida esa hech qachon berilmagan huquqiy fakt.
    /// Endi <c>null</c> «rozilik SO'RALMAGAN», qiymat esa «aynan shu
    /// lahzada berilgan» degani.
    /// </para>
    /// </remarks>
    public DateTimeOffset? OfferAcceptedAt { get; private set; }

    /// <summary>
    /// Anketani yaratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Barcha tekshiruv shu yerda: anketa faqat shu metod orqali paydo
    /// bo'ladi va keyin o'zgarmaydi (sinf izohidagi sabab).
    /// </para>
    /// <para>
    /// <b>Rozilik vaqti TEKSHIRILMAYDI</b> va uning sukut qiymati ham
    /// YO'Q: qiymat <c>null</c> bo'lishi mumkin bo'lgani bilan
    /// (<see cref="OfferAcceptedAt"/> izohi) uni chaqiruv joyida
    /// unutib qoldirib bo'lmasligi kerak. Sukut qiymat qo'yilsa,
    /// keyingi chaqiruvchi haqiqiy rozilikni jimgina tushirib
    /// qoldirardi — ya'ni aynan shu maydonda tuzatilgan xato boshqa
    /// tomondan qaytardi.
    /// </para>
    /// </remarks>
    public static Result<CandidateQuestionnaire> Create(
        Guid candidateId,
        EducationLevel education,
        MaritalStatus maritalStatus,
        LanguageLevel russianLevel,
        SalaryExpectation expectedSalary,
        DateTimeOffset? offerAcceptedAt,
        string? workExperience = null,
        string? otherLanguages = null,
        string? officeSkills = null,
        string? lastSalary = null,
        string? extraNote = null,
        bool hasDriverLicense = false,
        string? driverLicenseCategories = null)
    {
        if (candidateId == Guid.Empty)
        {
            return Error.Validation("anketa.nomzod_yoq", "Nomzod ko'rsatilishi shart.");
        }

        if (!Enum.IsDefined(education))
        {
            return Error.Validation("anketa.malumot_notogri", "Ma'lumot darajasi noto'g'ri.");
        }

        if (!Enum.IsDefined(maritalStatus))
        {
            return Error.Validation("anketa.oila_notogri", "Oilaviy holat noto'g'ri.");
        }

        if (!Enum.IsDefined(russianLevel))
        {
            return Error.Validation("anketa.til_notogri", "Til darajasi noto'g'ri.");
        }

        if (!Enum.IsDefined(expectedSalary))
        {
            return Error.Validation("anketa.maosh_notogri", "Kutilayotgan maosh oralig'i noto'g'ri.");
        }

        /*
          Guvohnoma YO'Q bo'lsa toifa ham bo'lmaydi — qiymat jimgina
          tashlanadi, xato qaytarilmaydi. Sabab: bu holat mijozning
          xatosi emas, botning qadam tartibidan kelib chiqadi (nomzod
          «Ha» deb toifani tanlab, keyin «Orqaga» bosib «Yo'q» ga
          o'tishi mumkin). Xato qaytarilsa, tugallangan anketa oxirgi
          qadamda yo'qolardi.
        */
        var categories = hasDriverLicense ? Trim(driverLicenseCategories) : null;

        return new CandidateQuestionnaire(NewId(), candidateId)
        {
            Education = education,
            MaritalStatus = maritalStatus,
            RussianLevel = russianLevel,
            ExpectedSalary = expectedSalary,
            OfferAcceptedAt = offerAcceptedAt,
            WorkExperience = Cut(workExperience, MaxWorkExperienceLength),
            OtherLanguages = Cut(otherLanguages, MaxOtherLanguagesLength),
            OfficeSkills = Cut(officeSkills, MaxOfficeSkillsLength),
            LastSalary = Cut(lastSalary, MaxLastSalaryLength),
            ExtraNote = Cut(extraNote, MaxExtraNoteLength),
            HasDriverLicense = hasDriverLicense,
            DriverLicenseCategories = Cut(categories, MaxDriverLicenseCategoriesLength),
        };
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Matnni tozalaydi va chegaraga sig'diradi.
    /// </summary>
    /// <remarks>
    /// ⭐ QIRQADI, xato qaytarmaydi. Bu qoida ataylab va u faqat SHU
    /// agregatda amal qiladi: matnlar botdan keladi, ya'ni ularni yozgan
    /// odam ekran oldida turibdi va uzunlik xatosi butun anketani oxirgi
    /// qadamda yo'qotardi. Nomzod uchun «matnim biroz qisqartirildi»
    /// «anketangiz yuborilmadi» dan behad yaxshiroq.
    /// </remarks>
    private static string? Cut(string? value, int limit)
    {
        var text = Trim(value);

        return text is null || text.Length <= limit
            ? text
            : text[..limit];
    }
}
