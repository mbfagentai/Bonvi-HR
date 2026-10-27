using Bonvi.Application.Me;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="ITeamViewJournal"/> — <c>ILogger</c> orqali, EventId 5420.
/// </summary>
/// <remarks>
/// Xato darajasida: ko'rish izi tushmay qolgani — audit jurnalidagi teshik, va uni kimdir
/// ko'rishi kerak (xato jurnali fayliga ham tushadi). Javob esa buzilmagan
/// (<c>TeamViewAudit</c> izohi). Faqat amal nomi va istisno — kim kimni ko'rgani yo'q.
/// </remarks>
internal sealed partial class TeamViewJournal : ITeamViewJournal
{
    private readonly ILogger<TeamViewJournal> _logger;

    public TeamViewJournal(ILogger<TeamViewJournal> logger) => _logger = logger;

    public void AuditNotRecorded(string action, Exception error) => LogAuditNotRecorded(error, action);

    [LoggerMessage(
        EventId = 5420,
        Level = LogLevel.Error,
        Message = "«Jamoam»: ko'rish ({Amal}) audit jurnaliga yozilmadi — javob o'zgarmadi")]
    private partial void LogAuditNotRecorded(Exception error, string amal);
}
