using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Scheduling;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Asosiy sahifa panelining holat qoidasi — bazasiz, egasi tasdiqlagan har bir chegara bilan.
/// </summary>
/// <remarks>
/// Butun zanjir (o'tishning kuni, tungi smena, org-filtr) <c>AttendanceReadModelTests</c> da,
/// haqiqiy PostgreSQL ustida. Bu yerda faqat qarorning o'zi: sekund aniqligidagi chegaralar
/// integratsiya testida sekin va shovqinli bo'lardi.
/// </remarks>
public sealed class DashboardStatusRuleTests
{
    /// <summary>2026-09-16 — chorshanba.</summary>
    private static readonly DateOnly Wednesday = AttendanceScenario.Monday.AddDays(2);

    private static DayPlan Office(DateOnly date) => DayPlan.For(AttendanceScenario.Schedule("09:00", "18:00", graceIn: 10), date);

    private static DateTimeOffset At(string time) => AttendanceScenario.At(Wednesday, time);

    [Fact]
    public void Jadval_topilmasa_otish_bolsa_ham_NoSchedule()
    {
        DashboardStatusRule.Classify(null, At("09:30"), Wednesday, At("10:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.NoSchedule, null));

        DashboardStatusRule.Classify(null, null, Wednesday, At("10:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.NoSchedule, null));
    }

    [Fact]
    public void Dam_olish_kunida_kelgan_ham_DayOff_kechikish_emas()
    {
        var dayOff = DayPlan.For(AttendanceScenario.Schedule("09:00", "18:00", daysOff: [3]), Wednesday);

        DashboardStatusRule.Classify(dayOff, At("11:45"), Wednesday, At("12:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.DayOff, null));
    }

    /// <summary>
    /// Chegara LAHZA bo'yicha va daqiqa REJA BOSHLANISHIDAN: 09:10:00 — vaqtida; 09:10:01 — kechikkan
    /// (tabelda bu 0 daqiqa bo'lardi); 09:33 — 33, 23 emas.
    /// </summary>
    [Theory]
    [InlineData("08:30:00", AttendanceDashboardStatus.OnTime, null)]
    [InlineData("09:10:00", AttendanceDashboardStatus.OnTime, null)]
    [InlineData("09:10:01", AttendanceDashboardStatus.Late, 10)]
    [InlineData("09:11:00", AttendanceDashboardStatus.Late, 11)]
    [InlineData("09:33:59", AttendanceDashboardStatus.Late, 33)]
    public void Kelgan_kun_yon_berish_chegarasi_va_kechikish_daqiqasi(string arrived, AttendanceDashboardStatus status, int? lateMinutes)
    {
        DashboardStatusRule.Classify(Office(Wednesday), At(arrived), Wednesday, At("18:00"))
            .ShouldBe(new DashboardVerdict(status, lateMinutes));
    }

    [Fact]
    public void Bugun_reja_boshlanishidan_oldin_NotStarted_roppa_rosa_boshlanishda_Absent()
    {
        var plan = Office(Wednesday);

        DashboardStatusRule.Classify(plan, null, Wednesday, At("08:59:59"))
            .Status.ShouldBe(AttendanceDashboardStatus.NotStarted);

        DashboardStatusRule.Classify(plan, null, Wednesday, At("09:00:00"))
            .Status.ShouldBe(AttendanceDashboardStatus.Absent, "yon berish tugashi kutilmaydi");
    }

    /// <summary>
    /// «Hali boshlanmagan» — tabel (<c>AttendanceDayBuilder</c>) ham aynan shu metod bilan
    /// o'tishsiz ish kunini «Kelmadi» emas, <c>Planned</c> deydi (audit #4).
    /// </summary>
    [Fact]
    public void Boshlanmaganlik_qoidasi_panel_va_tabel_uchun_bitta()
    {
        DashboardStatusRule.HasNotStarted(Office(Wednesday), Wednesday, At("08:59:59")).ShouldBeTrue();
        DashboardStatusRule.HasNotStarted(Office(Wednesday), Wednesday, At("09:00:00")).ShouldBeFalse();
        DashboardStatusRule.HasNotStarted(Office(Wednesday.AddDays(1)), Wednesday, At("23:00")).ShouldBeTrue();
        DashboardStatusRule.HasNotStarted(Office(Wednesday.AddDays(-1)), Wednesday, At("06:00")).ShouldBeFalse();
    }

    [Fact]
    public void Otgan_sana_hech_qachon_NotStarted_emas_kelajak_sana_doim_NotStarted()
    {
        // Soatlar ataylab «teskari»: kechagi sana uchun hozir 06:00 (reja soatidan oldin), ertangi
        // uchun 23:00 (keyin). Sana emas, faqat soat solishtirilsa, ikkala natija teskari chiqardi.
        DashboardStatusRule.Classify(Office(Wednesday.AddDays(-1)), null, Wednesday, At("06:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Absent, null));

        DashboardStatusRule.Classify(Office(Wednesday.AddDays(1)), null, Wednesday, At("23:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.NotStarted, null));
    }
}
