using System.Globalization;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Jadval kunining tanaffus oynasi tekshiruvi — SPEC §2.4.0, §2.4.0.1; §11.1 #63.
/// </summary>
/// <remarks>
/// <para>
/// Kodlar B1 chiqarganidek UCHTA (§2.4.0 jadvali): <c>davomat.jadval_tanaffus_toliq_emas</c>,
/// <c>davomat.jadval_tanaffus_oynasi</c>, <c>davomat.jadval_tanaffus_uzunligi</c>.
/// Loyihadagi «…_yarim», «…_tashqarida» kabi nomlar YO'Q — testlar ularga «tuzatilmasin».
/// </para>
/// <para>
/// ⚠️ Tungi smena ALOHIDA tekshiriladi: «smena ichida» xom soat qiymatlari bilan
/// solishtirilsa, 20:00→08:00 smenadagi 00:30 oynasi «smenadan oldin» bo'lib
/// ko'rinardi (§2.4.0.1). Kunduzgi smenada o'tadigan test bu xatoni ushlamaydi.
/// </para>
/// </remarks>
public sealed class WorkScheduleBreakWindowTests
{
    // ────────────────────── Ikkalasi yoki hech biri ──────────────────────

    [Theory]
    [InlineData("13:00", null)]
    [InlineData(null, "14:00")]
    public void Oynaning_faqat_bir_uchi_berilsa_toliq_emas(string? boshi, string? oxiri)
        => Xato(Kun("09:00", "18:00", 60, boshi, oxiri)).ShouldBe("davomat.jadval_tanaffus_toliq_emas");

    [Fact]
    public void Oyna_boshi_va_oxiri_teng_bolsa_rad_etiladi()
        => Xato(Kun("09:00", "18:00", 60, "13:00", "13:00")).ShouldBe("davomat.jadval_tanaffus_oynasi");

    // ────────────────────── Kunduzgi smena ──────────────────────

    /// <summary>Oddiy smena: oyna smenadan oldin, keyin, chegarani kesib — hammasi bitta kod.</summary>
    [Theory]
    [InlineData("08:00", "08:30")] // smena boshlanishidan oldin
    [InlineData("18:30", "19:00")] // smena tugagandan keyin
    [InlineData("08:30", "09:30")] // boshlanishni kesib o'tadi
    [InlineData("17:30", "18:30")] // tugashni kesib o'tadi
    [InlineData("14:00", "13:00")] // teskari
    [InlineData("08:00", "09:00")] // aynan smena boshida TUGAYDI — §2.4.0.1 dagi qat'iy «<»
    public void Kunduzgi_smenada_smenadan_tashqaridagi_oyna_rad_etiladi(string boshi, string oxiri)
        => Xato(Kun("09:00", "18:00", 60, boshi, oxiri)).ShouldBe("davomat.jadval_tanaffus_oynasi");

    /// <summary>Smena boshida boshlanadigan va aynan smena oxirida tugaydigan oyna — ichkarida.</summary>
    [Theory]
    [InlineData("13:00", "14:00")]
    [InlineData("09:00", "09:30")]
    [InlineData("17:00", "18:00")]
    public void Kunduzgi_smena_ichidagi_oyna_qabul_qilinadi(string boshi, string oxiri)
    {
        var jadval = Yarat(Kun("09:00", "18:00", 60, boshi, oxiri)).Value;

        var dushanba = jadval.Days.Single(day => day.DayIndex == 1);
        dushanba.BreakStart.ShouldBe(Soat(boshi));
        dushanba.BreakEnd.ShouldBe(Soat(oxiri));
        dushanba.BreakMinutes.ShouldBe(60);
    }

    // ────────────────────── Tungi smena ──────────────────────

