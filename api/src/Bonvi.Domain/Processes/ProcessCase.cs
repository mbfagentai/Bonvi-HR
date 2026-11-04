using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>Subject identifiers of a case — filled as facts appear (§6.2).</summary>
public sealed record ProcessCaseLinks(
    Guid? VacancyRequestId = null,
    Guid? CandidateId = null,
    Guid? EmploymentId = null,
    Guid? ProbationId = null,
    Guid? EmployeeId = null);

/// <summary>Input of <see cref="ProcessCase.Start"/>.</summary>
/// <param name="ProcessCode">§3.2 process code.</param>
/// <param name="TemplateVersionId">The Published version the case keeps for life.</param>
/// <param name="SubjectKind">What the case is about.</param>
/// <param name="SubjectRef">§3.2 subject reference (<c>vr:{id}</c>) — unique per process.</param>
/// <param name="StartedOn">Tashkent date of the start fact.</param>
/// <param name="Legacy">The start fact is older than <c>processes.startedOn</c> (§7.6).</param>
/// <param name="Links">Subject identifiers known at start.</param>
/// <param name="DepartmentId">Department for filters and grouping.</param>
/// <param name="ParentCaseId">hire-candidate → hire-request; hire-formalise → hire-candidate.</param>
/// <param name="AnchorDate">§3.5 / §3.7 anchor (e.g. the last working day).</param>
/// <param name="StartedBy">User of a manual start; <c>null</c> for a starter scan.</param>
public sealed record ProcessCaseStart(
    string ProcessCode,
    Guid TemplateVersionId,
    ProcessSubjectKind SubjectKind,
    string SubjectRef,
    DateOnly StartedOn,
    bool Legacy,
    ProcessCaseLinks Links,
    Guid? DepartmentId = null,
    Guid? ParentCaseId = null,
    DateOnly? AnchorDate = null,
    Guid? StartedBy = null);

/// <summary>
/// A running instance of a process for one subject — aggregate root with its steps
/// (<c>process_cases</c> + <c>process_case_steps</c>), docs/SPEC-mening-ishlarim.md §6.2–§6.3, §7.
/// </summary>
/// <remarks>
/// <para>
/// <b>State machine.</b> Case: <c>Active → Completed | Cancelled</c>, both reopenable to Active.
/// Steps (§7.4): the reconciler moves them between Locked / Active / Waiting / Blocked, closes them
/// as Done(fact | auto) or Skipped(condition), and reverts a fact-Done step when its fact disappears.
/// Users close Active steps by hand (Done(manual)), skip them («Kerak emas») and undo their own
/// action. Step changes need an Active case. WHICH transition the definition allows (kind,
/// <c>manual.allowed</c>, <c>skippable</c>, who holds the step) is checked in the Application layer;
/// here — the state rules only.
/// </para>
/// <para>
/// <b>Change detection.</b> Every reconciler method returns whether something changed and bumps
/// <see cref="Version"/> only then: a pass saves only when something changed (§7.4 p.6).
/// </para>
/// <para>
/// <b><see cref="Version"/></b> — optimistic concurrency (<c>AbsenceRequest</c> pattern): the
/// <c>UPDATE</c> is conditional on the loaded value; a client's value is compared through
/// <see cref="CheckVersion"/> (409 <c>jarayon.versiya</c>).
/// </para>
/// </remarks>
public sealed class ProcessCase : AggregateRoot
{
    /// <summary>Subject reference length.</summary>
    public const int MaxSubjectRefLength = 128;

    /// <summary>Outcome length (<c>terminated</c>).</summary>
    public const int MaxOutcomeLength = 32;

    /// <summary>Cancel reason length.</summary>
    public const int MaxCancelReasonLength = 1000;

    /// <summary>Cancel reason written when the subject disappears (§6.9).</summary>
    public const string SubjectDeletedReason = "subject_deleted";

    /// <summary>How long a user may undo their own manual action (§10 #12).</summary>
    public static readonly TimeSpan UndoWindow = TimeSpan.FromHours(24);

    private readonly List<ProcessCaseStep> _steps = [];

