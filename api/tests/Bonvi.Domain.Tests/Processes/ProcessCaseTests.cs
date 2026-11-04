using Bonvi.Domain.Blanks;
using Bonvi.Domain.Processes;

namespace Bonvi.Domain.Tests.Processes;

/// <summary>Case and step state machine (docs/SPEC-mening-ishlarim.md §7.4, §7.5, §10).</summary>
public sealed class ProcessCaseTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid VacancyRequest = Guid.NewGuid();
    private static readonly Guid Hr = Guid.NewGuid();
    private static readonly Guid OtherHr = Guid.NewGuid();
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static ProcessStepPlan[] Plan() =>
    [
        new("r01-1", 1, "request", BlankActorRoles.DepartmentHead),
        new("r01-2", 2, "approval", BlankActorRoles.Director),
        new("r01-3", 3, "posting", BlankActorRoles.Hr1),
    ];

    private static ProcessCaseStart StartInput(ProcessCaseLinks? links = null) => new(
        "hire-request",
        Guid.NewGuid(),
        ProcessSubjectKind.VacancyRequest,
        "vr:" + VacancyRequest,
        new DateOnly(2026, 11, 4),
        Legacy: false,
        links ?? new ProcessCaseLinks(VacancyRequestId: VacancyRequest));

    private static ProcessCase Started() => ProcessCase.Start(StartInput(), Plan(), At).Value;

    private static ProcessCase WithActive(string stepCode)
    {
        var processCase = Started();
        processCase.ActivateStep(stepCode, At).Value.ShouldBeTrue();
        return processCase;
    }

    private static ProcessCaseStep Step(ProcessCase processCase, string code) => processCase.FindStep(code).ShouldNotBeNull();

    // ---------- Start ----------

    [Fact]
    public void Start_creates_an_active_case_with_locked_steps_in_order()
    {
        var processCase = ProcessCase.Start(StartInput(), Plan().Reverse().ToArray(), At).Value;

        processCase.Status.ShouldBe(ProcessCaseStatus.Active);
        processCase.Version.ShouldBe(0);
        processCase.HasAuditedChange.ShouldBeTrue("a created case is audited (§14)");
        processCase.Steps.Select(step => step.StepCode).ShouldBe(["r01-1", "r01-2", "r01-3"]);
        processCase.Steps.ShouldAllBe(step => step.State == ProcessStepState.Locked && step.CaseId == processCase.Id);
        processCase.Steps.ShouldAllBe(step => !step.HasAuditedChange);
    }

    [Fact]
    public void Start_rejects_a_missing_subject_an_empty_plan_and_duplicate_steps()
    {
        ProcessCase.Start(StartInput(new ProcessCaseLinks()), Plan(), At).Error.ShouldBe(ProcessErrors.Invalid);
        ProcessCase.Start(StartInput(), [], At).Error.ShouldBe(ProcessErrors.Invalid);
        ProcessCase.Start(
                StartInput(),
                [new("a", 1, "col", null), new("a", 2, "col", null)],
                At)
            .Error.ShouldBe(ProcessErrors.Invalid);
    }

    // ---------- Complete / cancel / reopen ----------

    [Fact]
    public void Complete_skips_every_open_step_as_case_ended_and_keeps_closed_ones()
    {
        var processCase = WithActive("r01-1");
        processCase.CompleteStepManually("r01-1", Hr, payload: null, evidenceSha256: null, At).IsSuccess.ShouldBeTrue();

        processCase.Complete("hired", At.AddHours(1)).IsSuccess.ShouldBeTrue();

        processCase.Status.ShouldBe(ProcessCaseStatus.Completed);
        processCase.Outcome.ShouldBe("hired");
        processCase.CompletedAt.ShouldBe(At.AddHours(1));
        Step(processCase, "r01-1").Resolution.ShouldBe(ProcessStepResolution.Manual);
        Step(processCase, "r01-2").State.ShouldBe(ProcessStepState.Skipped);
        Step(processCase, "r01-2").Resolution.ShouldBe(ProcessStepResolution.CaseEnded);
        Step(processCase, "r01-3").Resolution.ShouldBe(ProcessStepResolution.CaseEnded);
    }

    [Fact]
    public void Complete_and_cancel_need_an_active_case()
    {
        var processCase = Started();
        processCase.Complete(null, At).IsSuccess.ShouldBeTrue();

        processCase.Complete(null, At).Error.ShouldBe(ProcessErrors.InvalidStatus);
        processCase.Cancel("Talab bekor qilindi", At).Error.ShouldBe(ProcessErrors.InvalidStatus);
    }

    [Fact]
    public void Cancel_needs_a_reason_and_is_audited()
    {
        var processCase = Started();

        processCase.Cancel("   ", At).Error.ShouldBe(ProcessErrors.Invalid);
        processCase.Cancel("  Talab bekor qilindi  ", At).IsSuccess.ShouldBeTrue();

        processCase.Status.ShouldBe(ProcessCaseStatus.Cancelled);
        processCase.CancelReason.ShouldBe("Talab bekor qilindi");
        processCase.CancelledAt.ShouldBe(At);
        processCase.HasAuditedChange.ShouldBeTrue();
    }

    [Fact]
    public void Subject_deletion_cancels_with_the_system_reason()
    {
        var processCase = Started();

        processCase.CancelBecauseSubjectDeleted(At).IsSuccess.ShouldBeTrue();

        processCase.CancelReason.ShouldBe(ProcessCase.SubjectDeletedReason);
    }

    [Fact]
    public void Reopen_relocks_case_ended_steps_and_clears_the_end()
    {
        var processCase = WithActive("r01-1");
        processCase.CompleteStepManually("r01-1", Hr, null, null, At).IsSuccess.ShouldBeTrue();
        processCase.Complete("hired", At).IsSuccess.ShouldBeTrue();
        var version = processCase.Version;

        processCase.Reopen(At.AddDays(1)).IsSuccess.ShouldBeTrue();

        processCase.Status.ShouldBe(ProcessCaseStatus.Active);
        processCase.Outcome.ShouldBeNull();
        processCase.CompletedAt.ShouldBeNull();
        processCase.Version.ShouldBe(version + 1);
        Step(processCase, "r01-1").Resolution.ShouldBe(ProcessStepResolution.Manual, "a person's Done survives the reopen");
        Step(processCase, "r01-2").State.ShouldBe(ProcessStepState.Locked);
        Step(processCase, "r01-2").Resolution.ShouldBeNull();
        Step(processCase, "r01-3").State.ShouldBe(ProcessStepState.Locked);
    }

    [Fact]
    public void Reopen_of_a_cancelled_case_clears_the_reason_and_an_active_case_is_not_reopened()
    {
        var processCase = Started();
        processCase.Reopen(At).Error.ShouldBe(ProcessErrors.InvalidStatus);

        processCase.Cancel("Talab bekor qilindi", At).IsSuccess.ShouldBeTrue();
        processCase.Reopen(At).IsSuccess.ShouldBeTrue();

        processCase.CancelReason.ShouldBeNull();
        processCase.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public void Step_changes_need_an_active_case()
    {
        var processCase = Started();
        processCase.Cancel("Talab bekor qilindi", At).IsSuccess.ShouldBeTrue();

        processCase.ActivateStep("r01-1", At).Error.ShouldBe(ProcessErrors.InvalidStatus);
        processCase.MarkStepDoneByFact("r01-1", ProcessStepResolution.Fact, At, null, At).Error.ShouldBe(ProcessErrors.InvalidStatus);
    }

    // ---------- Reconciler: facts ----------

    [Fact]
    public void MarkStepDoneByFact_closes_any_open_state_and_reports_no_change_on_a_repeat()
    {
        var processCase = Started();

        processCase.MarkStepDoneByFact("r01-2", ProcessStepResolution.Fact, At, "{\"no\":1}", At).Value.ShouldBeTrue();
        var version = processCase.Version;

        processCase.MarkStepDoneByFact("r01-2", ProcessStepResolution.Fact, At, "{\"no\":1}", At.AddMinutes(5)).Value.ShouldBeFalse();

        processCase.Version.ShouldBe(version, "an unchanged pass does not bump the version (§7.4 p.6)");
        var step = Step(processCase, "r01-2");
        step.State.ShouldBe(ProcessStepState.Done);
        step.Resolution.ShouldBe(ProcessStepResolution.Fact);
        step.DoneBy.ShouldBeNull();
        step.HasAuditedChange.ShouldBeFalse("fact-driven changes are not audited (§14)");
    }

    [Fact]
    public void MarkStepDoneByFact_leaves_a_manual_done_as_it_is()
    {
        var processCase = WithActive("r01-1");
        processCase.CompleteStepManually("r01-1", Hr, "{\"note\":\"x\"}", Sha, At).IsSuccess.ShouldBeTrue();
        var version = processCase.Version;

        processCase.MarkStepDoneByFact("r01-1", ProcessStepResolution.Fact, At.AddHours(1), null, At.AddHours(1))
            .Value.ShouldBeFalse();

        var step = Step(processCase, "r01-1");
        step.Resolution.ShouldBe(ProcessStepResolution.Manual);
        step.DoneBy.ShouldBe(Hr);
        step.Payload.ShouldBe("{\"note\":\"x\"}");
        processCase.Version.ShouldBe(version);
    }

    [Fact]
    public void MarkStepDoneByFact_accepts_only_fact_or_auto()
    {
        var processCase = Started();

        processCase.MarkStepDoneByFact("r01-1", ProcessStepResolution.Manual, At, null, At).Error.ShouldBe(ProcessErrors.Invalid);
        processCase.MarkStepDoneByFact("r01-1", ProcessStepResolution.Auto, At, null, At).Value.ShouldBeTrue();
        processCase.MarkStepDoneByFact("missing", ProcessStepResolution.Fact, At, null, At).Error.ShouldBe(ProcessErrors.NotFound);
    }

    [Theory]
    [InlineData(ProcessStepResolution.Fact)]
    [InlineData(ProcessStepResolution.Auto)]
    public void RevertStepFact_reopens_a_fact_or_auto_done_step(ProcessStepResolution resolution)
    {
        var processCase = Started();
        processCase.MarkStepDoneByFact("r01-1", resolution, At, "{}", At).Value.ShouldBeTrue();

        processCase.RevertStepFact("r01-1", At.AddHours(1)).Value.ShouldBeTrue();

        var step = Step(processCase, "r01-1");
        step.State.ShouldBe(ProcessStepState.Active);
        step.Resolution.ShouldBeNull();
        step.DoneAt.ShouldBeNull();
        step.Payload.ShouldBeNull();
    }

    [Fact]
    public void RevertStepFact_never_touches_a_manual_or_skipped_step()
    {
        var processCase = WithActive("r01-1");
        processCase.CompleteStepManually("r01-1", Hr, null, null, At).IsSuccess.ShouldBeTrue();
        processCase.SkipStepByCondition("r01-2", At).Value.ShouldBeTrue();

        processCase.RevertStepFact("r01-1", At).Value.ShouldBeFalse();
        processCase.RevertStepFact("r01-2", At).Value.ShouldBeFalse();
        processCase.RevertStepFact("r01-3", At).Value.ShouldBeFalse("a Locked step has no fact to revert");

        Step(processCase, "r01-1").Resolution.ShouldBe(ProcessStepResolution.Manual);
        Step(processCase, "r01-2").Resolution.ShouldBe(ProcessStepResolution.Condition);
    }

    [Fact]
    public void Open_state_moves_are_change_detected_and_block_needs_a_gate()
    {
        var processCase = Started();

        processCase.LockStep("r01-1", At).Value.ShouldBeFalse("already Locked");
        processCase.BlockStep("r01-1", " ", At).Error.ShouldBe(ProcessErrors.Invalid);
        processCase.BlockStep("r01-1", "Q12", At).Value.ShouldBeTrue();
        Step(processCase, "r01-1").BlockedGateCode.ShouldBe("Q12");

        processCase.WaitStep("r01-1", At).Value.ShouldBeTrue();
        Step(processCase, "r01-1").BlockedGateCode.ShouldBeNull();
    }

    [Fact]
    public void A_condition_skipped_step_is_recomputable_but_a_closed_one_is_not()
    {
        var processCase = Started();
        processCase.SkipStepByCondition("r01-2", At).Value.ShouldBeTrue();
        processCase.ActivateStep("r01-2", At).Value.ShouldBeTrue("the when condition came back");

        processCase.MarkStepDoneByFact("r01-3", ProcessStepResolution.Fact, At, null, At).Value.ShouldBeTrue();
        processCase.ActivateStep("r01-3", At).Error.ShouldBe(ProcessErrors.InvalidStepState);
    }

    // ---------- User actions ----------

    [Theory]
    [InlineData(ProcessStepState.Locked)]
    [InlineData(ProcessStepState.Waiting)]
    [InlineData(ProcessStepState.Blocked)]
    public void CompleteStepManually_only_when_the_step_is_active(ProcessStepState state)
    {
        var processCase = Started();
        _ = state switch
        {
            ProcessStepState.Waiting => processCase.WaitStep("r01-1", At),
            ProcessStepState.Blocked => processCase.BlockStep("r01-1", "Q12", At),
            _ => processCase.LockStep("r01-1", At),
        };

        processCase.CompleteStepManually("r01-1", Hr, null, null, At).Error.ShouldBe(ProcessErrors.InvalidStepState);
    }

    [Fact]
    public void CompleteStepManually_records_who_what_and_is_audited()
    {
        var processCase = WithActive("r01-1");
        var version = processCase.Version;

        processCase.CompleteStepManually("r01-1", Hr, "{\"date\":\"2026-11-04\"}", Sha, At).IsSuccess.ShouldBeTrue();

        var step = Step(processCase, "r01-1");
        step.State.ShouldBe(ProcessStepState.Done);
        step.Resolution.ShouldBe(ProcessStepResolution.Manual);
        step.DoneBy.ShouldBe(Hr);
        step.DoneAt.ShouldBe(At);
        step.EvidenceSha256.ShouldBe(Sha);
        step.HasAuditedChange.ShouldBeTrue();
        processCase.Version.ShouldBe(version + 1);
    }

    [Fact]
    public void CompleteStepManually_rejects_a_bad_hash_or_an_empty_user_and_a_closed_step()
    {
        var processCase = WithActive("r01-1");

        processCase.CompleteStepManually("r01-1", Guid.Empty, null, null, At).Error.ShouldBe(ProcessErrors.Invalid);
        processCase.CompleteStepManually("r01-1", Hr, null, Sha.ToUpperInvariant(), At).Error.ShouldBe(ProcessErrors.Invalid);

        processCase.CompleteStepManually("r01-1", Hr, null, null, At).IsSuccess.ShouldBeTrue();
        processCase.CompleteStepManually("r01-1", Hr, null, null, At).Error.ShouldBe(ProcessErrors.InvalidStepState);
    }

    [Fact]
    public void SkipStepByUser_legacy_only_on_a_legacy_case()
    {
        var processCase = Started();

        processCase.SkipStepByUser("r01-1", Hr, legacy: true, null, At).Error.ShouldBe(ProcessErrors.Invalid);
        processCase.SkipStepByUser("r01-1", Hr, legacy: false, null, At).IsSuccess.ShouldBeTrue();

        Step(processCase, "r01-1").Resolution.ShouldBe(ProcessStepResolution.UserSkip);
        Step(processCase, "r01-1").HasAuditedChange.ShouldBeTrue();
        processCase.SkipStepByUser("r01-1", Hr, legacy: false, null, At).Error.ShouldBe(ProcessErrors.InvalidStepState);
    }

    [Fact]
    public void UndoStep_by_the_same_user_within_24_hours()
    {
        var processCase = WithActive("r01-1");
        processCase.CompleteStepManually("r01-1", Hr, "{}", null, At).IsSuccess.ShouldBeTrue();

        processCase.UndoStep("r01-1", Hr, asManager: false, At + ProcessCase.UndoWindow).IsSuccess.ShouldBeTrue();

        var step = Step(processCase, "r01-1");
        step.State.ShouldBe(ProcessStepState.Active);
        step.Resolution.ShouldBeNull();
        step.DoneBy.ShouldBeNull();
        step.Payload.ShouldBeNull();
    }

    [Fact]
    public void UndoStep_after_24_hours_or_by_another_user_is_forbidden_unless_manager()
    {
        var processCase = WithActive("r01-1");
        processCase.CompleteStepManually("r01-1", Hr, null, null, At).IsSuccess.ShouldBeTrue();
        var late = At + ProcessCase.UndoWindow + TimeSpan.FromSeconds(1);

        processCase.UndoStep("r01-1", Hr, asManager: false, late).Error.ShouldBe(ProcessErrors.Forbidden);
        processCase.UndoStep("r01-1", OtherHr, asManager: false, At.AddMinutes(1)).Error.ShouldBe(ProcessErrors.Forbidden);

        processCase.UndoStep("r01-1", OtherHr, asManager: true, late.AddDays(30)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void UndoStep_only_for_user_closed_steps()
    {
        var processCase = Started();
        processCase.MarkStepDoneByFact("r01-1", ProcessStepResolution.Fact, At, null, At).Value.ShouldBeTrue();

        processCase.UndoStep("r01-1", Hr, asManager: true, At).Error.ShouldBe(ProcessErrors.InvalidStepState);
        processCase.UndoStep("r01-2", Hr, asManager: true, At).Error.ShouldBe(ProcessErrors.InvalidStepState);
    }

    [Fact]
    public void CheckVersion_compares_with_the_loaded_version()
    {
        var processCase = WithActive("r01-1");

        processCase.CheckVersion(processCase.Version).IsSuccess.ShouldBeTrue();
        processCase.CheckVersion(processCase.Version - 1).Error.ShouldBe(ProcessErrors.Stale);
    }

    [Fact]
    public void LinkSubjects_fills_empty_ids_and_refuses_a_different_one()
    {
        var processCase = Started();
        var candidate = Guid.NewGuid();

        processCase.LinkSubjects(new ProcessCaseLinks(CandidateId: candidate), At).Value.ShouldBeTrue();
        processCase.LinkSubjects(new ProcessCaseLinks(CandidateId: candidate), At).Value.ShouldBeFalse();
        processCase.LinkSubjects(new ProcessCaseLinks(CandidateId: Guid.NewGuid()), At).Error.ShouldBe(ProcessErrors.Invalid);

        processCase.CandidateId.ShouldBe(candidate);
    }
}
