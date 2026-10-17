using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Tasdiqlangan jadval so'rovi xodimning o'sha sanadagi rejasi o'rniga turadi
/// (SPEC-verifikatsiya-jadval-sorovi §C2). Sukut sahna — ofis 09:00–18:00, yon berish 10/10.
/// </summary>
public sealed class ScheduleOverrideTests
{
    [Fact]
    public void Uzaytirilgan_tanaffus_uyda_bolgan_vaqtni_ushlaydi_va_reja_kamayadi()
    {
        // Verifix misoli: 12:23–14:23 uyda, so'rov shu oraliqni tanaffus qiladi.
        var sahna = Office().Override(Monday, "09:00", "18:00", "12:23", "14:23");
        sahna.Alternating(Monday, "09:00", "12:23", "14:23", "18:00");

        var kun = sahna.Day(Monday);

        kun.Facts.PlannedMinutes.ShouldBe(540 - 120);
        kun.Facts.WorkedMinutes.ShouldBe(420);
        kun.Facts.BreakTakenMinutes.ShouldBe(120);
        kun.Facts.BreakDeductionMinutes.ShouldBe(0);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Kechroq_boshlanadigan_kunda_kechikish_yangi_rejadan()
    {
        var sahna = Office().Override(Monday, "10:00", "19:00");
        sahna.Alternating(Monday, "10:05", "19:00");

        var kun = sahna.Day(Monday);

        kun.Facts.LateMinutes.ShouldBe(0, "10:00 + 10 daqiqa yon berish");
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
    }

    [Fact]
    public void Dam_olish_kuniga_aylantirilgan_kun_kelmadi_emas()
    {
        var sahna = Office().Override(Monday, null, null);

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.DayOff);
        kun.ShouldStore.ShouldBeFalse();
    }

    [Fact]
    public void Boshqa_kunga_va_boshqa_xodimga_tasir_qilmaydi()
    {
        var sahna = Office().Override(Monday, "10:00", "19:00");

        var seshanba = sahna.Day(Tuesday);

        seshanba.Facts.PlanStartAt.ShouldBe(At(Tuesday, "09:00").ToUniversalTime());
    }

    [Fact]
    public void Jadvalsiz_xodimga_qollanmaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Override(Monday, "10:00", "19:00");

        sahna.Day(Monday).Facts.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
    }
}
