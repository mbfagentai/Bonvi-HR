using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Oklad ustunini o'qish qoidalari.
/// </summary>
/// <remarks>
/// Bloknotning oklad ustunida to'rt xil narsa yotibdi va ularni ajratmaslik
/// eng qimmat xatoga olib kelardi: <c>«Ишбай»</c> ni <c>0</c> deb yozib
/// qo'yish odamning maoshini nolga tushirardi.
/// </remarks>
public sealed class SalaryCellTests
{
    [Fact]
    public void Son_katak_summa_beradi()
    {
        var result = SalaryCell.Parse(ExcelCell.FromNumber(4_500_000));

        result.Kind.ShouldBe(SalaryCellKind.Amount);
        result.Salary!.Amount.ShouldBe(4_500_000m);
    }

    [Fact]
    public void Boshliq_bilan_yozilgan_son_ham_oqiladi()
    {
        // Bloknotda «3 000 000» ko'rinishi juda ko'p uchraydi.
        var result = SalaryCell.Parse(ExcelCell.FromText("3 000 000"));

        result.Kind.ShouldBe(SalaryCellKind.Amount);
        result.Salary!.Amount.ShouldBe(3_000_000m);
    }

    [Fact]
    public void Uzilmaydigan_boshliq_ajratgich_deb_qabul_qilinadi()
    {
        // Excel eksportida U+00A0 tez-tez uchraydi va ko'zga oddiy probeldek ko'rinadi.
        var result = SalaryCell.Parse(ExcelCell.FromText("5 000 000"));

        result.Kind.ShouldBe(SalaryCellKind.Amount);
        result.Salary!.Amount.ShouldBe(5_000_000m);
    }

    [Theory]
    [InlineData("Ўзгаришсиз")]
    [InlineData("Ўзгаришсиз.")]
    [InlineData("ўзгаришсиз")]
    [InlineData("Узгаришсиз")]
    public void Ozgarishsiz_alohida_holat_sifatida_tanilib_olinadi(string text)
        => SalaryCell.Parse(ExcelCell.FromText(text)).Kind.ShouldBe(SalaryCellKind.Unchanged);

    [Theory]
    [InlineData("Ишбай")]
    [InlineData("ишбай")]
    public void Ishbay_alohida_holat_sifatida_taniladi(string text)
    {
        var result = SalaryCell.Parse(ExcelCell.FromText(text));

        // Ishbayda belgilangan oklad YO'Q — bo'sh maydon bu yerda to'g'ri javob.
        result.Kind.ShouldBe(SalaryCellKind.PieceRate);
        result.Salary.ShouldBeNull();
    }

    [Theory]
    [InlineData("4000000+КПИ")]
    [InlineData("Ишбай+1000000")]
    [InlineData("KPI бўйича")]
    [InlineData("01.04.2024 дан 4 000 000")]
    public void Aralash_qiymat_qisman_oqilmaydi(string text)
    {
        // 4 000 000 ni olib qo'yish mumkin edi, lekin unda KPI qismi jimgina
        // yo'qolardi va bazada noto'liq fakt qolardi.
        var result = SalaryCell.Parse(ExcelCell.FromText(text));

        result.Kind.ShouldBe(SalaryCellKind.Unknown);
        result.Salary.ShouldBeNull();
    }

    [Theory]
    [InlineData("-")]
    [InlineData("—")]
    [InlineData("Оклад")]
    public void Mano_tashimaydigan_qiymat_bosh_katak_bilan_bir_xil(string text)
    {
        // «Оклад» — sarlavhaning ma'lumot qatoriga tushib qolgani (87 marta).
        // Uni «tushunarsiz» deb belgilash 87 ta foydasiz hisobot satri berardi.
        SalaryCell.Parse(ExcelCell.FromText(text)).Kind.ShouldBe(SalaryCellKind.Empty);
    }

    [Fact]
    public void Ishbay_ustiga_qoshimcha_sof_ishbay_deb_qaralmaydi()
    {
        // «Ишбай+1000000» — ishbay USTIGA belgilangan qo'shimcha. Uni sof
        // ishbay deb belgilash qo'shimchani jimgina yo'qotardi.
        SalaryCell.Parse(ExcelCell.FromText("Ишбай(+1000000)")).Kind.ShouldBe(SalaryCellKind.Unknown);
    }

    [Fact]
    public void Bosh_katak_bosh_deb_qaytadi()
        => SalaryCell.Parse(ExcelCell.Empty).Kind.ShouldBe(SalaryCellKind.Empty);

    [Fact]
    public void Xom_qiymat_hisobot_uchun_saqlanadi()
        => SalaryCell.Parse(ExcelCell.FromText("KPI бўйича")).Raw.ShouldBe("KPI бўйича");

    [Fact]
    public void Manfiy_summa_qabul_qilinmaydi()
        => SalaryCell.Parse(ExcelCell.FromNumber(-5000)).Kind.ShouldBe(SalaryCellKind.Unknown);
}
