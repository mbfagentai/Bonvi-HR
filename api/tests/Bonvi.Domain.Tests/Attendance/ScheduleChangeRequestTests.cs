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
}
