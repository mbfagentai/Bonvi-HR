using Bonvi.Application.Blanks.Definitions;
using static Bonvi.Application.Tests.Blanks.BlankFixtures;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>To'liqlik (§5.3), qadam sharti (dangasa) va oldindan to'ldirish.</summary>
public sealed class BlankCompletenessTests
{
    [Fact]
    public void Bo_limlar_doirasi()
    {
        var definition = F06();

        BlankCompleteness.SectionsInScope(definition, [], lastGroupOpen: false).ShouldBe(["hdr", "s1"], ignoreOrder: true);
        BlankCompleteness.SectionsInScope(definition, ["signHr4", "day7"], lastGroupOpen: false).ShouldBe(["hdr", "s1", "d7"], ignoreOrder: true);
        BlankCompleteness.SectionsInScope(definition, ["day7", "day30", "day90"], lastGroupOpen: true)
            .ShouldBe(["hdr", "s1", "d7", "d30", "s3"], ignoreOrder: true);
    }

    [Fact]
    public void Missing_ro_yxati_hujjat_tartibida()
    {
        var definition = F06();
        var scope = BlankCompleteness.SectionsInScope(definition, ["day7", "day30"], lastGroupOpen: false);

        BlankCompleteness.Missing(definition, [], scope).ShouldBe(["hireDate", "mentor", "day7Percent", "day30Decision"]);

        var answers = Json($$"""{ "hireDate": "2026-11-03", "mentor": { "id": "{{MentorId}}" }, "day7Percent": 80, "day30Decision": { "value": "stop" } }""");
        BlankCompleteness.Missing(definition, answers, scope).ShouldBe(["day30Decision.extra"]);
    }

    [Fact]
    public void Missing_yumshoq_eski_qiymat_to_ldirilgan_hisoblanadi()
    {
        var definition = Load("""[ { "id": "d", "type": "date", "label": "D", "required": true, "max": "today" } ]""");

        BlankCompleteness.Missing(definition, Json("""{ "d": "2999-01-01" }"""), new HashSet<string> { "main" }).ShouldBeEmpty();
    }

    [Fact]
    public void Missing_jadval_minRows_dan_kam()
    {
        var definition = Load("""[ { "id": "t", "type": "table", "required": true, "minRows": 2, "columns": [ { "id": "x", "type": "text", "label": "X" } ] } ]""");

        BlankCompleteness.Missing(definition, Json("""{ "t": [ { "x": "a" } ] }"""), new HashSet<string> { "main" }).ShouldBe(["t"]);
        BlankCompleteness.Missing(definition, Json("""{ "t": [ { "x": "a" }, { "x": "b" } ] }"""), new HashSet<string> { "main" }).ShouldBeEmpty();
    }

    [Fact]
    public void Qadam_sharti_istalgan_payt_baholanadi()
    {
        // HR-F13: guvohlar qadami faqat tushuntirish xati bo'sh bo'lsa
        var definition = Load(
            """[ { "id": "explanation", "type": "longtext", "label": "Tushuntirish" }, { "id": "late", "type": "computed", "label": "L", "valueType": "bool", "expr": { "const": true } } ]""",
            """
            [ { "id": "witness1", "title": "Guvoh", "order": 3, "actor": "witness", "mode": "inPerson", "when": { "field": "explanation", "filled": false } },
              { "id": "hr1", "title": "HR-1", "order": 5, "actor": "hr1", "mode": "electronic", "when": { "field": "late", "eq": true } },
              { "id": "hr2", "title": "HR-2", "order": 6, "actor": "hr2", "mode": "electronic" } ]
            """);

        BlankConditions.EvaluateStepCondition(definition, [], null, "witness1").ShouldBeTrue();
        BlankConditions.EvaluateStepCondition(definition, Json("""{ "explanation": "Kechikdim" }"""), null, "witness1").ShouldBeFalse();
        BlankConditions.EvaluateStepCondition(definition, [], Json("""{ "late": true }"""), "hr1").ShouldBeTrue();
        BlankConditions.EvaluateStepCondition(definition, [], [], "hr1").ShouldBeFalse();
        BlankConditions.EvaluateStepCondition(definition, [], null, "hr2").ShouldBeTrue();
        Should.Throw<ArgumentException>(() => BlankConditions.EvaluateStepCondition(definition, [], null, "nope"));
    }

    [Fact]
    public void Prefill_maydon_turiga_moslanadi()
    {
        var definition = F06();
        var head = Guid.Parse(MentorId);
        var subject = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000099");

        var answers = BlankPrefill.Apply(definition, new Dictionary<string, BlankPrefillValue>
        {
            [BlankPrefillKeys.SubjectFullName] = new(Text: "Ali Valiyev", Id: subject),
            [BlankPrefillKeys.SubjectPositionName] = new(Text: "Operator"),
            [BlankPrefillKeys.SubjectHireDate] = new(Date: new DateOnly(2026, 11, 3)),
            [BlankPrefillKeys.SubjectProbationEndDate] = new(Text: "sana emas"),
            [BlankPrefillKeys.SubjectDepartmentHeadId] = new(Text: "Bosh", Id: head),
        });

        answers["employee"]!.ToJsonString().ShouldBe($$"""{"id":"{{subject}}","label":"Ali Valiyev"}""");
        answers["position"]!.GetValue<string>().ShouldBe("Operator");
        answers["hireDate"]!.GetValue<string>().ShouldBe("2026-11-03");
        answers.ContainsKey("probationEnd").ShouldBeFalse("turga mos kelmagan qiymat tashlanadi");
        answers["manager"]!["id"]!.GetValue<string>().ShouldBe(head.ToString());

        BlankAnswersValidator.Validate(definition, answers, Options()).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Computed_yashirin_bo_lsa_hisoblanmaydi()
    {
        var definition = Load("""
            [ { "id": "on", "type": "yesno", "label": "On" },
              { "id": "c", "type": "computed", "label": "C", "valueType": "number", "when": { "field": "on", "eq": true }, "expr": { "const": 5 } } ]
            """);

        BlankComputedEvaluator.ComputeAll(definition, Json("""{ "on": false }""")).ContainsKey("c").ShouldBeFalse();
        BlankComputedEvaluator.ComputeAll(definition, Json("""{ "on": true }"""))["c"]!.GetValue<decimal>().ShouldBe(5m);
    }

    [Fact]
    public void Computed_valueType_ga_mos_kelmasa_null()
    {
        var definition = Load("""
            [ { "id": "d", "type": "date", "label": "D" },
              { "id": "end", "type": "computed", "label": "E", "valueType": "date", "expr": { "op": "addDays", "args": [ { "ref": "d" }, { "const": 90 } ] } },
              { "id": "bad", "type": "computed", "label": "B", "valueType": "number", "expr": { "op": "addDays", "args": [ { "ref": "d" }, { "const": 90 } ] } },
              { "id": "txt", "type": "computed", "label": "T", "valueType": "text", "expr": { "op": "div", "args": [ { "const": 7 }, { "const": 2 } ] } } ]
            """);

        var computed = BlankComputedEvaluator.ComputeAll(definition, Json("""{ "d": "2026-11-03" }"""));

        computed["end"]!.GetValue<string>().ShouldBe("2027-02-01");
        computed.ContainsKey("bad").ShouldBeFalse();
        computed["txt"]!.GetValue<string>().ShouldBe("3.5");
    }
}
