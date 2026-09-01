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
    public void Tillar_royxati_qoyiladi()
    {
        var p = EmployeeCompetency.Create(XodimId).Value;

        var natija = p.SetLanguageSkills(
        [
            new LanguageSkill(LanguageCode.English, LanguageLevel.Advanced),
            new LanguageSkill(LanguageCode.Russian, LanguageLevel.Native),
        ]);

        natija.IsSuccess.ShouldBeTrue();
        p.LanguageSkills.Count.ShouldBe(2);
        p.LanguageSkills.ShouldContain(l => l.Language == LanguageCode.English && l.Level == LanguageLevel.Advanced);
        p.LanguageSkills.ShouldContain(l => l.Language == LanguageCode.Russian && l.Level == LanguageLevel.Native);
    }

    [Fact]
    public void Takror_til_rad_etiladi_va_royxat_ozgarmaydi()
    {
        // «Ingliz — Elementary» va «Ingliz — Advanced» birga tursa,
        // «qay darajada biladi?» savoli javobsiz qolardi — butun amal rad.
        var p = EmployeeCompetency.Create(XodimId).Value;
        p.SetLanguageSkills([new LanguageSkill(LanguageCode.Uzbek, LanguageLevel.Native)]);

        var natija = p.SetLanguageSkills(
        [
            new LanguageSkill(LanguageCode.English, LanguageLevel.Elementary),
            new LanguageSkill(LanguageCode.English, LanguageLevel.Advanced),
        ]);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qobiliyat.til_takror");
        p.LanguageSkills.Count.ShouldBe(1, "rad etilgan amal ro'yxatni yarim yo'lda qoldirmasligi kerak");
        p.LanguageSkills[0].Language.ShouldBe(LanguageCode.Uzbek);
    }

    [Theory]
    [InlineData(99, (int)LanguageLevel.Advanced)]
    [InlineData((int)LanguageCode.English, 0)]
    [InlineData((int)LanguageCode.English, 6)]
    public void Royxatdan_tashqari_sanoq_qiymati_rad_etiladi(int til, int daraja)
    {
        // JSON'da sanoq nom bilan yursa ham, RAQAM yuborilsa binder uni
        // tekshirmasdan qabul qiladi — bazaga tushishidan oldin shu yerda ushlanadi.
        var p = EmployeeCompetency.Create(XodimId).Value;

        var natija = p.SetLanguageSkills([new LanguageSkill((LanguageCode)til, (LanguageLevel)daraja)]);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("qobiliyat.til_notogri");
        p.LanguageSkills.ShouldBeEmpty();
    }

    [Fact]
    public void Qayta_qoyish_royxatni_butunligicha_almashtiradi()
    {
        var p = EmployeeCompetency.Create(XodimId).Value;
        p.SetLanguageSkills(
        [
            new LanguageSkill(LanguageCode.English, LanguageLevel.Elementary),
            new LanguageSkill(LanguageCode.Tajik, LanguageLevel.Intermediate),
        ]);
        var inglizId = p.LanguageSkills.Single(l => l.Language == LanguageCode.English).Id;

        // Ingliz qoladi (darajasi o'sadi), tojik chiqib ketadi, koreys qo'shiladi.
        var natija = p.SetLanguageSkills(
        [
            new LanguageSkill(LanguageCode.English, LanguageLevel.Advanced),
            new LanguageSkill(LanguageCode.Korean, LanguageLevel.Elementary),
        ]);

        natija.IsSuccess.ShouldBeTrue();
        p.LanguageSkills.Count.ShouldBe(2);
        p.LanguageSkills.ShouldNotContain(l => l.Language == LanguageCode.Tajik);

        var ingliz = p.LanguageSkills.Single(l => l.Language == LanguageCode.English);
        ingliz.Level.ShouldBe(LanguageLevel.Advanced);
        ingliz.Id.ShouldBe(inglizId, "qolgan til o'chirilib qayta yaratilmasligi kerak — Id barqaror");
    }

    [Fact]
    public void Bosh_royxat_tillarni_tozalaydi_eski_matnga_tegmaydi()
    {
        // Eski erkin matn (Excel'dan ko'chgan) strukturaviy ro'yxatdan
        // MUSTAQIL yashaydi — uni faqat `SetLanguages` o'zgartiradi.
        var p = EmployeeCompetency.Create(XodimId).Value;
        p.SetLanguages("Рус тили , Инглиз тили");
        p.SetLanguageSkills([new LanguageSkill(LanguageCode.Russian, LanguageLevel.Advanced)]);

        p.SetLanguageSkills([]).IsSuccess.ShouldBeTrue();

        p.LanguageSkills.ShouldBeEmpty();
        p.Languages.ShouldBe("Рус тили , Инглиз тили");
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
