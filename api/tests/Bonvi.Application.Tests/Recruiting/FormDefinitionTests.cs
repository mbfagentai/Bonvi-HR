using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Dynamic;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>
/// HRMentor da yig'ilgan anketa ta'rifi (SPEC-anketa-konstruktori §3–§5): urug', tekshiruvchi,
/// o'quvchi va kanonik JSON.
/// </summary>
public sealed class FormDefinitionTests
{
    /// <summary>Urug' — har chaqiruvda yangi nusxa (testlar uni o'zgartiradi).</summary>
    private static JsonObject Urug() => JsonNode.Parse(HrF02Seed.Build().ToJsonString())!.AsObject();

    private static IReadOnlyList<FormIssue> Muammolar(JsonObject taRif)
        => FormDefinitionChecker.Check(JsonNode.Parse(taRif.ToJsonString())!.AsObject(), FormProfile.ByKey);

    private static JsonObject Maydon(JsonObject taRif, string id)
        => taRif["sections"]!.AsArray()
            .SelectMany(section => section!["screens"]!.AsArray())
            .SelectMany(screen => screen!["fields"]!.AsArray())
            .Select(field => field!.AsObject())
            .First(field => field["id"]!.GetValue<string>() == id);

    private static JsonArray Maydonlar(JsonObject taRif, string ekran)
        => taRif["sections"]!.AsArray()
            .SelectMany(section => section!["screens"]!.AsArray())
            .First(screen => screen!["id"]!.GetValue<string>() == ekran)!["fields"]!.AsArray();

    // ────────────────────────────────────────────────────────────
    //  Urug'
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Urug_muammosiz_va_har_safar_bir_xil()
    {
        Muammolar(Urug()).ShouldBeEmpty();
        CanonicalJson.Sha256(HrF02Seed.Build()).ShouldBe(CanonicalJson.Sha256(HrF02Seed.Build()));
        CanonicalJson.Size(HrF02Seed.Build()).ShouldBeLessThan(FormDefinitionChecker.MaxDefinitionBytes / 2);
    }

    [Fact]
    public void Urug_qogozdagi_11_bolim_va_B_qismning_3_bolimi()
    {
        var taRif = FormDefinitionReader.Read(Urug()).Value;

        taRif.Label.ShouldBe("3.1");
        taRif.Sections.Count.ShouldBe(14);
        taRif.Sections.Count(section => !section.Office).ShouldBe(11);
        taRif.Sections.Where(section => !section.Office).Sum(section => section.Screens.Count).ShouldBe(25);
        taRif.Sections[0].PdfTitle.ShouldBe("SHAXSIY MA’LUMOTLAR");
        taRif.Sections[11].PdfTitle.ShouldBe("HUJJATLAR NAZORATI");
        taRif.Pdf.Footer!.ShouldStartWith("Saqlash:");

        var familiya = taRif.Field("lastName")!;
        familiya.Label!.Latin.ShouldBe("Familiyangiz");
        familiya.Label.Cyrillic.ShouldBe("Фамилиянгиз");
        familiya.PdfLabel.ShouldBe("Familiyasi");
        familiya.MaxLength.ShouldBe(60);
        familiya.Bind.ShouldBe(FormProfile.LastName);

        var tillar = taRif.Field("languages")!;
        tillar.Options.Select(option => option.Value).ShouldBe(["Uzbek", "Russian", "English", "Chinese"]);
        tillar.Aspects.Select(aspect => aspect.Id).ShouldBe(["speaking", "reading", "writing"]);
        (tillar.ScaleMin, tillar.ScaleMax).ShouldBe((0, 5));
        tillar.RequiredRows.ShouldBe(["Russian"]);
        tillar.Other!.Max.ShouldBe(3);

        var ishlar = taRif.Field("jobs")!;
        ishlar.VisibleIf.ShouldBe(new FormCondition("hasWorkExperience", true, null));
        ishlar.Columns.Select(column => column.Kind).ShouldBe(
            [FieldKind.ShortText, FieldKind.ShortText, FieldKind.Period, FieldKind.ShortText]);
        taRif.Field("actualAddress")!.SameAs!.Field.ShouldBe("registeredAddress");
        taRif.Field("consentAccuracy")!.Text!.Latin.ShouldStartWith("Men, {{fullName}}:");
    }

