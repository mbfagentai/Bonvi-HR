using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>
/// 1-to'lqin shablonlari (HR-F06, F07, F08, F09, F13) — seeder ularni xuddi shu tekshiruvdan o'tkazadi,
/// shuning uchun bitta xato ham ishga tushishda shablonni yo'qotadi (SPEC-blankalar §4.8, §14 S3).
/// </summary>
public sealed class WaveOneTemplatesTests
{
    private static readonly string[] Codes = ["HR-F06", "HR-F07", "HR-F08", "HR-F09", "HR-F13"];

    private static string TemplatesDirectory => Path.Combine(AppContext.BaseDirectory, "Blanks", "Templates");

    public static TheoryData<string> WaveOne() => new(Codes);

    [Fact]
    public void Papkada_aynan_1_tolqin_shablonlari_bor()
        => Directory.GetFiles(TemplatesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ShouldBe(Codes, ignoreOrder: true);

    [Theory]
    [MemberData(nameof(WaveOne))]
    public void Shablon_tekshiruvdan_xatosiz_otadi(string code)
    {
        var json = File.ReadAllText(Path.Combine(TemplatesDirectory, code + ".json"));

        var result = BlankDefinitionValidator.Validate(json, code, "1.0");

        result.Errors.ShouldBeEmpty(string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Path}: {error.Message}")));
        result.Definition!.Code.ShouldBe(code);
        result.Definition.Steps.ShouldNotBeEmpty();
    }

    [Fact]
    public void HR_F07_test_otishi_har_qatorning_oz_foizidan_hisoblanadi()
    {
        // §4.11.1: ustun ichidagi `{"ref":"testPercent"}` — SHU qatorning ustuni
        var definition = Load("HR-F07");
        var answers = new JsonObject
        {
            ["testResults"] = new JsonArray(
                new JsonObject { ["_row"] = "d7", ["testPercent"] = 80 },
                new JsonObject { ["_row"] = "d85", ["testPercent"] = 60 }),
        };

        var computed = BlankComputedEvaluator.ComputeAll(definition, answers);

        computed.ToJsonString().ShouldContain("\"testResults[0].testPassed\":true");
        computed.ToJsonString().ShouldContain("\"testResults[1].testPassed\":false");
    }

    [Fact]
    public void HR_F13_guvohlar_tushuntirish_berilsa_otkazib_yuboriladi()
    {
        // §4.11.3: qadam sharti guruhi navbatga kelganda baholanadi
        var definition = Load("HR-F13");

        BlankConditions.EvaluateStepCondition(definition, [], null, "witness1").ShouldBeTrue();
        BlankConditions.EvaluateStepCondition(definition, new JsonObject { ["explanation"] = "Kechikdim, avtobus." }, null, "witness2")
            .ShouldBeFalse();
    }

    private static BlankDefinition Load(string code)
        => BlankDefinitionValidator.Load(File.ReadAllText(Path.Combine(TemplatesDirectory, code + ".json")));
}
