using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Domain.Common;
using static Bonvi.Application.Tests.Blanks.BlankFixtures;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>Javoblar tekshiruvi: har tur, chegaralar, variant qo'shimchasi, jadvallar, yashirin maydonlar, computed.</summary>
public sealed class BlankAnswersValidatorTests
{
    // ── F06: to'liq yurish ──

    [Fact]
    public void F06_qoralama_tozalanadi_va_computed_hisoblanadi()
    {
        var outcome = Ok(F06(), $$"""
            { "employee": { "id": "{{MentorId}}", "label": "  Ali Valiyev " }, "position": "  Operator ",
              "hireDate": "2026-11-03", "mentor": { "id": "{{MentorId}}" }, "day7Percent": 72.5,
              "day7Pass": false, "duties": "x", "day30Decision": "continue",
              "questions": [ { "date": "2026-11-05", "question": "Qayerda?" }, { "date": null, "question": "" } ] }
            """, Options("hdr", "s1"));

        outcome.Answers["position"]!.GetValue<string>().ShouldBe("Operator");
        outcome.Answers["employee"]!["label"]!.GetValue<string>().ShouldBe("Ali Valiyev");
        outcome.Answers.ContainsKey("day7Pass").ShouldBeFalse("computed javobga yozilmaydi");
        outcome.Answers.ContainsKey("duties").ShouldBeFalse("static saqlanmaydi");
        outcome.Answers["day30Decision"]!["value"]!.GetValue<string>().ShouldBe("continue");
        outcome.Answers["questions"]!.AsArray().Count.ShouldBe(1, "bo'sh qator tashlanadi");
        outcome.Computed["day7Pass"]!.GetValue<bool>().ShouldBeTrue();
        outcome.References.Count(reference => reference.Kind == "employee").ShouldBe(2);
    }

    [Fact]
    public void Majburiylik_faqat_berilgan_bo_limlarda()
    {
        Ok(F06(), "{}", Options());
        Fail(F06(), "{}", Options("hdr")).ShouldBe(("blanka.majburiy", "hireDate"));
        Fail(F06(), """{ "hireDate": "2026-11-03", "mentor": { "id": "0199f0a0-5e1d-7c00-8000-000000000011" } }""", Options("hdr", "d7"))
            .ShouldBe(("blanka.majburiy", "day7Percent"));
    }

    [Fact]
    public void Variant_qo_shimchasi_majburiy_va_tanlanmagan_variantda_tashlanadi()
    {
        Fail(F06(), """{ "day30Decision": { "value": "stop" } }""", Options("d30")).ShouldBe(("blanka.majburiy", "day30Decision.extra"));
        Ok(F06(), """{ "day30Decision": { "value": "stop" } }""", Options());

        var kept = Ok(F06(), """{ "day30Decision": { "value": "stop", "extra": " Kech qoladi " } }""", Options("d30"));
        kept.Answers["day30Decision"]!["extra"]!.GetValue<string>().ShouldBe("Kech qoladi");

        var dropped = Ok(F06(), """{ "day30Decision": { "value": "continue", "extra": "x" } }""", Options("d30"));
        dropped.Answers["day30Decision"]!.AsObject().ContainsKey("extra").ShouldBeFalse();
    }

    [Fact]
    public void Noma_lum_variant_va_noma_lum_kalit()
    {
        Fail(F06(), """{ "day30Decision": { "value": "pause" } }""", Options()).ShouldBe(("blanka.notogri", "day30Decision"));
        Fail(F06(), """{ "colour": "red" }""", Options()).ShouldBe(("blanka.notogri", "colour"));
        Validate(F06(), "[]", Options()).Error.Code.ShouldBe("blanka.json");
    }

    // ── Turlar ──

    [Fact]
    public void Matn_turlari()
    {
        var definition = Load("""[ { "id": "a", "type": "text", "label": "A", "maxLength": 5 }, { "id": "b", "type": "longtext", "label": "B" } ]""");

        Fail(definition, """{ "a": "123456" }""", Options()).ShouldBe(("blanka.notogri", "a"));
        Fail(definition, """{ "a": "a\nb" }""", Options()).ShouldBe(("blanka.notogri", "a"));
        Fail(definition, """{ "a": "x‮y" }""", Options()).ShouldBe(("blanka.notogri", "a"));
        Fail(definition, """{ "a": 5 }""", Options()).ShouldBe(("blanka.notogri", "a"));
        Ok(definition, """{ "b": "bir\nikki" }""", Options()).Answers["b"]!.GetValue<string>().ShouldBe("bir\nikki");
        Ok(definition, """{ "a": "   " }""", Options()).Answers.ContainsKey("a").ShouldBeFalse();
    }

