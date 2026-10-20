using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Dtos;

/// <summary>Jadval so'rovining bitta kuni.</summary>
/// <param name="Date">Sana.</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish.</param>
/// <param name="EndTime">Tugash (boshlanishdan oldin bo'lsa — tungi smena).</param>
/// <param name="BreakStart">Tanaffus boshi.</param>
/// <param name="BreakEnd">Tanaffus oxiri.</param>
/// <param name="SwapWith">Almashtirishda juftining sanasi.</param>
/// <param name="BreakMinutes">Ayiriladigan tanaffus (daqiqa) — oynadan farq qilishi mumkin.</param>
/// <param name="IsFlexible">«Erkin ish jadvali» (SPEC-erkin-jadval §5.2).</param>
/// <param name="RequiredMinutes">«Ish vaqti» (daqiqa) — <paramref name="IsFlexible"/> bo'lsagina bor.</param>
public sealed record ScheduleChangeDayDto(
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    DateOnly? SwapWith,
    int BreakMinutes,
    bool IsFlexible,
    int? RequiredMinutes);

/// <summary>Jadvalni o'zgartirish so'rovi (SPEC-verifikatsiya-jadval-sorovi §C3).</summary>
/// <remarks>
/// Xodim ma'lumoti (ism, rasm, lavozim, bo'lim) <see cref="AbsenceRequestDto"/> dagi kabi —
/// ikki xil so'rov bitta navbatda bir xil ko'rinishi uchun.
/// </remarks>
/// <param name="Id">So'rov.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmployeeName">To'liq ism.</param>
/// <param name="EmployeePhotoKey">Rasm kaliti.</param>
/// <param name="PositionName">Bugungi lavozim.</param>
/// <param name="DepartmentName">Bugungi bo'lim.</param>
/// <param name="Kind">Tur.</param>
/// <param name="Days">Kunlar — sana tartibida.</param>
/// <param name="Note">Xodim izohi.</param>
/// <param name="Status">Holat.</param>
/// <param name="DecisionNote">HR izohi.</param>
/// <param name="DecidedByName">Qaror qilgan HR.</param>
/// <param name="DecidedAt">Qaror lahzasi.</param>
/// <param name="CreatedAt">Yuborilgan lahza.</param>
/// <param name="SubmittedByEmployee">Xodim o'zi ilovadan yuborgan.</param>
/// <param name="CancelledByEmployee">Xodim o'zi qaytarib olgan.</param>
public sealed record ScheduleChangeRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? EmployeePhotoKey,
    string? PositionName,
    string? DepartmentName,
    ScheduleChangeKind Kind,
    IReadOnlyList<ScheduleChangeDayDto> Days,
    string? Note,
    AbsenceRequestStatus Status,
    string? DecisionNote,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    DateTimeOffset CreatedAt,
    bool SubmittedByEmployee,
    bool CancelledByEmployee);

/// <summary>Xodimning bir sanadagi amaldagi rejasi — jadval so'rovi formasini to'ldirish uchun.</summary>
/// <param name="Date">Sana.</param>
/// <param name="HasSchedule">Shu sanada xodimga jadval biriktirilganmi.</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish.</param>
/// <param name="EndTime">Tugash.</param>
/// <param name="BreakStart">Tanaffus boshi.</param>
/// <param name="BreakEnd">Tanaffus oxiri.</param>
/// <param name="BreakMinutes">
/// Ayiriladigan tanaffus (daqiqa). Jadvalda u oynadan alohida: 60 daqiqalik tushlik 12:00–15:00
/// oynasida. Forma uni o'zgartirilmagan kun bilan qaytaradi — aks holda oyna uzunligi olinardi.
/// </param>
/// <param name="IsFlexible">Amaldagi kun «Erkin ish jadvali»mi — forma belgini shundan to'ldiradi.</param>
/// <param name="RequiredMinutes">«Ish vaqti» (daqiqa) — faqat erkin kunda; aks holda <c>null</c>.</param>
public sealed record DayPlanDto(
    DateOnly Date,
    bool HasSchedule,
    bool IsWorking,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    int BreakMinutes = 0,
    bool IsFlexible = false,
    int? RequiredMinutes = null);
