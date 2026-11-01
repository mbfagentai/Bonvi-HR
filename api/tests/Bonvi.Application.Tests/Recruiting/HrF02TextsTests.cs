using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// HR-F02 matnlari HRMentor dan (SPEC-blankalar-hrmentor §4, §5, §8): katalog, HR javobini
/// tekshirish, amaldagi ro'yxatlar va server tekshiruvi.
/// </summary>
public sealed class HrF02TextsTests
{
    private static HrF02TextCatalog Katalog => HrF02TextCatalog.Current;

    // ────────────────────────────────────────────────────────────
    //  Katalog
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Katalogda_har_kiosk_kaliti_va_har_PDF_matni_bir_martadan()
    {
        using var stream = typeof(HrF02TextCatalog).Assembly.GetManifestResourceStream(
            "Bonvi.Application.Recruiting.Forms.Texts.kiosk-texts.json");
        stream.ShouldNotBeNull("kiosk-texts.json ichki resurs bo'lib qo'shilgan");

        var eksport = JsonNode.Parse(stream)!.AsObject();
        var kioskKalitlari = eksport["groups"]!.AsArray()
            .SelectMany(group => group!["keys"]!.AsArray().Select(key => key!.GetValue<string>()))
            .ToList();

        kioskKalitlari.ShouldNotBeEmpty();
        kioskKalitlari.ShouldBeUnique();

        foreach (var kalit in kioskKalitlari)
        {
            Katalog.Items.ShouldContainKey(kalit);
            Katalog.Items[kalit].IsBlank.ShouldBeFalse(kalit);
            Katalog.Items[kalit].Default.Keys.ShouldBe(FormTexts.Languages, ignoreOrder: true, kalit);
        }

        foreach (var kalit in QuestionnairePdfTexts.Keys)
        {
            Katalog.Items.ShouldContainKey(kalit);
            Katalog.Items[kalit].IsBlank.ShouldBeTrue(kalit);
            Katalog.Items[kalit].Default[FormTexts.Latin].ShouldBe(QuestionnairePdfTexts.Default(kalit));
        }

        Katalog.Items.Count.ShouldBe(kioskKalitlari.Count + QuestionnairePdfTexts.Keys.Count, "boshqa hech narsa yo'q");
    }

    [Fact]
    public void Katalog_JSON_i_va_hashi_barqaror_va_HRMentor_chegaralari_ichida()
    {
        var json = JsonNode.Parse(Katalog.Json)!.AsObject();

        json["schema"]!.GetValue<int>().ShouldBe(1);
        json["code"]!.GetValue<string>().ShouldBe(CandidateForm.HrF02);
        Katalog.Hash.Length.ShouldBe(64);

        // Bir xil manbadan — bir xil JSON (katalog faqat o'zgarganda qayta yuboriladi)
        HrF02TextCatalog.Build(KioskEksporti()).Hash.ShouldBe(Katalog.Hash);

        foreach (var item in Katalog.Items.Values)
        {
            item.Name.Length.ShouldBeLessThanOrEqualTo(300, item.Key);
            item.MaxLength.ShouldBeInRange(60, 2000, item.Key);
            item.Default.Values.ShouldAllBe(text => text.Length <= item.MaxLength && text.Length <= 5000);
        }

        foreach (var list in Katalog.Lists.Values)
        {
            list.Options.ShouldAllBe(option => FormTexts.Languages.All(language => option.Label.ContainsKey(language)));
            list.Options.Select(option => option.Value).ShouldBeUnique(list.Id);
        }
    }

