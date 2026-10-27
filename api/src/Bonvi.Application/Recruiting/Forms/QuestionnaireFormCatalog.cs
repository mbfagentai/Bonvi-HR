namespace Bonvi.Application.Recruiting.Forms;

/// <summary>Kompyuter jadvalining bitta dasturi: kalit va qog'ozdagi nomi.</summary>
public sealed record ComputerSkillItem(string Key, string Label);

/// <summary>Kompyuter jadvalining ustuni (guruh) — qog'ozdagi tartibda.</summary>
public sealed record ComputerSkillGroup(string Title, IReadOnlyList<ComputerSkillItem> Items);

/// <summary>
/// HR-F02 blankasining doimiy ro'yxatlari — qog'ozdagi tartib va matn bilan.
/// </summary>
/// <remarks>
/// <para>
/// BITTA joyda: tekshiruvchi kalitlarni shu yerdan oladi (noma'lum kalit —
/// 400), PDF esa yorliqlarni. Ikki nusxa bo'lganida forma yuborgan kalit
/// PDF'da «ko'rinmay» qolishi mumkin edi.
/// </para>
/// <para>
/// Yorliqlar qog'ozdagidek O'ZBEK LOTIN, qog'ozdagi tutuq belgilari bilan
/// (<c>‘</c> U+2018, <c>’</c> U+2019) — PDF blanka bilan harfma-harf bir xil
/// bo'lsin.
/// </para>
/// </remarks>
public static class QuestionnaireFormCatalog
{
    /// <summary>Kompyuter savodxonligi — 3 guruh, PDF jadvalining ustunlari tartibida.</summary>
    /// <remarks>
    /// Qisqartirilgan ro'yxat (egasi, 2026-10-26: korxona asosan operator, payvandchi va ofis
    /// xodimi oladi). Blankadagi 39 banddan 11 tasi qoldi; «Dasturlash» ustuni va kam
    /// uchraydigan dasturlar ATAYLAB yo'q — nomzod ularni «Boshqa dasturlar va dasturlash
    /// tillari» matniga yozadi. Olib tashlangan kalit endi noma'lum kalit kabi rad etiladi (400):
    /// eski versiya hech qachon chiqmagan, eski mijoz yo'q.
    /// </remarks>
    public static IReadOnlyList<ComputerSkillGroup> ComputerGroups { get; } =
    [
        new("Operatsion tizim",
        [
            new("windows", "Windows"),
            new("mobileOs", "Smartfon (Android / iOS)"),
        ]),
        new("Ofis dasturlari",
        [
            new("msWord", "MS Word"),
            new("msExcel", "MS Excel"),
            new("msPowerPoint", "MS PowerPoint"),
            new("googleWorkspace", "Google Docs / Sheets"),
        ]),
        new("Maxsus dasturlar",
        [
            new("oneC", "1C"),
            new("didox", "Didox, soliq.uz portallari"),
            new("crm", "CRM (AmoCRM, Bitrix24)"),
            new("autocad", "AutoCAD"),
            new("photoshop", "Photoshop / Canva / CorelDraw"),
        ]),
    ];

    /// <summary>Barcha kompyuter kalitlari — tekshiruv uchun.</summary>
    public static IReadOnlySet<string> ComputerKeys { get; } =
        ComputerGroups.SelectMany(group => group.Items).Select(item => item.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>Ofis guruhi — <c>CandidateQuestionnaire.OfficeSkills</c> matni shundan yig'iladi.</summary>
    public static ComputerSkillGroup OfficeGroup => ComputerGroups[1];

    /// <summary>Haydovchilik guvohnomasi toifalari — qog'ozdagi tartibda.</summary>
    public static IReadOnlyList<string> DriverLicenseCategories { get; } = ["A", "B", "C", "D", "BE", "CE", "DE"];

    /// <summary>Jadvalda doim turadigan to'rt til — qog'ozdagi tartibda.</summary>
    public static IReadOnlyList<FormLanguage> FixedLanguages { get; } =
        [FormLanguage.Uzbek, FormLanguage.Russian, FormLanguage.English, FormLanguage.Chinese];

    /// <summary>Til nomi — qog'ozdagi «Til» ustuni.</summary>
    public static string LanguageLabel(FormLanguage language) => language switch
    {
        FormLanguage.Uzbek => "O‘zbek tili",
        FormLanguage.Russian => "Rus tili",
        FormLanguage.English => "Ingliz tili",
        FormLanguage.Chinese => "Xitoy tili",
        _ => "Boshqa",
    };

    /// <summary>Qarindoshlik — egasining ro'yxatidagi matn.</summary>
    public static string RelationLabel(FormFamilyRelation relation) => relation switch
    {
        FormFamilyRelation.Father => "Otasi",
        FormFamilyRelation.Mother => "Onasi",
        FormFamilyRelation.Spouse => "Turmush o‘rtog‘i",
        FormFamilyRelation.Brother => "Akasi / ukasi",
        FormFamilyRelation.Sister => "Opasi / singlisi",
        FormFamilyRelation.Child => "Farzandi",
        _ => "Boshqa",
    };
}
