namespace Bonvi.IsupGateway.Tests;

/// <summary>Sozlama tekshiruvi — terminalning o'z chegaralari va sirlarni oshkor qilmaslik.</summary>
public sealed class IsupOptionsValidatorTests
{
    private readonly IsupOptionsValidator _validator = new();

    [Fact]
    public void Togri_sozlama_otadi()
        => _validator.Validate(null, Options()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Terminalsiz_sozlama_ham_otadi()
    {
        var options = Options();
        options.Devices.Clear();

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("zavod 1")]
    [InlineData("zavod.1")]
    [InlineData("123456789012345678901234567890123")]
    public void Notogri_terminal_ID_rad_etiladi(string deviceId)
    {
        var options = Options();
        options.Devices[0].DeviceId = deviceId;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Takror_terminal_ID_rad_etiladi()
    {
        var options = Options();
        options.Devices.Add(new IsupDeviceOptions { DeviceId = "zavod-1", Key = "Boshqa-Kalit-01", Token = "t2" });

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("takrorlangan");
    }

    [Theory]
    [InlineData("qisqa")]
    [InlineData("juda-uzun-kalit-17x")]
    [InlineData("bo'shliq bor")]
    [InlineData("kirillcha-калит")]
    public void Notogri_kalit_rad_etiladi_va_xabarda_korsatilmaydi(string key)
    {
        var options = Options();
        options.Devices[0].Key = key;

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotContain(key);
    }

    [Fact]
    public void Tokensiz_terminal_rad_etiladi()
    {
        var options = Options();
        options.Devices[0].Token = " ";

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://hr.bonvi.uz/isup-face")]
    [InlineData("ftp://hr.bonvi.uz/isup-face/")]
    [InlineData("isup-face/")]
    public void Rasm_manzili_notogri_bolsa_rad_etiladi(string faceUrlBase)
    {
        var options = Options();
        options.FaceUrlBase = faceUrlBase;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Oddiy_HTTP_rasm_manzili_ham_mumkin()
    {
        var options = Options();
        options.FaceUrlBase = "http://hr.bonvi.uz/isup-face/";

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    private static IsupOptions Options() => new()
    {
        FaceUrlBase = "https://hr.bonvi.uz/isup-face/",
        ApiBaseUrl = "http://api:8080",
        Devices =
        {
            new IsupDeviceOptions { DeviceId = "zavod-1", Key = "Kalit-1234567890", Token = "token-1" },
        },
    };
}
