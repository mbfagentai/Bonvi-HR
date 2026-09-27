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

    /// <summary>Parolni almashtirish yo'li — hisob bo'yicha qat'iy chegara ostida.</summary>
    [Fact]
    public async Task Parol_almashtirish_hisob_boyicha_qatiy_cheklangan()
    {
        var ct = TestContext.Current.CancellationToken;
        var belgi = Belgi();
        using var admin = await AdminMijozAsync();

        var xodim = await XodimAsync(belgi);
        using var tayyor = await IlovaMijoziAsync(admin, xodim.Id);
        var token = tayyor.DefaultRequestHeaders.Authorization!.Parameter!;

        using var server = Cheklovli(("RateLimiting:Login:PermitLimit", 2));
        using var mijoz = server.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        for (var i = 0; i < 2; i++)
        {
            using var javob = await mijoz.PostAsJsonAsync(
                new Uri("/api/employee-auth/password", UriKind.Relative),
                new { currentPassword = "taxmin-" + i, newPassword = "Boshqa_parol_2026" },
                ct);

            await XatoKutAsync(javob, HttpStatusCode.BadRequest, "xodim.parol_notogri");
        }

        using var toldi = await mijoz.PostAsJsonAsync(
            new Uri("/api/employee-auth/password", UriKind.Relative),
            new { currentPassword = "taxmin-3", newPassword = "Boshqa_parol_2026" },
            ct);

        await XatoKutAsync(toldi, HttpStatusCode.TooManyRequests, "rate_limit.oshib_ketdi");
    }

    /// <summary>Global cheklov yoqilgan, berilgan chegaralar bilan alohida server.</summary>
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
