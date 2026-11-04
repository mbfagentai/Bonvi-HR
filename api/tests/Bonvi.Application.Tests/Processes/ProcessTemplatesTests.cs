using Bonvi.Application.Processes.Definitions;

namespace Bonvi.Application.Tests.Processes;

/// <summary>
/// The five Phase 1 process templates (SPEC-mening-ishlarim §3.3–§3.7) — the seeder runs the same validator, so
/// one error here loses the template at startup.
/// </summary>
public sealed class ProcessTemplatesTests
{
    private static readonly string[] Codes = ["hire-request", "hire-candidate", "hire-formalise", "probation", "dismissal"];

    private static string TemplatesDirectory => Path.Combine(AppContext.BaseDirectory, "Processes", "Templates");

    public static TheoryData<string> All() => new(Codes);

    private static string Read(string code) => File.ReadAllText(Path.Combine(TemplatesDirectory, code + ".json"));

    private static ProcessDefinition Load(string code) => ProcessDefinitionValidator.Load(Read(code));

    [Fact]
    public void Folder_holds_exactly_the_phase_1_templates()
        => Directory.GetFiles(TemplatesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ShouldBe(Codes, ignoreOrder: true);

    [Theory]
    [MemberData(nameof(All))]
    public void Template_validates_without_errors(string code)
    {
        var result = ProcessDefinitionValidator.Validate(Read(code), code);

        result.Errors.ShouldBeEmpty(string.Join(Environment.NewLine, result.Errors));
        result.IsValid.ShouldBeTrue();
        result.Definition!.Code.ShouldBe(code);
        result.Definition.Schema.ShouldBe(ProcessDefinition.CurrentSchema);
    }

    [Theory]
    [MemberData(nameof(All))]
    public void Template_is_rejected_under_another_file_name(string code)
        => ProcessDefinitionValidator.Validate(Read(code), "not-" + code).Errors
            .ShouldContain(error => error.Path == "$.code");

    [Theory]
    [InlineData("hire-request", "vacancyRequest", new[] { "r02-1", "r02-2", "r02-3", "r02-4", "r02-14" })]
    [InlineData("hire-candidate", "candidate",
        new[] { "r02-5", "r02-6", "r02-7", "r02-9", "r02-10", "r02-8", "r02-11", "r02-12", "r02-13" })]
    [InlineData("hire-formalise", "employment",
        new[] { "r01-2", "r01-3", "r01-4", "r01-5", "mm", "r01-8a", "r01-6", "r01-7", "r01-9" })]
    [InlineData("probation", "employment",
        new[] { "p03-0", "p03-1", "p03-7", "p03-14", "p03-30", "p03-60", "p03-85", "p03-88", "p03-90" })]
    [InlineData("dismissal", "employment",
        new[] { "ex-1", "ex-2", "ex-3", "ex-4", "ex-5", "ex-6", "ex-7", "ex-8", "ex-8b", "ex-9", "ex-10", "ex-11" })]
    public void Step_ids_follow_the_spec_table_order(string code, string subjectKind, string[] ids)
    {
        var definition = Load(code);

        definition.SubjectKind.ShouldBe(subjectKind);
        definition.Steps.Select(step => step.Id).ShouldBe(ids);
    }

    [Theory]
    [InlineData("hire-request", new[] { "request", "approve", "search", "closed" })]
    [InlineData("hire-candidate", new[] { "contact", "form", "council", "check", "offer", "firstDay" })]
    [InlineData("hire-formalise", new[] { "docs", "order", "firstDay", "after" })]
    [InlineData("probation", new[] { "start", "d7", "d30", "final", "decision" })]
    [InlineData("dismissal", new[] { "start", "handover", "order", "settle" })]
    public void Board_columns_follow_the_spec(string code, string[] columns)
        => Load(code).Board.Select(column => column.Key).ShouldBe(columns);

    [Fact]
    public void R02_13_is_blocked_by_Q01_and_Q04()
    {
        var step = Load("hire-candidate").FindStep("r02-13")!;

        step.Gates.ShouldBe(
            [new ProcessGateRef(ProcessGateCodes.Q01, ProcessGateMode.Block), new ProcessGateRef(ProcessGateCodes.Q04, ProcessGateMode.Block)],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData("ex-8", "Q12")]
    [InlineData("ex-10", "Q12")]
    [InlineData("ex-3", "Q13")]
    public void Dismissal_hook_gates_block(string stepId, string gate)
        => Load("dismissal").FindStep(stepId)!.Gates.ShouldBe([new ProcessGateRef(gate, ProcessGateMode.Block)]);

    [Theory]
    [InlineData("hire-formalise", "r01-9", "Q03", ProcessGateMode.Block)]
    [InlineData("hire-formalise", "r01-8a", "Q02", ProcessGateMode.Warn)]
    [InlineData("hire-formalise", "r01-6", "Q05", ProcessGateMode.Warn)]
    [InlineData("hire-formalise", "r01-7", "Q06", ProcessGateMode.Warn)]
    [InlineData("dismissal", "ex-8b", "Q05", ProcessGateMode.Warn)]
    public void Engine_and_warning_gates_follow_the_spec(string code, string stepId, string gate, ProcessGateMode mode)
        => Load(code).FindStep(stepId)!.Gates.ShouldBe([new ProcessGateRef(gate, mode)]);

    [Theory]
    [InlineData("dismissal", "ex-4")]
    [InlineData("dismissal", "ex-5")]
    [InlineData("dismissal", "ex-6")]
    public void Akt_is_a_warning(string code, string stepId)
        => Load(code).FindStep(stepId)!.Gates.ShouldBe([new ProcessGateRef(ProcessGateCodes.Akt, ProcessGateMode.Warn)]);

    [Fact]
    public void Steps_without_a_spec_gate_have_none()
    {
        var gated = new HashSet<string> { "r02-13", "r01-9", "r01-8a", "r01-6", "r01-7", "ex-3", "ex-4", "ex-5", "ex-6", "ex-8", "ex-8b", "ex-10" };

        Codes.SelectMany(code => Load(code).Steps)
            .Where(step => step.Gates.Count > 0)
            .Select(step => step.Id)
            .ShouldBe(gated, ignoreOrder: true);
    }

    [Theory]
    [InlineData("hire-candidate", "r02-9")]
    [InlineData("hire-candidate", "r02-12")]
    [InlineData("hire-candidate", "r02-13")]
    public void HrMentor_fallback_without_inputs(string code, string stepId)
    {
        var step = Load(code).FindStep(stepId)!;

        step.HasManualFallback.ShouldBeTrue("§1.5: a fact step reading a not-yet-served blank needs manual.allowed");
        step.Manual!.Inputs.ShouldBeEmpty();
    }

    [Fact]
    public void R02_8_fallback_asks_for_the_background_check_outcome()
    {
        var step = Load("hire-candidate").FindStep("r02-8")!;

        step.HasManualFallback.ShouldBeTrue();
        var outcome = step.Manual!.Inputs.ShouldHaveSingleItem();
        outcome.Id.ShouldBe("outcome");
        outcome.Type.ShouldBe(ProcessInputType.Choice);
        outcome.Options.Select(option => option.Value).ShouldBe(["ok", "watch", "notRecommended"]);
    }

    [Fact]
    public void R02_10_fallback_asks_for_decision_and_council_date()
    {
        var step = Load("hire-candidate").FindStep("r02-10")!;

        step.HasManualFallback.ShouldBeTrue();
        step.Manual!.Inputs.Select(input => input.Id).ShouldBe(["decision", "councilDate"], ignoreOrder: true);
        var decision = step.Manual.Inputs.Single(input => input.Id == "decision");
        decision.Type.ShouldBe(ProcessInputType.Choice);
        decision.Options.Select(option => option.Value).ShouldBe(["accept", "conditional", "reserve", "reject"]);
        step.Manual.Inputs.Single(input => input.Id == "councilDate").Type.ShouldBe(ProcessInputType.Date);
    }

    [Fact]
    public void R02_11_fallback_asks_for_the_start_date()
    {
        var step = Load("hire-candidate").FindStep("r02-11")!;

        step.HasManualFallback.ShouldBeTrue();
        var startDate = step.Manual!.Inputs.ShouldHaveSingleItem();
        startDate.Id.ShouldBe("startDate");
        startDate.Type.ShouldBe(ProcessInputType.Date);
    }

    [Fact]
    public void R01_2_fallback_is_the_F05_documents_checklist()
    {
        var step = Load("hire-formalise").FindStep("r01-2")!;

        step.HasManualFallback.ShouldBeTrue();
        var docs = step.Manual!.Inputs.ShouldHaveSingleItem();
        docs.Id.ShouldBe("docs");
        docs.Type.ShouldBe(ProcessInputType.Checklist);
        docs.Required.ShouldBeTrue();
        docs.Options.Count.ShouldBe(12, "§3.5: checklist = the 12 F05 rows");
    }

    [Fact]
    public void R02_10_is_held_by_the_council_with_hr1_fallback()
    {
        var step = Load("hire-candidate").FindStep("r02-10")!;

        step.Role.ShouldBe(ProcessRoles.Council);
        step.FallbackRole.ShouldBe(ProcessRoles.Hr1);
    }

    [Fact]
    public void R02_8_runs_only_on_a_conditional_council_decision()
    {
        var when = Load("hire-candidate").FindStep("r02-8")!.When!;

        when.Fact.ShouldBe(ProcessFactNames.CandidateCouncilDecision);
        when.EqualsValue.ShouldBe(ProcessScalar.Of("conditional"));
    }

    [Fact]
    public void R02_7_is_optional_and_skippable()
    {
        var step = Load("hire-candidate").FindStep("r02-7")!;

        step.Optional.ShouldBeTrue();
        step.Skippable.ShouldBeTrue();
    }

    [Fact]
    public void Probation_early_steps_are_cut_off_five_days_before_the_end_but_the_final_ones_are_not()
    {
        var definition = Load("probation");
        var cut = new[] { "p03-0", "p03-1", "p03-7", "p03-14", "p03-30", "p03-60" };

        foreach (var id in cut)
        {
            var cutoff = definition.FindStep(id)!.SkipIfDueOnOrAfter;
            cutoff.ShouldNotBeNull(id);
            cutoff.ShouldBe(new ProcessDueRule(ProcessAnchors.ProbationEndDate, ProcessDueKind.CalendarDays, -5), id);
        }

        foreach (var id in new[] { "p03-85", "p03-88", "p03-90" })
        {
            definition.FindStep(id)!.SkipIfDueOnOrAfter.ShouldBeNull(id);
        }

        definition.Steps.Where(step => step.LegacySkippable).Select(step => step.Id).ShouldBe(cut);
    }

    [Theory]
    [InlineData("p03-0", "probation.startDate", -1)]
    [InlineData("p03-1", "probation.startDate", 0)]
    [InlineData("p03-7", "probation.startDate", 7)]
    [InlineData("p03-14", "probation.startDate", 14)]
    [InlineData("p03-30", "probation.startDate", 30)]
    [InlineData("p03-60", "probation.startDate", 60)]
    [InlineData("p03-85", "probation.endDate", -5)]
    [InlineData("p03-88", "probation.endDate", -2)]
    [InlineData("p03-90", "probation.endDate", 0)]
    public void Probation_dues_are_calendar_days(string stepId, string anchor, int days)
        => Load("probation").FindStep(stepId)!.Due.ShouldBe(new ProcessDueRule(anchor, ProcessDueKind.CalendarDays, days));

    [Fact]
    public void R02_12_due_falls_back_from_the_offer_start_date()
        => Load("hire-candidate").FindStep("r02-12")!.Due.ShouldBe(
            new ProcessDueRule(
                ProcessAnchors.OfferStartDate, ProcessDueKind.CalendarDays, -1,
                new ProcessDueRule(ProcessAnchors.StepDoneOn("r02-11"), ProcessDueKind.WorkingDays, 3)));

    [Fact]
    public void Auto_steps_are_held_by_the_system()
    {
        // §3.1: an auto step is never a task; its holder is «Tizim» (ex-1 included, decided 2026-11-04)
        var auto = Codes.SelectMany(code => Load(code).Steps).Where(step => step.Kind == ProcessStepKind.Auto).ToList();

        auto.Select(step => step.Id).ShouldBe(["r02-2", "r02-14", "r01-5", "ex-1", "ex-7", "ex-11"], ignoreOrder: true);
        auto.ShouldAllBe(step => step.Role == ProcessRoles.System);
        auto.Single(step => step.Id == "ex-1").Completion!.Fact.ShouldBe(ProcessFactNames.CaseStarted);
    }
}
