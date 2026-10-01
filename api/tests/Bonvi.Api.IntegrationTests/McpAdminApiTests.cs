using System.Net;
using System.Net.Http.Json;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Ulangan hisob (test uchun) — <c>GET /api/mcp/connections</c> qatori.</summary>
public sealed record McpConnectionResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    string ClientName,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    string Status);

/// <summary>Jurnal qatori (test uchun) — <c>GET /api/mcp/audit</c>.</summary>
public sealed record McpAuditResponse(
    long Id,
    DateTimeOffset At,
    string UserName,
    string ClientName,
    string Tool,
    string Outcome,
    int? RowCount,
    int DurationMs);

/// <summary>
/// «Tizim sozlamalari» → «AI ulanish (MCP)» endpointlari (<c>docs/SPEC-mcp.md</c> §8):
/// faqat <c>settings.manage</c>, holat, o'chirgich, ulangan hisoblar, uzish, jurnal.
/// </summary>
public sealed class McpAdminApiTests : IClassFixture<McpHost>
{
    private readonly McpHost _host;

    public McpAdminApiTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Bo'limning HAR BIR endpointi — usul, yo'l va tana.</summary>
    private static IEnumerable<(HttpMethod Usul, string Yol, object? Tana)> Endpointlar()
    {
        yield return (HttpMethod.Get, "/api/mcp/status", null);
        yield return (HttpMethod.Post, "/api/mcp/check", null);
        yield return (HttpMethod.Put, "/api/mcp/enabled", new { enabled = false });
        yield return (HttpMethod.Get, "/api/mcp/connections", null);
        yield return (HttpMethod.Post, $"/api/mcp/connections/{Guid.NewGuid()}/revoke", null);
        yield return (HttpMethod.Get, "/api/mcp/audit?take=10", null);
    }

    // ────────────────────────────────────────────────────────────
    //  RBAC
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anonim_sorov_401_oladi()
    {
        using var mijoz = _host.Client(McpFlow.RandomIp());

        foreach (var (usul, yol, tana) in Endpointlar())
        {
            using var javob = await YuborAsync(mijoz, usul, yol, tana);
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{usul} {yol}");
        }
    }

    /// <summary>
    /// «AI orqali o'qish» va boshqa ko'p ruxsati bor, lekin <c>settings.manage</c>
    /// yo'q hisob — bo'limga kira olmaydi va o'chirgichni bosa olmaydi.
    /// </summary>
    [Fact]
    public async Task Settings_manage_ruxsati_yoq_hisob_403_oladi()
    {
        var token = await TestAccounts.TokenAsync(
            _host.Factory,
            Permissions.McpConnect,
            Permissions.EmployeesView,
            Permissions.UsersManage,
            Permissions.RolesManage,
            Permissions.AuditView);

        using var mijoz = _host.BearerClient(token);

        foreach (var (usul, yol, tana) in Endpointlar())
        {
            using var javob = await YuborAsync(mijoz, usul, yol, tana);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }

        // O'chirgich joyida qoldi.
        using var admin = await _host.AdminClientAsync();
        (await HolatAsync(admin)).State.ShouldBe("On");
    }

    // ────────────────────────────────────────────────────────────
    //  Holat va o'chirgich
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Holat_javobi_shakli()
    {
        using var admin = await _host.AdminClientAsync();

        var holat = await HolatAsync(admin);

        holat.State.ShouldBe("On");
        holat.Reason.ShouldBeNull();
        holat.Enabled.ShouldBeTrue();
        holat.ServerUrl.ShouldBe(McpFlow.Resource);

        // «Qayta tekshirish» — rolni qayta tayyorlaydi, holat o'zgarmaydi.
        // Klasterdagi rolni boshqa sinf xostlari bilan bir vaqtda o'zgartirmaslik uchun — qulf ostida.
        await McpHost.ProvisionLock.WaitAsync(Ct);

        try
        {
            using var tekshir = await admin.PostAsync(new Uri("/api/mcp/check", UriKind.Relative), null, Ct);
            tekshir.StatusCode.ShouldBe(HttpStatusCode.OK, await tekshir.Content.ReadAsStringAsync(Ct));
            (await tekshir.Content.ReadFromJsonAsync<McpStatusResponse>(Ct))!.State.ShouldBe("On");
        }
        finally
        {
            McpHost.ProvisionLock.Release();
        }
    }

