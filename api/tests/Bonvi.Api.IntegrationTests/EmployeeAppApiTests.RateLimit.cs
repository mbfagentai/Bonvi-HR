using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodim ilovasi — tezlik cheklovining bo'linmalari.
/// </summary>
/// <remarks>
/// <para>
/// Zavodning Wi-Fi'si bitta ommaviy IP: ilgari xodim kirishi HR kirishi bilan
/// bitta «daqiqada 5» bo'linmani bo'lishardi, anonim yangilash esa umumiy
/// «IP bo'yicha 120» ga tushardi — smena boshida ikkalasi ham 429 berardi.
/// </para>
/// <para>
/// Testing muhitida global cheklov o'chiq, shu sababli har test o'z
/// sozlamasi bilan alohida server ko'taradi (<c>SecurityTests</c> dagi usul) —
/// hisoblagichlar testlar orasida aralashmaydi. Test mijozi bitta «IP» dan
/// keladi, ya'ni zavod holatining aynan o'zi.
/// </para>
/// </remarks>
public sealed partial class EmployeeAppApiTests
{
    /// <summary>HR kirishi to'lsa ham xodim kirishi ishlaydi — va daqiqadagi beshtadan ko'p.</summary>
    [Fact]
    public async Task Xodim_kirishi_HR_kirishidan_alohida_bolinmada()
    {
        var ct = TestContext.Current.CancellationToken;

        using var server = Cheklovli(("RateLimiting:Login:PermitLimit", 2), ("RateLimiting:Api:PermitLimit", 3));
        using var mijoz = server.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            using var hr = await mijoz.PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                new { login = TelefonRaqami(), password = "notogri" },
                ct);

