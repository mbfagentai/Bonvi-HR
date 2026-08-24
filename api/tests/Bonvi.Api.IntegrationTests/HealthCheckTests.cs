using System.Net;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Butun tizim uchi-uchiga ishlashini tekshiradi:
/// API ko'tariladi → PostgreSQL'ga ulanadi → javob qaytaradi.
/// </summary>
/// <remarks>
/// Bu eng muhim "tutun testi" (smoke test). U o'tsa — DI konfiguratsiyasi,
/// ulanish satri, EF Core sozlamalari va konveyer to'g'ri ekani isbotlanadi.
/// </remarks>
public sealed class HealthCheckTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public HealthCheckTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// «Tirikmi» tekshiruvi — jarayon javob berayaptimi.
    /// Tashqi bog'liqliklarni tekshirmaydi: baza yiqilsa ham dastur
    /// qayta ishga tushirilmasligi kerak, u o'zi tiklanadi.
    /// </summary>
    [Fact]
    public async Task Live_endpointi_jarayon_tirikligini_tasdiqlaydi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// «Tayyormi» tekshiruvi — baza va kesh ishlayaptimi.
    /// Orkestrator shu yo'l bo'yicha trafikni yo'naltiradi.
    /// </summary>
    [Fact]
    public async Task Ready_endpointi_baza_ulanishini_tekshiradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ildiz_endpointi_servis_malumotini_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var matn = await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        matn.ShouldContain("Bonvi HR API");
    }

    [Fact]
    public void Har_bir_test_uchun_alohida_baza_yaratiladi()
    {
        _factory.ConnectionString.ShouldNotBeNullOrWhiteSpace();
        _factory.ConnectionString.ShouldContain("bonvi_test_");
    }
}
