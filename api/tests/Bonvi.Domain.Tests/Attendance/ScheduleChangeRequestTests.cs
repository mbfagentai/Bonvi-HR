using Bonvi.Domain.Attendance;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Jadvalni o'zgartirish so'rovi — kunlar qoidasi va holatlar (SPEC-verifikatsiya-jadval-sorovi §C1).
/// </summary>
public sealed class ScheduleChangeRequestTests
{
    private static readonly Guid Xodim = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 10, 16);

    private static TimeOnly T(int soat, int daqiqa = 0) => new(soat, daqiqa);

    private static ScheduleChangeDayInput IshKuni(DateOnly sana, int boshi = 8, int oxiri = 18, (TimeOnly, TimeOnly)? tanaffus = null)
        => new(sana, true, T(boshi, 30), T(oxiri), tanaffus?.Item1, tanaffus?.Item2);

    [Fact]
    public void Tanaffusi_uzaytirilgan_kun_qabul_qilinadi()
    {
        // Verifix'dagi tipik so'rov: 08:30–18:00, tanaffus 12:23–14:23.
        var natija = ScheduleChangeRequest.Create(
            Xodim,
            ScheduleChangeKind.Change,
            [IshKuni(Sana, tanaffus: (T(12, 23), T(14, 23)))],
            "Tushlik va juma namoziga uyga bordim");

        natija.IsSuccess.ShouldBeTrue();
        var kun = natija.Value.Days.ShouldHaveSingleItem();
        kun.BreakMinutes.ShouldBe(120);
        natija.Value.Status.ShouldBe(AbsenceRequestStatus.Pending);
    }

    [Fact]
    public void Berilgan_tanaffus_daqiqasi_oynadan_ustun()
    {
        // Almashtirishda jadvaldan ko'chgan kun: 60 daqiqa 12:00–15:00 oynasida.
        var natija = ScheduleChangeRequest.Create(
            Xodim,
            ScheduleChangeKind.Change,
            [new ScheduleChangeDayInput(Sana, true, T(9), T(18), T(12), T(15), BreakMinutes: 60)]);

        natija.Value.Days.ShouldHaveSingleItem().BreakMinutes.ShouldBe(60);
    }

    [Fact]
    public void Dam_olish_kunida_tanaffus_daqiqasi_nol()
        => ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, false, BreakMinutes: 60)])
            .Value.Days.ShouldHaveSingleItem().BreakMinutes.ShouldBe(0);

    [Fact]
    public void Haddan_uzun_tanaffus_daqiqasi_rad_etiladi()
        => ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, true, T(9), T(18), BreakMinutes: WorkScheduleDay.MaxBreakMinutes + 1)])
            .Error.Code.ShouldBe("davomat.jadval_tanaffus_uzunligi");

    [Fact]
    public void Chekka_yildagi_sana_rad_etiladi()
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [IshKuni(DateOnly.MinValue)])
            .Error.Code.ShouldBe("davomat.jadval_sorovi_sana");

    [Fact]
    public void Doksan_ikki_kundan_keng_davr_rad_etiladi()
    {
        ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [IshKuni(Sana), IshKuni(Sana.AddDays(ScheduleChangeRequest.MaxSpanDays - 1))])
            .IsSuccess.ShouldBeTrue();

        ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [IshKuni(Sana), IshKuni(Sana.AddDays(ScheduleChangeRequest.MaxSpanDays))])
            .Error.Code.ShouldBe("davomat.jadval_sorovi_davri");
    }

    [Fact]
    public void Uzun_izoh_rad_etiladi()
    {
        var uzun = new string('a', ScheduleChangeRequest.MaxNoteLength + 1);

        ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [IshKuni(Sana)], uzun)
            .Error.Code.ShouldBe("davomat.izoh_uzun");

        var sorov = ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [IshKuni(Sana)]).Value;
        sorov.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow, uzun).Error.Code.ShouldBe("davomat.izoh_uzun");
        sorov.Status.ShouldBe(AbsenceRequestStatus.Pending);
    }

    [Fact]
    public void Dam_olish_kunida_vaqtlar_saqlanmaydi()
    {
        var natija = ScheduleChangeRequest.Create(
            Xodim,
            ScheduleChangeKind.Change,
            [new ScheduleChangeDayInput(Sana, false, T(9), T(18), T(13), T(14))]);

        var kun = natija.Value.Days.ShouldHaveSingleItem();
        kun.IsWorking.ShouldBeFalse();
        kun.StartTime.ShouldBeNull();
        kun.BreakStart.ShouldBeNull();
    }

    [Fact]
    public void Kunsiz_sorov_rad_etiladi()
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [])
            .Error.Code.ShouldBe("davomat.jadval_sorovi_kun_yoq");

    [Fact]
    public void Bir_sana_ikki_marta_rad_etiladi()
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [IshKuni(Sana), IshKuni(Sana)])
            .Error.Code.ShouldBe("davomat.jadval_sorovi_sana_takror");

    [Fact]
    public void Otiz_bir_kundan_kopi_rad_etiladi()
        => ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [.. Enumerable.Range(0, ScheduleChangeRequest.MaxDays + 1).Select(i => IshKuni(Sana.AddDays(i)))])
            .Error.Code.ShouldBe("davomat.jadval_sorovi_kop");

    [Fact]
    public void Ish_kunida_vaqtsiz_rad_etiladi()
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [new ScheduleChangeDayInput(Sana, true)])
            .Error.Code.ShouldBe("davomat.jadval_vaqt_bosh");

    [Fact]
    public void Smenadan_tashqaridagi_tanaffus_rad_etiladi()
        => ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [IshKuni(Sana, tanaffus: (T(19), T(20)))])
            .Error.Code.ShouldBe("davomat.jadval_tanaffus_oynasi");

    [Fact]
    public void Yarim_tanaffus_rad_etiladi()
        => ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, true, T(9), T(18), T(13), null)])
            .Error.Code.ShouldBe("davomat.jadval_tanaffus_toliq_emas");

    [Fact]
    public void Tungi_smenadagi_yarim_tundan_otuvchi_tanaffus_qabul_qilinadi()
    {
        var natija = ScheduleChangeRequest.Create(
            Xodim,
            ScheduleChangeKind.Change,
            [new ScheduleChangeDayInput(Sana, true, T(20), T(8), T(23, 30), T(0, 30))]);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Days[0].BreakMinutes.ShouldBe(60);
    }

    [Fact]
    public void Qaror_qabul_qilingan_sorov_tahrirlanmaydi_va_xodim_bekor_qila_olmaydi()
    {
        var sorov = ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [IshKuni(Sana)]).Value;
        sorov.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();

        sorov.Update(ScheduleChangeKind.Change, [IshKuni(Sana.AddDays(1))], null)
            .Error.Code.ShouldBe("davomat.sorov_qaror_qabul_qilingan");
        sorov.CancelBySubmitter(Guid.CreateVersion7(), DateTimeOffset.UtcNow)
            .Error.Code.ShouldBe("davomat.sorov_qaror_qabul_qilingan");

        // HR esa tasdiqlanganini ham bekor qiladi.
        sorov.Cancel(Guid.CreateVersion7(), DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        sorov.Status.ShouldBe(AbsenceRequestStatus.Cancelled);
    }

    // ────────────────────── Erkin ish jadvali (SPEC-erkin-jadval §2.5, §10.1) ──────────────────────

    /// <summary>Verifix formasi: 08:30–18:00, tanaffus 12:00–14:00, «Erkin» — norma oynadan: 570 − 120 = 450.</summary>
    [Fact]
    public void Erkin_kunda_norma_berilmasa_oyna_tanaffusi_bilan_sukut_450()
    {
        var kun = ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, true, T(8, 30), T(18), T(12), T(14), IsFlexible: true)])
            .Value.Days.ShouldHaveSingleItem();

        kun.IsFlexible.ShouldBeTrue();
        kun.BreakMinutes.ShouldBe(120);
        kun.RequiredMinutes.ShouldBe(450);
    }

    /// <summary>Berilgan tanaffus daqiqasi oynadan ustun — norma ham shundan: 540 − 60 = 480 (180 emas).</summary>
    [Fact]
    public void Erkin_kunda_sukut_berilgan_tanaffus_daqiqasidan_480()
        => ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, true, T(9), T(18), T(12), T(15), BreakMinutes: 60, IsFlexible: true)])
            .Value.Days.ShouldHaveSingleItem().RequiredMinutes.ShouldBe(480);

    [Fact]
    public void Erkin_kunda_berilgan_norma_saqlanadi_chegaradan_oshsa_rad_etiladi()
    {
        ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, true, T(8, 30), T(18), T(12), T(14), IsFlexible: true, RequiredMinutes: 420)])
            .Value.Days.ShouldHaveSingleItem().RequiredMinutes.ShouldBe(420);

        ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(Sana, true, T(8, 30), T(18), T(12), T(14), IsFlexible: true, RequiredMinutes: 451)])
            .Error.Code.ShouldBe("davomat.jadval_erkin_vaqti");
    }

    [Fact]
    public void Dam_olish_kunida_va_erkin_bolmagan_kunda_norma_saqlanmaydi()
    {
        var kunlar = ScheduleChangeRequest.Create(
                Xodim,
                ScheduleChangeKind.Change,
                [
                    new ScheduleChangeDayInput(Sana, false, IsFlexible: true, RequiredMinutes: 450),
                    new ScheduleChangeDayInput(Sana.AddDays(1), true, T(9), T(18), RequiredMinutes: 300),
                ])
            .Value.Days;

        kunlar.ShouldAllBe(kun => !kun.IsFlexible && kun.RequiredMinutes == null);
    }

    // ────────────────────── Tahrir va parallellik (SPEC-sorov-tahriri §2.3, §3.3) ──────────────────────

    private static readonly DateTimeOffset Lahza = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static ScheduleChangeRequest Yangi()
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [IshKuni(Sana)]).Value;

    [Fact]
    public void Yangi_sorov_nolinchi_versiyada()
    {
        var sorov = Yangi();

        sorov.Version.ShouldBe(0);
        sorov.EditedAt.ShouldBeNull();
        sorov.EditedByAccountId.ShouldBeNull();
    }

    [Fact]
    public void Tahrir_versiyani_oshiradi_va_ilova_belgisini_yozadi()
    {
        var sorov = Yangi();
        var hisob = Guid.CreateVersion7();

        sorov.Update(ScheduleChangeKind.Change, [IshKuni(Sana.AddDays(1))], "yangi", at: Lahza, editedByAccountId: hisob)
            .IsSuccess.ShouldBeTrue();

        sorov.Version.ShouldBe(1);
        sorov.EditedAt.ShouldBe(Lahza);
        sorov.EditedByAccountId.ShouldBe(hisob);
        sorov.Days.ShouldHaveSingleItem().Date.ShouldBe(Sana.AddDays(1));
    }

    [Fact]
    public void HR_tahriri_faqat_lahzani_yozadi()
    {
        var sorov = Yangi();
        sorov.Update(ScheduleChangeKind.Change, [IshKuni(Sana)], null, at: Lahza, editedByAccountId: Guid.CreateVersion7());

        sorov.Update(ScheduleChangeKind.Change, [IshKuni(Sana)], null, at: Lahza.AddHours(1)).IsSuccess.ShouldBeTrue();

        sorov.EditedAt.ShouldBe(Lahza.AddHours(1));
        sorov.EditedByAccountId.ShouldBeNull();
        sorov.Version.ShouldBe(2);
    }

    [Fact]
    public void Qaror_va_bekor_qilish_versiyani_oshiradi()
    {
        var tasdiq = Yangi();
        tasdiq.Approve(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        tasdiq.Version.ShouldBe(1);
        tasdiq.Cancel(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        tasdiq.Version.ShouldBe(2);

        var rad = Yangi();
        rad.Reject(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        rad.Version.ShouldBe(1);

        var qaytarilgan = Yangi();
        qaytarilgan.CancelBySubmitter(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        qaytarilgan.Version.ShouldBe(1);
    }

    [Fact]
    public void Rad_etilgan_amal_versiyaga_tegmaydi()
    {
        var sorov = Yangi();
        var uzun = new string('a', ScheduleChangeRequest.MaxNoteLength + 1);

        sorov.Update(ScheduleChangeKind.Change, [IshKuni(Sana)], uzun, at: Lahza).Error.Code.ShouldBe("davomat.izoh_uzun");
        sorov.Reject(Guid.CreateVersion7(), Lahza, uzun).Error.Code.ShouldBe("davomat.izoh_uzun");

        sorov.Version.ShouldBe(0);
        sorov.EditedAt.ShouldBeNull();
    }
}
