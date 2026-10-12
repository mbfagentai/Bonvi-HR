using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Audit;

/// <summary>
/// Audit jurnalini O'QISH use-case'larini ro'yxatga oladi (A2).
/// </summary>
/// <remarks>
/// Yozish tomoni bu yerda YO'Q: entity o'zgarishlarini <c>AuditTrailInterceptor</c>,
/// hodisalarni <c>IAuditLogger</c> yozadi (ikkalasi Infrastructure'da).
/// </remarks>
internal static class AuditRegistration
{
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        services.AddScoped<ListAuditEntriesQueryHandler>();
        services.AddScoped<GetAuditEntryQueryHandler>();
        services.AddScoped<ListAuditActorsQueryHandler>();
        services.AddScoped<ListEmployeeHistoryQueryHandler>();

        return services;
    }
}
