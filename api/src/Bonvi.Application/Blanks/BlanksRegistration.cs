using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Forms.Texts;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Blanks;

/// <summary>HRMentor blankalari va anketa matnlari (SPEC-blankalar-hrmentor).</summary>
internal static class BlanksRegistration
{
    public static IServiceCollection AddBlanks(this IServiceCollection services)
    {
        services.AddScoped<HrMentorSync>();
        services.AddScoped<HrMentorSyncJob>();
        services.AddScoped<FormTextsReader>();
        services.AddScoped<FormDefinitionsReader>();

        services.AddScoped<GetBlanksQueryHandler>();
        services.AddScoped<GetBlankFileQueryHandler>();
        services.AddScoped<SyncBlanksCommandHandler>();

        return services;
    }
}
