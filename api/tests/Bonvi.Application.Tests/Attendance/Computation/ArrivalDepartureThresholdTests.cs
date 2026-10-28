using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Erta kelish va kech ketish chegarasi — egasining qarori (2026-10-28).
/// </summary>
/// <remarks>
/// <para>
/// Sukut sahna: 09:00–18:00, tanaffussiz, erta kelish chegarasi 30, kech ketish chegarasi 60.
/// Reja boshlanishidan oldingi 30 daqiqa va tugashidan keyingi 60 daqiqa SANALMAYDI; undan
/// tashqaridagisi sanaladi — chegaraning o'zi oshgach ham sanalmaydi («Faqat chegaradan
/// keyingisi»). Sanalmagan vaqt ishlangan vaqtdan ham chiqadi, ortiqcha ish esa avvalgidek
/// <c>max(0, ishlangan − reja)</c>.
/// </para>
/// <para>
/// Kelish, ketish, kechikish va erta ketish chegaradan bexabar — ular o'zgarmaydi.
/// </para>
/// </remarks>
public sealed class ArrivalDepartureThresholdTests
{
    private const int Erta = 30;
    private const int Kech = 60;

    private static AttendanceScenario Sahna(int erta = Erta, int kech = Kech, int breakMinutes = 0, string? breakStart = null, string? breakEnd = null)
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", breakMinutes, breakStart, breakEnd, earlyThreshold: erta, lateThreshold: kech));
        return sahna;
    }

    /// <summary>Egasining misoli: 08:40 da keldi — chegara ichida, erta kelgani sanalmaydi.</summary>
    [Fact]
    public void Chegara_ichida_erta_kelgan_vaqt_sanalmaydi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "08:40", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(540);
        kun.OvertimeMinutes.ShouldBe(0);
        kun.FirstInAt.ShouldBe(At(Monday, "08:40").ToUniversalTime(), "kelish lahzasi o'zgarmaydi");
    }

    /// <summary>Egasining misoli: 08:00 da keldi — faqat chegaradan oldingisi (08:00–08:30) sanaladi.</summary>
    [Fact]
    public void Chegaradan_oldin_kelgan_faqat_chegaradan_tashqarisi_sanaladi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "08:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(570);
        kun.OvertimeMinutes.ShouldBe(30);
    }

    /// <summary>Egasining misoli: 18:50 da ketdi — chegara ichida, kech qolgani sanalmaydi.</summary>
    [Fact]
    public void Chegara_ichida_kech_ketgan_vaqt_sanalmaydi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "09:00", "18:50");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(540);
        kun.OvertimeMinutes.ShouldBe(0);
        kun.LastOutAt.ShouldBe(At(Monday, "18:50").ToUniversalTime(), "ketish lahzasi o'zgarmaydi");
    }

    /// <summary>Egasining misoli: 19:30 da ketdi — faqat 19:00–19:30 sanaladi.</summary>
    [Fact]
    public void Chegaradan_keyin_ketgan_faqat_chegaradan_tashqarisi_sanaladi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "09:00", "19:30");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(570);
        kun.OvertimeMinutes.ShouldBe(30);
    }

    [Fact]
    public void Ikkala_chegara_birga_kesiladi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "08:00", "19:30");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(600);
        kun.PlannedMinutes.ShouldBe(540);
        kun.OvertimeMinutes.ShouldBe(60);
    }

    /// <summary>Chegara 0 (sukut, mavjud jadvallar) — avvalgidek hamma borliq sanaladi.</summary>
    [Fact]
    public void Chegara_nol_bo_lsa_hammasi_sanaladi()
    {
        var sahna = Sahna(erta: 0, kech: 0);
        sahna.Alternating(Monday, "08:00", "19:30");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(690);
        kun.OvertimeMinutes.ShouldBe(150);
    }

    /// <summary>Faqat bir tomon berilgan: erta kelish chegarasi 0, kech ketishniki 60.</summary>
    [Fact]
    public void Faqat_bir_tomon_chegarasi_berilgan()
    {
        var sahna = Sahna(erta: 0, kech: 60);
        sahna.Alternating(Monday, "08:00", "19:30");

        sahna.Day(Monday).Facts.WorkedMinutes.ShouldBe(630, "60 (08:00–09:00) + 540 + 30 (19:00–19:30)");
    }

    /// <summary>Chegara ichida chiqib-kirgani ham sanalmaydi: 08:35–08:50 va 08:55–09:00 butunlay chegarada.</summary>
    [Fact]
    public void Chegara_ichidagi_bir_necha_oraliq_sanalmaydi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "08:35", "08:50", "08:55", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(540);
        kun.FirstInAt.ShouldBe(At(Monday, "08:35").ToUniversalTime());
    }

    /// <summary>Tanaffus oynasi smena ichida — chegara uning hisobiga tegmaydi.</summary>
    [Fact]
    public void Tanaffus_hisobi_chegaradan_bexabar()
    {
        var sahna = Sahna(breakMinutes: 60, breakStart: "12:00", breakEnd: "13:00");
        sahna.Alternating(Monday, "08:00", "19:30");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(540, "600 sanalgan − 60 tanaffus");
        kun.PlannedMinutes.ShouldBe(480);
        kun.OvertimeMinutes.ShouldBe(60);
    }

    /// <summary>Kechikish va erta ketish chegaraga bog'liq emas — chegarasiz jadvaldagidek.</summary>
    [Fact]
    public void Kechikish_va_erta_ketish_o_zgarmaydi()
    {
        var chegarali = Sahna();
        chegarali.Alternating(Monday, "09:25", "17:30");
        var chegarasiz = Sahna(erta: 0, kech: 0);
        chegarasiz.Alternating(Monday, "09:25", "17:30");

        var kun = chegarali.Day(Monday).Facts;
        var avval = chegarasiz.Day(Monday).Facts;

        kun.LateMinutes.ShouldBe(avval.LateMinutes);
        kun.EarlyLeaveMinutes.ShouldBe(avval.EarlyLeaveMinutes);
        kun.WorkedMinutes.ShouldBe(avval.WorkedMinutes, "borliq butunlay smena ichida");
        kun.ShortfallMinutes.ShouldBe(avval.ShortfallMinutes);
    }

    /// <summary>
    /// Tungi smena 20:00–08:00: dushanba 19:00 → seshanba 09:30. Sanaladi 19:00–19:30 (30),
    /// 20:00–08:00 (720) va 09:00–09:30 (30).
    /// </summary>
    [Fact]
    public void Tungi_smenada_chegara_yarim_tundan_o_tadi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00", earlyThreshold: Erta, lateThreshold: Kech));
        sahna.In(Monday, "19:00");
        sahna.Out(Tuesday, "09:30");

        var dushanba = sahna.Days(Monday, Tuesday)[0].Facts;

        dushanba.WorkedMinutes.ShouldBe(780);
        dushanba.OvertimeMinutes.ShouldBe(60);
    }

    /// <summary>Erkin kunda borliq oynaga kesilmaydi (2026-10-20) — chegara ham qo'llanmaydi.</summary>
    [Fact]
    public void Erkin_kunda_chegara_qo_llanmaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(FlexibleSchedule(earlyThreshold: Erta, lateThreshold: Kech));
        sahna.Alternating(Monday, "09:00", "19:30");

        sahna.Day(Monday).Facts.WorkedMinutes.ShouldBe(510, "FlexibleDayTests.F5 dagidek: 630 − 120");
    }

    /// <summary>Dam olish kunida reja yo'q — u yerdagi ish to'liq sanaladi.</summary>
    [Fact]
    public void Dam_olish_kunida_chegara_qo_llanmaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", daysOff: [1], earlyThreshold: Erta, lateThreshold: Kech));
        sahna.Alternating(Monday, "08:00", "19:30");

        sahna.Day(Monday).Facts.WorkedMinutes.ShouldBe(690);
    }

    /// <summary>
    /// Tasdiqlangan jadval so'rovi kunida chegara jadvalning o'zidan olinadi va SO'ROVDAGI
    /// smenaga qo'yiladi: 10:00–19:00, 09:00 → 20:30 — 09:00–09:30, 10:00–19:00, 20:00–20:30.
    /// </summary>
    [Fact]
    public void Jadval_so_rovi_kunida_chegara_so_rovdagi_smenaga_qo_yiladi()
    {
        var sahna = Sahna();
        sahna.Override(Monday, "10:00", "19:00");
        sahna.Alternating(Monday, "09:00", "20:30");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(600);
        kun.OvertimeMinutes.ShouldBe(60);
    }

    /// <summary>«Ish vaqti» va shaxsiy vaqt kesilgan oraliqlar bilan ishlaydi.</summary>
    [Fact]
    public void Juftlash_kesilgan_oraliqlarni_qaytaradi()
    {
        var sahna = Sahna();
        sahna.Alternating(Monday, "08:00", "19:30");

        var juftlash = sahna.Pairing(Monday);

        juftlash.ClosedMinutes.ShouldBe(600);
        juftlash.ClosedIntervals.ShouldNotBeNull().ShouldBe(
        [
            (At(Monday, "08:00").ToUniversalTime(), At(Monday, "08:30").ToUniversalTime()),
            (At(Monday, "09:00").ToUniversalTime(), At(Monday, "18:00").ToUniversalTime()),
            (At(Monday, "19:00").ToUniversalTime(), At(Monday, "19:30").ToUniversalTime()),
        ]);
    }
}
