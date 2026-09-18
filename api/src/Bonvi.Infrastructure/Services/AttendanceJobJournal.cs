using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Recompute;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IAttendanceJobJournal"/> — <c>ILogger</c> orqali, EventId 5300+.
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure qatlamida: qabul jurnalidan farqli, bu yerda HTTP tushunchasi
/// yo'q — vazifa so'rovsiz, fon oqimida yuradi.
/// </para>
/// <para>
/// ⚠️ Yozuvga faqat sonlar va sanalar tushadi (port izohi). Ogohlantirish
/// darajasidagi yozuvlar xato jurnali fayliga ham tushadi — shu sababli ular
/// ataylab kam: faqat «buzilgan navbat» va «o'chmagan fayl».
/// </para>
/// </remarks>
internal sealed partial class AttendanceJobJournal : IAttendanceJobJournal
{
    private readonly ILogger<AttendanceJobJournal> _logger;

    public AttendanceJobJournal(ILogger<AttendanceJobJournal> logger) => _logger = logger;

    public void DirtyRecomputeFinished(DirtyRecomputeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        // Har 15 daqiqada — bo'sh yurish konsolni to'ldirmasin.
        if (report is { PendingDays: 0, LateAttachedIdentities: 0 })
        {
            LogDirtyQueueEmpty();
            return;
        }

        LogDirtyRecomputeFinished(
            report.PendingDays,
            report.StoredDays,
            report.RemovedDays,
            report.FrozenDates,
            report.MarkedPasses,
            report.LateAttachedIdentities);
    }

    public void NightlyRecomputeFinished(DateOnly from, DateOnly to, AttendanceRecomputeOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        LogNightlyRecomputeFinished(
            from,
            to,
            outcome.EmployeeCount,
            outcome.StoredDays,
            outcome.RemovedDays,
            outcome.FrozenDates);
    }

    public void PhotoPurgeFinished(PhotoPurgeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        LogPhotoPurgeFinished(report.RetentionDays, report.ReceivedBefore, report.PurgedPhotos, report.Completed);

        if (report.FailedFiles > 0)
        {
            LogFilesNotDeleted(report.FailedFiles);
        }
    }

    public void PassPurgeDisabled() => LogPassPurgeDisabled();

    // Bog'langan-u kutayotgan eski o'tish — navbat buzilgan: ogohlantirish.
    public void ExpiredPendingPassesKept(DateOnly localDateBefore, int count)
        => LogPendingPassesKept(count, localDateBefore);

    public void PassPurgeFinished(PassPurgeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        LogPassPurgeFinished(
            report.RetentionDays,
            report.LocalDateBefore,
            report.DeletedPasses,
            report.DeletedFiles,
            report.Completed);

        if (report.FailedFiles > 0)
        {
            LogFilesNotDeleted(report.FailedFiles);
        }
    }

    [LoggerMessage(
        EventId = 5300,
        Level = LogLevel.Information,
        Message = "Davomat iflos navbati: {Juftliklar} (xodim, sana), {Yozildi} kun yozildi, {Ochirildi} o'chirildi, {Muzlagan} muzlagan sana, {Belgilandi} o'tish belgilandi, {KechikibBoglandi} raqam kechikib bog'landi")]
    private partial void LogDirtyRecomputeFinished(int juftliklar, int yozildi, int ochirildi, int muzlagan, int belgilandi, int kechikibBoglandi);

    [LoggerMessage(
        EventId = 5308,
        Level = LogLevel.Debug,
        Message = "Davomat iflos navbati bo'sh")]
    private partial void LogDirtyQueueEmpty();

    [LoggerMessage(
        EventId = 5301,
        Level = LogLevel.Information,
        Message = "Davomat tungi qayta hisoblash {Dan}…{Gacha}: {Xodimlar} xodim, {Yozildi} kun yozildi, {Ochirildi} o'chirildi, {Muzlagan} muzlagan sana")]
    private partial void LogNightlyRecomputeFinished(DateOnly dan, DateOnly gacha, int xodimlar, int yozildi, int ochirildi, int muzlagan);

    [LoggerMessage(
        EventId = 5302,
        Level = LogLevel.Information,
        Message = "Turniket suratlari tozalandi: muddat {Kun} kun, {Chegara} dan oldingi {Soni} ta surat, to'liq tugadi: {Tugadi}")]
    private partial void LogPhotoPurgeFinished(int kun, DateTimeOffset chegara, int soni, bool tugadi);

    [LoggerMessage(
        EventId = 5303,
        Level = LogLevel.Information,
        Message = "O'tishlarni tozalash o'tkazib yuborildi: saqlash muddati 0 — hech qachon o'chirilmaydi")]
    private partial void LogPassPurgeDisabled();

    [LoggerMessage(
        EventId = 5304,
        Level = LogLevel.Warning,
        Message = "{Soni} ta bog'langan o'tish {Chegara} dan eski, lekin hali qayta hisoblanmagan — O'CHIRILMADI. Iflos navbat ishlayaptimi?")]
    private partial void LogPendingPassesKept(int soni, DateOnly chegara);

    [LoggerMessage(
        EventId = 5306,
        Level = LogLevel.Information,
        Message = "Davomat o'tishlari tozalandi: muddat {Kun} kun, {Chegara} dan oldingi {Otishlar} ta o'tish va {Fayllar} ta surat, to'liq tugadi: {Tugadi}")]
    private partial void LogPassPurgeFinished(int kun, DateOnly chegara, int otishlar, int fayllar, bool tugadi);

    [LoggerMessage(
        EventId = 5307,
        Level = LogLevel.Warning,
        Message = "{Soni} ta turniket surati fayli o'chirilmadi (ruxsat yoki disk xatosi) — ularning qatori tegilmadi, ertaga qayta urinadi")]
    private partial void LogFilesNotDeleted(int soni);
}
