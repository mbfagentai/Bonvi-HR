using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Persistence.ReadModels;
using Bonvi.Infrastructure.Processes.Engine;
using Bonvi.Infrastructure.Processes.Facts;
using Bonvi.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Infrastructure.Processes;

/// <summary>
/// Infrastructure services of «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §16).
/// </summary>
/// <remarks>
/// The module keeps its own registrations here — the shared <c>DependencyInjection.cs</c> holds one
/// call line. Later slices (template seeder, fact readers, reconciler job) add their lines HERE.
/// </remarks>
public static class ProcessesInfrastructureRegistration
{
    /// <summary>Repositories, seeders.</summary>
    public static IServiceCollection AddProcessesInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IProcessTemplateRepository, ProcessTemplateRepository>();
        services.AddScoped<IProcessTemplateVersionRepository, ProcessTemplateVersionRepository>();
        services.AddScoped<IProcessCaseRepository, ProcessCaseRepository>();
        services.AddScoped<IProcessGateOverrideRepository, ProcessGateOverrideRepository>();
        services.AddScoped<IProcessStageBindingRepository, ProcessStageBindingRepository>();
        services.AddScoped<IProcessHolidayRepository, ProcessHolidayRepository>();

        services.AddScoped<ProcessTemplateSeeder>();
        services.AddScoped<ProcessHolidaySeeder>();

        services.AddProcessFacts();

        // Scoped: all three share the pass's AppDbContext, and the lock must sit in the pass's transaction.
        services.AddScoped<IProcessEngineReader, EfProcessEngineReader>();
        services.AddScoped<IProcessEngineLock, ProcessEngineLock>();
        services.AddScoped<IProcessEngineJournal, ProcessEngineJournal>();

        services.AddScoped<IProcessReadModel, ProcessReadModel>();

        return services;
    }
}
