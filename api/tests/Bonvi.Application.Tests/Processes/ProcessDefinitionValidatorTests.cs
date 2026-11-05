using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;

namespace Bonvi.Application.Tests.Processes;

/// <summary>
/// Template check — every rejection of SPEC-mening-ishlarim §3.1 «Validator» plus the schema extensions P2a added.
/// Each test starts from <see cref="MinimalJson"/> (valid) and breaks exactly one thing.
/// </summary>
public sealed class ProcessDefinitionValidatorTests
{
    private const string Code = "test-flow";

    /// <summary>steps[0] fact, steps[1] manual (prereq a, due from a), steps[2] auto.</summary>
    private const string MinimalJson = """
        {
          "schema": 1, "code": "test-flow", "version": "1.0", "title": "Test", "source": "HR-R02",
          "subjectKind": "candidate",
          "board": [ { "key": "one", "title": "One" }, { "key": "two", "title": "Two" } ],
          "steps": [
            { "id": "a", "title": "A", "column": "one", "role": "hr4", "kind": "fact",
              "completion": { "fact": "cand.contacted" }, "link": { "screen": "candidate" } },
            { "id": "b", "title": "B", "column": "two", "role": "hr4", "kind": "manual", "prereqs": ["a"],
              "due": { "from": "step:a.doneOn", "workingDays": 2 },
              "manual": { "allowed": true, "inputs": [ { "id": "note", "label": "Note", "type": "text", "required": true } ] },
              "link": { "screen": "none" } },
            { "id": "c", "title": "C", "column": "two", "role": "system", "kind": "auto", "prereqs": ["a"],
              "completion": { "fact": "cand.f02" }, "link": { "screen": "none" } }
          ]
        }
        """;

    private static JsonObject Step(JsonObject root, int index) => root["steps"]![index]!.AsObject();

    private static IReadOnlyList<ProcessDefinitionError> Errors(Action<JsonObject> mutate, string? expectedCode = Code)
    {
        var root = JsonNode.Parse(MinimalJson)!.AsObject();
        mutate(root);

        var result = ProcessDefinitionValidator.Validate(root.ToJsonString(), expectedCode);

        result.IsValid.ShouldBeFalse();
        result.Definition.ShouldBeNull("an invalid template must not yield a model");
        result.Errors.ShouldNotBeEmpty();
        return result.Errors;
    }

