using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Tests.Processes.Engine;

/// <summary>When a case ends (SPEC-mening-ishlarim §7.5), through whole passes on the real templates.</summary>
public sealed class ProcessCaseEndRulesTests
{
    private static void ShouldEnd(PassHarness harness, string? outcome)
    {
        harness.Case.Status.ShouldBe(ProcessCaseStatus.Completed);
        harness.Case.Outcome.ShouldBe(outcome);
        harness.Case.CompletedAt.ShouldNotBeNull();
        harness.Case.Steps.ShouldAllBe(step => step.IsClosed);
    }

    private static void ShouldRun(PassHarness harness)
    {
        harness.Case.Status.ShouldBe(ProcessCaseStatus.Active);
        harness.Case.Outcome.ShouldBeNull();
    }

    // ───────────────────────── hire-request ─────────────────────────

    [Theory]
    [InlineData("rejected")]
    [InlineData("cancelled")]
    public void A_refused_request_ends_with_the_decision_as_the_outcome(string decision)
    {
        var harness = PassHarness.HireRequest().Holds("vr.created").Holds("vr.decided", decision);

        harness.Pass();

        ShouldEnd(harness, decision);
        harness.Step("r02-3").Resolution.ShouldBe(ProcessStepResolution.CaseEnded);
    }

    [Fact]
    public void An_approved_request_runs_on()
    {
        var harness = PassHarness.HireRequest().Holds("vr.created").Holds("vr.decided", "approved");

        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void A_filled_request_ends_as_filled_with_the_closing_step_done()
    {
        var at = PassHarness.T0.AddDays(5);
        var harness = PassHarness.HireRequest().Holds("vr.created").Holds("vr.decided", "approved").Holds("vr.filled", at: at);

        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Filled);
        harness.Step("r02-14").State.ShouldBe(ProcessStepState.Done);
        harness.Step("r02-14").Resolution.ShouldBe(ProcessStepResolution.Auto);
        ProcessStepPayload.ReadInput(harness.Step("r02-14").Payload, "daysToClose").ShouldBe("5");
    }

    // ───────────────────────── hire-candidate ─────────────────────────

    private static PassHarness CandidateAtOffer(string decision, Guid? employmentId = null)
        => PassHarness.HireCandidate(employmentId: employmentId)
            .Holds("cand.contacted").Holds("cand.f02").Holds("cand.inF04").Holds("cand.f04Decision", decision);