    [Fact]
    public void Son_turlari()
    {
        var definition = Load("""
            [ { "id": "i", "type": "int", "label": "I", "min": 0, "max": 10 }, { "id": "p", "type": "percent", "label": "P" },
              { "id": "m", "type": "money", "label": "M" }, { "id": "s", "type": "score", "label": "S" }, { "id": "y", "type": "year", "label": "Y" } ]
            """);

        Fail(definition, """{ "i": 11 }""", Options()).ShouldBe(("blanka.notogri", "i"));
        Fail(definition, """{ "i": 1.5 }""", Options()).ShouldBe(("blanka.notogri", "i"));
        Fail(definition, """{ "p": 72.55 }""", Options()).ShouldBe(("blanka.notogri", "p"));
        Fail(definition, """{ "p": 101 }""", Options()).ShouldBe(("blanka.notogri", "p"));
        Fail(definition, """{ "m": -1 }""", Options()).ShouldBe(("blanka.notogri", "m"));
        Fail(definition, """{ "m": 1000000000001 }""", Options()).ShouldBe(("blanka.notogri", "m"));
        Fail(definition, """{ "s": 6 }""", Options()).ShouldBe(("blanka.notogri", "s"));
        Fail(definition, """{ "y": 1800 }""", Options()).ShouldBe(("blanka.notogri", "y"));
        Fail(definition, """{ "i": "5" }""", Options()).ShouldBe(("blanka.notogri", "i"));

        var outcome = Ok(definition, """{ "i": 10, "p": 72.5, "m": 1500000, "s": 1, "y": 2026 }""", Options());
        outcome.Answers["p"]!.GetValue<decimal>().ShouldBe(72.5m);
        outcome.Answers["m"]!.GetValue<long>().ShouldBe(1_500_000);
    }

    [Fact]
    public void Sana_turlari()
    {
        var definition = Load("""
            [ { "id": "d", "type": "date", "label": "D", "max": "today" }, { "id": "dt", "type": "datetime", "label": "DT", "min": "today-7" },
              { "id": "mo", "type": "month", "label": "MO", "max": "today" }, { "id": "r", "type": "dateRange", "label": "R" } ]
            """);

        Fail(definition, """{ "d": "2026-11-04" }""", Options()).ShouldBe(("blanka.notogri", "d"));
        Fail(definition, """{ "d": "03.11.2026" }""", Options()).ShouldBe(("blanka.notogri", "d"));
        Fail(definition, """{ "dt": "2026-11-03T10:00:00" }""", Options()).ShouldBe(("blanka.notogri", "dt"));
        Fail(definition, """{ "dt": "2026-10-20T10:00:00+05:00" }""", Options()).ShouldBe(("blanka.notogri", "dt"));
        Fail(definition, """{ "mo": "2026-12" }""", Options()).ShouldBe(("blanka.notogri", "mo"));
        Fail(definition, """{ "mo": "2026-13" }""", Options()).ShouldBe(("blanka.notogri", "mo"));
        Fail(definition, """{ "r": { "from": "2026-11-05", "to": "2026-11-01" } }""", Options()).ShouldBe(("blanka.notogri", "r.to"));

        Ok(definition, """{ "d": "2026-11-03", "dt": "2026-11-03T10:00:00+05:00", "mo": "2026-11", "r": { "from": "2026-11-01", "to": "2026-11-05" } }""", Options());
    }

    [Fact]
    public void Yesno_va_check()
    {
        var definition = Load("""[ { "id": "y", "type": "yesno", "label": "Y", "required": true }, { "id": "c", "type": "check", "label": "C", "required": true } ]""");

        Fail(definition, """{ "y": "ha", "c": true }""", Options()).ShouldBe(("blanka.notogri", "y"));
        Fail(definition, """{ "y": false, "c": false }""", Options("main")).ShouldBe(("blanka.majburiy", "c"));
        Ok(definition, """{ "y": false, "c": true }""", Options("main"));
    }

