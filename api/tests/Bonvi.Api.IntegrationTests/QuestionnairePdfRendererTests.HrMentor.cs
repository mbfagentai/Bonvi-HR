using System.Text.Json.Nodes;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HR ning HRMentor dagi matnlari PDF'da (SPEC-blankalar-hrmentor §5.3).
/// </summary>
public sealed partial class QuestionnairePdfRendererTests
{
    /// <summary>
    /// HR hamma matn va ro'yxatni asl holicha «saqlasa» — PDF bitta harfi ham o'zgarmaydi: har bir
    /// PDF matni katalogdagi asl matn bilan bir xil va ro'yxat varianti qog'ozdagi so'z bilan chiqadi.
    /// </summary>
    [Fact]
    public async Task Asl_matnlarga_teng_HR_matnlari_PDF_ni_ozgartirmaydi()
    {
        var katalog = HrF02TextCatalog.Current;

        var matnlar = new JsonObject();
        foreach (var item in katalog.Items.Values.Where(item => item.IsBlank))
        {
            matnlar[item.Key] = new JsonObject { ["uz-Latn"] = item.Default[FormTexts.Latin] };
        }

        var royxatlar = new JsonObject();
        foreach (var list in katalog.Lists.Values)
        {
            var guruhlar = new JsonObject();
            foreach (var group in list.Groups)
            {
                guruhlar[group.Id] = Tillar(group.Default);
            }

            var variantlar = new JsonArray();
            foreach (var option in list.Options)
            {
                variantlar.Add(new JsonObject { ["value"] = option.Value, ["group"] = option.Group, ["label"] = Tillar(option.Label), ["hidden"] = false });
            }

            royxatlar[list.Id] = new JsonObject { ["groups"] = guruhlar, ["options"] = variantlar };
        }

        var texts = Matnlar(new JsonObject { ["revision"] = 1, ["texts"] = matnlar, ["lists"] = royxatlar });
        texts.Blank.Count.ShouldBe(QuestionnairePdfTexts.Keys.Count, "har bir asl PDF matni tekshiruvdan o'tadi");
        texts.Lists.Count.ShouldBe(katalog.Lists.Count, "har bir ro'yxat tekshiruvdan o'tadi");

        foreach (var javoblar in new[] { QuestionnaireSamples.Full(Bugun), QuestionnaireSamples.Minimal(Bugun) })
        {
            var asl = Model(javoblar, photo: null, signature: null);

            var oldin = Sahifalar(await _renderer.RenderAsync(asl, TestContext.Current.CancellationToken));
            var keyin = Sahifalar(await _renderer.RenderAsync(asl with { Texts = texts }, TestContext.Current.CancellationToken));

            keyin.ShouldBe(oldin);
        }
    }

    [Fact]
    public async Task HR_matni_va_royxati_PDF_ga_tushadi()
    {
        var texts = Matnlar(new JsonObject
        {
            ["revision"] = 5,
            ["texts"] = new JsonObject
            {
                ["pdf.personal.title"] = new JsonObject { ["uz-Latn"] = "1. NOMZOD HAQIDA" },
                ["pdf.personal.militaryServed"] = new JsonObject { ["uz-Latn"] = "{{label}} ({{months}} oy)" },
                ["pdf.consent.intro"] = new JsonObject { ["uz-Latn"] = "Men {{name}} quyidagilarni tasdiqlayman:" },
            },
            ["lists"] = new JsonObject
            {
                // Uy turi: tartib almashdi, nomi o'zgardi
                [HrF02TextCatalog.HousingList] = new JsonObject
                {
                    ["options"] = new JsonArray(Variant("House", "Hovli uy"), Variant("Apartment", "Ko‘p qavatli uy")),
                },

                // Oilaviy holat: «Beva» yashirilgan (tanlanmagan — chiqmaydi), «Oilali» yashirilgan, lekin TANLANGAN
                [HrF02TextCatalog.MaritalList] = new JsonObject
                {
                    ["options"] = new JsonArray(
                        Variant("Married", "Oilali", hidden: true),
                        Variant("Unmarried", "Bo‘ydoq / turmush qurmagan"),
                        Variant("Divorced", "Ajrashgan"),
                        Variant("Widowed", "Beva", hidden: true)),
                },

                [HrF02TextCatalog.RelationList] = new JsonObject
                {
                    ["options"] = new JsonArray(Variant("Father", "Ota")),
                },

                // Kompyuter: HR qo'shgan dastur «Maxsus dasturlar» ustunida
                [HrF02TextCatalog.ComputerList] = new JsonObject
                {
                    ["options"] = new JsonArray(
                        Variant("windows", "Windows", group: "os"),
                        Variant("msWord", "MS Word", group: "office"),
                        Variant("x-kompas", "KOMPAS-3D", group: "special"),
                        Variant("autocad", "AutoCAD", group: "special")),
                },
            },
        });

        var tekshirilgan = Model(QuestionnaireSamples.Full(Bugun), photo: null, signature: null);
        var model = tekshirilgan with
        {
            Answers = tekshirilgan.Answers with
            {
                ComputerSkills = new Dictionary<string, int> { ["windows"] = 3, ["x-kompas"] = 4, ["msExcel"] = 2 },
            },
            Texts = texts,
        };

        var matn = Matn(await _renderer.RenderAsync(model, TestContext.Current.CancellationToken));

        matn.ShouldContain("1. NOMZOD HAQIDA");
        matn.ShouldNotContain("1. SHAXSIY MA’LUMOTLAR");
        matn.ShouldContain("☑ O‘tagan (", Case.Sensitive, "HR shabloni oy bilan");
        matn.ShouldContain("Men G‘ulomov Sardor", Case.Sensitive);
        matn.ShouldContain("quyidagilarni tasdiqlayman:");

        matn.ShouldContain("☑ Hovli uy ☐ Ko‘p qavatli uy", Case.Sensitive, "HR tartibi va nomi");
        matn.ShouldContain("☑ Oilali ☐ Bo‘ydoq / turmush qurmagan ☐ Ajrashgan", Case.Sensitive, "yashirin, lekin tanlangan — chiqadi");
        matn.ShouldNotContain("Beva", Case.Sensitive, "yashirin va tanlanmagan — chiqmaydi");

        matn.ShouldContain("Ota ", Case.Sensitive);

        matn.ShouldContain("KOMPAS-3D 4", Case.Sensitive, "HR qo'shgan dastur darajasi bilan");
        matn.ShouldContain("MS Excel 2", Case.Sensitive, "HR olib tashlagan, lekin tanlangan dastur — chiqadi");
        matn.ShouldNotContain("MS PowerPoint", Case.Sensitive, "HR olib tashlagan va tanlanmagan");
    }

    private static FormTexts Matnlar(JsonObject served)
    {
        served["code"] = CandidateForm.HrF02;
        var natija = HrF02TextCatalog.Current.Shape(served.ToJsonString());
        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.ToString() : null);

        return FormTexts.FromPayload(natija.Value.Revision, natija.Value.PayloadJson);
    }

    private static JsonObject Tillar(IReadOnlyDictionary<string, string> labels)
    {
        var node = new JsonObject();
        foreach (var (language, text) in labels)
        {
            node[language] = text;
        }

        return node;
    }

    private static JsonObject Variant(string value, string latin, string? group = null, bool hidden = false)
        => new()
        {
            ["value"] = value,
            ["group"] = group,
            ["label"] = new JsonObject { ["uz-Latn"] = latin, ["uz-Cyrl"] = latin, ["ru"] = latin },
            ["hidden"] = hidden,
        };
}
