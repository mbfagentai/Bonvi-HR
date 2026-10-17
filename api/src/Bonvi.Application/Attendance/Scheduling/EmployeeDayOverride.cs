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
    Guid RequestId,
    DateTimeOffset? DecidedAt = null)
{
    /// <summary>Tanaffus uzunligi — oynadan (yarim tundan o'tsa ham to'g'ri).</summary>
    public int BreakMinutes => IsWorking && BreakStart is { } start && BreakEnd is { } end && end != start
        ? (int)(end - start).TotalMinutes
        : 0;
}
