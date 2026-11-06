using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// SPEC §5.7 jadvalining HAR BIR qatori — ishlab chiqarish yuboradigan shaklda: yo'nalishi
/// NOMA'LUM o'tishlar bilan (audit #9).
/// </summary>
/// <remarks>
/// <para>
/// Qolgan testlar §5.7 ni aniq «kirish/chiqish» bilan tekshiradi. Agent esa terminaldan
/// yo'nalish olmaydi va doim <see cref="PassDirection.Unknown"/> yuboradi (SPEC-agent H8):
/// yo'nalishni server beradi: kunning birinchisi kirish, oxirgisi chiqish (egasi, 2026-11-06). Aniq yo'nalishli test yashil bo'lib, haqiqiy
/// kun boshqacha hisoblanishi mumkin edi — bu fayl shu bo'shliqni yopadi.
/// </para>
/// <para>
/// Faqat yo'nalishga bog'liq bo'lmagan qatorlar (jadval yo'q va o'tish yo'q, ishdan ketgan
/// va o'tish yo'q, oy o'rtasida ishga kirgan, dam olish kuni o'tishsiz, bog'lanmagan o'tish)
/// bu yerda takrorlanmaydi — ularda o'tish yo'q yoki kunga tushmaydi.
/// </para>
/// </remarks>
public sealed class UnknownDirectionEdgeCaseTests
{
    private static AttendanceScenario Lunch(int breakMinutes = 60)
        => Office(breakMinutes, "13:00", "14:00");

    // ────────────────────── §5.7 asosiy jadval ──────────────────────

    /// <summary>Owner, 2026-11-06: an odd count no longer means a missing exit, the last reading is the exit.</summary>
    [Fact]
    public void Toq_sonli_otishda_oxirgisi_chiqish()
    {
        var sahna = Office();
        sahna.Unknowns(Monday, "09:00", "12:00", "13:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.LastOutAt.ShouldBe(At(Monday, "13:00"));
        kun.WorkedMinutes.ShouldBe(240);
        kun.IgnoredPassCount.ShouldBe(1);
    }

    /// <summary>
    /// «Birinchisi chiqish» noma'lum yo'nalishda BO'LMAYDI — kunning birinchi o'tishi doim kirish.
    /// Kechqurun yolg'iz o'tgan odam «kirish yo'q» emas, «chiqish yo'q» bo'ladi.
    /// </summary>
    [Fact]
    public void Yolgiz_kechki_otish_kirish_deb_olinadi()
    {
        var sahna = Office();
        sahna.Unknown(Monday, "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kun.FirstInAt.ShouldBe(At(Monday, "18:00"));
        kun.WorkedMinutes.ShouldBe(0);
    }

    [Fact]
    public void Juft_sonli_navbatlashgan_kun_kelgan()
    {
        var sahna = Office();
        sahna.Unknowns(Monday, "09:15", "17:40");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(505);
        kun.LateMinutes.ShouldBe(5);
        kun.EarlyLeaveMinutes.ShouldBe(10);
    }

    [Fact]
    public void On_ta_otish_bitta_oraliq()
    {
        var sahna = Office();
        sahna.Unknowns(Monday, "08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:30");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(570);
        kun.PassCount.ShouldBe(10);
        kun.IgnoredPassCount.ShouldBe(8);
    }

    /// <summary>
    /// Owner, 2026-11-06, replacing the 2026-09-27 reading: a morning read twice 20 seconds apart no longer eats the
    /// evening exit. 09:00:00, 09:00:20, 18:00 is 09:00–18:00, the middle reading is not counted.
    /// </summary>
    [Fact]
    public void Yigirma_soniya_farqli_oqish_kechki_chiqishni_yemaydi()
    {
        var sahna = Office();
        sahna.Unknowns(Monday, "09:00:00", "09:00:20", "18:00:00");

        var kun = sahna.Day(Monday).Facts;

        kun.IgnoredPassCount.ShouldBe(1);
        kun.PassCount.ShouldBe(3);
        kun.WorkedMinutes.ShouldBe(540);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    /// <summary>§5.7: tungi smena 20:00→08:00 — BITTA qator boshlangan sanada, ikkala o'tish ham unga.</summary>
    [Fact]
    public void Tungi_smena_bitta_qator_boshlangan_kunda()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.Unknown(Monday, "19:52");
        sahna.Unknown(Tuesday, "08:07");

        var kunlar = sahna.Days(Monday, Tuesday);
        var dushanba = kunlar[0].Facts;

        dushanba.Status.ShouldBe(AttendanceDayStatus.Present);
        dushanba.PassCount.ShouldBe(2);
        dushanba.WorkedMinutes.ShouldBe(735);
        dushanba.PlanEndAt.ShouldBe(At(Tuesday, "08:00"));
        dushanba.FirstInAt.ShouldBe(At(Monday, "19:52"));
        dushanba.LastOutAt.ShouldBe(At(Tuesday, "08:07"));

        kunlar[1].Facts.PassCount.ShouldBe(0, "08:07 dagi chiqish seshanbaga tushmaydi");
    }

    [Fact]
    public void Tungi_smenadan_0805_da_ketgan_ortiqcha_besh()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.Unknown(Monday, "20:00");
        sahna.Unknown(Tuesday, "08:05");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.OvertimeMinutes.ShouldBe(5);
    }

    /// <summary>
    /// Ketma-ket ikki tungi smena: seshanba ertalabki chiqishi DUSHANBAGA, kechqurungi kirishi
    /// SESHANBAGA — navbat har kun alohida boshlanadi, ya'ni seshanbaning 19:55 i kirish.
    /// </summary>
    [Fact]
    public void Ketma_ket_tungi_smenalar_navbati_har_kunda_alohida()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.Unknown(Monday, "19:55");
        sahna.Unknowns(Tuesday, "08:02", "19:55");
        sahna.Unknown(Tuesday.AddDays(1), "08:01");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.WorkedMinutes.ShouldBe(727);
        kunlar[0].Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kunlar[1].Facts.WorkedMinutes.ShouldBe(726);
        kunlar[1].Facts.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Jadvalsiz_kunda_ishlangan_vaqt_hisoblanadi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Unknowns(Monday, "09:30", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
        kun.WorkedMinutes.ShouldBe(510);
        kun.PlannedMinutes.ShouldBe(0);
        kun.LateMinutes.ShouldBe(0);
        kun.EarlyLeaveMinutes.ShouldBe(0);
    }

    [Fact]
    public void Ishdan_ketganning_otishi_ishlamaydi_holatida()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ().Terminate(new DateOnly(2026, 9, 10), reasonId: null).IsSuccess.ShouldBeTrue();
        sahna.Assign(Schedule("09:00", "18:00"));
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday);

