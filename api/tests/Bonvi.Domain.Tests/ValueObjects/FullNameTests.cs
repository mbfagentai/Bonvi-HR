using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.ValueObjects;

/// <summary>To'liq ism testlari. Namunalar Excel'dagi haqiqiy yozuvlardan.</summary>
public sealed class FullNameTests
{
    [Fact]
    public void Uch_qismli_ism_togri_ajratiladi()
    {
        var natija = FullName.Parse("Мамажонов Бобуржон Фазлитдин ўғли");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.LastName.ShouldBe("Мамажонов");
        natija.Value.FirstName.ShouldBe("Бобуржон");
        natija.Value.MiddleName.ShouldBe("Фазлитдин ўғли");
    }

    [Fact]
    public void Rus_uslubidagi_otasining_ismi_ajratiladi()
    {
        var natija = FullName.Parse("Қушақов Фарходжон Абдуллаевич");

        natija.Value.LastName.ShouldBe("Қушақов");
        natija.Value.FirstName.ShouldBe("Фарходжон");
        natija.Value.MiddleName.ShouldBe("Абдуллаевич");
    }

    [Fact]
    public void Ikki_qismli_ism_otasining_ismisiz_qabul_qilinadi()
    {
        var natija = FullName.Parse("Салимов Аббосхон");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.MiddleName.ShouldBeNull();
        natija.Value.Full.ShouldBe("Салимов Аббосхон");
    }

    [Fact]
    public void Qizi_qoshimchasi_saqlanadi()
    {
        var natija = FullName.Parse("Дехқонова Одинахон Эргашали қизи");

        natija.Value.MiddleName.ShouldBe("Эргашали қизи");
    }

    [Fact]
    public void Qisqa_korinish_hosil_qilinadi()
    {
        var ism = FullName.Parse("Мамажонов Бобуржон Фазлитдин ўғли").Value;

        ism.ShortForm.ShouldBe("Мамажонов Б.Ф.");
    }

    [Fact]
    public void Ortiqcha_boshliqlar_tozalanadi()
    {
        var natija = FullName.Parse("  Мамажонов   Бобуржон  ");

        natija.Value.LastName.ShouldBe("Мамажонов");
        natija.Value.FirstName.ShouldBe("Бобуржон");
    }

    [Theory]
    [InlineData(null, "ism.bosh")]
    [InlineData("", "ism.bosh")]
    [InlineData("Мамажонов", "ism.tolik_emas")]
    public void Notolik_ism_rad_etiladi(string? kirish, string kutilgan)
    {
        var natija = FullName.Parse(kirish);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(kutilgan);
    }

    [Theory]
    [InlineData("ўғли")]
    [InlineData("o'g'li")]
    [InlineData("қизи")]
    [InlineData("QIZI")]
    public void Otasining_ismi_qoshimchalari_taniladi(string soz)
    {
        FullName.IsPatronymicSuffix(soz).ShouldBeTrue();
    }
}
