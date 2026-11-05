using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Processes;
using static Bonvi.Application.Tests.Processes.Queries.ProcessReadRows;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Application.Tests.Processes.Queries;

/// <summary>§8.1, §8.3: warnings are computed at read time, and a blocking gate's text has a stable code.</summary>
public sealed class ProcessGateReadingTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly string[] LateAndAct = [ProcessGateCodes.Q05, ProcessGateCodes.Akt];
    private static readonly string[] AllLateness = [ProcessGateCodes.Q02, ProcessGateCodes.Q05, ProcessGateCodes.Q06];

    private static ProcessStep Warns(params string[] codes)
        => Definition(gates: [.. codes.Select(code => new ProcessGateRef(code, ProcessGateMode.Warn))]);

    private static IReadOnlyList<string> Warnings(ProcessStep? definition, ProcessStepRow step, bool handoverActEmpty = false)
        => ProcessGateReading.Warnings(definition, step, Today, handoverActEmpty);

    [Theory]
    [InlineData(ProcessGateCodes.Q02)]
    [InlineData(ProcessGateCodes.Q05)]
    [InlineData(ProcessGateCodes.Q06)]
    public void A_lateness_warning_fires_the_day_after_the_due(string code)
        => Warnings(Warns(code), Step(due: Today.AddDays(-1))).ShouldHaveSingleItem().ShouldBe(code);

    [Theory]
    [InlineData(ProcessGateCodes.Q02)]
    [InlineData(ProcessGateCodes.Q05)]
    [InlineData(ProcessGateCodes.Q06)]
    public void A_lateness_warning_stays_quiet_on_the_due_date_itself(string code)
        => Warnings(Warns(code), Step(due: Today)).ShouldBeEmpty();

    [Theory]
    [InlineData(ProcessGateCodes.Q02)]
    [InlineData(ProcessGateCodes.Q05)]
    [InlineData(ProcessGateCodes.Q06)]
    public void A_lateness_warning_needs_a_due_date(string code)
        => Warnings(Warns(code), Step(due: null)).ShouldBeEmpty();

    [Theory]
    [InlineData(ProcessStepState.Active)]
    [InlineData(ProcessStepState.Blocked)]
    [InlineData(ProcessStepState.Waiting)]
    [InlineData(ProcessStepState.Locked)]
    public void The_legal_term_runs_whatever_holds_an_open_step_up(ProcessStepState state)
        => Warnings(Warns(ProcessGateCodes.Q05), Step(state, due: Today.AddDays(-1)))
            .ShouldHaveSingleItem()
            .ShouldBe(ProcessGateCodes.Q05);

    [Theory]
    [InlineData(ProcessStepState.Done)]
    [InlineData(ProcessStepState.Skipped)]
    public void A_closed_step_warns_of_nothing(ProcessStepState state)
        => Warnings(Warns(ProcessGateCodes.Q05, ProcessGateCodes.Akt), Step(state, due: Today.AddDays(-1)), handoverActEmpty: true)
            .ShouldBeEmpty();

    [Fact]
    public void A_step_with_no_definition_warns_of_nothing()
        => Warnings(null, Step(due: Today.AddDays(-1)), handoverActEmpty: true).ShouldBeEmpty();

    [Fact]
    public void The_act_warning_follows_the_act_and_not_the_calendar()
    {
        var definition = Warns(ProcessGateCodes.Akt);

        Warnings(definition, Step(due: null), handoverActEmpty: true).ShouldHaveSingleItem().ShouldBe(ProcessGateCodes.Akt);
        Warnings(definition, Step(due: Today.AddDays(30)), handoverActEmpty: true).ShouldHaveSingleItem().ShouldBe(ProcessGateCodes.Akt);
        Warnings(definition, Step(due: Today.AddDays(-30)), handoverActEmpty: false).ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_act_does_not_set_off_a_lateness_warning()
        => Warnings(Warns(ProcessGateCodes.Q05), Step(due: Today), handoverActEmpty: true).ShouldBeEmpty();

    [Fact]
    public void A_gate_in_block_mode_is_no_warning_even_past_due()
    {
        var definition = Definition(gates:
        [
            new ProcessGateRef(ProcessGateCodes.Q02, ProcessGateMode.Block),
            new ProcessGateRef(ProcessGateCodes.Akt, ProcessGateMode.Block),
        ]);

        Warnings(definition, Step(due: Today.AddDays(-1)), handoverActEmpty: true).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ProcessGateCodes.Q01)]
    [InlineData(ProcessGateCodes.Q03)]
    [InlineData(ProcessGateCodes.Q04)]
    [InlineData(ProcessGateCodes.Q12)]
    [InlineData(ProcessGateCodes.Q13)]
    public void Other_gates_declared_as_warnings_have_no_rule_and_stay_quiet(string code)
        => Warnings(Warns(code), Step(due: Today.AddDays(-1)), handoverActEmpty: true).ShouldBeEmpty();

    [Fact]
    public void Several_warnings_come_in_the_order_the_step_declares_them()
    {
        var definition = Definition(gates:
        [
            new ProcessGateRef(ProcessGateCodes.Q05, ProcessGateMode.Warn),
            new ProcessGateRef(ProcessGateCodes.Q04, ProcessGateMode.Block),
            new ProcessGateRef(ProcessGateCodes.Akt, ProcessGateMode.Warn),
        ]);

        Warnings(definition, Step(due: Today.AddDays(-1)), handoverActEmpty: true).ShouldBe(LateAndAct);
    }

    [Fact]
    public void Every_lateness_gate_on_a_step_fires_together()
        => Warnings(Warns(AllLateness), Step(due: Today.AddDays(-1))).ShouldBe(AllLateness);

    [Fact]
    public void No_step_is_refused()
        => Should.Throw<ArgumentNullException>(() => ProcessGateReading.Warnings(Warns(), null!, Today, handoverActEmpty: false));

    [Theory]
    [InlineData(ProcessGateCodes.Q03, "jarayon.jshshir_yoq")]
    [InlineData(ProcessGateCodes.Q04, "jarayon.yoriqnoma_yoq")]
    [InlineData(ProcessGateCodes.Q12, "jarayon.akt_yopilmagan")]
    public void A_blocking_gate_reads_as_its_translation_code(string gate, string errorCode)
        => ProcessGateReading.ErrorCodeOf(gate).ShouldBe(errorCode);

    [Theory]
    [InlineData(ProcessGateCodes.Q02)]
    [InlineData(ProcessGateCodes.Q05)]
    [InlineData(ProcessGateCodes.Q06)]
    [InlineData(ProcessGateCodes.Akt)]
    [InlineData(null)]
    public void Any_other_gate_has_no_blocking_text(string? gate)
        => ProcessGateReading.ErrorCodeOf(gate).ShouldBeNull();
}
