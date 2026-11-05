using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Processes;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Application.Tests.Processes.Engine;

/// <summary>One pass over one case on the real templates (SPEC-mening-ishlarim §7.4, §7.7, §8.3).</summary>
public sealed class ProcessPassTests
{
    private static readonly DateTimeOffset T0 = PassHarness.T0;

    private const string Accepted = "accept";

    /// <summary>r02-5 … r02-10 closed by facts with this council decision.</summary>
    private static PassHarness CandidateAtDecision(string decision, bool legacy = false, Guid? employmentId = null)
        => PassHarness.HireCandidate(legacy, employmentId)
            .Holds("cand.contacted")
            .Holds("cand.f02")
            .Holds("cand.inF04")
            .Holds("cand.f04Decision", decision);

    // ───────────────────────── A fact wins over order ─────────────────────────

    [Fact]
    public void A_later_step_closes_while_an_earlier_one_is_open()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.f02", at: T0.AddHours(-3));

        harness.Pass().Changed.ShouldBeTrue();

        harness.Step("r02-5").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r02-6").State.ShouldBe(ProcessStepState.Done);
        harness.Step("r02-6").Resolution.ShouldBe(ProcessStepResolution.Fact);
    }

    [Fact]
    public void Done_at_is_the_time_of_the_fact_not_of_the_pass()
    {
        var factTime = T0.AddDays(-2);
        var harness = PassHarness.HireCandidate().Holds("cand.contacted", at: factTime);

        harness.Pass();

        harness.Step("r02-5").DoneAt.ShouldBe(factTime);
    }

    [Fact]
    public void A_later_steps_closure_opens_the_step_behind_it_in_the_same_pass()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.f02");

        harness.Pass();

        harness.Step("r02-9").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void An_auto_step_is_done_with_the_auto_resolution()
    {
        var harness = PassHarness.Dismissal();

        harness.Pass();

        harness.Step("ex-1").State.ShouldBe(ProcessStepState.Done);
        harness.Step("ex-1").Resolution.ShouldBe(ProcessStepResolution.Auto);
        harness.Step("ex-1").DoneAt.ShouldBe(harness.Case.CreatedAt);
    }

    [Fact]
    public void All_of_needs_every_fact_and_takes_the_latest_time()
    {
        var later = T0.AddHours(-1);
        var harness = PassHarness.Dismissal()
            .Holds("dis.f09Opened")
            .Holds("dis.f09Step:handedOver", at: T0.AddHours(-5));

        harness.Pass();
        harness.Step("ex-4").State.ShouldBe(ProcessStepState.Active);

        harness.Holds("dis.f09Step:received", at: later);
        harness.Pass();

        harness.Step("ex-4").State.ShouldBe(ProcessStepState.Done);
        harness.Step("ex-4").DoneAt.ShouldBe(later);
    }

    [Fact]
    public void A_fact_without_a_time_keeps_the_time_it_was_first_seen()
    {
        var harness = PassHarness.HireRequest().HoldsWithoutTime("vr.created");

        harness.Pass();
        var first = harness.Step("r02-1").DoneAt;
        first.ShouldBe(harness.At);

        harness.Pass().Changed.ShouldBeFalse();

        harness.Step("r02-1").DoneAt.ShouldBe(first);
    }

    [Fact]
    public void A_newer_fact_time_is_written()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted", at: T0.AddDays(-2));
        harness.Pass();

        var newer = T0.AddDays(-1);
        harness.Holds("cand.contacted", at: newer);

        harness.Pass().Changed.ShouldBeTrue();
        harness.Step("r02-5").DoneAt.ShouldBe(newer);
    }

    [Fact]
    public void A_fact_of_a_subject_the_case_does_not_have_does_not_hold()
    {
        var harness = PassHarness.HireCandidate().Holds("emp.f11");

        harness.Pass();

        harness.Step("r02-13").IsClosed.ShouldBeFalse();
    }

    // ───────────────────────── Revert ─────────────────────────

    [Fact]
    public void A_fact_that_disappears_reverts_the_step_and_clears_the_closure()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted", "2");
        harness.Pass();
        harness.Step("r02-5").IsFactDone.ShouldBeTrue();

        harness.Drops("cand.contacted");
        harness.Pass().Changed.ShouldBeTrue();

        var step = harness.Step("r02-5");
        step.State.ShouldBe(ProcessStepState.Active);
        step.Resolution.ShouldBeNull();
        step.DoneAt.ShouldBeNull();
        step.Payload.ShouldBeNull();
    }

    [Fact]
    public void A_reverted_step_with_an_open_prerequisite_goes_back_to_locked()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.f02");
        harness.Pass();

        harness.Drops("cand.f02");
        harness.Pass();

        harness.Step("r02-6").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void A_revert_locks_the_steps_behind_it_again()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted").Holds("cand.f02");
        harness.Pass();
        harness.Step("r02-9").State.ShouldBe(ProcessStepState.Active);

        harness.Drops("cand.f02");
        harness.Pass();

        harness.Step("r02-6").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r02-9").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void An_auto_step_reverts_too()
    {
        var harness = PassHarness.Dismissal().Holds("dis.f09Approved");
        harness.Pass();
        harness.Step("ex-7").Resolution.ShouldBe(ProcessStepResolution.Auto);

        harness.Drops("dis.f09Approved");
        harness.Pass();

        harness.Step("ex-7").IsClosed.ShouldBeFalse();
    }

    // ───────────────────────── A person's word stays ─────────────────────────

    [Fact]
    public void A_manual_done_step_with_a_completion_fact_never_reverts()
    {
        var harness = PassHarness.HireCandidate();
        harness.Pass();
        harness.DoneByUser("r02-5", """{"result":"reached"}""");
        var doneAt = harness.Step("r02-5").DoneAt;

        harness.Pass();

        var step = harness.Step("r02-5");
        step.State.ShouldBe(ProcessStepState.Done);
        step.Resolution.ShouldBe(ProcessStepResolution.Manual);
        step.DoneBy.ShouldBe(PassHarness.User);
        step.DoneAt.ShouldBe(doneAt);
    }

    [Fact]
    public void A_later_fact_does_not_overwrite_a_manual_done()
    {
        var harness = PassHarness.HireCandidate();
        harness.Pass();
        harness.DoneByUser("r02-5", """{"result":"reached"}""");
        harness.Pass();

        harness.Holds("cand.contacted", at: T0.AddHours(2));
        harness.Pass().Changed.ShouldBeFalse();

        harness.Step("r02-5").Resolution.ShouldBe(ProcessStepResolution.Manual);
        harness.Step("r02-5").Payload.ShouldBe("""{"result":"reached"}""");
    }

    [Fact]
    public void A_manual_step_with_no_fact_stays_done()
    {
        var harness = PassHarness.FormaliseFromCandidate().Holds("cand.f05Step:docsHr2");
        harness.Pass();
        harness.DoneByUser("r01-3");

        harness.Pass();

        harness.Step("r01-3").State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public void A_user_skip_stays_while_the_condition_changes()
    {
        var harness = CandidateAtDecision("conditional");
        harness.Pass();
        harness.Case.SkipStepByUser("r02-8", PassHarness.User, legacy: false, payload: null, harness.At).IsSuccess.ShouldBeTrue();

        harness.Holds("cand.f04Decision", Accepted);
        harness.Pass();

        harness.Step("r02-8").Resolution.ShouldBe(ProcessStepResolution.UserSkip);
    }

    [Fact]
    public void After_undo_the_pass_recomputes_the_step()
    {
        var harness = PassHarness.HireCandidate();
        harness.Pass();
        harness.DoneByUser("r02-5");
        harness.Holds("cand.contacted");
        harness.Pass();

        harness.Case.UndoStep("r02-5", PassHarness.User, asManager: false, harness.At).IsSuccess.ShouldBeTrue();
        harness.Pass();

        harness.Step("r02-5").IsFactDone.ShouldBeTrue();
    }

    [Fact]
    public void A_manual_decision_drives_the_conditional_branch()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted").Holds("cand.f02").Holds("cand.inF04");
        harness.Pass();

        harness.DoneByUser("r02-10", """{"decision":"conditional","councilDate":"2026-11-05"}""");
        harness.Pass();

        harness.Step("r02-8").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r02-11").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void A_manual_accept_skips_the_check_and_opens_the_offer()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted").Holds("cand.f02").Holds("cand.inF04");
        harness.Pass();

        harness.DoneByUser("r02-10", """{"decision":"accept","councilDate":"2026-11-05"}""");
        harness.Pass();

        harness.Step("r02-8").Resolution.ShouldBe(ProcessStepResolution.Condition);
        harness.Step("r02-11").State.ShouldBe(ProcessStepState.Active);
    }

    // ───────────────────────── Locked / Active / conditions ─────────────────────────

    [Fact]
    public void Steps_with_open_prerequisites_are_locked_and_the_first_is_active()
    {
        var harness = PassHarness.HireCandidate();

        harness.Pass();

        harness.Step("r02-5").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r02-6").State.ShouldBe(ProcessStepState.Locked);
        harness.Step("r02-13").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void Skipped_counts_as_a_met_prerequisite()
    {
        var harness = CandidateAtDecision(Accepted);

        harness.Pass();

        harness.Step("r02-8").State.ShouldBe(ProcessStepState.Skipped);
        harness.Step("r02-11").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void An_optional_prerequisite_never_locks()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted");
        harness.Pass();
        harness.Step("r02-7").State.ShouldBe(ProcessStepState.Locked);

        var definition = harness.Definition;
        harness.Definition = definition with
        {
            Steps = definition.Steps
                .Select(step => step.Id == "r02-9" ? step with { Prereqs = ["r02-7"] } : step)
                .ToList(),
        };

        harness.Pass();

        harness.Step("r02-9").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void An_unknown_when_stays_locked_even_with_met_prerequisites()
    {
        var harness = PassHarness.HireCandidate().Holds("cand.contacted").Holds("cand.f02").Holds("cand.inF04");

        harness.Pass();

        harness.Step("r02-10").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r02-8").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void A_false_when_skips_by_condition_and_a_changed_value_reopens()
    {
        var harness = CandidateAtDecision(Accepted);
        harness.Pass();
        harness.Step("r02-8").Resolution.ShouldBe(ProcessStepResolution.Condition);

        harness.Holds("cand.f04Decision", "conditional");
        harness.Pass();

        harness.Step("r02-8").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r02-8").Resolution.ShouldBeNull();
    }

    [Fact]
    public void A_done_step_whose_when_turns_false_is_skipped()
    {
        var harness = CandidateAtDecision("conditional").Holds("cand.f03", "ok");
        harness.Pass();
        harness.Step("r02-8").IsFactDone.ShouldBeTrue();

        harness.Holds("cand.f04Decision", Accepted);
        harness.Pass();

        harness.Step("r02-8").State.ShouldBe(ProcessStepState.Skipped);
        harness.Step("r02-8").Resolution.ShouldBe(ProcessStepResolution.Condition);
        harness.Step("r02-8").Payload.ShouldBeNull();
    }

    [Theory]
    [InlineData("false", ProcessStepState.Active)]
    [InlineData("true", ProcessStepState.Skipped)]
    public void A_boolean_when_compares_with_the_fact_source_text(string day7Pass, ProcessStepState expected)
    {
        var harness = ProbationOf(days: 90).Holds("prob.f06").Holds("prob.f06Group1").Holds("prob.f06Step:day7", day7Pass);

        harness.Pass();

        harness.Step("p03-14").State.ShouldBe(expected);
    }

    // ───────────────────────── Legacy ─────────────────────────

    [Fact]
    public void Legacy_any_of_closes_a_step_in_a_legacy_case_only()
    {
        var legacy = PassHarness.HireCandidate(legacy: true).Holds("cand.stage:invited");
        var normal = PassHarness.HireCandidate(legacy: false).Holds("cand.stage:invited");

        legacy.Pass();
        normal.Pass();

        legacy.Step("r02-9").State.ShouldBe(ProcessStepState.Done);
        normal.Step("r02-9").IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void A_legacy_council_stage_answers_unknown_and_branches_as_accept()
    {
        var harness = PassHarness.HireCandidate(legacy: true)
            .Holds("cand.contacted").Holds("cand.f02").Holds("cand.inF04").Holds("cand.stage:council");

        harness.Pass();

        var decision = harness.Step("r02-10");
        decision.State.ShouldBe(ProcessStepState.Done);
        ProcessStepPayload.ReadInput(decision.Payload, "value").ShouldBe(ProcessCaseFacts.UnknownDecision);
        harness.Step("r02-8").Resolution.ShouldBe(ProcessStepResolution.Condition);
        harness.Step("r02-11").State.ShouldBe(ProcessStepState.Active);
    }

    // ───────────────────────── Blocked (§8.3) ─────────────────────────

    [Fact]
    public void Q03_blocks_the_payroll_step_until_the_employee_has_a_pinfl()
    {
        var harness = PassHarness.FormaliseDirect().Holds("emp.hireOrder");

        harness.Pass();

        harness.Step("r01-9").State.ShouldBe(ProcessStepState.Blocked);
        harness.Step("r01-9").BlockedGateCode.ShouldBe(ProcessGateCodes.Q03);

        harness.Subject = harness.Subject with { EmployeeHasPinfl = true };
        harness.Pass();

        harness.Step("r01-9").State.ShouldBe(ProcessStepState.Active);
        harness.Step("r01-9").BlockedGateCode.ShouldBeNull();
    }

    [Fact]
    public void A_blocked_step_cannot_be_closed_by_hand()
    {
        var harness = PassHarness.FormaliseDirect().Holds("emp.hireOrder");
        harness.Pass();

        harness.Case.CompleteStepManually("r01-9", PassHarness.User, null, null, harness.At).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_locked_step_is_not_blocked_even_when_its_gate_fails()
    {
        var harness = PassHarness.FormaliseDirect();

        harness.Pass();

        harness.Step("r01-9").State.ShouldBe(ProcessStepState.Locked);
        harness.Step("r01-9").BlockedGateCode.ShouldBeNull();
    }

    [Fact]
    public void Gates_not_enforced_never_block()
    {
        var harness = PassHarness.FormaliseDirect().Holds("emp.hireOrder");
        harness.GatesEnforced = false;

        harness.Pass();

        harness.Step("r01-9").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void A_legacy_case_never_blocks()
    {
        var harness = PassHarness.FormaliseDirect(legacy: true).Holds("emp.hireOrder");

        harness.Pass();

        harness.Step("r01-9").State.ShouldBe(ProcessStepState.Active);
    }

    private static PassHarness AtFirstDay(Guid? employmentId = null)
        => CandidateAtDecision(Accepted, employmentId: employmentId)
            .Holds("cand.f05Step:offerCandidate")
            .Holds("cand.f05Step:docsHandover");

    [Fact]
    public void Q04_blocks_the_first_day_until_the_safety_instruction_is_done()
    {
        var harness = AtFirstDay();

        harness.Pass();

        harness.Step("r02-13").State.ShouldBe(ProcessStepState.Blocked);
        harness.Step("r02-13").BlockedGateCode.ShouldBe(ProcessGateCodes.Q04);

        harness.Subject = harness.Subject with { SafetyInstructionDone = true };
        harness.Pass();

        harness.Step("r02-13").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void No_step_is_ever_written_as_waiting_for_another_case()
    {
        var harness = AtFirstDay();

        harness.Pass();

        harness.Case.Steps.ShouldNotContain(step => step.State == ProcessStepState.Waiting);
    }

    [Fact]
    public void A_fact_closes_a_step_past_its_failing_gate()
    {
        var harness = AtFirstDay(Guid.CreateVersion7()).Holds("emp.f11");

        harness.Pass();

        harness.Step("r02-13").State.ShouldBe(ProcessStepState.Done);
        harness.Step("r02-13").BlockedGateCode.ShouldBeNull();
    }

    [Fact]
    public void Q12_blocks_the_final_settlement_until_the_act_is_approved()
    {
        var harness = PassHarness.Dismissal().Holds("dis.terminated");

        harness.Pass();

        harness.Step("ex-8").State.ShouldBe(ProcessStepState.Done);
        harness.Step("ex-10").State.ShouldBe(ProcessStepState.Blocked);
        harness.Step("ex-10").BlockedGateCode.ShouldBe(ProcessGateCodes.Q12);
        harness.Step("ex-9").State.ShouldBe(ProcessStepState.Active);

        harness.Holds("dis.f09Approved");
        harness.Pass();

        harness.Step("ex-10").State.ShouldBe(ProcessStepState.Active);
    }

    /// <summary>Q12 on ex-8 and Q13 on ex-3 are the Terminate hook's job (§8.2): the holder must see the task.</summary>
    [Fact]
    public void Gates_enforced_by_a_hook_leave_the_step_active()
    {
        var atOrder = PassHarness.Dismissal().Holds("dis.f09Approved").Holds("dis.f08Approved");
        var fresh = PassHarness.Dismissal();

        atOrder.Pass();
        fresh.Pass();

        atOrder.Step("ex-8").State.ShouldBe(ProcessStepState.Active);
        fresh.Step("ex-3").State.ShouldBe(ProcessStepState.Active);
    }

    // ───────────────────────── Holder (§7.7) ─────────────────────────

    [Fact]
    public void An_office_role_holds_its_step_without_a_person()
    {
        var harness = PassHarness.Dismissal();

        harness.Pass();

        harness.Step("ex-2").HolderRole.ShouldBe("hr3");
        harness.Step("ex-2").HolderEmployeeId.ShouldBeNull();
    }

    [Fact]
    public void A_system_step_has_no_holder()
    {
        var harness = PassHarness.Dismissal();

        harness.Pass();

        harness.Step("ex-1").HolderRole.ShouldBeNull();
    }

    [Fact]
    public void A_department_head_with_a_user_holds_the_step()
    {
        var head = Guid.CreateVersion7();
        var harness = PassHarness.Dismissal();
        harness.Subject = new ProcessCaseSubject { DepartmentHeadWithUser = head };

        harness.Pass();

        harness.Step("ex-5").HolderRole.ShouldBe(ProcessRoles.DepartmentHead);
        harness.Step("ex-5").HolderEmployeeId.ShouldBe(head);
    }

    [Fact]
    public void Without_a_user_a_person_role_falls_back()
    {
        var harness = PassHarness.Dismissal();

        harness.Pass();

        harness.Step("ex-5").HolderRole.ShouldBe("hr3");
        harness.Step("ex-5").HolderEmployeeId.ShouldBeNull();
        harness.Step("ex-4").HolderRole.ShouldBe("hr3");
    }

    [Fact]
    public void The_subject_employee_with_a_user_holds_the_step()
    {
        var employee = Guid.CreateVersion7();
        var harness = PassHarness.Dismissal();
        harness.Subject = new ProcessCaseSubject { SubjectEmployeeWithUser = employee };

        harness.Pass();

        harness.Step("ex-4").HolderRole.ShouldBe(ProcessRoles.SubjectEmployee);
        harness.Step("ex-4").HolderEmployeeId.ShouldBe(employee);
    }

    [Fact]
    public void An_open_step_follows_a_new_head_and_a_closed_one_keeps_the_old()
    {
        var oldHead = Guid.CreateVersion7();
        var newHead = Guid.CreateVersion7();
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened").Holds("dis.f09Step:handedOver").Holds("dis.f09Step:received");
        harness.Subject = new ProcessCaseSubject { DepartmentHeadWithUser = oldHead };
        harness.Pass();

        harness.Holds("dis.f09Step:signHead");
        harness.Pass();
        harness.Step("ex-5").State.ShouldBe(ProcessStepState.Done);

        harness.Subject = new ProcessCaseSubject { DepartmentHeadWithUser = newHead };
        harness.Pass();

        harness.Step("ex-5").HolderEmployeeId.ShouldBe(oldHead);
    }

    // ───────────────────────── Case fields and dues ─────────────────────────

    [Fact]
    public void The_department_is_refreshed_from_the_subject()
    {
        var department = Guid.CreateVersion7();
        var harness = PassHarness.Dismissal();
        harness.Subject = new ProcessCaseSubject { DepartmentId = department };

        harness.Pass();

        harness.Case.DepartmentId.ShouldBe(department);
    }

    [Fact]
    public void Due_counts_working_days_from_the_case_start()
    {
        var harness = PassHarness.HireRequest();

        harness.Pass();

        harness.Step("r02-3").DueDate.ShouldBe(new DateOnly(2026, 11, 4));
    }

    [Fact]
    public void Due_from_a_step_anchor_uses_the_closure_of_the_same_pass()
    {
        var harness = PassHarness.HireRequest().Holds("vr.created").Holds("vr.decided", "approved", at: T0.AddDays(1));

        harness.Pass();

        // Decided Tue 3 Nov + 1 working day.
        harness.Step("r02-4").DueDate.ShouldBe(new DateOnly(2026, 11, 4));
    }

    [Fact]
    public void A_due_earlier_than_the_case_start_is_floored_outside_legacy()
    {
        var harness = PassHarness.Dismissal(manualLastDay: new DateOnly(2026, 11, 3));

        harness.Pass();

        harness.Step("ex-4").DueDate.ShouldBe(PassHarness.StartedOn);
    }

    [Fact]
    public void A_manual_last_day_outranks_the_f08_and_the_termination()
    {
        var manual = new DateOnly(2026, 11, 20);
        var harness = PassHarness.Dismissal(manualLastDay: manual).Holds(ProcessFacts.DisF08LeaveDate, "2026-11-25");
        harness.Subject = new ProcessCaseSubject { TerminationDate = new DateOnly(2026, 11, 27) };

        harness.Pass();

        harness.Case.AnchorDate.ShouldBe(manual);
    }

    [Fact]
    public void The_dismissal_anchor_is_the_f08_date_then_the_termination()
    {
        var harness = PassHarness.Dismissal().Holds(ProcessFacts.DisF08LeaveDate, "2026-11-25");
        harness.Subject = new ProcessCaseSubject { TerminationDate = new DateOnly(2026, 11, 27) };

        harness.Pass();
        harness.Case.AnchorDate.ShouldBe(new DateOnly(2026, 11, 25));
        harness.Step("ex-3").DueDate.ShouldBe(new DateOnly(2026, 11, 25));

        harness.Drops(ProcessFacts.DisF08LeaveDate);
        harness.Pass();
        harness.Case.AnchorDate.ShouldBe(new DateOnly(2026, 11, 27));
    }

    [Fact]
    public void A_direct_hire_anchors_on_the_hire_date()
    {
        var hired = new DateOnly(2026, 11, 10);
        var harness = PassHarness.FormaliseDirect();
        harness.Subject = new ProcessCaseSubject { HireDate = hired };

        harness.Pass();

        harness.Case.AnchorDate.ShouldBe(hired);
    }

    [Fact]
    public void A_formalise_case_prefers_the_f05_date_over_its_seed_and_the_hire()
    {
        var seed = new DateOnly(2026, 11, 9);
        var harness = PassHarness.FormaliseFromCandidate(anchor: seed);
        harness.Subject = new ProcessCaseSubject { HireDate = new DateOnly(2026, 11, 11) };

        harness.Pass();
        harness.Case.AnchorDate.ShouldBe(seed);

        harness.Holds(ProcessFacts.CandF05StartDate, "2026-11-10");
        harness.Pass();
        harness.Case.AnchorDate.ShouldBe(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void The_offer_start_date_feeds_the_handover_due_from_the_manual_input()
    {
        var harness = CandidateAtDecision(Accepted);
        harness.Pass();

        harness.DoneByUser("r02-11", """{"startDate":"2026-11-16"}""");
        harness.Pass();

        harness.Step("r02-12").DueDate.ShouldBe(new DateOnly(2026, 11, 15));
    }

    // ───────────────────────── Probation cut-off (§3.6) ─────────────────────────

    private static PassHarness ProbationOf(int days)
    {
        var harness = PassHarness.Probation();
        harness.Subject = new ProcessCaseSubject
        {
            ProbationStart = PassHarness.StartedOn,
            ProbationEnd = PassHarness.StartedOn.AddDays(days),
        };

        return harness;
    }

    [Fact]
    public void A_check_in_due_in_the_last_five_days_is_skipped_by_condition()
    {
        var harness = ProbationOf(days: 30);

        harness.Pass();

        harness.Step("p03-30").Resolution.ShouldBe(ProcessStepResolution.Condition);
        harness.Step("p03-60").Resolution.ShouldBe(ProcessStepResolution.Condition);
        harness.Step("p03-7").IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void The_cut_off_comes_before_an_unknown_when()
    {
        var harness = ProbationOf(days: 15);

        harness.Pass();

        harness.Step("p03-14").Resolution.ShouldBe(ProcessStepResolution.Condition);
    }

    [Fact]
    public void A_cut_off_check_in_that_has_its_fact_is_done()
    {
        var harness = ProbationOf(days: 30).Holds("prob.f06Step:day30");

        harness.Pass();

        harness.Step("p03-30").IsFactDone.ShouldBeTrue();
    }

    // ───────────────────────── Links ─────────────────────────

    [Fact]
    public void The_proof_of_a_blank_step_links_as_a_submission()
    {
        var f09 = Guid.CreateVersion7();
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened", linked: f09);

        harness.Pass();

        harness.Step("ex-2").LinkedBlankSubmissionId.ShouldBe(f09);
        harness.Step("ex-2").LinkedRecordId.ShouldBeNull();
    }

    [Fact]
    public void An_open_step_links_the_blank_it_is_working_on()
    {
        var f09 = Guid.CreateVersion7();
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened", linked: f09);

        harness.Pass();

        harness.Step("ex-4").IsClosed.ShouldBeFalse();
        harness.Step("ex-4").LinkedBlankSubmissionId.ShouldBe(f09);
    }

    [Fact]
    public void A_form_links_as_a_record_because_it_is_no_blank_submission()
    {
        var form = Guid.CreateVersion7();
        var harness = PassHarness.HireCandidate().Holds("cand.f02", linked: form);

        harness.Pass();

        harness.Step("r02-6").LinkedRecordId.ShouldBe(form);
        harness.Step("r02-6").LinkedBlankSubmissionId.ShouldBeNull();
    }

    // ───────────────────────── Offer signal ─────────────────────────

    [Fact]
    public void The_accepted_offer_is_reported_on_every_pass_for_the_starter()
    {
        var harness = AtFirstDay();

        harness.Pass().OfferAccepted.ShouldBeTrue();
        harness.Pass().OfferAccepted.ShouldBeTrue();
    }

    [Fact]
    public void A_manually_closed_offer_on_the_accept_branch_is_reported()
    {
        var harness = CandidateAtDecision(Accepted);
        harness.Pass().OfferAccepted.ShouldBeFalse();

        harness.DoneByUser("r02-11", """{"startDate":"2026-11-16"}""");

        harness.Pass().OfferAccepted.ShouldBeTrue();
    }

    [Fact]
    public void An_open_offer_is_not_reported()
        => CandidateAtDecision(Accepted).Pass().OfferAccepted.ShouldBeFalse();

    // ───────────────────────── Nothing changed → nothing written ─────────────────────────

    public static TheoryData<string> Scenarios() =>
    [
        "request fresh", "request approved",
        "candidate fresh", "candidate conditional", "candidate blocked", "candidate legacy",
        "formalise direct", "formalise candidate", "formalise blocked",
        "probation short", "probation running",
        "dismissal manual", "dismissal terminated", "dismissal blocked",
    ];

    private static PassHarness Scenario(string name) => name switch
    {
        "request fresh" => PassHarness.HireRequest(),
        "request approved" => PassHarness.HireRequest().Holds("vr.created").Holds("vr.decided", "approved").Holds("vr.firstCandidate"),
        "candidate fresh" => PassHarness.HireCandidate(),
        "candidate conditional" => CandidateAtDecision("conditional").Holds("cand.f03", "watch"),
        "candidate blocked" => AtFirstDay(),
        "candidate legacy" => PassHarness.HireCandidate(legacy: true).Holds("cand.contacted").Holds("cand.stage:council"),
        "formalise direct" => PassHarness.FormaliseDirect().Holds("emp.hired").Holds("emp.hireOrder", "B-17").Holds("emp.attendance"),
        "formalise candidate" => PassHarness.FormaliseFromCandidate(Guid.CreateVersion7()).Holds("cand.f05Step:docsHr2").Holds(ProcessFacts.CandF05StartDate, "2026-11-10"),
        "formalise blocked" => PassHarness.FormaliseDirect().Holds("emp.hireOrder"),
        "probation short" => ProbationOf(days: 30),
        "probation running" => ProbationOf(days: 90).Holds("prob.f06").Holds("prob.f06Group1").Holds("prob.f06Step:day7", "false", linked: Guid.CreateVersion7()),
        "dismissal manual" => PassHarness.Dismissal(manualLastDay: new DateOnly(2026, 11, 20)),
        "dismissal terminated" => PassHarness.Dismissal().Holds("dis.f09Opened", linked: Guid.CreateVersion7()).Holds("dis.f09Approved").Holds("dis.terminated"),
        "dismissal blocked" => PassHarness.Dismissal().Holds("dis.terminated"),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void A_second_pass_after_the_round_trip_reports_no_change(string name)
    {
        var harness = Scenario(name);

        harness.Pass().Changed.ShouldBeTrue();

        harness.ShouldBeStable();
    }

    [Fact]
    public void A_fact_value_payload_re_spaced_by_jsonb_is_not_a_change()
    {
        var harness = CandidateAtDecision("conditional");
        harness.Pass();

        harness.SaveAndReload();

        harness.Step("r02-10").Payload.ShouldBe("""{"value": "conditional"}""");
        harness.Pass().Changed.ShouldBeFalse();
    }

    [Fact]
    public void A_case_that_is_not_active_is_left_alone()
    {
        var harness = PassHarness.HireRequest();
        harness.Case.Cancel("sabab", T0).IsSuccess.ShouldBeTrue();

        var result = harness.Pass();

        result.Changed.ShouldBeFalse();
        harness.Step("r02-1").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void A_deleted_subject_cancels_the_case()
    {
        var harness = PassHarness.HireCandidate();
        harness.Subject = new ProcessCaseSubject { Exists = false };

        harness.Pass().Changed.ShouldBeTrue();

        harness.Case.Status.ShouldBe(ProcessCaseStatus.Cancelled);
        harness.Case.CancelReason.ShouldBe(ProcessCase.SubjectDeletedReason);
        harness.ShouldBeStable();
    }

    // ───────────────────────── Every template, every step ─────────────────────────

    [Theory]
    [InlineData(ProcessCodes.HireRequest)]
    [InlineData(ProcessCodes.HireCandidate)]
    [InlineData(ProcessCodes.HireFormalise)]
    [InlineData(ProcessCodes.Probation)]
    [InlineData(ProcessCodes.Dismissal)]
    public void A_fresh_case_passes_without_a_problem_on_its_real_template(string code)
    {
        var harness = code switch
        {
            ProcessCodes.HireRequest => PassHarness.HireRequest(),
            ProcessCodes.HireCandidate => PassHarness.HireCandidate(),
            ProcessCodes.HireFormalise => PassHarness.FormaliseDirect(),
            ProcessCodes.Probation => ProbationOf(days: 90),
            _ => PassHarness.Dismissal(),
        };

        harness.Pass();

        harness.Case.Status.ShouldBe(ProcessCaseStatus.Active);
        harness.Case.Steps.Count.ShouldBe(harness.Definition.Steps.Count);
    }
}
