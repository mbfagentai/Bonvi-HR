using Bonvi.Application.Processes.Facts;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Infrastructure.Processes.Facts;

/// <summary>
/// Registers the process fact catalogue (docs/SPEC-mening-ishlarim.md §7.2). Called from
/// <c>AddProcessesInfrastructure()</c> (hook H9) once the processes module is wired.
/// </summary>
public static class ProcessFactsRegistration
{
    public static IServiceCollection AddProcessFacts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped: shares the request's / job's AppDbContext.
        services.AddScoped<IProcessFactSource, EfProcessFactSource>();

        return services;
    }
}