    private ProcessCase(Guid id, ProcessCaseStart start, DateTimeOffset at)
        : base(id)
    {
        ProcessCode = start.ProcessCode;
        TemplateVersionId = start.TemplateVersionId;
        Status = ProcessCaseStatus.Active;
        Legacy = start.Legacy;
        SubjectKind = start.SubjectKind;
        SubjectRef = start.SubjectRef;
        VacancyRequestId = start.Links.VacancyRequestId;
        CandidateId = start.Links.CandidateId;
        EmploymentId = start.Links.EmploymentId;
        ProbationId = start.Links.ProbationId;
        EmployeeId = start.Links.EmployeeId;
        DepartmentId = start.DepartmentId;
        ParentCaseId = start.ParentCaseId;
        AnchorDate = start.AnchorDate;
        StartedOn = start.StartedOn;
        StartedBy = start.StartedBy;
        CreatedAt = at;
        UpdatedAt = at;
        HasAuditedChange = true;
    }

    private ProcessCase() { }

    /// <summary>§3.2 process code.</summary>
    public string ProcessCode { get; private set; } = string.Empty;

    /// <summary>The version the case started with — kept for life (§6.9).</summary>
    public Guid TemplateVersionId { get; private set; }

    public ProcessCaseStatus Status { get; private set; }

    /// <summary><c>hired</c>, <c>declined</c>, <c>rejected</c>, <c>passed</c>, <c>failed</c>, <c>terminated</c>, …</summary>
    public string? Outcome { get; private set; }

    /// <summary>The subject's start fact is older than <c>processes.startedOn</c>: gates warn only (§7.6).</summary>
    public bool Legacy { get; private set; }

    public ProcessSubjectKind SubjectKind { get; private set; }

    /// <summary>§3.2 subject reference — unique per process over all statuses.</summary>
    public string SubjectRef { get; private set; } = string.Empty;

    public Guid? VacancyRequestId { get; private set; }

    public Guid? CandidateId { get; private set; }

    public Guid? EmploymentId { get; private set; }

    public Guid? ProbationId { get; private set; }

    public Guid? EmployeeId { get; private set; }

    /// <summary>Refreshed each pass (filters, «Kim kechikdi» grouping).</summary>
    public Guid? DepartmentId { get; private set; }

    public Guid? ParentCaseId { get; private set; }

    /// <summary>§3.5 / §3.7 anchor date.</summary>
    public DateOnly? AnchorDate { get; private set; }

    /// <summary>Tashkent date of the start fact.</summary>
    public DateOnly StartedOn { get; private set; }

    /// <summary>User of a manual start.</summary>
    public Guid? StartedBy { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelReason { get; private set; }

    /// <summary>Concurrency token — +1 on every change, only through this aggregate's methods.</summary>
    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Steps in definition order.</summary>
    public IReadOnlyList<ProcessCaseStep> Steps => _steps;

    /// <summary>
    /// Not persisted. <c>true</c> when this instance carries a change that §14 audits (created,
    /// cancelled, reopened). Reconciler refreshes leave it <c>false</c>. Read by the audit catalog only.
    /// </summary>
    public bool HasAuditedChange { get; private set; }

    /// <summary>Starts a case with its steps (all Locked — the first pass computes their state).</summary>
    public static Result<ProcessCase> Start(ProcessCaseStart start, IReadOnlyList<ProcessStepPlan> plan, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(start.ProcessCode) || start.ProcessCode.Length > ProcessTemplate.MaxCodeLength
            || string.IsNullOrWhiteSpace(start.SubjectRef) || start.SubjectRef.Length > MaxSubjectRefLength
            || start.TemplateVersionId == Guid.Empty
            || !Enum.IsDefined(start.SubjectKind)
            || !HasSubject(start.SubjectKind, start.Links)
            || plan.Count == 0
            || plan.Select(step => step.StepCode).Distinct(StringComparer.Ordinal).Count() != plan.Count)
        {
            return ProcessErrors.Invalid;
        }

        var processCase = new ProcessCase(NewId(), start, at);
        foreach (var stepPlan in plan.OrderBy(step => step.OrderNo))
        {
            var step = ProcessCaseStep.Create(processCase.Id, stepPlan, at);
            if (step.IsFailure)
            {
                return step.Error;
            }

            processCase._steps.Add(step.Value);
        }

        return processCase;
    }

    /// <summary>Compares the client's version with the loaded one.</summary>
    public Result CheckVersion(int expected)
        => expected == Version ? Result.Success() : ProcessErrors.Stale;

    /// <summary>The step with this definition id; <c>null</c> if the case has none.</summary>
    public ProcessCaseStep? FindStep(string stepCode)
        => _steps.FirstOrDefault(step => string.Equals(step.StepCode, stepCode, StringComparison.Ordinal));

    // ───────────────────────── Case lifecycle ─────────────────────────