    [Fact]
    public void Multi_tanlov()
    {
        var definition = Load("""
            [ { "id": "r", "type": "multi", "label": "R", "maxSelected": 2, "options": [
                { "value": "k01", "label": "1" }, { "value": "k02", "label": "2" }, { "value": "k06", "label": "6", "extra": { "type": "text", "label": "Qayer", "required": true } } ] } ]
            """);

        Fail(definition, """{ "r": ["k01", "k02", "k06"] }""", Options()).ShouldBe(("blanka.notogri", "r"));
        Fail(definition, """{ "r": ["k01", "k01"] }""", Options()).ShouldBe(("blanka.notogri", "r[1]"));
        Fail(definition, """{ "r": [{ "value": "k06" }] }""", Options("main")).ShouldBe(("blanka.majburiy", "r[0].extra"));

        var outcome = Ok(definition, """{ "r": ["k01", { "value": "k06", "extra": "Toshkent" }] }""", Options("main"));
        outcome.Answers["r"]!.ToJsonString().ShouldBe("""[{"value":"k01"},{"value":"k06","extra":"Toshkent"}]""");
    }

    [Fact]
    public void Havola_turlari()
    {
        var definition = Load("""
            [ { "id": "dep", "type": "ref", "refKind": "department", "label": "D" },
              { "id": "f06", "type": "docRef", "label": "F06", "templateCodes": ["HR-F06"] },
              { "id": "ord", "type": "order", "label": "Buyruq", "required": true },
              { "id": "ev", "type": "file", "label": "Dalil", "accept": ["image", "pdf"], "maxFiles": 2 } ]
            """);
        const string Id1 = "0199f0a0-5e1d-7c00-8000-000000000001";
        const string Id2 = "0199f0a0-5e1d-7c00-8000-000000000002";
        const string Id3 = "0199f0a0-5e1d-7c00-8000-000000000003";

        Fail(definition, """{ "dep": { "id": "x" } }""", Options()).ShouldBe(("blanka.notogri", "dep.id"));
        Fail(definition, $$"""{ "f06": { "submissionId": "{{Id1}}", "templateCode": "HR-F07" } }""", Options()).ShouldBe(("blanka.notogri", "f06.templateCode"));
        Fail(definition, """{ "ord": { "number": "12-K" } }""", Options("main")).ShouldBe(("blanka.majburiy", "ord"));
        Fail(definition, $$"""{ "ev": ["{{Id1}}", "{{Id2}}", "{{Id3}}"] }""", Options()).ShouldBe(("blanka.notogri", "ev"));
        Fail(definition, $$"""{ "ev": ["{{Id1}}", "{{Id1}}"] }""", Options()).ShouldBe(("blanka.notogri", "ev[1]"));

        var outcome = Ok(definition, $$"""
            { "dep": { "id": "{{Id1}}", "label": "Ombor" }, "f06": { "submissionId": "{{Id2}}", "templateCode": "HR-F06", "number": "F06-0001" },
              "ord": { "number": "12-K", "date": "2026-11-01" }, "ev": ["{{Id3}}"] }
            """, Options("main"));

        outcome.References.ShouldBe(
        [
            new BlankAnswerReference("dep", "department", Guid.Parse(Id1)),
            new BlankAnswerReference("f06", "docRef", Guid.Parse(Id2), "HR-F06"),
            new BlankAnswerReference("ev", "file", Guid.Parse(Id3)),
        ]);
    }

    // ── Jadvallar ──

