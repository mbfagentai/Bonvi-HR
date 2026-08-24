using Bonvi.Etl.Excel;
using Bonvi.Etl.Mapping;

namespace Bonvi.Etl.Tests;

/// <summary>
/// Telefon katagini ajratish qoidalari.
/// </summary>
/// <remarks>
/// Bloknotda 13 ta katakda ikkita raqam, bittasida to'rtta raqam bor va
/// ajratgichlar har xil. Ajratmaslik butun katakni yaroqsiz qilardi:
/// «947026789/905083221» hech qanday telefon raqamiga aylanmaydi.
/// </remarks>
public sealed class PhoneCellTests
{
    [Fact]
    public void Bitta_raqam_asosiy_bolib_qaytadi()
    {
        var result = PhoneCell.Parse(ExcelCell.FromNumber(905088888));

        result.Primary.ShouldNotBeNull();
        result.Second.ShouldBeNull();
        result.Rejected.ShouldBeEmpty();
    }

    [Fact]
    public void Ikkita_raqam_ajratiladi_birinchisi_asosiy()
    {
        var result = PhoneCell.Parse(ExcelCell.FromText("972434870/335972525"));

        result.Primary.ShouldNotBeNull();
        result.Second.ShouldNotBeNull();
        result.Primary!.Value.ShouldNotBe(result.Second!.Value);
    }

    [Fact]
    public void Uchinchi_va_keyingi_raqamlar_yoqolmaydi_alohida_qaytadi()
    {
        // Modelda ikkita raqam uchun joy bor. Qolganini jimgina tashlab
        // yuborish o'rniga hisobotga chiqarish kerak.
        var result = PhoneCell.Parse(
            ExcelCell.FromText("935599355/944491527/941341341/948152998"));

        result.Primary.ShouldNotBeNull();
        result.Second.ShouldNotBeNull();
        result.Extra.Count.ShouldBe(2);
    }

    [Fact]
    public void Bir_xil_raqam_ikki_marta_yozilgan_bolsa_bir_marta_olinadi()
    {
        var result = PhoneCell.Parse(ExcelCell.FromText("905088888 905088888"));

        result.Primary.ShouldNotBeNull();
        result.Second.ShouldBeNull();
    }

    [Fact]
    public void Raqamga_aylanmagan_bolak_jimgina_tashlanmaydi()
    {
        // «Тели йўқ» kabi yozuvlar hisobotda ko'rinishi kerak.
        var result = PhoneCell.Parse(ExcelCell.FromText("Тели йўқ"));

        result.Primary.ShouldBeNull();
        result.Rejected.ShouldNotBeEmpty();
    }

    [Fact]
    public void Bosh_katak_bosh_natija_beradi()
    {
        var result = PhoneCell.Parse(ExcelCell.Empty);

        result.Primary.ShouldBeNull();
        result.Rejected.ShouldBeEmpty();
    }
}
