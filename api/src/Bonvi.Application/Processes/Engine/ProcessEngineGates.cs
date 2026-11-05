using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// The gates a pass evaluates (§8.3): Q03 on r01-9, Q04 on r02-13, the engine part of Q12 on ex-10.
/// </summary>
/// <remarks>
/// Every other <c>block</c> in a template — Q01, Q13, Q12 on ex-8 — is enforced by the Hire / Terminate
/// hooks (§8.2): such a step stays Active, otherwise its holder would never see the task the hook is
/// guarding. Warn gates only colour the card at read time. A legacy case and a gate mode other than
/// <c>enforce</c> never block (§6.6, §7.6). A usable override on this very step and gate lifts the block for as long
/// as it stays usable (§8.4); Q12 only from the termination date on (§8.1).
/// </remarks>
public static class ProcessEngineGates
{
    /// <summary>The first gate that blocks <paramref name="step"/> now; <c>null</c> when none does.</summary>
    /// <param name="step">The step.</param>
    /// <param name="processCase">Its case.</param>
    /// <param name="facts">The case's facts.</param>
    /// <param name="subject">The case's subject.</param>
    /// <param name="gatesEnforced">Whether the gate mode is <c>enforce</c>.</param>
    /// <param name="overrides">The usable overrides of this case.</param>
    /// <param name="today">The pass's Tashkent date.</param>
    public static string? FirstFailing(
        ProcessStep step,
        ProcessCase processCase,
        ProcessCaseFacts facts,
        ProcessCaseSubject subject,
        bool gatesEnforced,
        IReadOnlyCollection<ProcessGateOverride> overrides,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(processCase);

        if (!gatesEnforced || processCase.Legacy)
        {
            return null;
        }

        return step.Gates
            .Where(gate => gate.Mode == ProcessGateMode.Block
                && Fails(gate.Code, step.Id, facts, subject)
                && !Overridden(gate.Code, step.Id, subject, overrides, today))
            .Select(gate => gate.Code)
            .FirstOrDefault();
    }

    private static bool Overridden(string gateCode, string stepId, ProcessCaseSubject subject, IReadOnlyCollection<ProcessGateOverride> overrides, DateOnly today)
        => overrides.Any(row => row.GateCode == gateCode && row.StepCode == stepId)
            && (gateCode != Definitions.ProcessGateCodes.Q12 || subject.TerminationDate <= today);

    private static bool Fails(string gateCode, string stepId, ProcessCaseFacts facts, ProcessCaseSubject subject)
        => (gateCode, stepId) switch
        {
            (Definitions.ProcessGateCodes.Q03, ProcessStepIds.PayrollHandover) => !subject.EmployeeHasPinfl,
            (Definitions.ProcessGateCodes.Q04, ProcessStepIds.FirstDay) => !subject.SafetyInstructionDone,
            (Definitions.ProcessGateCodes.Q12, ProcessStepIds.FinalSettlement) => !facts.Get(ProcessFacts.DisF09Approved).Holds,
            _ => false,
        };
}