    [Fact]
    public void Erkin_jadval_maxRows_va_minRows()
    {
        var definition = Load("""
            [ { "id": "refs", "type": "table", "required": true, "minRows": 2, "maxRows": 3, "columns": [
                { "id": "name", "type": "text", "label": "Ism", "required": true }, { "id": "phone", "type": "text", "label": "Tel" } ] } ]
            """);

        Fail(definition, """{ "refs": [ {"name":"a"}, {"name":"b"}, {"name":"c"}, {"name":"d"} ] }""", Options()).ShouldBe(("blanka.notogri", "refs"));
        Fail(definition, """{ "refs": [ {"name":"a"} ] }""", Options("main")).ShouldBe(("blanka.majburiy", "refs"));
        Fail(definition, """{ "refs": [ {"name":"a"}, {"phone":"90"} ] }""", Options("main")).ShouldBe(("blanka.majburiy", "refs[1].name"));
        Fail(definition, """{ "refs": [ {"name":"a", "age": 3} ] }""", Options()).ShouldBe(("blanka.notogri", "refs[0].age"));
        Ok(definition, """{ "refs": [ {"name":"a"}, {"name":"b"} ] }""", Options("main"));
    }

    private const string FixedTable = """
        [ { "id": "tests", "type": "table", "fixedRows": [
              { "id": "d7", "label": "7-kun", "cells": { "note": { "readOnly": true, "text": "HR-4" } } },
              { "id": "d85", "label": "85-kun" } ],
            "columns": [
              { "id": "date", "type": "date", "label": "Sana" },
              { "id": "testPercent", "type": "percent", "label": "Natija (%)", "required": true },
              { "id": "note", "type": "text", "label": "Izoh" },
              { "id": "passed", "type": "computed", "label": "O'tdi", "valueType": "bool",
                "expr": { "op": "gte", "args": [ { "ref": "testPercent" }, { "ref": "threshold" } ] } } ] },
          { "id": "threshold", "type": "int", "label": "Chegara" },
          { "id": "best", "type": "computed", "label": "Eng yaxshi", "valueType": "number", "expr": { "op": "max", "args": [ { "ref": "tests[*].testPercent" } ] } },
          { "id": "allPassed", "type": "computed", "label": "Hammasi", "valueType": "number", "expr": { "op": "count", "args": [ { "ref": "tests[*].passed" } ] } } ]
        """;

    [Fact]
    public void Qat_iy_qatorli_jadval()
    {
        var definition = Load(FixedTable);

        Fail(definition, """{ "tests": [ {} ] }""", Options()).ShouldBe(("blanka.notogri", "tests"));
        Fail(definition, """{ "tests": [ { "_row": "d85" }, { "_row": "d7" } ] }""", Options()).ShouldBe(("blanka.notogri", "tests[0]._row"));
        Fail(definition, """{ "tests": [ { "testPercent": 80 }, {} ] }""", Options("main")).ShouldBe(("blanka.majburiy", "tests[1].testPercent"));

        var outcome = Ok(definition, """{ "tests": [ { "testPercent": 80, "note": "yozib bo'lmaydi" }, { "testPercent": 60 } ] }""", Options());
        outcome.Answers["tests"]!.ToJsonString().ShouldBe("""[{"_row":"d7","testPercent":80},{"_row":"d85","testPercent":60}]""");
    }

    [Fact]
    public void Ustun_havolasi_avval_o_z_qatoriga_keyin_yuqori_maydonga()
    {
        // Orkestrator qarori 1: testPercent — shu qatordan, threshold — yuqori darajadan
        var outcome = Ok(Load(FixedTable), """{ "threshold": 70, "tests": [ { "testPercent": 80 }, { "testPercent": 60 } ] }""", Options());

        outcome.Computed["tests[0].passed"]!.GetValue<bool>().ShouldBeTrue();
        outcome.Computed["tests[1].passed"]!.GetValue<bool>().ShouldBeFalse();
        outcome.Computed["best"]!.GetValue<decimal>().ShouldBe(80m);
        outcome.Computed["allPassed"]!.GetValue<decimal>().ShouldBe(2m);
    }

    [Fact]
    public void Ustun_sharti_o_z_qatoriga_qaraydi()
    {
        var definition = Load("""
            [ { "id": "kind", "type": "text", "label": "Yuqori" },
              { "id": "t", "type": "table", "columns": [
                { "id": "kind", "type": "choice", "label": "Tur", "options": [ { "value": "other", "label": "Boshqa" }, { "value": "a", "label": "A" } ] },
                { "id": "detail", "type": "text", "label": "Tafsilot", "required": true, "when": { "field": "kind", "eq": "other" } } ] } ]
            """);

        var outcome = Ok(definition, """{ "kind": "other", "t": [ { "kind": "a", "detail": "tashlanadi" }, { "kind": "other", "detail": "qoladi" } ] }""", Options());

        outcome.Answers["t"]!.ToJsonString().ShouldBe("""[{"kind":{"value":"a"}},{"kind":{"value":"other"},"detail":"qoladi"}]""");
        Fail(definition, """{ "t": [ { "kind": "other" } ] }""", Options("main")).ShouldBe(("blanka.majburiy", "t[0].detail"));
    }

