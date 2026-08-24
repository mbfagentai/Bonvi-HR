using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bonvi.Api.Extensions;
using Bonvi.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xavfsizlik sarlavhalari va tezlik cheklovining haqiqiy HTTP javobida
/// ishlashini tekshiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="IStartupFilter"/>:</b> bu testlar middleware'ning o'zini
/// sinaydi va <c>Program.cs</c> ga ulanishidan mustaqil. Shu sababli middleware
/// konveyerga test doirasida qo'shiladi.
/// </para>
/// <para>
/// <c>Program.cs</c> ga <c>app.UseBonviSecurity()</c> ulangandan keyin filtr
/// keraksiz bo'ladi — uni o'chirib, oddiy <c>_factory.CreateClient()</c> ga
/// o'tish mumkin. Sarlavhalar indeks orqali yoziladi (Append emas), shu sababli
/// ikkalasi bir vaqtda ishlasa ham javobda takrorlanmaydi.
/// </para>
/// </remarks>
public sealed class SecurityTests : IClassFixture<BonviApiFactory>
{
    /// <summary>API javoblari uchun kutilayotgan qat'iy CSP.</summary>
    private const string QatiyCsp =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private readonly BonviApiFactory _factory;

    public SecurityTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Xavfsizlik sarlavhalari
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Xavfsizlik_sarlavhalari_har_bir_javobda_boladi()
    {
        using var client = XavfsizlikMijozi();

        using var javob = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Sarlavha(javob, "X-Content-Type-Options").ShouldBe("nosniff");
        Sarlavha(javob, "X-Frame-Options").ShouldBe("DENY");
        Sarlavha(javob, "Referrer-Policy").ShouldBe("strict-origin-when-cross-origin");
        Sarlavha(javob, "X-Permitted-Cross-Domain-Policies").ShouldBe("none");
        Sarlavha(javob, "Cross-Origin-Opener-Policy").ShouldBe("same-origin");
        Sarlavha(javob, "Content-Security-Policy").ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Server_sarlavhasi_javobda_bolmasligi_kerak()
    {
        using var client = XavfsizlikMijozi();

        using var javob = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Server turi va versiyasi hujumchiga ma'lum zaifliklarni qidirish
        // uchun boshlang'ich nuqta beradi.
        Sarlavha(javob, "Server").ShouldBeNull();
        Sarlavha(javob, "X-Powered-By").ShouldBeNull();
        Sarlavha(javob, "X-AspNet-Version").ShouldBeNull();
    }

    [Fact]
    public async Task Permissions_Policy_kamera_mikrofon_va_geolokatsiyani_ochiradi()
    {
        using var client = XavfsizlikMijozi();

        using var javob = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var siyosat = Sarlavha(javob, "Permissions-Policy");

        siyosat.ShouldNotBeNull();
        siyosat.ShouldContain("camera=()");
        siyosat.ShouldContain("microphone=()");
        siyosat.ShouldContain("geolocation=()");
    }

    [Fact]
    public async Task API_yolida_eng_qatiy_CSP_beriladi()
    {
        using var client = XavfsizlikMijozi();

        // Ataylab muvaffaqiyatsiz so'rov (401 yoki 404 — avtorizatsiya
        // sozlamasiga bog'liq). Aynan shunday javoblarda ham sarlavhalar
        // bo'lishi kerak: xato javobi ham brauzerda ochilishi mumkin.
        using var javob = await client.GetAsync(
            new Uri("/api/mavjud-emas", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.IsSuccessStatusCode.ShouldBeFalse();
        Sarlavha(javob, "Content-Security-Policy").ShouldBe(QatiyCsp);
    }

    [Fact]
    public async Task API_javoblari_brauzer_keshiga_yozilmasligi_kerak()
    {
        using var client = XavfsizlikMijozi();

        using var javob = await client.GetAsync(
            new Uri("/api/mavjud-emas", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Pasport, JSHSHIR va oylik umumiy kompyuterning disk keshida qolmasin.
        javob.Headers.CacheControl.ShouldNotBeNull();
        javob.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task HSTS_shifrlanmagan_ulanishda_yuborilmaydi()
    {
        using var client = XavfsizlikMijozi();

        using var javob = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // HSTS faqat HTTPS + Production'da. http://localhost uchun yuborilsa,
        // brauzer domenni eslab qoladi va boshqa mahalliy loyihalar ham buziladi.
        Sarlavha(javob, "Strict-Transport-Security").ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  Tezlik cheklovi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chegaradan_oshgan_sorov_429_va_ProblemDetails_qaytaradi()
    {
        const int chegara = 3;

        using var client = TezlikMijozi(chegara);

        // Chegaragacha bo'lgan so'rovlar o'tishi kerak.
        for (var i = 0; i < chegara; i++)
        {
            using var oq = await client.GetAsync(
                new Uri("/", UriKind.Relative),
                TestContext.Current.CancellationToken);

            oq.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var javob = await client.GetAsync(
            new Uri("/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Retry-After bo'lmasa mijoz qachon qayta urinishni bilmaydi va
        // odatda darhol qayta so'raydi — bu yuklamani yanada oshiradi.
        javob.Headers.RetryAfter.ShouldNotBeNull();

        javob.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await javob.Content.ReadFromJsonAsync<ProblemDetails>(
            TestContext.Current.CancellationToken);

        // Frontend matnni emas, KODNI tarjima qiladi.
        problem.ShouldNotBeNull();
        problem!.Title.ShouldBe("rate_limit.oshib_ketdi");
        problem.Status.ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void Tezlik_siyosati_nomlari_bir_biridan_farq_qiladi()
    {
        // Ikki siyosat bir xil nomga ega bo'lsa, biri ikkinchisini jimgina
        // almashtiradi va cheklov kutilganidan boshqacha ishlaydi.
        var nomlar = new[] { RateLimitPolicies.Login, RateLimitPolicies.Api, RateLimitPolicies.Export };

        nomlar.ShouldAllBe(nom => !string.IsNullOrWhiteSpace(nom));
        nomlar.Distinct(StringComparer.Ordinal).Count().ShouldBe(nomlar.Length);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static string? Sarlavha(HttpResponseMessage javob, string nom)
        => javob.Headers.TryGetValues(nom, out var qiymatlar)
            ? string.Join(", ", qiymatlar)
            : null;

    private HttpClient XavfsizlikMijozi() => _factory.CreateClient();

    private HttpClient TezlikMijozi(int chegara)
        => _factory
            .WithWebHostBuilder(builder =>
            {
                // Testing muhitida global cheklov standart holatda o'chiq —
                // bu yerda uni ataylab yoqamiz va chegarani pasaytiramiz.
                //
                // Servislarni QAYTA ro'yxatga olish shart emas: `Program.cs`
                // allaqachon `AddBonviRateLimiting` va `UseRateLimiter` ni ulagan.
                // Ikkinchi marta chaqirilsa «policy already exists» xatosi chiqadi.
                builder.UseSetting("RateLimiting:Enabled", "true");
                builder.UseSetting(
                    "RateLimiting:Api:PermitLimit",
                    chegara.ToString(CultureInfo.InvariantCulture));
            })
            .CreateClient();
}