        kun.ShouldStore.ShouldBeTrue();
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.NotEmployed);
        kun.Facts.WorkedMinutes.ShouldBe(540);
    }

    [Fact]
    public void Dam_olish_kunida_ishlagan()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", daysOff: [1]));
        sahna.Unknowns(Monday, "11:00", "15:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.DayOffWorked);
        kun.WorkedMinutes.ShouldBe(240);
        kun.LateMinutes.ShouldBe(0);
        kun.EarlyLeaveMinutes.ShouldBe(0);
    }

    [Fact]
    public void Ikki_munosabatda_kun_bitta_va_asosiysidan()
    {
        var sahna = new AttendanceScenario();
        var asosiyLavozim = Guid.CreateVersion7();
        var qoshimchaLavozim = Guid.CreateVersion7();
        var asosiy = sahna.Employ(positionId: asosiyLavozim);
        sahna.Employ(hired: new DateOnly(2025, 6, 1), positionId: qoshimchaLavozim).SetPrimary(false);

        var ofis = Schedule("09:00", "18:00");
        sahna.Assign(ofis, ScheduleScope.Position, asosiyLavozim);
        sahna.Assign(Schedule("20:00", "08:00"), ScheduleScope.Position, qoshimchaLavozim);
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.EmploymentId.ShouldBe(asosiy.Id);
        kun.WorkScheduleId.ShouldBe(ofis.Id);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Soati_oggan_qurilmaning_nomalum_otishi_odatdagidek()
    {
        var sahna = Office();

        foreach (var time in new[] { "09:00", "18:00" })
        {
            var otish = AttendancePass.Record(
                Guid.CreateVersion7(),
                AttendanceIdentityProvider.Verifix,
                "1001",
                At(Monday, time),
                receivedAt: At(Monday, time).AddMinutes(10),
                dedupeKey: $"kalit-{time}").Value;
            otish.Resolve(sahna.EmployeeId, At(Monday, time));
            otish.Direction.ShouldBe(PassDirection.Unknown);

            sahna.Pass(PassReading.From(otish));
        }

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(540);
    }

    // ────────────────────── §5.7 tanaffus bloki (09:00–18:00, 60, oyna 13:00–14:00) ──────────────────────

    [Fact]
    public void Oynasiz_jadvalda_ushlanma_yoq()
    {
        var sahna = Office(breakMinutes: 60);
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(540);
    }

    /// <summary>
    /// (readings, breakTaken, breakDeduction, worked). Owner, 2026-11-06: readings between the first and last are not
    /// counted on such a terminal, so a trip out is never taken from the day and the whole break is deducted.
    /// </summary>
    [Theory]
    [InlineData("chiqmagan", new[] { "09:00", "18:00" }, 0, 60, 480)]
    [InlineData("aynan oyna bo'yicha tushlik", new[] { "09:00", "13:00", "14:00", "18:00" }, 0, 60, 480)]
    [InlineData("tanaffusdan uzoq chiqqan", new[] { "09:00", "12:30", "14:00", "18:00" }, 0, 60, 480)]
    [InlineData("oynadan tashqarida chiqqan", new[] { "09:00", "10:00", "10:30", "18:00" }, 0, 60, 480)]
    [InlineData("oyna ichida ikki qisqa chiqish", new[] { "09:00", "13:05", "13:20", "13:40", "13:55", "18:00" }, 0, 60, 480)]
    [InlineData("oyna chegarasidan o'tgan chiqish", new[] { "09:00", "12:45", "13:25", "18:00" }, 0, 60, 480)]
    [InlineData("oynadan oldin uyga ketgan", new[] { "09:00", "12:55" }, 0, 0, 235)]
    [InlineData("oyna o'rtasida kelgan", new[] { "13:30", "18:00" }, 0, 30, 240)]
    [InlineData("oyna ichida ketgan", new[] { "09:00", "13:15" }, 0, 15, 240)]
    public void Tanaffus_jadvali_nomalum_yonalish_bilan(string holat, string[] vaqtlar, int olingan, int ushlanma, int ishlangan)
    {
        var sahna = Lunch();
        sahna.Unknowns(Monday, vaqtlar);

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(olingan, holat);
        kun.BreakDeductionMinutes.ShouldBe(ushlanma, holat);
        kun.WorkedMinutes.ShouldBe(ishlangan, holat);
        kun.Status.ShouldBe(AttendanceDayStatus.Present, holat);
    }

    [Fact]
    public void Tanaffus_30_oyna_60_chiqmagan()
    {
        var sahna = Lunch(breakMinutes: 30);
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakDeductionMinutes.ShouldBe(30);
        kun.WorkedMinutes.ShouldBe(510);
    }

    [Fact]
    public void Oynali_kunda_uchta_otishda_oxirgisi_chiqish()
    {
        var sahna = Lunch();
        sahna.Unknowns(Monday, "09:00", "13:00", "14:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(60);
        kun.WorkedMinutes.ShouldBe(240);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Oynadan_oldin_chiqish_yozilmagan()
    {
        var sahna = Lunch();
        sahna.Unknown(Monday, "09:00");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
    }

    /// <summary>
    /// §5.7: tungi smena 20:00→08:00, oyna 00:30→01:30, 00:35 da chiqib 01:10 da qaytgan. Oyna
    /// SESHANBA 00:30 ga qo'yiladi; to'rt noma'lum o'tish navbat bilan — kirish, chiqish, kirish, chiqish.
    /// </summary>
    [Fact]
    public void Tungi_smenadagi_tanaffus_oynasi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00", breakMinutes: 60, breakStart: "00:30", breakEnd: "01:30"));
        sahna.Unknown(Monday, "20:00");
        sahna.Unknowns(Tuesday, "00:35", "01:10", "08:00");

        var kunlar = sahna.Days(Monday, Tuesday);
        var kun = kunlar[0].Facts;

        kun.BreakTakenMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(60);
        kun.WorkedMinutes.ShouldBe(660, "720 − 60");
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kunlar[1].Facts.PassCount.ShouldBe(0, "bitta qator, boshlangan sanada");
    }

    [Fact]
    public void Dam_olish_kunida_ishlaganda_ushlanma_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", breakMinutes: 60, breakStart: "13:00", breakEnd: "14:00", daysOff: [1]));
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.DayOffWorked);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(540);
    }

    [Fact]
    public void Jadvalsiz_kunda_ushlanma_yoq()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(540);
    }

    /// <summary>
    /// Aralash yo'nalish — ochiq savol (egasi hozircha o'zgartirmagan): telefonda «Kirish» 08:55,
    /// keyin umumiy terminal 09:00 va 18:00. Ma'lum kirishdan keyin navbat chiqish kutadi —
    /// 09:00 chiqish, 18:00 kirish bo'lib qoladi. Bu sinov joriy xatti-harakatni QOTIRADI: qaror
    /// o'zgarsa, u ataylab yangilanadi (docs/ASSUMPTIONS.md, 2026-09-27).
    /// </summary>
    [Fact]
    public void Aralash_yonalish_joriy_xatti_harakati_qotirilgan()
    {
        var sahna = Office();
        sahna.In(Monday, "08:55");
        sahna.Unknowns(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(5);
        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
    }
}
