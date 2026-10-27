using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Api.Common;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Recruiting;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Odamga tegishli hamma joyda mijozning HAQIQIY IP'si (SPEC-planshet-anketa §2.3.3): kirish
/// cheklovlari, audit qatori, yangilash tokenlari.
/// </summary>
/// <remarks>
/// <para>
/// Prod'da API ulanish manzili sifatida Cloudflare chekkasini ko'radi; Caddy haqiqiy mijozni
/// <see cref="ClientIp.HeaderName"/> qilib beradi. Test mijozlari shu sarlavhani o'zi qo'yadi —
/// TestServer'da ulanish manzili umuman yo'q, ya'ni sarlavhasiz hamma so'rov bitta «chekka» dan
/// kelgandek (aynan prod'dagi muammo).
/// </para>
/// <para>
/// Qurilma yo'llari (qabul, agent) o'zgarmagan — ularning mavjud testlari (<c>AttendanceIngestApiTests</c>,
/// <c>AttendanceAgentApiTests</c>) va <see cref="HttpRequestContextTests"/> qo'riqlaydi.
/// </para>
/// </remarks>
public sealed class ClientIpApiTests : IClassFixture<BonviApiFactory>
{
    private static int _telefonSanogi;

    private readonly BonviApiFactory _factory;

    public ClientIpApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HR_kirish_cheklovi_mijoz_IPsi_boyicha_A_tolsa_B_va_sarlavhasiz_kiradi()
    {
        await using var server = Cheklovli(("RateLimiting:Login:PermitLimit", 2));

        using var a = Mijoz(server, "203.0.113.31");
        using var b = Mijoz(server, "203.0.113.32");
        using var sarlavhasiz = server.CreateClient();

        for (var urinish = 0; urinish < 2; urinish++)
        {
            using var javob = await NotogriKirishAsync(a, "/api/auth/login");
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{urinish + 1}-urinish");
        }

        using (var toldi = await NotogriKirishAsync(a, "/api/auth/login"))
        {
            toldi.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "A ning bo'linmasi to'ldi");
        }

        // Boshqa odam (B) va sarlavhasiz so'rov — o'z bo'linmasida: Cloudflare chekkasi umumiy bo'lsa ham
        using (var boshqa = await NotogriKirishAsync(b, "/api/auth/login"))
        {
            boshqa.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "B ni A ning urinishlari to'smaydi");
        }

        using var chekka = await NotogriKirishAsync(sarlavhasiz, "/api/auth/login");
        chekka.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Xodim_kirish_cheklovi_ham_mijoz_IPsi_boyicha()
    {
        await using var server = Cheklovli(("RateLimiting:EmployeeLogin:PermitLimit", 2));

        using var a = Mijoz(server, "203.0.113.33");
        using var b = Mijoz(server, "203.0.113.34");

        for (var urinish = 0; urinish < 2; urinish++)
        {
            using var javob = await NotogriKirishAsync(a, "/api/employee-auth/login");
            javob.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, $"{urinish + 1}-urinish");
        }

        using (var toldi = await NotogriKirishAsync(a, "/api/employee-auth/login"))
        {
            toldi.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }

        using var boshqa = await NotogriKirishAsync(b, "/api/employee-auth/login");
        boshqa.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, "zavoddagi boshqa odam — o'z bo'linmasi");
    }

    /// <summary>Kirish, yangilash va audit hodisasi — HAMMASI haqiqiy IP bilan.</summary>
    [Fact]
    public async Task Kirish_va_yangilash_tokeni_mijoz_IPsini_yozadi()
    {
        const string kirishIp = "203.0.113.35";
        const string yangilashIp = "203.0.113.36";

        using var a = Mijoz(_factory, kirishIp);

        using var kirish = await a.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            Ct);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(kirish));

        var auth = await kirish.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        using var b = Mijoz(_factory, yangilashIp);
        using var yangilash = await b.PostAsJsonAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            new { refreshToken = auth!.RefreshToken },
            Ct);
        yangilash.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(yangilash));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var eski = await db.RefreshTokens.AsNoTracking().SingleAsync(token => token.CreatedByIp == kirishIp, Ct);
        eski.RevokedByIp.ShouldBe(yangilashIp, "yangilashda eski token B dan bekor qilingan");

        (await db.RefreshTokens.AsNoTracking().CountAsync(token => token.CreatedByIp == yangilashIp, Ct))
            .ShouldBe(1, "yangi token B ning IP'si bilan");

        var hodisa = await db.AuditLogs.AsNoTracking()
            .SingleAsync(row => row.Action == AuditAction.Login && row.IpAddress == kirishIp, Ct);
        hodisa.UserLogin.ShouldNotBeNull().ShouldEndWith(BonviApiFactory.TestAdminLogin);
    }

    [Fact]
    public async Task HR_amali_audit_qatoriga_mijoz_IPsini_yozadi()
    {
        const string ip = "203.0.113.37";

        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        admin.DefaultRequestHeaders.Add(ClientIp.HeaderName, ip);

        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = "IP sinovi " + Guid.NewGuid().ToString("N")[..8], openings = 1, openedAt = new DateOnly(2026, 10, 1) },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await PublicApiHarness.MatnAsync(javob));

        using var hujjat = JsonDocument.Parse(await PublicApiHarness.MatnAsync(javob));
        var vakansiya = hujjat.RootElement.GetProperty("id").GetGuid().ToString();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qatorlar = await db.AuditLogs.AsNoTracking()
            .Where(row => row.EntityName == nameof(Vacancy) && row.EntityId == vakansiya)
            .ToListAsync(Ct);

        qatorlar.ShouldNotBeEmpty();
        qatorlar.ShouldAllBe(row => row.IpAddress == ip && row.ActorKind == AuditActorKind.User);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Global cheklov yoqilgan alohida xost (Testing'da sukut bo'yicha o'chiq — <c>SecurityTests</c> usuli).</summary>
    private WebApplicationFactory<Program> Cheklovli(params (string Kalit, int Qiymat)[] sozlamalar)
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");

            foreach (var (kalit, qiymat) in sozlamalar)
            {
                builder.UseSetting(kalit, qiymat.ToString(CultureInfo.InvariantCulture));
            }
        });

    /// <summary>Caddy ortidagi mijoz — haqiqiy IP sarlavhada.</summary>
    private static HttpClient Mijoz(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ClientIp.HeaderName, ip);

        return client;
    }

    /// <summary>Har safar boshqa (yo'q) raqam — hisob qulfi emas, faqat IP oynasi sanaladi.</summary>
    private static Task<HttpResponseMessage> NotogriKirishAsync(HttpClient client, string path)
        => client.PostAsJsonAsync(new Uri(path, UriKind.Relative), new { login = Telefon(), password = "notogri_parol" }, Ct);

    private static string Telefon()
        => "93"
           + Random.Shared.Next(100, 999).ToString(CultureInfo.InvariantCulture)
           + (Interlocked.Increment(ref _telefonSanogi) % 10_000).ToString("D4", CultureInfo.InvariantCulture);
}
