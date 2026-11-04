using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Processes.Definitions;

/// <summary>
/// Process definitions (SPEC-mening-ishlarim slice P2a). Wired into <c>DependencyInjection.cs</c> at merge.
/// </summary>
/// <remarks>
/// Everything P2a builds is pure and static: <see cref="ProcessDefinitionValidator"/>,
/// <see cref="ProcessDueEvaluator"/>, the fact / anchor / role / screen / gate catalogues, and
/// <c>Calendar.WorkingDayCalendar</c>, which the pass builds from the <c>process_holidays</c> rows. This is
/// the seam where P2b registers the holiday-backed calendar source and the template seeder.
/// </remarks>
public static class ProcessDefinitionsRegistration
{
    public static IServiceCollection AddProcessDefinitions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
