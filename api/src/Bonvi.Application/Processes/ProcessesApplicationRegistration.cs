using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
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

        return services;
    }
}
