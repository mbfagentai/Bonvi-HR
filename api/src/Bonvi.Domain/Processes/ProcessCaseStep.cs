using System.Text;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>
/// Step plan taken from the definition when a case starts — input of <see cref="ProcessCase.Start"/>.
/// </summary>
/// <param name="StepCode">Definition step <c>id</c> (<c>r02-3</c>).</param>
/// <param name="OrderNo">Definition order.</param>
/// <param name="BoardColumn">Definition <c>column</c>.</param>
/// <param name="HolderRole">§9.1 role code; <c>null</c> for system steps.</param>
public sealed record ProcessStepPlan(string StepCode, int OrderNo, string BoardColumn, string? HolderRole);

/// <summary>
/// One step of a process case (<c>process_case_steps</c>), docs/SPEC-mening-ishlarim.md §6.3.
/// </summary>
/// <remarks>
/// <para>
/// Meaningless without its case — no repository; created and changed only through
/// <see cref="ProcessCase"/> (whether a transition is allowed depends on the case status), so the
/// mutators are <c>internal</c> and each returns whether anything changed: the reconciler saves
/// only when something did (§7.4 p.6).
/// </para>
/// <para>
/// <b>Who closed it</b> is <see cref="Resolution"/>: a fact-Done step reverts when its fact
/// disappears, a manual Done never does.
/// </para>
/// </remarks>
public sealed class ProcessCaseStep : Entity
{
    /// <summary>Step code length.</summary>
    public const int MaxStepCodeLength = 64;

    /// <summary>Board column key length.</summary>
    public const int MaxBoardColumnLength = 32;

    /// <summary>Gate code length (<c>Q12</c>, <c>AKT</c>).</summary>
    public const int MaxGateCodeLength = 16;

    /// <summary>Largest payload (UTF-8 bytes) — 64 KB.</summary>
    public const int MaxPayloadBytes = 64 * 1024;

    /// <summary>Hex length of a SHA-256.</summary>
    public const int Sha256HexLength = 64;

    private ProcessCaseStep(Guid id, Guid caseId, ProcessStepPlan plan, DateTimeOffset at)
        : base(id)
    {
        CaseId = caseId;
        StepCode = plan.StepCode;
        OrderNo = plan.OrderNo;
        BoardColumn = plan.BoardColumn;
        HolderRole = plan.HolderRole;
        State = ProcessStepState.Locked;
        UpdatedAt = at;
    }

    private ProcessCaseStep() { }

    public Guid CaseId { get; private set; }

    /// <summary>Definition step <c>id</c> — unique within the case.</summary>
    public string StepCode { get; private set; } = string.Empty;

    public int OrderNo { get; private set; }

    /// <summary>Definition <c>column</c> — the board column the card shows in.</summary>
    public string BoardColumn { get; private set; } = string.Empty;

    public ProcessStepState State { get; private set; }

    /// <summary>How a Done / Skipped step was closed; <c>null</c> while open.</summary>
    public ProcessStepResolution? Resolution { get; private set; }

    /// <summary>§9.1 role code; <c>null</c> for system steps.</summary>
    public string? HolderRole { get; private set; }

    /// <summary>Resolved person (department head, subject employee); <c>null</c> for office roles.</summary>
    public Guid? HolderEmployeeId { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>Time of the FACT (not of the pass), or of the user's click.</summary>
    public DateTimeOffset? DoneAt { get; private set; }

    /// <summary>The user who closed it by hand; <c>null</c> for fact / auto / condition.</summary>
    public Guid? DoneBy { get; private set; }

    /// <summary>Manual inputs or the fact value (JSON, <c>jsonb</c>).</summary>
    public string? Payload { get; private set; }

    /// <summary>SHA-256 of the canonical payload JSON for manual D1 steps (lower-case hex).</summary>
    public string? EvidenceSha256 { get; private set; }

    /// <summary>The failing gate while <see cref="State"/> is Blocked.</summary>
    public string? BlockedGateCode { get; private set; }

    /// <summary>The blank submission the step reads.</summary>
    public Guid? LinkedBlankSubmissionId { get; private set; }

    /// <summary>Vacancy request / candidate form / employment event the step reads.</summary>
    public Guid? LinkedRecordId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Not persisted. <c>true</c> when this instance carries a user action that §14 audits
    /// (manual Done, user skip, undo). Fact-driven changes leave it <c>false</c> — they are derived and
    /// their source change is audited where it happened. Read by the audit catalog only.
    /// </summary>
    public bool HasAuditedChange { get; private set; }

    /// <summary>Done or Skipped.</summary>
    public bool IsClosed => State is ProcessStepState.Done or ProcessStepState.Skipped;

    /// <summary>Done by a fact or by the system — the kind the reconciler may revert.</summary>
    public bool IsFactDone => State == ProcessStepState.Done
        && Resolution is ProcessStepResolution.Fact or ProcessStepResolution.Auto;

    /// <summary>Closed by a user (manual Done, «Kerak emas», legacy skip) — the kind «undo» reopens.</summary>
    public bool IsUserClosed => Resolution is ProcessStepResolution.Manual
        or ProcessStepResolution.UserSkip
        or ProcessStepResolution.Legacy;

    /// <summary>Open, or skipped only because its condition was false — the states a pass may recompute.</summary>
    internal bool IsRecomputable => !IsClosed
        || (State == ProcessStepState.Skipped && Resolution == ProcessStepResolution.Condition);

    internal static Result<ProcessCaseStep> Create(Guid caseId, ProcessStepPlan plan, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(plan.StepCode) || plan.StepCode.Length > MaxStepCodeLength
            || string.IsNullOrWhiteSpace(plan.BoardColumn) || plan.BoardColumn.Length > MaxBoardColumnLength
            || plan.OrderNo < 0
            || plan.HolderRole is { Length: 0 or > BlankActorRoles.MaxLength })
        {
            return ProcessErrors.Invalid;
        }

        return new ProcessCaseStep(NewId(), caseId, plan, at);
    }

