using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Tanaffus ushlanmasi — SPEC §5.5.1, §5.7 (tanaffus bloki), §11.1 #31, #51–#64.
/// </summary>
/// <remarks>
/// <para>
/// Sukut sahna: 09:00–18:00, <c>break_minutes = 60</c>, oyna 13:00–14:00.
/// </para>
/// <para>
/// ⚠️ Formula oynadagi BORLIQ bilan cheklangan (§13 #14 hal qilingan):
/// <c>max(0, min(break − taken, presentInWindow))</c>. SPEC ning oldingi
/// tahriridagi «12:55 da uyga ketdi ⇒ 60 ushlanadi» qatori shu qaror bilan
/// 0 ga aylangan — testlar eski qiymatga «tuzatilmasin».
/// </para>
/// </remarks>
public sealed class BreakDeductionTests
{
    private static AttendanceScenario Lunch(int breakMinutes = 60)
        => Office(breakMinutes, "13:00", "14:00");

    // ────────────────────── Oyna yo'q ──────────────────────

    /// <summary>
    /// #31. Oynasiz jadvalda tanaffus UMUMAN ayrilmaydi — rejadan esa ayriladi.
    /// Tuzatishdan oldin yaratilgan har bir jadval aynan shunday ishlashda davom etadi.
    /// </summary>
    [Fact]
    public void Oyna_yoq_bolsa_tanaffus_ishlangan_vaqtdan_ayrilmaydi()
    {
        var sahna = Office(breakMinutes: 60);
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday);

