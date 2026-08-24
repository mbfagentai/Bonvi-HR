using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.ValueObjects;

/// <summary>
/// Pasport seriyasi testlari — asosiy e'tibor kirill/lotin gomogliflariga.
/// </summary>
public sealed class PassportSeriesTests
{
    [Fact]
    public void Lotin_harflari_bilan_qabul_qilinadi()
    {
        var natija = PassportSeries.Create("AB5725936");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Value.ShouldBe("AB5725936");
    }

    [Fact]
    public void Kirill_harflari_lotinga_aylantiriladi()
    {
        // Excel'dagi haqiqiy yozuv: «АВ5725936» — А = U+0410, В = U+0412 (kirill!)
        const string kirillcha = "АВ5725936";

        var natija = PassportSeries.Create(kirillcha);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Value.ShouldBe("AB5725936");
    }

    [Fact]
    public void Kirill_va_lotin_yozuvlari_TENG_hisoblanadi()
    {
        // Bu Excel'dagi eng jimgina xato: ikkala yozuv ko'zga bir xil,
        // lekin qidiruvda ular boshqa-boshqa satr edi.
        var lotin = PassportSeries.Create("AB5725936").Value;
        var kirill = PassportSeries.Create("АВ5725936").Value;

        lotin.ShouldBe(kirill);
        lotin.GetHashCode().ShouldBe(kirill.GetHashCode());
    }

    [Theory]
    [InlineData("АА", "AA")]  // А А
    [InlineData("ЕК", "EK")]  // Е К
    [InlineData("МН", "MH")]  // М Н
    [InlineData("ОР", "OP")]  // О Р
    [InlineData("СТ", "CT")]  // С Т
    [InlineData("ХУ", "XY")]  // Х У
    public void Barcha_gomogliflar_togri_aylantiriladi(string kirill, string lotin)
    {
        PassportSeries.Normalize(kirill).ShouldBe(lotin);
    }

    [Theory]
    [InlineData("AB 5725936")]
    [InlineData("AB-5725936")]
    [InlineData("ab5725936")]
    [InlineData("№AB5725936")]
    public void Ajratgich_va_kichik_harf_tozalanadi(string kirish)
    {
        PassportSeries.Create(kirish).Value.Value.ShouldBe("AB5725936");
    }

    [Fact]
    public void Seriya_va_raqam_alohida_olinadi()
    {
        var pasport = PassportSeries.Create("AB5725936").Value;

        pasport.Series.ShouldBe("AB");
        pasport.Number.ShouldBe("5725936");
    }

    [Theory]
    [InlineData(null, "pasport.bosh")]
    [InlineData("", "pasport.bosh")]
    [InlineData("AB572593", "pasport.uzunlik")]     // 6 raqam
    [InlineData("AB57259367", "pasport.uzunlik")]   // 8 raqam
    [InlineData("A15725936", "pasport.seriya")]     // raqam seriya o'rnida
    public void Notogri_format_rad_etiladi(string? kirish, string kutilgan)
    {
        var natija = PassportSeries.Create(kirish);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(kutilgan);
    }
}
