using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Safety;

/// <summary>Baxtsiz hodisalar jurnali (M11) use-case'larini ro'yxatga oladi.</summary>
internal static class SafetyRegistration
{
    public static IServiceCollection AddSafety(this IServiceCollection services)
    {
        services.AddScoped<IncidentMapper>();

        services.AddScoped<RecordIncidentCommandHandler>();
        services.AddScoped<UpdateIncidentCommandHandler>();
        services.AddScoped<ConcludeIncidentCommandHandler>();
        services.AddScoped<DeleteIncidentCommandHandler>();

        services.AddScoped<ListIncidentsQueryHandler>();
        services.AddScoped<GetIncidentQueryHandler>();

        return services;
    }
}
