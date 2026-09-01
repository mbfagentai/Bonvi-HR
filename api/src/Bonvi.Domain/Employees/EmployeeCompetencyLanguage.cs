using Bonvi.Domain.Common;

namespace Bonvi.Domain.Employees;

/// <summary>
/// Til kodi — QAT'IY ro'yxat, ma'lumotnoma EMAS.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega ma'lumotnoma emas.</b> Yuridik shaxs yoki lavozimdan farqli,
/// bu HR o'zi to'ldiradigan ro'yxat emas — dunyodagi tillar ro'yxati.
/// U kompaniyaga qarab o'zgarmaydi, shu sababli bazada jadval ochib,
/// urug'lantirib, alifbo kolatsiyasini sozlab yurish ortiqcha yuk
/// bo'lardi. Sanoq esa migratsiyasiz, kod bilan birga yashaydi.
/// </para>
/// <para>
/// Ro'yxat O'zbekiston HR kontekstiga moslab tuzilgan: avval mahalliy va
/// qo'shni tillar, keyin mintaqada ish beruvchilar tez-tez so'raydigan
/// chet tillari. Yangi til OXIRIGA qo'shiladi — raqamlar qadalgan,
/// o'rtaga qo'shish eski yozuvlarning ma'nosini jimgina o'zgartirardi.
/// </para>
/// </remarks>
public enum LanguageCode
{
    Uzbek = 1,
    Russian = 2,
    English = 3,
    Karakalpak = 4,
    Kazakh = 5,
    Kyrgyz = 6,
    Tajik = 7,
    Turkmen = 8,
    Azerbaijani = 9,
    Turkish = 10,
    Arabic = 11,
    Persian = 12,
    Chinese = 13,
    Korean = 14,
    Japanese = 15,
    German = 16,
    French = 17,
    Spanish = 18,
    Italian = 19,
    Ukrainian = 20,
    Hindi = 21,
}

/// <summary>
/// Til bilish darajasi.
/// </summary>
/// <remarks>
/// Atigi 5 pog'ona va tushunarli so'zlar bilan — foydalanuvchi aynan
/// shunday so'radi («elementary, advanced kabi»). CEFR (A1–C2) ATAYLAB
/// olinmadi: HR sertifikatga qaramay, suhbatdan taxminiy baho qo'yadi,
/// olti pog'onali rasmiy shkala esa yolg'on aniqlik taassurotini berardi.
/// Raqamlar o'sish tartibida qadalgan — «darajasi kamida X» kabi
/// solishtirish kerak bo'lsa, raqamning o'zi yetadi.
/// </remarks>
public enum LanguageLevel
{
    Elementary = 1,
    Intermediate = 2,
    UpperIntermediate = 3,
    Advanced = 4,
    Native = 5,
}

/// <summary>
/// «Til + daraja» juftligi — <see cref="EmployeeCompetency.SetLanguageSkills"/>
/// ga beriladigan kirish qiymati.
/// </summary>
/// <remarks>
/// Alohida struct, tuple emas: chaqiruvchi qatlamlar (Application, API)
/// maydon nomlari bilan ishlaydi va JSON'da ham aynan shu ikki nom
/// (<c>language</c>, <c>level</c>) yuradi.
/// </remarks>
public readonly record struct LanguageSkill(LanguageCode Language, LanguageLevel Level);

/// <summary>
/// Xodim biladigan bitta til va uning darajasi — qobiliyatlar profilining
/// bolasi (jadval: <c>employee_competency_languages</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida jadval, matn ustuni emas.</b> Eski <c>Languages</c>
/// erkin matn edi («Рус тили , Инглиз тили») — uni filtrlash ham,
/// sanash ham iloji yo'q edi. Endi har bir til alohida qator: «ingliz
/// tilini Advanced biladiganlar» oddiy SQL so'rovi bo'ladi.
/// </para>
/// <para>
/// <b>Hayoti profilga bog'liq</b> — <see cref="Organization.PositionDuty"/>
/// lavozim bilan qanday yashasa, bu ham profil bilan shunday: alohida
/// repozitoriysi yo'q, o'zgartirish faqat
/// <see cref="EmployeeCompetency.SetLanguageSkills"/> orqali, profil
/// o'chsa kaskad bilan o'chadi.
/// </para>
/// </remarks>
public sealed class EmployeeCompetencyLanguage : Entity
{
    private EmployeeCompetencyLanguage(
        Guid id,
        Guid employeeCompetencyId,
        LanguageCode language,
        LanguageLevel level)
        : base(id)
    {
        EmployeeCompetencyId = employeeCompetencyId;
        Language = language;
        Level = level;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeCompetencyLanguage() { }

    public Guid EmployeeCompetencyId { get; private set; }

    public LanguageCode Language { get; private set; }

    public LanguageLevel Level { get; private set; }

    internal static EmployeeCompetencyLanguage Create(
        Guid employeeCompetencyId,
        LanguageCode language,
        LanguageLevel level)
        => new(NewId(), employeeCompetencyId, language, level);

    /// <summary>
    /// Darajani yangilaydi. <c>internal</c> — til ro'yxati faqat agregat
    /// ildizi orqali o'zgaradi, aks holda takrorlanish qoidasi chetlab
    /// o'tilishi mumkin edi.
    /// </summary>
    internal void SetLevel(LanguageLevel level) => Level = level;
}
