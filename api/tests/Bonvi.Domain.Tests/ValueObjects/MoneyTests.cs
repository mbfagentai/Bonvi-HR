using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.ValueObjects;

/// <summary>Pul miqdori testlari.</summary>
public sealed class MoneyTests
{
    [Fact]
    public void Musbat_miqdor_qabul_qilinadi()
    {
        // Excel'dagi haqiqiy oklad
        var natija = Money.Create(4_500_000m);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Amount.ShouldBe(4_500_000m);
        natija.Value.Currency.ShouldBe("UZS");
    }

    [Fact]
    public void Korsatish_uchun_formatlanadi()
    {
        Money.Create(4_500_000m).Value.Formatted.ShouldBe("4 500 000 so'm");
    }

    [Fact]
    public void Manfiy_miqdor_rad_etiladi()
    {
        var natija = Money.Create(-1000m);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("pul.manfiy");
    }

    [Fact]
    public void Ikkitadan_ortiq_kasr_xona_rad_etiladi()
    {
        var natija = Money.Create(1000.555m);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("pul.aniqlik");
    }

    [Fact]
    public void Bir_xil_valyutadagi_summalar_qoshiladi()
    {
        var a = Money.Create(4_000_000m).Value;
        var b = Money.Create(500_000m).Value;

        var natija = a.Add(b);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Amount.ShouldBe(4_500_000m);
    }

    [Fact]
    public void Turli_valyutalar_qoshilmaydi()
    {
        var som = Money.Create(4_000_000m, "UZS").Value;
        var dollar = Money.Create(100m, "USD").Value;

        var natija = som.Add(dollar);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("pul.valyuta_mos_emas");
    }

    [Fact]
    public void Bir_xil_qiymatli_summalar_teng()
    {
        Money.Create(1000m).Value.ShouldBe(Money.Create(1000m).Value);
    }

    [Fact]
    public void Notogri_valyuta_kodi_rad_etiladi()
    {
        Money.Create(1000m, "UZSS").IsFailure.ShouldBeTrue();
        Money.Create(1000m, "UZ").IsFailure.ShouldBeTrue();
    }
}
