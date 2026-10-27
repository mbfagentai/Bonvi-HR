using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// Yo'qlik va jadval so'rovlari eksportining umumiy yorliqlari — ikki ro'yxat bitta navbatda
/// bir xil ko'rinadi (<c>ScheduleChangeRequestDto</c> izohi), fayllari ham.
/// </summary>
internal static class AttendanceRequestExportText
{
    /// <summary>Holat — ekrandagi belgi bilan bir xil (<c>attendance.requestStatus*</c>).</summary>
    public static string Status(AbsenceRequestStatus status) => status switch
    {
        AbsenceRequestStatus.Pending => "Kutilmoqda",
        AbsenceRequestStatus.Approved => "Tasdiqlangan",
        AbsenceRequestStatus.Rejected => "Rad etilgan",
        AbsenceRequestStatus.Cancelled => "Bekor qilingan",
        _ => status.ToString(),
    };

    /// <summary>
    /// «Qaror qilgan» — HR ning ismi; so'rovni xodimning O'ZI ilovadan qaytarib olgan bo'lsa —
    /// ekrandagi yozuv. Ikkalasi birga bo'lmaydi: xodim faqat KUTAYOTGAN so'rovni qaytaradi va
    /// HR qarori yozilmaydi (<c>AbsenceRequest.CancelBySubmitter</c>).
    /// </summary>
    public static string? DecidedBy(string? decidedByName, bool cancelledByEmployee)
        => decidedByName ?? (cancelledByEmployee ? "Xodim ilovadan qaytarib oldi" : null);
}
