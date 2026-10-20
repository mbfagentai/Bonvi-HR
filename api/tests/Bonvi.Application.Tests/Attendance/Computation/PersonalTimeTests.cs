using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Shaxsiy vaqt — SPEC-verifikatsiya-sorov-tahriri §5.8 ning har bir misoli (E1–E21) alohida test.
/// </summary>
/// <remarks>
/// <para>
/// Sukut sahna (§5.8): qat'iy kun, dushanba, smena 08:30–17:30, tanaffus 60 daqiqa 12:00–15:00 oynasida,
/// yon berish 10/10, reja 480; «hozir» — uzoq kelajak (har bir kun o'tgan), so'rovlar tasdiqlangan.
/// </para>
/// <para>
/// Egasining misoli — E1: 08:25 da keldi, 17:35 da ketdi, shaxsiy 14:00–16:00 → ishladi 6:00, shaxsiy
/// 2:00, kechikish ham, erta ketish ham yo'q.
/// </para>
/// </remarks>
public sealed class PersonalTimeTests
{
    private static readonly DateOnly Sunday = Monday.AddDays(6);

    /// <summary>§5.8 ning sukut sahnasi.</summary>
    private static AttendanceScenario Base()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("08:30", "17:30", 60, "12:00", "15:00"));
        return sahna;
    }

    /// <summary>Sukut sahnaning dushanba rejasi — sof qoidalarni to'g'ridan-to'g'ri sinash uchun.</summary>
    private static DayPlan BasePlan() => DayPlan.For(Schedule("08:30", "17:30", 60, "12:00", "15:00"), Monday);

    private static void Sonlar(
        ComputedAttendanceDay kun,
        int worked,
        int personal,
        int late,
        int early,
        int taken,
        int deducted,
        AttendanceDayStatus status)
    {
        kun.Facts.WorkedMinutes.ShouldBe(worked, "ishlangan");
        kun.Facts.PersonalMinutes.ShouldBe(personal, "shaxsiy");
        kun.Facts.LateMinutes.ShouldBe(late, "kechikish");
        kun.Facts.EarlyLeaveMinutes.ShouldBe(early, "erta ketish");
        kun.Facts.BreakTakenMinutes.ShouldBe(taken, "olingan tanaffus");
        kun.Facts.BreakDeductionMinutes.ShouldBe(deducted, "ushlangan tanaffus");
        kun.Facts.Status.ShouldBe(status);
    }

    [Fact]
    public void E1_egasining_misoli_ishladi_6_00_shaxsiy_2_00()
    {
        var sahna = Base().PersonalTime(Monday, "14:00", "16:00");
        sahna.Alternating(Monday, "08:25", "17:35");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, worked: 360, personal: 120, late: 0, early: 0, taken: 0, deducted: 60, AttendanceDayStatus.Present);
        kun.Facts.PlannedMinutes.ShouldBe(480);
        kun.Facts.OvertimeMinutes.ShouldBe(0, "reja tashqarisidagi 10 daqiqa shaxsiy vaqtni «sotib olmaydi»");
        kun.Facts.CreditedMinutes.ShouldBe(0);
        kun.Facts.FirstInAt.ShouldBe(At(Monday, "08:25").ToUniversalTime());
        kun.Facts.LastOutAt.ShouldBe(At(Monday, "17:35").ToUniversalTime());
        kun.ShouldStore.ShouldBeTrue();
    }

    [Fact]
    public void E2_oraliqda_chiqib_kirgani_hech_narsani_ozgartirmaydi()
    {
        var sahna = Base().PersonalTime(Monday, "14:00", "16:00");
        sahna.Alternating(Monday, "08:25", "14:00", "16:00", "17:35");

        Sonlar(sahna.DayCredited(Monday), 360, 120, 0, 0, 0, 60, AttendanceDayStatus.Present);
    }

    [Fact]
    public void E3_erta_qaytgan_vaqt_shaxsiy_oraliqda_olib_tashlanadi()
    {
        var sahna = Base().PersonalTime(Monday, "14:00", "16:00");
        sahna.Alternating(Monday, "08:30", "13:55", "15:10", "17:30");

        // 15:10–16:00 shaxsiy oraliqda — olib tashlanadi; 13:55–14:00 bo'shlig'i — 5 daqiqalik tanaffus.
        Sonlar(sahna.DayCredited(Monday), 360, 120, 0, 0, 5, 55, AttendanceDayStatus.Present);
    }

    [Fact]
    public void E4_ertalabki_shaxsiy_vaqt_kutilgan_kelishni_suradi()
    {
        var sahna = Base().PersonalTime(Monday, "08:30", "10:00");
        sahna.Alternating(Monday, "10:05", "17:30");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 385, 90, 0, 0, 0, 60, AttendanceDayStatus.Present);
        kun.Personal.ShouldNotBeNull().ExpectedStartAt.ShouldBe(At(Monday, "10:00").ToUniversalTime());
    }

    [Fact]
    public void E5_kechikish_oraliq_oxiri_va_yon_berishdan_sanaladi()
    {
        var sahna = Base().PersonalTime(Monday, "08:30", "10:00");
        sahna.Alternating(Monday, "10:30", "17:30");

        Sonlar(sahna.DayCredited(Monday), 360, 90, 20, 0, 0, 60, AttendanceDayStatus.Present);
    }

    [Fact]
    public void E6_kechki_shaxsiy_vaqt_kutilgan_ketishni_suradi()
    {
        var sahna = Base().PersonalTime(Monday, "16:00", "17:30");
        sahna.Alternating(Monday, "08:30", "16:00");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 390, 90, 0, 0, 0, 60, AttendanceDayStatus.Present);
        kun.Personal.ShouldNotBeNull().ExpectedEndAt.ShouldBe(At(Monday, "16:00").ToUniversalTime());
    }

    [Fact]
    public void E7_butun_tanaffus_oynasi_shaxsiy_tushlik_ichida_deb_olinadi()
    {
        var sahna = Base().PersonalTime(Monday, "12:00", "15:00");
        sahna.Alternating(Monday, "08:30", "12:00", "15:00", "17:30");

        // Oyna to'liq shaxsiy: 60 daqiqalik tushlik shaxsiy vaqt ichida — 180 − 60.
        Sonlar(sahna.DayCredited(Monday), 360, 120, 0, 0, 0, 0, AttendanceDayStatus.Present);
    }

    [Fact]
    public void E8_kun_ortasidagi_shaxsiy_vaqt_kechikishdan_ayriladi()
    {
        var sahna = Base().PersonalTime(Monday, "10:00", "12:00");
        sahna.Alternating(Monday, "11:00", "17:30");

        // Kechikish = 08:40–10:00; 11:00–12:00 shaxsiy oraliqda — olib tashlanadi.
        Sonlar(sahna.DayCredited(Monday), 270, 120, 80, 0, 0, 60, AttendanceDayStatus.Present);
    }

    [Fact]
    public void E9_qisman_shaxsiy_vaqt_bilan_kelmagan_kun_Kelmadi()
    {
        var sahna = Base().PersonalTime(Monday, "14:00", "16:00");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 0, 120, 0, 0, 0, 0, AttendanceDayStatus.Absent);
        kun.ShouldStore.ShouldBeTrue();
    }

    [Fact]
    public void E10_butun_reja_shaxsiy_bolsa_kelmagan_kun_Sababli()
    {
        var sahna = Base().PersonalTime(Monday, "08:00", "18:00");

        var kun = sahna.DayCredited(Monday);

        // Rejaga kesilgan 540 − tushlik 60.
        Sonlar(kun, 0, 480, 0, 0, 0, 0, AttendanceDayStatus.Excused);
        kun.Personal.ShouldNotBeNull().ExcusesDay.ShouldBeTrue();
    }

    [Fact]
    public void E11_shaxsiy_vaqt_tushlik_soati_bolsa_hech_narsa_turmaydi()
    {
        var sahna = Office(breakMinutes: 60, breakStart: "13:00", breakEnd: "14:00").PersonalTime(Monday, "13:00", "14:00");
        sahna.Alternating(Monday, "09:00", "13:00", "14:00", "18:00");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 480, 0, 0, 0, 0, 0, AttendanceDayStatus.Present);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void E12_ertalabki_shaxsiy_vaqt_tugaguncha_Hali_boshlanmagan_keyin_Kelmadi()
    {
        var erta = Base().PersonalTime(Monday, "08:30", "10:00").Now(Monday, "09:00");

        var kun = erta.DayCredited(Monday);
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Planned);
        kun.ShouldStore.ShouldBeFalse("hali boshlanmagan kun uchun qator yo'q");
        kun.Personal.ShouldNotBeNull().NotStartedUntil.ShouldBe(At(Monday, "10:00").ToUniversalTime());

        var kech = Base().PersonalTime(Monday, "08:30", "10:00").Now(Monday, "10:01");

        var keyin = kech.DayCredited(Monday);
        keyin.Facts.Status.ShouldBe(AttendanceDayStatus.Absent);
        keyin.ShouldStore.ShouldBeTrue();
    }

    [Fact]
    public void E13_erkin_kunda_shaxsiy_vaqt_cheklanmaydi_kam_ishlangan_yoq()
    {
        var sahna = FlexibleOffice().PersonalTime(Monday, "14:00", "16:00");
        sahna.Alternating(Monday, "08:30", "14:00", "16:00", "19:00");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 390, 120, 0, 0, 0, 120, AttendanceDayStatus.Present);
        kun.Facts.PlannedMinutes.ShouldBe(450);
        kun.Facts.ShortfallMinutes.ShouldBe(0, "450 − 120 − 390 < 0");
        kun.Personal.ShouldNotBeNull().NotStartedUntil.ShouldBe(At(Monday, "12:30").ToUniversalTime());
    }

    [Fact]
    public void E14_erkin_kunda_kam_ishlangan_shaxsiy_vaqtni_hisobga_oladi()
    {
        var sahna = FlexibleOffice().PersonalTime(Monday, "14:00", "16:00");
        sahna.Alternating(Monday, "08:30", "14:00", "16:00", "17:00");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 270, 120, 0, 0, 0, 120, AttendanceDayStatus.Present);
        kun.Facts.ShortfallMinutes.ShouldBe(60, "shaxsiy tuzatishsiz 180 bo'lardi");
    }

    [Fact]
    public void E15_tungi_smenada_kredit_shaxsiy_vaqtni_qoplamaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00", 60, "00:30", "01:30"));
        sahna.WorkCredit(Monday);
        sahna.PersonalTime(Tuesday, "02:00", "03:00");

        var kunlar = sahna.DaysCredited(Monday, Tuesday);
        var dushanba = kunlar[0];

        Sonlar(dushanba, 600, 60, 0, 0, 0, 60, AttendanceDayStatus.Present);
        dushanba.Facts.CreditedMinutes.ShouldBe(600);
        dushanba.Facts.PlannedMinutes.ShouldBe(660);
        dushanba.Facts.OvertimeMinutes.ShouldBe(0);
        dushanba.CreditedFromAt.ShouldBe(At(Monday, "20:00").ToUniversalTime());

        kunlar[1].Facts.PersonalMinutes.ShouldBe(0, "shaxsiy oraliq faqat dushanba smenasiga tushadi");
    }

    [Fact]
    public void E16_bekor_qilingan_sorovdan_keyin_bugungi_sonlar()
    {
        // E1 ning o'tishlari — so'rov bekor qilingan (yuklovchi uni bermaydi).
        var sahna = Base();
        sahna.Alternating(Monday, "08:25", "17:35");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 490, 0, 0, 0, 0, 60, AttendanceDayStatus.Present);
        kun.Facts.OvertimeMinutes.ShouldBe(10);
        kun.Personal.ShouldBeNull();
    }

    [Fact]
    public void E17_Sorovsiz_kun_aynan_avvalgidek()
    {
        var sahna = Base();
        sahna.Alternating(Monday, "08:25", "17:35");

        var soroviBilan = sahna.DayCredited(Monday);
        var sorovsiz = sahna.Day(Monday);

        soroviBilan.Facts.ShouldBe(sorovsiz.Facts, "yozuv tengligi — har bir maydon");
        soroviBilan.Facts.PersonalMinutes.ShouldBe(0);
        soroviBilan.Personal.ShouldBeNull();
    }

    [Fact]
    public void E18_dam_olish_kunida_shaxsiy_vaqt_tasir_qilmaydi()
    {
        static AttendanceScenario Sahna()
        {
            var sahna = new AttendanceScenario();
            sahna.Employ();
            sahna.Assign(Schedule("08:30", "17:30", 60, "12:00", "15:00", daysOff: [7]));
            sahna.Alternating(Sunday, "10:00", "12:00");
            return sahna.PersonalTime(Sunday, "10:00", "11:00");
        }

        var sahna = Sahna();
        var bilan = sahna.DayCredited(Sunday);
        var usiz = sahna.Day(Sunday);

        bilan.Facts.ShouldBe(usiz.Facts);
        bilan.Facts.Status.ShouldBe(AttendanceDayStatus.DayOffWorked);
        bilan.Facts.PersonalMinutes.ShouldBe(0);
        bilan.Personal.ShouldBeNull("rejasiz kunda P bo'sh");
    }

    [Fact]
    public void E19_reja_oxiridan_oshgan_shaxsiy_vaqt_rejaga_kesiladi()
    {
        var sahna = Base().PersonalTime(Monday, "17:00", "19:00");
        sahna.Alternating(Monday, "08:30", "17:00");

        Sonlar(sahna.DayCredited(Monday), 450, 30, 0, 0, 0, 60, AttendanceDayStatus.Present);
    }

    [Fact]
    public void E20_oynasiz_jadvalda_chegara_tushlikni_ham_ushlaydi()
    {
        var sahna = Office(breakMinutes: 60).PersonalTime(Monday, "13:00", "14:00");
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 420, 60, 0, 0, 0, 0, AttendanceDayStatus.Present);
        kun.Facts.OvertimeMinutes.ShouldBe(0);

        // So'rovsiz: oynasiz kunda ushlanma yo'q — 540, ortiqcha ish 60.
        var sorovsiz = Office(breakMinutes: 60);
        sorovsiz.Alternating(Monday, "09:00", "18:00");
        sorovsiz.DayCredited(Monday).Facts.WorkedMinutes.ShouldBe(540);
        sorovsiz.DayCredited(Monday).Facts.OvertimeMinutes.ShouldBe(60);
    }

    [Fact]
    public void E21_erkin_kunda_shaxsiy_vaqt_normaga_teng_bolsa_Sababli()
    {
        var sahna = FlexibleOffice(breakMinutes: 0, breakStart: null, breakEnd: null, requiredMinutes: 240)
            .PersonalTime(Monday, "08:30", "12:30");

        var kun = sahna.DayCredited(Monday);

        Sonlar(kun, 0, 240, 0, 0, 0, 0, AttendanceDayStatus.Excused);
        kun.Facts.ShortfallMinutes.ShouldBe(0);
    }

    // ────────────────────── Qo'shimcha qoidalar (§8.2) ──────────────────────

    [Fact]
    public void Shaxsiy_sorov_ish_vaqtiga_qoshilmaydi()
    {
        // O'tishsiz kun, faqat shaxsiy so'rov — kredit hech qachon paydo bo'lmaydi.
        var sahna = Base().PersonalTime(Monday, "09:00", "12:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.CreditedMinutes.ShouldBe(0);
        kun.Facts.WorkedMinutes.ShouldBe(0);
        kun.CreditedFromAt.ShouldBeNull();

        WorkCreditRule.IntervalsFor(
                Monday,
                BasePlan(),
                [new WorkCredit(Monday, Monday, Time("09:00"), Time("12:00"), CountsAsWorkTime: false)],
                DateTimeOffset.MaxValue)
            .ShouldBeEmpty("shaxsiy tur kredit oralig'i bermaydi");
    }

    [Fact]
    public void Rejadan_tashqaridagi_shaxsiy_vaqt_kunni_ozgartirmaydi()
    {
        var sahna = Base().PersonalTime(Monday, "18:00", "20:00");
        sahna.Alternating(Monday, "08:25", "17:35");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.ShouldBe(sahna.Day(Monday).Facts, "so'rovsiz hisob bilan har bir maydon teng");
        kun.Personal.ShouldBeNull();
    }

    [Fact]
    public void Ish_vaqti_sorovi_bilan_shaxsiy_vaqt_bir_kunda()
    {
        // 08:30–10:00 «Ishxona ishi bilan», 14:00–16:00 shaxsiy; 10:00 da keldi, 17:30 da ketdi.
        var sahna = Base()
            .WorkCredit(Monday, fromTime: "08:30", toTime: "10:00")
            .PersonalTime(Monday, "14:00", "16:00");
        sahna.Alternating(Monday, "10:00", "17:30");

        var kun = sahna.DayCredited(Monday);

        // O'tishlar: 10:00–14:00 + 16:00–17:30 = 330 − 60 = 270; kredit 90 (xona 480 − 120 − 270 = 90).
        kun.Facts.WorkedMinutes.ShouldBe(360);
        kun.Facts.CreditedMinutes.ShouldBe(90);
        kun.Facts.PersonalMinutes.ShouldBe(120);
        kun.Facts.LateMinutes.ShouldBe(0, "ertalab so'rov bo'yicha «borliq»");
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Shaxsiy_oraliqlar_rejaga_kesiladi_va_birlashtiriladi()
    {
        var oraliqlar = PersonalTimeRule.IntervalsFor(
            Monday,
            BasePlan(),
            [
                new WorkCredit(Monday, Monday, Time("07:00"), Time("09:00"), CountsAsWorkTime: false),
                new WorkCredit(Monday, Monday, Time("09:00"), Time("10:00"), CountsAsWorkTime: false),
                new WorkCredit(Monday, Monday, Time("11:00"), Time("12:00"), CountsAsWorkTime: true),
                new WorkCredit(Monday, Monday, null, null, CountsAsWorkTime: false),
            ]);

        oraliqlar.ShouldHaveSingleItem()
            .ShouldBe((At(Monday, "08:30").ToUniversalTime(), At(Monday, "10:00").ToUniversalTime()));
    }
}
