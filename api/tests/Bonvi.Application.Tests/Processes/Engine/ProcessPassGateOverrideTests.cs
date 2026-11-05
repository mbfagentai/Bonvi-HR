using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Application.Tests.Processes.Engine;

/// <summary>
/// What a pass does with the overrides it is handed (SPEC-mening-ishlarim §8.1, §8.3, §8.4). Which rows are usable,
/// and spending them, belong to the batch; <c>ProcessGateTests</c> covers those against real PostgreSQL.
/// </summary>
public sealed class ProcessPassGateOverrideTests
{
    private const string Payroll = "r01-9";
    private const string FirstDay = "r02-13";
    private const string TerminationOrder = "ex-8";
    private const string Settlement = "ex-10";
    private const string NoStep = "none";

    /// <summary>The Tashkent date of every pass that does not advance the clock.</summary>
    private static readonly DateOnly PassDay = new(2026, 11, 2);

    private static PassHarness AtPayroll() => PassHarness.FormaliseDirect().Holds("emp.hireOrder");

    private static PassHarness AtFirstDay(Guid? employmentId = null)
        => PassHarness.HireCandidate(employmentId: employmentId)
            .Holds("cand.contacted")
            .Holds("cand.f02")
            .Holds("cand.inF04")
            .Holds("cand.f04Decision", "accept")
            .Holds("cand.f05Step:offerCandidate")
            .Holds("cand.f05Step:docsHandover");

    private static PassHarness AtSettlement(DateOnly? terminationDate)
    {
        var harness = PassHarness.Dismissal().Holds("dis.terminated");
        harness.Subject = new ProcessCaseSubject { TerminationDate = terminationDate };
        return harness;
    }

    private static PassHarness At(string stepId) => stepId switch
    {
        Payroll => AtPayroll(),
        FirstDay => AtFirstDay(),
        Settlement => AtSettlement(PassDay),
        _ => throw new ArgumentOutOfRangeException(nameof(stepId)),
    };

    private static ProcessGateOverride Grant(PassHarness harness, string gate, string? step)
        => ProcessGateOverride.GrantForCase(
            gate,
            harness.Case.Id,
            step,
            harness.Case.EmploymentId,
            harness.Case.CandidateId,
            "Mutaxassis ruxsat berdi",
            PassHarness.User,
            harness.At).Value;

    private static void ShouldBeBlocked(PassHarness harness, string step, string gate)
    {
        harness.Step(step).State.ShouldBe(ProcessStepState.Blocked);
        harness.Step(step).BlockedGateCode.ShouldBe(gate);
    }

    private static void ShouldBeLifted(PassHarness harness, string step)
    {
        harness.Step(step).State.ShouldBe(ProcessStepState.Active);
        harness.Step(step).BlockedGateCode.ShouldBeNull();
    }

    private static void ShouldBeUnspent(PassHarness harness, ProcessGateOverride row)
    {
        row.ConsumedAt.ShouldBeNull();
        row.ConsumedOperation.ShouldBeNull();
        row.IsUsableAt(harness.At).ShouldBeTrue();
    }

    // ───────────────────────── It lifts the block ─────────────────────────

