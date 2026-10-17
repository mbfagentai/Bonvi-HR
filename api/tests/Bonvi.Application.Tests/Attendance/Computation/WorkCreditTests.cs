using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// «Ish vaqti hisoblanadigan» tasdiqlangan so'rov — SPEC-so'rovlar §B3 AMENDED 2026-10-17.
/// </summary>
/// <remarks>
/// Egasining misoli (Verifix skrinshoti): 12:33 da chiqib, boshqa zavodga borib, 14:22 da
/// qaytgan odam «Ишхона иши билан» so'rovini yozadi — shu vaqt ISHLANGAN bo'lishi kerak.
/// Sukut sahna — ofis 09:00–18:00, yon berish 10/10, tanaffussiz.
/// </remarks>
public sealed class WorkCreditTests
{
    private static readonly DateOnly Sunday = Monday.AddDays(6);

    [Fact]
    public void Butun_kunlik_sorov_otishsiz_kunni_ishlagan_qiladi()
    {
        var sahna = Office().WorkCredit(Monday);

        var kun = sahna.DayCredited(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.Facts.WorkedMinutes.ShouldBe(540);
        kun.Facts.CreditedMinutes.ShouldBe(540);
        kun.Facts.LateMinutes.ShouldBe(0);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
        kun.Facts.FirstInAt.ShouldBeNull("o'tish yo'q — terminal yozmagan vaqt «kirish» bo'lib chiqmaydi");
        kun.ShouldStore.ShouldBeTrue();
    }

    [Fact]
    public void Butun_kunlik_sorovda_tanaffus_baribir_ushlanadi()
    {
        var sahna = Office(breakMinutes: 60, breakStart: "13:00", breakEnd: "14:00").WorkCredit(Monday);

        var kun = sahna.DayCredited(Monday);

        kun.Facts.WorkedMinutes.ShouldBe(480);
        kun.Facts.PlannedMinutes.ShouldBe(480);
        kun.Facts.BreakDeductionMinutes.ShouldBe(60);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Ertalabki_sorov_kechikishni_yopadi()
    {
        var sahna = Office().WorkCredit(Monday, fromTime: "09:00", toTime: "10:30");
        sahna.Alternating(Monday, "10:35", "18:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.Facts.LateMinutes.ShouldBe(0);
        kun.Facts.CreditedMinutes.ShouldBe(90);
        kun.Facts.WorkedMinutes.ShouldBe(90 + 445);
        kun.Facts.FirstInAt.ShouldBe(At(Monday, "10:35").ToUniversalTime());
        kun.CreditedFromAt.ShouldBe(At(Monday, "09:00").ToUniversalTime());
    }

    [Fact]
    public void Kun_ortasidagi_bosh_vaqt_sorov_bilan_toladi()
    {
        var sahna = Office().WorkCredit(Monday, fromTime: "12:33", toTime: "14:22");
        sahna.Alternating(Monday, "09:00", "12:33", "14:22", "18:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.WorkedMinutes.ShouldBe(540);
        kun.Facts.CreditedMinutes.ShouldBe(109);
    }

    [Fact]
    public void Sorovsiz_kun_avvalgidek()
    {
        var sahna = Office();
        sahna.Alternating(Monday, "09:00", "12:33", "14:22", "18:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.WorkedMinutes.ShouldBe(431);
        kun.Facts.CreditedMinutes.ShouldBe(0);
        kun.CreditedFromAt.ShouldBeNull();
    }

    [Fact]
    public void Smenadan_tashqaridagi_sorov_ortiqcha_ish_yasamaydi()
    {
        var sahna = Office().WorkCredit(Monday, fromTime: "18:00", toTime: "21:00");
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.WorkedMinutes.ShouldBe(540);
        kun.Facts.CreditedMinutes.ShouldBe(0);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Binoda_bolgan_vaqtga_tushgan_sorov_hech_narsa_qoshmaydi()
    {
        var sahna = Office().WorkCredit(Monday, fromTime: "11:00", toTime: "12:00");
        sahna.Alternating(Monday, "09:00", "18:00");

        sahna.DayCredited(Monday).Facts.CreditedMinutes.ShouldBe(0);
    }

    [Fact]
    public void Tanaffus_oynasiz_jadvalda_sorov_rejadan_oshirmaydi()
    {
        // Tanaffus 60 daqiqa, lekin oyna yo'q — reja 480, smena 540.
        var sahna = Office(breakMinutes: 60).WorkCredit(Monday);

        var kun = sahna.DayCredited(Monday);

        kun.Facts.WorkedMinutes.ShouldBe(480);
        kun.Facts.CreditedMinutes.ShouldBe(480);
        kun.Facts.OvertimeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Kelajakdagi_kun_hali_sababli_bolib_turadi()
    {
        var sahna = Office().Now(Monday, "08:00").WorkCredit(Monday);

        var kun = sahna.DayCredited(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Excused);
        kun.Facts.CreditedMinutes.ShouldBe(0);
    }

    [Fact]
    public void Bugungi_sorov_faqat_hozirgacha_hisoblanadi()
    {
        var sahna = Office().Now(Monday, "11:00").WorkCredit(Monday);

        var kun = sahna.DayCredited(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.Facts.CreditedMinutes.ShouldBe(120);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0, "reja hali tugamagan");
    }

    [Fact]
    public void Chiqishi_yozilmagan_kunda_erta_ketish_sanalmaydi()
    {
        var sahna = Office().WorkCredit(Monday, fromTime: "09:00", toTime: "12:00");
        sahna.In(Monday, "12:05");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.MissingExit);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
        kun.Facts.CreditedMinutes.ShouldBe(180);
    }

    [Fact]
    public void Sorovdan_keyin_ketgan_odamning_erta_ketishi_sorov_oxiridan()
    {
        var sahna = Office().WorkCredit(Monday, fromTime: "09:00", toTime: "15:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(170);
    }

    [Fact]
    public void Dam_olish_kunidagi_sorov_ishlangan_vaqt_bermaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", daysOff: [7]));
        sahna.WorkCredit(Sunday);

        var kun = sahna.DayCredited(Sunday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.DayOff);
        kun.Facts.CreditedMinutes.ShouldBe(0);
        kun.ShouldStore.ShouldBeFalse();
    }

    [Fact]
    public void Tungi_smenaga_ertangi_sanadagi_vaqtli_sorov_tushadi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00"));
        sahna.WorkCredit(Tuesday, fromTime: "02:00", toTime: "04:00");
        sahna.Alternating(Monday, "20:00", "23:59");
        sahna.Pass(Tuesday, "04:00", PassDirection.In);
        sahna.Pass(Tuesday, "08:00", PassDirection.Out);

        var kun = sahna.DayCredited(Monday);

        kun.Facts.CreditedMinutes.ShouldBe(120);
    }

    [Fact]
    public void Rejadan_kop_ishlagan_kunga_sorov_hech_narsa_qoshmaydi()
    {
        // 09:00–12:00 va 13:00–20:00 — 600 daqiqa, reja 540; bo'shliq so'rov bilan yopilgan.
        var sahna = Office().WorkCredit(Monday, fromTime: "12:00", toTime: "13:00");
        sahna.Alternating(Monday, "09:00", "12:00", "13:00", "20:00");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.CreditedMinutes.ShouldBe(0);
        kun.Facts.WorkedMinutes.ShouldBe(600);
        kun.Facts.OvertimeMinutes.ShouldBe(60, "ortiqcha ish faqat terminaldan — o'zgarmaydi");
    }

    [Fact]
    public void Tanaffus_oynasini_qisman_qamragan_kunda_tanaffus_bir_marta_ushlanadi()
    {
        // 09:00–13:30 binoda, 13:30–18:00 boshqa zavodda; tanaffus 60, oyna 13:00–14:00.
        var sahna = Office(breakMinutes: 60, breakStart: "13:00", breakEnd: "14:00")
            .WorkCredit(Monday, fromTime: "13:30", toTime: "18:00");
        sahna.Alternating(Monday, "09:00", "13:30");

        var kun = sahna.DayCredited(Monday);

        kun.Facts.WorkedMinutes.ShouldBe(480, "reja: 540 − 60");
        kun.Facts.CreditedMinutes.ShouldBe(240, "o'tishlardan 270 − 30 = 240, qolgani so'rovdan");
        kun.Facts.BreakDeductionMinutes.ShouldBe(60);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
        kun.Facts.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Sorov_yuklanadigan_sanalar_bir_kun_keyinni_ham_oladi()
    {
        WorkCreditRule.RequestDatesNeeded(Monday, Tuesday).ShouldBe((Monday, Tuesday.AddDays(1)));
    }
}
