using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Scheduling;

/// <summary>
/// Jadval kunini lahzalarga aylantirish — SPEC §2.4.0.1, §5.3, §5.5.1 (oyna formulasi), §5.5 6-qadam.
/// </summary>
public sealed class DayPlanTests
{
    [Fact]
    public void Ofis_smenasi_Toshkent_vaqtida_boshlanadi_UTC_da_saqlanadi()
    {
        var reja = DayPlan.For(Schedule("09:00", "18:00", breakMinutes: 60), Monday);

        reja.IsWorking.ShouldBeTrue();
        reja.PlanStartAt.ShouldBe(new DateTimeOffset(2026, 9, 14, 4, 0, 0, TimeSpan.Zero));
        reja.PlanEndAt.ShouldBe(new DateTimeOffset(2026, 9, 14, 13, 0, 0, TimeSpan.Zero));
        reja.PlanStartAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        reja.PlannedMinutes.ShouldBe(480);
        reja.GraceInMinutes.ShouldBe(10);
    }

    [Fact]
    public void Tungi_smena_ertangi_kunda_tugaydi()
    {
        var reja = DayPlan.For(Schedule("20:00", "08:00"), Monday);

        reja.PlanStartAt.ShouldBe(At(Monday, "20:00"));
        reja.PlanEndAt.ShouldBe(At(Tuesday, "08:00"));
        reja.PlanEndAt!.Value.TashkentDate().ShouldBe(Tuesday);
        reja.PlannedMinutes.ShouldBe(720);
    }

    /// <summary>
    /// SPEC §5.5.1 tuzatishi: dushanbadagi 20:00–08:00 smenaning 00:30–01:30 oynasi
    /// SESHANBAGA tushadi. «Sana + soat» uni dushanba 00:30 ga qo'yardi.
    /// </summary>
    [Fact]
    public void Tungi_smenaning_tanaffus_oynasi_ertangi_kunga_tushadi()
    {
        var reja = DayPlan.For(Schedule("20:00", "08:00", 60, "00:30", "01:30"), Monday);

        reja.HasBreakWindow.ShouldBeTrue();
        reja.BreakWindowStartAt.ShouldBe(At(Tuesday, "00:30"));
        reja.BreakWindowEndAt.ShouldBe(At(Tuesday, "01:30"));
    }

    [Fact]
    public void Tungi_smenaning_yarim_tundan_oldingi_oynasi_shu_kunda_qoladi()
    {
        var reja = DayPlan.For(Schedule("20:00", "08:00", 30, "23:30", "00:00"), Monday);

        reja.BreakWindowStartAt.ShouldBe(At(Monday, "23:30"));
        reja.BreakWindowEndAt.ShouldBe(At(Tuesday, "00:00"));
    }

    [Fact]
    public void Oynasiz_kunda_oyna_yoq()
    {
        var reja = DayPlan.For(Schedule("09:00", "18:00", breakMinutes: 60), Monday);

        reja.HasBreakWindow.ShouldBeFalse();
        reja.BreakWindowStartAt.ShouldBeNull();
        reja.BreakMinutes.ShouldBe(60);
    }

    /// <summary>
    /// Yillik to'r katagi (SPEC-jadval-to'ri §B1) o'z tanaffus OYNASINI beradi: naqshda oyna yo'q
    /// bo'lsa ham, katakdagisi o'qiladi. Busiz katakdagi tanaffus rejadan ayirilib, faktdan
    /// ayirilmasdi (§5.5.1) va har bir shunday sanada soxta ortiqcha ish chiqardi.
    /// </summary>
    [Fact]
    public void Sana_katagining_tanaffus_oynasi_naqshdan_ustun()
    {
        var jadval = Schedule("09:00", "18:00");
        var katak = WorkScheduleDate.Create(
            jadval.Id,
            new WorkScheduleDateInput(Monday, true, new TimeOnly(9, 0), new TimeOnly(18, 0), 60, new TimeOnly(13, 0), new TimeOnly(14, 0))).Value;

        DayPlan.For(jadval, Monday).HasBreakWindow.ShouldBeFalse("sinov sharti: naqshda oyna yo'q");

        var reja = DayPlan.For(jadval, Monday, katak);

        reja.HasBreakWindow.ShouldBeTrue();
        reja.BreakWindowStartAt.ShouldBe(At(Monday, "13:00"));
        reja.BreakWindowEndAt.ShouldBe(At(Monday, "14:00"));
        reja.BreakMinutes.ShouldBe(60);
        reja.PlannedMinutes.ShouldBe(480);
    }

    /// <summary>Oynasiz katak — oyna yo'q, garchi tanaffus daqiqasi bo'lsa ham (§5.5.1).</summary>
    [Fact]
    public void Oynasiz_sana_katagida_oyna_yoq()
    {
        var jadval = Schedule("09:00", "18:00", 60, "13:00", "14:00");
        var katak = WorkScheduleDate.Create(
            jadval.Id,
            new WorkScheduleDateInput(Monday, true, new TimeOnly(9, 0), new TimeOnly(18, 0), 60)).Value;

        var reja = DayPlan.For(jadval, Monday, katak);

        reja.HasBreakWindow.ShouldBeFalse("katak naqshni butunlay almashtiradi — naqshning oynasi ulanmaydi");
        reja.PlannedMinutes.ShouldBe(480);
    }

