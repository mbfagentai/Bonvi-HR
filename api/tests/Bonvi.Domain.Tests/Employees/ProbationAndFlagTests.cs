using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>Sinov muddati va xodim belgilari mantig'i.</summary>
public sealed class ProbationAndFlagTests
{
    private static readonly Guid MunosabatId = Guid.CreateVersion7();
    private static readonly Guid XodimId = Guid.CreateVersion7();
    private static readonly DateOnly Boshlangan = new(2026, 1, 15);

    // ────────────────────── Sinov muddati ──────────────────────

    [Fact]
    public void Standart_sinov_muddati_90_kun()
    {
        var s = Probation.Start(MunosabatId, Boshlangan).Value;

        s.EndDate.ShouldBe(Boshlangan.AddDays(90));
        s.Status.ShouldBe(ProbationStatus.InProgress);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(181)]
    public void Mehnat_kodeksi_chegarasidan_tashqari_muddat_rad_etiladi(int kun)
    {
        var natija = Probation.Start(MunosabatId, Boshlangan, kun);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("sinov.muddat");
    }

    [Fact]
    public void Tugashiga_7_kun_qolganda_ogohlantirish_kerak()
    {
        // HR so'ragan asosiy imkoniyat: Excel'da bu qo'lda kuzatilardi
        // va shu sababli tez-tez o'tkazib yuborilardi
        var s = Probation.Start(MunosabatId, Boshlangan).Value;

        s.NeedsWarning(s.EndDate.AddDays(-8)).ShouldBeFalse();
        s.NeedsWarning(s.EndDate.AddDays(-7)).ShouldBeTrue();
        s.NeedsWarning(s.EndDate).ShouldBeTrue();
    }

    [Fact]
    public void Yakunlangan_sinovda_ogohlantirish_kerak_emas()
    {
        var s = Probation.Start(MunosabatId, Boshlangan).Value;
        s.Resolve(ProbationStatus.Passed, s.EndDate);

        s.NeedsWarning(s.EndDate.AddDays(-1)).ShouldBeFalse();
    }

    [Fact]
    public void Qolgan_kunlar_togri_hisoblanadi()
    {
        var s = Probation.Start(MunosabatId, Boshlangan, 30).Value;

        s.DaysLeft(Boshlangan).ShouldBe(30);
        s.DaysLeft(Boshlangan.AddDays(35)).ShouldBe(-5, "muddat o'tib ketgan");
    }

    [Fact]
    public void Natija_bir_marta_chiqariladi()
    {
        var s = Probation.Start(MunosabatId, Boshlangan).Value;
        s.Resolve(ProbationStatus.Passed, s.EndDate, "Yaxshi ishlayapti");

        s.Resolve(ProbationStatus.Failed, s.EndDate).Error.Code.ShouldBe("sinov.yakunlangan");
        s.Conclusion.ShouldBe("Yaxshi ishlayapti");
    }

    [Fact]
    public void Yakuniy_natija_InProgress_bola_olmaydi()
    {
        var s = Probation.Start(MunosabatId, Boshlangan).Value;

        s.Resolve(ProbationStatus.InProgress, Boshlangan).Error.Code.ShouldBe("sinov.natija");
    }

    [Fact]
    public void Muddat_uzaytiriladi_lekin_180_kundan_oshmaydi()
    {
        var s = Probation.Start(MunosabatId, Boshlangan, 90).Value;

        s.Extend(Boshlangan.AddDays(150)).IsSuccess.ShouldBeTrue();
        s.EndDate.ShouldBe(Boshlangan.AddDays(150));

        s.Extend(Boshlangan.AddDays(181)).Error.Code.ShouldBe("sinov.muddat");
    }

    [Fact]
    public void Muddatni_qisqartirish_uzaytirish_emas()
    {
        var s = Probation.Start(MunosabatId, Boshlangan, 90).Value;

        s.Extend(Boshlangan.AddDays(60)).Error.Code.ShouldBe("sinov.uzaytirish");
    }

    // ────────────────────── Belgilar ──────────────────────

    [Fact]
    public void Qora_royxatga_sababsiz_qoyib_bolmaydi()
    {
        // Excel'da sababsiz yozuvlar bor edi va keyin hech kim
        // nima uchun ekanini bilmasdi
        var natija = EmployeeFlag.Raise(XodimId, EmployeeFlagType.Blacklist, Boshlangan);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("belgi.sabab_bosh");
    }

    [Fact]
    public void Rezerv_belgisi_sababsiz_ham_qoyiladi()
    {
        EmployeeFlag.Raise(XodimId, EmployeeFlagType.Reserve, Boshlangan)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Muddatsiz_belgi_doim_kuchda()
    {
        var b = EmployeeFlag.Raise(XodimId, EmployeeFlagType.Reserve, Boshlangan).Value;

        b.IsActiveOn(Boshlangan).ShouldBeTrue();
        b.IsActiveOn(Boshlangan.AddYears(5)).ShouldBeTrue();
        b.IsActiveOn(Boshlangan.AddDays(-1)).ShouldBeFalse();
    }

    [Fact]
    public void Muddatli_belgi_oraliqda_kuchda()
    {
        var b = EmployeeFlag.Raise(
            XodimId,
            EmployeeFlagType.Monitoring,
            Boshlangan,
            "Mijoz shikoyati",
            Boshlangan.AddMonths(3)).Value;

        b.IsActiveOn(Boshlangan.AddMonths(1)).ShouldBeTrue();
        b.IsActiveOn(Boshlangan.AddMonths(4)).ShouldBeFalse();
    }

    [Fact]
    public void Belgi_yopilgandan_keyin_qayta_yopilmaydi()
    {
        var b = EmployeeFlag.Raise(XodimId, EmployeeFlagType.Monitoring, Boshlangan, "Sabab").Value;
        b.Close(Boshlangan.AddMonths(2), "Muammo hal bo'ldi");

        b.Close(Boshlangan.AddMonths(3)).Error.Code.ShouldBe("belgi.yopilgan");
        b.Reason.ShouldNotBeNull();
        b.Reason.ShouldContain("Muammo hal bo'ldi");
    }

    [Fact]
    public void Tugash_sanasi_boshlanishdan_oldin_bola_olmaydi()
    {
        EmployeeFlag.Raise(
            XodimId,
            EmployeeFlagType.Reserve,
            Boshlangan,
            endDate: Boshlangan.AddDays(-1))
            .Error.Code.ShouldBe("belgi.sana");
    }
}
