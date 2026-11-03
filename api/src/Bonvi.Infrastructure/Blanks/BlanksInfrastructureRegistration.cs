using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Infrastructure.Blanks;

/// <summary>
/// «Blankalar» modulining Infrastructure xizmatlari (docs/SPEC-blankalar.md §12).
/// </summary>
/// <remarks>
/// Modul o'z ro'yxatini shu faylda yuritadi — umumiy <c>DependencyInjection.cs</c> da faqat
/// bitta chaqiruv qatori. Keyingi bo'laklar (fayl tekshiruvchi, PDF, oldindan to'ldirish
/// manbai) o'z qatorini SHU YERGA qo'shadi.
/// </remarks>
public static class BlanksInfrastructureRegistration
{
    /// <summary>Repozitoriylar va raqam hisoblagichi.</summary>
    public static IServiceCollection AddBlanksInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBlankTemplateRepository, BlankTemplateRepository>();
        services.AddScoped<IBlankTemplateVersionRepository, BlankTemplateVersionRepository>();
        services.AddScoped<IBlankSubmissionRepository, BlankSubmissionRepository>();
        services.AddScoped<IBlankRoleAssignmentRepository, BlankRoleAssignmentRepository>();
        services.AddScoped<IBlankNumberCounter, BlankNumberCounterStore>();

        return services;
    }
}
