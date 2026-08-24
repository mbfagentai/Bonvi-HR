using System.Text;
using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Alias jadvalining uchta javobi.
/// </summary>
/// <remarks>
/// Ularni farqlash butun hisobotning aniqligini belgilaydi: «jadvalda bor,
/// lekin ataylab bog'lanmagan» qiymat hisobotga tushmasligi kerak (bu
/// kutilgan hol, 400 dan ortiq katak shunday), «jadvalda umuman yo'q»
/// qiymat esa albatta tushishi kerak.
/// </remarks>
public sealed class AliasTableTests
{
    private const string Json = """
        {
          "positions": {
            "Коровул": "qorovul",
            "Қоровул": "qorovul",
            "Юк ташувчи": "yuk-tashuvchi",
            "46188.0": null,
            "Лавозими": null
          },
          "directions": {
            "Вело, метан, шина": "velo|metan|shina",
            "Вело": "velo"
          },
          "unmapped": ["tushunarsiz qiymat"]
        }
        """;

    private static AliasTable Table()
        => AliasTable.Parse(new MemoryStream(Encoding.UTF8.GetBytes(Json)));

    [Fact]
    public void Kod_topilsa_qaytariladi()
    {
        var match = Table().Position(ExcelCell.FromText("Коровул"));

        match.Known.ShouldBeTrue();
        match.First.ShouldBe("qorovul");
    }

    [Fact]
    public void Kirill_imlo_variantlari_bir_kodga_boglanadi()
        => Table().Position(ExcelCell.FromText("Қоровул")).First.ShouldBe("qorovul");

    [Fact]
    public void Registr_va_ortiqcha_boshliq_hisobga_olinmaydi()
    {
        // Bloknotda bir xil qiymat «Юк ташувчи» va «юк  ташувчи» ko'rinishida ham bor.
        Table().Position(ExcelCell.FromText("юк  ташувчи")).First.ShouldBe("yuk-tashuvchi");
    }

    [Fact]
    public void Ataylab_boglanmagan_qiymat_malum_deb_qaytadi()
    {
        // «Лавозими» — sarlavha qoldig'i. U ma'lumotnomaga tegishli emas,
        // lekin NOMA'LUM ham emas: hisobotga tushmasligi kerak.
        var match = Table().Position(ExcelCell.FromText("Лавозими"));

        match.Known.ShouldBeTrue();
        match.HasCode.ShouldBeFalse();
    }

    [Fact]
    public void Jadvalda_yoq_qiymat_nomalum_deb_qaytadi()
    {
        var match = Table().Position(ExcelCell.FromText("Космонавт"));

        match.Known.ShouldBeFalse();
        match.HasCode.ShouldBeFalse();
    }

    [Fact]
    public void Python_yozgan_son_kaliti_ClosedXML_korinishida_ham_topiladi()
    {
        // Jadval Python bilan tuzilgan va u yerda son katagi «46188.0» edi,
        // ClosedXML esa «46188» beradi. Ikkinchi shakl ham sinaladi.
        Table().Position(ExcelCell.FromNumber(46188)).Known.ShouldBeTrue();
    }

    [Fact]
    public void Bitta_katak_bir_nechta_kodga_boglanishi_mumkin()
    {
        // Yo'nalish kombinatsiyasi bloknotda odatiy hol.
        var match = Table().Direction(ExcelCell.FromText("Вело, метан, шина"));

        match.Codes.Count.ShouldBe(3);
        match.Codes.ShouldContain("metan");
    }

    [Fact]
    public void Bosh_katak_hisobotga_tushmaydi()
    {
        var match = Table().Position(ExcelCell.Empty);

        match.Known.ShouldBeTrue();
        match.HasCode.ShouldBeFalse();
    }

    [Fact]
    public void Unmapped_bolimi_qidiruvga_aralashmaydi()
    {
        // U massivlardan iborat va ko'chirishga kerak emas — jadval
        // uni o'qishda jimgina tashlab ketishi kerak.
        Should.NotThrow(Table);
    }

    [Fact]
    public void Kalit_normallashtirish_boshliq_va_registrni_olib_tashlaydi()
        => AliasTable.Key("  Маркетинг   Бўлими ").ShouldBe("маркетинг бўлими");
}