    /// <summary>
    /// Completes the case (§7.5). Steps still open are Skipped(caseEnded) — when the case ends because
    /// every non-optional step is closed these are the optional ones; when an end fact ends it early
    /// they are whatever was left. <see cref="Reopen"/> reverses both.
    /// </summary>
    public Result Complete(string? outcome, DateTimeOffset at)
    {
        if (Status != ProcessCaseStatus.Active)
        {
            return ProcessErrors.InvalidStatus;
        }

        var cleanOutcome = Clean(outcome);
        if (cleanOutcome is { Length: > MaxOutcomeLength })
        {
            return ProcessErrors.Invalid;
        }

        foreach (var step in _steps.Where(step => !step.IsClosed))
        {
            step.SetSkipped(ProcessStepResolution.CaseEnded, by: null, payload: null, doneAt: null, at);
        }

        Status = ProcessCaseStatus.Completed;
        Outcome = cleanOutcome;
        CompletedAt = at;
        Touch(at);
        return Result.Success();
    }

    /// <summary>Cancels the case — a manager with a reason, or the system (<see cref="CancelBecauseSubjectDeleted"/>).</summary>
    public Result Cancel(string? reason, DateTimeOffset at)
    {
        if (Status != ProcessCaseStatus.Active)
        {
            return ProcessErrors.InvalidStatus;
        }

        var cleanReason = Clean(reason);
        if (cleanReason is null || cleanReason.Length > MaxCancelReasonLength)
        {
            return ProcessErrors.Invalid;
        }

        Status = ProcessCaseStatus.Cancelled;
        CancelReason = cleanReason;
        CancelledAt = at;
        HasAuditedChange = true;
        Touch(at);
        return Result.Success();
    }

    /// <summary>The subject was deleted (§6.9).</summary>
    public Result CancelBecauseSubjectDeleted(DateTimeOffset at) => Cancel(SubjectDeletedReason, at);

    /// <summary>Back to Active (§7.5); steps closed by the case end reopen and the next pass recomputes them.</summary>
    public Result Reopen(DateTimeOffset at)
    {
        if (Status == ProcessCaseStatus.Active)
        {
            return ProcessErrors.InvalidStatus;
        }

        foreach (var step in _steps.Where(step => step.Resolution == ProcessStepResolution.CaseEnded))
        {
            step.SetOpenState(ProcessStepState.Locked, gateCode: null, at);
        }

        Status = ProcessCaseStatus.Active;
        Outcome = null;
        CompletedAt = null;
        CancelledAt = null;
        CancelReason = null;
        HasAuditedChange = true;
        Touch(at);
        return Result.Success();
    }

    // ───────────────────────── Reconciler: case fields ─────────────────────────

    /// <summary>
    /// Fills subject identifiers that are still empty (e.g. <c>employment_id</c> once the candidate is
    /// hired). A different value for an already filled one is an error: the subject never changes.
    /// </summary>
    public Result<bool> LinkSubjects(ProcessCaseLinks links, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(links);

        if (Conflicts(VacancyRequestId, links.VacancyRequestId)
            || Conflicts(CandidateId, links.CandidateId)
            || Conflicts(EmploymentId, links.EmploymentId)
            || Conflicts(ProbationId, links.ProbationId)
            || Conflicts(EmployeeId, links.EmployeeId))
        {
            return ProcessErrors.Invalid;
        }

        var before = (VacancyRequestId, CandidateId, EmploymentId, ProbationId, EmployeeId);
        VacancyRequestId ??= links.VacancyRequestId;
        CandidateId ??= links.CandidateId;
        EmploymentId ??= links.EmploymentId;
        ProbationId ??= links.ProbationId;
        EmployeeId ??= links.EmployeeId;

        return Changed(before != (VacancyRequestId, CandidateId, EmploymentId, ProbationId, EmployeeId), at);
    }

    /// <summary>Refreshes the department (each pass).</summary>
    public bool RefreshDepartment(Guid? departmentId, DateTimeOffset at)
    {
        if (DepartmentId == departmentId)
        {
            return false;
        }

        DepartmentId = departmentId;
        return Changed(true, at);
    }

    /// <summary>Sets the anchor date once the fact that carries it is known.</summary>
    public bool RefreshAnchorDate(DateOnly? anchorDate, DateTimeOffset at)
    {
        if (AnchorDate == anchorDate)
        {
            return false;
        }

        AnchorDate = anchorDate;
        return Changed(true, at);
    }

    // ───────────────────────── Reconciler: steps ─────────────────────────

