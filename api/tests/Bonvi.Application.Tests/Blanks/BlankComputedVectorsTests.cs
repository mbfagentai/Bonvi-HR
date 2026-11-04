using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>
/// Umumiy vektorlar (<c>computed-vectors.json</c>): shartlar va computed ifodalar. Veb (TS) baholovchisi
/// ham aynan shu faylni o'tishi shart (SPEC-blankalar §4.5).
/// </summary>
public sealed class BlankComputedVectorsTests
{
    private static readonly Lazy<JsonObject> Vectors = new(() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Blanks", "computed-vectors.json")))!.AsObject());

    public static TheoryData<string> ConditionCases() => Names("conditions");

    public static TheoryData<string> ComputedCases() => Names("computed");

    [Theory]
    [MemberData(nameof(ConditionCases))]
    public void Shart_vektori(string name)
    {
        var vector = Find("conditions", name);
        var condition = BlankConditions.Parse(vector["when"]);

        BlankConditions.Evaluate(condition, vector["answers"]!.AsObject())
            .ShouldBe(vector["expected"]!.GetValue<bool>(), name);
    }

    [Theory]
    [MemberData(nameof(ComputedCases))]
    public void Computed_vektori(string name)
    {
        var vector = Find("computed", name);
        var expr = BlankComputedEvaluator.Parse(vector["expr"]);

        var actual = BlankComputedEvaluator.Evaluate(expr, vector["answers"]!.AsObject());
        var expected = vector["expected"];

        switch (expected?.GetValueKind())
        {
            case null or JsonValueKind.Null:
                actual.ShouldBeNull(name);
                break;
            case JsonValueKind.Number:
                actual.ShouldNotBeNull(name);
                ((double)actual.GetValue<decimal>()).ShouldBe(expected.GetValue<double>(), 1e-9, name);
                break;
            case JsonValueKind.True or JsonValueKind.False:
                actual.ShouldNotBeNull(name);
                actual.GetValue<bool>().ShouldBe(expected.GetValue<bool>(), name);
                break;
            default:
                actual.ShouldNotBeNull(name);
                actual.GetValue<string>().ShouldBe(expected.GetValue<string>(), name);
                break;
        }
    }

    [Fact]
    public void Vektor_nomlari_takrorlanmaydi()
    {
        foreach (var group in new[] { "conditions", "computed" })
        {
            var names = Vectors.Value[group]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ToList();
            names.Count.ShouldBe(names.Distinct(StringComparer.Ordinal).Count(), group);
            names.Count.ShouldBeGreaterThan(10, group);
        }
    }

    private static TheoryData<string> Names(string group)
    {
        var data = new TheoryData<string>();

        foreach (var item in Vectors.Value[group]!.AsArray())
        {
            data.Add(item!["name"]!.GetValue<string>());
        }

        return data;
    }

    private static JsonObject Find(string group, string name)
        => Vectors.Value[group]!.AsArray()
            .Single(item => string.Equals(item!["name"]!.GetValue<string>(), name, StringComparison.Ordinal))!
            .AsObject();

    [Fact]
    public void Ish_kuni_dushanbadan_jumagacha()
    {
        // 2026-11-03 — seshanba: vektorlardagi sanalar shunga tayanadi
        DateOnly.Parse("2026-11-03", CultureInfo.InvariantCulture).DayOfWeek.ShouldBe(DayOfWeek.Tuesday);
    }
}
