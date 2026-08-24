using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;

namespace Bonvi.Etl.Tests;

/// <summary>
/// «СИНОВ МУДДАТИ» ustunini o'qish qoidalari.
/// </summary>
/// <remarks>
/// Arxiv varag'ida bu ustun siljib ketgan va 400 dan ortiq katakda lavozim
/// nomi yotibdi. Aynan shu sababli «tushunarsiz» holat alohida ajratilgan:
/// noto'g'ri ustundan olingan qiymatni sinov muddati deb yozib qo'yish
/// bo'sh maydondan yomonroq bo'lardi.
/// </remarks>
public sealed class ProbationCellTests
{
    [Fact]
    public void Bosh_katak_bosh_deb_qaytadi()
        => ProbationCell.Parse(ExcelCell.Empty).Kind.ShouldBe(ProbationCellKind.Empty);

    [Fact]
    public void Sana_seriyasi_tugash_sanasi_deb_oqiladi()
    {
        var result = ProbationCell.Parse(ExcelCell.FromNumber(46327));

        result.Kind.ShouldBe(ProbationCellKind.EndDate);
        result.EndDate.ShouldBe(new DateOnly(2026, 11, 1));
    }

    [Theory]
    [InlineData("Тугатилган")]
    [InlineData("тугатилган")]
    [InlineData("Тўгатилган")]
    [InlineData("Тугатилди")]
    public void Tugatilgan_sozining_barcha_yozilishi_taniladi(string text)
        => ProbationCell.Parse(ExcelCell.FromText(text)).Kind.ShouldBe(ProbationCellKind.Completed);

    [Theory]
    [InlineData("Юк ташувчи")]
    [InlineData("Краска ходими")]
    public void Ustun_siljishidan_kelgan_lavozim_nomi_ishlatilmaydi(string text)
    {
        var result = ProbationCell.Parse(ExcelCell.FromText(text));

        result.Kind.ShouldBe(ProbationCellKind.Unknown);
        result.EndDate.ShouldBeNull();
    }

    [Fact]
    public void Xom_qiymat_hisobot_uchun_saqlanadi()
        => ProbationCell.Parse(ExcelCell.FromText("Юк ташувчи")).Raw.ShouldBe("Юк ташувчи");
}
