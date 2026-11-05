using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

/// <summary>A holder as a step stores it: a role and, for a person role, the resolved employee.</summary>
public sealed record ProcessHolderRef(string? Role, Guid? EmployeeId);

/// <summary>How the reads explain a gate (§8.1, §8.3): its text, who can clear it, whether a warning fires.</summary>
public static class ProcessGateReading
{
    /// <summary>The <c>jarayon.*</c> code whose translation is a blocking gate's text; warn gates have none.</summary>
    public static string? ErrorCodeOf(string? gateCode) => ErrorOf(gateCode)?.Code;

    /// <summary>The refusal of a blocking gate; warn gates have none.</summary>
    public static Error? ErrorOf(string? gateCode) => gateCode switch
    {
        ProcessGateCodes.Q03 => ProcessErrors.PinflMissing,
        ProcessGateCodes.Q04 => ProcessErrors.SafetyBriefingMissing,
        ProcessGateCodes.Q12 => ProcessErrors.HandoverActOpen,
        _ => null,
    };

    /// <summary>
    /// Warnings are computed here because they are never stored: the engine only blocks (§8.3).
    /// Q02, Q05, Q06 fire on an open step past its due, Locked ones too: the legal term runs whatever holds the
    /// step up. Q06's «checklist incomplete» cannot occur: a required checklist has to be complete for «Bajarildi».
    /// </summary>
    public static IReadOnlyList<string> Warnings(ProcessStep? definition, ProcessStepRow step, DateOnly today, bool handoverActEmpty)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (definition is null || !step.IsOpen)
        {
            return [];
        }

        var pastDue = step.DueDate is { } due && due < today;

        return
        [
            .. definition.Gates
                .Where(gate => gate.Mode == ProcessGateMode.Warn)
                .Select(gate => gate.Code)
                .Where(code => code switch
                {
                    ProcessGateCodes.Q02 or ProcessGateCodes.Q05 or ProcessGateCodes.Q06 => pastDue,
                    ProcessGateCodes.Akt => handoverActEmpty,
                    _ => false,
                }),
        ];
    }

    /// <summary>
    /// §8.3: the holder of the thing a Blocked step misses. Q04 → the holder of step <c>mm</c> of the child
    /// hire-formalise case (safety engineer until that case exists); Q12 → the first open HR-F09 step of the case.
    /// Any other gate (Q03) is cleared by the step's own holder.
    /// </summary>
    public static async Task<IReadOnlyDictionary<(Guid CaseId, string StepCode), ProcessHolderRef>> ResolversAsync(
        IReadOnlyCollection<ProcessStepRow> steps,
        IReadOnlyDictionary<Guid, ProcessCaseRow> cases,
        IReadOnlyDictionary<Guid, ProcessDefinition> definitions,
        IProcessReadModel readModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(readModel);

        var blocked = steps.Where(step => step.State == ProcessStepState.Blocked).ToList();
        if (blocked.Count == 0)
        {
            return new Dictionary<(Guid, string), ProcessHolderRef>();
        }

        var safetyByParent = await SafetyStepsAsync(blocked, readModel, cancellationToken).ConfigureAwait(false);
        var handoverByCase = await HandoverHoldersAsync(blocked, cases, definitions, readModel, cancellationToken).ConfigureAwait(false);

        return blocked.ToDictionary(
            step => (step.CaseId, step.StepCode),
            step => step.BlockedGateCode switch
            {
                ProcessGateCodes.Q04 => safetyByParent.GetValueOrDefault(step.CaseId)
                    ?? new ProcessHolderRef(ProcessRoles.SafetyEngineer, null),
                ProcessGateCodes.Q12 when handoverByCase.TryGetValue(step.CaseId, out var holder) => holder,
                _ => new ProcessHolderRef(step.HolderRole, step.HolderEmployeeId),
            });
    }

    private static async Task<Dictionary<Guid, ProcessHolderRef>> SafetyStepsAsync(
        List<ProcessStepRow> blocked,
        IProcessReadModel readModel,
        CancellationToken cancellationToken)
    {
        var caseIds = blocked
            .Where(step => step.BlockedGateCode == ProcessGateCodes.Q04)
            .Select(step => step.CaseId)
            .ToHashSet();
        if (caseIds.Count == 0)
        {
            return [];
        }

        var rows = await readModel
            .ListChildStepsAsync(caseIds, ProcessStepIds.SafetyInstruction, cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.ParentCaseId)
            .ToDictionary(group => group.Key, group => new ProcessHolderRef(group.First().Step.HolderRole, group.First().Step.HolderEmployeeId));
    }

    private static async Task<Dictionary<Guid, ProcessHolderRef>> HandoverHoldersAsync(
        List<ProcessStepRow> blocked,
        IReadOnlyDictionary<Guid, ProcessCaseRow> cases,
        IReadOnlyDictionary<Guid, ProcessDefinition> definitions,
        IProcessReadModel readModel,
        CancellationToken cancellationToken)
    {
        var caseIds = blocked
            .Where(step => step.BlockedGateCode == ProcessGateCodes.Q12)
            .Select(step => step.CaseId)
            .ToHashSet();
        if (caseIds.Count == 0)
        {
            return [];
        }

        var rows = await readModel.ListStepsOfCasesAsync(caseIds, cancellationToken).ConfigureAwait(false);

        var holders = new Dictionary<Guid, ProcessHolderRef>();
        foreach (var row in rows.Where(row => row.IsOpen).OrderBy(row => row.OrderNo))
        {
            var isHandover = cases.TryGetValue(row.CaseId, out var processCase)
                && definitions.TryGetValue(processCase.TemplateVersionId, out var definition)
                && definition.FindStep(row.StepCode)?.Blank == ProcessBlanks.F09;

            if (isHandover && row.HolderRole is not null)
            {
                holders.TryAdd(row.CaseId, new ProcessHolderRef(row.HolderRole, row.HolderEmployeeId));
            }
        }

        return holders;
    }
}
