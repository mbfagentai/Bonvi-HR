using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Processes.Inputs;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Commands;

/// <summary>POST <c>/cases/{id}/steps/{code}/done</c>.</summary>
/// <param name="CaseId">The case.</param>
/// <param name="StepCode">The step.</param>
/// <param name="Version">The case version the client acted on.</param>
/// <param name="Inputs">The manual inputs, see <see cref="ProcessManualInputs"/>.</param>
/// <param name="MissingReason"><c>processes.manage</c> only: why the step closes while required data is missing. Ignored when none is.</param>
public sealed record CompleteProcessStepCommand(Guid CaseId, string StepCode, int? Version, JsonElement? Inputs, string? MissingReason);

/// <summary>POST <c>/cases/{id}/steps/{code}/skip</c>.</summary>
/// <param name="CaseId">The case.</param>
/// <param name="StepCode">The step.</param>
/// <param name="Version">The case version the client acted on.</param>
/// <param name="Reason">Required when only the legacy rule allows the skip (§10 #11).</param>
public sealed record SkipProcessStepCommand(Guid CaseId, string StepCode, int? Version, string? Reason);

/// <summary>POST <c>/cases/{id}/steps/{code}/undo</c>.</summary>
public sealed record UndoProcessStepCommand(Guid CaseId, string StepCode, int? Version);

/// <summary>A step ready to be decided on: found, versioned, with who is asking.</summary>
internal sealed record ProcessStepTarget(
    ProcessCase Case,
    ProcessCaseStep Step,
    ProcessDefinition? CaseDefinition,
    ProcessStep? Definition,
    ProcessCaseRow CaseRow,
    ProcessStepRow StepRow,
    ProcessViewer Viewer,
    Guid UserId,
    DateTimeOffset At);

