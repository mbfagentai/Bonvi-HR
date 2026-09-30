using Bonvi.Application.Attendance.Recompute;

namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>
/// Davomat fon vazifalarining jurnali — port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega port, <c>ILogger</c> emas.</b> Application qatlami loglash
/// kutubxonasiga bog'lanmagan (<c>IAttendanceIngestJournal</c> bilan bir xil
/// sabab). Fon vazifasida esa jurnal — yagona guvoh: tungi tozalash nima
/// o'chirganini hech qanday ekran ko'rsatmaydi.
/// </para>
/// <para>
/// ⚠️ Metodlarga faqat SONLAR, sanalar va muddatlar beriladi — shaxs raqami,
/// xodim nomi, fayl kaliti yoki qurilma tokeni HECH QACHON.
/// </para>
/// </remarks>
public interface IAttendanceJobJournal
{
    /// <summary>Iflos navbat yurishi tugadi.</summary>
    /// <param name="report">Sanoqlar.</param>
    void DirtyRecomputeFinished(DirtyRecomputeReport report);

    /// <summary>Tungi to'liq qayta hisoblash tugadi.</summary>
    /// <param name="from">Davr boshi.</param>
    /// <param name="to">Davr oxiri.</param>
    /// <param name="outcome">Sanoqlar.</param>
    void NightlyRecomputeFinished(DateOnly from, DateOnly to, AttendanceRecomputeOutcome outcome);

    /// <summary>Boshlangan smenalar vazifasi tugadi — yo'q «Kelmadi» qatorlari yozildi.</summary>
    /// <param name="from">Davr boshi.</param>
    /// <param name="to">Davr oxiri.</param>
    /// <param name="outcome">Sanoqlar (<c>StoredDays</c> — qo'shilgan qatorlar).</param>
    void StartedShiftsFilled(DateOnly from, DateOnly to, AttendanceRecomputeOutcome outcome);

    /// <summary>Mehnat o'zgarishlari vazifasi tugadi.</summary>
    /// <param name="report">Sanoqlar.</param>
    void EmploymentChangesRecomputed(EmploymentChangesReport report);

    /// <summary>Surat tozalash tugadi.</summary>
    /// <param name="report">Sanoqlar.</param>
    void PhotoPurgeFinished(PhotoPurgeReport report);

    /// <summary>O'tish tozalash o'tkazib yuborildi — muddat «hech qachon» (<c>0</c>).</summary>
    void PassPurgeDisabled();

    /// <summary>
    /// Muddati o'tgan, xodimga bog'langan, lekin hali hisobga olinmagani uchun O'CHIRILMAGAN
    /// o'tishlar bor — qayta hisoblash navbati buzilgan.
    /// </summary>
    /// <param name="localDateBefore">Muddat chegarasi.</param>
    /// <param name="count">Qoldirilganlar soni.</param>
    void ExpiredPendingPassesKept(DateOnly localDateBefore, int count);

    /// <summary>O'tish tozalash tugadi.</summary>
    /// <param name="report">Sanoqlar.</param>
    void PassPurgeFinished(PassPurgeReport report);
}

/// <summary>Iflos navbat yurishining sanoqlari.</summary>
/// <param name="LateAttachedIdentities">Kechikib bog'langan shaxs raqamlari (moslik bor edi, o'tish bog'lanmagan).</param>
/// <param name="PendingDays">Navbatdan olingan (xodim, sana) juftliklari.</param>
/// <param name="StoredDays">Yozilgan kun yozuvlari.</param>
/// <param name="RemovedDays">O'chirilgan eskirgan kun yozuvlari.</param>
/// <param name="FrozenDates">Muzlagani uchun hisoblanmagan sanalar.</param>
/// <param name="MarkedPasses">«Hisobga olindi» deb belgilangan o'tishlar.</param>
public sealed record DirtyRecomputeReport(
    int LateAttachedIdentities,
    int PendingDays,
    int StoredDays,
    int RemovedDays,
    int FrozenDates,
    int MarkedPasses);

/// <summary>Mehnat o'zgarishlari vazifasi yurishining sanoqlari.</summary>
/// <param name="ChangedEmployees">Munosabati yoki mehnat yo'li o'zgargan xodimlar.</param>
/// <param name="RecomputedEmployees">Kunlari qayta hisoblangan xodimlar (faqat kelajakka ta'sir qilganlari chiqarilgan).</param>
/// <param name="StoredDays">Yozilgan kun yozuvlari.</param>
/// <param name="RemovedDays">O'chirilgan eskirgan kun yozuvlari.</param>
/// <param name="FrozenDates">Muzlagani uchun hisoblanmagan sanalar.</param>
public sealed record EmploymentChangesReport(
    int ChangedEmployees,
    int RecomputedEmployees,
    int StoredDays,
    int RemovedDays,
    int FrozenDates);

/// <summary>Surat tozalash yurishining sanoqlari.</summary>
/// <param name="RetentionDays">Ishlatilgan muddat.</param>
/// <param name="ReceivedBefore">Shu lahzadan oldin qabul qilingan suratlar tozalandi.</param>
/// <param name="PurgedPhotos">Tozalangan suratlar.</param>
/// <param name="FailedFiles">O'chirib bo'lmagan fayllar — ularning qatori tegilmadi.</param>
/// <param name="Completed"><c>false</c> — vaqt chegarasi tugadi yoki fayl xatosi to'xtatdi, qolgani ertaga.</param>
public sealed record PhotoPurgeReport(
    int RetentionDays,
    DateTimeOffset ReceivedBefore,
    int PurgedPhotos,
    int FailedFiles,
    bool Completed);

/// <summary>O'tish tozalash yurishining sanoqlari.</summary>
/// <param name="RetentionDays">Ishlatilgan muddat — <c>max(pass, photo)</c>.</param>
/// <param name="LocalDateBefore">Shu sanadan oldingi o'tishlar o'chirildi.</param>
/// <param name="DeletedPasses">O'chirilgan qatorlar.</param>
/// <param name="DeletedFiles">Qator bilan birga o'chirilgan surat fayllari.</param>
/// <param name="FailedFiles">O'chirib bo'lmagan fayllar — ularning qatori ham qoldirildi.</param>
/// <param name="Completed"><c>false</c> — vaqt chegarasi tugadi yoki fayl xatosi to'xtatdi, qolgani ertaga.</param>
public sealed record PassPurgeReport(
    int RetentionDays,
    DateOnly LocalDateBefore,
    int DeletedPasses,
    int DeletedFiles,
    int FailedFiles,
    bool Completed);
