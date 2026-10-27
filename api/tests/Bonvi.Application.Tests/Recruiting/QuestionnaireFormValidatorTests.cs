using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Planshet anketasining server tekshiruvi va mavjud maydonlarga ko'chirish
/// (SPEC-planshet-anketa §3, §5).
/// </summary>
public sealed class QuestionnaireFormValidatorTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 24);

    /// <summary>«Qayerdan bilgan» — aniqlik so'raydigan kanal (seed'dagi «Tanishlar orqali»).</summary>
    private static readonly ReferralChannelOptionDto Tanishlar =
        new(Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000001"), "Tanishlar orqali", true, true);

    /// <summary>Aniqlik so'ramaydigan kanal.</summary>
    private static readonly ReferralChannelOptionDto Telegram =
        new(Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000005"), "Telegram", true, false);

    /// <summary>Seansdagi FAOL kanallar — tekshiruvchi yangi tanlovni faqat shulardan qabul qiladi.</summary>
    private static readonly ReferralChannelOptionDto[] Kanallar = [Tanishlar, Telegram];

    // ────────────────────────────────────────────────────────────
    //  To'g'ri anketa va tozalash
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Toliq_anketa_otadi_va_tozalanadi()
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with
        {
            LastName = "  Gʻulomov ",
            Telegram = "@sardor_g",
            Phone = "90 555 11 22",
            Email = " ",
        }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        var a = natija.Value;
        a.LastName.ShouldBe("Gʻulomov");
        a.Telegram.ShouldBe("sardor_g");
        a.Phone.ShouldBe("+998905551122");
        a.Email.ShouldBeNull("bo'sh matn — javob yo'q");
    }

    [Fact]
    public void Shartli_maydonlar_sharti_bolmasa_tashlanadi()
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with
        {
            MilitaryService = FormMilitaryService.Deferred,
            MilitaryServiceMonths = 12,
            HasDriverLicense = false,
            DriverLicenseCategories = ["B"],
            HasCar = false,
            CarModel = "Cobalt",
            HasAcquaintances = false,
            AcquaintancesDetail = "Kimdir",
            HasLoan = false,
            LoanMonthlyPayment = 100,
        }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        var a = natija.Value;
        a.MilitaryService.ShouldBe(FormMilitaryService.Deferred);
        a.MilitaryServiceMonths.ShouldBeNull("muddat faqat «O'tagan» da");
        a.DriverLicenseCategories.ShouldBeEmpty();
        a.CarModel.ShouldBeNull();
        a.AcquaintancesDetail.ShouldBeNull();
        a.LoanMonthlyPayment.ShouldBeNull();
    }

    /// <summary>
    /// Jinsi, pasport va JSHSHIR so'ralmaydi, harbiy xizmat hammaga ixtiyoriy (egasi, 2026-10-26) —
    /// ularsiz anketa o'tadi.
    /// </summary>
    [Fact]
    public void Jinsi_pasporti_va_harbiy_xizmati_yoq_anketa_otadi()
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with { MilitaryService = null, MilitaryServiceMonths = 12 }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.MilitaryService.ShouldBeNull();
        natija.Value.MilitaryServiceMonths.ShouldBeNull("javob yo'q — muddat ham yo'q");
    }

    [Fact]
    public void Harbiy_xizmat_otagan_muddati_bilan_saqlanadi()
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with { MilitaryService = FormMilitaryService.Served, MilitaryServiceMonths = 12 }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.MilitaryService.ShouldBe(FormMilitaryService.Served);
        natija.Value.MilitaryServiceMonths.ShouldBe(12);
    }

    /// <summary>
    /// Olib tashlangan kalitlarni (eski forma yoki qo'lda yasalgan so'rov) yuborish xato emas —
    /// ular boshqa noma'lum kalitlar kabi e'tiborsiz qoladi va saqlanadigan JSON'ga TUSHMAYDI.
    /// </summary>
    [Fact]
    public void Olib_tashlangan_kalitlar_etiborsiz_qoladi_va_saqlanmaydi()
    {
        var eski = JsonNode.Parse(QuestionnaireFormJson.Write(Toliq()))!.AsObject();
        eski["gender"] = "Male";
        eski["passportNumber"] = "AD1234567";
        eski["passportIssuedBy"] = "Yunusobod tumani IIB";
        eski["passportIssuedAt"] = "2020-01-15";
        eski["pinfl"] = "31203951234567";

        var oqilgan = QuestionnaireFormJson.Read(eski.ToJsonString());
        oqilgan.ShouldNotBeNull("noma'lum kalit JSON'ni buzuq qilmaydi");

        var natija = QuestionnaireFormValidator.Validate(oqilgan, Bugun, Kanallar);
        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        var saqlanadi = QuestionnaireFormJson.Write(natija.Value);
        foreach (var kalit in new[] { "gender", "passportNumber", "passportIssuedBy", "passportIssuedAt", "pinfl" })
        {
            saqlanadi.ShouldNotContain($"\"{kalit}\"", Case.Sensitive, kalit);
        }

        saqlanadi.ShouldNotContain("AD1234567");
        saqlanadi.ShouldNotContain("31203951234567");
    }

    /// <summary>
    /// «Qayerdan bilgan» — ma'lumotnomadagi kanal (SPEC-qayerdan-bilgan §5): aniqlik faqat kanal
    /// so'rasa majburiy va saqlanadi; so'ramaydigan kanalda kelgan matn tashlanadi.
    /// </summary>
    [Fact]
    public void Qayerdan_bilgan_aniqligi_faqat_soraydigan_kanalda_saqlanadi()
    {
        var tanish = QuestionnaireFormValidator.Validate(Toliq() with { ReferralDetail = "  Karimov Anvar " }, Bugun, Kanallar);

        tanish.IsSuccess.ShouldBeTrue(tanish.IsFailure ? tanish.Error.ToString() : null);
        tanish.Value.ReferralChannelId.ShouldBe(Tanishlar.Id);
        tanish.Value.ReferralDetail.ShouldBe("Karimov Anvar");

        var telegram = QuestionnaireFormValidator.Validate(
            Toliq() with { ReferralChannelId = Telegram.Id, ReferralDetail = "eski matn" },
            Bugun,
            Kanallar);

        telegram.IsSuccess.ShouldBeTrue(telegram.IsFailure ? telegram.Error.ToString() : null);
        telegram.Value.ReferralChannelId.ShouldBe(Telegram.Id);
        telegram.Value.ReferralDetail.ShouldBeNull("kanal aniqlik so'ramaydi — matn saqlanmaydi");

        var aniqliksiz = QuestionnaireFormValidator.Validate(
            Toliq() with { ReferralChannelId = Telegram.Id, ReferralDetail = null },
            Bugun,
            Kanallar);

        aniqliksiz.IsSuccess.ShouldBeTrue("so'ramaydigan kanalda aniqlik majburiy emas");
    }

    /// <summary>Eski sanoq kaliti (<c>referralSource</c>) endi tanilmaydi — kanal yo'q deb hisoblanadi.</summary>
    [Fact]
    public void Eski_referralSource_kaliti_kanal_orniga_otmaydi()
    {
        var eski = JsonNode.Parse(QuestionnaireFormJson.Write(Toliq() with { ReferralChannelId = null }))!.AsObject();
        eski["referralSource"] = "Telegram";

        var natija = QuestionnaireFormValidator.Validate(QuestionnaireFormJson.Read(eski.ToJsonString())!, Bugun, Kanallar);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("anketa.majburiy");
        natija.Error.Details["field"].ShouldBe("referralChannelId");
    }

    [Fact]
    public void Toifalar_va_tillar_qogozdagi_tartibda()
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with
        {
            DriverLicenseCategories = ["ce", "B", "A"],
            Languages =
            [
                new FormLanguageSkill(FormLanguage.Other, "Turk tili", 1, 1, 1),
                new FormLanguageSkill(FormLanguage.Russian, null, 3, 3, 3),
                new FormLanguageSkill(FormLanguage.Uzbek, null, 5, 5, 5),
            ],
        }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.DriverLicenseCategories.ShouldBe(["A", "B", "CE"]);
        natija.Value.Languages!.Select(row => row.Language).ShouldBe([FormLanguage.Uzbek, FormLanguage.Russian, FormLanguage.Other]);
    }

    /// <summary>
    /// Kompyuter jadvali — qisqartirilgan 11 dastur, 3 guruh (egasi, 2026-10-26). Forma xuddi
    /// shu kalitlarni yuboradi: ro'yxat o'zgarsa, forma ham o'zgarishi kerak.
    /// </summary>
    [Fact]
    public void Kompyuter_jadvali_11_dastur_3_guruh_qogozdagi_tartibda()
    {
        var guruhlar = QuestionnaireFormCatalog.ComputerGroups;

        guruhlar.Select(group => group.Title).ShouldBe(["Operatsion tizim", "Ofis dasturlari", "Maxsus dasturlar"]);
        guruhlar[0].Items.Select(item => item.Key).ShouldBe(["windows", "mobileOs"]);
        guruhlar[1].Items.Select(item => item.Key).ShouldBe(["msWord", "msExcel", "msPowerPoint", "googleWorkspace"]);
        guruhlar[2].Items.Select(item => item.Key).ShouldBe(["oneC", "didox", "crm", "autocad", "photoshop"]);

        guruhlar.SelectMany(group => group.Items).Select(item => item.Label).ShouldBe(
        [
            "Windows", "Smartfon (Android / iOS)",
            "MS Word", "MS Excel", "MS PowerPoint", "Google Docs / Sheets",
            "1C", "Didox, soliq.uz portallari", "CRM (AmoCRM, Bitrix24)", "AutoCAD", "Photoshop / Canva / CorelDraw",
        ]);

        QuestionnaireFormCatalog.ComputerKeys.Count.ShouldBe(11);
        QuestionnaireFormCatalog.OfficeGroup.Title.ShouldBe("Ofis dasturlari");
    }

    [Fact]
    public void Kompyuter_jadvalining_hamma_11_dasturi_qabul_qilinadi()
    {
        var hammasi = new Dictionary<string, int>
        {
            ["windows"] = 4,
            ["mobileOs"] = 3,
            ["msWord"] = 2,
            ["msExcel"] = 1,
            ["msPowerPoint"] = 2,
            ["googleWorkspace"] = 3,
            ["oneC"] = 4,
            ["didox"] = 1,
            ["crm"] = 2,
            ["autocad"] = 3,
            ["photoshop"] = 4,
        };

        var natija = QuestionnaireFormValidator.Validate(Toliq() with
        {
            ComputerSkills = hammasi,
            ComputerSkillsOther = "  Python, KOMPAS-3D ",
        }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.ComputerSkills.ShouldBe(hammasi, ignoreOrder: true);
        natija.Value.ComputerSkillsOther.ShouldBe("Python, KOMPAS-3D", "ro'yxatda yo'q dastur va dasturlash tili — erkin matnda");
    }

    /// <summary>
    /// Qisqartirishda olib tashlangan 20 kalit (butun «Dasturlash» ustuni bilan) — boshqa noma'lum
    /// kalit kabi 400. Eski versiya chiqmagan, ularni yuboradigan mijoz yo'q.
    /// </summary>
    [Theory]
    [InlineData("linux")]
    [InlineData("macos")]
    [InlineData("msAccess")]
    [InlineData("msOutlook")]
    [InlineData("notion")]
    [InlineData("acrobat")]
    [InlineData("sap")]
    [InlineData("powerBi")]
    [InlineData("solidworks")]
    [InlineData("corelCanvaFigma")]
    [InlineData("python")]
    [InlineData("javascript")]
    [InlineData("csharp")]
    [InlineData("java")]
    [InlineData("cpp")]
    [InlineData("php")]
    [InlineData("sql")]
    [InlineData("htmlCss")]
    [InlineData("kotlinSwift")]
    [InlineData("oneCLanguage")]
    public void Olib_tashlangan_dastur_kaliti_rad_etiladi(string kalit)
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with
        {
            ComputerSkills = new Dictionary<string, int> { ["msWord"] = 3, [kalit] = 2 },
        }, Bugun, Kanallar);

        natija.IsFailure.ShouldBeTrue(kalit);
        natija.Error.Code.ShouldBe("anketa.notogri", kalit);
        natija.Error.Details["field"].ShouldBe($"computerSkills.{kalit}");
    }

    // ────────────────────────────────────────────────────────────
    //  Xatolar — kod va maydon
    // ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Xatolar))]
    public void Xato_birinchi_maydon_bilan_qaytadi(string nom, QuestionnaireFormAnswers javoblar, string kod, string maydon)
    {
        var natija = QuestionnaireFormValidator.Validate(javoblar, Bugun, Kanallar);

        natija.IsFailure.ShouldBeTrue(nom);
        natija.Error.Code.ShouldBe(kod, nom);
        natija.Error.Details["field"].ShouldBe(maydon, nom);
        natija.Error.Message.ShouldBe(maydon, "javobning «detail» i — maydon yo'li");
    }

    public static TheoryData<string, QuestionnaireFormAnswers, string, string> Xatolar() => new()
    {
        { "familiya yo'q", Toliq() with { LastName = " " }, "anketa.majburiy", "lastName" },
        { "ism uzun", Toliq() with { FirstName = new string('a', 61) }, "anketa.uzun", "firstName" },
        { "15 yosh", Toliq() with { BirthDate = Bugun.AddYears(-15) }, "anketa.notogri", "birthDate" },
        { "71 yosh", Toliq() with { BirthDate = Bugun.AddYears(-71) }, "anketa.notogri", "birthDate" },
        { "telefon yo'q", Toliq() with { Phone = null }, "anketa.majburiy", "phone" },
        { "telefon chet el", Toliq() with { Phone = "+7 916 123 45 67" }, "telefon.mamlakat_kodi", "phone" },
        { "telegram qisqa", Toliq() with { Telegram = "@abc" }, "anketa.notogri", "telegram" },
        { "e-mail", Toliq() with { Email = "kimdir.uz" }, "anketa.notogri", "email" },
        { "harbiy xizmat noma'lum qiymat", Toliq() with { MilitaryService = (FormMilitaryService)9 }, "anketa.notogri", "militaryService" },
        { "harbiy xizmat 61 oy", Toliq() with { MilitaryService = FormMilitaryService.Served, MilitaryServiceMonths = 61 }, "anketa.notogri", "militaryServiceMonths" },
        { "harbiy xizmat 0 oy", Toliq() with { MilitaryService = FormMilitaryService.Served, MilitaryServiceMonths = 0 }, "anketa.notogri", "militaryServiceMonths" },
        { "guvohnoma bor, toifa yo'q", Toliq() with { DriverLicenseCategories = [] }, "anketa.majburiy", "driverLicenseCategories" },
        { "noma'lum toifa", Toliq() with { DriverLicenseCategories = ["Z"] }, "anketa.notogri", "driverLicenseCategories" },
        { "o'qish joyi nomsiz", Toliq() with { Educations = [new FormEducation(" ")] }, "anketa.majburiy", "educations[0].institution" },
        { "o'qish yillari teskari", Toliq() with { Educations = [new FormEducation("TATU", null, 2020, 2015)] }, "anketa.notogri", "educations[0].endYear" },
        { "olti o'qish joyi", Toliq() with { Educations = [.. Enumerable.Repeat(new FormEducation("TATU"), 6)] }, "anketa.notogri", "educations" },
        { "rus tili yo'q", Toliq() with { Languages = [new FormLanguageSkill(FormLanguage.Uzbek, null, 5, 5, 5)] }, "anketa.majburiy", "languages" },
        { "rus tili darajasiz", Toliq() with { Languages = [new FormLanguageSkill(FormLanguage.Russian, null, 3, null, 3)] }, "anketa.majburiy", "languages[0].reading" },
        { "daraja 6", Toliq() with { Languages = [new FormLanguageSkill(FormLanguage.Russian, null, 6, 3, 3)] }, "anketa.notogri", "languages[0].speaking" },
        { "boshqa til nomsiz", Toliq() with { Languages = [new FormLanguageSkill(FormLanguage.Russian, null, 3, 3, 3), new FormLanguageSkill(FormLanguage.Other, null, 1, 1, 1)] }, "anketa.majburiy", "languages[1].otherName" },
        { "noma'lum dastur", Toliq() with { ComputerSkills = new Dictionary<string, int> { ["tiktok"] = 3 } }, "anketa.notogri", "computerSkills.tiktok" },
        { "dastur darajasi 5", Toliq() with { ComputerSkills = new Dictionary<string, int> { ["msExcel"] = 5 } }, "anketa.notogri", "computerSkills.msExcel" },
        { "ish oyi formati", Toliq() with { Jobs = [new FormJob("MBF", null, "03.2021")] }, "anketa.notogri", "jobs[0].from" },
        { "ish kelajakda", Toliq() with { Jobs = [new FormJob("MBF", null, "2027-01")] }, "anketa.notogri", "jobs[0].from" },
        { "oila a'zosi yo'q", Toliq() with { FamilyMembers = [] }, "anketa.majburiy", "familyMembers" },
        { "qarindoshlik yo'q", Toliq() with { FamilyMembers = [new FormFamilyMember(null, "Ona")] }, "anketa.majburiy", "familyMembers[0].relation" },
        { "tavsiya telefoni", Toliq() with { References = [new FormReference("Anvar", null, "abc")] }, "anketa.notogri", "references[0].phone" },
        { "tanish kim — yo'q", Toliq() with { ReferralDetail = null }, "anketa.majburiy", "referralDetail" },
        { "tanish kim — uzun", Toliq() with { ReferralDetail = new string('a', 201) }, "anketa.uzun", "referralDetail" },
        { "qayerdan bilgan — yo'q", Toliq() with { ReferralChannelId = null }, "anketa.majburiy", "referralChannelId" },
        { "qayerdan bilgan — faol emas yoki noma'lum", Toliq() with { ReferralChannelId = Guid.CreateVersion7() }, "qayerdan.topilmadi", "referralChannelId" },
        { "maosh yo'q", Toliq() with { ExpectedSalary = null }, "anketa.majburiy", "expectedSalary" },
        { "maosh manfiy", Toliq() with { ExpectedSalary = -1 }, "anketa.notogri", "expectedSalary" },
        { "ishga kecha", Toliq() with { AvailableFrom = Bugun.AddDays(-1) }, "anketa.notogri", "availableFrom" },
        { "boshqa ish tafsilotsiz", Toliq() with { HasOtherJob = true, OtherJobDetail = null }, "anketa.majburiy", "otherJobDetail" },
        { "sudlangan tafsilotsiz", Toliq() with { HasConviction = true }, "anketa.majburiy", "convictionDetail" },
        { "motivatsiya yo'q", Toliq() with { Motivation = null }, "anketa.majburiy", "motivation" },
        { "uzun matn", Toliq() with { Weaknesses = new string('x', 1001) }, "anketa.uzun", "weaknesses" },
        { "rozilik yo'q", Toliq() with { ConsentConfidentiality = false }, "anketa.rozilik", "consentConfidentiality" },
        { "imzo ismi yo'q", Toliq() with { SignatureName = null }, "anketa.majburiy", "signatureName" },
        { "imzo boshqa odam", Toliq() with { SignatureName = "Karimov Aziz" }, "anketa.imzo_ism", "signatureName" },
        { "familiyada NUL", Toliq() with { LastName = "G\u0000ulomov" }, "anketa.belgi", "lastName" },
        { "familiyada yo'nalish almashtirgich", Toliq() with { LastName = "\u202EvomoluG" }, "anketa.belgi", "lastName" },
        { "manzilda yangi qator", Toliq() with { RegisteredAddress = "Toshkent\nChilonzor" }, "anketa.belgi", "registeredAddress" },
        { "ish joyida ko'rinmas belgi", Toliq() with { Jobs = [new FormJob("MBF\u200B", null, "2021-03", null, true)] }, "anketa.belgi", "jobs[0].organization" },
        { "uzun matnda yo'nalish izolyatori", Toliq() with { Strengths = "Mas’uliyat\u2066" }, "anketa.belgi", "strengths" },
    };

    // ────────────────────────────────────────────────────────────
    //  Ruxsat etilmagan belgilar (xavfsizlik ko'rigi L3)
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("\u0000")]
    [InlineData("\u0007")]
    [InlineData("\u007F")]
    [InlineData("\u202A")]
    [InlineData("\u202E")]
    [InlineData("\u2066")]
    [InlineData("\u2069")]
    [InlineData("\u200B")]
    [InlineData("\u200D")]
    [InlineData("\u2060")]
    [InlineData("\uFEFF")]
    [InlineData("\uE000")]
    [InlineData("\u0378")]
    public void Boshqarish_yonalish_korinmas_va_tayinlanmagan_belgi_rad_etiladi(string belgi)
    {
        QuestionnaireFormValidator.HasForbiddenCharacter("Sardor" + belgi + "bek", multiline: false).ShouldBeTrue();
        QuestionnaireFormValidator.HasForbiddenCharacter("Sardor" + belgi + "bek", multiline: true).ShouldBeTrue("uzun matnda ham");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\t")]
    public void Yangi_qator_faqat_uzun_matnda(string belgi)
    {
        QuestionnaireFormValidator.HasForbiddenCharacter("birinchi" + belgi + "ikkinchi", multiline: true).ShouldBeFalse();
        QuestionnaireFormValidator.HasForbiddenCharacter("birinchi" + belgi + "ikkinchi", multiline: false).ShouldBeTrue();
    }

    [Theory]
    [InlineData("G‘ulomov Sardor")]
    [InlineData("Ғуломов Сардор Бахтиёр ўғли")]
    [InlineData("Mas’uliyatli, «intizomli» — 100% 😊")]
    [InlineData("Ёқубов-Ўринбоев")]
    public void Oddiy_matn_otadi(string matn)
        => QuestionnaireFormValidator.HasForbiddenCharacter(matn, multiline: false).ShouldBeFalse();

    [Fact]
    public void Uzun_matnda_xatboshi_qabul_qilinadi()
    {
        var natija = QuestionnaireFormValidator.Validate(Toliq() with { Motivation = "Oila.\nKasbda o‘sish." }, Bugun, Kanallar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);
        natija.Value.Motivation.ShouldBe("Oila.\nKasbda o‘sish.");
    }

    // ────────────────────────────────────────────────────────────
    //  Imzo ismi — lotin/kirill, tutuqlar
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("G‘ulomov Sardor", "G'ulomov", "Sardor")]
    [InlineData("gulomov sardor baxtiyor o‘g‘li", "Gʻulomov", "Sardor")]
    [InlineData("Ғуломов Сардор", "G‘ulomov", "Sardor")]
    [InlineData("Karimova Oydin", "Каримова", "Ойдин")]
    [InlineData("O‘tkir   Yo‘ldoshev", "Йўлдошев", "Ўткир")]
    public void Imzo_ismi_yozuv_va_tutuqdan_qat_i_nazar_mos_keladi(string imzo, string familiya, string ism)
        => QuestionnaireFormValidator.SignatureMatches(imzo, familiya, ism).ShouldBeTrue();

    [Theory]
    [InlineData("Sardor", "G‘ulomov", "Sardor")]
    [InlineData("", "G‘ulomov", "Sardor")]
    [InlineData("Karimov Aziz", "G‘ulomov", "Sardor")]
    public void Imzo_ismi_mos_kelmasa_rad_etiladi(string imzo, string familiya, string ism)
        => QuestionnaireFormValidator.SignatureMatches(imzo, familiya, ism).ShouldBeFalse();

    // ────────────────────────────────────────────────────────────
    //  Mavjud maydonlarga ko'chirish (SPEC §5)
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(500_000, SalaryExpectation.From1To2Million)]
    [InlineData(1_999_999, SalaryExpectation.From1To2Million)]
    [InlineData(2_000_000, SalaryExpectation.From2To3Million)]
    [InlineData(3_500_000, SalaryExpectation.From3To4Million)]
    [InlineData(4_000_000, SalaryExpectation.From4To5Million)]
    [InlineData(4_999_999, SalaryExpectation.From4To5Million)]
    [InlineData(5_000_000, SalaryExpectation.From5To7Million)]
    [InlineData(6_999_999, SalaryExpectation.From5To7Million)]
    [InlineData(7_000_000, SalaryExpectation.From7Million)]
    public void Aniq_maosh_oraliqqa_ogiriladi(long summa, SalaryExpectation oraliq)
        => QuestionnaireFormMapping.SalaryRange(summa).ShouldBe(oraliq);

    [Theory]
    [InlineData(0, LanguageLevel.Elementary)]
    [InlineData(1, LanguageLevel.Elementary)]
    [InlineData(2, LanguageLevel.Intermediate)]
    [InlineData(3, LanguageLevel.UpperIntermediate)]
    [InlineData(4, LanguageLevel.Advanced)]
    [InlineData(5, LanguageLevel.Native)]
    public void Rus_tili_darajasi_ogiriladi(int daraja, LanguageLevel kutilgan)
        => QuestionnaireFormMapping.RussianLevel(daraja).ShouldBe(kutilgan);

    [Fact]
    public void Anketa_mavjud_anketa_maydonlariga_ochadi()
    {
        var javoblar = QuestionnaireFormValidator.Validate(Toliq(), Bugun, Kanallar).Value;
        var rozilik = new DateTimeOffset(2026, 10, 24, 14, 41, 0, TimeSpan.FromHours(5));

        var anketa = QuestionnaireFormMapping.ToQuestionnaire(Guid.CreateVersion7(), javoblar, rozilik);

        anketa.IsSuccess.ShouldBeTrue(anketa.IsFailure ? anketa.Error.ToString() : null);

        var q = anketa.Value;
        q.Education.ShouldBe(EducationLevel.HigherMaster);
        q.MaritalStatus.ShouldBe(MaritalStatus.Widowed);
        q.RussianLevel.ShouldBe(LanguageLevel.UpperIntermediate);
        q.ExpectedSalary.ShouldBe(SalaryExpectation.From4To5Million);
        q.OfferAcceptedAt.ShouldBe(rozilik);
        q.OfferAcceptedAt!.Value.Offset.ShouldBe(TimeSpan.Zero, "timestamptz faqat UTC qabul qiladi");
        q.HasDriverLicense.ShouldBeTrue();
        q.DriverLicenseCategories.ShouldBe("BC");
        q.LastSalary.ShouldBe("3 000 000 so‘m");
        q.OfficeSkills.ShouldBe("MS Word — 3, MS Excel — 2, Google Docs / Sheets — 1", "faqat ofis guruhi — 1C bu qatorga tushmaydi");
        q.OtherLanguages.ShouldBe("O‘zbek tili: so‘zlashuv 5, o‘qish 5, yozish 5");
        q.WorkExperience.ShouldBe("«Metan Gaz» MChJ — Payvandchi (03.2021–hozir)\nUmumiy: 5 yil 3 oy");
    }

    [Fact]
    public void Fayl_nomi_tutuqsiz_va_bosh_joysiz()
        => SubmitQuestionnaireFormCommandHandler
            .FileName(1024, Toliq() with { LastName = "G‘ulomov", FirstName = "Ali Vali" })
            .ShouldBe("Anketa-HR-F02-1024-Gulomov-Ali-Vali.pdf");

    /// <summary>Barcha majburiy maydonlari to'g'ri anketa — har bir xato testi undan bitta maydonni buzadi.</summary>
    private static QuestionnaireFormAnswers Toliq() => new()
    {
        LastName = "G‘ulomov",
        FirstName = "Sardor",
        BirthDate = Bugun.AddYears(-27),
        RegisteredAddress = "Toshkent sh.",
        ActualAddress = "Toshkent vil.",
        Phone = "+998905551122",
        MaritalStatus = MaritalStatus.Widowed,
        MilitaryService = FormMilitaryService.Reserve,
        HasDriverLicense = true,
        DriverLicenseCategories = ["B", "C"],
        EducationLevel = EducationLevel.HigherMaster,
        Languages =
        [
            new FormLanguageSkill(FormLanguage.Uzbek, null, 5, 5, 5),
            new FormLanguageSkill(FormLanguage.Russian, null, 3, 3, 2),
        ],
        ComputerSkills = new Dictionary<string, int> { ["msWord"] = 3, ["msExcel"] = 2, ["googleWorkspace"] = 1, ["oneC"] = 1 },
        Jobs = [new FormJob("«Metan Gaz» MChJ", "Payvandchi", "2021-03", null, true)],
        TotalExperienceYears = 5,
        TotalExperienceMonths = 3,
        FamilyMembers = [new FormFamilyMember(FormFamilyRelation.Mother, "G‘ulomova Dilnoza")],
        ReferralChannelId = Tanishlar.Id,
        ReferralDetail = "Karimov Anvar",
        HasAcquaintances = false,
        LastSalary = 3_000_000,
        ExpectedSalary = 4_500_000,
        AvailableFrom = Bugun,
        CanWorkShifts = true,
        CanWorkOvertime = true,
        CanTravel = false,
        HasOtherJob = false,
        AgreesToTrial = true,
        HasHealthRestriction = false,
        HasConviction = false,
        Motivation = "Oila",
        WhyUs = "Barqaror",
        Strengths = "Mas’uliyat",
        Weaknesses = "Shoshqaloqlik",
        ConsentAccuracy = true,
        ConsentPersonalData = true,
        ConsentReferenceCheck = true,
        ConsentNotifyChanges = true,
        ConsentConfidentiality = true,
        SignatureName = "G‘ulomov Sardor",
    };
}