    // ── Yashirin maydonlar ──

    [Fact]
    public void Yashirin_maydon_tashlanadi_tekshirilmaydi_va_majburiy_emas()
    {
        var definition = Load("""
            [ { "id": "initiative", "type": "choice", "label": "Tashabbus", "options": [ { "value": "employee", "label": "Xodim" }, { "value": "company", "label": "Korxona" } ] },
              { "id": "q1", "type": "int", "label": "Q1", "required": true, "when": { "field": "initiative", "eq": "employee" } } ]
            """);

        var outcome = Ok(definition, """{ "initiative": "company", "q1": "noto'g'ri tur" }""", Options("main"));
        outcome.Answers.ContainsKey("q1").ShouldBeFalse();

        Fail(definition, """{ "initiative": "employee" }""", Options("main")).ShouldBe(("blanka.majburiy", "q1"));
    }

    [Fact]
    public void Oldinga_qaragan_shart_va_yashirin_bo_lim()
    {
        // Bo'lim sharti keyingi bo'limdagi maydonga qaraydi (orkestrator qarori 2)
        var definition = BlankDefinitionValidator.Load("""
            { "schema": 1, "code": "T", "version": "1", "title": "T", "subject": "employee", "sections": [
              { "id": "q", "title": "Savollar", "when": { "field": "consent", "eq": true }, "fields": [
                { "id": "answer", "type": "text", "label": "Javob", "required": true } ] },
              { "id": "c", "title": "Rozilik", "fields": [ { "id": "consent", "type": "yesno", "label": "Rozimi" } ] } ] }
            """);

        Ok(definition, """{ "answer": "bor", "consent": false }""", Options("q")).Answers.ContainsKey("answer").ShouldBeFalse();
        Fail(definition, """{ "consent": true }""", Options("q")).ShouldBe(("blanka.majburiy", "answer"));
    }

    [Fact]
    public void Shart_computed_qiymatga_qarashi_mumkin()
    {
        var definition = Load("""
            [ { "id": "p", "type": "percent", "label": "P" },
              { "id": "pass", "type": "computed", "label": "O'tdi", "valueType": "bool", "expr": { "op": "gte", "args": [ { "ref": "p" }, { "const": 70 } ] } },
              { "id": "plan", "type": "longtext", "label": "Reja", "when": { "field": "pass", "eq": false } } ]
            """);

        Ok(definition, """{ "p": 80, "plan": "x" }""", Options()).Answers.ContainsKey("plan").ShouldBeFalse();
        Ok(definition, """{ "p": 50, "plan": "x" }""", Options()).Answers.ContainsKey("plan").ShouldBeTrue();
    }

    [Fact]
    public void O_zgargan_maydonlar()
    {
        var changed = BlankAnswersValidator.ChangedFields(Json("""{ "a": 1, "b": { "x": 1 }, "c": "z" }"""), Json("""{ "a": 1, "b": { "x": 2 }, "d": true }"""));

        changed.ShouldBe(["b", "c", "d"], ignoreOrder: true);
    }

    // ── Yordamchilar ──

    private static Result<BlankAnswersOutcome> Validate(BlankDefinition definition, string json, BlankAnswersOptions options)
        => BlankAnswersValidator.Validate(definition, JsonNode.Parse(json), options);

    private static BlankAnswersOutcome Ok(BlankDefinition definition, string json, BlankAnswersOptions options)
    {
        var result = Validate(definition, json, options);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? $"{result.Error.Code} {result.Error.Message}" : null);
        return result.Value;
    }

    private static (string Code, string Path) Fail(BlankDefinition definition, string json, BlankAnswersOptions options)
    {
        var result = Validate(definition, json, options);
        result.IsFailure.ShouldBeTrue("xato kutilgan edi");
        return (result.Error.Code, result.Error.Message);
    }
}