    [Fact]
    public void An_override_lifts_q03_on_the_payroll_step_though_the_employee_still_has_no_pinfl()
    {
        var harness = AtPayroll();
        harness.Pass();
        ShouldBeBlocked(harness, Payroll, ProcessGateCodes.Q03);

        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q03, Payroll)];
        harness.Pass().Changed.ShouldBeTrue();

        harness.Subject.EmployeeHasPinfl.ShouldBeFalse();
        ShouldBeLifted(harness, Payroll);
    }

    [Fact]
    public void An_override_lifts_q04_on_the_first_day_though_the_safety_instruction_is_not_done()
    {
        var harness = AtFirstDay();
        harness.Pass();
        ShouldBeBlocked(harness, FirstDay, ProcessGateCodes.Q04);

        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q04, FirstDay)];
        harness.Pass().Changed.ShouldBeTrue();

        harness.Subject.SafetyInstructionDone.ShouldBeFalse();
        ShouldBeLifted(harness, FirstDay);
    }

    [Fact]
    public void An_override_lifts_q12_on_the_settlement_on_the_termination_date_itself()
    {
        var harness = AtSettlement(PassDay);
        harness.Pass();
        ShouldBeBlocked(harness, Settlement, ProcessGateCodes.Q12);

        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q12, Settlement)];
        harness.Pass().Changed.ShouldBeTrue();

        harness.At.TashkentDate().ShouldBe(PassDay);
        ShouldBeLifted(harness, Settlement);
    }

    [Fact]
    public void An_override_lifts_q12_on_the_settlement_after_the_termination_date()
    {
        var harness = AtSettlement(PassDay.AddDays(-1));
        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q12, Settlement)];

        harness.Pass();

        ShouldBeLifted(harness, Settlement);
    }

    [Fact]
    public void The_right_override_lifts_the_block_whatever_else_the_case_holds()
    {
        var harness = AtPayroll();
        harness.GateOverrides =
        [
            Grant(harness, ProcessGateCodes.Q04, FirstDay),
            Grant(harness, ProcessGateCodes.Q03, Payroll),
        ];

        harness.Pass();

        ShouldBeLifted(harness, Payroll);
    }

    [Fact]
    public void A_lifted_step_stays_lifted_and_later_passes_write_nothing()
    {
        var harness = AtPayroll();
        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q03, Payroll)];
        harness.Pass();

        harness.ShouldBeStable();

        ShouldBeLifted(harness, Payroll);
    }

    [Fact]
    public void The_block_comes_back_once_the_pass_no_longer_gets_the_override()
    {
        var harness = AtPayroll();
        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q03, Payroll)];
        harness.Pass();
        ShouldBeLifted(harness, Payroll);

        harness.GateOverrides = [];
        harness.Pass().Changed.ShouldBeTrue();

        ShouldBeBlocked(harness, Payroll, ProcessGateCodes.Q03);
    }

    // ───────────────────────── Q12 waits for the termination date ─────────────────────────

    [Fact]
    public void A_q12_override_does_not_lift_the_settlement_before_the_termination_date()
    {
        var harness = AtSettlement(PassDay.AddDays(1));
        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q12, Settlement)];

        harness.Pass();

        ShouldBeBlocked(harness, Settlement, ProcessGateCodes.Q12);
    }

    [Fact]
    public void A_q12_override_does_not_lift_the_settlement_with_no_termination_date()
    {
        var harness = AtSettlement(terminationDate: null);
        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q12, Settlement)];

        harness.Pass();

        ShouldBeBlocked(harness, Settlement, ProcessGateCodes.Q12);
    }

    /// <summary>The evening of 2 Nov UTC is already 3 Nov in Tashkent: the last day is a Tashkent date.</summary>
    [Fact]
    public void A_q12_override_starts_to_lift_at_midnight_in_tashkent_not_in_utc()
    {
        var lastDay = PassDay.AddDays(1);
        var harness = AtSettlement(lastDay);
        harness.GateOverrides = [Grant(harness, ProcessGateCodes.Q12, Settlement)];

        harness.Advance(TimeSpan.FromHours(13) + TimeSpan.FromMinutes(50)).Pass();

        harness.At.TashkentDate().ShouldBe(PassDay);
        ShouldBeBlocked(harness, Settlement, ProcessGateCodes.Q12);

        harness.Advance(TimeSpan.FromMinutes(10)).Pass();

        harness.At.TashkentDate().ShouldBe(lastDay);
        DateOnly.FromDateTime(harness.At.UtcDateTime).ShouldBe(PassDay);
        ShouldBeLifted(harness, Settlement);
    }

    // ───────────────────────── Only its own gate on its own step ─────────────────────────

    [Theory]
    [InlineData(Payroll, ProcessGateCodes.Q03, ProcessGateCodes.Q04, Payroll)]
    [InlineData(Payroll, ProcessGateCodes.Q03, ProcessGateCodes.Q03, FirstDay)]
    [InlineData(Payroll, ProcessGateCodes.Q03, ProcessGateCodes.Q03, NoStep)]
    [InlineData(FirstDay, ProcessGateCodes.Q04, ProcessGateCodes.Q03, FirstDay)]
    [InlineData(FirstDay, ProcessGateCodes.Q04, ProcessGateCodes.Q04, Payroll)]
    [InlineData(FirstDay, ProcessGateCodes.Q04, ProcessGateCodes.Q04, NoStep)]
    [InlineData(Settlement, ProcessGateCodes.Q12, ProcessGateCodes.Q13, Settlement)]
    [InlineData(Settlement, ProcessGateCodes.Q12, ProcessGateCodes.Q12, TerminationOrder)]
    [InlineData(Settlement, ProcessGateCodes.Q12, ProcessGateCodes.Q12, NoStep)]
    public void An_override_on_another_gate_or_step_does_not_lift(string blockedStep, string blockingGate, string grantedGate, string grantedStep)
    {
        var harness = At(blockedStep);
        harness.GateOverrides = [Grant(harness, grantedGate, grantedStep == NoStep ? null : grantedStep)];

        harness.Pass();

        ShouldBeBlocked(harness, blockedStep, blockingGate);
    }

    // ───────────────────────── The pass does not spend it ─────────────────────────

    [Fact]
    public void A_pass_that_lifts_the_block_leaves_the_override_unspent()
    {
        var harness = AtPayroll();
        var row = Grant(harness, ProcessGateCodes.Q03, Payroll);
        harness.GateOverrides = [row];

        harness.Pass();
        harness.Pass();

        ShouldBeLifted(harness, Payroll);
        ShouldBeUnspent(harness, row);
    }

    [Fact]
    public void A_pass_after_the_holder_closed_the_lifted_step_leaves_the_override_unspent()
    {
        var harness = AtPayroll();
        var row = Grant(harness, ProcessGateCodes.Q03, Payroll);
        harness.GateOverrides = [row];
        harness.Pass();

        harness.DoneByUser(Payroll);
        harness.Pass();

        harness.Step(Payroll).State.ShouldBe(ProcessStepState.Done);
        ShouldBeUnspent(harness, row);
    }

    [Fact]
    public void A_pass_whose_fact_closes_the_guarded_step_leaves_the_override_unspent()
    {
        var harness = AtFirstDay(Guid.CreateVersion7());
        var row = Grant(harness, ProcessGateCodes.Q04, FirstDay);
        harness.GateOverrides = [row];

        harness.Holds("emp.f11").Pass();

        harness.Step(FirstDay).State.ShouldBe(ProcessStepState.Done);
        harness.Step(FirstDay).Resolution.ShouldBe(ProcessStepResolution.Fact);
        ShouldBeUnspent(harness, row);
    }

    [Fact]
    public void A_pass_that_refuses_the_override_leaves_it_unspent()
    {
        var harness = AtSettlement(PassDay.AddDays(1));
        var row = Grant(harness, ProcessGateCodes.Q12, Settlement);
        harness.GateOverrides = [row];

        harness.Pass();

        ShouldBeBlocked(harness, Settlement, ProcessGateCodes.Q12);
        ShouldBeUnspent(harness, row);
    }
}