        kun.HasBreakWindow.ShouldBeFalse();
        kun.Facts.WorkedMinutes.ShouldBe(540);
        kun.Facts.PlannedMinutes.ShouldBe(480);
        kun.Facts.LateMinutes.ShouldBe(0);
        kun.Facts.EarlyLeaveMinutes.ShouldBe(0);
        kun.Facts.OvertimeMinutes.ShouldBe(60);
        kun.Facts.BreakTakenMinutes.ShouldBe(0);
        kun.Facts.BreakDeductionMinutes.ShouldBe(0);
    }

    // ────────────────────── Tuzatishning butun ma'nosi ──────────────────────

    /// <summary>
    /// #51 + #52. Binodan chiqmagan va tushlikka aynan oyna bo'yicha chiqqan odam
    /// BIR XIL ishlagan vaqt oladi. Bitta testda — o'quvchi ikki sonni boshida
    /// ushlab turmasligi uchun.
    /// </summary>
    [Fact]
    public void Chiqmagan_va_tushlikka_chiqqan_bir_xil_ishlagan_vaqt_oladi()
    {
        var chiqmagan = Lunch();
        chiqmagan.Alternating(Monday, "09:00", "18:00");

        var tushlikkaChiqqan = Lunch();
        tushlikkaChiqqan.Alternating(Monday, "09:00", "13:00", "14:00", "18:00");

        var ichkarida = chiqmagan.Day(Monday).Facts;
        var tushlikda = tushlikkaChiqqan.Day(Monday).Facts;

        ichkarida.WorkedMinutes.ShouldBe(tushlikda.WorkedMinutes);
        ichkarida.WorkedMinutes.ShouldBe(480);

        ichkarida.BreakTakenMinutes.ShouldBe(0);
        ichkarida.BreakDeductionMinutes.ShouldBe(60, "dalil yo'q — tanaffus to'liq ushlanadi");

        tushlikda.BreakTakenMinutes.ShouldBe(60);
        tushlikda.BreakDeductionMinutes.ShouldBe(0, "oyna to'liq olingan — ushlanadigan narsa yo'q");
    }

    /// <summary>#53. Tanaffusning yarmi olingan.</summary>
    [Fact]
    public void Yarim_tanaffus_olinsa_qolgan_yarmi_ayriladi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "13:00", "13:30", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(30);
        kun.BreakDeductionMinutes.ShouldBe(30);
        kun.WorkedMinutes.ShouldBe(480);
    }

    /// <summary>#54. Tanaffusdan uzun chiqish — ortiqchasi yig'indida allaqachon yo'q, qaytib berilmaydi.</summary>
    [Fact]
    public void Tanaffusdan_uzun_chiqishda_ushlanma_manfiy_bolmaydi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "12:30", "14:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(60, "faqat oyna ichidagi qismi");
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(450);
    }

    /// <summary>#55. Oynadan tashqaridagi chiqish tanaffus hisoblanmaydi — ikki alohida fakt.</summary>
    [Fact]
    public void Oynadan_tashqaridagi_chiqish_tanaffus_hisoblanmaydi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "10:00", "10:30", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(60);
        kun.WorkedMinutes.ShouldBe(450, "yarim soat yig'indidan ham ketdi, tanaffus ham ushlandi");
    }

    /// <summary>#56. Oyna ichidagi ikki qisqa chiqish qo'shiladi.</summary>
    [Fact]
    public void Oyna_ichidagi_ikki_chiqish_qoshiladi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "13:05", "13:20", "13:40", "13:55", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(30);
        kun.BreakDeductionMinutes.ShouldBe(30);
        kun.WorkedMinutes.ShouldBe(480);
    }

    /// <summary>#57. Oyna chegarasini kesib o'tgan chiqish — faqat ichkaridagi qismi.</summary>
    [Fact]
    public void Oyna_chegarasidagi_chiqishning_faqat_ichki_qismi_sanaladi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "12:45", "13:25", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(25);
        kun.BreakDeductionMinutes.ShouldBe(35);
        kun.WorkedMinutes.ShouldBe(465);
    }

    // ────────────────────── Oynadagi borliq bilan cheklash ──────────────────────

    /// <summary>
    /// #58. Oyna ochilmasdan uyga ketgan odam binoda
    /// bo'lmagan tushlik uchun jazolanmaydi. Oxirgi chiqishdan keyingi dum baribir
    /// «olingan tanaffus» EMAS — aks holda erta ketish sonni yaxshilardi.
    /// </summary>
    [Fact]
    public void Oynadan_oldin_uyga_ketganidan_tanaffus_ushlanmaydi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "12:55");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(0, "dum tashqarida o'tgan vaqt emas");
        kun.BreakDeductionMinutes.ShouldBe(0, "oynada bir daqiqa ham binoda bo'lmagan");
        kun.WorkedMinutes.ShouldBe(235);
        // Yon berish 10 daqiqa: (18:00 − 10) − 12:55 = 295.
        kun.EarlyLeaveMinutes.ShouldBe(295, "erta ketish o'z maydonida ko'rinadi");
    }

    /// <summary>#58a. Kelishdan oldingi bosh ham tanaffus emas — u hali kelmagan edi.</summary>
    [Fact]
    public void Oyna_orasida_kelganidan_faqat_kelgandan_keyingi_qismi_ushlanadi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "13:30", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(30);
        kun.WorkedMinutes.ShouldBe(240);
    }

    /// <summary>#58b.</summary>
    [Fact]
    public void Oyna_orasida_qaytmay_ketganidan_faqat_ketgunicha_qismi_ushlanadi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "13:15");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(15);
        kun.WorkedMinutes.ShouldBe(240);
    }

    /// <summary>
    /// #58c. Egiluvchan oyna: 60 daqiqalik oyna, 30 daqiqalik tanaffus. Cheklov —
    /// ship: ushlanmani oyna uzunligigacha ko'tarmaydi. Ichkarida qolgan ham, yarim
    /// soatga chiqqan ham — bir xil ishlagan vaqt.
    /// </summary>
    [Fact]
    public void Egiluvchan_oynada_tanaffus_uzunligidan_ortiq_ushlanmaydi()
    {
        var ichkarida = Lunch(breakMinutes: 30);
        ichkarida.Alternating(Monday, "09:00", "18:00");

        var chiqqan = Lunch(breakMinutes: 30);
        chiqqan.Alternating(Monday, "09:00", "13:00", "13:30", "18:00");

        var qolgan = ichkarida.Day(Monday).Facts;
        var olgan = chiqqan.Day(Monday).Facts;

        qolgan.BreakDeductionMinutes.ShouldBe(30);
        olgan.BreakDeductionMinutes.ShouldBe(0);
        qolgan.WorkedMinutes.ShouldBe(510);
        olgan.WorkedMinutes.ShouldBe(510);
    }

    /// <summary>
    /// #60. Ushlanma hech qachon ishlangan vaqtdan oshmaydi. <c>BreakDeductionMinutes == 20</c>
    /// ham tekshiriladi: nol pastki chegaradan emas, CHEKLOVDAN kelgani shunda ko'rinadi.
    /// </summary>
    [Fact]
    public void Ushlanma_ishlangan_vaqtdan_oshmaydi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "13:20", "13:40");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakDeductionMinutes.ShouldBe(20);
        kun.WorkedMinutes.ShouldBe(0);
    }

    /// <summary>
    /// #60 (xossa). Tasodifiy, lekin qayta o'ynatiladigan (qat'iy urug') o'tishlar
    /// ketma-ketligida: <c>ushlanma ≤ oynadagi borliq ≤ yopilgan yig'indi</c>.
    /// Ishlangan vaqtni manfiy bo'lishdan pastki chegara emas, AYNAN shu tengsizlik
    /// saqlaydi. O'tishlar yarmida oyna atrofiga millisekund aniqlikda to'planadi —
    /// qisqa oraliqlar daqiqani kesish xatosini aynan shu yerda ochadi.
    /// </summary>
    [Fact]
    public void Ushlanma_oynadagi_borliqdan_borliq_yopilgan_yigindidan_oshmaydi()
    {
        var random = new Random(20260917);
        DayPlan[] rejalar =
        [
            DayPlan.For(Schedule("09:00", "18:00", 60, "13:00", "14:00"), Monday),
            DayPlan.For(Schedule("09:00", "18:00", 30, "13:00", "14:00"), Monday),
            DayPlan.For(Schedule("09:00", "18:00", 90, "13:00", "14:00"), Monday),
            DayPlan.For(Schedule("20:00", "08:00", 60, "00:30", "01:30"), Monday),
        ];

        for (var sinov = 0; sinov < 20_000; sinov++)
        {
            var reja = rejalar[sinov % rejalar.Length];
            var oyna = reja.BreakWindowStartAt!.Value;

            var otishlar = Enumerable.Range(0, random.Next(0, 13))
                .Select(_ => new PassReading(
                    Guid.CreateVersion7(),
                    Guid.Empty,
                    sinov % 2 == 0
                        ? oyna.AddMilliseconds(random.Next(-20 * 60_000, 80 * 60_000))
                        : reja.PlanStartAt!.Value.AddSeconds(random.Next(-3600, 13 * 3600)),
                    (PassDirection)random.Next(1, 4)))
                .ToList();

            var natija = PassPairing.Pair(otishlar, reja);
            var izoh = $"{sinov}-sinov";

            natija.BreakDeductionMinutes.ShouldBeInRange(0, natija.PresentInWindowMinutes, izoh);
            natija.BreakDeductionMinutes.ShouldBeLessThanOrEqualTo(reja.BreakMinutes, izoh);
            natija.PresentInWindowMinutes.ShouldBeLessThanOrEqualTo(natija.ClosedMinutes, izoh);
            natija.WorkedMinutes.ShouldBe(natija.ClosedMinutes - natija.BreakDeductionMinutes, izoh);
            (natija.BreakTakenMinutes + natija.PresentInWindowMinutes).ShouldBeLessThanOrEqualTo(60, izoh);
        }
    }

    // ────────────────────── Chiqishi yozilmagan kun ──────────────────────

    /// <summary>
    /// #59. Yopilmagan oxirgi oraliq daqiqa ham, ushlanma ham bermaydi — va 13:00→14:00
    /// bo'shlig'ini ham YOPMAYDI: bo'shliq faqat ikki yopilgan oraliq orasida sanaladi.
    /// </summary>
    [Fact]
    public void Chiqishi_yozilmagan_kunda_ochiq_oraliq_ushlanma_bermaydi()
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "13:00", "14:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(240);
        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
    }

    /// <summary>
    /// Aralash yo'nalishda (egasi o'zgartirmagan) ikki yopilgan oraliq orasidagi yetim
    /// chiqish yoki tashlab ketilgan kirish bo'shliq chegarasini siljitmaydi: tashqarida —
    /// 13:00 dan keyingi YOPILGAN oraliq boshlangan 14:00 gacha.
    /// </summary>
    /// <remarks>
    /// Yo'nalishi to'liq MA'LUM kunda bu holat endi yo'q: ortiqcha chiqish oldingisining
    /// o'rnini, ortiqcha kirish keyingisining o'rnini oladi (egasining qarori, 2026-09-27) —
    /// pastdagi <see cref="Malum_yonalishli_ortiqcha_otish_haqiqiy_chegara_bolib_qoladi"/>.
    /// </remarks>
    [Theory]
    [InlineData(PassDirection.Out, AttendanceDayStatus.MissingEntry)]
    [InlineData(PassDirection.In, AttendanceDayStatus.MissingExit)]
    public void Yopilgan_oraliqlar_orasidagi_ortiqcha_otish_boshliqni_siljitmaydi(
        PassDirection ortiqcha,
        AttendanceDayStatus holat)
    {
        var sahna = Lunch();
        sahna.Unknown(Monday, "09:00");
        sahna.Unknown(Monday, "13:00");

        if (ortiqcha == PassDirection.In)
        {
            // Navbat bilan kirish bo'ladigan o'tish, keyin MA'LUM kirish — oraliq tashlanadi.
            sahna.Unknown(Monday, "13:30");
            sahna.In(Monday, "14:00");
        }
        else
        {
            // Navbat bilan chiqish bo'lgan 13:00 dan keyin MA'LUM chiqish — yetim.
            sahna.Out(Monday, "13:30");
            sahna.Unknown(Monday, "14:00");
        }

        sahna.Unknown(Monday, "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(60);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(480);
        kun.Status.ShouldBe(holat);
    }

    /// <summary>
    /// Egasining qarori (2026-09-27): yo'nalishi MA'LUM ortiqcha o'tish endi haqiqiy chegara —
    /// 13:00 va 13:30 dagi ikki chiqishdan oxirgisi (odam 13:30 da chiqqan), 13:30 va 14:00
    /// dagi ikki kirishdan birinchisi (odam 13:30 da qaytgan). Ikkalasida tashqarida 30 daqiqa,
    /// oynada binoda 30 daqiqa — ushlanma 30, ishlangan 480, kun to'liq.
    /// </summary>
    [Theory]
    [InlineData(PassDirection.Out)]
    [InlineData(PassDirection.In)]
    public void Malum_yonalishli_ortiqcha_otish_haqiqiy_chegara_bolib_qoladi(PassDirection ortiqcha)
    {
        var sahna = Lunch();
        sahna.Alternating(Monday, "09:00", "13:00");
        sahna.Pass(Monday, "13:30", ortiqcha);
        sahna.Alternating(Monday, "14:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.BreakTakenMinutes.ShouldBe(30);
        kun.BreakDeductionMinutes.ShouldBe(30);
        kun.WorkedMinutes.ShouldBe(480);
        kun.IgnoredPassCount.ShouldBe(1);
        kun.Status.ShouldBe(AttendanceDayStatus.Present);
    }

    [Fact]
    public void Oynadan_oldin_chiqishi_yozilmagan_kunda_ishlangan_vaqt_nol()
    {
        var sahna = Lunch();
        sahna.In(Monday, "09:00");

        var kun = sahna.Day(Monday).Facts;

        kun.WorkedMinutes.ShouldBe(0);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.Status.ShouldBe(AttendanceDayStatus.MissingExit);
    }

    // ────────────────────── Tungi smena ──────────────────────

    /// <summary>
    /// #61. 20:00→08:00, oyna 00:30→01:30. Oyna SESHANBA 00:30 ga tushadi —
    /// «sana + soat» uni dushanba 00:30 ga qo'yib, har kecha to'liq tanaffusni
    /// ushlardi va kunduzgi testlar buni hech qachon sezmasdi.
    /// </summary>
    [Fact]
    public void Tungi_smenada_oyna_smenaning_oz_oqiga_qoyiladi()
    {
        var sahna = new AttendanceScenario();
        var munosabat = sahna.Employ();
        sahna.Assign(Schedule("20:00", "08:00", breakMinutes: 60, breakStart: "00:30", breakEnd: "01:30"));

        var reja = sahna.Resolver().Resolve(munosabat, Monday)!.Day;
        reja.BreakWindowStartAt.ShouldBe(At(Tuesday, "00:30"), "dushanba 00:30 emas");
        reja.BreakWindowEndAt.ShouldBe(At(Tuesday, "01:30"));

        sahna.In(Monday, "20:00");
        sahna.Out(Tuesday, "00:35");
        sahna.In(Tuesday, "01:10");
        sahna.Out(Tuesday, "08:00");

        var kunlar = sahna.Days(Monday, Tuesday);
        var dushanba = kunlar[0];

        dushanba.LocalDate.ShouldBe(Monday);
        dushanba.Facts.BreakTakenMinutes.ShouldBe(35);
        dushanba.Facts.BreakDeductionMinutes.ShouldBe(25);
        dushanba.Facts.WorkedMinutes.ShouldBe(660);
        dushanba.Facts.PlannedMinutes.ShouldBe(660);
        dushanba.Facts.PassCount.ShouldBe(4);
        dushanba.Facts.Status.ShouldBe(AttendanceDayStatus.Present);

        kunlar[1].Facts.PassCount.ShouldBe(0, "smena bitta — to'rtala o'tish dushanbaniki");
    }

    // ────────────────────── Oyna bo'lmaydigan holatlar ──────────────────────

    /// <summary>#62. Dam olish kunida oyna ham, tanaffus ham yo'q — xom yig'indi.</summary>
    [Fact]
    public void Dam_olish_kunida_ishlaganda_tanaffus_ayrilmaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Assign(Schedule("09:00", "18:00", 60, "13:00", "14:00", daysOff: [1]));
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday);

        kun.Facts.Status.ShouldBe(AttendanceDayStatus.DayOffWorked);
        kun.HasBreakWindow.ShouldBeFalse();
        kun.Facts.BreakDeductionMinutes.ShouldBe(0);
        kun.Facts.WorkedMinutes.ShouldBe(540);
    }

    [Fact]
    public void Jadvalsiz_kunda_tanaffus_ayrilmaydi()
    {
        var sahna = new AttendanceScenario();
        sahna.Employ();
        sahna.Alternating(Monday, "09:00", "18:00");

        var kun = sahna.Day(Monday).Facts;

        kun.Status.ShouldBe(AttendanceDayStatus.NoSchedule);
        kun.BreakDeductionMinutes.ShouldBe(0);
        kun.WorkedMinutes.ShouldBe(540);
    }

    /// <summary>
    /// #64. Reja har doim <c>break_minutes</c> ni ayiradi — oyna bor-yo'qligi va
    /// odam nima qilganidan qat'i nazar. Reja va fakt bir xil shartda solishtiriladi.
    /// </summary>
    [Fact]
    public void Reja_tanaffus_oynasiga_va_otishlarga_bogliq_emas()
    {
        AttendanceScenario[] sahnalar =
        [
            Office(breakMinutes: 60),
            Lunch(),
            Lunch(),
            Lunch(),
        ];

        sahnalar[0].Alternating(Monday, "09:00", "18:00");
        sahnalar[1].Alternating(Monday, "09:00", "18:00");
        sahnalar[2].Alternating(Monday, "09:00", "13:00", "14:00", "18:00");
        sahnalar[3].Alternating(Monday, "09:00", "12:55");

        foreach (var sahna in sahnalar)
        {
            sahna.Day(Monday).Facts.PlannedMinutes.ShouldBe(480);
        }
    }
}
