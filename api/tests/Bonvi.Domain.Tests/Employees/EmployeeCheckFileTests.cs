using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Tests.Employees;

/// <summary>
/// Tekshiruv materiali — «Tekshiruvlar» kartasidagi dalil fayli.
/// </summary>
public sealed class EmployeeCheckFileTests
{
    private static readonly Guid XodimId = Guid.CreateVersion7();

    [Fact]
    public void Tekshirilgan_fayl_biriktiriladi()
    {
        var natija = EmployeeCheckFile.Attach(XodimId, " kalit-1 ", " mehnat.png ", "image/png", 1024);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.EmployeeId.ShouldBe(XodimId);
        natija.Value.FileKey.ShouldBe("kalit-1");
        natija.Value.FileName.ShouldBe("mehnat.png");
        natija.Value.SizeBytes.ShouldBe(1024);
    }

    [Theory]
    [InlineData("", "image/png", 10)]
    [InlineData("kalit", " ", 10)]
    [InlineData("kalit", "image/png", 0)]
    public void Faylsiz_material_biriktirilmaydi(string kalit, string tur, long hajm)
    {
        var natija = EmployeeCheckFile.Attach(XodimId, kalit, "a.png", tur, hajm);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("hujjat.fayl_bosh");
    }

    [Fact]
    public void Xodimsiz_material_biriktirilmaydi()
    {
        EmployeeCheckFile.Attach(Guid.Empty, "kalit", "a.png", "image/png", 10)
            .Error.Code.ShouldBe("tekshiruv_fayli.xodim_yoq");
    }

    [Fact]
    public void Nomsiz_fayl_standart_nom_oladi()
    {
        EmployeeCheckFile.Attach(XodimId, "k", "  ", "image/png", 1).Value.FileName.ShouldBe("material");
        EmployeeCheckFile.Attach(XodimId, "k", "C:\\Users\\hr\\", "image/png", 1).Value.FileName.ShouldBe("material");
    }

    [Theory]
    [InlineData("C:\\Users\\hr\\Desktop\\dalil.pdf")]
    [InlineData("/home/hr/dalil.pdf")]
    public void Yol_olib_tashlanadi(string nom)
    {
        // Mijoz to'liq yo'l yuborishi mumkin — ekranda faqat nom ko'rinadi
        EmployeeCheckFile.Attach(XodimId, "k", nom, "application/pdf", 1).Value.FileName.ShouldBe("dalil.pdf");
    }

    [Fact]
    public void Uzun_nom_qisqaradi_lekin_kengaytma_saqlanadi()
    {
        // Ustun 255 belgi — undan uzun nom saqlashda 500 bilan yiqilardi
        var nom = EmployeeCheckFile.Attach(XodimId, "k", new string('a', 300) + ".png", "image/png", 1).Value.FileName;

        nom.Length.ShouldBe(EmployeeCheckFile.MaxFileNameLength);
        nom.ShouldEndWith(".png");
    }

    [Fact]
    public void Kesish_emojini_yarmidan_bolmaydi()
    {
        // 250 ta harf + emoji (2 ta UTF-16 birligi) + ... — kesish nuqtasi
        // aynan emojining o'rtasiga tushadi
        var asl = new string('a', 250) + "😀" + new string('b', 20) + ".pdf";

        var nom = EmployeeCheckFile.Attach(XodimId, "k", asl, "application/pdf", 1).Value.FileName;

        nom.Length.ShouldBeLessThanOrEqualTo(EmployeeCheckFile.MaxFileNameLength);
        char.IsHighSurrogate(nom[^5]).ShouldBeFalse("yarim juftlik qolmasligi kerak");
        nom.ShouldEndWith(".pdf");
    }
}
