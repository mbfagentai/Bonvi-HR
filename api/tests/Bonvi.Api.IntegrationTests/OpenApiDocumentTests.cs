using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Ikkala OpenAPI hujjati ham ochiladi va to'g'ri bo'linadi.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Nega bu test bor.</b> 2026-08-29 da aniqlangan tuzoq: so'rov
/// rekordida <c>Guid = default</c> ko'rinishidagi sukut qiymat BUTUN
/// hujjatni 500 bilan yiqitadi. Bu faqat hujjat SO'RALGANDA ko'rinadi —
/// yig'ilish ham, boshqa testlar ham uni sezmaydi, va nosozlik
/// <c>./bonvi gen</c> ishlamay qolganda, ya'ni ancha keyin chiqadi.
/// </para>
/// <para>
/// <b>Muhit <c>Development</c> ga o'tkaziladi</b>: hujjat marshruti
/// ataylab faqat o'sha yerda ulanadi (<c>Program.cs</c>) — production'da
/// hujjat umuman berilmaydi.
/// </para>
/// </remarks>
public sealed class OpenApiDocumentTests : IClassFixture<BonviApiFactory>, IDisposable
{
    private readonly WebApplicationFactory<Program> _hujjatli;

    public OpenApiDocumentTests(BonviApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _hujjatli = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    public void Dispose() => _hujjatli.Dispose();

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/openapi/public.json")]
    public async Task Hujjat_ochiladi_va_JSON_bolib_oqiladi(string yol)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = _hujjatli.CreateClient();
        using var javob = await client.GetAsync(new Uri(yol, UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"{yol}: {await javob.Content.ReadAsStringAsync(bekorQilish)}");

        var matn = await javob.Content.ReadAsStringAsync(bekorQilish);

        // Parse ATAYLAB: 200 qaytishi yetarli emas — hujjat buzilgan
        // bo'lsa ham qismi yozilib ulgurgan bo'lishi mumkin.
        using var hujjat = JsonDocument.Parse(matn);

        hujjat.RootElement.TryGetProperty("paths", out var yollar).ShouldBeTrue();
        yollar.EnumerateObject().Any().ShouldBeTrue("hujjatda birorta ham marshrut yo'q");
    }

    /// <summary>
    /// Ommaviy hujjatda FAQAT ommaviy marshrutlar bor.
    /// </summary>
    /// <remarks>
    /// SPEC §8 ning butun mazmuni shu: iste'molchi o'zi chaqira
    /// oladigan narsani ko'radi, ichki API ni emas. Standart sozlamada
    /// guruh nomi ko'rsatilmagan endpoint HAR BIR hujjatga tushadi —
    /// ya'ni bu ajratish qo'lda qo'yilgan va uni qo'lda qo'riqlash kerak.
    /// </remarks>
    [Fact]
    public async Task Ommaviy_hujjatda_faqat_ommaviy_marshrutlar_bor()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = _hujjatli.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/openapi/public.json", UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync(bekorQilish));

        var yollar = hujjat.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(item => item.Name)
            .ToList();

        yollar.ShouldNotBeEmpty();
        yollar.ShouldAllBe(yol => yol.StartsWith("/api/public/v1", StringComparison.Ordinal));

        // Ommaviy hujjatda FAQAT `get` bo'lishi kerak — SPEC §1.
        foreach (var yol in hujjat.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var metod in yol.Value.EnumerateObject())
            {
                metod.Name.ShouldBe("get", $"«{yol.Name}» da faqat GET bo'lishi kerak");
            }
        }
    }

    /// <summary>Ichki hujjatda ommaviy marshrutlar YO'Q.</summary>
    /// <remarks>
    /// Teskari tomon ham kerak: aks holda `./bonvi gen` tashqi API uchun
    /// ham TypeScript klient yasab, ichki web ilovaga kalit talab
    /// qiladigan funksiyalarni qo'shib qo'yardi.
    /// </remarks>
    [Fact]
    public async Task Ichki_hujjatda_ommaviy_marshrutlar_yoq()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = _hujjatli.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/openapi/v1.json", UriKind.Relative),
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync(bekorQilish));

        var yollar = hujjat.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(item => item.Name)
            .ToList();

        yollar.ShouldNotContain(yol => yol.StartsWith("/api/public/", StringComparison.Ordinal));

        // Kalitlarni boshqarish esa ICHKI hujjatda BO'LISHI kerak —
        // sozlamalar ekrani `./bonvi gen` natijasidan foydalanadi.
        yollar.ShouldContain("/api/api-keys");
    }
}