    [Fact]
    public async Task Ochirish_va_yoqish_aylanadi_va_bazaga_yoziladi()
    {
        using var admin = await _host.AdminClientAsync();

        try
        {
            var ochiq = await McpHost.SetEnabledAsync(admin, enabled: false);
            ochiq.State.ShouldBe("Off");
            ochiq.Enabled.ShouldBeFalse();
            ochiq.ServerUrl.ShouldBe(McpFlow.Resource);

            (await HolatAsync(admin)).State.ShouldBe("Off");
            (await SozlamaAsync()).ShouldBe("false");
        }
        finally
        {
            var yoqiq = await McpHost.SetEnabledAsync(admin, enabled: true);
            yoqiq.State.ShouldBe("On");
            yoqiq.Enabled.ShouldBeTrue();
        }

        (await HolatAsync(admin)).State.ShouldBe("On");
        (await SozlamaAsync()).ShouldBe("true");
    }

    // ────────────────────────────────────────────────────────────
    //  Ulangan hisoblar va uzish
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ulanishlar_royxati_yangi_ulanishni_korsatadi_va_uzish_uni_oldiradi()
    {
        var flow = new McpFlow(_host);
        var mijozNomi = "Ulanish AI " + McpHost.Belgi();
        var hisob = await TestAccounts.CreateAsync(_host.Factory, Permissions.McpConnect, Permissions.EmployeesView);
        var tokens = await flow.ConnectAsync(hisob.Login, hisob.Password, mijozNomi);

        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.OK);

        using var admin = await _host.AdminClientAsync();

        var qator = (await UlanishlarAsync(admin)).Single(ulanish => ulanish.ClientName == mijozNomi);
        qator.UserId.ShouldBe(hisob.UserId);
        qator.UserName.ShouldBe("Синов Ҳисоб");
        qator.Status.ShouldBe("active");
        qator.RevokedAt.ShouldBeNull();
        qator.ConnectedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        qator.LastUsedAt.ShouldNotBeNull("ulanish ishlatilgan — oxirgi ishlatilgan vaqt yozilishi kerak");

        // Uzish — DARHOL kuchga kiradi.
        using (var uzish = await admin.PostAsync(new Uri($"/api/mcp/connections/{qator.Id}/revoke", UriKind.Relative), null, Ct))
        {
            uzish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await uzish.Content.ReadAsStringAsync(Ct));
        }

        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.Unauthorized);

        using (var yangilash = await flow.TokenAsync(McpFlow.RefreshForm(tokens.Refresh, tokens.ClientId)))
        {
            yangilash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // Yozuv o'chirilmaydi — ro'yxatda «revoked» bo'lib qoladi.
        var keyin = (await UlanishlarAsync(admin)).Single(ulanish => ulanish.Id == qator.Id);
        keyin.Status.ShouldBe("revoked");
        keyin.RevokedAt.ShouldNotBeNull();

        // Idempotent: qayta uzish ham 204.
        using (var qayta = await admin.PostAsync(new Uri($"/api/mcp/connections/{qator.Id}/revoke", UriKind.Relative), null, Ct))
        {
            qayta.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Kim uzgani — administratorning identifikatori.
        var kim = await _host.StoreAsync(db => db.McpGrants.AsNoTracking()
            .Where(grant => grant.Id == qator.Id)
            .Select(grant => grant.RevokedBy)
            .SingleAsync(Ct));
        kim.ShouldNotBeNullOrWhiteSpace();
        kim.ShouldNotStartWith("system:");
    }

    [Fact]
    public async Task Notanish_ulanishni_uzish_404()
    {
        using var admin = await _host.AdminClientAsync();

        using var javob = await admin.PostAsync(new Uri($"/api/mcp/connections/{Guid.NewGuid()}/revoke", UriKind.Relative), null, Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(Ct);
        muammo!.Title.ShouldBe("mcp.ulanish_topilmadi");
    }

    // ────────────────────────────────────────────────────────────
    //  Jurnal
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Jurnal_vosita_chaqiruvini_ok_bilan_korsatadi()
    {
        var flow = new McpFlow(_host);
        var mijozNomi = "Jurnal AI " + McpHost.Belgi();
        var tokens = await flow.ConnectAdminAsync(mijozNomi);

        var natija = await flow.CallAsync(tokens.Access, "overview");
        natija.IsError.ShouldBeFalse(natija.Text);

        using var admin = await _host.AdminClientAsync();
        var qatorlar = await admin.GetFromJsonAsync<List<McpAuditResponse>>(new Uri("/api/mcp/audit?take=50", UriKind.Relative), Ct);

        var qator = qatorlar!.First(yozuv => yozuv.ClientName == mijozNomi);
        qator.Tool.ShouldBe("overview");
        qator.Outcome.ShouldBe("ok");
        qator.UserName.ShouldBe("Test Administrator Testovich");
        qator.RowCount.ShouldNotBeNull();
        qator.At.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));

        // Argumentlar javobda YO'Q (sir bo'lishi mumkin) — faqat jadvalda.
        var matn = await admin.GetStringAsync(new Uri("/api/mcp/audit?take=50", UriKind.Relative), Ct);
        matn.ShouldNotContain("\"arguments\"");

        // «take» chegaralangan.
        var bitta = await admin.GetFromJsonAsync<List<McpAuditResponse>>(new Uri("/api/mcp/audit?take=1", UriKind.Relative), Ct);
        bitta!.Count.ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> YuborAsync(HttpClient mijoz, HttpMethod usul, string yol, object? tana)
    {
        using var sorov = new HttpRequestMessage(usul, new Uri(yol, UriKind.Relative));

        if (tana is not null)
        {
            sorov.Content = JsonContent.Create(tana);
        }

        return await mijoz.SendAsync(sorov, Ct);
    }

    private static async Task<McpStatusResponse> HolatAsync(HttpClient admin)
    {
        var holat = await admin.GetFromJsonAsync<McpStatusResponse>(new Uri("/api/mcp/status", UriKind.Relative), Ct);
        holat.ShouldNotBeNull();
        return holat;
    }

    private static async Task<List<McpConnectionResponse>> UlanishlarAsync(HttpClient admin)
    {
        var royxat = await admin.GetFromJsonAsync<List<McpConnectionResponse>>(new Uri("/api/mcp/connections", UriKind.Relative), Ct);
        royxat.ShouldNotBeNull();
        return royxat;
    }

    private Task<string?> SozlamaAsync() => _host.DbAsync(db => db.SystemSettings.AsNoTracking()
        .Where(setting => setting.Key == SystemSettingKeys.McpEnabled)
        .Select(setting => setting.Value)
        .FirstOrDefaultAsync(Ct));
}

