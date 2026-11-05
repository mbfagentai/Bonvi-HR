using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Processes.Engine;

/// <summary>What one pass reads once and keeps for its length, so a long pass sees one consistent state (§6.10).</summary>
/// <param name="StartedOn">The engine's start date: a pass exists only when it is set (§7.6).</param>
/// <param name="Settings">The engine settings.</param>
/// <param name="Calendar">The working-day calendar.</param>
/// <param name="StageBindings">§6.5 <c>binding_code → pipeline_stage_id</c>.</param>
/// <param name="Definitions">The templates.</param>
/// <param name="At">The pass time: everything the pass writes carries this one instant.</param>
public sealed record ProcessEnginePass(
    DateOnly StartedOn,
    ProcessEngineSettings Settings,
    IWorkingDayCalendar Calendar,
    IReadOnlyDictionary<string, Guid?> StageBindings,
    ProcessDefinitionCatalog Definitions,
    DateTimeOffset At);

/// <summary>Builds a <see cref="ProcessEnginePass"/>; runs inside the pass's transaction, a retry reads everything again.</summary>
public sealed class ProcessEnginePassLoader
{
    private readonly IWorkingDayCalendarSource _calendar;
    private readonly IProcessStageBindingRepository _bindings;
    private readonly IProcessTemplateRepository _templates;
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly IProcessEngineJournal _journal;
    private readonly IClock _clock;

    public ProcessEnginePassLoader(
        IWorkingDayCalendarSource calendar,
        IProcessStageBindingRepository bindings,
        IProcessTemplateRepository templates,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        IClock clock)
    {
        _calendar = calendar;
        _bindings = bindings;
        _templates = templates;
        _versions = versions;
        _journal = journal;
        _clock = clock;
    }

    public async Task<ProcessEnginePass> LoadAsync(DateOnly startedOn, ProcessEngineSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var calendar = await _calendar.LoadAsync(cancellationToken).ConfigureAwait(false);
        var bindings = await _bindings.ListAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await ProcessDefinitionCatalog.LoadAsync(_templates, _versions, _journal, cancellationToken).ConfigureAwait(false);

        return new ProcessEnginePass(
            startedOn,
            settings,
            calendar,
            bindings.ToDictionary(row => row.BindingCode, row => row.PipelineStageId, StringComparer.Ordinal),
            definitions,
            _clock.UtcNow);
    }
}