    [Theory]
    [InlineData("reject", "2")]
    [InlineData("reserve", "5")]
    public void A_closed_decline_branch_ends_as_declined(string decision, string status)
    {
        var harness = CandidateAtOffer(decision).Holds("cand.status", status);

        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Declined);
        harness.Step("r02-11").IsFactDone.ShouldBeTrue();
        harness.Step("r02-12").Resolution.ShouldBe(ProcessStepResolution.Condition);
        harness.Step("r02-13").Resolution.ShouldBe(ProcessStepResolution.Condition);
    }

    [Fact]
    public void A_decline_branch_still_open_does_not_end_the_case()
    {
        var harness = CandidateAtOffer("reserve").Holds("cand.status", "1");

        harness.Pass();

        ShouldRun(harness);
        harness.Step("r02-11").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void A_manually_closed_decline_ends_as_declined()
    {
        var harness = CandidateAtOffer("reject").Holds("cand.status", "1");
        harness.Pass();

        harness.DoneByUser("r02-11", """{"note":"yozildi"}""");
        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Declined);
    }

    [Fact]
    public void A_notRecommended_check_declines_a_conditional_decision()
    {
        var harness = CandidateAtOffer("conditional").Holds("cand.f03", "notRecommended").Holds("cand.status", "5");

        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Declined);
    }

    [Theory]
    [InlineData("2", "rejected")]
    [InlineData("4", "withdrawn")]
    [InlineData("6", "blacklisted")]
    public void A_gone_candidate_ends_at_any_step_with_the_status_as_the_outcome(string status, string outcome)
    {
        var harness = PassHarness.HireCandidate().Holds("cand.status", status);

        harness.Pass();

        ShouldEnd(harness, outcome);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("5")]
    public void Other_statuses_do_not_end_a_candidate_at_an_early_step(string status)
    {
        var harness = PassHarness.HireCandidate().Holds("cand.status", status);

        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void The_hire_alone_does_not_end_the_candidate_case()
    {
        var harness = PassHarness.HireCandidate(employmentId: Guid.CreateVersion7()).Holds("cand.hired").Holds("cand.status", "3");

        harness.Pass();

        ShouldRun(harness);
        harness.Step("r02-13").IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void The_hire_with_the_first_day_done_ends_as_hired()
    {
        var harness = CandidateAtOffer("accept", Guid.CreateVersion7())
            .Holds("cand.f05Step:offerCandidate")
            .Holds("cand.f05Step:docsHandover")
            .Holds("cand.hired")
            .Holds("emp.f11");

        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Hired);
        harness.Step("r02-13").State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public void The_first_day_done_without_a_hire_does_not_say_hired()
    {
        var harness = CandidateAtOffer("accept", Guid.CreateVersion7())
            .Holds("cand.f05Step:offerCandidate")
            .Holds("cand.f05Step:docsHandover")
            .Holds("emp.f11");

        harness.Pass();

        harness.Case.Outcome.ShouldNotBe(ProcessOutcomes.Hired);
    }

    [Fact]
    public void An_open_optional_step_is_skipped_when_the_case_ends()
    {
        var harness = CandidateAtOffer("accept", Guid.CreateVersion7())
            .Holds("cand.f05Step:offerCandidate")
            .Holds("cand.f05Step:docsHandover")
            .Holds("cand.hired")
            .Holds("emp.f11");

        harness.Pass();

        harness.Step("r02-7").Resolution.ShouldBe(ProcessStepResolution.CaseEnded);
    }

    // ───────────────────────── hire-formalise ─────────────────────────

    [Theory]
    [InlineData("2", "rejected")]
    [InlineData("4", "withdrawn")]
    [InlineData("6", "blacklisted")]
    public void A_formalise_case_with_no_employment_ends_when_the_candidate_is_gone(string status, string outcome)
    {
        var harness = PassHarness.FormaliseFromCandidate().Holds("cand.status", status);

        harness.Pass();

        ShouldEnd(harness, outcome);
    }

    [Fact]
    public void A_formalise_case_with_an_employment_ignores_the_candidate_status()
    {
        var harness = PassHarness.FormaliseFromCandidate(Guid.CreateVersion7()).Holds("cand.status", "2");

        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void A_formalise_case_ends_with_no_outcome_when_every_step_is_closed()
    {
        var harness = PassHarness.FormaliseDirect()
            .Holds("emp.hired").Holds("emp.hireOrder").Holds("emp.attendance").Holds("cand.f05Step:docsHr2");
        harness.Subject = new ProcessCaseSubject { EmployeeHasPinfl = true };
        harness.Pass();

        foreach (var id in new[] { "r01-2", "r01-3", "mm", "r01-6", "r01-7" })
        {
            ShouldRun(harness);
            harness.DoneByUser(id, """{"note":"ok"}""");
            harness.Pass();
        }

        ShouldRun(harness);
        harness.DoneByUser("r01-9");
        harness.Pass();

        ShouldEnd(harness, null);
    }

    // ───────────────────────── probation ─────────────────────────

    [Theory]
    [InlineData(ProbationStatus.Passed, "passed")]
    [InlineData(ProbationStatus.Failed, "failed")]
    [InlineData(ProbationStatus.LeftEarly, "leftEarly")]
    [InlineData(ProbationStatus.Cancelled, "cancelled")]
    public void A_resolved_probation_ends_with_its_status(ProbationStatus status, string outcome)
    {
        var harness = PassHarness.Probation().Holds(ProcessFacts.ProbResolved, ((int)status).ToString(global::System.Globalization.CultureInfo.InvariantCulture));

        harness.Pass();

        ShouldEnd(harness, outcome);
        harness.Step("p03-90").State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public void A_probation_in_progress_runs_on()
    {
        var harness = PassHarness.Probation();

        harness.Pass();

        ShouldRun(harness);
    }

    // ───────────────────────── dismissal ─────────────────────────

    private static PassHarness FullyActed()
        => PassHarness.Dismissal()
            .Holds("dis.f09Opened")
            .Holds("dis.f08Approved")
            .Holds("dis.f09Step:handedOver").Holds("dis.f09Step:received")
            .Holds("dis.f09Step:signHead").Holds("dis.f09Step:hr3Control")
            .Holds("dis.f09Approved")
            .Holds("dis.terminated");

    [Fact]
    public void Ex_11_done_leaves_the_steps_that_only_begin_after_the_termination_open()
    {
        var harness = FullyActed();
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Terminated };

        harness.Pass();

        ShouldRun(harness);
        harness.Step("ex-11").State.ShouldBe(ProcessStepState.Done);
        harness.Step("ex-8b").State.ShouldBe(ProcessStepState.Active);
        harness.Step("ex-9").State.ShouldBe(ProcessStepState.Active);
        harness.Step("ex-10").State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public void A_dismissal_ends_as_terminated_when_the_settlement_steps_are_closed()
    {
        var harness = FullyActed();
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Terminated };
        harness.Pass();

        harness.DoneByUser("ex-8b", """{"removedOn":"2026-11-03"}""");
        harness.DoneByUser("ex-9");
        harness.Pass();
        ShouldRun(harness);

        harness.DoneByUser("ex-10", """{"paidOn":"2026-11-03"}""");
        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Terminated);
    }

    [Fact]
    public void A_fresh_dismissal_with_an_active_employment_is_not_withdrawn()
    {
        var harness = PassHarness.Dismissal();
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Active };

        harness.Pass();
        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void A_cancelled_act_with_an_active_employment_ends_as_withdrawn()
    {
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Active };
        harness.Pass();
        ShouldRun(harness);

        harness.Drops("dis.f09Opened");
        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Withdrawn);
    }

    [Theory]
    [InlineData(EmploymentStatus.Active)]
    [InlineData(EmploymentStatus.Probation)]
    public void A_termination_undone_with_no_act_ends_as_withdrawn(EmploymentStatus workingAgain)
    {
        var harness = PassHarness.Dismissal().Holds("dis.terminated");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Terminated };
        harness.Pass();
        ShouldRun(harness);

        harness.Drops("dis.terminated");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = workingAgain };
        harness.Pass();

        ShouldEnd(harness, ProcessOutcomes.Withdrawn);
    }

    [Fact]
    public void An_open_act_keeps_the_dismissal_running()
    {
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened").Holds("dis.terminated");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Terminated };
        harness.Pass();

        harness.Drops("dis.terminated");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Active };
        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void A_cancelled_act_of_a_terminated_employment_is_not_withdrawn()
    {
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened").Holds("dis.terminated");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Terminated };
        harness.Pass();

        harness.Drops("dis.f09Opened");
        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void An_unknown_employment_status_is_not_withdrawn()
    {
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened");
        harness.Pass();

        harness.Drops("dis.f09Opened");
        harness.Pass();

        ShouldRun(harness);
    }

    [Fact]
    public void A_reopened_withdrawn_case_recomputes_and_runs_on_with_the_new_act()
    {
        var harness = PassHarness.Dismissal().Holds("dis.f09Opened");
        harness.Subject = new ProcessCaseSubject { EmploymentStatus = EmploymentStatus.Active };
        harness.Pass();
        harness.Drops("dis.f09Opened");
        harness.Pass();
        ShouldEnd(harness, ProcessOutcomes.Withdrawn);

        harness.Case.Reopen(harness.At).IsSuccess.ShouldBeTrue();
        harness.Holds("dis.f09Opened");
        harness.Pass();

        ShouldRun(harness);
        harness.Step("ex-2").IsFactDone.ShouldBeTrue();
        harness.Step("ex-3").State.ShouldBe(ProcessStepState.Active);
    }

    // ───────────────────────── after the end ─────────────────────────

    [Theory]
    [InlineData("request")]
    [InlineData("candidate")]
    [InlineData("probation")]
    public void A_completed_case_is_stable_and_ignores_later_facts(string flow)
    {
        var harness = flow switch
        {
            "request" => PassHarness.HireRequest().Holds("vr.created").Holds("vr.decided", "rejected"),
            "candidate" => PassHarness.HireCandidate().Holds("cand.status", "2"),
            _ => PassHarness.Probation().Holds(ProcessFacts.ProbResolved, "2"),
        };
        harness.Pass();
        harness.ShouldBeStable();

        harness.Holds("vr.filled").Drops("vr.created");
        harness.Pass().Changed.ShouldBeFalse();

        harness.Case.Status.ShouldBe(ProcessCaseStatus.Completed);
    }

    [Fact]
    public void A_case_ending_with_a_fact_shows_its_step_as_done_not_as_skipped()
    {
        var harness = PassHarness.HireRequest().Holds("vr.filled");

        harness.Pass();

        harness.Step("r02-14").State.ShouldBe(ProcessStepState.Done);
    }
}
