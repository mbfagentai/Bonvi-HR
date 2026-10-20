using System.Globalization;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// «Erkin ish jadvali» — kun tavsifidagi «Ish vaqti» (norma) qoidasi, SPEC-verifikatsiya-erkin-jadval §2.4, §2.5.
/// </summary>
/// <remarks>
/// Norma: 1 … (smena − tanaffus) daqiqa; berilmasa sukut — eng kattasi. Dam olish kunida bayroq
/// jimgina tozalanadi, erkin bo'lmagan kunga yuborilgan norma tashlanadi. Tekshiruv tartibida
/// erkin kun qoidasi OXIRGI.
/// </remarks>
public sealed class WorkScheduleFlexibleTests
{
    private const string Kod = "davomat.jadval_erkin_vaqti";

    // ────────────────────── Sukut va chegara ──────────────────────

    /// <summary>SPEC §2.5 misollari: berilmagan norma = smena − tanaffus.</summary>
    [Theory]
    [InlineData("08:30", "18:00", 120, 450)]
    [InlineData("09:00", "18:00", 60, 480)]
    [InlineData("20:00", "08:00", 60, 660)]
    [InlineData("08:00", "20:00", 0, 720)]
    public void Norma_berilmasa_sukut_smena_minus_tanaffus_yoziladi(string boshi, string oxiri, int tanaffus, int kutilgan)
    {
        var dushanba = Dushanba(Yarat(Erkin(boshi, oxiri, tanaffus)).Value);

        dushanba.IsFlexible.ShouldBeTrue();
        dushanba.RequiredMinutes.ShouldBe(kutilgan);
        WorkScheduleDay.MaxRequiredMinutes(Soat(boshi), Soat(oxiri), tanaffus).ShouldBe(kutilgan);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(240)]
    [InlineData(450)]
    public void Chegaradagi_berilgan_norma_saqlanadi(int norma)
        => Dushanba(Yarat(Erkin("08:30", "18:00", 120, "12:00", "14:00", norma)).Value).RequiredMinutes.ShouldBe(norma);

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(451)]
    public void Chegaradan_tashqaridagi_norma_rad_etiladi(int norma)
        => Xato(Erkin("08:30", "18:00", 120, "12:00", "14:00", norma)).ShouldBe(Kod);

    /// <summary>Tanaffus smenaga teng yoki uzun — erkin kun mumkin emas, sukut ham (0) rad etiladi.</summary>
    [Fact]
    public void Tanaffus_smenadan_uzun_bolsa_erkin_kun_rad_etiladi()
    {
        Xato(Erkin("09:00", "12:00", 180)).ShouldBe(Kod);
        Xato(Erkin("09:00", "12:00", 240)).ShouldBe(Kod);
    }

    /// <summary>Xuddi shu tanaffus qat'iy kunda — oldingidek qabul (rejasi 0, SPEC §5.5 6-qadam).</summary>
    [Fact]
    public void Qatiy_kunda_norma_qoidasi_ishlamaydi()
        => Yarat(Kun("09:00", "12:00", 240)).IsSuccess.ShouldBeTrue();

    // ────────────────────── Tozalash ──────────────────────

    [Fact]
    public void Dam_olish_kunidagi_erkin_belgi_jimgina_tozalanadi()
    {
        var kunlar = HaftaKunlari(Kun("09:00", "18:00", 60));
        kunlar[6] = new WorkScheduleDayInput(7, IsWorking: false, IsFlexible: true, RequiredMinutes: 0);

        var natija = WorkSchedule.Create("Dam olishda erkin", kunlar);

        natija.IsSuccess.ShouldBeTrue("dam olish kunida norma tekshirilmaydi — u tashlanadi");
        var yakshanba = natija.Value.Days.Single(day => day.DayIndex == 7);
        yakshanba.IsFlexible.ShouldBeFalse();
        yakshanba.RequiredMinutes.ShouldBeNull();
    }

    [Fact]
    public void Erkin_bolmagan_kunga_yuborilgan_norma_tashlanadi()
    {
        var dushanba = Dushanba(Yarat(Kun("09:00", "18:00", 60) with { RequiredMinutes = 999 }).Value);

        dushanba.IsFlexible.ShouldBeFalse();
        dushanba.RequiredMinutes.ShouldBeNull();
    }

    [Fact]
    public void Tahrirda_erkin_kun_qatiy_kunga_qaytsa_norma_tozalanadi()
    {
        var jadval = Yarat(Erkin("08:30", "18:00", 120, "12:00", "14:00", 400)).Value;

        jadval.Update("Sinov jadvali", HaftaKunlari(Kun("08:30", "18:00", 120, "12:00", "14:00")), 10, 10)
            .IsSuccess.ShouldBeTrue();

        Dushanba(jadval).IsFlexible.ShouldBeFalse();
        Dushanba(jadval).RequiredMinutes.ShouldBeNull();
    }

    // ────────────────────── Tartib ──────────────────────

    /// <summary>Tanaffus oynasi xatosi erkin kun xatosidan OLDIN aytiladi (§2.5, tekshiruv tartibi).</summary>
    [Fact]
    public void Notogri_tanaffus_oynasi_erkin_kun_xatosidan_oldin()
        => Xato(Erkin("08:30", "18:00", 120, "07:00", "08:00", 0)).ShouldBe("davomat.jadval_tanaffus_oynasi");

    [Fact]
    public void Bosh_vaqt_erkin_kun_xatosidan_oldin()
        => Xato(new WorkScheduleDayInput(1, IsWorking: true, IsFlexible: true, RequiredMinutes: 0))
            .ShouldBe("davomat.jadval_vaqt_bosh");

    // ────────────────────── Yillik to'r katagi ──────────────────────

    [Fact]
    public void Sana_katagi_ham_sukut_normani_yozadi()
    {
        var katak = WorkScheduleDate.Create(Guid.CreateVersion7(), Katak(isFlexible: true)).Value;

        katak.IsFlexible.ShouldBeTrue();
        katak.RequiredMinutes.ShouldBe(450);
    }

    [Fact]
    public void Sana_katagida_chegaradan_tashqari_norma_rad_etiladi()
    {
        var yaratish = WorkScheduleDate.Create(Guid.CreateVersion7(), Katak(isFlexible: true, norma: 451));
        yaratish.Error.Code.ShouldBe(Kod);
        yaratish.Error.Type.ShouldBe(ErrorType.Validation);

        var katak = WorkScheduleDate.Create(Guid.CreateVersion7(), Katak(isFlexible: true, norma: 300)).Value;
        katak.Update(Katak(isFlexible: true, norma: 0)).Error.Code.ShouldBe(Kod);
        katak.RequiredMinutes.ShouldBe(300, "rad etilgan tahrir katakni o'zgartirmaydi");
    }

    [Fact]
    public void Sana_katagi_dam_olishga_aylansa_erkinlik_tozalanadi()
    {
        var katak = WorkScheduleDate.Create(Guid.CreateVersion7(), Katak(isFlexible: true, norma: 300)).Value;

        katak.Update(new WorkScheduleDateInput(katak.Date, IsWorking: false, IsFlexible: true, RequiredMinutes: 300))
            .IsSuccess.ShouldBeTrue();

        katak.IsFlexible.ShouldBeFalse();
        katak.RequiredMinutes.ShouldBeNull();
    }

    [Fact]
    public void Sana_katagida_erkin_bolmasa_norma_tashlanadi()
        => WorkScheduleDate.Create(Guid.CreateVersion7(), Katak(isFlexible: false, norma: 300))
            .Value.RequiredMinutes.ShouldBeNull();

    // ────────────────────── Yordamchilar ──────────────────────

    private static TimeOnly Soat(string qiymat) => TimeOnly.Parse(qiymat, CultureInfo.InvariantCulture);

    private static WorkScheduleDayInput Kun(
        string boshi,
        string oxiri,
        int tanaffus,
        string? oynaBoshi = null,
        string? oynaOxiri = null)
        => new(
            1,
            IsWorking: true,
            Soat(boshi),
            Soat(oxiri),
            tanaffus,
            oynaBoshi is null ? null : Soat(oynaBoshi),
            oynaOxiri is null ? null : Soat(oynaOxiri));

    private static WorkScheduleDayInput Erkin(
        string boshi,
        string oxiri,
        int tanaffus,
        string? oynaBoshi = null,
        string? oynaOxiri = null,
        int? norma = null)
        => Kun(boshi, oxiri, tanaffus, oynaBoshi, oynaOxiri) with { IsFlexible = true, RequiredMinutes = norma };

    /// <summary>08:30–18:00, tanaffus 120 (12:00–14:00) katagi.</summary>
    private static WorkScheduleDateInput Katak(bool isFlexible, int? norma = null)
        => new(
            new DateOnly(2026, 10, 21),
            IsWorking: true,
            Soat("08:30"),
            Soat("18:00"),
            120,
            Soat("12:00"),
            Soat("14:00"),
            isFlexible,
            norma);

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

    private static WorkScheduleDay Dushanba(WorkSchedule jadval) => jadval.Days.Single(day => day.DayIndex == 1);

    private static string Xato(WorkScheduleDayInput dushanba)
    {
        var natija = Yarat(dushanba);

        natija.IsFailure.ShouldBeTrue("tekshiruv o'tkazib yubordi");
        natija.Error.Type.ShouldBe(ErrorType.Validation);

        return natija.Error.Code;
    }
}
