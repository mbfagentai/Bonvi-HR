using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>
/// Vakansiya nomi — e'londa turadigan qisqa nom.
/// </summary>
/// <remarks>
/// Ma'lumotnoma sifatida u boshqa oltitasi bilan bir xil ishlaydi, shu
/// sababli bu yerda faqat O'ZIGA xos ikkita savol tekshiriladi: nomsiz
/// yozuv bo'lmaydi va nom <see cref="Domain.Common.ReferenceEntity"/>
/// ning tozalash qoidasidan o'tadi (Excel'dan ko'chirilgan matnlarda
/// ikkilangan bo'shliq odatiy hol).
/// </remarks>
public sealed class VacancyTitleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nomsiz_yaratilmaydi(string? nom)
    {
        var natija = VacancyTitle.Create(nom);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya_nomi.nom_bosh");
    }

    [Fact]
    public void Nom_tozalanadi_va_yozuv_faol_boshlanadi()
    {
        var natija = VacancyTitle.Create("  Сотувчи-консультант  ");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Name.ShouldBe("Сотувчи-консультант");
        natija.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Ichki_takroriy_boshliq_yigiladi()
    {
        // «Сотувчи  консультант» va «Сотувчи консультант» bitta e'lon
        // nomi bo'lishi kerak: aks holda ro'yxatda ikkita bir xil ko'ringan
        // qator turardi va unikal indeks ularni ushlab qololmasdi.
        var natija = VacancyTitle.Create("Сотувчи  консультант");

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Name.ShouldBe("Сотувчи консультант");
    }

    [Fact]
    public void Yozuv_ochirilmaydi_faqat_faolsizlantiriladi()
    {
        // Eski vakansiyalarda unga havola qolgan — ma'lumotnomaning
        // umumiy qoidasi bu yerda ham amal qiladi.
        var nom = VacancyTitle.Create("Ҳайдовчи").Value;

        nom.Deactivate("Бу лавозимга одам изланмайди");

        nom.IsActive.ShouldBeFalse();
        nom.Note.ShouldBe("Бу лавозимга одам изланмайди");
    }
}