    private static ProcessDefinition Valid(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(MinimalJson)!.AsObject();
        mutate(root);

        var result = ProcessDefinitionValidator.Validate(root.ToJsonString(), Code);

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors));
        return result.Definition!;
    }

    // ── baseline ───────────────────────────────────────────────

    [Fact]
    public void Minimal_definition_is_valid_and_fully_read()
    {
        var definition = Valid(_ => { });

        definition.Code.ShouldBe(Code);
        definition.SubjectKind.ShouldBe("candidate");
        definition.Board.Select(column => column.Key).ShouldBe(["one", "two"]);
        definition.Steps.Select(step => step.Id).ShouldBe(["a", "b", "c"]);
        definition.FindStep("a")!.Completion!.Fact.ShouldBe("cand.contacted");
        definition.FindStep("b")!.Kind.ShouldBe(ProcessStepKind.Manual);
        definition.FindStep("b")!.Due.ShouldBe(new ProcessDueRule("step:a.doneOn", ProcessDueKind.WorkingDays, 2));
        definition.FindStep("b")!.Manual!.Inputs.ShouldHaveSingleItem().Id.ShouldBe("note");
        definition.FindStep("c")!.Kind.ShouldBe(ProcessStepKind.Auto);
    }

    [Fact]
    public void Expected_code_null_skips_the_file_name_check()
        => ProcessDefinitionValidator.Validate(MinimalJson).IsValid.ShouldBeTrue();

    [Fact]
    public void Wrong_expected_code()
        => Errors(_ => { }, "hire-request")
            .ShouldContain(error => error.Path == "$.code" && error.Message.Contains("hire-request", StringComparison.Ordinal));

    [Fact]
    public void Not_json()
    {
        var result = ProcessDefinitionValidator.Validate("{ not json", Code);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Path.ShouldBe("$");
    }

    [Fact]
    public void Root_must_be_an_object()
        => ProcessDefinitionValidator.Validate("[]", Code).Errors.ShouldContain(error => error.Path == "$");

    [Fact]
    public void Load_throws_on_an_invalid_template()
        => Should.Throw<InvalidOperationException>(() => ProcessDefinitionValidator.Load(MinimalJson.Replace("\"hr4\"", "\"hr9\"", StringComparison.Ordinal)))
            .Message.ShouldContain("hr9");

    [Fact]
    public void Every_error_is_reported_at_once()
    {
        var errors = Errors(root =>
        {
            Step(root, 0)["role"] = "nobody";
            Step(root, 1)["link"]!["screen"] = "nowhere";
        });

        errors.ShouldContain(error => error.Path == "steps[0].role");
        errors.ShouldContain(error => error.Path == "steps[1].link.screen");
    }

    // ── header and board ───────────────────────────────────────

    [Fact]
    public void Unsupported_schema()
        => Errors(root => root["schema"] = 2).ShouldContain(error => error.Path == "$.schema");

    [Fact]
    public void Missing_schema()
        => Errors(root => root.Remove("schema")).ShouldContain(error => error.Path == "$.schema");

    [Theory]
    [InlineData("Hire_Request")]
    [InlineData("1hire")]
    public void Code_must_be_kebab_case(string code)
        => Errors(root => root["code"] = code, expectedCode: null).ShouldContain(error => error.Path == "$.code");

    [Fact]
    public void Version_shape()
        => Errors(root => root["version"] = "v1").ShouldContain(error => error.Path == "$.version");

    [Fact]
    public void Unknown_subject_kind()
        => Errors(root => root["subjectKind"] = "vacancy").ShouldContain(error => error.Path == "$.subjectKind");

    [Fact]
    public void No_steps()
        => Errors(root => root["steps"] = new JsonArray()).ShouldContain(error => error.Path == "$.steps");

    [Fact]
    public void Missing_required_top_key()
        => Errors(root => root.Remove("title")).ShouldContain(error => error.Path == "$.title");

    [Fact]
    public void Duplicate_board_column()
        => Errors(root => root["board"]![1]!["key"] = "one").ShouldContain(error => error.Path == "board[1].key");

    [Fact]
    public void Step_column_must_be_on_the_board()
        => Errors(root => Step(root, 0)["column"] = "three").ShouldContain(error => error.Path == "steps[0].column");

    // ── unknown keys / kinds / roles / facts / anchors / screens ──

    [Fact]
    public void Unknown_top_key()
        => Errors(root => root["colour"] = "red").ShouldContain(error => error.Path == "$.colour");

    [Fact]
    public void Unknown_step_key()
        => Errors(root => Step(root, 0)["colour"] = "red").ShouldContain(error => error.Path == "steps[0].colour");

    [Fact]
    public void Unknown_due_key()
        => Errors(root => Step(root, 1)["due"]!["hours"] = 4).ShouldContain(error => error.Path == "steps[1].due.hours");

    [Fact]
    public void Unknown_completion_arg()
        => Errors(root => Step(root, 0)["completion"]!["args"] = new JsonObject { ["notEquals"] = "x" })
            .ShouldContain(error => error.Path == "steps[0].completion.args.notEquals");

    [Fact]
    public void Unknown_input_key()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]![0]!["placeholder"] = "x")
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0].placeholder");

    [Fact]
    public void Unknown_kind()
        => Errors(root => Step(root, 0)["kind"] = "timer").ShouldContain(error => error.Path == "steps[0].kind");

    [Fact]
    public void Unknown_input_type()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]![0]!["type"] = "photo")
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0].type");

    [Fact]
    public void Unknown_role()
        => Errors(root => Step(root, 0)["role"] = "hr9").ShouldContain(error => error.Path == "steps[0].role");

    [Fact]
    public void Unknown_completion_fact()
        => Errors(root => Step(root, 0)["completion"]!["fact"] = "cand.teleported")
            .ShouldContain(error => error.Path == "steps[0].completion.fact" && error.Message.Contains("cand.teleported", StringComparison.Ordinal));

    [Fact]
    public void Unknown_family_parameter()
        => Errors(root => Step(root, 0)["completion"]!["fact"] = "cand.stage:hired")
            .ShouldContain(error => error.Path == "steps[0].completion.fact");

    [Fact]
    public void Known_family_parameter_is_accepted()
        => Valid(root => Step(root, 0)["completion"]!["fact"] = "cand.stage:council")
            .FindStep("a")!.Completion!.Fact.ShouldBe("cand.stage:council");

    [Fact]
    public void Unknown_when_fact()
        => Errors(root => Step(root, 0)["when"] = new JsonObject { ["fact"] = "x.y", ["equals"] = true })
            .ShouldContain(error => error.Path == "steps[0].when.fact");

    [Fact]
    public void Unknown_allOf_fact()
        => Errors(root => Step(root, 0)["completion"]!["args"] = new JsonObject { ["allOf"] = new JsonArray("x.y") })
            .ShouldContain(error => error.Path == "steps[0].completion.args.allOf[0]");

    [Fact]
    public void Unknown_legacyAnyOf_fact()
        => Errors(root => Step(root, 0)["completion"]!["args"] = new JsonObject { ["legacyAnyOf"] = new JsonArray("x.y") })
            .ShouldContain(error => error.Path == "steps[0].completion.args.legacyAnyOf[0]");

    [Fact]
    public void Unknown_fact_inside_byValue_branch()
        => Errors(root => Step(root, 0)["completion"]!["args"] = new JsonObject
            {
                ["byValue"] = new JsonObject { ["accept"] = new JsonObject { ["fact"] = "x.y" } },
            })
            .ShouldContain(error => error.Path == "steps[0].completion.args.byValue.accept.fact");

    [Fact]
    public void Unknown_requiredWhen_fact()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "basis", ["label"] = "Basis", ["type"] = "text",
                ["requiredWhen"] = new JsonObject { ["fact"] = "x.y", ["equals"] = true },
            }))
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[1].requiredWhen.fact");

    [Fact]
    public void Unknown_anchor()
        => Errors(root => Step(root, 1)["due"]!["from"] = "case.endedOn")
            .ShouldContain(error => error.Path == "steps[1].due.from" && error.Message.Contains("case.endedOn", StringComparison.Ordinal));

    [Fact]
    public void Unknown_anchor_in_a_nested_fallback()
        => Errors(root => Step(root, 1)["due"]!["fallback"] = new JsonObject
            {
                ["from"] = "candidate.appliedOn", ["days"] = 1,
                ["fallback"] = new JsonObject { ["from"] = "bogus", ["days"] = 1 },
            })
            .ShouldContain(error => error.Path == "steps[1].due.fallback.fallback.from");

    [Fact]
    public void Unknown_anchor_in_skipIfDueOnOrAfter()
        => Errors(root => Step(root, 0)["skipIfDueOnOrAfter"] = new JsonObject { ["from"] = "probation.closedOn", ["days"] = -5 })
            .ShouldContain(error => error.Path == "steps[0].skipIfDueOnOrAfter.from");

    [Theory]
    [InlineData("case.startedOn")]
    [InlineData("case.anchorDate")]
    [InlineData("candidate.appliedOn")]
    [InlineData("employment.hireDate")]
    [InlineData("employment.hireOrderDate")]
    [InlineData("employment.terminationDate")]
    [InlineData("probation.startDate")]
    [InlineData("probation.endDate")]
    [InlineData("f05.startDate")]
    public void Every_catalogue_anchor_is_accepted(string anchor)
        => Valid(root => Step(root, 1)["due"]!["from"] = anchor).FindStep("b")!.Due!.From.ShouldBe(anchor);

    [Fact]
    public void Unknown_screen()
        => Errors(root => Step(root, 0)["link"]!["screen"] = "payroll").ShouldContain(error => error.Path == "steps[0].link.screen");

    [Fact]
    public void Missing_link()
        => Errors(root => Step(root, 0).Remove("link")).ShouldContain(error => error.Path == "steps[0].link");

    [Fact]
    public void Screen_arg_it_does_not_take()
        => Errors(root => Step(root, 0)["link"]!["args"] = new JsonObject { ["template"] = "HR-F02" })
            .ShouldContain(error => error.Path == "steps[0].link.args.template");

    [Fact]
    public void Blank_screen_needs_a_template()
        => Errors(root => Step(root, 0)["link"] = new JsonObject { ["screen"] = "blank" })
            .ShouldContain(error => error.Path == "steps[0].link.args.template");

    [Fact]
    public void Blank_screen_template_outside_the_catalogue()
        => Errors(root => Step(root, 0)["link"] = new JsonObject
            {
                ["screen"] = "blank", ["args"] = new JsonObject { ["template"] = "HR-F20" },
            })
            .ShouldContain(error => error.Path == "steps[0].link.args.template");

    [Fact]
    public void Blank_screen_unknown_subject()
        => Errors(root => Step(root, 0)["link"] = new JsonObject
            {
                ["screen"] = "blank", ["args"] = new JsonObject { ["template"] = "HR-F03", ["subject"] = "vacancy" },
            })
            .ShouldContain(error => error.Path == "steps[0].link.args.subject");

    [Fact]
    public void Blank_screen_with_template_and_subject_is_valid()
        => Valid(root => Step(root, 0)["link"] = new JsonObject
            {
                ["screen"] = "blank", ["args"] = new JsonObject { ["template"] = "HR-F03", ["subject"] = "candidate" },
            })
            .FindStep("a")!.Link.Args["template"].ShouldBe("HR-F03");

    [Theory]
    [InlineData("http://mehnat.uz")]
    [InlineData("mehnat.uz")]
    public void External_link_must_be_absolute_https(string url)
        => Errors(root => Step(root, 1)["link"] = new JsonObject
            {
                ["screen"] = "external", ["args"] = new JsonObject { ["url"] = url },
            })
            .ShouldContain(error => error.Path == "steps[1].link.args.url");

    [Fact]
    public void External_link_needs_a_url()
        => Errors(root => Step(root, 1)["link"] = new JsonObject { ["screen"] = "external" })
            .ShouldContain(error => error.Path == "steps[1].link.args.url");

    // ── ids and prereqs ────────────────────────────────────────

    [Fact]
    public void Duplicate_step_id()
        => Errors(root => Step(root, 2)["id"] = "b")
            .ShouldContain(error => error.Path == "steps[2].id" && error.Message.Contains("Duplicate", StringComparison.Ordinal));

    [Theory]
    [InlineData("R02-1")]
    [InlineData("r02_1")]
    [InlineData("-r02")]
    public void Step_id_shape(string id)
        => Errors(root => Step(root, 0)["id"] = id).ShouldContain(error => error.Path == "steps[0].id");

    [Fact]
    public void Unknown_prereq()
        => Errors(root => Step(root, 1)["prereqs"] = new JsonArray("zz"))
            .ShouldContain(error => error.Path == "steps[1].prereqs[0]" && error.Message.Contains("zz", StringComparison.Ordinal));

    [Fact]
    public void Self_prereq()
        => Errors(root => Step(root, 0)["prereqs"] = new JsonArray("a")).ShouldContain(error => error.Path == "steps[0].prereqs[0]");

    [Fact]
    public void Prereq_cycle_is_reported_as_a_path()
    {
        var errors = Errors(root => Step(root, 0)["prereqs"] = new JsonArray("b"));

        var cycle = errors.ShouldHaveSingleItem();
        cycle.Path.ShouldBe("$.steps");
        cycle.Message.ShouldContain("cycle");
        cycle.Message.ShouldContain("a");
        cycle.Message.ShouldContain("b");
    }

    [Fact]
    public void Three_step_prereq_cycle()
        => Errors(root =>
            {
                Step(root, 0)["prereqs"] = new JsonArray("c");
                Step(root, 2)["prereqs"] = new JsonArray("b");
            })
            .ShouldContain(error => error.Path == "$.steps" && error.Message.Contains("cycle", StringComparison.Ordinal));

    [Fact]
    public void Diamond_is_not_a_cycle()
        => Valid(root => Step(root, 2)["prereqs"] = new JsonArray("a", "b")).FindStep("c")!.Prereqs.ShouldBe(["a", "b"]);

    [Fact]
    public void Step_anchor_pointing_to_an_unknown_step()
        => Errors(root => Step(root, 1)["due"]!["from"] = "step:zz.doneOn")
            .ShouldContain(error => error.Path == "steps[1].due.from" && error.Message.Contains("zz", StringComparison.Ordinal));

    [Fact]
    public void Step_anchor_pointing_to_itself()
        => Errors(root => Step(root, 1)["due"]!["from"] = "step:b.doneOn").ShouldContain(error => error.Path == "steps[1].due.from");

    [Fact]
    public void Malformed_step_anchor_is_unknown()
        => Errors(root => Step(root, 1)["due"]!["from"] = "step:a").ShouldContain(error => error.Path == "steps[1].due.from");

    // ── due shape ──────────────────────────────────────────────

    [Fact]
    public void Due_needs_exactly_one_offset()
        => Errors(root => Step(root, 1)["due"]!["days"] = 3).ShouldContain(error => error.Path == "steps[1].due");

    [Fact]
    public void Due_without_an_offset()
        => Errors(root => Step(root, 1)["due"] = new JsonObject { ["from"] = "case.startedOn" })
            .ShouldContain(error => error.Path == "steps[1].due");

    [Fact]
    public void Unknown_due_rule()
        => Errors(root => Step(root, 1)["due"] = new JsonObject { ["from"] = "case.startedOn", ["rule"] = "nextMonday" })
            .ShouldContain(error => error.Path == "steps[1].due.rule");

    [Fact]
    public void Next_council_day_rule_is_read()
        => Valid(root => Step(root, 1)["due"] = new JsonObject { ["from"] = "step:a.doneOn", ["rule"] = "nextCouncilDay" })
            .FindStep("b")!.Due.ShouldBe(new ProcessDueRule("step:a.doneOn", ProcessDueKind.NextCouncilDay, 0));

    [Fact]
    public void Negative_working_days_are_allowed()
        => Valid(root => Step(root, 1)["due"]!["workingDays"] = -5).FindStep("b")!.Due!.Offset.ShouldBe(-5);

    [Fact]
    public void Due_offset_must_be_a_whole_number()
        => Errors(root => Step(root, 1)["due"]!["workingDays"] = 1.5).ShouldContain(error => error.Path == "steps[1].due.workingDays");

    // ── kinds ──────────────────────────────────────────────────

    [Fact]
    public void Manual_step_without_manual()
        => Errors(root => Step(root, 1).Remove("manual")).ShouldContain(error => error.Path == "steps[1].manual");

    [Fact]
    public void Manual_step_with_manual_allowed_false()
        => Errors(root => Step(root, 1)["manual"]!["allowed"] = false)
            .ShouldContain(error => error.Path == "steps[1].manual.allowed");

    [Fact]
    public void Manual_step_with_a_completion_fact()
        => Errors(root => Step(root, 1)["completion"] = new JsonObject { ["fact"] = "cand.f02" })
            .ShouldContain(error => error.Path == "steps[1].completion");

    [Fact]
    public void Fact_step_without_completion()
        => Errors(root => Step(root, 0).Remove("completion")).ShouldContain(error => error.Path == "steps[0].completion");

    [Fact]
    public void Fact_step_with_a_manual_fallback_is_valid()
    {
        var step = Valid(root => Step(root, 0)["manual"] = new JsonObject { ["allowed"] = true }).FindStep("a")!;

        step.HasManualFallback.ShouldBeTrue();
        step.Manual!.Inputs.ShouldBeEmpty();
    }

    [Fact]
    public void Auto_step_with_due()
        => Errors(root => Step(root, 2)["due"] = new JsonObject { ["from"] = "case.startedOn", ["days"] = 1 })
            .ShouldContain(error => error.Path == "steps[2].due");

    [Fact]
    public void Auto_step_with_manual()
        => Errors(root => Step(root, 2)["manual"] = new JsonObject { ["allowed"] = true })
            .ShouldContain(error => error.Path == "steps[2].manual");

    [Fact]
    public void Auto_step_with_gate()
        => Errors(root => Step(root, 2)["gate"] = new JsonObject { ["code"] = "Q02", ["mode"] = "warn" })
            .ShouldContain(error => error.Path == "steps[2].gate");

    [Fact]
    public void Auto_step_without_completion()
        => Errors(root => Step(root, 2).Remove("completion")).ShouldContain(error => error.Path == "steps[2].completion");

    // ── gates ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Q07")]
    [InlineData("Q11")]
    [InlineData("q01")]
    [InlineData("Q99")]
    public void Gate_code_outside_the_catalogue(string code)
        => Errors(root => Step(root, 1)["gate"] = new JsonObject { ["code"] = code, ["mode"] = "block" })
            .ShouldContain(error => error.Path == "steps[1].gate.code" && error.Message.Contains(code, StringComparison.Ordinal));

    [Fact]
    public void Unknown_gate_mode()
        => Errors(root => Step(root, 1)["gate"] = new JsonObject { ["code"] = "Q01", ["mode"] = "soft" })
            .ShouldContain(error => error.Path == "steps[1].gate.mode");

    [Fact]
    public void A_skippable_step_cannot_sit_behind_a_block_gate()
        => Errors(root =>
            {
                Step(root, 1)["gate"] = new JsonObject { ["code"] = "Q03", ["mode"] = "block" };
                Step(root, 1)["skippable"] = true;
            })
            .ShouldContain(error => error.Path == "steps[1].skippable");

    [Fact]
    public void A_skippable_step_may_carry_a_warn_gate()
        => Valid(root =>
        {
            Step(root, 1)["gate"] = new JsonObject { ["code"] = "Q02", ["mode"] = "warn" };
            Step(root, 1)["skippable"] = true;
        });

    [Fact]
    public void A_legacy_skippable_step_may_sit_behind_a_block_gate()
        => Valid(root =>
        {
            Step(root, 1)["gate"] = new JsonObject { ["code"] = "Q03", ["mode"] = "block" };
            Step(root, 1)["legacySkippable"] = true;
        });

    [Fact]
    public void Gate_array_is_read_in_order()
        => Valid(root => Step(root, 1)["gate"] = new JsonArray(
                new JsonObject { ["code"] = "Q01", ["mode"] = "block" },
                new JsonObject { ["code"] = "Q04", ["mode"] = "warn" }))
            .FindStep("b")!.Gates.ShouldBe([new ProcessGateRef("Q01", ProcessGateMode.Block), new ProcessGateRef("Q04", ProcessGateMode.Warn)]);

    [Fact]
    public void Gate_array_with_an_unknown_code_points_at_the_item()
        => Errors(root => Step(root, 1)["gate"] = new JsonArray(
                new JsonObject { ["code"] = "Q01", ["mode"] = "block" },
                new JsonObject { ["code"] = "Q08", ["mode"] = "block" }))
            .ShouldContain(error => error.Path == "steps[1].gate[1].code");

    [Fact]
    public void Gate_listed_twice()
        => Errors(root => Step(root, 1)["gate"] = new JsonArray(
                new JsonObject { ["code"] = "Q01", ["mode"] = "block" },
                new JsonObject { ["code"] = "Q01", ["mode"] = "warn" }))
            .ShouldContain(error => error.Path == "steps[1].gate[1].code");

    // ── blanks, signatures, roles ──────────────────────────────

    [Theory]
    [InlineData("HR-F00")]
    [InlineData("HR-F20")]
    [InlineData("HR-F1")]
    [InlineData("HR-R02")]
    [InlineData("hr-f05")]
    public void Blank_code_outside_HR_F01_F19(string blank)
        => Errors(root => Step(root, 0)["blank"] = blank).ShouldContain(error => error.Path == "steps[0].blank");

    [Theory]
    [InlineData("HR-F01")]
    [InlineData("HR-F09")]
    [InlineData("HR-F10")]
    [InlineData("HR-F19")]
    public void Blank_code_inside_the_range(string blank)
        => Valid(root => Step(root, 0)["blank"] = blank).FindStep("a")!.Blank.ShouldBe(blank);

    [Fact]
    public void Unknown_signature()
        => Errors(root => Step(root, 0)["signature"] = "D4").ShouldContain(error => error.Path == "steps[0].signature");

    [Theory]
    [InlineData("departmentHead")]
    [InlineData("subjectEmployee")]
    [InlineData("system")]
    public void Fallback_role_must_be_an_office_role(string fallback)
        => Errors(root =>
            {
                Step(root, 0)["role"] = "departmentHead";
                Step(root, 0)["fallbackRole"] = fallback;
            })
            .ShouldContain(error => error.Path == "steps[0].fallbackRole");

    [Fact]
    public void Unknown_fallback_role()
        => Errors(root => Step(root, 0)["fallbackRole"] = "hr9").ShouldContain(error => error.Path == "steps[0].fallbackRole");

    [Fact]
    public void Fallback_equal_to_the_role()
        => Errors(root => Step(root, 0)["fallbackRole"] = "hr4").ShouldContain(error => error.Path == "steps[0].fallbackRole");

    [Fact]
    public void Person_role_with_office_fallback_is_valid()
        => Valid(root =>
            {
                Step(root, 0)["role"] = "departmentHead";
                Step(root, 0)["fallbackRole"] = "hr4";
            })
            .FindStep("a")!.FallbackRole.ShouldBe("hr4");

    // ── completion args ────────────────────────────────────────

    [Fact]
    public void Only_one_value_test()
        => Errors(root => Step(root, 0)["completion"]!["args"] = new JsonObject
            {
                ["equals"] = "a", ["in"] = new JsonArray("a", "b"),
            })
            .ShouldContain(error => error.Path == "steps[0].completion.args");

    [Fact]
    public void ManualValue_must_name_an_input_of_the_step()
        => Errors(root =>
            {
                Step(root, 0)["manual"] = new JsonObject { ["allowed"] = true };
                Step(root, 0)["completion"]!["args"] = new JsonObject { ["manualValue"] = "decision" };
            })
            .ShouldContain(error => error.Path == "steps[0].completion.args.manualValue");

    [Fact]
    public void Completion_args_are_read()
    {
        var completion = Valid(root => Step(root, 0)["completion"] = JsonNode.Parse("""
            { "fact": "cand.f04Decision", "args": {
                "legacyAnyOf": ["cand.stage:council"], "valueField": "decision",
                "byValue": { "accept": { "fact": "cand.f05Step:offerCandidate" } } } }
            """)).FindStep("a")!.Completion!;

        completion.Fact.ShouldBe("cand.f04Decision");
        completion.LegacyAnyOf.ShouldBe(["cand.stage:council"]);
        completion.ValueField.ShouldBe("decision");
        completion.ByValue["accept"].Fact.ShouldBe("cand.f05Step:offerCandidate");
    }

    // ── manual inputs ──────────────────────────────────────────

    [Fact]
    public void Duplicate_input_id()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]!.AsArray()
                .Add(new JsonObject { ["id"] = "note", ["label"] = "Again", ["type"] = "text" }))
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[1].id");

    [Fact]
    public void Options_on_a_text_input()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]![0]!["options"] = new JsonArray(new JsonObject { ["value"] = "x", ["label"] = "X" }))
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0].options");

    [Fact]
    public void Choice_without_options()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]![0]!["type"] = "choice")
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0].options");

    [Fact]
    public void Min_greater_than_max()
        => Errors(root =>
            {
                var input = Step(root, 1)["manual"]!["inputs"]![0]!;
                input["type"] = "int";
                input["min"] = 5;
                input["max"] = 1;
            })
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0].min");

    [Fact]
    public void Min_on_a_text_input()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]![0]!["min"] = 1)
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0]");

    [Fact]
    public void RequiredWhen_with_both_input_and_fact()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "basis", ["label"] = "Basis", ["type"] = "text",
                ["requiredWhen"] = new JsonObject { ["input"] = "note", ["fact"] = "cand.f02", ["equals"] = "x" },
            }))
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[1].requiredWhen");

    [Fact]
    public void RequiredWhen_on_a_required_input()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]![0]!["requiredWhen"] =
                new JsonObject { ["fact"] = "cand.f02", ["equals"] = true })
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[0].requiredWhen");

    [Fact]
    public void RequiredWhen_pointing_at_a_missing_input()
        => Errors(root => Step(root, 1)["manual"]!["inputs"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "basis", ["label"] = "Basis", ["type"] = "text",
                ["requiredWhen"] = new JsonObject { ["input"] = "term", ["equals"] = "fixed" },
            }))
            .ShouldContain(error => error.Path == "steps[1].manual.inputs[1].requiredWhen.input");

    [Fact]
    public void RequiredWhen_on_another_input_is_valid()
    {
        var input = Valid(root => Step(root, 1)["manual"]!["inputs"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "basis", ["label"] = "Basis", ["type"] = "text",
                ["requiredWhen"] = new JsonObject { ["input"] = "note", ["equals"] = "fixed" },
            }))
            .FindStep("b")!.Manual!.Inputs[1];

        input.RequiredWhen.ShouldBe(new ProcessRequiredWhen("note", null, ProcessScalar.Of("fixed")));
    }

    // ── wrong JSON types ───────────────────────────────────────

    [Fact]
    public void Boolean_key_with_a_string()
        => Errors(root => Step(root, 0)["optional"] = "yes").ShouldContain(error => error.Path == "steps[0].optional");

    [Fact]
    public void Prereqs_must_be_an_array()
        => Errors(root => Step(root, 1)["prereqs"] = "a").ShouldContain(error => error.Path == "steps[1].prereqs");

    [Fact]
    public void Step_must_be_an_object()
        => Errors(root => root["steps"]!.AsArray().Add("d")).ShouldContain(error => error.Path == "steps[3]");
}
