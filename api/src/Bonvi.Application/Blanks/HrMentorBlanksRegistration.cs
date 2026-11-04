using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Application.Recruiting.Forms.Texts;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Blanks;

/// <summary>HRMentor blankalari va anketa matnlari (SPEC-blankalar-hrmentor).</summary>
internal static class HrMentorBlanksRegistration
{
    public static IServiceCollection AddHrMentorBlanks(this IServiceCollection services)
    {
        services.AddScoped<HrMentorSync>();
        services.AddScoped<HrMentorSyncJob>();
        services.AddScoped<HrMentorChangeWatch>();
        services.AddScoped<FormTextsReader>();
        services.AddScoped<FormDefinitionsReader>();

        services.AddScoped<GetHrMentorBlanksQueryHandler>();
        services.AddScoped<GetHrMentorBlankFileQueryHandler>();
        services.AddScoped<SyncHrMentorBlanksCommandHandler>();

        return services;
    }
}
