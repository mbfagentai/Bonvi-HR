using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Mcp;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Rol (test uchun) — <c>GET /api/roles</c> qatori.</summary>
public sealed record McpRoleResponse(Guid Id, string Code, string Name, IReadOnlyList<string> Permissions);

/// <summary>
/// AI ulanish — OAuth 2.1 oqimi (<c>docs/SPEC-mcp.md</c> §5, §10): metama'lumot,
/// dinamik ro'yxatdan o'tish, PKCE bilan avtorizatsiya, token almashinuvi,
/// kodni va yangilash tokenini qayta ishlatish, bekor qilish, o'chirgich va
/// har chaqiruvdagi ruxsat tekshiruvi.
/// </summary>
/// <remarks>
/// Haqiqiy PostgreSQL ustida, haqiqiy kirish sahifasi orqali — brauzer yuradigan
/// yo'ldan (<see cref="McpFlow"/>).
/// </remarks>
public sealed class McpOAuthTests : IClassFixture<McpHost>
{
    private static readonly string[] YomonManzillar =
    [
        // Ruxsat ro'yxatida yo'q https xost
        "https://evil.example.com/cb",
        "https://claude.ai.evil.com/cb",

        // Ruxsat etilgan xost, lekin nostandart port yoki http
        "https://claude.ai:8443/cb",
        "http://claude.ai/cb",

        // Maxsus sxemalar
        "cursor://anysphere.cursor-retrieval/oauth/callback",
        "javascript:alert(1)",

        // Fragment va foydalanuvchi ma'lumoti
        "https://claude.ai/api/mcp/auth_callback#frag",
        "https://user:pw@claude.ai/cb",

        // Kanonik bo'lmagan yozuv
        "HTTPS://claude.ai/api/mcp/auth_callback",
        "https://claude.ai/a/../cb",
        "not a uri",
    ];

    private static readonly string[] ParolTuri = ["password"];

    private readonly McpHost _host;

    public McpOAuthTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private McpFlow Flow() => new(_host);