    [Fact]
    public void Kun_oynasi_rejadan_olti_soat_oldin_va_keyin()
    {
        var reja = DayPlan.For(Schedule("20:00", "08:00"), Monday);

        reja.AttributionFrom.ShouldBe(At(Monday, "14:00"));
        reja.AttributionTo.ShouldBe(At(Tuesday, "14:00"));
    }

    [Fact]
    public void Dam_olish_kunida_reja_yoq()
    {
        var reja = DayPlan.For(Schedule("09:00", "18:00", 60, "13:00", "14:00", daysOff: [1]), Monday);

        reja.IsWorking.ShouldBeFalse();
        reja.PlanStartAt.ShouldBeNull();
        reja.PlanEndAt.ShouldBeNull();
        reja.PlannedMinutes.ShouldBe(0);
        reja.BreakMinutes.ShouldBe(0);
        reja.HasBreakWindow.ShouldBeFalse();
        reja.AttributionFrom.ShouldBeNull();
    }

    /// <summary><see cref="DayOfWeek.Sunday"/> — 0, jadvalda esa 7.</summary>
    [Fact]
    public void Yakshanba_jadvalning_yettinchi_kuni()
    {
        var jadval = Schedule("09:00", "18:00", daysOff: [7]);
        var yakshanba = new DateOnly(2026, 9, 20);

        yakshanba.DayOfWeek.ShouldBe(DayOfWeek.Sunday);
        DayPlan.For(jadval, yakshanba).IsWorking.ShouldBeFalse();
        DayPlan.For(jadval, yakshanba.AddDays(-1)).IsWorking.ShouldBeTrue();
    }

    [Fact]
    public void Har_bir_hafta_kuni_oz_qatoridan_oqiladi()
    {
        var jadval = Schedule(
        [
            WorkingDay(1, "09:00", "18:00"),
            WorkingDay(2, "10:00", "19:00"),
            WorkingDay(3, "09:00", "18:00"),
            WorkingDay(4, "09:00", "18:00"),
            WorkingDay(5, "09:00", "14:00"),
            new WorkScheduleDayInput(6, IsWorking: false),
            new WorkScheduleDayInput(7, IsWorking: false),
        ]);

        DayPlan.For(jadval, Tuesday).PlanStartAt.ShouldBe(At(Tuesday, "10:00"));
        DayPlan.For(jadval, new DateOnly(2026, 9, 18)).PlannedMinutes.ShouldBe(300);
    }

    // ────────────────────── Erkin ish jadvali (SPEC-erkin-jadval §4) ──────────────────────

    [Fact]
    public void Erkin_naqsh_kunida_reja_normaning_ozi_va_eng_kech_kelish()
    {
        var reja = DayPlan.For(FlexibleSchedule(requiredMinutes: 400), Monday);

        reja.IsFlexible.ShouldBeTrue();
        reja.PlannedMinutes.ShouldBe(400);
        reja.PlanStartAt.ShouldBe(At(Monday, "08:30"));
        reja.PlanEndAt.ShouldBe(At(Monday, "18:00"));
        reja.LatestStartAt.ShouldBe(At(Monday, "11:20"));
        reja.BreakMinutes.ShouldBe(120, "tanaffus qoidasi o'zgarmaydi");
    }

    [Fact]
    public void Qatiy_kunda_eng_kech_kelish_reja_boshlanishi()
    {
        var reja = DayPlan.For(Schedule("09:00", "18:00", 60), Monday);

        reja.IsFlexible.ShouldBeFalse();
        reja.LatestStartAt.ShouldBe(reja.PlanStartAt);
    }

    [Fact]
    public void Erkin_sana_katagi_naqshdan_ustun()
    {
        var jadval = Schedule("09:00", "18:00", 60);
        var katak = WorkScheduleDate.Create(
            jadval.Id,
            new WorkScheduleDateInput(
                Monday, true, new TimeOnly(8, 30), new TimeOnly(18, 0), 120, new TimeOnly(12, 0), new TimeOnly(14, 0),
                IsFlexible: true)).Value;

        var reja = DayPlan.For(jadval, Monday, katak);

        reja.IsFlexible.ShouldBeTrue();
        reja.PlannedMinutes.ShouldBe(450, "sukut norma katakda yozilgan");
        reja.LatestStartAt.ShouldBe(At(Monday, "10:30"));
        DayPlan.For(jadval, Tuesday).IsFlexible.ShouldBeFalse();
    }

    [Fact]
    public void Erkin_sorov_kunida_reja_normadan()
    {
        var jadval = Schedule("09:00", "18:00");
        var kun = new EmployeeDayOverride(
            Guid.CreateVersion7(), Monday, true, new TimeOnly(10, 0), new TimeOnly(19, 0), null, null, 0,
            Guid.CreateVersion7(), IsFlexible: true, RequiredMinutes: 300);

        var reja = DayPlan.ForOverride(jadval, kun);

        reja.IsFlexible.ShouldBeTrue();
        reja.PlannedMinutes.ShouldBe(300);
        reja.LatestStartAt.ShouldBe(At(Monday, "14:00"));
    }

    /// <summary>Erkin naqsh ustidagi dam olish katagi — erkinlik ham, eng kech kelish ham yo'q.</summary>
    [Fact]
    public void Dam_olish_katagida_erkinlik_yoq()
    {
        var jadval = FlexibleSchedule();
        var katak = WorkScheduleDate.Create(jadval.Id, new WorkScheduleDateInput(Monday, IsWorking: false)).Value;

        var reja = DayPlan.For(jadval, Monday, katak);

        reja.IsFlexible.ShouldBeFalse();
        reja.LatestStartAt.ShouldBeNull();
    }
}
