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
            day.BreakStart is { } start && day.BreakEnd is { } end && end < start);
    }
}
