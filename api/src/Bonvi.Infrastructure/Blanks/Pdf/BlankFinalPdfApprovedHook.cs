using Bonvi.Application.Blanks.Pdf;
using Bonvi.Application.Blanks.Steps;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Blanks.Pdf;

/// <summary>
/// Writes the final PDF snapshot when a blank becomes Approved (SPEC-blankalar §5, §10).
/// </summary>
/// <remarks>
/// Lives in Infrastructure only because the Application layer has no logging: a failed snapshot is
/// logged here and swallowed — the approval is already committed and on-demand rendering still works.
/// </remarks>
public sealed partial class BlankFinalPdfApprovedHook : IBlankApprovedHook
{
    private readonly BlankFinalPdfService _finalPdf;
    private readonly ILogger<BlankFinalPdfApprovedHook> _logger;

    public BlankFinalPdfApprovedHook(BlankFinalPdfService finalPdf, ILogger<BlankFinalPdfApprovedHook> logger)
    {
        _finalPdf = finalPdf;
        _logger = logger;
    }

    public async Task OnApprovedAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await _finalPdf.StoreFinalPdfAsync(submissionId, cancellationToken).ConfigureAwait(false);
            if (stored.IsFailure)
            {
                LogRefused(submissionId, stored.Error.Code);
            }
        }
#pragma warning disable CA1031 // The hook contract: never throw after a committed approval.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogFailed(exception, submissionId);
        }
    }

    [LoggerMessage(
        EventId = 2410,
        Level = LogLevel.Warning,
        Message = "Blanka {SubmissionId}: yakuniy PDF saqlanmadi ({Code})")]
    private partial void LogRefused(Guid submissionId, string code);

    [LoggerMessage(
        EventId = 2411,
        Level = LogLevel.Error,
        Message = "Blanka {SubmissionId}: yakuniy PDF chizish/saqlashda xato — tasdiq kuchda, PDF so'rov bo'yicha chiziladi")]
    private partial void LogFailed(Exception exception, Guid submissionId);
}
