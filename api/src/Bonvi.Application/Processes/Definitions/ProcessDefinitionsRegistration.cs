using Bonvi.Application.Processes.Calendar;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Processes.Definitions;

/// <summary>Process definitions and the working-day calendar (SPEC-mening-ishlarim slices P2a, P2b).</summary>
/// <remarks>
/// <see cref="ProcessDefinitionValidator"/>, <see cref="ProcessDueEvaluator"/>, the fact / anchor / role / screen /
/// gate catalogues and <c>Calendar.WorkingDayCalendar</c> are pure and static, so only the calendar source needs a
/// line here. The template seeder works on the database and is registered in Infrastructure.
/// </remarks>
public static class ProcessDefinitionsRegistration
{
    public static IServiceCollection AddProcessDefinitions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IWorkingDayCalendarSource, HolidayWorkingDayCalendarSource>();

        return services;
    }
}
