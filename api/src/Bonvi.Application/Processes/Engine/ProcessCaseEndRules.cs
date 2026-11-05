using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Processes.Engine;

/// <summary>Why a case ends now. <see cref="Outcome"/> may be <c>null</c>: not every process has an outcome.</summary>
public sealed record ProcessCaseEnd(string? Outcome);

/// <summary>
/// §7.5 — when a case ends. Evaluated after the pass has closed the steps it can, so a fact that both
/// closes a step and ends the case shows as Done rather than Skipped(caseEnded).
/// </summary>
/// <remarks>
/// <para>
/// <b>The hire does not end a hire-candidate case by itself.</b> r02-13 (the first day, HR-F11, gate Q04)
/// comes after the hire; ending at the hire would skip the very step the gate guards. So <c>hired</c> is
/// the outcome once r02-13 is Done too.
/// </para>
/// <para>
/// <b>«Back to Active» of a dismissal needs a «was»</b>: a freshly started case also has an active
/// employment and no F09 yet, so <c>dismissalWasUnderWay</c> carries whether the act or the
/// termination had counted before this pass.
/// </para>
/// </remarks>
public static class ProcessCaseEndRules
{
    /// <summary>The end of the case, or <c>null</c> while it runs.</summary>
    /// <param name="processCase">The case, with this pass's step states.</param>
    /// <param name="definition">Its definition.</param>
    /// <param name="facts">Its facts.</param>
    /// <param name="subject">Its subject rows.</param>
    /// <param name="dismissalWasUnderWay">ex-2 or ex-8 was fact-Done when the pass began.</param>
    public static ProcessCaseEnd? Evaluate(
        ProcessCase processCase,
        ProcessDefinition definition,
        ProcessCaseFacts facts,
        ProcessCaseSubject subject,
        bool dismissalWasUnderWay)
    {
        ArgumentNullException.ThrowIfNull(processCase);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(subject);

        return EndFact(processCase, facts, subject, dismissalWasUnderWay)
            ?? (AllClosed(processCase, definition) ? new ProcessCaseEnd(OutcomeOfFullRun(processCase, facts)) : null);
    }

    private static ProcessCaseEnd? EndFact(ProcessCase processCase, ProcessCaseFacts facts, ProcessCaseSubject subject, bool dismissalWasUnderWay)
        => processCase.ProcessCode switch
        {
            ProcessCodes.HireRequest => RequestEnd(facts),
            ProcessCodes.HireCandidate => CandidateEnd(processCase, facts),

            // Beyond §7.5: an offer accepted, then the candidate rejected or gone before any hire — nothing
            // can ever close this case (r01-5 needs an employment), and a hire does not come for it.
            ProcessCodes.HireFormalise when processCase.EmploymentId is null => CandidateGone(facts),

            ProcessCodes.Probation => ProbationEnd(facts),
            ProcessCodes.Dismissal => DismissalEnd(processCase, facts, subject, dismissalWasUnderWay),
            _ => null,
        };

    private static ProcessCaseEnd? RequestEnd(ProcessCaseFacts facts)
    {
        if (facts.Get(ProcessFacts.VrDecided) is { Holds: true, Value: "rejected" or "cancelled" } decided)
        {
            return new ProcessCaseEnd(decided.Value);
        }

        return facts.Get(ProcessFacts.VrFilled).Holds ? new ProcessCaseEnd(ProcessOutcomes.Filled) : null;
    }

    private static ProcessCaseEnd? CandidateEnd(ProcessCase processCase, ProcessCaseFacts facts)
    {
        if (IsDone(processCase, ProcessStepIds.Offer)
            && facts.Get(ProcessFactNames.OfferBranch).Value == ProcessCaseFacts.BranchDecline)
        {
            return new ProcessCaseEnd(ProcessOutcomes.Declined);
        }

        if (CandidateGone(facts) is { } gone)
        {
            return gone;
        }

        return facts.Get(ProcessFacts.CandHired).Holds && IsDone(processCase, ProcessStepIds.FirstDay)
            ? new ProcessCaseEnd(ProcessOutcomes.Hired)
            : null;
    }

    /// <summary><c>cand.status</c> 2 / 4 / 6 → the outcome is the status, at any step.</summary>
    private static ProcessCaseEnd? CandidateGone(ProcessCaseFacts facts)
    {
        var status = facts.Get(ProcessFacts.CandStatus).Value;
        return status == Number(CandidateStatus.Rejected) ? new ProcessCaseEnd("rejected")
            : status == Number(CandidateStatus.Withdrawn) ? new ProcessCaseEnd(ProcessOutcomes.Withdrawn)
            : status == Number(CandidateStatus.Blacklisted) ? new ProcessCaseEnd("blacklisted")
            : null;
    }

    private static ProcessCaseEnd? ProbationEnd(ProcessCaseFacts facts)
    {
        var resolved = facts.Get(ProcessFacts.ProbResolved);
        if (!resolved.Holds)
        {
            return null;
        }

        return new ProcessCaseEnd(
            resolved.Value == Number(ProbationStatus.Passed) ? "passed"
            : resolved.Value == Number(ProbationStatus.Failed) ? "failed"
            : resolved.Value == Number(ProbationStatus.LeftEarly) ? "leftEarly"
            : "cancelled");
    }

    private static ProcessCaseEnd? DismissalEnd(ProcessCase processCase, ProcessCaseFacts facts, ProcessCaseSubject subject, bool wasUnderWay)
    {
        if (IsDone(processCase, ProcessStepIds.ActRegistered))
        {
            return new ProcessCaseEnd(ProcessOutcomes.Terminated);
        }

        return wasUnderWay
            && subject.EmploymentStatus is not EmploymentStatus.Terminated
            && !facts.Get(ProcessFacts.DisF09Opened).Holds
                ? new ProcessCaseEnd(ProcessOutcomes.Withdrawn)
                : null;
    }

    private static string? OutcomeOfFullRun(ProcessCase processCase, ProcessCaseFacts facts)
        => processCase.ProcessCode == ProcessCodes.HireCandidate && facts.Get(ProcessFacts.CandHired).Holds
            ? ProcessOutcomes.Hired
            : null;

    private static bool AllClosed(ProcessCase processCase, ProcessDefinition definition)
        => definition.Steps
            .Where(step => !step.Optional)
            .All(step => processCase.FindStep(step.Id) is null or { IsClosed: true });

    private static bool IsDone(ProcessCase processCase, string stepId)
        => processCase.FindStep(stepId) is { State: ProcessStepState.Done };

    private static string Number<TEnum>(TEnum status)
        where TEnum : struct, Enum
        => Convert.ToInt32(status, global::System.Globalization.CultureInfo.InvariantCulture)
            .ToString(global::System.Globalization.CultureInfo.InvariantCulture);
}
