using Bonvi.Application.Blanks.Submissions;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Blanks;

/// <summary>«Blankalar» moduli (docs/SPEC-blankalar.md).</summary>
public static class BlanksRegistration
{
    public static IServiceCollection AddBlanks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Nashr qilingan ta'rif o'zgarmaydi (§3.2) — tahlil natijasi jarayon davomida keshlanadi.
        services.AddSingleton<BlankDefinitionCache>();
        services.AddScoped<BlankActorResolver>();
        services.AddScoped<BlankSubmissionDetails>();

        // Shablonlar va rollar (#1–#5)
        services.AddScoped<ListBlankTemplatesQueryHandler>();
        services.AddScoped<GetBlankTemplateVersionQueryHandler>();
        services.AddScoped<SetBlankTemplateActiveCommandHandler>();
        services.AddScoped<ListBlankRolesQueryHandler>();
        services.AddScoped<SetBlankRoleUsersCommandHandler>();

        // Blankalar (#6–#11)
        services.AddScoped<ListBlankSubmissionsQueryHandler>();
        services.AddScoped<GetBlankSubmissionQueryHandler>();
        services.AddScoped<CreateBlankSubmissionCommandHandler>();
        services.AddScoped<SaveBlankAnswersCommandHandler>();
        services.AddScoped<SubmitBlankSubmissionCommandHandler>();
        services.AddScoped<CancelBlankSubmissionCommandHandler>();

        services.AddBlankSteps();
        services.AddBlankFiles();

        return services;
    }
}
