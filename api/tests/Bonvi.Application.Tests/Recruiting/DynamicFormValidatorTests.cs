using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// Konstruktor anketasining server tekshiruvi (SPEC-anketa-konstruktori §6.1, AC-AK10): har qoida o'z
/// kodi va maydon yo'li bilan; yashirin maydon javobi tashlanadi (R5).
/// </summary>
/// <remarks>
/// Javoblar HRMentor shaklida qo'lda yozilgan (o'rnatilgan anketadan o'girilmagan) — tekshiruvchining
/// o'zi sinaladi, o'giruvchi emas.
/// </remarks>
public sealed class DynamicFormValidatorTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 24);

    private static readonly ReferralChannelOptionDto Tanishlar =
        new(Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000001"), "Tanishlar orqali", true, true);

    private static readonly ReferralChannelOptionDto Telegram =
        new(Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000005"), "Telegram", true, false);

    private static readonly ReferralChannelOptionDto[] Kanallar = [Tanishlar, Telegram];

    private static readonly FormDefinition Urug = FormDefinitionReader.Read(HrF02Seed.Build()).Value;

    /// <summary>Faqat majburiy javoblar — avval ishlamagan, mashinasiz.</summary>
    private static JsonObject Minimal() => new()
    {
        ["lastName"] = "Karimova",
        ["firstName"] = "Oydin",
        ["birthDate"] = "2007-01-10",
        ["registeredAddress"] = "Toshkent sh., Chilonzor tumani",
        ["actualAddress"] = "Toshkent sh., Chilonzor tumani",
        ["phone"] = "90 555 11 99",
        ["maritalStatus"] = "Unmarried",
        ["hasDriverLicense"] = false,
        ["educationLevel"] = "SecondarySchool",
        ["languages"] = new JsonObject { ["rows"] = new JsonObject { ["Russian"] = new JsonArray(1, 1, 0) } },
        ["hasWorkExperience"] = false,
        ["familyMembers"] = new JsonArray(new JsonObject { ["relation"] = "Mother", ["fullName"] = "Karimova Nigora" }),
        ["referral"] = new JsonObject { ["channel"] = Telegram.Id.ToString() },
        ["hasAcquaintances"] = false,
        ["expectedSalary"] = 2_500_000,
        ["startAfter"] = "OnHireDay",
        ["canWorkShifts"] = false,
        ["canWorkOvertime"] = true,
        ["canTravel"] = false,
        ["hasOtherJob"] = false,
        ["hasLoan"] = false,
        ["agreesToTrial"] = true,
        ["hasHealthRestriction"] = false,
        ["hasConviction"] = false,
        ["motivation"] = "Tajriba orttirish.",
        ["whyUs"] = "Uyga yaqin.",
        ["strengths"] = "Tirishqoqman.",
        ["weaknesses"] = "Tajribam kam.",
        ["motivators"] = "Yaxshi jamoa.",
        ["successDefinition"] = "Ishni vaqtida bajarish.",
        ["biggestChallenge"] = "Imtihonga tayyorlanish.",
        ["stressHandling"] = "Tinchlanib olaman.",
        ["unfairnessToYou"] = "Rahbarga aytaman.",
        ["unfairnessToCompany"] = "Xabar beraman.",
        ["teamwork"] = "Ha.",
        ["studentWorkExperience"] = "Yo‘q.",
        ["fiveYearPlan"] = "Mutaxassis bo‘lib.",
        ["extraNote"] = "Yo‘q.",
        ["consentAccuracy"] = true,
        ["consentPersonalData"] = true,
        ["consentReferenceCheck"] = true,
        ["consentNotifyChanges"] = true,
        ["consentConfidentiality"] = true,
        ["signature"] = new JsonObject { ["name"] = "Karimova Oydin" },
    };

    private static JsonObject Ishlagan()
    {
        var javoblar = Minimal();
        javoblar["hasWorkExperience"] = true;
        javoblar["jobs"] = new JsonArray(new JsonObject
        {
            ["organization"] = "«Metan Gaz» MChJ",
            ["position"] = "Payvandchi",
            ["period"] = new JsonObject { ["from"] = "2021-03", ["current"] = true },
        });
        javoblar["references"] = new JsonArray(new JsonObject { ["fullName"] = "Karimov Anvar" });
        javoblar["lastJobAchievement"] = "Brak ulushini kamaytirdik.";
        javoblar["bestIdea"] = "Tekshiruv shablonini taklif qildim.";
        javoblar["previousTeamOpinion"] = "Jamoa yaxshi edi.";
        return javoblar;
    }

    private static Result<DynamicFormAnswers> Tekshir(JsonObject javoblar)
        => DynamicFormValidator.Validate(javoblar, Urug, Bugun, Kanallar);

    // ────────────────────────────────────────────────────────────
    //  To'g'ri javob va tozalash
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Minimal_javob_otadi_va_tozalanadi()
    {
        var javoblar = Minimal();
        javoblar["lastName"] = "  Karimova ";
        javoblar["telegram"] = "@oydin_k";
        javoblar["email"] = " ";
        javoblar["nimadir"] = "ta'rifda yo'q kalit";

        var natija = Tekshir(javoblar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? $"{natija.Error.Code} {natija.Error.Message}" : null);
        var qiymatlar = natija.Value.Values;

        qiymatlar["lastName"]!.GetValue<string>().ShouldBe("Karimova");
        qiymatlar["phone"]!.GetValue<string>().ShouldBe("+998905551199", "nomzod telefoni — to'liq mobil shaklda");
        qiymatlar["telegram"]!.GetValue<string>().ShouldBe("oydin_k");
        qiymatlar.ContainsKey("email").ShouldBeFalse("bo'sh javob saqlanmaydi");
        qiymatlar.ContainsKey("nimadir").ShouldBeFalse("ta'rifda yo'q kalit e'tiborsiz");
        qiymatlar["referral"]!["channel"]!.GetValue<string>().ShouldBe(Telegram.Id.ToString());
    }

    [Fact]
    public void Kodda_yasalgan_va_JSON_dan_oqilgan_sonlar_bir_xil_qabul_qilinadi()
    {
        var kodda = Tekshir(Minimal());
        var jsondan = Tekshir(JsonNode.Parse(Minimal().ToJsonString())!.AsObject());

        kodda.IsSuccess.ShouldBeTrue();
        jsondan.IsSuccess.ShouldBeTrue();
        kodda.Value.Write().ShouldBe(jsondan.Value.Write());
    }

    // ────────────────────────────────────────────────────────────
    //  R5: yashirin maydon javobi tashlanadi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Yashirin_maydonlar_javobi_tashlanadi_zanjir_boyicha_ham()
    {
        var javoblar = Minimal();
        javoblar["hasCar"] = false;
        javoblar["carModel"] = "Nexia";
        javoblar["driverLicenseCategories"] = new JsonArray("B");
        javoblar["militaryServiceMonths"] = 12;

        // Avval ishlamagan — ish joylari, tavsiya beruvchilar va ishga oid savollar ko'rinmaydi
        javoblar["jobs"] = Ishlagan()["jobs"]!.DeepClone();
        javoblar["references"] = Ishlagan()["references"]!.DeepClone();
        javoblar["lastJobAchievement"] = "Ko'rinmagan savol.";

        var natija = Tekshir(javoblar);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? $"{natija.Error.Code} {natija.Error.Message}" : null);
        var qiymatlar = natija.Value.Values;

        qiymatlar["hasCar"]!.GetValue<bool>().ShouldBeFalse();
        foreach (var kalit in new[] { "carModel", "driverLicenseCategories", "militaryServiceMonths", "jobs", "references", "lastJobAchievement" })
        {
            qiymatlar.ContainsKey(kalit).ShouldBeFalse(kalit);
        }
    }

    [Fact]
    public void Korinadigan_bolsa_saqlanadi_va_majburiysi_soraladi()
    {
        var ishlagan = Tekshir(Ishlagan());
        ishlagan.IsSuccess.ShouldBeTrue(ishlagan.IsFailure ? $"{ishlagan.Error.Code} {ishlagan.Error.Message}" : null);
        ishlagan.Value.Values["jobs"]![0]!["period"]!.ToJsonString().ShouldBe("""{"from":"2021-03","current":true}""");

        var mashinali = Minimal();
        mashinali["hasCar"] = true;
        mashinali["carModel"] = "Nexia";
        Tekshir(mashinali).Value.Values["carModel"]!.GetValue<string>().ShouldBe("Nexia");
    }

    // ────────────────────────────────────────────────────────────
    //  §6.1: har qoida — o'z kodi va yo'li
    // ────────────────────────────────────────────────────────────

    public static TheoryData<string, string, string> Xatolar() => new()
    {
        { "familiya_yoq", "anketa.majburiy", "lastName" },
        { "familiya_bosh", "anketa.majburiy", "lastName" },
        { "javob_harfsiz", "anketa.majburiy", "motivation" },
        { "familiya_uzun", "anketa.uzun", "lastName" },
        { "familiya_bidi", "anketa.belgi", "lastName" },
        { "familiya_yangi_qator", "anketa.belgi", "lastName" },
        { "familiya_son", "anketa.notogri", "lastName" },
        { "tugilgan_kichik", "anketa.notogri", "birthDate" },
        { "tugilgan_shakl", "anketa.notogri", "birthDate" },
        { "tugilgan_yoq_kun", "anketa.notogri", "birthDate" },
        { "variant_notanish", "anketa.notogri", "maritalStatus" },
        { "hani_matn", "anketa.notogri", "hasLoan" },
        { "pul_manfiy", "anketa.notogri", "expectedSalary" },
        { "pul_kasr", "anketa.notogri", "expectedSalary" },
        { "email", "anketa.notogri", "email" },
        { "telegram", "anketa.notogri", "telegram" },
        { "rus_tili_yoq", "anketa.majburiy", "languages.rows.Russian" },
        { "rus_tili_toliqmas", "anketa.majburiy", "languages.rows.Russian" },
        { "daraja_shkaladan", "anketa.notogri", "languages.rows.Russian" },
        { "daraja_jihat_soni", "anketa.notogri", "languages.rows.Russian" },
        { "til_notanish", "anketa.notogri", "languages.rows.Klingon" },
        { "oila_bosh", "anketa.majburiy", "familyMembers" },
        { "oila_ismsiz", "anketa.majburiy", "familyMembers[0].fullName" },
        { "oila_qarindosh_notanish", "anketa.notogri", "familyMembers[0].relation" },
        { "ish_joyi_yoq", "anketa.majburiy", "jobs" },
        { "davr_teskari", "anketa.notogri", "jobs[0].period" },
        { "davr_kelajak", "anketa.notogri", "jobs[0].period" },
        { "oqish_yillari_teskari", "anketa.notogri", "educations[0].years" },
        { "rozilik_yoq", "anketa.rozilik", "consentAccuracy" },
        { "rozilik_false", "anketa.rozilik", "consentPersonalData" },
        { "imzo_boshqa", "anketa.imzo_ism", "signature.name" },
        { "imzo_yoq", "anketa.majburiy", "signature.name" },
        { "kanal_yoq", "anketa.majburiy", "referral.channel" },
        { "kanal_notanish", "qayerdan.topilmadi", "referral.channel" },
        { "toifa_takror", "anketa.notogri", "driverLicenseCategories" },
    };

    [Theory]
    [MemberData(nameof(Xatolar))]
    public void Qoida_buzilsa_oz_kodi_va_maydon_yoli_bilan_rad(string holat, string kod, string yol)
    {
        var javoblar = Holat(holat);

        var natija = Tekshir(javoblar);

        natija.IsFailure.ShouldBeTrue(holat);
        natija.Error.Code.ShouldBe(kod, holat);
        natija.Error.Message.ShouldBe(yol, holat);
        natija.Error.Details["field"].ShouldBe(yol, holat);
    }

    [Fact]
    public void Nomzod_telefoni_faqat_Ozbekiston_mobil_raqami()
    {
        var javoblar = Minimal();
        javoblar["phone"] = "+7 916 123 45 67";

        var natija = Tekshir(javoblar);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldStartWith("telefon.");
        natija.Error.Details["field"].ShouldBe("phone");

        // Oila a'zosining telefoni (mobile_only: false) — shahar yoki xorijiy raqam ham bo'ladi
        var oila = Minimal();
        oila["familyMembers"]![0]!["phone"] = "+7 916 123 45 67";
        Tekshir(oila).Value.Values["familyMembers"]![0]!["phone"]!.GetValue<string>().ShouldBe("+7 916 123 45 67");

        var shahar = Minimal();
        shahar["familyMembers"]![0]!["phone"] = "71 234-56-7a";
        Tekshir(shahar).Error.Details["field"].ShouldBe("familyMembers[0].phone");
    }

    private static JsonObject Holat(string holat)
    {
        var javoblar = holat is "ish_joyi_yoq" or "davr_teskari" or "davr_kelajak" ? Ishlagan() : Minimal();

        switch (holat)
        {
            case "familiya_yoq":
                javoblar.Remove("lastName");
                break;
            case "familiya_bosh":
                javoblar["lastName"] = "   ";
                break;
            case "javob_harfsiz":
                javoblar["motivation"] = "- 1";
                break;
            case "familiya_uzun":
                javoblar["lastName"] = new string('a', 200);
                break;
            case "familiya_bidi":
                javoblar["lastName"] = "Kari‮mova";
                break;
            case "familiya_yangi_qator":
                javoblar["lastName"] = "Kari\nmova";
                break;
            case "familiya_son":
                javoblar["lastName"] = 42;
                break;
            case "tugilgan_kichik":
                javoblar["birthDate"] = "2015-01-10";
                break;
            case "tugilgan_shakl":
                javoblar["birthDate"] = "10.01.2007";
                break;
            case "tugilgan_yoq_kun":
                javoblar["birthDate"] = "2007-02-30";
                break;
            case "variant_notanish":
                javoblar["maritalStatus"] = "Single";
                break;
            case "hani_matn":
                javoblar["hasLoan"] = "ha";
                break;
            case "pul_manfiy":
                javoblar["expectedSalary"] = -1;
                break;
            case "pul_kasr":
                javoblar["expectedSalary"] = 2.5;
                break;
            case "email":
                javoblar["email"] = "oydin.example.uz";
                break;
            case "telegram":
                javoblar["telegram"] = "@oy din";
                break;
            case "rus_tili_yoq":
                javoblar["languages"] = new JsonObject { ["rows"] = new JsonObject { ["Uzbek"] = new JsonArray(5, 5, 5) } };
                break;
            case "rus_tili_toliqmas":
                javoblar["languages"] = new JsonObject { ["rows"] = new JsonObject { ["Russian"] = new JsonArray(1, null, 1) } };
                break;
            case "daraja_shkaladan":
                javoblar["languages"] = new JsonObject { ["rows"] = new JsonObject { ["Russian"] = new JsonArray(6, 1, 1) } };
                break;
            case "daraja_jihat_soni":
                javoblar["languages"] = new JsonObject { ["rows"] = new JsonObject { ["Russian"] = new JsonArray(1, 1) } };
                break;
            case "til_notanish":
                javoblar["languages"] = new JsonObject
                {
                    ["rows"] = new JsonObject { ["Russian"] = new JsonArray(1, 1, 1), ["Klingon"] = new JsonArray(1, 1, 1) },
                };
                break;
            case "oila_bosh":
                javoblar["familyMembers"] = new JsonArray();
                break;
            case "oila_ismsiz":
                javoblar["familyMembers"] = new JsonArray(new JsonObject { ["relation"] = "Mother", ["birthYear"] = 1975 });
                break;
            case "oila_qarindosh_notanish":
                javoblar["familyMembers"] = new JsonArray(new JsonObject { ["relation"] = "Cousin", ["fullName"] = "Karimova Nigora" });
                break;
            case "ish_joyi_yoq":
                javoblar.Remove("jobs");
                break;
            case "davr_teskari":
                javoblar["jobs"]![0]!["period"] = new JsonObject { ["from"] = "2021-03", ["to"] = "2020-01" };
                break;
            case "davr_kelajak":
                javoblar["jobs"]![0]!["period"] = new JsonObject { ["from"] = "2026-12" };
                break;
            case "oqish_yillari_teskari":
                javoblar["educations"] = new JsonArray(new JsonObject
                {
                    ["institution"] = "Qibray kolleji",
                    ["years"] = new JsonObject { ["from"] = 2019, ["to"] = 2015 },
                });
                break;
            case "rozilik_yoq":
                javoblar.Remove("consentAccuracy");
                break;
            case "rozilik_false":
                javoblar["consentPersonalData"] = false;
                break;
            case "imzo_boshqa":
                javoblar["signature"] = new JsonObject { ["name"] = "Boshqa Odam" };
                break;
            case "imzo_yoq":
                javoblar.Remove("signature");
                break;
            case "kanal_yoq":
                javoblar.Remove("referral");
                break;
            case "kanal_notanish":
                javoblar["referral"] = new JsonObject { ["channel"] = Guid.NewGuid().ToString() };
                break;
            case "toifa_takror":
                javoblar["hasDriverLicense"] = true;
                javoblar["driverLicenseCategories"] = new JsonArray("B", "B");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(holat), holat, null);
        }

        return javoblar;
    }
}
