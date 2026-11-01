using Bonvi.Application.Blanks;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// HRMentor sinxronlash jurnali (SPEC-blankalar-hrmentor §3). Faqat kodlar va sonlar — kalit,
/// manzil yoki javob tanasi hech qachon yozilmaydi.
/// </summary>
internal sealed partial class HrMentorSyncJournal : IHrMentorSyncJournal
{
    private readonly ILogger<HrMentorSyncJournal> _logger;

    public HrMentorSyncJournal(ILogger<HrMentorSyncJournal> logger) => _logger = logger;

    public void StepFailed(HrMentorSyncStep step, string errorCode) => LogStepFailed(step, errorCode);

    public void BlankSkipped(Guid documentId, string errorCode) => LogBlankSkipped(documentId, errorCode);

    public void Finished(HrMentorSyncReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        LogFinished(
            report.CatalogPushed,
            report.TextsRevision,
            report.BlanksAdded,
            report.BlanksUpdated,
            report.BlanksRemoved,
            report.ErrorCode ?? "-");

        if (report.ProfilePushed || report.Definition is not null)
        {
            LogForm(
                report.ProfilePushed,
                report.Definition is { Version: { } version } ? version.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : report.Definition is not null ? "ichki" : "-");
        }
    }

    [LoggerMessage(
        EventId = 4800,
        Level = LogLevel.Warning,
        Message = "HRMentor: «{Step}» qadami bajarilmadi ({ErrorCode}) — oxirgi nusxa qoldi, qolgan qadamlar davom etdi")]
    private partial void LogStepFailed(HrMentorSyncStep step, string errorCode);

    [LoggerMessage(
        EventId = 4801,
        Level = LogLevel.Warning,
        Message = "HRMentor: blanka {DocumentId} olinmadi ({ErrorCode}) — eski nusxasi qoldi")]
    private partial void LogBlankSkipped(Guid documentId, string errorCode);

    [LoggerMessage(
        EventId = 4802,
        Level = LogLevel.Information,
        Message = "HRMentor: katalog yuborildi={CatalogPushed}, yangi matnlar reviziyasi={TextsRevision}, blankalar +{Added} ~{Updated} -{Removed}, xato={ErrorCode}")]
    private partial void LogFinished(bool catalogPushed, int? textsRevision, int added, int updated, int removed, string errorCode);

    [LoggerMessage(
        EventId = 4803,
        Level = LogLevel.Information,
        Message = "HRMentor: profil yuborildi={ProfilePushed}, amaldagi anketa={Definition}")]
    private partial void LogForm(bool profilePushed, string definition);
}