            hr.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        using (var toldi = await mijoz.PostAsJsonAsync(
                   new Uri("/api/auth/login", UriKind.Relative),
                   new { login = TelefonRaqami(), password = "notogri" },
                   ct))
        {
            toldi.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "HR bo'linmasining o'zi o'zgarmagan");
        }

        // Xodim kirishi — o'z bo'linmasida: HR ning 2 lik va umumiy 3 lik
        // chegaralaridan ham ko'p urinish o'tadi (har biri boshqa raqam — blok emas).
        for (var i = 0; i < 8; i++)
        {
            using var xodim = await mijoz.PostAsJsonAsync(
                new Uri("/api/employee-auth/login", UriKind.Relative),
                new { login = TelefonRaqami(), password = "notogri" },
                ct);

            await XatoKutAsync(xodim, HttpStatusCode.BadRequest, "xodim.notogri_malumot");
        }
    }

    /// <summary>
    /// Yangilash IP bo'yicha emas, TOKEN bo'yicha cheklanadi: ko'p xodim — o'tadi, bitta token tinimsiz — 429.
    /// </summary>
    [Fact]
    public async Task Yangilash_token_boyicha_cheklanadi()
    {
        var ct = TestContext.Current.CancellationToken;

        using var server = Cheklovli(("RateLimiting:Api:PermitLimit", 3), ("RateLimiting:RefreshToken:PermitLimit", 2));
        using var mijoz = server.CreateClient();

        // Zavoddagi o'nta telefon (bitta IP, har birining o'z tokeni) — umumiy 3 lik chegaraga urilmaydi.
        for (var i = 0; i < 10; i++)
        {
            using var javob = await mijoz.PostAsJsonAsync(
                new Uri("/api/employee-auth/refresh", UriKind.Relative),
                new { refreshToken = "telefon-" + i + "-" + Belgi() },
                ct);

            await XatoKutAsync(javob, HttpStatusCode.Forbidden, "xodim.token_yaroqsiz");
        }

        // Bitta token tinimsiz — faqat U to'siladi.
        var bitta = "sikl-" + Belgi();

        for (var i = 0; i < 2; i++)
        {
            using var javob = await mijoz.PostAsJsonAsync(
                new Uri("/api/employee-auth/refresh", UriKind.Relative),
                new { refreshToken = bitta },
                ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var toldi = await mijoz.PostAsJsonAsync(
            new Uri("/api/employee-auth/refresh", UriKind.Relative),
            new { refreshToken = bitta },
            ct);

        await XatoKutAsync(toldi, HttpStatusCode.TooManyRequests, "rate_limit.oshib_ketdi");
        toldi.Headers.RetryAfter.ShouldNotBeNull();
        (await JsonAsync(toldi)).GetProperty("retryAfterSeconds").GetInt32().ShouldBeGreaterThan(0);

        // Qo'shni token ishlashda davom etadi.
        using var qoshni = await mijoz.PostAsJsonAsync(
            new Uri("/api/employee-auth/refresh", UriKind.Relative),
            new { refreshToken = "qoshni-" + Belgi() },
            ct);

        qoshni.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Qurilma agenti umumiy (IP bo'yicha) cheklovdan ozod — o'z siyosati bor.</summary>
    [Fact]
    public async Task Agent_yoli_umumiy_cheklovdan_ozod()
    {
        var ct = TestContext.Current.CancellationToken;

        using var server = Cheklovli(("RateLimiting:Api:PermitLimit", 2));
        using var mijoz = server.CreateClient();

        for (var i = 0; i < 6; i++)
        {
            using var javob = await mijoz.GetAsync(new Uri("/api/attendance/agent/roster", UriKind.Relative), ct);

            javob.StatusCode.ShouldNotBe(
                HttpStatusCode.TooManyRequests,
                "yozish paytida agent har odam uchun rasm so'raydi — umumiy 2 lik chegara uni to'xtatmasligi kerak");
        }

        // Nazorat: umumiy chegara boshqa yo'lda HAQIQATAN yoqiq.
        HttpStatusCode oxirgi = default;
        for (var i = 0; i < 3; i++)
        {
            using var javob = await mijoz.GetAsync(new Uri("/", UriKind.Relative), ct);
            oxirgi = javob.StatusCode;
        }

        oxirgi.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Ilova so'rovlari (<c>/api/me/*</c>) XODIM bo'yicha cheklanadi: bitta IP dagi ikki xodim bir-birining
    /// chegarasini yemaydi, bitta xodimning o'zi esa o'z chegarasida to'xtaydi.
    /// </summary>
    /// <remarks>
    /// main'da <c>UseRateLimiter</c> avtorizatsiyadan OLDINGA ko'chgan (tashqi API kalitini tanlab ko'rish
    /// sanalsin deb). Xodim sxemasi esa faqat avtorizatsiyada o'qilardi — cheklov paytida so'rov anonim
    /// ko'rinib, zavoddagi hamma telefon bitta «IP bo'yicha 120» bo'linmasiga tushardi.
    /// </remarks>
    [Fact]
    public async Task Ilova_sorovlari_xodim_boyicha_cheklanadi_IP_boyicha_emas()
    {
        var ct = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var tokenlar = new List<string>();

        foreach (var belgi in new[] { "rl1-" + Belgi(), "rl2-" + Belgi() })
        {
            var xodim = await XodimAsync(belgi);
            tokenlar.Add((await KirganJuftlikAsync(admin, xodim.Id)).GetProperty("accessToken").GetString()!);
        }

        using var server = Cheklovli(("RateLimiting:Api:PermitLimit", 2));
        using var mijoz = server.CreateClient();

        async Task<HttpStatusCode> MeningAsync(string token)
        {
            using var sorov = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/me", UriKind.Relative));
            sorov.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var javob = await mijoz.SendAsync(sorov, ct);
            return javob.StatusCode;
        }

        // Ikki xodim, bitta IP, har biri ikkitadan — umumiy «IP bo'yicha 2» bo'lsa, uchinchisi 429 bo'lardi.
        foreach (var token in tokenlar)
        {
            for (var i = 0; i < 2; i++)
            {
                (await MeningAsync(token)).ShouldBe(HttpStatusCode.OK);
            }
        }

        // Nazorat: xodimning o'z chegarasi HAQIQATAN ishlaydi.
        (await MeningAsync(tokenlar[0])).ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Xodim kirishining parol tekshiruvi navbati: bir vaqtda faqat ruxsat etilgan son ishlanadi,
    /// navbat to'lsa qolgani darhol 429 — HAR XIL raqamlar bilan ham. HR kirishi esa O'Z navbatida:
    /// xodim navbati to'lganda ham u 429 olmaydi.
    /// </summary>
    [Fact]
    public async Task Parol_tekshiruvi_navbatda_ortigi_429_HR_navbati_alohida()
    {
        var ct = TestContext.Current.CancellationToken;

        using var server = Cheklovli(
            ("RateLimiting:PasswordHashing:PermitLimit", 1),
            ("RateLimiting:PasswordHashing:QueueLimit", 0),
            ("RateLimiting:EmployeeLogin:PermitLimit", 1000));
        using var mijoz = server.CreateClient();

        async Task<HttpStatusCode> KirishAsync(string yol)
        {
            using var javob = await mijoz.PostAsJsonAsync(
                new Uri(yol, UriKind.Relative),
                new { login = TelefonRaqami(), password = "notogri" },
                ct);
            return javob.StatusCode;
        }

        var xodimlar = Enumerable.Range(0, 16).Select(_ => KirishAsync("/api/employee-auth/login")).ToList();
        var hr = KirishAsync("/api/auth/login");

        var natijalar = await Task.WhenAll(xodimlar);

        natijalar.ShouldContain(HttpStatusCode.TooManyRequests, "navbat 0 — parallel urinishlarning bir qismi rad etilishi kerak");
        natijalar.ShouldContain(HttpStatusCode.BadRequest, "ruxsat olganlari odatdagidek tekshiriladi");
        (await hr).ShouldNotBe(HttpStatusCode.TooManyRequests, "HR kirishi xodim navbatiga bog'liq emas");
    }

    /// <summary>
    /// Navbatda KUTGAN so'rov IP oynasining ruxsatini ikkinchi marta yemaydi: oynada 4 ta,
    /// navbatda 1 ish joyi — bitta IP'dan 3 ta parallel kirish hammasi o'tadi (429 yo'q).
    /// </summary>
    [Fact]
    public async Task Navbatda_kutish_IP_oynasini_ikki_marta_yemaydi()
    {
        var ct = TestContext.Current.CancellationToken;

        using var server = Cheklovli(
            ("RateLimiting:PasswordHashing:PermitLimit", 1),
            ("RateLimiting:PasswordHashing:QueueLimit", 20),
            ("RateLimiting:EmployeeLogin:PermitLimit", 4));
        using var mijoz = server.CreateClient();

        async Task<HttpStatusCode> KirishAsync()
        {
            using var javob = await mijoz.PostAsJsonAsync(
                new Uri("/api/employee-auth/login", UriKind.Relative),
                new { login = TelefonRaqami(), password = "notogri" },
                ct);
            return javob.StatusCode;
        }

        var natijalar = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => KirishAsync()));

        natijalar.ShouldAllBe(kod => kod == HttpStatusCode.BadRequest);
    }

    /// <summary>Tokensiz kirish yo'llarida tana 16 KB dan katta bo'lsa — o'qilmasdan 413.</summary>
    [Theory]
    [InlineData("/api/employee-auth/login")]
    [InlineData("/api/auth/login")]
    public async Task Kirish_yolida_katta_tana_413(string yol)
    {
        var ct = TestContext.Current.CancellationToken;
        using var mijoz = _factory.CreateClient();

        // `StringContent` — `Content-Length` sarlavhasi bilan (brauzer va ilova aynan shunday yuboradi).
        using var tana = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { login = TelefonRaqami(), password = new string('x', 20 * 1024) }),
            System.Text.Encoding.UTF8,
            "application/json");
        using var javob = await mijoz.PostAsync(new Uri(yol, UriKind.Relative), tana, ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>Global cheklov yoqilgan, berilgan chegaralar bilan alohida server.</summary>
    /// <summary>Rasm o'qish umumiy bo'linmani yemaydi: ko'p rasmli sahifadan keyin ham API ishlaydi.</summary>
    [Fact]
    public async Task Rasm_oqish_umumiy_API_bolinmasini_yemaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = Cheklovli(("RateLimiting:Api:PermitLimit", 3), ("RateLimiting:Photos:PermitLimit", 20));
        using var mijoz = server.CreateClient();

        // Umumiy chegaradan (3) ancha ko'p rasm so'rovi — hech biri 429 emas
        for (var i = 0; i < 10; i++)
        {
            using var rasm = await mijoz.GetAsync(new Uri($"/api/employees/{Guid.NewGuid()}/photo", UriKind.Relative), ct);
            rasm.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        // Umumiy bo'linma butun: oddiy so'rov hali o'tadi
        using var oddiy = await mijoz.GetAsync(new Uri("/api/auth/me", UriKind.Relative), ct);
        oddiy.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, "rasmlar umumiy 3 talik bo'linmani yemasligi kerak");

        // Rasm bo'linmasining o'z chegarasi bor
        var radEtildi = false;
        for (var i = 0; i < 15 && !radEtildi; i++)
        {
            using var rasm = await mijoz.GetAsync(new Uri($"/api/employees/{Guid.NewGuid()}/photo", UriKind.Relative), ct);
            radEtildi = rasm.StatusCode == HttpStatusCode.TooManyRequests;
        }

        radEtildi.ShouldBeTrue("rasm bo'linmasi ham cheksiz emas");
    }

    private WebApplicationFactory<Program> Cheklovli(params (string Kalit, int Qiymat)[] sozlamalar)
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");

            foreach (var (kalit, qiymat) in sozlamalar)
            {
                builder.UseSetting(kalit, qiymat.ToString(CultureInfo.InvariantCulture));
            }
        });
}