/// <summary>
/// Serverda sozlanmagan MCP (<c>Mcp:DbPassword</c> yo'q — oddiy test xosti):
/// YOPIQ-SUKUT (<c>docs/SPEC-mcp.md</c> §2.2) — <c>/mcp</c> 503, OAuth 404,
/// o'chirgich saqlanadi, lekin holat «NotConfigured» bo'lib qoladi.
/// </summary>
public sealed class McpNotConfiguredTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public McpNotConfiguredTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sozlanmagan_MCP_yopiq_va_holat_sababini_aytadi()
    {
        using var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Add("X-Forwarded-For", McpFlow.RandomIp());

        using (var kirish = await mijoz.PostAsJsonAsync(
                   new Uri("/api/auth/login", UriKind.Relative),
                   new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
                   Ct))
        {
            kirish.StatusCode.ShouldBe(HttpStatusCode.OK);
            var auth = await kirish.Content.ReadFromJsonAsync<AuthResponse>(Ct);
            mijoz.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        }

        var holat = await mijoz.GetFromJsonAsync<McpStatusResponse>(new Uri("/api/mcp/status", UriKind.Relative), Ct);
        holat!.State.ShouldBe("NotConfigured");
        holat.Reason.ShouldNotBeNull();
        holat.Reason.ShouldContain("Mcp:DbPassword");
        holat.Enabled.ShouldBeFalse();

        // Egasi yoqsa ham — qiymat saqlanadi, lekin MCP ochilmaydi.
        using (var yoq = await mijoz.PutAsJsonAsync(new Uri("/api/mcp/enabled", UriKind.Relative), new { enabled = true }, Ct))
        {
            yoq.StatusCode.ShouldBe(HttpStatusCode.OK);
            var keyin = await yoq.Content.ReadFromJsonAsync<McpStatusResponse>(Ct);
            keyin!.State.ShouldBe("NotConfigured");
            keyin.Enabled.ShouldBeTrue();
        }

        using (var mcp = await mijoz.PostAsJsonAsync(
                   new Uri("/mcp", UriKind.Relative),
                   new { jsonrpc = "2.0", id = 1, method = "tools/list" },
                   Ct))
        {
            mcp.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }

        foreach (var yol in new[] { "/.well-known/oauth-authorization-server", "/.well-known/oauth-protected-resource/mcp", "/oauth/authorize?client_id=x" })
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound, yol);
        }

        using (var royxat = await mijoz.PostAsJsonAsync(
                   new Uri("/oauth/register", UriKind.Relative),
                   new { client_name = "x", redirect_uris = new[] { McpFlow.RedirectUri } },
                   Ct))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // Ro'yxat va jurnal baribir o'qiladi (bo'sh).
        (await mijoz.GetFromJsonAsync<List<McpConnectionResponse>>(new Uri("/api/mcp/connections", UriKind.Relative), Ct))!.ShouldBeEmpty();
        (await mijoz.GetFromJsonAsync<List<McpAuditResponse>>(new Uri("/api/mcp/audit", UriKind.Relative), Ct))!.ShouldBeEmpty();
    }
}