    // ────────────────────────────────────────────────────────────
    //  Metama'lumot va 401
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Metamalumot_hujjatlari_standartga_mos()
    {
        var flow = Flow();

        foreach (var yol in new[] { "/.well-known/oauth-protected-resource", "/.well-known/oauth-protected-resource/mcp" })
        {
            using var javob = await flow.GetAsync(new Uri(yol, UriKind.Relative));
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, yol);

            using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync(Ct));
            var r = hujjat.RootElement;
            r.GetProperty("resource").GetString().ShouldBe(McpFlow.Resource);
            r.GetProperty("authorization_servers")[0].GetString().ShouldBe("http://localhost");
            r.GetProperty("scopes_supported")[0].GetString().ShouldBe("hr.read");
            r.GetProperty("bearer_methods_supported")[0].GetString().ShouldBe("header");
        }

        foreach (var yol in new[] { "/.well-known/oauth-authorization-server", "/.well-known/oauth-authorization-server/mcp" })
        {
            using var javob = await flow.GetAsync(new Uri(yol, UriKind.Relative));
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, yol);
            javob.Headers.CacheControl?.NoStore.ShouldBe(true);

            using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync(Ct));
            var r = hujjat.RootElement;
            r.GetProperty("issuer").GetString().ShouldBe("http://localhost");
            r.GetProperty("authorization_endpoint").GetString().ShouldBe("http://localhost/oauth/authorize");
            r.GetProperty("token_endpoint").GetString().ShouldBe("http://localhost/oauth/token");
            r.GetProperty("registration_endpoint").GetString().ShouldBe("http://localhost/oauth/register");
            r.GetProperty("revocation_endpoint").GetString().ShouldBe("http://localhost/oauth/revoke");
            Satrlar(r.GetProperty("code_challenge_methods_supported")).ShouldBe("S256");
            Satrlar(r.GetProperty("response_types_supported")).ShouldBe("code");
            Satrlar(r.GetProperty("grant_types_supported")).ShouldBe("authorization_code,refresh_token");
            Satrlar(r.GetProperty("token_endpoint_auth_methods_supported")).ShouldBe("none");
        }

        // OpenID taxallusi yo'q — biz OIDC provayderi emasmiz.
        using var oidc = await flow.GetAsync(new Uri("/.well-known/openid-configuration", UriKind.Relative));
        oidc.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tokensiz_yoki_begona_token_bilan_mcp_401_va_resource_metadata_sarlavhasi()
    {
        var flow = Flow();

        using (var tokensiz = await flow.PostMcpAsync(null, new { jsonrpc = "2.0", id = 1, method = "tools/list" }))
        {
            tokensiz.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            var sarlavha = tokensiz.Headers.WwwAuthenticate.ToString();
            sarlavha.ShouldContain("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/mcp\"");
            sarlavha.ShouldNotContain("invalid_token");
        }

        using (var soxta = await flow.PostMcpAsync("bhr_at_haqiqiy_bolmagan_token", new { jsonrpc = "2.0", id = 1, method = "tools/list" }))
        {
            soxta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            soxta.Headers.WwwAuthenticate.ToString().ShouldContain("invalid_token");
        }

        // Veb-panel JWT'si MCP tokeni EMAS — super administratorniki bo'lsa ham.
        using var admin = await _host.AdminClientAsync();
        var jwt = admin.DefaultRequestHeaders.Authorization!.Parameter!;
        (await flow.ProbeAsync(jwt)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────
    //  Dinamik ro'yxatdan o'tish (RFC 7591)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DCR_ruxsat_etilgan_manzillarni_qabul_qiladi()
    {
        var flow = Flow();

        (await flow.RegisterAsync("Claude", "https://claude.ai/api/mcp/auth_callback")).ShouldStartWith("mcp_");
        (await flow.RegisterAsync("ChatGPT", "https://chatgpt.com/connector_platform_oauth_redirect")).ShouldStartWith("mcp_");
        (await flow.RegisterAsync("Desktop", "http://localhost:6274/oauth/callback")).ShouldStartWith("mcp_");

        // Ochiq mijoz — sir berilmaydi, nima so'ralgan bo'lsa ham.
        using var javob = await Flow().RegisterRawAsync(new
        {
            client_name = "Sirli",
            redirect_uris = new[] { McpFlow.RedirectUri },
            token_endpoint_auth_method = "client_secret_basic",
        });

        javob.StatusCode.ShouldBe(HttpStatusCode.Created);
        var matn = await javob.Content.ReadAsStringAsync(Ct);
        matn.ShouldNotContain("client_secret\"");
        matn.ShouldContain("\"token_endpoint_auth_method\":\"none\"");
        matn.ShouldContain("\"scope\":\"hr.read\"");
    }

    [Fact]
    public async Task DCR_yaroqsiz_qaytish_manzilini_rad_etadi()
    {
        foreach (var yomon in YomonManzillar)
        {
            // Har urinish — yangi IP: ro'yxatdan o'tish IP bo'yicha soatiga 10 ta.
            using var javob = await Flow().RegisterRawAsync(new { client_name = "x", redirect_uris = new[] { yomon } });

            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, yomon);
            (await javob.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_redirect_uri", customMessage: yomon);
        }
    }

    [Fact]
    public async Task DCR_notogri_shakldagi_tanani_rad_etadi()
    {
        // Massiv emas — satr
        await RadEtiladiAsync(new { client_name = "x", redirect_uris = McpFlow.RedirectUri }, "invalid_redirect_uri");

        // Umuman yo'q
        await RadEtiladiAsync(new { client_name = "x" }, "invalid_redirect_uri");

        // Bo'sh va 5 tadan ko'p
        await RadEtiladiAsync(new { client_name = "x", redirect_uris = Array.Empty<string>() }, "invalid_redirect_uri");
        await RadEtiladiAsync(
            new { client_name = "x", redirect_uris = Enumerable.Range(1, 6).Select(i => $"http://127.0.0.1:{5000 + i}/cb").ToArray() },
            "invalid_redirect_uri");

        // Satr bo'lmagan element
        await RadEtiladiAsync(new { client_name = "x", redirect_uris = new object[] { 1 } }, "invalid_redirect_uri");

        // grant_types: massiv emas / ruxsat etilmagan tur
        await RadEtiladiAsync(
            new { client_name = "x", redirect_uris = new[] { McpFlow.RedirectUri }, grant_types = "authorization_code" },
            "invalid_client_metadata");
        await RadEtiladiAsync(
            new { client_name = "x", redirect_uris = new[] { McpFlow.RedirectUri }, grant_types = ParolTuri },
            "invalid_client_metadata");

        // JSON obyekt emas
        await RadEtiladiAsync(new List<int> { 1, 2 }, "invalid_client_metadata");

        using var client = Flow().Client();
        using var buzuq = new StringContent("{ bu json emas", Encoding.UTF8, "application/json");
        using var javob = await client.PostAsync(new Uri("/oauth/register", UriKind.Relative), buzuq, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await javob.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_client_metadata");
    }

    [Fact]
    public async Task Mijoz_nomi_tozalanadi()
    {
        using var javob = await Flow().RegisterRawAsync(new
        {
            client_name = "Bonvi\u202E rasmiy\u200B\n\u0007" + new string('x', 200),
            redirect_uris = new[] { McpFlow.RedirectUri },
        });

        javob.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync(Ct));
        var nom = hujjat.RootElement.GetProperty("client_name").GetString()!;

        nom.Length.ShouldBeLessThanOrEqualTo(McpClient.MaxNameLength);
        nom.ShouldStartWith("Bonvi rasmiy xxx");
        nom.ShouldNotContain("\u202E", Case.Sensitive);
        nom.ShouldNotContain("\u200B", Case.Sensitive);
        nom.Any(char.IsControl).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Avtorizatsiya sahifasi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PKCE_bilan_toliq_oqim_token_beradi_va_bazada_faqat_xesh_turadi()
    {
        var flow = Flow();
        var hisob = await TestAccounts.CreateAsync(_host.Factory, Permissions.McpConnect, Permissions.EmployeesView);

        var clientId = await flow.RegisterAsync();
        var (verifier, challenge) = McpFlow.Pkce();
        var code = await flow.CodeAsync(clientId, challenge, hisob.Login, hisob.Password);
        code.ShouldStartWith("bhr_ac_");

        using var javob = await flow.TokenAsync(McpFlow.CodeForm(code, clientId, verifier));
        var matn = await javob.Content.ReadAsStringAsync(Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);
        javob.Headers.CacheControl?.NoStore.ShouldBe(true);

        using var hujjat = JsonDocument.Parse(matn);
        var r = hujjat.RootElement;
        var access = r.GetProperty("access_token").GetString()!;
        var refresh = r.GetProperty("refresh_token").GetString()!;

        access.ShouldStartWith("bhr_at_");
        refresh.ShouldStartWith("bhr_rt_");
        r.GetProperty("token_type").GetString().ShouldBe("Bearer");
        r.GetProperty("expires_in").GetInt32().ShouldBe(3600);
        r.GetProperty("scope").GetString().ShouldBe("hr.read");

        var init = await flow.InitializeAsync(access);
        init["serverInfo"]!["name"]!.GetValue<string>().ShouldBe("bonvi-hr");

        // Bazada sir emas, faqat SHA-256 xeshi; kod ishlatilgan deb belgilangan.
        var (xeshlar, ishlatilgan) = await _host.StoreAsync(async db =>
        {
            var grant = await db.McpGrants.AsNoTracking().SingleAsync(g => g.UserId == hisob.UserId, Ct);
            grant.ClientId.ShouldBe(clientId);
            grant.RevokedAt.ShouldBeNull();

            var tokens = await db.McpTokens.AsNoTracking().Where(t => t.GrantId == grant.Id).Select(t => t.TokenHash).ToListAsync(Ct);
            var codeRow = await db.McpAuthCodes.AsNoTracking().SingleAsync(c => c.UserId == hisob.UserId, Ct);
            return (tokens, codeRow.ConsumedAt is not null && codeRow.GrantId == grant.Id);
        });

        xeshlar.Count.ShouldBe(2);
        xeshlar.ShouldNotContain(access);
        xeshlar.ShouldNotContain(refresh);
        xeshlar.ShouldAllBe(xesh => xesh.Length == 64 && xesh.All(belgi => char.IsAsciiHexDigitLower(belgi)));
        ishlatilgan.ShouldBeTrue();
    }

    [Fact]
    public async Task Notogri_parol_kod_bermaydi()
    {
        var flow = Flow();
        var hisob = await TestAccounts.CreateAsync(_host.Factory, Permissions.McpConnect, Permissions.EmployeesView);
        var clientId = await flow.RegisterAsync();
        var (_, challenge) = McpFlow.Pkce();

        using var javob = await flow.LoginAsync(clientId, challenge, hisob.Login, hisob.Password + "x");

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        javob.Headers.Location.ShouldBeNull();

        var html = await McpFlow.HtmlAsync(javob);
        html.ShouldContain("Telefon raqami yoki parol noto'g'ri.");

        // Qayta urinish uchun YANGI tutqich beriladi, rozilik bosqichi yo'q.
        McpFlow.ReqOf(html).ShouldNotBeNullOrWhiteSpace();
        html.ShouldNotContain("Ruxsat berish");

        // Mavjud bo'lmagan raqam ham AYNAN shu umumiy xato bilan (hisob borligi oshkor qilinmaydi).
        using var notanish = await Flow().LoginAsync(clientId, challenge, "991234567", "Nimadir_2026");
        (await McpFlow.HtmlAsync(notanish)).ShouldContain("Telefon raqami yoki parol noto'g'ri.");

        (await _host.StoreAsync(db => db.McpAuthCodes.CountAsync(c => c.UserId == hisob.UserId, Ct))).ShouldBe(0);
    }

    /// <summary>
    /// HR menejeriga o'xshash hisob: xodimlarni ko'radi, lekin rolida
    /// «AI orqali o'qish» yo'q (<c>hr_manager</c> sukutida ham u yo'q).
    /// </summary>
    [Fact]
    public async Task Mcp_connect_ruxsati_yoq_hisob_403_sahifa_oladi()
    {
        var flow = Flow();
        var hisob = await TestAccounts.CreateAsync(
            _host.Factory,
            Permissions.EmployeesView,
            Permissions.EmploymentView,
            Permissions.RecruitingView,
            Permissions.AttendanceView);

        var clientId = await flow.RegisterAsync();
        var (_, challenge) = McpFlow.Pkce();

        using var javob = await flow.LoginAsync(clientId, challenge, hisob.Login, hisob.Password);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        javob.Headers.Location.ShouldBeNull();

        var html = await McpFlow.HtmlAsync(javob);
        html.ShouldContain("AI orqali o'qish");
        html.ShouldNotContain("name=\"req\"");

        (await _host.StoreAsync(db => db.McpAuthCodes.CountAsync(c => c.UserId == hisob.UserId, Ct))).ShouldBe(0);
    }

    [Fact]
    public async Task Faol_bolmagan_hisob_kod_olmaydi()
    {
        var flow = Flow();
        var hisob = await TestAccounts.CreateAsync(_host.Factory, Permissions.McpConnect, Permissions.EmployeesView);

        using (var admin = await _host.AdminClientAsync())
        using (var faolsiz = await admin.PostAsync(new Uri($"/api/users/{hisob.UserId}/deactivate", UriKind.Relative), null, Ct))
        {
            faolsiz.StatusCode.ShouldBe(HttpStatusCode.NoContent, await faolsiz.Content.ReadAsStringAsync(Ct));
        }

        var clientId = await flow.RegisterAsync();
        var (_, challenge) = McpFlow.Pkce();

        using var javob = await flow.LoginAsync(clientId, challenge, hisob.Login, hisob.Password);

        javob.Headers.Location.ShouldBeNull();
        var html = await McpFlow.HtmlAsync(javob);
        html.ShouldContain("Hisob faol emas");
        html.ShouldNotContain("Ruxsat berish");

        (await _host.StoreAsync(db => db.McpAuthCodes.CountAsync(c => c.UserId == hisob.UserId, Ct))).ShouldBe(0);
    }

    [Fact]
    public async Task Notanish_mijoz_yoki_mos_kelmagan_qaytish_manzili_yonaltirilmaydi()
    {
        var flow = Flow();
        var clientId = await flow.RegisterAsync();
        var (_, challenge) = McpFlow.Pkce();

        using (var notanish = await flow.GetAsync(McpFlow.AuthorizeUri("mcp_notanish_mijoz", challenge)))
        {
            notanish.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            notanish.Headers.Location.ShouldBeNull();
            (await McpFlow.HtmlAsync(notanish)).ShouldContain("Noma'lum ilova");
        }

        foreach (var boshqa in new[] { "https://evil.example.com/cb", "https://claude.ai/api/mcp/auth_callback", "http://127.0.0.1:53123/boshqa" })
        {
            using var javob = await flow.GetAsync(McpFlow.AuthorizeUri(clientId, challenge, redirect: boshqa));
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, boshqa);
            javob.Headers.Location.ShouldBeNull();
            (await McpFlow.HtmlAsync(javob)).ShouldContain("Qaytish manzili", customMessage: boshqa);
        }

        // Loopback'da boshqa port — mumkin (RFC 8252 §7.3): sahifa ochiladi.
        using var port = await flow.GetAsync(McpFlow.AuthorizeUri(clientId, challenge, redirect: "http://127.0.0.1:61000/callback"));
        port.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("yoq")]
    [InlineData("S256-qisqa")]
    public async Task PKCE_S256_bolmasa_xato_bilan_qaytariladi(string holat)
    {
        var flow = Flow();
        var clientId = await flow.RegisterAsync();
        var (verifier, challenge) = McpFlow.Pkce();

        var manzil = holat switch
        {
            "plain" => McpFlow.AuthorizeUri(clientId, verifier, method: "plain"),
            "yoq" => McpFlow.AuthorizeUri(clientId, challenge: null, method: null),
            _ => McpFlow.AuthorizeUri(clientId, challenge[..20]),
        };

        using var javob = await flow.GetAsync(manzil);

        javob.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var parametrlar = McpFlow.RedirectQuery(javob);
        parametrlar["error"].ShouldBe("invalid_request");
        parametrlar["state"].ShouldBe(McpFlow.State);
        parametrlar.ShouldNotContainKey("code");
    }

    [Fact]
    public async Task Rad_etilsa_access_denied_qaytadi()
    {
        var flow = Flow();
        var hisob = await TestAccounts.CreateAsync(_host.Factory, Permissions.McpConnect);
        var clientId = await flow.RegisterAsync();
        var (_, challenge) = McpFlow.Pkce();

        var parametrlar = await flow.ConsentAsync(clientId, challenge, hisob.Login, hisob.Password, decision: "deny");

        parametrlar["error"].ShouldBe("access_denied");
        parametrlar.ShouldNotContainKey("code");
    }

    [Fact]
    public async Task Kirish_formasi_IP_boyicha_cheklangan()
    {
        var flow = Flow();
        var oxirgi = HttpStatusCode.OK;

        for (var i = 0; i < 12; i++)
        {
            using var javob = await flow.PostFormAsync("/oauth/authorize", new Dictionary<string, string>
            {
                ["step"] = "login",
                ["req"] = "yoq",
                ["login"] = "991234567",
                ["password"] = "x",
            });

            oxirgi = javob.StatusCode;
        }

        oxirgi.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ────────────────────────────────────────────────────────────
    //  Token
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Kod_qayta_ishlatilsa_rad_etiladi_va_birinchi_ulanish_bekor_qilinadi()
    {
        var flow = Flow();
        var hisob = await TestAccounts.CreateAsync(_host.Factory, Permissions.McpConnect, Permissions.EmployeesView);
        var clientId = await flow.RegisterAsync();
        var (verifier, challenge) = McpFlow.Pkce();
        var code = await flow.CodeAsync(clientId, challenge, hisob.Login, hisob.Password);

        // Boshqa verifier — PKCE yiqiladi, kod esa ISHLATILMAYDI.
        using (var notogri = await flow.TokenAsync(McpFlow.CodeForm(code, clientId, McpFlow.Pkce().Verifier)))
        {
            notogri.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await notogri.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_grant");
        }

        // Boshqa mijoz nomidan — rad.
        using (var begona = await flow.TokenAsync(McpFlow.CodeForm(code, "mcp_begona", verifier)))
        {
            begona.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        McpTokens birinchi;
        using (var ok = await flow.TokenAsync(McpFlow.CodeForm(code, clientId, verifier)))
        {
            birinchi = await McpFlow.TokensOfAsync(ok, clientId);
        }

        (await flow.ProbeAsync(birinchi.Access)).ShouldBe(HttpStatusCode.OK);

        // Qayta kelgan kod — u sizib chiqqan: rad etiladi VA birinchi almashinuv yaratgan ulanish o'ladi.
        using (var takror = await flow.TokenAsync(McpFlow.CodeForm(code, clientId, verifier)))
        {
            takror.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await takror.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_grant");
        }

        (await flow.ProbeAsync(birinchi.Access)).ShouldBe(HttpStatusCode.Unauthorized);

        using (var yangilash = await flow.TokenAsync(McpFlow.RefreshForm(birinchi.Refresh, clientId)))
        {
            yangilash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var kim = await _host.StoreAsync(db => db.McpGrants.AsNoTracking()
            .Where(g => g.UserId == hisob.UserId)
            .Select(g => g.RevokedBy)
            .SingleAsync(Ct));
        kim.ShouldBe("system:code-replay");
    }

    [Fact]
    public async Task Refresh_aylanadi_eski_refresh_qayta_ishlatilsa_ulanish_bekor_qilinadi()
    {
        var flow = Flow();
        var (hisob, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);

        McpTokens ikkinchi;
        using (var yangilash = await flow.TokenAsync(McpFlow.RefreshForm(tokens.Refresh, tokens.ClientId)))
        {
            ikkinchi = await McpFlow.TokensOfAsync(yangilash, tokens.ClientId);
        }

        ikkinchi.Refresh.ShouldNotBe(tokens.Refresh);
        ikkinchi.Access.ShouldNotBe(tokens.Access);
        (await flow.ProbeAsync(ikkinchi.Access)).ShouldBe(HttpStatusCode.OK);

        // Boshqa mijoz yangi tokenni ishlata olmaydi.
        using (var begona = await flow.TokenAsync(McpFlow.RefreshForm(ikkinchi.Refresh, "mcp_begona")))
        {
            begona.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // ESKI yangilash tokeni qayta keldi — o'g'irlik belgisi: butun ulanish o'ladi.
        using (var eski = await flow.TokenAsync(McpFlow.RefreshForm(tokens.Refresh, tokens.ClientId)))
        {
            eski.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await eski.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_grant");
        }

        (await flow.ProbeAsync(ikkinchi.Access)).ShouldBe(HttpStatusCode.Unauthorized);

        using (var olik = await flow.TokenAsync(McpFlow.RefreshForm(ikkinchi.Refresh, tokens.ClientId)))
        {
            olik.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var kim = await _host.StoreAsync(db => db.McpGrants.AsNoTracking()
            .Where(g => g.UserId == hisob.UserId)
            .Select(g => g.RevokedBy)
            .SingleAsync(Ct));
        kim.ShouldBe("system:refresh-reuse");
    }

    [Fact]
    public async Task Oauth_revoke_ulanishni_uzadi()
    {
        var flow = Flow();
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);
        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.OK);

        // Yangilash tokenini bekor qilish butun juftni (ulanishni) yopadi.
        using (var javob = await flow.PostFormAsync("/oauth/revoke", new Dictionary<string, string> { ["token"] = tokens.Refresh }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.Unauthorized);

        using (var yangilash = await flow.TokenAsync(McpFlow.RefreshForm(tokens.Refresh, tokens.ClientId)))
        {
            yangilash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // Notanish token — baribir 200 (RFC 7009 §2.2).
        using var notanish = await flow.PostFormAsync("/oauth/revoke", new Dictionary<string, string> { ["token"] = "bhr_rt_notanish" });
        notanish.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────
    //  Har chaqiruvdagi yangilik
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Roldan_mcp_connect_olinsa_keyingi_chaqiruv_401()
    {
        var flow = Flow();
        var (hisob, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);
        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.OK);

        using var admin = await _host.AdminClientAsync();
        var rollar = await admin.GetFromJsonAsync<List<McpRoleResponse>>(new Uri("/api/roles", UriKind.Relative), Ct);
        var rol = rollar!.Single(r => r.Id == hisob.RoleId);
        rol.Permissions.ShouldContain(Permissions.McpConnect);

        // «Rollar» muharriri orqali — haqiqiy yo'l: rol tahriri kesh versiyasini oshiradi.
        using (var tahrir = await admin.PutAsJsonAsync(
                   new Uri($"/api/roles/{rol.Id}", UriKind.Relative),
                   new { name = rol.Name, permissions = new[] { Permissions.EmployeesView } },
                   Ct))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await tahrir.Content.ReadAsStringAsync(Ct));
        }

        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.Unauthorized);

        using var yangilash = await flow.TokenAsync(McpFlow.RefreshForm(tokens.Refresh, tokens.ClientId));
        yangilash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await yangilash.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_grant");
    }

    [Fact]
    public async Task Hisob_faolsizlantirilsa_ulanish_darhol_ishlamay_qoladi()
    {
        var flow = Flow();
        var (hisob, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);
        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.OK);

        using (var admin = await _host.AdminClientAsync())
        using (var faolsiz = await admin.PostAsync(new Uri($"/api/users/{hisob.UserId}/deactivate", UriKind.Relative), null, Ct))
        {
            faolsiz.StatusCode.ShouldBe(HttpStatusCode.NoContent, await faolsiz.Content.ReadAsStringAsync(Ct));
        }

        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MCP_ochirilsa_mcp_503_va_oauth_404()
    {
        var flow = Flow();
        var tokens = await flow.ConnectAdminAsync();
        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.OK);

        using var admin = await _host.AdminClientAsync();
        (await McpHost.SetEnabledAsync(admin, enabled: false)).State.ShouldBe("Off");

        try
        {
            (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await flow.ProbeAsync(null)).ShouldBe(HttpStatusCode.ServiceUnavailable);

            foreach (var yol in new[]
                     {
                         "/.well-known/oauth-protected-resource",
                         "/.well-known/oauth-protected-resource/mcp",
                         "/.well-known/oauth-authorization-server",
                         "/.well-known/oauth-authorization-server/mcp",
                     })
            {
                using var javob = await flow.GetAsync(new Uri(yol, UriKind.Relative));
                javob.StatusCode.ShouldBe(HttpStatusCode.NotFound, yol);
            }

            using (var sahifa = await flow.GetAsync(McpFlow.AuthorizeUri(tokens.ClientId, McpFlow.Pkce().Challenge)))
            {
                sahifa.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }

            using (var royxat = await flow.RegisterRawAsync(new { client_name = "x", redirect_uris = new[] { McpFlow.RedirectUri } }))
            {
                royxat.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }

            using (var token = await flow.TokenAsync(McpFlow.RefreshForm(tokens.Refresh, tokens.ClientId)))
            {
                token.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }

            using (var bekor = await flow.PostFormAsync("/oauth/revoke", new Dictionary<string, string> { ["token"] = tokens.Refresh }))
            {
                bekor.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }

            using (var forma = await flow.PostFormAsync("/oauth/authorize", new Dictionary<string, string> { ["step"] = "login", ["req"] = "x" }))
            {
                forma.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }
        finally
        {
            (await McpHost.SetEnabledAsync(admin, enabled: true)).State.ShouldBe("On");
        }

        // Qayta yoqilganda mavjud ulanish yana ishlaydi (o'chirish uni bekor qilmaydi).
        (await flow.ProbeAsync(tokens.Access)).ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────

    private async Task RadEtiladiAsync(object tana, string kutilganXato)
    {
        using var javob = await Flow().RegisterRawAsync(tana);
        var matn = await javob.Content.ReadAsStringAsync(Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, matn);
        matn.ShouldContain(kutilganXato);
    }

    /// <summary>JSON massividagi satrlar — tartiblangan, vergul bilan (taqqoslash oson bo'lsin).</summary>
    private static string Satrlar(JsonElement massiv)
        => string.Join(',', massiv.EnumerateArray().Select(element => element.GetString()!).Order(StringComparer.Ordinal));
}

/// <summary>
/// Dinamik ro'yxatdan o'tishning umumiy chegarasi — hali ULANMAGAN mijozlar
/// soni bo'yicha (<c>docs/SPEC-mcp.md</c> §5).
/// </summary>
/// <remarks>
/// Alohida sinf — alohida baza: chegaraga yetgan bazada bir soat davomida
/// yangi mijoz ro'yxatdan o'tolmaydi va bu boshqa testlarni yiqitardi.
/// </remarks>
public sealed class McpRegistrationCapTests : IClassFixture<McpHost>
{
    private readonly McpHost _host;

    public McpRegistrationCapTests(McpHost host) => _host = host;

    [Fact]
    public async Task Ulanmagan_mijozlar_chegarasidan_keyin_ro_yxatdan_otish_429()
    {
        // Har so'rov — yangi IP: IP bo'yicha soatlik chegara emas, UMUMIY chegara sinaladi.
        for (var i = 0; i < McpLimits.MaxPendingRegistrations; i++)
        {
            await new McpFlow(_host).RegisterAsync("Sinov " + i);
        }

        using var javob = await new McpFlow(_host).RegisterRawAsync(new { client_name = "ortiqcha", redirect_uris = new[] { McpFlow.RedirectUri } });

        javob.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("temporarily_unavailable");
    }
}
