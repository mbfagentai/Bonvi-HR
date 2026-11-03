using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Blanks;

/// <summary>Qadam amallari (tasdiqlash, planshetda imzo, qaytarish, rad etish) — S6 to'ldiradi.</summary>
public static class BlankStepsRegistration
{
    public static IServiceCollection AddBlankSteps(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
