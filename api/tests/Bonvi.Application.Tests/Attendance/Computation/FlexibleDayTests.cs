using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// «Erkin ish jadvali» kunining hisobi — SPEC-verifikatsiya-erkin-jadval §2.3, F1–F16 misollari.
/// </summary>
/// <remarks>
/// <para>
/// Sukut sahna (§2.3 «Base»): erkin, oyna 08:30–18:00, tanaffus 120 daqiqa 12:00–14:00 oynasida,
/// norma 450 (7:30, sukut). «Hozir» — reja tugagandan keyin, aytilmagan bo'lsa.
/// </para>
/// <para>
/// ⭐ <b>Borliq oynaga KESILMAYDI</b> — egasining qarori (2026-10-20), SPEC dagi kesish o'rniga:
/// erkin kunda BUTUN borliq sanaladi, tanaffus qoidasi bugungidek, oyna faqat yo'l-yo'riq
/// (kun oynasi, tanaffus oynasi, eng kech kelish). Kesishni nazarda tutgan misollar (F4, F5, F7)
/// shu qarorga ko'ra qayta hisoblangan; egasining o'z misoli — alohida test.
/// </para>
/// </remarks>
public sealed class FlexibleDayTests
{
    private const int Norma = 450;

    [Fact]
    public void F1_oyna_boshidan_oxirigacha_norma_bajarilgan()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "08:30", "18:00");

        var kun = Hisob(sahna, closed: 570, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 450, shortfall: 0);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.LateMinutes.ShouldBe(0);
        kun.PlannedMinutes.ShouldBe(Norma);
        kun.IsFlexible.ShouldBeTrue();
    }

    [Fact]
    public void F2_kech_kelgan_kechikmaydi_kam_ishlaydi()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "10:00", "18:00");

        var kun = Hisob(sahna, closed: 480, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 360, shortfall: 90);
        kun.LateMinutes.ShouldBe(0, "qat'iy kunda 80 bo'lardi (F16)");
        kun.EarlyLeaveMinutes.ShouldBe(0);
    }

    [Fact]
    public void F3_tushlikka_chiqqan_kunda_tanaffus_ushlanmaydi()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "08:30", "12:00", "14:00", "18:00");

        var kun = Hisob(sahna, closed: 450, breakTaken: 120, presentInWindow: 0, deduction: 0, worked: 450, shortfall: 0);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    /// <summary>
    /// 07:00–16:00: oynadan oldingi 1,5 soat ham SANALADI (egasi) — ishlangan 540 − 120 = 420,
    /// kam 30. Birinchi kirish — haqiqiy 07:00, erta ketish yo'q.
    /// </summary>
    [Fact]
    public void F4_oynadan_oldin_kelgan_vaqt_ham_sanaladi()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "07:00", "16:00");

        var kun = Hisob(sahna, closed: 540, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 420, shortfall: 30);
        kun.FirstInAt.ShouldBe(At(Monday, "07:00").ToUniversalTime());
        kun.EarlyLeaveMinutes.ShouldBe(0, "qat'iy kunda 110 bo'lardi");
    }

    /// <summary>09:00–19:30: oynadan keyingi vaqt ham sanaladi — 630 − 120 = 510, ortiqcha 60, kam yo'q.</summary>
    [Fact]
    public void F5_oynadan_keyin_qolgan_vaqt_ham_sanaladi_va_ortiqcha_bo_ladi()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "09:00", "19:30");

        var kun = Hisob(sahna, closed: 630, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 510, shortfall: 0);
        kun.OvertimeMinutes.ShouldBe(60);
    }

    /// <summary>13:00 da ketdi, qaytmadi — dum tanaffus bo'shlig'i emas; binoda bo'lgan 60 daqiqa ushlanadi.</summary>
    [Fact]
    public void F6_tushlikda_ketib_qaytmagan()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "08:30", "13:00");

        var kun = Hisob(sahna, closed: 270, breakTaken: 0, presentInWindow: 60, deduction: 60, worked: 210, shortfall: 240);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    /// <summary>
    /// 18:30–22:00 (kun oynasining +6 soati ichida) — oynadan tashqaridagi borliq ham sanaladi:
    /// 210 ishlangan, tanaffus oynasida bo'lmagan — ushlanma yo'q, kam 240.
    /// </summary>
    [Fact]
    public void F7_oyna_tugagach_kelgan_vaqt_ham_sanaladi()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "18:30", "22:00");

        var kun = Hisob(sahna, closed: 210, breakTaken: 0, presentInWindow: 0, deduction: 0, worked: 210, shortfall: 240);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.PassCount.ShouldBe(2, "ikkala o'tish ham shu kunga tushadi");
    }

    [Fact]
    public void F8_chiqishi_yozilmagan_kunda_kam_ishlangan_yoq()
    {
        var sahna = FlexibleOffice();
        sahna.In(Monday, "09:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kun.WorkedMinutes.ShouldBe(0);
        kun.ShortfallMinutes.ShouldBe(0, "holatning o'zi aytadi — ikki marta sanalmaydi");
    }

    /// <summary>10:00 — eng kech kelish (18:00 − 7:30 = 10:30) hali kelmagan: «Hali boshlanmagan», qator yo'q.</summary>
    [Fact]
    public void F9_eng_kech_kelishgacha_hali_boshlanmagan()
    {
        var kun = FlexibleOffice().Now(Monday, "10:00").Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Planned);
        kun.ShouldStore.ShouldBeFalse();
        kun.Facts.ShortfallMinutes.ShouldBe(0);

        DashboardStatusRule.Classify(Reja(), null, Monday, At(Monday, "10:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.NotStarted, null));
    }

    [Fact]
    public void F10_eng_kech_kelishdan_keyin_kelmadi()
    {
        var kun = FlexibleOffice().Now(Monday, "10:31").Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Absent);
        kun.ShouldStore.ShouldBeTrue("boshlangan smenalar vazifasi shu qatorni yozadi");
        kun.Facts.ShortfallMinutes.ShouldBe(0);

        DashboardStatusRule.Classify(Reja(), null, Monday, At(Monday, "10:31"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.Absent, null));
    }

    [Fact]
    public void F11_kichik_norma_kech_boshlansa_ham_bajariladi()
    {
        var sahna = FlexibleOffice(requiredMinutes: 240);
        sahna.Alternating(Monday, "14:00", "18:00");

        var kun = Hisob(sahna, closed: 240, breakTaken: 0, presentInWindow: 0, deduction: 0, worked: 240, shortfall: 0);
        kun.PlannedMinutes.ShouldBe(240);
        Reja(requiredMinutes: 240).LatestStartAt.ShouldBe(At(Monday, "14:00").ToUniversalTime());
    }

    /// <summary>«Ishxona ishi bilan» 10:00–12:00 + binoda 14:00–18:00: 360 (120 so'rovdan), kam 90.</summary>
    [Fact]
    public void F12_ish_vaqti_sorovi_kam_ishlanganni_kamaytiradi()
    {
        var sahna = FlexibleOffice().WorkCredit(Monday, fromTime: "10:00", toTime: "12:00");
        sahna.Alternating(Monday, "14:00", "18:00");

        var kun = sahna.DayCredited(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(360);
        kun.CreditedMinutes.ShouldBe(120);
        kun.BreakTakenMinutes.ShouldBe(120);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.ShortfallMinutes.ShouldBe(90);
        kun.LateMinutes.ShouldBe(0);
    }

    [Fact]
    public void F13_sababli_kunda_kam_ishlangan_yoq()
    {
        var kun = FlexibleOffice().DaysExcused(new HashSet<DateOnly> { Monday }, Monday).ShouldHaveSingleItem().Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Excused);
        kun.WorkedMinutes.ShouldBe(0);
        kun.ShortfallMinutes.ShouldBe(0);
    }

    /// <summary>Tungi erkin smena 20:00–08:00, norma 10 soat: bitta qator, dushanbada.</summary>
    [Fact]
    public void F14_tungi_erkin_smena_bitta_qator_boshlangan_sanada()
    {
        var sahna = FlexibleOffice("20:00", "08:00", breakMinutes: 0, breakStart: null, breakEnd: null, requiredMinutes: 600);
        sahna.In(Monday, "21:00");
        sahna.Out(Tuesday, "07:00");

        var kunlar = sahna.Days(Monday, Tuesday);
        var dushanba = kunlar[0].Facts;

        dushanba.Status.ShouldBe(AttendanceDayStatus.Present);
        dushanba.WorkedMinutes.ShouldBe(600);
        dushanba.ShortfallMinutes.ShouldBe(0);
        dushanba.PassCount.ShouldBe(2);
        kunlar[1].Facts.PassCount.ShouldBe(0, "07:00 dagi chiqish seshanbaga tushmaydi");

        DayPlan.For(FlexibleSchedule("20:00", "08:00", 0, null, null, 600), Monday)
            .LatestStartAt.ShouldBe(At(Monday, "22:00").ToUniversalTime());
    }

    /// <summary>
    /// Tungi erkin smena, norma 4 soat, hozir seshanba 01:00: eng kech kelish seshanba 04:00 — kun
    /// SANASI o'tgan bo'lsa ham hali boshlanmagan.
    /// </summary>
    [Fact]
    public void F15_tungi_smenada_eng_kech_kelish_ertangi_sanada()
    {
        var kun = FlexibleOffice("20:00", "08:00", breakMinutes: 0, breakStart: null, breakEnd: null, requiredMinutes: 240)
            .Now(Tuesday, "01:00")
            .Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Planned);
        kun.ShouldStore.ShouldBeFalse();
        kun.Facts.ShortfallMinutes.ShouldBe(0);

        var reja = DayPlan.For(FlexibleSchedule("20:00", "08:00", 0, null, null, 240), Monday);
        reja.LatestStartAt.ShouldBe(At(Tuesday, "04:00").ToUniversalTime());
        DashboardStatusRule.Classify(reja, null, Tuesday, At(Tuesday, "01:00"))
            .ShouldBe(new DashboardVerdict(AttendanceDashboardStatus.NotStarted, null));
    }

    /// <summary>Regressiya: F2 ning o'tishlari QAT'IY kunda — bugungi hisob o'zgarmagan.</summary>
    [Fact]
    public void F16_qatiy_kun_ozgarmagan()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("08:30", "18:00", 120, "12:00", "14:00"));
        sahna.Alternating(Monday, "10:00", "18:00");

        var kun = Hisob(sahna, closed: 480, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 360, shortfall: 0);
        kun.LateMinutes.ShouldBe(80, "10:00 − (08:30 + 10 daqiqa yon berish)");
        kun.EarlyLeaveMinutes.ShouldBe(0);
        kun.PlannedMinutes.ShouldBe(450);
        kun.IsFlexible.ShouldBeFalse();
    }

    // ────────────────────── Egasining qarori (2026-10-20) va chegaralar ──────────────────────

    /// <summary>Egasining misoli: 08:30–18:00, norma 7:30; 08:00 da keldi, 17:30 da ketdi — kam ishlangan yo'q.</summary>
    [Fact]
    public void Egasi_misoli_ertaroq_kelib_ertaroq_ketgan_normani_bajargan()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "08:00", "17:30");

        var kun = Hisob(sahna, closed: 570, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 450, shortfall: 0);
        kun.LateMinutes.ShouldBe(0);
        kun.EarlyLeaveMinutes.ShouldBe(0);
        kun.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Kech_kelib_kech_ketgan_normani_bajargan()
    {
        var sahna = FlexibleOffice();
        sahna.Alternating(Monday, "10:00", "19:30");

        Hisob(sahna, closed: 570, breakTaken: 0, presentInWindow: 120, deduction: 120, worked: 450, shortfall: 0);
    }

    /// <summary>Reja tugashidan oldin yozilgan qatorda kam ishlangan 0 — tungi qayta hisoblash to'ldiradi.</summary>
    [Fact]
    public void Reja_tugamaguncha_kam_ishlangan_yozilmaydi()
    {
        var sahna = FlexibleOffice().Now(Monday, "17:00");
        sahna.Alternating(Monday, "08:30", "12:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.WorkedMinutes.ShouldBe(210);
        kun.ShortfallMinutes.ShouldBe(0);
    }

    [Fact]
    public void Kelmagan_erkin_kunda_kam_ishlangan_yoq()
    {
        var kun = FlexibleOffice().Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.Absent);
        kun.ShortfallMinutes.ShouldBe(0, "kelmagan kun holat bilan sanaladi");
        kun.IsFlexible.ShouldBeTrue();
    }

    /// <summary>Tasdiqlangan erkin so'rov kuni qat'iy jadval ustida — o'sha sana erkin hisoblanadi.</summary>
    [Fact]
    public void Erkin_sorov_kuni_qatiy_jadval_ustida()
    {
        var sahna = Office().Override(Monday, "08:30", "18:00", isFlexible: true, breakStart: "12:00", breakEnd: "14:00");
        sahna.Alternating(Monday, "10:00", "18:00");

        var kunlar = sahna.Days(Monday, Tuesday);

        kunlar[0].Facts.IsFlexible.ShouldBeTrue();
        kunlar[0].Facts.LateMinutes.ShouldBe(0);
        kunlar[0].Facts.ShortfallMinutes.ShouldBe(90);
        kunlar[1].Facts.IsFlexible.ShouldBeFalse("faqat so'ralgan sana");
    }

    /// <summary>Qat'iy so'rov kuni erkin jadval ustida — kun butunligicha almashadi, erkinlik ulanmaydi.</summary>
    [Fact]
    public void Qatiy_sorov_kuni_erkin_jadval_ustida()
    {
        var sahna = FlexibleOffice().Override(Monday, "08:30", "18:00", "12:00", "14:00");
        sahna.Alternating(Monday, "10:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.IsFlexible.ShouldBeFalse();
        kun.LateMinutes.ShouldBe(80);
        kun.ShortfallMinutes.ShouldBe(0);
    }

    // ────────────────────── Yordamchilar ──────────────────────

    /// <summary>Sukut sahnaning dushanba rejasi.</summary>
    private static DayPlan Reja(int? requiredMinutes = null)
        => DayPlan.For(FlexibleSchedule(requiredMinutes: requiredMinutes), Monday);

    /// <summary>
    /// Dushanbani hisoblaydi va §2.3 jadvalining har bir ustunini tekshiradi: yopilgan yig'indi,
    /// breakTaken, presentInWindow, ushlanma, ishlangan, kam ishlangan.
    /// </summary>
    private static AttendanceDayFacts Hisob(
        AttendanceScenario sahna,
        int closed,
        int breakTaken,
        int presentInWindow,
        int deduction,
        int worked,
        int shortfall)
    {
        var juftlash = sahna.Pairing(Monday);
        juftlash.ClosedMinutes.ShouldBe(closed, "yopilgan oraliqlar yig'indisi");
        juftlash.PresentInWindowMinutes.ShouldBe(presentInWindow, "presentInWindow");

        var kun = sahna.Day(Monday).Facts;
        kun.BreakTakenMinutes.ShouldBe(breakTaken, "breakTaken");
        kun.BreakDeductionMinutes.ShouldBe(deduction, "ushlanma");
        kun.WorkedMinutes.ShouldBe(worked, "ishlangan");
        kun.ShortfallMinutes.ShouldBe(shortfall, "kam ishlangan");

        return kun;
    }
}
