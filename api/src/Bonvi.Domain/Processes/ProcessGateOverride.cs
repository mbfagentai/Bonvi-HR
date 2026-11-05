using Bonvi.Domain.Common;

namespace Bonvi.Domain.Processes;

/// <summary>Gate codes of docs/SPEC-mening-ishlarim.md §8.1 (Q07–Q11 do not exist, C1).</summary>
public static class ProcessGateCodes
{
    public const string Q01 = "Q01";
    public const string Q02 = "Q02";
    public const string Q03 = "Q03";
    public const string Q04 = "Q04";
    public const string Q05 = "Q05";
    public const string Q06 = "Q06";
    public const string Q12 = "Q12";
    public const string Q13 = "Q13";
    public const string Akt = "AKT";

    /// <summary>Every known gate code.</summary>
    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal) { Q01, Q02, Q03, Q04, Q05, Q06, Q12, Q13, Akt };
}

/// <summary>The guarded command that consumed an override (§6.4 <c>consumed_operation</c>).</summary>
public static class ProcessGateOperations
{
    public const string Hire = "hire";
    public const string Terminate = "terminate";

    /// <summary>An engine gate's override: the guarded step closed, so a later undo finds the gate back.</summary>
    public const string Step = "step";

    /// <summary>Column length.</summary>
    public const int MaxLength = 16;

    public static bool IsKnown(string? operation) => operation is Hire or Terminate or Step;
}

/// <summary>
/// A single-use, 24-hour exception to a blocking gate (<c>process_gate_overrides</c>),
/// docs/SPEC-mening-ishlarim.md §6.4, §8.4.
/// </summary>
/// <remarks>
/// <para>
/// Granted on a case gate (<see cref="CaseId"/> set), or for a direct hire without a case
/// (<see cref="CaseId"/> <c>null</c>, <see cref="ForUserId"/> = the operator allowed to hire).
/// Who may grant (<c>processes.override</c> + the hr1 rule) is checked by the Application layer.
/// </para>
/// <para>
/// The guard <see cref="Consume"/>s it inside the guarded command's transaction — a second
/// command, or one after <see cref="ExpiresAt"/>, finds it unusable and the gate blocks again.
/// </para>
/// </remarks>
public sealed class ProcessGateOverride : AggregateRoot
{
    /// <summary>Shortest reason (§6.4).</summary>
    public const int MinReasonLength = 10;

    /// <summary>Longest reason.</summary>
    public const int MaxReasonLength = 1000;

    /// <summary>How long an unused override stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private ProcessGateOverride(
        Guid id,
        string gateCode,
        Guid? caseId,
        string? stepCode,
        Guid? employmentId,
        Guid? candidateId,
        Guid? forUserId,
        string reason,
        Guid grantedBy,
        DateTimeOffset at)
        : base(id)
    {
        GateCode = gateCode;
        CaseId = caseId;
        StepCode = stepCode;
        EmploymentId = employmentId;
        CandidateId = candidateId;
        ForUserId = forUserId;
        Reason = reason;
        GrantedBy = grantedBy;
        GrantedAt = at;
        ExpiresAt = at + Lifetime;
    }

    private ProcessGateOverride() { }

    /// <summary>§8.1 gate code.</summary>
    public string GateCode { get; private set; } = string.Empty;

    /// <summary>The case whose gate is overridden; <c>null</c> for a direct hire.</summary>
    public Guid? CaseId { get; private set; }

    /// <summary>The step carrying the gate (engine gates).</summary>
    public string? StepCode { get; private set; }

    public Guid? EmploymentId { get; private set; }

    public Guid? CandidateId { get; private set; }

    /// <summary>Direct hire: the operator allowed to run the command.</summary>
    public Guid? ForUserId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid GrantedBy { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    /// <summary><see cref="GrantedAt"/> + <see cref="Lifetime"/>.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary><see cref="ProcessGateOperations"/> value of the command that used it.</summary>
    public string? ConsumedOperation { get; private set; }

    /// <summary>Not consumed and not expired at <paramref name="at"/>.</summary>
    public bool IsUsableAt(DateTimeOffset at) => ConsumedAt is null && at < ExpiresAt;

    /// <summary>Override on a case gate (§8.4 <c>POST /cases/{id}/gates/{code}/override</c>).</summary>
    public static Result<ProcessGateOverride> GrantForCase(
        string? gateCode,
        Guid caseId,
        string? stepCode,
        Guid? employmentId,
        Guid? candidateId,
        string? reason,
        Guid grantedBy,
        DateTimeOffset at)
    {
        if (caseId == Guid.Empty)
        {
            return ProcessErrors.Invalid;
        }

        var cleanStep = Clean(stepCode);
        if (cleanStep is { Length: > ProcessCaseStep.MaxStepCodeLength })
        {
            return ProcessErrors.Invalid;
        }

        return Grant(gateCode, caseId, cleanStep, employmentId, candidateId, forUserId: null, reason, grantedBy, at);
    }

    /// <summary>Direct hire without a candidate (§8.4 <c>POST /gate-overrides/direct-hire</c>) — gate Q01.</summary>
    public static Result<ProcessGateOverride> GrantForDirectHire(Guid forUserId, string? reason, Guid grantedBy, DateTimeOffset at)
    {
        if (forUserId == Guid.Empty)
        {
            return ProcessErrors.Invalid;
        }

        return Grant(ProcessGateCodes.Q01, caseId: null, stepCode: null, employmentId: null, candidateId: null, forUserId, reason, grantedBy, at);
    }

    /// <summary>Uses the override for <paramref name="operation"/> — once, before it expires.</summary>
    public Result Consume(string? operation, DateTimeOffset at)
    {
        if (!ProcessGateOperations.IsKnown(operation))
        {
            return ProcessErrors.Invalid;
        }

        if (!IsUsableAt(at))
        {
            return ProcessErrors.OverrideNotUsable;
        }

        ConsumedAt = at;
        ConsumedOperation = operation;
        return Result.Success();
    }

    private static Result<ProcessGateOverride> Grant(
        string? gateCode,
        Guid? caseId,
        string? stepCode,
        Guid? employmentId,
        Guid? candidateId,
        Guid? forUserId,
        string? reason,
        Guid grantedBy,
        DateTimeOffset at)
    {
        var cleanGate = Clean(gateCode);
        if (cleanGate is null || !ProcessGateCodes.All.Contains(cleanGate) || grantedBy == Guid.Empty)
        {
            return ProcessErrors.Invalid;
        }

        var cleanReason = Clean(reason);
        if (cleanReason is null || cleanReason.Length < MinReasonLength || cleanReason.Length > MaxReasonLength)
        {
            return ProcessErrors.Invalid;
        }

        return new ProcessGateOverride(
            NewId(), cleanGate, caseId, stepCode, employmentId, candidateId, forUserId, cleanReason, grantedBy, at);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
