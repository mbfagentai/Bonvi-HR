using Bonvi.Application.Identity.EmployeeAccounts;

namespace Bonvi.Application.Tests.Identity;

/// <summary>
/// Xodim ilovasi paroli — avtomatik 6 raqam va HR yozgan parolning tekshiruvi (egasining qarori, 2026-09-27).
/// </summary>
public sealed class EmployeePasswordTests
{
    [Fact]
    public void Avtomatik_parol_olti_raqam_va_arzimas_emas()
    {
        var parollar = Enumerable.Range(0, 5_000).Select(_ => EmployeePassword.Generate()).ToList();

        parollar.ShouldAllBe(parol => parol.Length == EmployeePassword.GeneratedLength && parol.All(char.IsAsciiDigit));
        parollar.ShouldAllBe(parol => !EmployeePassword.IsTriviallyWeak(parol));

        // Tasodif haqiqatan tasodif: 5000 tadan kamida 4900 tasi har xil
        // (millionlik fazoda kutilgan takror ~12 ta).
        parollar.Distinct(StringComparer.Ordinal).Count().ShouldBeGreaterThan(4_900);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("777777")]
    [InlineData("123456")]
    [InlineData("012345")]
    [InlineData("456789")]
    [InlineData("654321")]
    [InlineData("987654")]
    [InlineData("543210")]
    [InlineData("121212")]
    [InlineData("909090")]
    [InlineData("123123")]
    [InlineData("258258")]
    public void Arzimas_birikmalar_tanib_olinadi(string parol)
        => EmployeePassword.IsTriviallyWeak(parol).ShouldBeTrue();

    [Theory]
    [InlineData("583920")]
    [InlineData("102938")]
    [InlineData("135790")]
    [InlineData("112233")]
    [InlineData("123457")]
    public void Oddiy_parollar_arzimas_emas(string parol)
        => EmployeePassword.IsTriviallyWeak(parol).ShouldBeFalse();

    [Theory]
    [InlineData(null, "xodim.parol_bosh")]
    [InlineData("", "xodim.parol_bosh")]
    [InlineData("     ", "xodim.parol_bosh")]
    [InlineData("12345", "xodim.parol_qisqa")]
    [InlineData("  1234 ", "xodim.parol_qisqa")]
    public void HR_parolining_xatolari(string? parol, string kod)
        => EmployeePassword.Validate(parol).Error.Code.ShouldBe(kod);

    [Fact]
    public void HR_paroli_uzunlik_chegarasi()
    {
        EmployeePassword.Validate(new string('x', EmployeePassword.MaxLength)).IsSuccess.ShouldBeTrue();
        EmployeePassword.Validate(new string('x', EmployeePassword.MaxLength + 1)).Error.Code.ShouldBe("xodim.parol_uzun");
    }

    [Fact]
    public void HR_paroli_chetdagi_boshliqlarsiz_saqlanadi()
    {
        EmployeePassword.Validate("  Bahor 2026 ").Value.ShouldBe("Bahor 2026");
    }

    /// <summary>
    /// HR yozgan arzimas raqamli parol RAD etiladi — ilova internetga ochiq, bunday
    /// PIN hujumchi birinchi terib ko'radigan qiymat (2026-10-12).
    /// </summary>
    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("654321")]
    [InlineData("121212")]
    [InlineData("123123")]
    [InlineData(" 777777 ")]
    public void HR_yozgan_arzimas_parol_rad_etiladi(string parol)
        => EmployeePassword.Validate(parol).Error.Code.ShouldBe("xodim.parol_oddiy");

    /// <summary>Oddiy bo'lmagan raqamli va harfli parollar o'tadi.</summary>
    [Theory]
    [InlineData("777691")]
    [InlineData("482913")]
    [InlineData("4829135")]
    [InlineData("aaaaaa")]
    public void HR_yozgan_oddiy_bolmagan_parol_qabul_qilinadi(string parol)
        => EmployeePassword.Validate(parol).IsSuccess.ShouldBeTrue();
}
