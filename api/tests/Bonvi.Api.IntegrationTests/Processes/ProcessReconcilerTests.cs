using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The reconciler against real PostgreSQL (docs/SPEC-mening-ishlarim.md §3.2, §6.9, §7.3–§7.5): starters, batch
/// reconcile and the pass around them, driven directly. <c>.Flows.cs</c> walks each flow end to end, <c>.Inline.cs</c> covers the job and the inline wrapper.
/// </summary>
/// <remarks>
/// A pass sweeps the whole database and the database is shared by the tests of the class, so each test
/// asserts only on its own subjects. Run counts are asserted only by tests that run a pass first to settle
/// what the others left.
/// </remarks>
public sealed partial class ProcessReconcilerTests : IClassFixture<BonviApiFactory>
{
    private const string F08Answers = """{"leaveDate":"2026-12-31"}""";

    private readonly BonviApiFactory _factory;

    public ProcessReconcilerTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => _factory.Services.GetRequiredService<IClock>().Today;

    // ────────────────────────────────────────────────────────────
    //  Dormant, lock, change detection
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dormant_engine_starts_nothing()
    {
        await SetStartedOnAsync(null);
        var request = await VacancyRequestAsync();

        var result = await RunAsync();

        result.Status.ShouldBe(ProcessReconcileStatus.Dormant);
        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).ShouldBeNull();
    }

    [Fact]
    public async Task Busy_lock_is_an_answer_and_the_pass_writes_nothing()
    {
        await StartEngineAsync();
        var request = await VacancyRequestAsync();

        var result = await DbAsync(db => db.ExecuteInTransactionAsync(
            async token =>
            {
                (await new ProcessEngineLock(db).TryAcquireAsync(token)).ShouldBeTrue();
                return await RunAsync();
            },
            Ct));

        result.Status.ShouldBe(ProcessReconcileStatus.Busy);
        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).ShouldBeNull();
    }

    [Fact]
    public async Task Second_pass_writes_nothing_and_leaves_the_watermark()
    {
        await StartEngineAsync();
        await CandidateAsync(await VacancyAsync());
        await HireAsync();

        var first = await RunAsync();
        first.Status.ShouldBe(ProcessReconcileStatus.Finished);
        first.Started.ShouldBeGreaterThan(0);
        var watermark = await WatermarkAsync();
        watermark.ShouldNotBeNullOrEmpty();

        await SqlAsync($"UPDATE system_settings SET value = 'kept' WHERE key = {SystemSettingKeys.ProcessesReconcileWatermark}");

        var second = await RunAsync();

        second.ShouldBe(new ProcessReconcileResult(ProcessReconcileStatus.Finished, 0, 0));
        (await WatermarkAsync()).ShouldBe("kept");
    }

    // ────────────────────────────────────────────────────────────
    //  hire-request, hire-candidate
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Candidate_case_takes_the_latest_active_request_of_its_vacancy_as_parent()
    {
        await StartEngineAsync();
        var vacancy = await VacancyAsync();
        var older = await VacancyRequestAsync(vacancy);
        await RunAsync();

        var latest = await VacancyRequestAsync(vacancy);
        var candidate = await CandidateAsync(vacancy);
        await RunAsync();

        var olderCase = await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(older.Id));
        var latestCase = await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(latest.Id));
        var candidateCase = await CandidateCaseAsync(candidate);

        latestCase.Status.ShouldBe(ProcessCaseStatus.Active);
        latestCase.DepartmentId.ShouldBe(latest.DepartmentId);
        latestCase.StartedOn.ShouldBe(Today);
        olderCase.Id.ShouldNotBe(latestCase.Id);

        candidateCase.ParentCaseId.ShouldBe(latestCase.Id);
        candidateCase.DepartmentId.ShouldBe(latest.DepartmentId);
        candidateCase.Legacy.ShouldBeFalse();
        candidateCase.FindStep("r02-5")!.State.ShouldBe(ProcessStepState.Active);
        candidateCase.FindStep("r02-6")!.State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public async Task Candidate_with_no_request_starts_with_no_parent()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());

        await RunAsync();

        var candidateCase = await CandidateCaseAsync(candidate);
        candidateCase.ParentCaseId.ShouldBeNull();
        candidateCase.DepartmentId.ShouldBeNull();
    }

    [Fact]
    public async Task Reapplication_ends_the_old_case_and_its_open_formalise_child()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var old = await CandidateCaseAsync(candidate);
        await AcceptOfferAsync(old.Id, startDate: null);
        await RunAsync();
        var child = await FormaliseOfAsync(old);

        var tomorrow = Today.AddDays(1);
        await ReapplyAsync(candidate, ProcessSubjectRefs.StartOfDay(tomorrow));
        await RunAsync();

        var ended = await CaseByIdAsync(old.Id);
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBe(ProcessOutcomes.Reapplied);

        var endedChild = await CaseByIdAsync(child.Id);
        endedChild.Status.ShouldBe(ProcessCaseStatus.Completed);
        endedChild.Outcome.ShouldBe(ProcessOutcomes.Reapplied);

        var fresh = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, tomorrow));
        fresh.Status.ShouldBe(ProcessCaseStatus.Active);
    }

    [Fact]
    public async Task Same_day_reapplication_keeps_the_one_case()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var existing = await CandidateCaseAsync(candidate);

        await ReapplyAsync(candidate, ProcessSubjectRefs.StartOfDay(Today).AddHours(23));
        await RunAsync();

        var after = await CandidateCaseAsync(candidate);
        after.Id.ShouldBe(existing.Id);
        after.Status.ShouldBe(ProcessCaseStatus.Active);
    }

    [Fact]
    public async Task Deleted_candidate_cancels_the_case()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();

        await SqlAsync($"UPDATE candidates SET deleted_at = now() WHERE id = {candidate}");
        await RunAsync();

        var cancelled = await CandidateCaseAsync(candidate);
        cancelled.Status.ShouldBe(ProcessCaseStatus.Cancelled);
        cancelled.CancelReason.ShouldBe(ProcessCase.SubjectDeletedReason);
    }

    // ────────────────────────────────────────────────────────────
    //  hire-formalise (a)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accepted_offer_starts_one_child_anchored_on_the_manual_start_date()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var parent = await CandidateCaseAsync(candidate);
        var startDate = Today.AddDays(10);

        await AcceptOfferAsync(parent.Id, startDate);
        await RunAsync();
        await RunAsync();

        var child = await FormaliseOfAsync(parent);
        child.SubjectRef.ShouldBe(ProcessSubjectRefs.FormaliseFromCandidate(candidate, Today));
        child.AnchorDate.ShouldBe(startDate);
        child.CandidateId.ShouldBe(candidate);
        child.EmploymentId.ShouldBeNull();
        child.Legacy.ShouldBeFalse();
    }

    [Fact]
    public async Task Hire_of_an_accepted_candidate_links_the_employment_and_starts_no_second_formalise()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var parent = await CandidateCaseAsync(candidate);
        await AcceptOfferAsync(parent.Id, Today.AddDays(10));
        await RunAsync();

        var hire = await HireAsync(candidateId: candidate);
        await RunAsync();

        var child = await FormaliseOfAsync(parent);
        child.EmploymentId.ShouldBe(hire.EmploymentId);
        child.EmployeeId.ShouldBe(hire.EmployeeId);
        child.AnchorDate.ShouldBe(Today.AddDays(10), "the offer's date stands until HR-F05 names one");

        var linkedParent = await CaseByIdAsync(parent.Id);
        linkedParent.EmploymentId.ShouldBe(hire.EmploymentId);

        (await FindCaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId))).ShouldBeNull();
    }

    [Fact]
    public async Task First_day_stays_blocked_until_the_child_safety_instruction_is_closed()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var parent = await CandidateCaseAsync(candidate);
        await AcceptOfferAsync(parent.Id, Today.AddDays(10));
        await CloseManuallyAsync(parent.Id, "r02-12", payload: null);
        await RunAsync();

        var blocked = (await CaseByIdAsync(parent.Id)).FindStep(ProcessStepIds.FirstDay)!;
        blocked.State.ShouldBe(ProcessStepState.Blocked);
        blocked.BlockedGateCode.ShouldBe(Bonvi.Application.Processes.Definitions.ProcessGateCodes.Q04);

        var child = await FormaliseOfAsync(parent);
        await CloseManuallyAsync(child.Id, ProcessStepIds.SafetyInstruction, payload: null);
        await RunAsync();

        (await CaseByIdAsync(parent.Id)).FindStep(ProcessStepIds.FirstDay)!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task Hire_another_case_holds_is_reported_and_the_pass_survives()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var first = await CandidateCaseAsync(candidate);
        var hire = await HireAsync(candidateId: candidate);
        await RunAsync();
        (await CaseByIdAsync(first.Id)).EmploymentId.ShouldBe(hire.EmploymentId);

        // The second application begins before the hire's time, so the hire counts for both.
        var tomorrow = Today.AddDays(1);
        var appliedAt = ProcessSubjectRefs.StartOfDay(tomorrow);
        await SqlAsync($"UPDATE employment_events SET recorded_at = {appliedAt.AddHours(1)} WHERE employment_id = {hire.EmploymentId}");
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.InProgress} WHERE id = {candidate}");
        await ReapplyAsync(candidate, appliedAt);

        var journal = new RecordingJournal();
        var result = await RunAsync(journal);

        result.Status.ShouldBe(ProcessReconcileStatus.Finished);
        var second = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, tomorrow));
        second.EmploymentId.ShouldBeNull();
        journal.Problems.ShouldContain(problem => problem.CaseId == second.Id && problem.Text.Contains("held by another case"));
    }

    // ────────────────────────────────────────────────────────────
    //  hire-formalise (b)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Direct_hire_starts_a_case_anchored_on_the_hire_date()
    {
        await StartEngineAsync();
        var hire = await HireAsync();

        await RunAsync();

        var direct = await CaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId));
        direct.EmploymentId.ShouldBe(hire.EmploymentId);
        direct.EmployeeId.ShouldBe(hire.EmployeeId);
        direct.AnchorDate.ShouldBe(Today);
        direct.ParentCaseId.ShouldBeNull();
        direct.Legacy.ShouldBeFalse();
        direct.FindStep("r01-5")!.State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public async Task Hire_before_any_offer_takes_the_candidate_case_as_parent_and_no_offer_adds_a_child()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var parent = await CandidateCaseAsync(candidate);

        var hire = await HireAsync(candidateId: candidate);
        await RunAsync();

        var direct = await CaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId));
        direct.ParentCaseId.ShouldBe(parent.Id);
        direct.CandidateId.ShouldBe(candidate);

        await AcceptOfferAsync(parent.Id, Today.AddDays(1));
        await RunAsync();

        (await FindCaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromCandidate(candidate, Today))).ShouldBeNull();
    }

    [Fact]
    public async Task Hire_of_a_candidate_who_applied_before_the_start_is_legacy()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        var before = ProcessSubjectRefs.StartOfDay(Today.AddDays(-30));
        await SqlAsync($"UPDATE candidates SET created_at = {before} WHERE id = {candidate}");

        var hire = await HireAsync(candidateId: candidate);
        await RunAsync();

        (await CaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId)))
            .Legacy.ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────
    //  probation
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Probation_starts_a_case_with_its_links()
    {
        await StartEngineAsync();
        var probation = await ProbationAsync(Today);

        await RunAsync();

        var started = await CaseAsync(ProcessCodes.Probation, ProcessSubjectRefs.Probation(probation.ProbationId));
        started.ProbationId.ShouldBe(probation.ProbationId);
        started.EmploymentId.ShouldBe(probation.EmploymentId);
        started.EmployeeId.ShouldBe(probation.EmployeeId);
        started.StartedOn.ShouldBe(Today);
    }

    // ────────────────────────────────────────────────────────────
    //  dismissal
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dismissal_blank_starts_a_case()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);

        await RunAsync();

        var started = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));
        started.Status.ShouldBe(ProcessCaseStatus.Active);
        started.Legacy.ShouldBeFalse();
        started.EmployeeId.ShouldBe(hire.EmployeeId);
        started.FindStep("ex-1")!.State.ShouldBe(ProcessStepState.Done);
        started.FindStep(ProcessStepIds.ActOpened)!.State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public async Task Dismissal_blank_older_than_the_start_makes_the_case_legacy()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        var older = await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        var before = ProcessSubjectRefs.StartOfDay(Today.AddDays(-30));
        await SqlAsync($"UPDATE blank_submissions SET created_at = {before} WHERE id = {older}");
        await BlankAsync(ProcessBlanks.F08, hire, F08Answers);

        await RunAsync();

        (await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).Legacy.ShouldBeTrue();
    }

    [Fact]
    public async Task Termination_with_no_case_starts_a_late_case()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        await RunAsync();

        await TerminateAsync(hire);
        await RunAsync();

        var late = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));
        late.Status.ShouldBe(ProcessCaseStatus.Active);
        late.StartedOn.ShouldBe(Today);
        late.AnchorDate.ShouldBe(Today);
        late.FindStep(ProcessStepIds.Terminated)!.State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public async Task Withdrawn_dismissal_stays_closed_until_a_newer_start_fact_then_reopens()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        var act = await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        var subjectRef = ProcessSubjectRefs.Dismissal(hire.EmploymentId);

        await CancelBlankAsync(act);
        await RunAsync();
        await RunAsync();

        var withdrawn = await CaseAsync(ProcessCodes.Dismissal, subjectRef);
        withdrawn.Status.ShouldBe(ProcessCaseStatus.Completed);
        withdrawn.Outcome.ShouldBe(ProcessOutcomes.Withdrawn);

        await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        await RunAsync();

        var reopened = await CaseAsync(ProcessCodes.Dismissal, subjectRef);
        reopened.Id.ShouldBe(withdrawn.Id);
        reopened.Status.ShouldBe(ProcessCaseStatus.Active);
        reopened.Outcome.ShouldBeNull();
        reopened.CompletedAt.ShouldBeNull();
        reopened.FindStep(ProcessStepIds.ActOpened)!.State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public async Task Withdrawn_dismissal_ignores_a_start_fact_older_than_the_withdrawal()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        await BlankAsync(ProcessBlanks.F08, hire, F08Answers);
        var act = await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        var subjectRef = ProcessSubjectRefs.Dismissal(hire.EmploymentId);

        await CancelBlankAsync(act);
        await RunAsync();
        await RunAsync();

        var withdrawn = await CaseAsync(ProcessCodes.Dismissal, subjectRef);
        withdrawn.Status.ShouldBe(ProcessCaseStatus.Completed, "the F08 still lives but is older than the withdrawal");
        withdrawn.Outcome.ShouldBe(ProcessOutcomes.Withdrawn);
    }

    [Fact]
    public async Task Termination_after_a_withdrawal_reopens_the_case()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        var act = await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        await CancelBlankAsync(act);
        await RunAsync();

        await TerminateAsync(hire);
        await RunAsync();

        (await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId)))
            .Status.ShouldBe(ProcessCaseStatus.Active);
    }

    // ────────────────────────────────────────────────────────────
    //  One case, templates, reopen
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_case_run_passes_that_case_and_starts_its_child_and_nothing_else()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var parent = await CandidateCaseAsync(candidate);
        await AcceptOfferAsync(parent.Id, Today.AddDays(3));
        var untouched = await VacancyRequestAsync();

        var result = await InScopeAsync((reconciler, _) => reconciler.RunCaseAsync(parent.Id, Ct));

        result.Status.ShouldBe(ProcessReconcileStatus.Finished);
        result.Started.ShouldBe(1);
        (await FormaliseOfAsync(parent)).AnchorDate.ShouldBe(Today.AddDays(3));
        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(untouched.Id))).ShouldBeNull();
    }

    [Fact]
    public async Task One_case_run_leaves_an_unknown_or_ended_case_alone()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var ended = await CandidateCaseAsync(candidate);
        await SqlAsync($"UPDATE candidates SET deleted_at = now() WHERE id = {candidate}");
        await RunAsync();

        (await InScopeAsync((reconciler, _) => reconciler.RunCaseAsync(ended.Id, Ct)))
            .ShouldBe(new ProcessReconcileResult(ProcessReconcileStatus.Finished, 0, 0));
        (await InScopeAsync((reconciler, _) => reconciler.RunCaseAsync(Guid.NewGuid(), Ct)))
            .ShouldBe(new ProcessReconcileResult(ProcessReconcileStatus.Finished, 0, 0));
    }

    [Fact]
    public async Task Running_case_keeps_a_retired_version_and_the_starter_of_that_process_waits()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var running = await CandidateCaseAsync(candidate);
        var versionId = running.TemplateVersionId;

        await SqlAsync($"UPDATE process_template_versions SET status = {(short)ProcessTemplateVersionStatus.Retired} WHERE id = {versionId}");
        try
        {
            await CloseManuallyAsync(running.Id, "r02-5", payload: null);
            var waiting = await CandidateAsync(await VacancyAsync());

            var journal = new RecordingJournal();
            await RunAsync(journal);

            (await CandidateCaseAsync(candidate)).FindStep("r02-6")!.State.ShouldBe(ProcessStepState.Active);
            journal.MissingTemplates.ShouldContain(ProcessCodes.HireCandidate);
            (await FindCaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(waiting, Today))).ShouldBeNull();
        }
        finally
        {
            await SqlAsync($"UPDATE process_template_versions SET status = {(short)ProcessTemplateVersionStatus.Published} WHERE id = {versionId}");
        }
    }

    /// <summary>What <c>POST /cases/{id}/reopen</c> will meet: §7.5 end facts are evaluated again, the reopen does not outlast them.</summary>
    [Fact]
    public async Task Reopened_case_whose_end_fact_still_holds_completes_again()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var rejected = await CandidateCaseAsync(candidate);
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.Rejected} WHERE id = {candidate}");
        await RunAsync();
        (await CaseByIdAsync(rejected.Id)).Outcome.ShouldBe("rejected");

        await DbAsync(async db =>
        {
            var row = await db.ProcessCases.Include(processCase => processCase.Steps).SingleAsync(processCase => processCase.Id == rejected.Id, Ct);
            row.Reopen(DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });
        await RunAsync();

        var again = await CaseByIdAsync(rejected.Id);
        again.Status.ShouldBe(ProcessCaseStatus.Completed);
        again.Outcome.ShouldBe("rejected");
    }

    /// <summary>
    /// r02-11 closes on the decline branch's <c>cand.status</c>, whose linked id is the candidate. On this blank step
    /// that id must not reach <c>linked_blank_submission_id</c>: its foreign key would fail the whole pass.
    /// </summary>
    [Fact]
    public async Task Offer_closed_on_a_declined_candidate_saves_and_the_case_ends_declined()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CandidateCaseAsync(candidate);

        await CloseManuallyAsync(processCase.Id, "r02-10", """{"decision":"reject"}""");
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.Rejected} WHERE id = {candidate}");
        await RunAsync();

        var ended = await CaseByIdAsync(processCase.Id);
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBe(ProcessOutcomes.Declined);

        var offer = ended.FindStep(ProcessStepIds.Offer)!;
        offer.State.ShouldBe(ProcessStepState.Done);
        offer.LinkedBlankSubmissionId.ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    private const string F09Dismissal = """{"kind":{"value":"dismissal"}}""";

    private sealed record Hire(Guid EmployeeId, Guid EmploymentId);

    private sealed record ProbationSubject(Guid EmployeeId, Guid EmploymentId, Guid ProbationId);

    /// <summary>Remembers what the engine journals, so a test can assert on the problems of its own case.</summary>
    private sealed class RecordingJournal : IProcessEngineJournal
    {
        public List<(Guid CaseId, string Text)> Problems { get; } = [];

        public List<string> MissingTemplates { get; } = [];

        public List<Exception> Failures { get; } = [];

        public void PassFinished(int started, int changedRows)
        {
        }

        public void PassFailed(Exception exception) => Failures.Add(exception);

        public void CaseProblem(Guid caseId, string processCode, string problem) => Problems.Add((caseId, problem));

        public void TemplateMissing(string processCode) => MissingTemplates.Add(processCode);

        public void TemplateUnreadable(Guid versionId, Exception exception)
        {
        }

        public void GateFailedOpen(string operation, Guid employeeId, Exception exception)
        {
        }
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>The reconciler from DI; a test that reads the journal gets the same graph around its own.</summary>
    private static ProcessReconciler Resolve(IServiceProvider services, IProcessEngineJournal? journal)
        => journal is null
            ? services.GetRequiredService<ProcessReconciler>()
            : ActivatorUtilities.CreateInstance<ProcessReconciler>(
                services,
                journal,
                ActivatorUtilities.CreateInstance<ProcessEnginePassLoader>(services, journal),
                ActivatorUtilities.CreateInstance<ProcessStarters>(services, journal),
                ActivatorUtilities.CreateInstance<ProcessCaseBatch>(services, journal));

    private async Task<T> InScopeAsync<T>(Func<ProcessReconciler, IServiceProvider, Task<T>> work, IProcessEngineJournal? journal = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(Resolve(scope.ServiceProvider, journal), scope.ServiceProvider);
    }

    private Task<ProcessReconcileResult> RunAsync(IProcessEngineJournal? journal = null)
        => InScopeAsync((reconciler, _) => reconciler.RunAsync(Ct), journal);

    private Task<int> StartEngineAsync() => SetStartedOnAsync(Today);

    private Task<int> SetStartedOnAsync(DateOnly? startedOn) => DbAsync(async db =>
    {
        await ProcessTestGates.EnforceAsync(_factory.Services, Ct);

        var value = startedOn?.ToString(SystemSettingKeys.ProcessesStartedOnFormat, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        var row = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == SystemSettingKeys.ProcessesStartedOn, Ct);

        if (row is null)
        {
            db.SystemSettings.Add(SystemSetting.CreateServerValue(SystemSettingKeys.ProcessesStartedOn, value).Value);
        }
        else
        {
            row.SetServerValue(value).IsSuccess.ShouldBeTrue();
        }

        return await db.SaveChangesAsync(Ct);
    });

    private Task<string?> WatermarkAsync() => DbAsync(db => db.SystemSettings.AsNoTracking()
        .Where(setting => setting.Key == SystemSettingKeys.ProcessesReconcileWatermark)
        .Select(setting => (string?)setting.Value)
        .SingleOrDefaultAsync(Ct));

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> SqlAsync(FormattableString sql)
        => DbAsync(db => db.Database.ExecuteSqlAsync(sql, Ct));

    private Task<ProcessCase?> FindCaseAsync(string processCode, string subjectRef)
        => DbAsync(db => db.ProcessCases.AsNoTracking()
            .Include(processCase => processCase.Steps)
            .SingleOrDefaultAsync(processCase => processCase.ProcessCode == processCode && processCase.SubjectRef == subjectRef, Ct));

    private async Task<ProcessCase> CaseAsync(string processCode, string subjectRef)
        => (await FindCaseAsync(processCode, subjectRef)).ShouldNotBeNull();

    private Task<ProcessCase> CandidateCaseAsync(Guid candidateId)
        => CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidateId, Today));

    private Task<ProcessCase> CaseByIdAsync(Guid id)
        => DbAsync(db => db.ProcessCases.AsNoTracking()
            .Include(processCase => processCase.Steps)
            .SingleAsync(processCase => processCase.Id == id, Ct));

    private Task<ProcessCase> FormaliseOfAsync(ProcessCase parent)
        => DbAsync(db => db.ProcessCases.AsNoTracking()
            .Include(processCase => processCase.Steps)
            .SingleAsync(processCase => processCase.ParentCaseId == parent.Id && processCase.ProcessCode == ProcessCodes.HireFormalise, Ct));

    private Task<int> ReapplyAsync(Guid candidateId, DateTimeOffset appliedAt)
        => SqlAsync($"UPDATE candidates SET reapplied_at = {appliedAt} WHERE id = {candidateId}");

    /// <summary>The council's accept and the offer, closed by hand as HRMentor's blanks are not served yet (§1.5).</summary>
    private async Task AcceptOfferAsync(Guid caseId, DateOnly? startDate)
    {
        await CloseManuallyAsync(caseId, "r02-10", """{"decision":"accept"}""");
        await CloseManuallyAsync(
            caseId,
            ProcessStepIds.Offer,
            startDate is { } date ? $$"""{"{{ProcessStepPayload.StartDateInput}}":"{{date:yyyy-MM-dd}}"}""" : null);
    }

    /// <summary>Opens the step past its order and closes it: the state rules are the aggregate's, who may act is P6's.</summary>
    private async Task CloseManuallyAsync(Guid caseId, string stepCode, string? payload)
    {
        var by = await UserIdAsync();

        await DbAsync(async db =>
        {
            var row = await db.ProcessCases.Include(processCase => processCase.Steps).SingleAsync(processCase => processCase.Id == caseId, Ct);
            var now = DateTimeOffset.UtcNow;

            row.ActivateStep(stepCode, now).IsSuccess.ShouldBeTrue();
            row.CompleteStepManually(stepCode, by, payload, evidenceSha256: null, now).IsSuccess.ShouldBeTrue();

            return await db.SaveChangesAsync(Ct);
        });
    }

    private Task<Guid> UserIdAsync() => DbAsync(async db =>
    {
        var phone = PhoneNumber.Create("90" + Random.Shared.Next(1_000_000, 9_999_999)).Value;
        var user = User.Create(phone, "test-hash", FullName.Create("Foydalanuvchi" + Suffix(), "Test", null).Value).Value;
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    });

    private Task<Guid> VacancyAsync() => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Engine vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(Ct);
        return vacancy.Id;
    });

    private Task<Guid> CandidateAsync(Guid vacancyId) => DbAsync(async db =>
    {
        var candidate = Candidate.Register(FullName.Create("Motorov" + Suffix(), "Test", null).Value, vacancyId).Value;
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(Ct);
        return candidate.Id;
    });

    private async Task<VacancyRequest> VacancyRequestAsync(Guid? vacancyId = null)
    {
        var request = await DbAsync(async db =>
        {
            var department = Department.Create("Engine department " + Suffix()).Value;
            db.Departments.Add(department);
            var requester = Employee.Create(FullName.Create("Sorovchi" + Suffix(), "Test", null).Value).Value;
            db.Employees.Add(requester);

            var row = VacancyRequest.Create(department.Id, requester.Id, "Engine position", 1, neededBy: null, note: null).Value;
            db.VacancyRequests.Add(row);

            await db.SaveChangesAsync(Ct);
            return row;
        });

        if (vacancyId is { } id)
        {
            await SqlAsync($"UPDATE vacancy_requests SET vacancy_id = {id} WHERE id = {request.Id}");
        }

        return request;
    }

    /// <summary>A hire (optionally of a candidate, who is then marked Hired).</summary>
    private Task<Hire> HireAsync(Guid? candidateId = null, string? orderNo = null) => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Engine MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var employee = Employee.Create(FullName.Create("Xodimov" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(employee);

        var employment = Employment.Hire(employee.Id, legalEntity.Id, Today, orderNo: orderNo, candidateId: candidateId).Value;
        db.Employments.Add(employment);

        if (candidateId is { } id)
        {
            var candidate = await db.Candidates.SingleAsync(row => row.Id == id, Ct);
            candidate.MarkHired(Today).IsSuccess.ShouldBeTrue();
        }

        await db.SaveChangesAsync(Ct);
        return new Hire(employee.Id, employment.Id);
    });

    private async Task<ProbationSubject> ProbationAsync(DateOnly start)
    {
        var hire = await HireAsync();
        return await DbAsync(async db =>
        {
            var probation = Probation.Import(hire.EmploymentId, start, start.AddDays(90)).Value;
            db.Probations.Add(probation);
            await db.SaveChangesAsync(Ct);
            return new ProbationSubject(hire.EmployeeId, hire.EmploymentId, probation.Id);
        });
    }

    private Task<int> TerminateAsync(Hire hire) => DbAsync(async db =>
    {
        var employment = await db.Employments.SingleAsync(row => row.Id == hire.EmploymentId, Ct);
        employment.Terminate(Today, reasonId: null).IsSuccess.ShouldBeTrue();
        return await db.SaveChangesAsync(Ct);
    });

    /// <summary>A draft blank: a draft is live, which is all the starters and <c>dis.f09Opened</c> ask.</summary>
    private Task<Guid> BlankAsync(string code, Hire hire, string answers) => DbAsync(async db =>
    {
        var template = await db.BlankTemplates.SingleAsync(row => row.Code == code, Ct);
        var version = await db.BlankTemplateVersions
            .SingleAsync(row => row.TemplateId == template.Id && row.Status == BlankTemplateVersionStatus.Published, Ct);

        var blank = BlankSubmission.Create(
            template,
            version,
            new BlankSubject(hire.EmployeeId, hire.EmploymentId),
            new BlankAnswersState(answers, "{}", "[]", 0, null)).Value;

        db.BlankSubmissions.Add(blank);
        await db.SaveChangesAsync(Ct);
        return blank.Id;
    });

    private Task<int> CancelBlankAsync(Guid blankId) => DbAsync(async db =>
    {
        var blank = await db.BlankSubmissions.SingleAsync(row => row.Id == blankId, Ct);
        blank.Cancel("test", DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        return await db.SaveChangesAsync(Ct);
    });
}