    [Fact]
    public void Royxat_qiymatlari_javob_saqlaydigan_sanoqlar_bilan_bir_xil()
    {
        Qiymatlar(HrF02TextCatalog.HousingList).ShouldBe(Enum.GetNames<FormHousingType>());
        Qiymatlar(HrF02TextCatalog.MaritalList).ShouldBe(Enum.GetNames<MaritalStatus>(), ignoreOrder: true);
        Qiymatlar(HrF02TextCatalog.MilitaryList).ShouldBe(Enum.GetNames<FormMilitaryService>(), ignoreOrder: true);
        Qiymatlar(HrF02TextCatalog.EducationList).ShouldBe(Enum.GetNames<EducationLevel>(), ignoreOrder: true);
        Qiymatlar(HrF02TextCatalog.StartAfterList).ShouldBe(Enum.GetNames<FormStartAfter>(), ignoreOrder: true);
        Qiymatlar(HrF02TextCatalog.RelationList).ShouldBe(Enum.GetNames<FormFamilyRelation>(), ignoreOrder: true);
        Qiymatlar(HrF02TextCatalog.LanguageList).ShouldBe(Enum.GetNames<FormLanguage>(), ignoreOrder: true);
        Qiymatlar(HrF02TextCatalog.DriverLicenseList).ShouldBe(QuestionnaireFormCatalog.DriverLicenseCategories);
        Qiymatlar(HrF02TextCatalog.ComputerList).ShouldBe(QuestionnaireFormCatalog.ComputerKeys, ignoreOrder: true);

        var kompyuter = Katalog.Lists[HrF02TextCatalog.ComputerList];
        kompyuter.Open.ShouldBeTrue();
        kompyuter.Groups.Select(group => group.Id).ShouldBe(HrF02TextCatalog.ComputerGroupIds);
        Katalog.Lists.Values.Where(list => list.Id != HrF02TextCatalog.ComputerList).ShouldAllBe(list => !list.Open);
    }

