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
    public void Nomsiz_fayl_standart_nom_oladi_uzuni_esa_qisqaradi()
    {
        // Ustun 255 belgi — undan uzun nom saqlashda 500 bilan yiqilardi
        EmployeeCheckFile.Attach(XodimId, "k", "  ", "image/png", 1).Value.FileName.ShouldBe("material");

        var uzun = new string('a', 300) + ".png";
        EmployeeCheckFile.Attach(XodimId, "k", uzun, "image/png", 1).Value.FileName.Length
            .ShouldBe(EmployeeCheckFile.MaxFileNameLength);
    }
}