/// <summary>
/// What every step command does around its one decision (§10 #10–#12): brings the case up to date, loads it, checks
/// the version, saves, and answers with the case as the screen reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The case's pass runs first</b>, in its own scope, so the decision is made on current facts: a JSHSHIR removed a
/// minute ago is a Blocked step here, not an Active one left by the last pass. The pass moves the version only when
/// something really changed, and then the client has acted on a state that is gone: 409, and it looks again.
/// </para>
/// <para>
/// <b>No engine lock.</b> A step write touches one case and the <c>version</c> token guards it against a pass running
/// alongside: the loser of that race, either side, writes nothing. A pass that loses is journalled and runs again
/// within seconds.
/// </para>
/// </remarks>
public sealed class ProcessStepWriter
{
    private readonly ProcessInlineReconcile _inline;
    private readonly IProcessCaseRepository _cases;
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly IProcessEngineJournal _journal;
    private readonly ProcessDefinitionCache _definitions;
    private readonly ProcessViewerLoader _viewers;
    private readonly GetProcessCaseQueryHandler _details;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ProcessStepWriter(
        ProcessInlineReconcile inline,
        IProcessCaseRepository cases,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        ProcessDefinitionCache definitions,
        ProcessViewerLoader viewers,
        GetProcessCaseQueryHandler details,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _inline = inline;
        _cases = cases;
        _versions = versions;
        _journal = journal;
        _definitions = definitions;
        _viewers = viewers;
        _details = details;
        _fault = fault;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    internal async Task<Result<ProcessCaseDetailDto>> ActAsync(
        Guid caseId,
        string stepCode,
        int? version,
        Func<ProcessStepTarget, CancellationToken, Task<Result>> decide,
        CancellationToken cancellationToken)
    {
        if (version is not { } expected)
        {
            return ProcessErrors.Invalid;
        }

        await _inline.BeforeCaseReadAsync(caseId, cancellationToken).ConfigureAwait(false);

        var target = await LoadAsync(caseId, stepCode, expected, cancellationToken).ConfigureAwait(false);
        if (target.IsFailure)
        {
            return target.Error;
        }

        var decided = await decide(target.Value, cancellationToken).ConfigureAwait(false);
        if (decided.IsFailure)
        {
            return decided.Error;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        return await _details.HandleAsync(caseId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ProcessStepTarget>> LoadAsync(Guid caseId, string stepCode, int version, CancellationToken cancellationToken)
    {
        var (viewer, _) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);

        // ProcessStepAbility.CanUndo compares DoneBy with the viewer's id, and both are null on a fact-closed step.
        if (viewer.UserId is not { } userId)
        {
            return ProcessErrors.Forbidden;
        }

        var processCase = await _cases.GetWithStepsAsync(caseId, cancellationToken).ConfigureAwait(false);
        if (processCase is null)
        {
            return ProcessErrors.NotFound;
        }

        var checkedVersion = processCase.CheckVersion(version);
        if (checkedVersion.IsFailure)
        {
            return checkedVersion.Error;
        }

        if (processCase.FindStep(stepCode) is not { } step)
        {
            return ProcessErrors.NotFound;
        }

        if (processCase.Status != ProcessCaseStatus.Active)
        {
            return ProcessErrors.InvalidStatus;
        }

        var definitions = await _definitions
            .GetAsync([processCase.TemplateVersionId], _versions, _journal, cancellationToken)
            .ConfigureAwait(false);

        var caseDefinition = definitions.GetValueOrDefault(processCase.TemplateVersionId);

        return new ProcessStepTarget(
            processCase,
            step,
            caseDefinition,
            caseDefinition?.Steps.FirstOrDefault(row => row.Id == stepCode),
            ProcessCaseWriteRows.CaseRow(processCase),
            ProcessCaseWriteRows.StepRow(step),
            viewer,
            userId,
            _clock.UtcNow);
    }
}

/// <summary>«Bajarildi» (§10 #10): the step's state (a failing gate shows there), who may act (§9.3), then the inputs.</summary>
public sealed class CompleteProcessStepCommandHandler
{
    /// <summary>§6.3: the signature level whose manual close keeps a hash of what was entered.</summary>
    private const string EvidencedSignature = "D1";

    private readonly ProcessStepWriter _writer;
    private readonly IProcessFactSource _facts;
    private readonly IProcessStageBindingRepository _bindings;
    private readonly ProcessInputAutoLoader _autoItems;
    private readonly IAuditLogger _audit;

    public CompleteProcessStepCommandHandler(
        ProcessStepWriter writer,
        IProcessFactSource facts,
        IProcessStageBindingRepository bindings,
        ProcessInputAutoLoader autoItems,
        IAuditLogger audit)
    {
        _writer = writer;
        _facts = facts;
        _bindings = bindings;
        _autoItems = autoItems;
        _audit = audit;
    }

    public Task<Result<ProcessCaseDetailDto>> HandleAsync(CompleteProcessStepCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _writer.ActAsync(
            command.CaseId,
            command.StepCode,
            command.Version,
            (target, token) => CompleteAsync(target, command, token),
            cancellationToken);
    }

    private async Task<Result> CompleteAsync(ProcessStepTarget target, CompleteProcessStepCommand command, CancellationToken cancellationToken)
    {
        if (target.Step.State == ProcessStepState.Blocked && ProcessGateReading.ErrorOf(target.Step.BlockedGateCode) is { } gate)
        {
            return gate;
        }

        if (target.Step.State != ProcessStepState.Active)
        {
            return ProcessErrors.InvalidStepState;
        }

        if (!ProcessStepAbility.CanComplete(target.Definition, target.StepRow, target.CaseRow, target.Viewer))
        {
            // Holding it and still not allowed: this is not a step a person closes.
            return ProcessStepAbility.MayAct(target.StepRow, target.Viewer) ? ProcessErrors.InvalidStepState : ProcessErrors.Forbidden;
        }

        var definition = target.Definition!;

        var facts = await FactsWhenAskedAsync(target, cancellationToken).ConfigureAwait(false);

        var autoItems = await AutoItemsAsync(target, cancellationToken).ConfigureAwait(false);

        var outcome = ProcessManualInputs.Validate(
            definition.Manual,
            command.Inputs,
            name => facts?.Get(name) ?? FactValue.NotHeld,
            autoItems,
            command.MissingReason,
            target.Viewer.CanManage);

        if (outcome.IsFailure)
        {
            return outcome.Error;
        }

        var evidence = definition.Signature == EvidencedSignature && outcome.Value.Payload is { } canonical
            ? ProcessManualInputs.EvidenceOf(canonical)
            : null;

        var completed = target.Case.CompleteStepManually(target.Step.StepCode, target.UserId, outcome.Value.Payload, evidence, target.At);

        if (completed.IsSuccess && outcome.Value.MissingOverride is { } missingOverride)
        {
            AuditOverride(target, missingOverride);
        }

        return completed;
    }

    /// <summary>The same loader the read uses, so what the screen showed and what is enforced can't disagree.</summary>
    private async Task<ProcessStepAutoItems?> AutoItemsAsync(ProcessStepTarget target, CancellationToken cancellationToken)
    {
        var processCase = target.Case;
        if (!ProcessInputSources.Covers(processCase.ProcessCode, target.Definition!))
        {
            return null;
        }

        var items = await _autoItems
            .LoadAsync(
                processCase.ProcessCode,
                target.CaseDefinition!,
                processCase.Steps.ToDictionary(step => step.StepCode, step => step.State, StringComparer.Ordinal),
                processCase.EmployeeId,
                processCase.EmploymentId,
                processCase.CandidateId,
                cancellationToken)
            .ConfigureAwait(false);

        return items.GetValueOrDefault(target.Step.StepCode);
    }

    private void AuditOverride(ProcessStepTarget target, ProcessMissingOverride missingOverride)
    {
        var items = new JsonArray();
        foreach (var item in missingOverride.Items)
        {
            items.Add(new JsonObject { ["input"] = item.Input, ["item"] = item.Value });
        }

        _audit.Record(
            AuditAction.Update,
            nameof(ProcessCase),
            target.Case.Id.ToString(),
            newValues: new JsonObject
            {
                ["action"] = "closedWithMissingData",
                ["step"] = target.Step.StepCode,
                ["reason"] = missingOverride.Reason,
                ["items"] = items,
            }.ToJsonString());
    }

    /// <summary>Only <c>requiredWhen.fact</c> reads a fact, and most steps have none: then no query is spent.</summary>
    private async Task<ProcessCaseFacts?> FactsWhenAskedAsync(ProcessStepTarget target, CancellationToken cancellationToken)
    {
        if (target.Definition?.Manual?.Inputs.Any(input => input.RequiredWhen?.Fact is not null) != true)
        {
            return null;
        }

        var processCase = target.Case;
        var bindings = await _bindings.ListAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = await _facts
            .LoadAsync(
                new ProcessFactQuery
                {
                    VacancyRequestIds = OneOf(processCase.VacancyRequestId),
                    Candidates = ProcessCaseFacts.CandidateSubject(processCase) is { } candidate ? [candidate] : [],
                    EmploymentIds = OneOf(processCase.EmploymentId),
                    ProbationIds = OneOf(processCase.ProbationId),
                    EmployeeIds = OneOf(processCase.EmployeeId),
                    StageBindings = bindings.ToDictionary(row => row.BindingCode, row => row.PipelineStageId, StringComparer.Ordinal),
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessCaseFacts(processCase, target.CaseDefinition!, snapshot);
    }

    private static Guid[] OneOf(Guid? id) => id is { } value ? [value] : [];
}

/// <summary>«Kerak emas» (§10 #11).</summary>
/// <remarks>
/// §7.6 calls the legacy skip «one click» and §10 #11 asks for a reason: the contract wins, one click plus a sentence.
/// A step skippable in any case needs no reason, though one given is kept.
/// </remarks>
public sealed class SkipProcessStepCommandHandler
{
    private readonly ProcessStepWriter _writer;

    public SkipProcessStepCommandHandler(ProcessStepWriter writer) => _writer = writer;

    public Task<Result<ProcessCaseDetailDto>> HandleAsync(SkipProcessStepCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _writer.ActAsync(
            command.CaseId,
            command.StepCode,
            command.Version,
            (target, _) => Task.FromResult(Skip(target, command.Reason)),
            cancellationToken);
    }

    private static Result Skip(ProcessStepTarget target, string? rawReason)
    {
        if (target.Step.IsClosed)
        {
            return ProcessErrors.InvalidStepState;
        }

        if (!ProcessStepAbility.CanSkip(target.Definition, target.StepRow, target.CaseRow, target.Viewer))
        {
            return ProcessStepAbility.MayAct(target.StepRow, target.Viewer) ? ProcessErrors.InvalidStepState : ProcessErrors.Forbidden;
        }

        var reason = rawReason?.Trim();
        var legacySkip = !target.Definition!.Skippable;

        if ((legacySkip && string.IsNullOrEmpty(reason))
            || reason is { Length: > ProcessCase.MaxCancelReasonLength }
            || ProcessFreeText.HasNul(reason))
        {
            return ProcessErrors.Invalid;
        }

        var payload = string.IsNullOrEmpty(reason) ? null : new JsonObject { ["reason"] = reason }.ToJsonString();

        return target.Case.SkipStepByUser(target.Step.StepCode, target.UserId, legacySkip, payload, target.At);
    }
}

/// <summary>Undo (§10 #12): the actor's own within 24 hours, or a manager's. The next pass recomputes the reopened step.</summary>
public sealed class UndoProcessStepCommandHandler
{
    private readonly ProcessStepWriter _writer;

    public UndoProcessStepCommandHandler(ProcessStepWriter writer) => _writer = writer;

    public Task<Result<ProcessCaseDetailDto>> HandleAsync(UndoProcessStepCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _writer.ActAsync(
            command.CaseId,
            command.StepCode,
            command.Version,
            (target, _) => Task.FromResult(Undo(target)),
            cancellationToken);
    }

    private static Result Undo(ProcessStepTarget target)
    {
        if (!target.Step.IsUserClosed)
        {
            return ProcessErrors.InvalidStepState;
        }

        if (!ProcessStepAbility.CanUndo(target.StepRow, target.CaseRow, target.Viewer, target.At))
        {
            return ProcessErrors.Forbidden;
        }

        return target.Case.UndoStep(target.Step.StepCode, target.UserId, target.Viewer.CanManage, target.At);
    }
}
