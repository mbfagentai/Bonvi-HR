using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// Hisoblash uchun o'tishdan kerak bo'lgan to'rtta fakt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega butun <see cref="AttendancePass"/> emas.</b> Qayta hisoblash
/// bir to'plamda o'n minglab o'tishni o'qiydi, va to'liq obyekt bilan
/// birga har biridagi <c>raw_event</c> (jsonb) ham xotiraga tushardi —
/// hisoblash undan birorta ham bayt o'qimaydi. Bu yozuvni bazadan
/// to'g'ridan-to'g'ri proyeksiya qilish mumkin.
/// </para>
/// <para>
/// ⭐ <b>O'zgarmas qiymat.</b> Hisoblash navbat bilan yo'nalish taxmin
/// qiladi va sakrashlarni tashlaydi, lekin bularning birortasi o'tishga
/// QAYTA YOZILMAYDI (SPEC §2.3): bu yerda yozishning imkoni ham yo'q.
/// </para>
/// </remarks>
/// <param name="Id">O'tish identifikatori — bir lahzadagi o'tishlarni tartiblash uchun.</param>
/// <param name="EmployeeId">Topilgan xodim; bog'lanmagan o'tishda <c>null</c>.</param>
/// <param name="OccurredAt">O'tish lahzasi.</param>
/// <param name="Direction">Qurilma aytgan (yoki rolidan kelib chiqqan) yo'nalish.</param>
public readonly record struct PassReading(
    Guid Id,
    Guid? EmployeeId,
    DateTimeOffset OccurredAt,
    PassDirection Direction)
{
    /// <summary>O'tish Toshkent kalendarining qaysi kuniga tushadi.</summary>
    /// <remarks>
    /// Saqlangan <c>local_date</c> bilan AYNAN bir xil funksiyadan
    /// hisoblanadi (<see cref="BonviTime.TashkentDate"/>), ya'ni ikkalasi
    /// ajralib keta olmaydi.
    /// </remarks>
    public DateOnly LocalDate => OccurredAt.TashkentDate();

    /// <summary>Saqlangan o'tishdan o'qiydi.</summary>
    /// <param name="pass">Saqlangan o'tish.</param>
    public static PassReading From(AttendancePass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);

        return new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction);
    }
}