    internal static bool IsValidPayload(string? payload)
        => payload is null || (!string.IsNullOrWhiteSpace(payload) && Encoding.UTF8.GetByteCount(payload) <= MaxPayloadBytes);

    internal static bool IsValidSha256(string? value)
        => value is null || (value.Length == Sha256HexLength && value.All(char.IsAsciiHexDigitLower));

    internal bool SetOpenState(ProcessStepState state, string? gateCode, DateTimeOffset at)
    {
        if (State == state && Resolution is null && BlockedGateCode == gateCode)
        {
            return false;
        }

        State = state;
        Resolution = null;
        BlockedGateCode = gateCode;
        ClearClosure();
        UpdatedAt = at;
        return true;
    }

    internal bool SetDoneByFact(
        ProcessStepResolution resolution,
        DateTimeOffset doneAt,
        string? payload,
        DateTimeOffset at)
    {
        if (State == ProcessStepState.Done && Resolution == resolution && DoneAt == doneAt && Payload == payload)
        {
            return false;
        }

        State = ProcessStepState.Done;
        Resolution = resolution;
        DoneAt = doneAt;
        DoneBy = null;
        Payload = payload;
        EvidenceSha256 = null;
        BlockedGateCode = null;
        UpdatedAt = at;
        return true;
    }

    internal bool SetSkipped(ProcessStepResolution resolution, Guid? by, string? payload, DateTimeOffset? doneAt, DateTimeOffset at)
    {
        if (State == ProcessStepState.Skipped && Resolution == resolution && DoneBy == by && Payload == payload)
        {
            return false;
        }

        State = ProcessStepState.Skipped;
        Resolution = resolution;
        DoneAt = doneAt;
        DoneBy = by;
        Payload = payload;
        EvidenceSha256 = null;
        BlockedGateCode = null;
        UpdatedAt = at;
        HasAuditedChange |= by is not null;
        return true;
    }

    internal void SetDoneManually(Guid by, string? payload, string? evidenceSha256, DateTimeOffset at)
    {
        State = ProcessStepState.Done;
        Resolution = ProcessStepResolution.Manual;
        DoneAt = at;
        DoneBy = by;
        Payload = payload;
        EvidenceSha256 = evidenceSha256;
        BlockedGateCode = null;
        UpdatedAt = at;
        HasAuditedChange = true;
    }

    /// <summary>Reopens a user-closed step (undo) — the next pass recomputes Locked / Blocked.</summary>
    internal void Reopen(DateTimeOffset at)
    {
        State = ProcessStepState.Active;
        Resolution = null;
        BlockedGateCode = null;
        ClearClosure();
        UpdatedAt = at;
        HasAuditedChange = true;
    }

    internal bool Refresh(string? holderRole, Guid? holderEmployeeId, DateOnly? dueDate, DateTimeOffset at)
    {
        if (HolderRole == holderRole && HolderEmployeeId == holderEmployeeId && DueDate == dueDate)
        {
            return false;
        }

        HolderRole = holderRole;
        HolderEmployeeId = holderEmployeeId;
        DueDate = dueDate;
        UpdatedAt = at;
        return true;
    }

    internal bool Link(Guid? blankSubmissionId, Guid? recordId, DateTimeOffset at)
    {
        if (LinkedBlankSubmissionId == blankSubmissionId && LinkedRecordId == recordId)
        {
            return false;
        }

        LinkedBlankSubmissionId = blankSubmissionId;
        LinkedRecordId = recordId;
        UpdatedAt = at;
        return true;
    }

    private void ClearClosure()
    {
        DoneAt = null;
        DoneBy = null;
        Payload = null;
        EvidenceSha256 = null;
    }
}