    /// <summary>
    /// The completion fact holds (§7.4 p.2) — a fact wins over order, so any state may become Done.
    /// A manual Done is kept as it is (a person's word is not overwritten by a later fact).
    /// </summary>
    /// <param name="stepCode">Step id.</param>
    /// <param name="resolution"><see cref="ProcessStepResolution.Fact"/> or <see cref="ProcessStepResolution.Auto"/>.</param>
    /// <param name="doneAt">Time of the fact.</param>
    /// <param name="payload">Fact value (JSON) or <c>null</c>.</param>
    /// <param name="at">Time of the pass.</param>
    public Result<bool> MarkStepDoneByFact(
        string stepCode,
        ProcessStepResolution resolution,
        DateTimeOffset doneAt,
        string? payload,
        DateTimeOffset at)
    {
        if (resolution is not (ProcessStepResolution.Fact or ProcessStepResolution.Auto)
            || !ProcessCaseStep.IsValidPayload(payload))
        {
            return ProcessErrors.Invalid;
        }

        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var step = found.Value;
        if (step.State == ProcessStepState.Done && step.Resolution == ProcessStepResolution.Manual)
        {
            return false;
        }

        return Changed(step.SetDoneByFact(resolution, doneAt, payload, at), at);
    }

    /// <summary>The fact of a fact-Done step disappeared — back to Active (the pass recomputes). Manual Done never reverts.</summary>
    public Result<bool> RevertStepFact(string stepCode, DateTimeOffset at)
    {
        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var step = found.Value;
        if (!step.IsFactDone)
        {
            return false;
        }

        return Changed(step.SetOpenState(ProcessStepState.Active, gateCode: null, at), at);
    }

    /// <summary>The step's <c>when</c> condition is false (§7.4 p.2).</summary>
    public Result<bool> SkipStepByCondition(string stepCode, DateTimeOffset at)
    {
        var found = RecomputableStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        return Changed(found.Value.SetSkipped(ProcessStepResolution.Condition, by: null, payload: null, doneAt: null, at), at);
    }

    /// <summary>A non-optional prerequisite is open, or the <c>when</c> fact is unknown.</summary>
    public Result<bool> LockStep(string stepCode, DateTimeOffset at) => SetOpen(stepCode, ProcessStepState.Locked, null, at);

    /// <summary>Prerequisites met and no blocking gate fails.</summary>
    public Result<bool> ActivateStep(string stepCode, DateTimeOffset at) => SetOpen(stepCode, ProcessStepState.Active, null, at);

    /// <summary>Waiting for another case (state 3, §7.4 p.4).</summary>
    public Result<bool> WaitStep(string stepCode, DateTimeOffset at) => SetOpen(stepCode, ProcessStepState.Waiting, null, at);

    /// <summary>Prerequisites met but the blocking gate <paramref name="gateCode"/> fails (§8.3).</summary>
    public Result<bool> BlockStep(string stepCode, string? gateCode, DateTimeOffset at)
    {
        var cleanGate = Clean(gateCode);
        if (cleanGate is null || cleanGate.Length > ProcessCaseStep.MaxGateCodeLength)
        {
            return ProcessErrors.Invalid;
        }

        return SetOpen(stepCode, ProcessStepState.Blocked, cleanGate, at);
    }

    /// <summary>Recomputed holder and due date (§7.4 p.5).</summary>
    public Result<bool> RefreshStep(string stepCode, string? holderRole, Guid? holderEmployeeId, DateOnly? dueDate, DateTimeOffset at)
    {
        var cleanRole = Clean(holderRole);
        if (cleanRole is { Length: > BlankActorRoles.MaxLength })
        {
            return ProcessErrors.Invalid;
        }

        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        return Changed(found.Value.Refresh(cleanRole, holderEmployeeId, dueDate, at), at);
    }

    /// <summary>The blank and / or record the step reads.</summary>
    public Result<bool> LinkStep(string stepCode, Guid? blankSubmissionId, Guid? recordId, DateTimeOffset at)
    {
        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        return Changed(found.Value.Link(blankSubmissionId, recordId, at), at);
    }

    // ───────────────────────── User actions on steps ─────────────────────────

