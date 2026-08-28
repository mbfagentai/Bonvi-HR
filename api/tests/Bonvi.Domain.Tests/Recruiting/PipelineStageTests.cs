using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>
/// Voronka bosqichi — ma'lumotnoma, lekin TARTIBI bor.
/// </summary>
/// <remarks>
/// Tartib aynan shu ma'lumotnomada saqlanib qolgani (qolgan sakkiztasidan
/// farqli) tasodif emas — sababi <see cref="PipelineStage.SortOrder"/>
/// izohida. Shu sababli u testda ham alohida qoplanadi.
/// </remarks>
public sealed class PipelineStageTests
{
    [Fact]
    public void Nomsiz_yaratilmaydi()
    {
        var natija = PipelineStage.Create("   ");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.nom_bosh");
    }

    [Fact]
    public void Manfiy_tartib_bilan_yaratilmaydi()
    {
        var natija = PipelineStage.Create("Кенгаш", -1);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.tartib");
    }

    [Fact]
    public void Nom_ortiqcha_boshliqlardan_tozalanadi()
    {
        var natija = PipelineStage.Create("  Телефон   суҳбат  ", 1);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Name.ShouldBe("Телефон суҳбат", "ma'lumotnoma nomi normallashtiriladi");
        natija.Value.SortOrder.ShouldBe(1);
        natija.Value.IsActive.ShouldBeTrue("yangi bosqich faol bo'lib tug'iladi");
    }

    [Fact]
    public void Tartibi_ozgartiriladi()
    {
        var bosqich = PipelineStage.Create("Кенгаш", 3).Value;

        var natija = bosqich.MoveTo(0);

        natija.IsSuccess.ShouldBeTrue();
        bosqich.SortOrder.ShouldBe(0);
    }

    [Fact]
    public void Manfiy_tartibga_kochirilmaydi()
    {
        var bosqich = PipelineStage.Create("Кенгаш", 3).Value;

        var natija = bosqich.MoveTo(-5);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("bosqich.tartib");
        bosqich.SortOrder.ShouldBe(3, "rad etilgan amal holatni o'zgartirmaydi");
    }

    [Fact]
    public void Faolsizlantirilgan_bosqich_royxatda_qoladi()
    {
        var bosqich = PipelineStage.Create("Кенгаш", 3).Value;

        bosqich.Deactivate("Кенгаш бекор қилинди");

        bosqich.IsActive.ShouldBeFalse();
        bosqich.SortOrder.ShouldBe(3, "faolsizlantirish tartibni buzmaydi");
        bosqich.Note.ShouldBe("Кенгаш бекор қилинди");
    }
}
