using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;
using System.Globalization;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Inputs;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Processes.Queries;

/// <summary>§10 #6: everything stuck, for everyone with <c>processes.view</c>; plus the overrides of the last 30 days.</summary>
public sealed class GetProcessLateQueryHandler
{
    private const int OverrideLogDays = 30;

    private readonly ProcessInlineReconcile _inline;
    private readonly IProcessReadModel _readModel;
    private readonly ProcessViewerLoader _viewers;
    private readonly ProcessReadSetLoader _sets;
    private readonly IProcessGateOverrideRepository _overrides;
    private readonly IClock _clock;

    public GetProcessLateQueryHandler(
        ProcessInlineReconcile inline,
        IProcessReadModel readModel,
        ProcessViewerLoader viewers,
        ProcessReadSetLoader sets,
        IProcessGateOverrideRepository overrides,
        IClock clock)
    {
        _inline = inline;
        _readModel = readModel;
        _viewers = viewers;
        _sets = sets;
        _overrides = overrides;
        _clock = clock;
    }

    public async Task<Result<ProcessLateDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        await _inline.BeforeReadAsync(cancellationToken).ConfigureAwait(false);

        var today = _clock.Today;
        var (_, directory) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);

        var versions = await _readModel.ListActiveVersionIdsAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await _sets.DefinitionsAsync(versions, cancellationToken).ConfigureAwait(false);

        // AKT fires before the step is late, so those steps have to be read even when on time.
        var actSteps = definitions.Values
            .SelectMany(definition => definition.Steps)
            .Where(step => step.Gates.Any(gate => gate is { Code: ProcessGateCodes.Akt, Mode: ProcessGateMode.Warn }))
            .Select(step => step.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var steps = await _readModel.ListProblemStepsAsync(today, actSteps, cancellationToken).ConfigureAwait(false);
        var cases = await _readModel
            .ListCasesAsync([.. steps.Select(step => step.CaseId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        var overrides = await _overrides
            .ListGrantedSinceAsync(_clock.UtcNow.AddDays(-OverrideLogDays), cancellationToken)
            .ConfigureAwait(false);
        var overrideUsers = overrides
            .SelectMany(row => new[] { row.GrantedBy, row.ForUserId ?? Guid.Empty })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var set = await _sets.LoadAsync(cases, steps, directory, overrideUsers, cancellationToken).ConfigureAwait(false);

        // Most days late first: that is the order «Kim kechikdi» is read in.
        var worstFirst = steps
            .OrderBy(step => step.DueDate ?? DateOnly.MaxValue)
            .ThenBy(step => step.CaseId)
            .ThenBy(step => step.OrderNo)
            .ToList();

        return new ProcessLateDto(
            [.. worstFirst.Where(step => step.State == ProcessStepState.Blocked).Select(set.Task)],
            [.. worstFirst.Where(step => step.State == ProcessStepState.Active && set.DaysLate(step.DueDate) > 0).Select(set.Task)],
            [.. worstFirst.Where(step => set.WarningsOf(step).Count > 0).Select(set.Task)],
            [
                .. overrides.Select(row => new ProcessGateOverrideRowDto(
                    row.Id,
                    row.GateCode,
                    row.CaseId,
                    row.StepCode,
                    row.EmploymentId,
                    row.CandidateId,
                    row.ForUserId,
                    row.ForUserId is { } forUser ? set.Names.Users.GetValueOrDefault(forUser) : null,
                    row.Reason,
                    row.GrantedBy,
                    set.Names.Users.GetValueOrDefault(row.GrantedBy),
                    row.GrantedAt,
                    row.ExpiresAt,
                    row.ConsumedAt,
                    row.ConsumedOperation)),
            ]);
    }
}

/// <summary>§10 #7: the published definitions, as far as the screens need them (no completion rules).</summary>
public sealed class ListProcessTemplatesQueryHandler
{
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly ProcessReadSetLoader _sets;

    public ListProcessTemplatesQueryHandler(IProcessTemplateVersionRepository versions, ProcessReadSetLoader sets)
    {
        _versions = versions;
        _sets = sets;
    }

    public async Task<Result<IReadOnlyList<ProcessTemplateDto>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var published = await _versions.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await _sets.DefinitionsAsync(published.Select(row => row.Id), cancellationToken).ConfigureAwait(false);

        IReadOnlyList<ProcessTemplateDto> items =
        [
            .. definitions.Values
                .OrderBy(definition => definition.Code, StringComparer.Ordinal)
                .Select(ProcessDefinitionDtos.Template),
        ];

        return Result.Success(items);
    }
}

/// <summary>§10 #8. Reads the settings only: the question is about the engine, so it must not run the engine.</summary>
public sealed class GetProcessStatusQueryHandler
{
    private readonly SystemSettingsReader _settings;

    public GetProcessStatusQueryHandler(SystemSettingsReader settings) => _settings = settings;

    public async Task<Result<ProcessStatusDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var engine = ProcessEngineSettings.From(settings);

        DateTimeOffset? lastPass = DateTimeOffset.TryParse(
            settings.Hidden(SystemSettingKeys.ProcessesReconcileWatermark),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var at)
            ? at
            : null;

        return new ProcessStatusDto(
            engine.IsDormant ? "dormant" : "active",
            engine.StartedOn,
            engine.GatesMode,
            settings.Number(SystemSettingKeys.ProcessesCouncilWeekday),
            lastPass);
    }
}

