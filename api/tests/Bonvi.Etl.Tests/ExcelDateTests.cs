using Bonvi.Etl.Excel;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Excel sana seriyasini o'qish qoidalari.
/// </summary>
/// <remarks>
/// Bu qoidaning narxi yuqori: bir kunga adashgan sana Excel'da ko'rinmaydi,
/// lekin mehnat stajini, sinov muddatini va pensiya hisobini buzadi.
/// </remarks>
public sealed class ExcelDateTests
{
    [Fact]
    public void Odatiy_seriya_togri_sanaga_aylanadi()
    {
        // 46212 — bloknotda ko'p uchraydigan qiymat.
        ExcelDate.FromSerial(46212).ShouldBe(new DateOnly(2026, 7, 9));
    }

    [Fact]
    public void Excelning_mavjud_bolmagan_1900_02_29_seriyasi_rad_etiladi()
    {
        // Excel 1900-yilni kabisa deb hisoblaydi. 60-seriya — mavjud bo'lmagan kun.
        ExcelDate.FromSerial(60).ShouldBeNull();
    }

    [Fact]
    public void Kabisa_xatosidan_oldingi_va_keyingi_seriyalar_bir_kunga_surilmaydi()
    {
        ExcelDate.FromSerial(59).ShouldBe(new DateOnly(1900, 2, 28));
        ExcelDate.FromSerial(61).ShouldBe(new DateOnly(1900, 3, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2_958_466)]
    public void Chegaradan_tashqaridagi_seriya_rad_etiladi(double serial)
        => ExcelDate.FromSerial(serial).ShouldBeNull();

    [Fact]
    public void Kasr_qism_tashlab_yuboriladi()
    {
        // Excel'da kasr qism — kun ichidagi vaqt, sana uchun ahamiyatsiz.
        ExcelDate.FromSerial(46212.75).ShouldBe(ExcelDate.FromSerial(46212));
    }

    [Fact]
    public void Ishonchsiz_kichik_son_sana_deb_qabul_qilinmaydi()
    {
        // «Ишлаган вакти (кун)» ustunidan tushib qolgan 69 ham to'g'ri seriya,
        // lekin u 1900-yilning martiga aylanadi — bu yolg'on ma'lumot bo'lardi.
        ExcelDate.Read(ExcelCell.FromNumber(69)).ShouldBeNull();
    }

    [Fact]
    public void Ishonchlilik_tekshiruvi_ochirilsa_kichik_son_ham_oqiladi()
        => ExcelDate.Read(ExcelCell.FromNumber(69), requirePlausible: false)
            .ShouldBe(new DateOnly(1900, 3, 9));

    [Fact]
    public void Haqiqiy_sana_tipidagi_katak_ozgarishsiz_qaytadi()
    {
        var date = new DateOnly(2025, 2, 17);
        ExcelDate.Read(ExcelCell.FromDate(date)).ShouldBe(date);
    }

    [Theory]
    [InlineData("17.02.2025")]
    [InlineData("17/02/2025")]
    [InlineData("2025-02-17")]
    [InlineData("17-02-2025")]
    public void Matn_korinishidagi_sana_oqiladi(string text)
        => ExcelDate.Read(ExcelCell.FromText(text)).ShouldBe(new DateOnly(2025, 2, 17));

    [Fact]
    public void Matn_bolib_saqlangan_seriya_ham_oqiladi()
    {
        // «91.5» varag'ida sinov muddati ustuni matn sifatida saqlangan.
        ExcelDate.Read(ExcelCell.FromText("45413")).ShouldBe(new DateOnly(2024, 5, 1));
    }

    [Fact]
    public void Sana_bolmagan_matn_null_qaytaradi()
        => ExcelDate.Read(ExcelCell.FromText("Тугатилган")).ShouldBeNull();

    [Fact]
    public void Bosh_katak_null_qaytaradi()
        => ExcelDate.Read(ExcelCell.Empty).ShouldBeNull();
}