    /// <summary>
    /// 20:00→08:00, oyna 00:30→01:30 — §2.4.0.1 dagi aynan misol: xom soatda
    /// «00:30 &lt; 20:00», smenaning o'z o'qida esa 270…330 ∈ [0, 720].
    /// </summary>
    [Fact]
    public void Tungi_smenada_yarim_tundan_keyingi_oyna_qabul_qilinadi()
    {
        var jadval = Yarat(Kun("20:00", "08:00", 60, "00:30", "01:30")).Value;

        var dushanba = jadval.Days.Single(day => day.DayIndex == 1);
        dushanba.CrossesMidnight.ShouldBeTrue();
        dushanba.BreakStart.ShouldBe(new TimeOnly(0, 30));
        dushanba.BreakEnd.ShouldBe(new TimeOnly(1, 30));
    }

    /// <summary>Yarim tunni kesib o'tadigan oyna tungi smena ichida — ham qabul.</summary>
    [Fact]
    public void Tungi_smenada_yarim_tunni_kesib_otgan_oyna_qabul_qilinadi()
        => Yarat(Kun("20:00", "08:00", 30, "23:45", "00:15")).IsSuccess.ShouldBeTrue();

    /// <summary>Tungi smenada ikki yo'nalishda ham tashqarida: boshlanishdan oldin va tugagandan keyin.</summary>
    [Theory]
    [InlineData("19:00", "19:30")] // smena boshlanishidan oldin
    [InlineData("08:30", "09:00")] // smena tugagandan keyin (ertalab)
    [InlineData("07:30", "08:30")] // tugashni kesib o'tadi
    [InlineData("12:00", "13:00")] // kunduz — smenaga umuman tegmaydi
    public void Tungi_smenada_smenadan_tashqaridagi_oyna_rad_etiladi(string boshi, string oxiri)
        => Xato(Kun("20:00", "08:00", 60, boshi, oxiri)).ShouldBe("davomat.jadval_tanaffus_oynasi");

    // ────────────────────── Uzunlik va dam olish kuni ──────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(WorkScheduleDay.MaxBreakMinutes + 1)]
    public void Tanaffus_uzunligi_chegaradan_tashqarida_rad_etiladi(int daqiqa)
        => Xato(Kun("09:00", "18:00", daqiqa, "13:00", "14:00")).ShouldBe("davomat.jadval_tanaffus_uzunligi");

    [Fact]
    public void Tanaffus_uzunligining_yuqori_chegarasi_qabul_qilinadi()
        => Yarat(Kun("09:00", "18:00", WorkScheduleDay.MaxBreakMinutes)).IsSuccess.ShouldBeTrue();

    /// <summary>Oyna bor, tanaffus 0 daqiqa — qabul (hech narsa ushlanmaydi, §2.4.0).</summary>
    [Fact]
    public void Nol_daqiqali_tanaffus_oyna_bilan_qabul_qilinadi()
    {
        var dushanba = Yarat(Kun("09:00", "18:00", 0, "13:00", "14:00")).Value.Days.Single(day => day.DayIndex == 1);

        dushanba.BreakMinutes.ShouldBe(0);
        dushanba.BreakStart.ShouldBe(new TimeOnly(13, 0));
    }

    /// <summary>
    /// Dam olish kunidagi oyna XATO EMAS — jimgina tozalanadi: smena yo'q, oyna qo'yadigan joy
    /// ham yo'q. Hatto smenadan «tashqaridagi» qiymatlar ham tekshirilmaydi.
    /// </summary>
    [Fact]
    public void Dam_olish_kunidagi_oyna_qabul_qilinadi_va_tozalanadi()
    {
        var kunlar = HaftaKunlari(Kun("09:00", "18:00", 60, "13:00", "14:00"));
        kunlar[6] = new WorkScheduleDayInput(7, IsWorking: false, Soat("09:00"), Soat("18:00"), 60, Soat("03:00"), Soat("02:00"));

        var natija = WorkSchedule.Create("Dam olish oynasi", kunlar);

        natija.IsSuccess.ShouldBeTrue();
        var yakshanba = natija.Value.Days.Single(day => day.DayIndex == 7);
        yakshanba.IsWorking.ShouldBeFalse();
        yakshanba.BreakStart.ShouldBeNull();
        yakshanba.BreakEnd.ShouldBeNull();
        yakshanba.BreakMinutes.ShouldBe(0);
        yakshanba.StartTime.ShouldBeNull();
    }

