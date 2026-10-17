namespace Bonvi.Application.Attendance.Scheduling;

/// <summary>
/// Tasdiqlangan jadval so'rovining bitta kuni — xodimning o'sha sanadagi rejasi o'rniga
/// (SPEC-verifikatsiya-jadval-sorovi §C2).
/// </summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Date">Sana (smena boshlanadigan Toshkent sanasi).</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish.</param>
/// <param name="EndTime">Tugash — boshlanishdan oldin bo'lsa tungi smena.</param>
/// <param name="BreakStart">Tanaffus boshi.</param>
/// <param name="BreakEnd">Tanaffus oxiri.</param>
/// <param name="BreakMinutes">Ayiriladigan tanaffus (daqiqa) — so'rov kunida saqlangani.</param>
/// <param name="RequestId">So'rov — «nega shu soat» savoliga javob.</param>
/// <param name="DecidedAt">Qaror lahzasi — bir sanaga ikki qaror kelsa, oxirgisi yutadi.</param>
public sealed record EmployeeDayOverride(
    Guid EmployeeId,
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd,
    int BreakMinutes,
    Guid RequestId,
    DateTimeOffset? DecidedAt = null);
