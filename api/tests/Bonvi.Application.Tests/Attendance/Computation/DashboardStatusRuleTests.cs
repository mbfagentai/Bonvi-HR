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
    public void Jadval_topilmasa_otish_bolsa_Keldi_bolmasa_NoSchedule()
    {
        // Egasining qarori (2026-10-12): jadvalsiz xodimning ham kirdi-chiqdisi olinadi.
        DashboardStatusRule.Classify(null, At("09:30"), Wednesday, At("10:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Arrived, null));

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

    // ────────────────────── Erkin ish jadvali (SPEC-erkin-jadval §2.2, 13 va 16-qatorlar) ──────────────────────

    /// <summary>08:30–18:00, norma 7:30 — eng kech kelish 10:30.</summary>
    private static DayPlan Flexible(DateOnly date) => DayPlan.For(AttendanceScenario.FlexibleSchedule(), date);

    /// <summary>Erkin kunda kelgan — doim «Vaqtida», kechikish daqiqasisiz; 11:00 da ham.</summary>
    [Theory]
    [InlineData("08:00")]
    [InlineData("10:00")]
    [InlineData("11:00")]
    [InlineData("16:30")]
    public void Erkin_kunda_kelgan_doim_vaqtida(string arrived)
        => DashboardStatusRule.Classify(Flexible(Wednesday), At(arrived), Wednesday, At("17:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.OnTime, null));

    [Fact]
    public void Erkin_kunda_eng_kech_kelishgacha_NotStarted_keyin_Absent()
    {
        var plan = Flexible(Wednesday);

        DashboardStatusRule.Classify(plan, null, Wednesday, At("10:29:59"))
            .Status.ShouldBe(AttendanceDashboardStatus.NotStarted, "08:30 dan keyin ham — hali kelishi mumkin");
        DashboardStatusRule.Classify(plan, null, Wednesday, At("10:30:00"))
            .Status.ShouldBe(AttendanceDashboardStatus.Absent);

        DashboardStatusRule.HasNotStarted(plan, Wednesday, At("10:29:59")).ShouldBeTrue();
        DashboardStatusRule.HasNotStarted(plan, Wednesday, At("10:30:00")).ShouldBeFalse();
    }

    [Fact]
    public void Erkin_kunda_sababli_kelmagan_Excused()
        => DashboardStatusRule.Classify(Flexible(Wednesday), null, Wednesday, At("11:00"), excused: true)
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Excused, null));

    /// <summary>
    /// Tungi erkin smena 20:00–08:00, norma 4 soat: seshanba 01:00 da dushanba kuni hali boshlanmagan
    /// (eng kech kelish seshanba 04:00) — SANA o'tgan bo'lsa ham; 04:00 dan keyin «Kelmadi».
    /// </summary>
    [Fact]
    public void Tungi_erkin_smenada_eng_kech_kelish_ertangi_sanada()
    {
        var monday = AttendanceScenario.Monday;
        var tuesday = AttendanceScenario.Tuesday;
        var plan = DayPlan.For(AttendanceScenario.FlexibleSchedule("20:00", "08:00", 0, null, null, 240), monday);

        DashboardStatusRule.Classify(plan, null, tuesday, AttendanceScenario.At(tuesday, "01:00"))
            .Status.ShouldBe(AttendanceDashboardStatus.NotStarted);
        DashboardStatusRule.Classify(plan, null, tuesday, AttendanceScenario.At(tuesday, "04:00"))
            .Status.ShouldBe(AttendanceDashboardStatus.Absent);
    }

    [Fact]
    public void Erkin_kunda_kelajak_sana_doim_NotStarted()
        => DashboardStatusRule.HasNotStarted(Flexible(Wednesday.AddDays(1)), Wednesday, At("23:00")).ShouldBeTrue();

    // ────────────────────── Shaxsiy vaqt (SPEC-sorov-tahriri §5.6, §5.8 panel tekshiruvlari) ──────────────────────

    /// <summary>§5.8 sukut rejasi: 08:30–17:30, tanaffus 60 daqiqa 12:00–15:00, yon berish 10.</summary>
    private static DayPlan PersonalBase(DateOnly date)
        => DayPlan.For(AttendanceScenario.Schedule("08:30", "17:30", 60, "12:00", "15:00"), date);

    /// <summary>Kunning shaxsiy vaqti — o'tishsiz, hisoblovchining o'z qoidasi bilan.</summary>
    private static PersonalTime Personal(DayPlan plan, string from, string to)
    {
        var intervals = PersonalTimeRule.IntervalsFor(
            plan.Date,
            plan,
            [new WorkCredit(plan.Date, plan.Date, AttendanceScenario.Time(from), AttendanceScenario.Time(to), CountsAsWorkTime: false)]);

        return PersonalTimeRule.Apply(PassPairing.Pair([], plan), plan, [], intervals).Personal;
    }

    [Fact]
    public void E12_ertalabki_shaxsiy_vaqtda_NotStarted_keyin_Absent_kelgan_vaqtida_yoki_kechikkan()
    {
        var plan = PersonalBase(Wednesday);
        var personal = Personal(plan, "08:30", "10:00");

        DashboardStatusRule.Classify(plan, null, Wednesday, At("09:00"), personal: personal)
            .Status.ShouldBe(AttendanceDashboardStatus.NotStarted);
        DashboardStatusRule.Classify(plan, null, Wednesday, At("10:01"), personal: personal)
            .Status.ShouldBe(AttendanceDashboardStatus.Absent);

        DashboardStatusRule.Classify(plan, At("10:05"), Wednesday, At("10:30"), personal: personal)
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.OnTime, null));
        DashboardStatusRule.Classify(plan, At("10:20"), Wednesday, At("10:30"), personal: personal)
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Late, 20));
    }

    [Fact]
    public void E1_kunduzgi_shaxsiy_vaqt_ertalabki_kelishga_tegmaydi()
        => DashboardStatusRule.Classify(
                PersonalBase(Wednesday),
                At("08:25"),
                Wednesday,
                At("18:00"),
                personal: Personal(PersonalBase(Wednesday), "14:00", "16:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.OnTime, null));

    [Fact]
    public void E5_kechikish_oraliq_oxiridan_yon_berishsiz()
        => DashboardStatusRule.Classify(
                PersonalBase(Wednesday),
                At("10:30"),
                Wednesday,
                At("18:00"),
                personal: Personal(PersonalBase(Wednesday), "08:30", "10:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Late, 30));

    [Fact]
    public void E8_kun_ortasidagi_shaxsiy_vaqt_panel_kechikishidan_ayriladi()
        => DashboardStatusRule.Classify(
                PersonalBase(Wednesday),
                At("11:00"),
                Wednesday,
                At("18:00"),
                personal: Personal(PersonalBase(Wednesday), "10:00", "12:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Late, 90));

    [Fact]
    public void E10_butun_reja_shaxsiy_Excused_E9_qisman_Absent()
    {
        var plan = PersonalBase(Wednesday);

        DashboardStatusRule.Classify(plan, null, Wednesday, At("18:00"), personal: Personal(plan, "08:00", "18:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Excused, null));
        DashboardStatusRule.Classify(plan, null, Wednesday, At("18:00"), personal: Personal(plan, "14:00", "16:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Absent, null));
    }

    [Fact]
    public void Boshlanmaganlik_chegarasi_berilsa_shu_lahzagacha_berilmasa_bugungi_qoida()
    {
        var plan = PersonalBase(Wednesday);
        var until = At("10:00");

        DashboardStatusRule.HasNotStarted(plan, Wednesday, At("09:59:59"), until).ShouldBeTrue();
        DashboardStatusRule.HasNotStarted(plan, Wednesday, At("10:00"), until).ShouldBeFalse();
        DashboardStatusRule.HasNotStarted(plan, Wednesday, At("09:59:59")).ShouldBeFalse("odatdagi qoida — reja 08:30 da boshlangan");
        DashboardStatusRule.HasNotStarted(PersonalBase(Wednesday.AddDays(1)), Wednesday, At("23:00"), until)
            .ShouldBeTrue("kelajak sana — doim boshlanmagan");
    }
}
