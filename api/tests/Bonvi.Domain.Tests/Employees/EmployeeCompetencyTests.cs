using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>Xodim qobiliyatlar profili mantig'i (91.15).</summary>
public sealed class EmployeeCompetencyTests
{
    private static readonly Guid XodimId = Guid.CreateVersion7();

    [Fact]
    public void Xodimsiz_profil_yaratilmaydi()
    {
        var natija = EmployeeCompetency.Create(Guid.Empty);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qobiliyat.xodim_bosh");
    }

    [Fact]
    public void Bosh_profil_hamma_maydoni_null_boladi()
    {
        // Excel'da 554 yozuvning aksariyatida faqat 1-2 ustun to'ldirilgan —
        // profil to'liqlik talab qilmasligi kerak.
        var p = EmployeeCompetency.Create(XodimId).Value;

        p.Languages.ShouldBeNull();
        p.ComputerLiteracyPercent.ShouldBeNull();
        p.Specialization.ShouldBeNull();
        p.OtherAbilities.ShouldBeNull();
        p.Interest.ShouldBeNull();
        p.EducationPlace.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Kompyuter_savodxonligi_chegaradan_tashqari_rad_etiladi(int foiz)
    {
        var p = EmployeeCompetency.Create(XodimId).Value;

        var natija = p.SetComputerLiteracy(foiz);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qobiliyat.kompyuter_foiz");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Kompyuter_savodxonligi_chegara_ichida_qoyiladi(int foiz)
    {
        var p = EmployeeCompetency.Create(XodimId).Value;

        p.SetComputerLiteracy(foiz).IsSuccess.ShouldBeTrue();
        p.ComputerLiteracyPercent.ShouldBe(foiz);
    }

    [Fact]
    public void Kompyuter_savodxonligi_null_bilan_tozalanadi()
    {
        var p = EmployeeCompetency.Create(XodimId).Value;
        p.SetComputerLiteracy(70);

        p.SetComputerLiteracy(null).IsSuccess.ShouldBeTrue();
        p.ComputerLiteracyPercent.ShouldBeNull();
    }

    [Fact]
    public void Matn_maydonlari_chetdagi_boshliqdan_tozalanadi()
    {
        var p = EmployeeCompetency.Create(XodimId).Value;

        p.SetLanguages("  Инглиз ва рус тили  ");
        p.SetSpecialization("  Ихтисодиёт ");
        p.SetOtherAbilities(" ");
        p.SetInterest("Дизайн\t");
        p.SetEducationPlace("  ТДТУ");

        p.Languages.ShouldBe("Инглиз ва рус тили");
        p.Specialization.ShouldBe("Ихтисодиёт");
        p.OtherAbilities.ShouldBeNull("faqat bo'shliqdan iborat qiymat null bo'lishi kerak");
        p.Interest.ShouldBe("Дизайн");
        p.EducationPlace.ShouldBe("ТДТУ");
    }
}
