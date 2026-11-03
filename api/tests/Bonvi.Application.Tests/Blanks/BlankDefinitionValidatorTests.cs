using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using static Bonvi.Application.Tests.Blanks.BlankFixtures;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>Ta'rif tekshiruvi — §4.9 namunasi va §4.8 dagi har bir rad etish.</summary>
public sealed class BlankDefinitionValidatorTests
{
    [Fact]
    public void Spec_HR_F06_namunasi_otadi()
    {
        var result = BlankDefinitionValidator.Validate(HrF06, "HR-F06", "1.0");

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors));
        var definition = result.Definition!;

        definition.Code.ShouldBe("HR-F06");
        definition.Sections.Count.ShouldBe(5);
        definition.Steps.Count.ShouldBe(7);
        definition.FindField("day30Decision")!.Options[2].Extra!.Required.ShouldBeTrue();
        definition.FindStep("signMentor")!.ActorField.ShouldBe("mentor");
        definition.FindStep("day90")!.CanReturn.ShouldBeTrue("sukut — true");
        definition.FindStep("day90")!.Decision!.Options.Count.ShouldBe(2);
        definition.DraftSectionIds().ShouldBe(["hdr", "s1"], ignoreOrder: true);
        definition.StepSectionIds("day7").ShouldBe(["d7"]);
        definition.OpenSectionIds().ShouldBe(["s3"]);
        BlankPrefill.KeysOf(definition).Count.ShouldBe(5);
    }

    [Fact]
    public void Yaroqsiz_JSON()
        => Errors("{ not json").ShouldContain(error => error.Path == "$");

    [Fact]
    public void Kod_va_versiya_kutilganga_teng_bolishi_kerak()
    {
        var errors = BlankDefinitionValidator.Validate(HrF06, "HR-F07", "2.0").Errors;

        errors.ShouldContain(error => error.Path == "$.code");
        errors.ShouldContain(error => error.Path == "$.version");
    }

    [Fact]
    public void Noma_lum_kalit()
        => Errors(MutateF06(root => Field(root, 0, 1)["colour"] = "red"))
            .ShouldContain(error => error.Path == "sections[0].fields[1].colour");

    [Fact]
    public void Boshqa_turning_kaliti_ham_noma_lum()
        => Errors(MutateF06(root => Field(root, 0, 2)["maxLength"] = 10))
            .ShouldContain(error => error.Path == "sections[0].fields[2].maxLength");

    [Fact]
    public void Noma_lum_maydon_turi()
        => Errors(MutateF06(root => Field(root, 0, 1)["type"] = "colour"))
            .ShouldContain(error => error.Path == "sections[0].fields[1].type");

    [Fact]
    public void Takroriy_maydon_id_si_bo_limlar_orasida_ham()
        => Errors(MutateF06(root => Field(root, 2, 0)["id"] = "position"))
            .ShouldContain(error => error.Message.Contains("position", StringComparison.Ordinal));

    [Fact]
    public void Takroriy_qadam_id_si()
        => Errors(MutateF06(root => root["steps"]![1]!["id"] = "signEmployee"))
            .ShouldContain(error => error.Path == "steps[1].id");

    [Fact]
    public void Id_shakli()
        => Errors(MutateF06(root => Field(root, 0, 1)["id"] = "Position"))
            .ShouldContain(error => error.Path == "sections[0].fields[1].id");

    [Fact]
    public void When_noma_lum_maydonga()
        => Errors(MutateF06(root => Field(root, 0, 1)["when"] = new JsonObject { ["field"] = "nope", ["eq"] = "x" }))
            .ShouldContain(error => error.Path == "sections[0].fields[1].when");

    [Fact]
    public void Expr_noma_lum_havolaga()
        => Errors(MutateF06(root => Field(root, 2, 1)["expr"] = Expr("""{ "op": "gte", "args": [ { "ref": "nope" }, { "const": 70 } ] }""")))
            .ShouldContain(error => error.Path == "sections[2].fields[1].expr");

    [Fact]
    public void Expr_noma_lum_jadval_ustuniga()
        => Errors(MutateF06(root => Field(root, 2, 1)["expr"] = Expr("""{ "op": "count", "args": [ { "ref": "questions[*].nope" } ] }""")))
            .ShouldContain(error => error.Message.Contains("questions[*].nope", StringComparison.Ordinal));

    [Fact]
    public void Noma_lum_amal_va_argumentlar_soni()
    {
        Errors(MutateF06(root => Field(root, 2, 1)["expr"] = Expr("""{ "op": "pow", "args": [ { "const": 1 } ] }""")))
            .ShouldContain(error => error.Path == "sections[2].fields[1].expr.op");
        Errors(MutateF06(root => Field(root, 2, 1)["expr"] = Expr("""{ "op": "gte", "args": [ { "const": 1 } ] }""")))
            .ShouldContain(error => error.Path == "sections[2].fields[1].expr.args");
    }

    [Fact]
    public void Due_sana_bo_lmagan_maydonga()
        => Errors(MutateF06(root => root["steps"]![4]!["due"]!["field"] = "position"))
            .ShouldContain(error => error.Path == "steps[4].due.field");

    [Fact]
    public void DueDate_noma_lum_maydonga()
        => Errors(MutateF06(root => root["dueDate"] = new JsonObject { ["field"] = "nope" }))
            .ShouldContain(error => error.Path == "$.dueDate.field");

    [Fact]
    public void Fill_step_noma_lum_qadamga()
        => Errors(MutateF06(root => root["sections"]![2]!["fill"]!["step"] = "day8"))
            .ShouldContain(error => error.Path == "sections[2].fill.step");

    [Fact]
    public void Variant_qiymatsiz_yoki_noto_g_ri_shaklda()
    {
        Errors(MutateF06(root => Field(root, 3, 0)["options"]![0]!.AsObject().Remove("value")))
            .ShouldContain(error => error.Path == "sections[3].fields[0].options[0].value");
        Errors(MutateF06(root => Field(root, 3, 0)["options"]![0]!["value"] = "Extra-Interview"))
            .ShouldContain(error => error.Path == "sections[3].fields[0].options[0].value");
        Errors(MutateF06(root => Field(root, 3, 0)["options"]![1]!["value"] = "continue"))
            .ShouldContain(error => error.Message.Contains("Takroriy", StringComparison.Ordinal));
    }

    [Fact]
    public void Snake_case_variant_qiymati_otadi()
        => Errors(MutateF06(root => Field(root, 3, 0)["options"]![1]!["value"] = "extra_interview")).ShouldBeEmpty();

    [Fact]
    public void Jadval_ichida_jadval_bo_lmaydi()
        => Errors(MutateF06(root => Field(root, 4, 0)["columns"]!.AsArray().Add(Expr(
                """{ "id": "inner", "type": "table", "columns": [ { "id": "x", "type": "text", "label": "X" } ] }"""))))
            .ShouldContain(error => error.Path == "sections[4].fields[0].columns[3].type");

    [Fact]
    public void Noma_lum_rol()
        => Errors(MutateF06(root => root["steps"]![3]!["actor"] = "hr9"))
            .ShouldContain(error => error.Path == "steps[3].actor");

    [Fact]
    public void Field_roli_xodim_ref_maydoniga_ishora_qilishi_kerak()
    {
        Errors(MutateF06(root => root["steps"]![1]!["actor"] = "field:position"))
            .ShouldContain(error => error.Path == "steps[1].actor");
        Errors(MutateF06(root => Field(root, 0, 5)["refKind"] = "department"))
            .ShouldContain(error => error.Path == "steps[1].actor");
    }

    [Fact]
    public void Maydonlar_soni_chegarasi()
    {
        var fields = string.Join(",", Enumerable.Range(0, BlankDefinitionValidator.MaxFields + 1)
            .Select(i => $$"""{ "id": "f{{i}}", "type": "text", "label": "F" }"""));

        Errors(Single($"[{fields}]")).ShouldContain(error => error.Path == "$.sections");
    }

    [Fact]
    public void Qadamlar_soni_chegarasi()
    {
        var steps = string.Join(",", Enumerable.Range(0, BlankDefinitionValidator.MaxSteps + 1)
            .Select(i => $$"""{ "id": "s{{i}}", "title": "S", "order": 1, "actor": "hr1", "mode": "electronic" }"""));

        Errors(Single("""[ { "id": "a", "type": "text", "label": "A" } ]""", $"[{steps}]")).ShouldContain(error => error.Path == "$.steps");
    }

    [Fact]
    public void Tur_chegaralari()
    {
        Errors(Single("""[ { "id": "a", "type": "text", "label": "A", "maxLength": 501 } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "longtext", "label": "A", "maxLength": 4001 } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "money", "label": "A", "max": 1000000000001 } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "table", "maxRows": 201, "columns": [ { "id": "x", "type": "text", "label": "X" } ] } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "file", "label": "A", "accept": ["exe"] } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "file", "label": "A", "accept": ["pdf"], "maxFiles": 21 } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "date", "label": "A", "max": "tomorrow" } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "a", "type": "date", "label": "A", "min": "today-30", "max": "today+7" } ]""")).ShouldBeEmpty();
    }

    [Fact]
    public void Noma_lum_prefill_manbasi()
        => Errors(MutateF06(root => Field(root, 0, 1)["prefill"] = "subject.salary"))
            .ShouldContain(error => error.Path == "sections[0].fields[1].prefill");

    [Fact]
    public void Static_o_rinbosari_noma_lum_maydonga()
    {
        Errors(Single("""[ { "id": "a", "type": "static", "text": "Men {{nope}} tanishdim" } ]""")).ShouldNotBeEmpty();
        Errors(Single("""[ { "id": "n", "type": "text", "label": "N" }, { "id": "a", "type": "static", "text": "Men {{n}} tanishdim" } ]""")).ShouldBeEmpty();
    }

    [Fact]
    public void Bandlar_kamayish_tartibida()
        => Errors(Single("""
            [ { "id": "s", "type": "int", "label": "S" },
              { "id": "b", "type": "computed", "label": "B", "valueType": "text",
                "expr": { "op": "band", "value": { "ref": "s" }, "bands": [ { "from": 3.0, "label": "x" }, { "from": 3.5, "label": "y" } ] } } ]
            """)).ShouldNotBeEmpty();

    // ── Orkestrator qarori 2: oldinga havola ruxsat, tsikl — yo'q ──

    [Fact]
    public void Oldinga_havolalar_ruxsat_etiladi()
    {
        var errors = Errors(Single("""
            [ { "id": "total", "type": "computed", "label": "Jami", "valueType": "number", "expr": { "op": "sum", "args": [ { "ref": "k[*].w" } ] } },
              { "id": "note", "type": "text", "label": "Izoh", "when": { "field": "kind", "eq": "other" } },
              { "id": "kind", "type": "choice", "label": "Tur", "options": [ { "value": "other", "label": "Boshqa" } ] },
              { "id": "k", "type": "table", "columns": [ { "id": "w", "type": "int", "label": "Vazn" } ] } ]
            """));

        errors.ShouldBeEmpty(string.Join("; ", errors));
    }

    [Fact]
    public void Computed_tsikli_rad_etiladi()
        => Errors(Single("""
            [ { "id": "a", "type": "computed", "label": "A", "valueType": "number", "expr": { "op": "add", "args": [ { "ref": "b" }, { "const": 1 } ] } },
              { "id": "b", "type": "computed", "label": "B", "valueType": "number", "expr": { "op": "add", "args": [ { "ref": "a" }, { "const": 1 } ] } } ]
            """)).ShouldContain(error => error.Message.StartsWith("Tsikl", StringComparison.Ordinal));

    [Fact]
    public void Shart_tsikli_rad_etiladi()
        => Errors(Single("""
            [ { "id": "a", "type": "text", "label": "A", "when": { "field": "b", "filled": true } },
              { "id": "b", "type": "text", "label": "B", "when": { "field": "a", "filled": true } } ]
            """)).ShouldContain(error => error.Message.StartsWith("Tsikl", StringComparison.Ordinal));

    [Fact]
    public void Shart_va_computed_aralash_tsikli_rad_etiladi()
        => Errors(Single("""
            [ { "id": "a", "type": "int", "label": "A", "when": { "field": "c", "eq": true } },
              { "id": "c", "type": "computed", "label": "C", "valueType": "bool", "expr": { "op": "gte", "args": [ { "ref": "a" }, { "const": 1 } ] } } ]
            """)).ShouldContain(error => error.Message.StartsWith("Tsikl", StringComparison.Ordinal));

    [Fact]
    public void Bolim_sharti_o_z_maydoniga_tayansa_tsikl()
    {
        var json = MutateF06(root => root["sections"]![0]!["when"] = new JsonObject { ["field"] = "position", ["filled"] = true });

        Errors(json).ShouldContain(error => error.Message.StartsWith("Tsikl", StringComparison.Ordinal));
    }

    [Fact]
    public void Ustun_o_z_qatoridagi_ustunga_tayanadi_tsikl_bo_lsa_rad()
    {
        Errors(Single("""
            [ { "id": "t", "type": "table", "columns": [
                { "id": "p", "type": "percent", "label": "P" },
                { "id": "ok", "type": "computed", "label": "OK", "valueType": "bool", "expr": { "op": "gte", "args": [ { "ref": "p" }, { "const": 70 } ] } } ] } ]
            """)).ShouldBeEmpty();

        Errors(Single("""
            [ { "id": "t", "type": "table", "columns": [
                { "id": "x", "type": "computed", "label": "X", "valueType": "number", "expr": { "op": "add", "args": [ { "ref": "y" }, { "const": 1 } ] } },
                { "id": "y", "type": "computed", "label": "Y", "valueType": "number", "expr": { "op": "add", "args": [ { "ref": "x" }, { "const": 1 } ] } } ] } ]
            """)).ShouldContain(error => error.Message.StartsWith("Tsikl", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_yaroqsiz_ta_rifda_istisno()
        => Should.Throw<InvalidOperationException>(() => BlankDefinitionValidator.Load("{}"));

    private static IReadOnlyList<BlankDefinitionError> Errors(string json) => BlankDefinitionValidator.Validate(json).Errors;

    private static JsonNode Expr(string json) => JsonNode.Parse(json)!;
}
