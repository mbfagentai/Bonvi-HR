using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// O'tish qaysi kunga tegishli — SPEC §5.3.
/// </summary>
public sealed class PassAttributionTests
{
    private static PassReading P(DateOnly date, string time)
        => new(Guid.CreateVersion7(), Guid.Empty, At(date, time), PassDirection.In);

    /// <summary>Jadvalni har bir sana uchun rejaga aylantiradi (munosabat har doim bor deb).</summary>
    private static Func<DateOnly, DayPlan?> Plans(WorkSchedule schedule) => date => DayPlan.For(schedule, date);

    [Fact]
    public void Jadval_yoq_bolsa_otish_oz_sanasida_qoladi()
    {
        PassAttribution.DateFor(P(Tuesday, "01:00"), _ => null).ShouldBe(Tuesday);
    }

    [Fact]
    public void Tungi_smenaning_ertalabki_chiqishi_boshlangan_kunga_tushadi()
    {
        var tungi = Plans(Schedule("20:00", "08:00"));

        PassAttribution.DateFor(P(Monday, "19:52"), tungi).ShouldBe(Monday);
        PassAttribution.DateFor(P(Tuesday, "08:07"), tungi).ShouldBe(Monday);
    }

    [Fact]
    public void Kechagi_tungi_smena_tushdan_keyingi_otishni_olmaydi()
    {
        var tungi = Plans(Schedule("20:00", "08:00"));

        // Dushanba oynasi seshanba 14:00 da tugaydi; 15:00 — seshanbaning o'z smenasi.
        PassAttribution.DateFor(P(Tuesday, "15:00"), tungi).ShouldBe(Tuesday);
    }

    /// <summary>Oynalar orasiga tushgan o'tish hech qaysi smenaga «yopishtirilmaydi».</summary>
    [Fact]
    public void Hech_bir_oynaga_tushmagan_otish_oz_sanasida_qoladi()
    {
        var ofis = Plans(Schedule("09:00", "18:00"));

        // Dushanba oynasi seshanba 00:00 da tugaydi, seshanbaniki 03:00 da boshlanadi.
        PassAttribution.DateFor(P(Tuesday, "01:00"), ofis).ShouldBe(Tuesday);
    }

    [Fact]
    public void Oyna_chegaralari_ichida()
    {
        var ofis = Plans(Schedule("09:00", "18:00"));

        PassAttribution.DateFor(P(Tuesday, "00:00"), ofis).ShouldBe(Monday, "18:00 + 6 soat — oxirgi chekka ichida");
        PassAttribution.DateFor(P(Tuesday, "03:00"), ofis).ShouldBe(Tuesday, "09:00 − 6 soat — birinchi chekka ichida");
    }

    [Fact]
    public void Dam_olish_kunida_oyna_yoq()
    {
        var jadval = Plans(Schedule("09:00", "18:00", daysOff: [1]));

        // Dushanba dam — yakshanbaning oynasi tugagan, seshanbaniki boshlanmagan.
        PassAttribution.DateFor(P(Monday, "12:00"), jadval).ShouldBe(Monday);
    }

    [Fact]
    public void Ikki_oyna_qamrasa_markazi_yaqinrogi_yutadi()
    {
        // Dushanba 20:00–08:00 (oyna markazi seshanba 02:00),
        // seshanba 09:00–18:00 (oyna markazi seshanba 13:30).
        var jadval = Plans(MondayNightTuesdayDay("09:00", "18:00"));

        PassAttribution.DateFor(P(Tuesday, "08:30"), jadval).ShouldBe(Tuesday, "13:30 ga 5 soat, 02:00 ga 6,5");
        PassAttribution.DateFor(P(Tuesday, "07:00"), jadval).ShouldBe(Monday, "02:00 ga 5 soat, 13:30 ga 6,5");
    }

    [Fact]
    public void Markazlar_teng_bolsa_erta_sana_yutadi()
    {
        // Dushanba 20:00–08:00 (markaz seshanba 02:00),
        // seshanba 06:00–10:00 (oyna 00:00–16:00, markaz 08:00). 05:00 — ikkalasiga 3 soat.
        var jadval = Plans(MondayNightTuesdayDay("06:00", "10:00"));

        PassAttribution.DateFor(P(Tuesday, "05:00"), jadval).ShouldBe(Monday);
    }

    private static WorkSchedule MondayNightTuesdayDay(string tuesdayStart, string tuesdayEnd)
        => Schedule(
        [
            WorkingDay(1, "20:00", "08:00"),
            WorkingDay(2, tuesdayStart, tuesdayEnd),
            new WorkScheduleDayInput(3, IsWorking: false),
            new WorkScheduleDayInput(4, IsWorking: false),
            new WorkScheduleDayInput(5, IsWorking: false),
            new WorkScheduleDayInput(6, IsWorking: false),
            new WorkScheduleDayInput(7, IsWorking: false),
        ]);
}