    /// <summary>Tahrirlash ham xuddi shu qoidadan o'tadi — va rad etilganda jadval o'zgarmaydi.</summary>
    [Fact]
    public void Tahrirda_ham_oyna_tekshiriladi_va_rad_etilsa_kunlar_ozgarmaydi()
    {
        var jadval = Yarat(Kun("20:00", "08:00", 60, "00:30", "01:30")).Value;

        var natija = jadval.Update("Tungi", HaftaKunlari(Kun("20:00", "08:00", 60, "19:00", "19:30")), 10, 10);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("davomat.jadval_tanaffus_oynasi");
        jadval.Days.Single(day => day.DayIndex == 1).BreakStart.ShouldBe(new TimeOnly(0, 30));
    }

    // ────────────────────── Bo'sh identifikatorlar — §6.9.1 ──────────────────────

    /// <summary>Bo'sh xodim identifikatori — <c>_yoq</c> (400), <c>_topilmadi</c> (404) emas.</summary>
    [Fact]
    public void Moslikda_bosh_xodim_xodim_yoq()
    {
        var natija = AttendanceIdentity.Link(AttendanceIdentityProvider.Verifix, "10427", Guid.Empty);

        natija.Error.Code.ShouldBe("davomat.xodim_yoq");
        natija.Error.Type.ShouldBe(ErrorType.Validation);
    }

    /// <summary>Bo'sh jadval identifikatori — <c>davomat.jadval_yoq</c> (400).</summary>
    [Fact]
    public void Biriktirmada_bosh_jadval_jadval_yoq()
    {
        var natija = ScheduleAssignment.Assign(Guid.Empty, ScheduleScope.Employee, Guid.CreateVersion7(), new DateOnly(2026, 1, 1));

        natija.Error.Code.ShouldBe("davomat.jadval_yoq");
        natija.Error.Type.ShouldBe(ErrorType.Validation);
    }

    // ────────────────────── Yordamchilar ──────────────────────

    private static TimeOnly Soat(string qiymat) => TimeOnly.Parse(qiymat, CultureInfo.InvariantCulture);

    private static WorkScheduleDayInput Kun(string boshi, string oxiri, int tanaffus, string? oynaBoshi = null, string? oynaOxiri = null)
        => new(
            1,
            IsWorking: true,
            Soat(boshi),
            Soat(oxiri),
            tanaffus,
            oynaBoshi is null ? null : Soat(oynaBoshi),
            oynaOxiri is null ? null : Soat(oynaOxiri));

    /// <summary>Dushanba — tekshirilayotgan kun; qolgan olti kun oynasiz 09:00–18:00.</summary>
    private static WorkScheduleDayInput[] HaftaKunlari(WorkScheduleDayInput dushanba)
        =>
        [
            dushanba with { DayIndex = 1 },
            .. Enumerable.Range(2, WorkSchedule.WeeklyDayCount - 1)
                .Select(index => new WorkScheduleDayInput(index, IsWorking: true, Soat("09:00"), Soat("18:00"))),
        ];

    private static Result<WorkSchedule> Yarat(WorkScheduleDayInput dushanba)
        => WorkSchedule.Create("Sinov jadvali", HaftaKunlari(dushanba));

    private static string Xato(WorkScheduleDayInput dushanba)
    {
        var natija = Yarat(dushanba);

        natija.IsFailure.ShouldBeTrue("tekshiruv o'tkazib yubordi");
        natija.Error.Type.ShouldBe(ErrorType.Validation);

        return natija.Error.Code;
    }
}
