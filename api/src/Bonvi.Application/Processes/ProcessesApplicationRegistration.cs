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

        return services;
    }
}