/// <summary>Definition records → response records; the definition keeps <c>ProcessScalar</c> and rules to itself.</summary>
public static class ProcessDefinitionDtos
{
    public static ProcessTemplateDto Template(ProcessDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new ProcessTemplateDto(
            definition.Code,
            definition.Version,
            definition.Title,
            definition.Source,
            Enum.Parse<ProcessSubjectKind>(definition.SubjectKind, ignoreCase: true),
            [.. definition.Board.Select(column => new ProcessColumnDto(column.Key, column.Title))],
            [.. definition.Steps.Select(Step)]);
    }

    /// <param name="manual">The step's <c>manual</c>.</param>
    /// <param name="autoItems">A case's open step: its data-backed items. <c>null</c> on a template, which has no subject.</param>
    public static ProcessManualDto? Manual(ProcessManual? manual, ProcessStepAutoItems? autoItems = null)
        => manual is null ? null : new ProcessManualDto(manual.Allowed, [.. manual.Inputs.Select(input => Input(input, autoItems))]);

    private static ProcessTemplateStepDto Step(ProcessStep step)
        => new(
            step.Id,
            step.Title,
            step.Column,
            step.Role,
            step.FallbackRole,
            step.Blank,
            step.Signature,
            step.Kind,
            step.Prereqs,
            step.Optional,
            step.Skippable,
            step.LegacySkippable,
            [.. step.Gates.Select(gate => new ProcessTemplateGateDto(gate.Code, gate.Mode))],
            Manual(step.Manual),
            step.Guide,
            step.SourceRef,
            ProcessReadSet.LinkOf(step));

    private static ProcessManualInputDto Input(ProcessManualInput input, ProcessStepAutoItems? autoItems)
        => new(
            input.Id,
            input.Label,
            input.Type,
            input.Required,
            input.RequiredWhen is { } when ? new ProcessRequiredWhenDto(when.Input, when.Fact, when.EqualsValue.ToString()) : null,
            [.. input.Options.Select(option => new ProcessOptionDto(option.Value, option.Label))],
            input.Min,
            input.Max,
            autoItems?.ByInput.GetValueOrDefault(input.Id)?.Select(AutoItem).ToList());

    private static ProcessAutoItemDto AutoItem(ProcessAutoItem item)
        => new(item.Value, item.State, item.Mark, item.Source, item.SourceDetail, item.Reason, item.Fill);
}
