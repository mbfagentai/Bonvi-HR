using Bonvi.Application.Blanks.Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bonvi.Application.Blanks;

/// <summary>Qadam amallari (tasdiqlash, planshetda imzo, qaytarish, rad etish, imzo rasmi — §7 #12–#16).</summary>
public static class BlankStepsRegistration
{
    public static IServiceCollection AddBlankSteps(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<BlankStepActions>();
        services.AddScoped<ApproveBlankStepCommandHandler>();
        services.AddScoped<SignBlankStepInPersonCommandHandler>();
        services.AddScoped<ReturnBlankStepCommandHandler>();
        services.AddScoped<RejectBlankStepCommandHandler>();
        services.AddScoped<GetBlankStepSignatureQueryHandler>();

        // Tasdiqlanganda yakuniy PDF — S7 o'z amalga oshirishini ro'yxatdan o'tkazadi (TryAdd: uniki ustun).
        services.TryAddScoped<IBlankApprovedHook, NoBlankApprovedHook>();

        return services;
    }
}
