using Bonvi.Application.Blanks.Files;
using Bonvi.Application.Blanks.Pdf;
using Bonvi.Domain.Abstractions;
using Bonvi.Infrastructure.Blanks.Pdf;
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
    /// <summary>Repozitoriylar, raqam hisoblagichi va fayl tekshiruvchi.</summary>
    public static IServiceCollection AddBlanksInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBlankTemplateRepository, BlankTemplateRepository>();
        services.AddScoped<IBlankTemplateVersionRepository, BlankTemplateVersionRepository>();
        services.AddScoped<IBlankSubmissionRepository, BlankSubmissionRepository>();
        services.AddScoped<IBlankRoleAssignmentRepository, BlankRoleAssignmentRepository>();
        services.AddScoped<IBlankNumberCounter, BlankNumberCounterStore>();

        // Fayl turini tarkibidan aniqlaydi (SPEC §8) — holatsiz.
        services.AddSingleton<IBlankFileInspector, BlankFileInspector>();

        // Generic blank PDF (SPEC §10) — stateless; fonts come from the shared bundled resolver.
        services.AddSingleton<IBlankPdfRenderer, BlankPdfRenderer>();

        return services;
    }
}