    // ────────────────────────────────────────────────────────────
    //  HR javobini tekshirish (Shape)
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("salom")]
    [InlineData("[]")]
    [InlineData("""{"texts":{}}""")]
    [InlineData("""{"revision":0}""")]
    [InlineData("""{"revision":"3"}""")]
    [InlineData("""{"revision":3,"code":"HR-F09"}""")]
    public void Buzuq_javob_texts_invalid_va_hech_narsa_saqlanmaydi(string javob)
    {
        var natija = Katalog.Shape(javob);

        natija.IsFailure.ShouldBeTrue(javob);
        natija.Error.ShouldBe(HrMentorErrors.TextsInvalid);
    }

    [Fact]
    public void Yaroqsiz_matnlar_tashlanadi_qolgani_qabul_qilinadi()
    {
        var uzun = new string('a', Katalog.Items["kiosk.start.title"].MaxLength + 1);

        var texts = Shaped(new JsonObject
        {
            ["revision"] = 4,
            ["code"] = CandidateForm.HrF02,
            ["texts"] = new JsonObject
            {
                ["kiosk.start.title"] = Uch("Nomzod  anketasi\n(yangi)", "Анкета (новая)", "Анкета (новая)"),
                ["kiosk.screen.personal"] = Uch(uzun, "Шахсий", "Личные"),
                ["kiosk.error.tooLong"] = Uch("Ko'pi bilan {{max}} ta belgi", "Кўпи билан {{n}} та", "Не более {{max}}"),
                ["kiosk.noSuchKey"] = Uch("x", "x", "x"),
                ["pdf.personal.title"] = new JsonObject { ["uz-Latn"] = "1. SHAXSIY MA'LUMOT" },
                ["pdf.personal.militaryServed"] = new JsonObject { ["uz-Latn"] = "{{label}} ({{months}} oy)" },
                ["pdf.head.page"] = new JsonObject { ["uz-Latn"] = "{{page}}-bet" },
            },
        });

        texts.Revision.ShouldBe(4);

        // Qator ko'chirish — bo'shliq, ichki qo'sh bo'shliq — tegilmaydi
        texts.Strings[FormTexts.Latin]["kiosk.start.title"].ShouldBe("Nomzod  anketasi (yangi)");
        texts.Strings[FormTexts.Russian]["kiosk.start.title"].ShouldBe("Анкета (новая)");

        // Chegaradan uzun — tashlandi (shu tilda), boshqa tillari qoldi
        texts.Strings[FormTexts.Latin].ShouldNotContainKey("kiosk.screen.personal");
        texts.Strings[FormTexts.Russian]["kiosk.screen.personal"].ShouldBe("Личные");

        // O'rin belgisi asl matnnikidan farqli — faqat o'sha til tashlandi; lotinda tutuq qog'ozdagidek
        texts.Strings[FormTexts.Latin]["kiosk.error.tooLong"].ShouldBe("Ko‘pi bilan {{max}} ta belgi");
        texts.Strings[FormTexts.Cyrillic].ShouldNotContainKey("kiosk.error.tooLong");
        texts.Strings[FormTexts.Russian]["kiosk.error.tooLong"].ShouldBe("Не более {{max}}");

        // Noma'lum kalit — yo'q
        texts.Strings.Values.ShouldAllBe(strings => !strings.ContainsKey("kiosk.noSuchKey"));

        // PDF matnlari — alohida, nomzodga yuborilmaydi
        texts.Blank["pdf.personal.title"].ShouldBe("1. SHAXSIY MA’LUMOT");
        texts.Blank["pdf.personal.militaryServed"].ShouldBe("{{label}} ({{months}} oy)");
        texts.Blank.ShouldNotContainKey("pdf.head.page", "asl matnda o'rin belgisi yo'q");
        texts.Strings.Values.ShouldAllBe(strings => strings.Keys.All(key => !key.StartsWith("pdf.", StringComparison.Ordinal)));
    }

    [Fact]
    public void Yopiq_royxat_tushib_qolgan_qiymat_oxiriga_qoshiladi_noma_lumi_tashlanadi()
    {
        var texts = Shaped(Royxatli(HrF02TextCatalog.MaritalList, new JsonObject
        {
            ["options"] = new JsonArray(
                Variant("Widowed", "Beva ayol"),
                Variant("Married", "Oilali", hidden: false),
                Variant("x-civil", "Fuqarolik nikohi"),
                Variant("Divorced", "Ajrashgan", hidden: true)),
        }));

        var royxat = texts.Lists[HrF02TextCatalog.MaritalList];
        royxat.Options.Select(option => option.Value).ShouldBe(["Widowed", "Married", "Divorced", "Unmarried"]);
        royxat.Options[0].Label[FormTexts.Latin].ShouldBe("Beva ayol");
        royxat.Options[2].Hidden.ShouldBeTrue();
        royxat.Options[3].Hidden.ShouldBeFalse("HR javobida yo'q — katalogdagidek");
        royxat.Groups.ShouldBeEmpty();
    }

    [Fact]
    public void Royxatda_korinadigan_variant_yetmasa_ozgarish_tashlanadi()
    {
        // Tillar — hammasi ko'rinishi shart (min_visible 5)
        var texts = Shaped(Royxatli(HrF02TextCatalog.LanguageList, new JsonObject
        {
            ["options"] = new JsonArray(Variant("Chinese", "Xitoy tili", hidden: true)),
        }));

        texts.HasList(HrF02TextCatalog.LanguageList).ShouldBeFalse();
    }

    [Fact]
    public void Ochiq_royxat_HR_qoshgan_dasturni_oladi_yaroqsiz_qiymatni_tashlaydi()
    {
        var texts = Shaped(Royxatli(HrF02TextCatalog.ComputerList, new JsonObject
        {
            ["groups"] = new JsonObject { ["special"] = Uch("Ishlab chiqarish dasturlari", "Ишлаб чиқариш", "Производство") },
            ["options"] = new JsonArray(
                Variant("msExcel", "MS Excel", group: "office"),
                Variant("x-kompas", "KOMPAS-3D", group: "special"),
                Variant("x-bad value", "Yomon", group: "special"),
                Variant("python", "Python", group: "special"),
                Variant("x-nogroup", "Guruhsiz", group: "nosuch"),
                Variant("windows", "Windows", group: "os", hidden: true)),
        }));

        var royxat = texts.Lists[HrF02TextCatalog.ComputerList];
        royxat.Options.Select(option => option.Value).ShouldBe(["msExcel", "x-kompas", "x-nogroup", "windows"], "ochiq ro'yxatda yo'q asl qiymat — HR olib tashlagan");
        royxat.Options.Single(option => option.Value == "x-kompas").Group.ShouldBe("special");
        royxat.Options.Single(option => option.Value == "x-nogroup").Group.ShouldBe("os", "noma'lum guruh — birinchisi");
        royxat.Options.Single(option => option.Value == "x-kompas").Label[FormTexts.Russian].ShouldBe("KOMPAS-3D");
        royxat.Groups["special"][FormTexts.Latin].ShouldBe("Ishlab chiqarish dasturlari");
        royxat.Groups["office"][FormTexts.Latin].ShouldBe("Ofis dasturlari", "HR tegmagan guruh — katalogdagidek");

        texts.ComputerValues().ShouldBe(["msExcel", "x-kompas", "x-nogroup", "windows"], ignoreOrder: true);
    }

    [Fact]
    public void Ochiq_royxat_chegaradan_oshsa_ozgarish_tashlanadi()
    {
        var variantlar = new JsonArray();
        for (var index = 0; index <= HrF02TextCatalog.MaxComputerOptions; index++)
        {
            variantlar.Add(Variant($"x-dastur{index}", $"Dastur {index}", group: "special"));
        }

        var texts = Shaped(Royxatli(HrF02TextCatalog.ComputerList, new JsonObject { ["options"] = variantlar }));

        texts.HasList(HrF02TextCatalog.ComputerList).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Amaldagi matnlar (FormTexts)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void PDF_matni_HR_niki_yoki_asl_ornini_bosish_va_tutuq()
    {
        FormTexts.Empty.Pdf("pdf.personal.title").ShouldBe(QuestionnairePdfTexts.Default("pdf.personal.title"));
        FormTexts.Empty.Pdf("pdf.personal.militaryServed", ("label", "O‘tagan"), ("months", "12")).ShouldBe("O‘tagan, muddati: 12 oy");
        FormTexts.Empty.PdfAround("pdf.consent.intro", "name").ShouldBe(("Men, ", " (F.I.Sh.):"));
        Should.Throw<KeyNotFoundException>(() => FormTexts.Empty.Pdf("pdf.noSuchKey"));

        var texts = Shaped(new JsonObject
        {
            ["revision"] = 2,
            ["texts"] = new JsonObject
            {
                ["pdf.personal.militaryServed"] = new JsonObject { ["uz-Latn"] = "{{label}} — {{months}} oy" },
                ["pdf.consent.intro"] = new JsonObject { ["uz-Latn"] = "Men {{name}} quyidagilarni tasdiqlayman:" },
                ["pdf.health.title"] = new JsonObject { ["uz-Latn"] = "9. SOG'LIQ" },
            },
        });

        texts.Pdf("pdf.personal.militaryServed", ("label", "O‘tagan"), ("months", "6")).ShouldBe("O‘tagan — 6 oy");
        texts.PdfAround("pdf.consent.intro", "name").ShouldBe(("Men ", " quyidagilarni tasdiqlayman:"));
        texts.Pdf("pdf.health.title").ShouldBe("9. SOG‘LIQ");
        texts.Pdf("pdf.personal.title").ShouldBe(QuestionnairePdfTexts.Default("pdf.personal.title"), "HR tegmagan — asl");
    }

    [Fact]
    public void Kompyuter_ustunlari_HR_royxati_boyicha_yashirilgani_va_olib_tashlangani_faqat_tanlangan_bolsa()
    {
        FormTexts.Empty.ComputerGroups(new Dictionary<string, int>()).ShouldBeSameAs(QuestionnaireFormCatalog.ComputerGroups);

        var texts = Shaped(Royxatli(HrF02TextCatalog.ComputerList, new JsonObject
        {
            ["groups"] = new JsonObject { ["special"] = Uch("Ishlab chiqarish", "Ишлаб чиқариш", "Производство") },
            ["options"] = new JsonArray(
                Variant("msExcel", "MS Excel", group: "office"),
                Variant("msWord", "Word (matn)", group: "office"),
                Variant("googleWorkspace", "Google Docs / Sheets", group: "office", hidden: true),
                Variant("x-kompas", "KOMPAS-3D", group: "special"),
                Variant("windows", "Windows", group: "os")),
        }));

        // Hech narsa tanlanmagan: yashirilgani va olib tashlanganlari yo'q
        var bosh = texts.ComputerGroups(new Dictionary<string, int>());
        bosh.Select(group => group.Title).ShouldBe(["Operatsion tizim", "Ofis dasturlari", "Ishlab chiqarish"]);
        bosh[0].Items.Select(item => item.Key).ShouldBe(["windows"]);
        bosh[1].Items.Select(item => (item.Key, item.Label)).ShouldBe([("msExcel", "MS Excel"), ("msWord", "Word (matn)")]);
        bosh[2].Items.Select(item => (item.Key, item.Label)).ShouldBe([("x-kompas", "KOMPAS-3D")]);

        // Tanlangan yashirin va olib tashlangan dastur — imzolangan javob yo'qolmaydi
        var tanlangan = texts.ComputerGroups(new Dictionary<string, int> { ["googleWorkspace"] = 2, ["autocad"] = 3, ["x-kompas"] = 4 });
        tanlangan[1].Items.Select(item => item.Key).ShouldBe(["msExcel", "msWord", "googleWorkspace"]);
        tanlangan[2].Items.Select(item => (item.Key, item.Label)).ShouldBe([("x-kompas", "KOMPAS-3D"), ("autocad", "AutoCAD")]);
    }

    [Fact]
    public void HR_tegmagan_variant_nomi_ozgarmagan_hisoblanadi()
    {
        var texts = Shaped(Royxatli(HrF02TextCatalog.EducationList, new JsonObject
        {
            ["options"] = new JsonArray(
                Variant("HigherMaster", "Oliy (magistr)"),
                Variant("SecondarySchool", "O'rta (maktab)"),
                Variant("HigherComplete", "Oliy — bakalavr")),
        }));

        var variantlar = texts.Options(HrF02TextCatalog.EducationList);
        FormTexts.ChangedLatin(HrF02TextCatalog.EducationList, variantlar[0]).ShouldBeNull();
        FormTexts.ChangedLatin(HrF02TextCatalog.EducationList, variantlar[1]).ShouldBeNull("faqat tutuq farqi — o'zgarmagan");
        FormTexts.ChangedLatin(HrF02TextCatalog.EducationList, variantlar[2]).ShouldBe("Oliy — bakalavr");
    }

    [Fact]
    public void Saqlangan_qator_buzuq_bolsa_ornatilgan_matnlar()
    {
        var texts = FormTexts.FromPayload(7, "{buzuq");

        texts.Revision.ShouldBeNull();
        texts.Lists.ShouldBeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    //  Server tekshiruvi — HR qo'shgan dastur (§5.2)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void HR_qoshgan_dastur_qabul_qilinadi_noma_lumi_baribir_rad_etiladi()
    {
        var bugun = new DateOnly(2026, 10, 24);
        var kanal = new ReferralChannelOptionDto(Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000005"), "Telegram", true, false);
        var royxat = new HashSet<string>(StringComparer.Ordinal) { "x-kompas" };

        var javob = Namuna(kanal.Id) with { ComputerSkills = new Dictionary<string, int> { ["msWord"] = 3, ["x-kompas"] = 2 } };

        var qabul = QuestionnaireFormValidator.Validate(javob, bugun, [kanal], royxat);
        qabul.IsSuccess.ShouldBeTrue(qabul.IsFailure ? qabul.Error.ToString() : null);
        qabul.Value.ComputerSkills!["x-kompas"].ShouldBe(2);

        var royxatsiz = QuestionnaireFormValidator.Validate(javob, bugun, [kanal]);
        royxatsiz.IsFailure.ShouldBeTrue();
        royxatsiz.Error.Details["field"].ShouldBe("computerSkills.x-kompas");

        var begona = QuestionnaireFormValidator.Validate(
            javob with { ComputerSkills = new Dictionary<string, int> { ["x-boshqa"] = 2 } }, bugun, [kanal], royxat);
        begona.IsFailure.ShouldBeTrue();
        begona.Error.Details["field"].ShouldBe("computerSkills.x-boshqa");
    }

    [Fact]
    public void Anketa_xulosasidagi_ofis_dasturlari_HR_nomi_bilan()
    {
        var texts = Shaped(Royxatli(HrF02TextCatalog.ComputerList, new JsonObject
        {
            ["options"] = new JsonArray(
                Variant("msWord", "Word (matn)", group: "office"),
                Variant("x-onlyoffice", "OnlyOffice", group: "office")),
        }));

        var javob = Namuna(Guid.NewGuid()) with
        {
            ComputerSkills = new Dictionary<string, int> { ["msWord"] = 3, ["x-onlyoffice"] = 2, ["msExcel"] = 4 },
        };

        var anketa = QuestionnaireFormMapping.ToQuestionnaire(Guid.NewGuid(), javob, DateTimeOffset.UtcNow, texts);
        anketa.IsSuccess.ShouldBeTrue(anketa.IsFailure ? anketa.Error.ToString() : null);
        anketa.Value.OfficeSkills.ShouldBe("Word (matn) — 3, OnlyOffice — 2, MS Excel — 4");

        var asl = QuestionnaireFormMapping.ToQuestionnaire(Guid.NewGuid(), javob with { ComputerSkills = new Dictionary<string, int> { ["msWord"] = 3 } }, DateTimeOffset.UtcNow);
        asl.Value.OfficeSkills.ShouldBe("MS Word — 3");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static string[] Qiymatlar(string listId) => [.. Katalog.Lists[listId].Options.Select(option => option.Value)];

    private static string KioskEksporti()
    {
        using var stream = typeof(HrF02TextCatalog).Assembly.GetManifestResourceStream(
            "Bonvi.Application.Recruiting.Forms.Texts.kiosk-texts.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static FormTexts Shaped(JsonObject served)
    {
        var natija = Katalog.Shape(served.ToJsonString());
        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        return FormTexts.FromPayload(natija.Value.Revision, natija.Value.PayloadJson);
    }

    private static JsonObject Royxatli(string listId, JsonObject list)
        => new() { ["revision"] = 3, ["code"] = CandidateForm.HrF02, ["lists"] = new JsonObject { [listId] = list } };

    private static JsonObject Uch(string latin, string cyrillic, string russian)
        => new() { ["uz-Latn"] = latin, ["uz-Cyrl"] = cyrillic, ["ru"] = russian };

    private static JsonObject Variant(string value, string latin, string? group = null, bool hidden = false)
        => new()
        {
            ["value"] = value,
            ["group"] = group,
            ["label"] = Uch(latin, latin, latin),
            ["hidden"] = hidden,
        };

    /// <summary>Validatordan o'tadigan anketa (<c>QuestionnaireFormValidatorTests.Toliq</c> nusxasi).</summary>
    private static QuestionnaireFormAnswers Namuna(Guid referralChannelId) => new()
    {
        LastName = "G‘ulomov",
        FirstName = "Sardor",
        BirthDate = new DateOnly(1999, 5, 1),
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
        ComputerSkills = new Dictionary<string, int> { ["msWord"] = 3 },
        HasWorkExperience = true,
        Jobs = [new FormJob("«Metan Gaz» MChJ", "Payvandchi", "2021-03", null, true)],
        References = [new FormReference("Karimov Anvar", "«Metan Gaz» MChJ, sex boshlig‘i", "+998901112233")],
        FamilyMembers = [new FormFamilyMember(FormFamilyRelation.Mother, "G‘ulomova Dilnoza")],
        ReferralChannelId = referralChannelId,
        HasAcquaintances = false,
        LastSalary = 3_000_000,
        ExpectedSalary = 4_500_000,
        StartAfter = FormStartAfter.OneWeek,
        CanWorkShifts = true,
        CanWorkOvertime = true,
        CanTravel = false,
        HasOtherJob = false,
        HasLoan = false,
        AgreesToTrial = true,
        HasHealthRestriction = false,
        HasConviction = false,
        Motivation = "Oila",
        WhyUs = "Barqaror",
        Strengths = "Mas’uliyat",
        Weaknesses = "Shoshqaloqlik",
        Motivators = "Adolatli baholash",
        SuccessDefinition = "Ishni vaqtida bajarish",
        LastJobAchievement = "Brak ulushini kamaytirdik",
        BestIdea = "Tekshiruv shabloni",
        BiggestChallenge = "Muddatli buyurtma",
        StressHandling = "Tinch",
        UnfairnessToYou = "Rahbar bilan gaplashaman",
        UnfairnessToCompany = "Xabar beraman",
        Teamwork = "Ha",
        PreviousTeamOpinion = "Yaxshi jamoa",
        StudentWorkExperience = "Yo‘q",
        FiveYearPlan = "Usta",
        ExtraNote = "Yo‘q",
        ConsentAccuracy = true,
        ConsentPersonalData = true,
        ConsentReferenceCheck = true,
        ConsentNotifyChanges = true,
        ConsentConfidentiality = true,
        SignatureName = "G‘ulomov Sardor",
    };
}