    [Fact]
    public void Urugdagi_javob_kalitlari_ornatilgan_anketaniki()
    {
        var taRif = FormDefinitionReader.Read(Urug()).Value;
        var kalitlar = typeof(QuestionnaireFormAnswers).GetProperties()
            .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
            .ToHashSet(StringComparer.Ordinal);

        // o'rnatilgan anketada boshqacha saqlanadiganlar
        string[] boshqacha = ["vacancy", "photo", "referral", "signature"];

        foreach (var maydon in taRif.CandidateFields.Where(field => field.Kind.HasAnswer() && !boshqacha.Contains(field.Id)))
        {
            kalitlar.ShouldContain(maydon.Id, $"«{maydon.Id}» o'rnatilgan anketada yo'q");
        }
    }

    [Fact]
    public void Profil_HRMentor_shaklida()
    {
        var profil = FormProfile.Json(HrF02Seed.Build());

        profil["schema"]!.GetValue<int>().ShouldBe(2);
        profil["code"]!.GetValue<string>().ShouldBe("HR-F02");
        var boglanishlar = profil["bindings"]!.AsArray();
        boglanishlar.Count.ShouldBe(FormProfile.Bindings.Count);
        boglanishlar.Select(item => item!["key"]!.GetValue<string>()).ShouldBeUnique();

        var tillar = boglanishlar.First(item => item!["key"]!.GetValue<string>() == FormProfile.Languages)!;
        tillar["scale"]!.ToJsonString().ShouldBe("""{"min":0,"max":5}""");
        tillar["kinds"]!.ToJsonString().ShouldBe("""["levels"]""");

        // HRMentor bilan yonma-yon tekshirish uchun (BONVI_PROFILE_OUT=<fayl>)
        if (Environment.GetEnvironmentVariable("BONVI_PROFILE_OUT") is { Length: > 0 } fayl)
        {
            File.WriteAllText(fayl, profil.ToJsonString());
            File.WriteAllText(fayl + ".sha256", CanonicalJson.Sha256(HrF02Seed.Build()));
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Tekshiruvchi — rad etiladigan ta'riflar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Majburiy_boglanish_yoq_bolsa_rad()
    {
        var taRif = Urug();
        var maydonlar = Maydonlar(taRif, "contacts");
        maydonlar.RemoveAt(0);

        Muammolar(taRif).ShouldBe([new FormIssue("forms.binding_missing", "sections")]);
        FormDefinitionReader.Read(taRif).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Nomalum_kalit_va_tur_rad()
    {
        var taRif = Urug();
        Maydon(taRif, "hasCar")["color"] = "red";
        Maydon(taRif, "mahalla")["kind"] = "map";

        var muammolar = Muammolar(taRif);
        muammolar.ShouldContain(new FormIssue("forms.key_unknown", "sections[0].screens[5].fields[2].color"));
        muammolar.ShouldContain(new FormIssue("forms.kind_unknown", "sections[0].screens[2].fields[3]"));

        var ildiz = Urug();
        ildiz["theme"] = "dark";
        Muammolar(ildiz).ShouldBe([new FormIssue("forms.key_unknown", "theme")]);
    }

    [Fact]
    public void Sarlavhadagi_bolim_katagi_nomi_ixtiyoriy_va_qisqa()
    {
        // Qog'oz HR-F02 v2.0 sarlavhasida «Yo'nalish» (HRMentor SPEC §19.3).
        var taRif = Urug();
        FormDefinitionReader.Read(taRif).Value.Pdf.HeadDepartment.ShouldBeNull("berilmasa — o'rnatilgan «Bo'lim»");

        taRif["pdf"]!["head_department"] = "Yo‘nalish";
        Muammolar(taRif).ShouldBeEmpty();
        FormDefinitionReader.Read(taRif).Value.Pdf.HeadDepartment.ShouldBe("Yo‘nalish");

        taRif["pdf"]!["head_department"] = new string('x', 61);
        Muammolar(taRif).ShouldBe([new FormIssue("forms.text_invalid", "pdf.head_department")]);
    }

    [Fact]
    public void Shart_va_boglanish_qoidalari()
    {
        var taRif = Urug();
        Maydon(taRif, "carModel")["visible_if"] = new JsonObject { ["field"] = "hasCar", ["in"] = new JsonArray("Yes") };
        Maydon(taRif, "phone")["visible_if"] = new JsonObject { ["field"] = "hasCar", ["is"] = true };
        Maydon(taRif, "maritalStatus")["options"]!.AsArray().Add(new JsonObject
        {
            ["value"] = "x-fuqarolik",
            ["label"] = new JsonObject { ["uz-Latn"] = "Fuqarolik nikohi" },
        });

        Muammolar(taRif).Select(issue => issue.Code).ShouldBe(
            ["forms.condition_invalid", "forms.condition_invalid", "forms.binding_values", "forms.binding_conditional"],
            ignoreOrder: true);
    }

    [Fact]
    public void Imzo_ismdan_oldin_kelsa_rad()
    {
        var taRif = Urug();
        var imzo = Maydonlar(taRif, "signature-page");
        var maydon = imzo[0]!;
        imzo.RemoveAt(0);
        imzo.Add(new JsonObject
        {
            ["id"] = "note",
            ["kind"] = "info",
            ["text"] = new JsonObject { ["uz-Latn"] = "Rahmat" },
        });
        Maydonlar(taRif, "vacancy-page").Add(maydon);

        Muammolar(taRif).ShouldBe(
        [
            new FormIssue("forms.signature_before_name", "sections[0].screens[0].fields[2]"),
            new FormIssue("forms.signature_before_name", "sections[0].screens[0].fields[2]"),
        ]);
    }

    [Fact]
    public void Matn_belgilari_va_chegaralari()
    {
        var taRif = Urug();
        Maydon(taRif, "hasCar")["label"] = new JsonObject { ["uz-Latn"] = "Avto​mobil" };
        Maydon(taRif, "mahalla")["label"] = new JsonObject { ["uz-Cyrl"] = "Маҳалла" };
        Maydon(taRif, "mahalla")["max_length"] = 301;
        Maydon(taRif, "childrenCount")["max"] = 2.5;

        Muammolar(taRif).ShouldBe(
        [
            new FormIssue("forms.text_missing", "sections[0].screens[2].fields[3].label"),
            new FormIssue("forms.range_invalid", "sections[0].screens[2].fields[3].max_length"),
            new FormIssue("forms.range_invalid", "sections[0].screens[4].fields[1].max"),
            new FormIssue("forms.text_invalid", "sections[0].screens[5].fields[2].label"),
        ]);
    }

    [Fact]
    public void Sarlavha_va_hajm_tekshiriladi()
    {
        var sxema = Urug();
        sxema["schema"] = 3;
        FormDefinitionReader.Read(sxema).Error.Code.ShouldBe("hrmentor.definition_invalid");

        var kod = Urug();
        kod["code"] = "HR-F03";
        FormDefinitionReader.Read(kod).IsFailure.ShouldBeTrue();

        var versiya = Urug();
        versiya["label"] = "4.0 beta";
        FormDefinitionReader.Read(versiya).IsFailure.ShouldBeTrue();

        FormDefinitionReader.Read("[1]").IsFailure.ShouldBeTrue();
        FormDefinitionReader.Read("""{"a":1,"a":2}""").IsFailure.ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────
    //  Kanonik JSON — HRMentor (Python json.dumps) bilan bir xil
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void Kanonik_JSON_Python_bilan_bir_xil()
    {
        var qiymat = JsonNode.Parse("""{"b":1,"a":"x\ny\"\\\u0001","ä":true,"A":[null,-5,{"z":false,"y":"G‘ulom"}],"😀":0,"￿":1}""");

        // python -c 'import json;print(json.dumps(v, ensure_ascii=False, sort_keys=True, separators=(",", ":")))'
        CanonicalJson.Write(qiymat).ShouldBe(
            "{\"A\":[null,-5,{\"y\":\"G‘ulom\",\"z\":false}],\"a\":\"x\\ny\\\"\\\\\\u0001\",\"b\":1,\"ä\":true,\"￿\":1,\"😀\":0}");
    }
}
