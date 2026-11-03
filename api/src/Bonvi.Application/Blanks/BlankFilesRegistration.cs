using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Blanks;

/// <summary>Fayllar va PDF — S7 to'ldiradi.</summary>
public static class BlankFilesRegistration
{
    public static IServiceCollection AddBlankFiles(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
