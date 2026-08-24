using Bonvi.Etl.Excel;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Excel katagini ko'chirishga tushunarli ko'rinishga o'tkazish qoidalari.
/// </summary>
public sealed class ExcelCellTests
{
    [Fact]
    public void Katta_son_eksponentasiz_yoziladi()
    {
        // JSHSHIR va telefon Excel'da son bo'lib yotibdi. Standart
        // `ToString()` ularni «3.15E13» qilib yuboradi va qiymat buziladi.
        ExcelCell.FromNumber(31501914320045d).Text.ShouldBe("31501914320045");
    }

    [Fact]
    public void Telefon_raqami_ham_butunligicha_qoladi()
        => ExcelCell.FromNumber(905088888d).Text.ShouldBe("905088888");

    [Fact]
    public void Butun_son_katak_deb_taniladi()
    {
        ExcelCell.FromNumber(46212).IsWholeNumber.ShouldBeTrue();
        ExcelCell.FromNumber(46212.5).IsWholeNumber.ShouldBeFalse();
    }

    [Fact]
    public void Ichki_ortiqcha_boshliqlar_bitta_probelga_keltiriladi()
        => ExcelCell.Normalize("Маркетинг   бўлими").ShouldBe("Маркетинг бўлими");

    [Fact]
    public void Tabulyatsiya_va_uzilmaydigan_boshliq_ham_ajratgich_deb_qaraladi()
    {
        // Excel eksportida ular oddiy probeldek ko'rinadi, lekin alias
        // jadvalidagi kalitga mos kelmaydi — aynan shu bloknotdagi
        // tarqoqlikning bir sababi edi.
        ExcelCell.Normalize("Маркетинг\tбўлими").ShouldBe("Маркетинг бўлими");
        ExcelCell.Normalize("Маркетинг бўлими").ShouldBe("Маркетинг бўлими");
    }

    [Fact]
    public void Faqat_boshliqdan_iborat_matn_bosh_katak_beradi()
        => ExcelCell.FromText("   ").IsEmpty.ShouldBeTrue();

    [Fact]
    public void Bosh_katakning_matni_bosh_satr()
    {
        ExcelCell.Empty.Text.ShouldBe(string.Empty);
        ExcelCell.Empty.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Sana_katak_kunni_saqlaydi()
    {
        var cell = ExcelCell.FromDate(new DateOnly(2025, 2, 17));

        cell.Date.ShouldBe(new DateOnly(2025, 2, 17));
        cell.Text.ShouldBe("17.02.2025");
    }
}
