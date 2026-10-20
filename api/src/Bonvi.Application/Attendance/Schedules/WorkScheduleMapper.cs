using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Schedules;

/// <summary><see cref="WorkSchedule"/> ni DTO ga o'giradi — sof funksiya, bazaga murojaatsiz.</summary>
public static class WorkScheduleMapper
{
    /// <summary>Jadval — kunlari <c>DayIndex</c> tartibida.</summary>
    /// <param name="schedule">Kunlari bilan yuklangan jadval.</param>
    public static WorkScheduleDto ToDto(WorkSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return new WorkScheduleDto(
            schedule.Id,
            schedule.Name,
            schedule.Kind,
            schedule.GraceInMinutes,
            schedule.GraceOutMinutes,
            schedule.IsActive,
            schedule.Note,
            [.. schedule.Days.OrderBy(day => day.DayIndex).Select(ToDto)]);
    }

    /// <summary>Bitta kun.</summary>
    /// <remarks>
    /// <c>BreakCrossesMidnight</c> — AYNAN <c>breakEnd &lt; breakStart</c> soat
    /// qiymati bo'yicha (SPEC §6.4): 23:30→00:30 uchun <c>true</c>, oyna
    /// bo'lmasa <c>false</c>. Muharrir uchun qulaylik, hisoblash unga tayanmaydi.
    /// </remarks>
    /// <param name="day">Jadval kuni.</param>
    public static WorkScheduleDayDto ToDto(WorkScheduleDay day)
    {
        ArgumentNullException.ThrowIfNull(day);

        return new WorkScheduleDayDto(
            day.DayIndex,
            day.IsWorking,
            day.StartTime,
            day.EndTime,
            day.BreakMinutes,
            day.BreakStart,
            day.BreakEnd,
            day.BreakStart is { } start && day.BreakEnd is { } end && end < start,
            // Dam olish kunida «erkin» yo'q: eski image bilan yozilgan qator ham shunday ko'rinsin.
            day.IsWorking && day.IsFlexible,
            day.IsWorking && day.IsFlexible ? day.RequiredMinutes : null);
    }

    /// <summary>Yillik to'rning katagi — hafta kuni shaklida (<c>GET /schedules/resolve</c>).</summary>
    /// <remarks>
    /// <c>DayIndex</c> — katak sanasining ISO hafta kuni (1 — dushanba … 7 — yakshanba):
    /// ekran kun nomini shundan chiqaradi. Qolgan maydonlar naqsh kunidagi bilan bir
    /// ma'noda, <c>BreakCrossesMidnight</c> ham aynan o'sha qoida bilan.
    /// </remarks>
    /// <param name="date">Jadvalning shu sanaga yozilgan katagi.</param>
    public static WorkScheduleDayDto ToDto(WorkScheduleDate date)
    {
        ArgumentNullException.ThrowIfNull(date);

        return new WorkScheduleDayDto(
            date.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.Date.DayOfWeek,
            date.IsWorking,
            date.StartTime,
            date.EndTime,
            date.BreakMinutes,
            date.BreakStart,
            date.BreakEnd,
            date.BreakStart is { } start && date.BreakEnd is { } end && end < start,
            date.IsWorking && date.IsFlexible,
            date.IsWorking && date.IsFlexible ? date.RequiredMinutes : null);
    }
}
