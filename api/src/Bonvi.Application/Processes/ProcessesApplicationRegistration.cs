using Bonvi.Application.Processes.Commands;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Gates;
using Bonvi.Application.Processes.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Processes;

/// <summary>«Mening ishlarim» — the process engine (docs/SPEC-mening-ishlarim.md).</summary>
/// <remarks>
/// The module keeps its own registrations here — the shared <c>DependencyInjection.cs</c> holds one
/// call line. Later slices (definitions, reconciler, gates, queries) add their lines HERE.
/// </remarks>
public static class ProcessesApplicationRegistration
{
    public static IServiceCollection AddProcessesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProcessDefinitions();

        services.AddScoped<ProcessEnginePassLoader>();
        services.AddScoped<ProcessStarters>();
        services.AddScoped<ProcessCaseBatch>();
        services.AddScoped<ProcessReconciler>();
        services.AddScoped<ProcessReconcileJob>();

        // A singleton: it holds the throttle, and opens a scope of its own for each pass.
        services.AddSingleton<ProcessInlineReconcile>();

        services.AddScoped<IProcessGate, ProcessGate>();
        services.AddScoped<GrantCaseGateOverrideCommandHandler>();
        services.AddScoped<GrantDirectHireOverrideCommandHandler>();
        services.AddScoped<ListGateOverridesQueryHandler>();
        // Reads (§10 #1–#8). A version's definition never changes, so its parsed form is kept for the process's life.
        services.AddSingleton<ProcessDefinitionCache>();
        services.AddScoped<ProcessViewerLoader>();
        services.AddScoped<ProcessReadSetLoader>();
        services.AddScoped<Inputs.ProcessInputAutoLoader>();
        services.AddScoped<ProcessTartibReader>();
        services.AddScoped<GetMyWorkQueryHandler>();
        services.AddScoped<GetMyProcessCountQueryHandler>();
        services.AddScoped<ListProcessCasesQueryHandler>();
        services.AddScoped<GetProcessBoardQueryHandler>();
        services.AddScoped<GetProcessCaseQueryHandler>();
        services.AddScoped<GetProcessLateQueryHandler>();
        services.AddScoped<ListProcessTemplatesQueryHandler>();
        services.AddScoped<GetProcessStatusQueryHandler>();

        // Writes (§10 #9–#13, #16–#18).
        services.AddScoped<ProcessStepWriter>();
        services.AddScoped<StartProcessCaseCommandHandler>();
        services.AddScoped<CompleteProcessStepCommandHandler>();
        services.AddScoped<SkipProcessStepCommandHandler>();
        services.AddScoped<UndoProcessStepCommandHandler>();
        services.AddScoped<CancelProcessCaseCommandHandler>();
        services.AddScoped<ReopenProcessCaseCommandHandler>();
        services.AddScoped<RunProcessReconcileCommandHandler>();
        services.AddScoped<ListProcessStageBindingsQueryHandler>();
        services.AddScoped<SetProcessStageBindingsCommandHandler>();
        services.AddScoped<ListProcessHolidaysQueryHandler>();
        services.AddScoped<SaveProcessHolidayCommandHandler>();
        services.AddScoped<DeleteProcessHolidayCommandHandler>();

        return services;
    }
}
