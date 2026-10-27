using Bonvi.Application.Recruiting.Forms;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary><see cref="IQuestionnaireFormJournal"/> — Serilog orqali (faqat raqam va kod).</summary>
internal sealed partial class QuestionnaireFormJournal : IQuestionnaireFormJournal
{
    private readonly ILogger<QuestionnaireFormJournal> _logger;

    public QuestionnaireFormJournal(ILogger<QuestionnaireFormJournal> logger) => _logger = logger;

    public void PdfNotAttached(int formNumber, string reasonCode) => LogPdfNotAttached(formNumber, reasonCode);

    [LoggerMessage(
        EventId = 4701,
        Level = LogLevel.Warning,
        Message = "Anketa №{FormNumber} saqlandi, lekin PDF nomzod fayllariga biriktirilmadi: {ReasonCode} — HR uni «Onlayn anketa» kartasidan oladi")]
    private partial void LogPdfNotAttached(int formNumber, string reasonCode);
}
