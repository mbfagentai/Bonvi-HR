using System.Text.RegularExpressions;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>
/// HRMentor manzili (SPEC-blankalar-hrmentor §2): tashqi tarmoqda faqat https, shifrsiz
/// http faqat shu mashina ichida. Sozlama naqshi va <see cref="HrMentorConnection.From"/>
/// bir xil qaror chiqarishi shart — biri o'tkazib, ikkinchisi rad etsa, saqlangan
/// manzil «ulangan» ko'rinib, hech qachon ishlamas edi.
/// </summary>
public sealed class HrMentorConnectionTests
{
    private const string Kalit = "hrm_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Theory]
    [InlineData("https://hrmentor.bonvi.uz")]
    [InlineData("https://hrmentor.bonvi.uz/")]
    [InlineData("http://localhost:8000")]
    [InlineData("http://127.0.0.1:8290")]
    [InlineData("http://[::1]:8000")]
    [InlineData("http://host.docker.internal:8000")]
    [InlineData("http://host.docker.internal")]
    public void Ruxsat_etilgan_manzil_naqshdan_ham_ulanishdan_ham_otadi(string manzil)
    {
        Regex.IsMatch(manzil, SystemSettingKeys.HrMentorUrlPattern).ShouldBeTrue();

        var ulanish = HrMentorConnection.From(manzil, Kalit);

        ulanish.ShouldNotBeNull();
        ulanish.BaseUrl.AbsoluteUri.ShouldStartWith(manzil.TrimEnd('/'));
    }

    [Theory]
    [InlineData("http://hrmentor.bonvi.uz")]
    [InlineData("http://10.0.0.5:8000")]
    [InlineData("http://localhost@evil.example")]
    [InlineData("http://127.0.0.1.evil.example")]
    [InlineData("http://host.docker.internal.evil.example")]
    [InlineData("ftp://localhost")]
    [InlineData("localhost:8000")]
    public void Tashqi_http_va_boshqa_sxema_ikkalasida_ham_rad(string manzil)
    {
        Regex.IsMatch(manzil, SystemSettingKeys.HrMentorUrlPattern).ShouldBeFalse();
        HrMentorConnection.From(manzil, Kalit).ShouldBeNull();
    }

    [Fact]
    public void Manzil_yoki_kalit_bosh_bolsa_ulanish_ochiq()
    {
        HrMentorConnection.From("", Kalit).ShouldBeNull();
        HrMentorConnection.From("https://hrmentor.bonvi.uz", " ").ShouldBeNull();
    }

    [Fact]
    public void Ulanish_matnida_kalit_korinmaydi()
    {
        var ulanish = HrMentorConnection.From("https://hrmentor.bonvi.uz", Kalit);

        ulanish.ShouldNotBeNull();
        ulanish.ToString().ShouldNotContain(Kalit);
    }
}
