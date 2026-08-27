namespace Bonvi.Etl.Tests;

/// <summary>
/// Buyruq satri qoidalari.
/// </summary>
/// <remarks>
/// Bu vositadagi eng qimmat xato — tasodifan bazaga yozib yuborish.
/// Shu sababli himoya qoidalari testda qadalgan, hujjatda emas.
/// </remarks>
public sealed class EtlOptionsTests
{
    private const string Source = "/data/xodimlar.xlsx";

    [Fact]
    public void Standart_rejim_quruq_yurish()
    {
        var (options, error) = EtlOptions.Parse(["--source", Source]);

        error.ShouldBeNull();
        options!.Apply.ShouldBeFalse();
    }

    [Fact]
    public void Yozish_uchun_ulanish_satri_majburiy()
    {
        // «--apply» ni yozib, «--connection» ni unutish — qayerga yozilayotgani
        // noaniq qoladi. Bunday buyruq umuman bajarilmasligi kerak.
        var (options, error) = EtlOptions.Parse(["--source", Source, "--apply"]);

        options.ShouldBeNull();
        error!.ShouldContain("--connection");
    }

    [Fact]
    public void Apply_va_dry_run_birga_berilmaydi()
    {
        var (options, error) = EtlOptions.Parse(
            ["--source", Source, "--connection", "Host=db", "--apply", "--dry-run"]);

        options.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void Manba_korsatilmasa_xato()
    {
        var (options, error) = EtlOptions.Parse(["--limit", "10"]);

        options.ShouldBeNull();
        error!.ShouldContain("--source");
    }

    [Fact]
    public void Nomalum_parametr_jimgina_etiborsiz_qoldirilmaydi()
    {
        // Xato yozilgan parametr jim tashlab yuborilsa, foydalanuvchi
        // buyruq bajarildi deb o'ylardi — aslida boshqa narsa bajarilgan.
        var (options, error) = EtlOptions.Parse(["--source", Source, "--force"]);

        options.ShouldBeNull();
        error!.ShouldContain("--force");
    }

    [Theory]
    [InlineData("employees", EtlStep.Employees)]
    [InlineData("xodimlar", EtlStep.Employees)]
    [InlineData("events,flags", EtlStep.Events | EtlStep.Flags)]
    [InlineData("hodisalar,belgilar", EtlStep.Events | EtlStep.Flags)]
    [InlineData("profiles", EtlStep.Profiles)]
    [InlineData("profil", EtlStep.Profiles)]
    [InlineData("all", EtlStep.All)]
    public void Qadamlar_royxati_ikki_tilda_oqiladi(string value, EtlStep expected)
    {
        var (options, error) = EtlOptions.Parse(["--source", Source, "--steps", value]);

        error.ShouldBeNull();
        options!.Steps.ShouldBe(expected);
    }

    [Fact]
    public void Nomalum_qadam_xato_beradi()
    {
        var (options, error) = EtlOptions.Parse(["--source", Source, "--steps", "oklad"]);

        options.ShouldBeNull();
        error!.ShouldContain("oklad");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("kop")]
    public void Notogri_cheklov_qabul_qilinmaydi(string value)
    {
        var (options, _) = EtlOptions.Parse(["--source", Source, "--limit", value]);

        options.ShouldBeNull();
    }

    [Fact]
    public void Standart_yuridik_shaxs_kodi_bosh_qoldirilishi_mumkin()
    {
        // Bo'sh satr — «dalilsiz mehnat munosabati umuman yaratilmasin» degani.
        var (options, error) = EtlOptions.Parse(["--source", Source, "--default-legal-entity", ""]);

        error.ShouldBeNull();
        options!.DefaultLegalEntityCode.ShouldBe(string.Empty);
    }

    [Fact]
    public void Tanlangan_qadam_Has_orqali_bilinadi()
    {
        var (options, _) = EtlOptions.Parse(["--source", Source, "--steps", "events"]);

        options!.Has(EtlStep.Events).ShouldBeTrue();
        options.Has(EtlStep.Employees).ShouldBeFalse();
    }

    [Fact]
    public void Har_bir_qadam_All_ichiga_kiradi()
    {
        // Yangi qadam qo'shilib, `All` ga kiritilmasa — u standart yugurishda
        // JIMGINA bajarilmay qolardi va buni hech kim sezmasdi: hisobotda
        // qadam umuman ko'rinmaydi, xato ham chiqmaydi. Aynan shunday xato
        // profil boyitish qadamida bir marta sodir bo'lgan, shu sababli
        // qoida testga qadaldi.
        foreach (var step in Enum.GetValues<EtlStep>())
        {
            if (step is EtlStep.None or EtlStep.All)
            {
                continue;
            }

            EtlStep.All.HasFlag(step).ShouldBeTrue($"«{step}» qadami «All» ga kiritilmagan.");
        }
    }

    [Fact]
    public void Qadam_korsatilmasa_hammasi_bajariladi()
    {
        var (options, _) = EtlOptions.Parse(["--source", Source]);

        options!.Has(EtlStep.Profiles).ShouldBeTrue();
    }
}
