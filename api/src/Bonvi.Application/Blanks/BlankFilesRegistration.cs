using Bonvi.Application.Blanks.Files;
using Bonvi.Application.Blanks.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Blanks;

/// <summary>Blank attachments and PDF (SPEC-blankalar §7 #17–#20, §8, §10).</summary>
public static class BlankFilesRegistration
{
    public static IServiceCollection AddBlankFiles(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Attachments (#17–#19)
        services.AddScoped<BlankFileAccess>();
        services.AddScoped<UploadBlankAttachmentCommandHandler>();
        services.AddScoped<RemoveBlankAttachmentCommandHandler>();
        services.AddScoped<GetBlankAttachmentFileQueryHandler>();

        // PDF (#20) and the final snapshot written at Approved — called by the steps slice.
        services.AddScoped<BlankPdfModelBuilder>();
        services.AddScoped<GetBlankPdfQueryHandler>();
        services.AddScoped<BlankFinalPdfService>();

        return services;
    }
}