    /// <summary>
    /// «Bajarildi» (§10 #10) — only an Active step: a Locked step's prerequisites are open, a Blocked
    /// step's gate fails and a closed step is closed. Inputs are validated by the Application layer.
    /// </summary>
    /// <param name="stepCode">Step id.</param>
    /// <param name="by">Acting user.</param>
    /// <param name="payload">Manual inputs (JSON) or <c>null</c> when the step has none.</param>
    /// <param name="evidenceSha256">SHA-256 of the canonical payload for D1 steps (lower-case hex) or <c>null</c>.</param>
    /// <param name="at">Click time.</param>
    public Result CompleteStepManually(string stepCode, Guid by, string? payload, string? evidenceSha256, DateTimeOffset at)
    {
        if (by == Guid.Empty || !ProcessCaseStep.IsValidPayload(payload) || !ProcessCaseStep.IsValidSha256(evidenceSha256))
        {
            return ProcessErrors.Invalid;
        }

        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var step = found.Value;
        if (step.State != ProcessStepState.Active)
        {
            return ProcessErrors.InvalidStepState;
        }

        step.SetDoneManually(by, payload, evidenceSha256, at);
        Touch(at);
        return Result.Success();
    }

    /// <summary>
    /// «Kerak emas» (§10 #11) — an open step skipped by a user: <c>skippable</c>
    /// (<see cref="ProcessStepResolution.UserSkip"/>) or a legacy case's <c>legacySkippable</c>
    /// (<see cref="ProcessStepResolution.Legacy"/>). Which one is allowed is the definition's call.
    /// </summary>
    public Result SkipStepByUser(string stepCode, Guid by, bool legacy, string? payload, DateTimeOffset at)
    {
        if (by == Guid.Empty || !ProcessCaseStep.IsValidPayload(payload) || (legacy && !Legacy))
        {
            return ProcessErrors.Invalid;
        }

        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var step = found.Value;
        if (step.IsClosed)
        {
            return ProcessErrors.InvalidStepState;
        }

        var resolution = legacy ? ProcessStepResolution.Legacy : ProcessStepResolution.UserSkip;
        step.SetSkipped(resolution, by, payload, doneAt: at, at);
        Touch(at);
        return Result.Success();
    }

    /// <summary>
    /// Undo (§10 #12) — reopens a step a user closed. The user may undo their own action within
    /// <see cref="UndoWindow"/>; a <c>processes.manage</c> holder (<paramref name="asManager"/>) any time.
    /// </summary>
    public Result UndoStep(string stepCode, Guid by, bool asManager, DateTimeOffset at)
    {
        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var step = found.Value;
        if (!step.IsUserClosed)
        {
            return ProcessErrors.InvalidStepState;
        }

        if (!asManager && (step.DoneBy != by || step.DoneAt is not { } doneAt || at - doneAt > UndoWindow))
        {
            return ProcessErrors.Forbidden;
        }

        step.Reopen(at);
        Touch(at);
        return Result.Success();
    }

    // ───────────────────────── helpers ─────────────────────────

    private static bool HasSubject(ProcessSubjectKind kind, ProcessCaseLinks links) => kind switch
    {
        ProcessSubjectKind.VacancyRequest => links.VacancyRequestId is not null,
        ProcessSubjectKind.Candidate => links.CandidateId is not null,

        // hire-formalise (a) starts from the candidate; the employment arrives with the hire fact.
        ProcessSubjectKind.Employment => links.EmploymentId is not null || links.CandidateId is not null,
        _ => true,
    };

    private static bool Conflicts(Guid? current, Guid? incoming)
        => current is not null && incoming is not null && current != incoming;

    private Result<bool> SetOpen(string stepCode, ProcessStepState state, string? gateCode, DateTimeOffset at)
    {
        var found = RecomputableStep(stepCode);
        if (found.IsFailure)
        {
            return found.Error;
        }

        return Changed(found.Value.SetOpenState(state, gateCode, at), at);
    }

    /// <summary>A step of an Active case.</summary>
    private Result<ProcessCaseStep> OpenStep(string stepCode)
    {
        if (Status != ProcessCaseStatus.Active)
        {
            return ProcessErrors.InvalidStatus;
        }

        return FindStep(stepCode) is { } step ? step : ProcessErrors.NotFound;
    }

    /// <summary>A step a pass may move between open states: open, or skipped only by its condition.</summary>
    private Result<ProcessCaseStep> RecomputableStep(string stepCode)
    {
        var found = OpenStep(stepCode);
        if (found.IsFailure)
        {
            return found;
        }

        return found.Value.IsRecomputable ? found : ProcessErrors.InvalidStepState;
    }

    private bool Changed(bool changed, DateTimeOffset at)
    {
        if (changed)
        {
            Touch(at);
        }

        return changed;
    }

    private void Touch(DateTimeOffset at)
    {
        Version++;
        UpdatedAt = at;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
