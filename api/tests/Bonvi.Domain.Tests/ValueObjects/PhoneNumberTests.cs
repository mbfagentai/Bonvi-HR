using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.ValueObjects;

/// <summary>Telefon raqami testlari. Namunalar Excel'dagi haqiqiy formatlardan.</summary>
public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("905088888")]           // Excel'da eng keng tarqalgan ko'rinish
    [InlineData("+998905088888")]
    [InlineData("998905088888")]
    [InlineData("+998 90 508 88 88")]
    [InlineData("90-508-88-88")]
    [InlineData("(90) 508-88-88")]
    public void Turli_ko_rinishlar_bitta_formatga_keltiriladi(string kirish)
    {
        var natija = PhoneNumber.Create(kirish);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.Value.ShouldBe("+998905088888");
    }

    [Fact]
    public void Korsatish_uchun_formatlanadi()
    {
        var telefon = PhoneNumber.Create("905088888").Value;

        telefon.Formatted.ShouldBe("+998 90 508 88 88");
        telefon.OperatorCode.ShouldBe("90");
    }

    [Theory]
    [InlineData("90")]   // Ucell / Beeline
    [InlineData("91")]
    [InlineData("93")]
    [InlineData("94")]
    [InlineData("95")]
    [InlineData("97")]
    [InlineData("98")]
    [InlineData("99")]
    [InlineData("88")]
    [InlineData("77")]
    [InlineData("50")]
    [InlineData("33")]
    [InlineData("20")]
    public void Bonvi_malumotidagi_barcha_operator_kodlari_qabul_qilinadi(string kod)
    {
        var natija = PhoneNumber.Create(kod + "5088888");

        natija.IsSuccess.ShouldBeTrue($"«{kod}» kodi Excel'da uchraydi va rad etilmasligi kerak");
    }

    [Theory]
    [InlineData(null, "telefon.bosh")]
    [InlineData("", "telefon.bosh")]
    [InlineData("12345", "telefon.uzunlik")]
    [InlineData("+79161234567", "telefon.mamlakat_kodi")]  // Rossiya raqami
    [InlineData("005088888", "telefon.operator")]
    public void Notogri_raqam_rad_etiladi(string? kirish, string kutilgan)
    {
        var natija = PhoneNumber.Create(kirish);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(kutilgan);
    }

    [Fact]
    public void Bitta_katakdagi_ikkita_raqam_ajratiladi()
    {
        // Excel'da 13 ta yozuvda 18 xona — ikkita raqam yopishib qolgan
        var raqamlar = PhoneNumber.SplitMultiple("905088888903081818");

        raqamlar.Count.ShouldBe(2);
        raqamlar[0].ShouldBe("905088888");
        raqamlar[1].ShouldBe("903081818");
    }

    [Fact]
    public void Vergul_bilan_ajratilgan_raqamlar_bolinadi()
    {
        var raqamlar = PhoneNumber.SplitMultiple("905088888, 903081818");

        raqamlar.Count.ShouldBe(2);
        PhoneNumber.Create(raqamlar[0]).IsSuccess.ShouldBeTrue();
        PhoneNumber.Create(raqamlar[1]).IsSuccess.ShouldBeTrue();
    }
}
