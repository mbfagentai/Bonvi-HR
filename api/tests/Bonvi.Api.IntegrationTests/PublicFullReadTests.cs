using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bonvi.Api.Endpoints;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Doirasi bilan yaratilgan kalit — testlar uchun.</summary>
public sealed record ScopedKeyResponse(Guid Id, string Name, string Prefix, string Key, string Scope);

/// <summary>
/// «To'liq o'qish» doirasidagi tashqi API kaliti (<c>/api/public/v1/full</c>).
/// </summary>
/// <remarks>
/// Egasining qarori (2026-10-11): rahbarning AI yordamchisi kalit bilan
/// «barcha ma'lumotni» olsin — pasport, JSHSHIR va fayllardan tashqari.
/// Vositalar MCP bilan bir xil, shuning uchun MCP xosti (faqat-o'qish roli
/// tayyor) ishlatiladi.
/// </remarks>
public sealed class PublicFullReadTests : IClassFixture<McpHost>
{
    private readonly McpHost _host;

    public PublicFullReadTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Oddiy_kalit_toliq_oqishga_kira_olmaydi_403()
    {
        var kalit = await KalitAsync(scope: null);
        kalit.Scope.ShouldBe("Basic", "doira berilmasa — eski xulq: Basic");

        using var mijoz = KalitMijozi(kalit.Key);

        foreach (var yol in new[] { "/api/public/v1/full", "/api/public/v1/full/overview" })
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), Ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, yol);
            (await Json(javob))["title"]!.GetValue<string>().ShouldBe("tashqi.doira_yetmaydi");
        }

        // Odatiy ommaviy yuza esa ishlayveradi.
        using var ping = await mijoz.GetAsync(new Uri("/api/public/v1/ping", UriKind.Relative), Ct);
        ping.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Katalog_barcha_MCP_vositalarini_parametrlari_bilan_beradi()
    {
        var kalit = await KalitAsync("FullRead");
        kalit.Scope.ShouldBe("FullRead");

        using var mijoz = KalitMijozi(kalit.Key);
        using var javob = await mijoz.GetAsync(new Uri("/api/public/v1/full", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        var vositalar = (await Json(javob))["tools"]!.AsArray();
        var nomlar = vositalar.Select(tool => tool!["name"]!.GetValue<string>()).ToList();

        var kutilgan = McpSetup.ToolTypes
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolAttribute), false)
                .OfType<ModelContextProtocol.Server.McpServerToolAttribute>()
                .FirstOrDefault()?.Name)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

        nomlar.ShouldBe(kutilgan, "tashqi API MCP serveri ochadigan AYNAN o'sha vositalarni berishi kerak");

        var qidiruv = vositalar.Single(tool => tool!["name"]!.GetValue<string>() == "search_employees")!;
        qidiruv["url"]!.GetValue<string>().ShouldBe("/api/public/v1/full/search_employees");
        qidiruv["parameters"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ShouldContain("query");

        var karta = vositalar.Single(tool => tool!["name"]!.GetValue<string>() == "get_employee")!;
        karta["parameters"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "id")!["required"]!.GetValue<bool>()
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Toliq_kalit_xodimni_oqiydi_lekin_pasport_jshshir_rasmni_ololmaydi()
    {
        var belgi = McpHost.Belgi();
        var familiya = "Тўлиқов" + belgi;
        var jshshir = "3150588" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);
        var pasport = "KX9" + Random.Shared.Next(100_000, 999_999).ToString(CultureInfo.InvariantCulture);
        var rasm = $"toliqsir/rasm-{belgi}.jpg";

        Guid xodimId;

        await using (var qamrov = _host.Factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create("To'liq sinov MChJ " + belgi).Value;
            var bolim = Department.Create("To'liq bo'limi " + belgi).Value;
            var xodim = Employee.Create(FullName.Create(familiya, "Ходим", "Тестович").Value, new DateOnly(1988, 5, 15)).Value;

            xodim.SetPinfl(Pinfl.FromTrusted(jshshir)).IsSuccess.ShouldBeTrue();
            xodim.SetPassport(PassportSeries.Create(pasport).Value);
            xodim.SetAddress("Toshkent, Chilonzor 1-kvartal");
            xodim.SetPhoto(rasm);

            kontekst.AddRange(firma, bolim, xodim);
            kontekst.Add(Employment.Hire(xodim.Id, firma.Id, McpToolContext.Today.AddDays(-40), bolim.Id).Value);
            await kontekst.SaveChangesAsync(Ct);

            xodimId = xodim.Id;
        }

        var kalit = await KalitAsync("FullRead");
        using var mijoz = KalitMijozi(kalit.Key);

        foreach (var yol in new[]
                 {
                     $"/api/public/v1/full/search_employees?query={Uri.EscapeDataString(familiya)}&status=all",
                     $"/api/public/v1/full/get_employee?id={xodimId}",
                 })
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            var matn = await javob.Content.ReadAsStringAsync(Ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);
            matn.ShouldContain(familiya);
            matn.ShouldNotContain(jshshir, Case.Sensitive, "JSHSHIR hech qachon chiqmaydi");
            matn.ShouldNotContain(pasport, Case.Insensitive, "pasport hech qachon chiqmaydi");
            matn.ShouldNotContain(rasm, Case.Sensitive, "fayl kaliti hech qachon chiqmaydi");
        }

        // «Barcha ma'lumot» — manzil (oddiy kalitda yo'q) bu yerda bor.
        using (var karta = await mijoz.GetAsync(new Uri($"/api/public/v1/full/get_employee?id={xodimId}", UriKind.Relative), Ct))
        {
            (await karta.Content.ReadAsStringAsync(Ct)).ShouldContain("Chilonzor");
        }
    }

    [Fact]
    public async Task Toliq_kalit_hech_bir_vositada_ruxsat_yoq_olmaydi()
    {
        var kalit = await KalitAsync("FullRead");
        using var mijoz = KalitMijozi(kalit.Key);

        using var katalog = await mijoz.GetAsync(new Uri("/api/public/v1/full", UriKind.Relative), Ct);
        var vositalar = (await Json(katalog))["tools"]!.AsArray();

        foreach (var vosita in vositalar)
        {
            var nom = vosita!["name"]!.GetValue<string>();

            // Majburiy parametri borlarga (get_employee, get_candidate) mavjud bo'lmagan id.
            var majburiy = vosita["parameters"]!.AsArray()
                .Where(item => item!["required"]!.GetValue<bool>())
                .Select(item => $"{item!["name"]!.GetValue<string>()}={Guid.NewGuid()}");

            var sorov = string.Join('&', majburiy);
            using var javob = await mijoz.GetAsync(
                new Uri($"/api/public/v1/full/{nom}{(sorov.Length > 0 ? "?" + sorov : string.Empty)}", UriKind.Relative),
                Ct);

            var matn = await javob.Content.ReadAsStringAsync(Ct);

            javob.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, $"{nom}: {matn}");
            javob.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError, $"{nom}: {matn}");
            matn.ShouldNotContain("tashqi.ruxsat_yoq", Case.Sensitive, $"{nom}: «ReadPermissions» ro'yxatini to'ldiring");
        }

        PublicFullReadEndpoints.ReadPermissions.ShouldNotContain(Bonvi.Domain.Identity.Permissions.McpConnect);
    }

    [Fact]
    public async Task Notogri_sorovlar_tushunarli_xato_beradi()
    {
        var kalit = await KalitAsync("FullRead");
        using var mijoz = KalitMijozi(kalit.Key);

        async Task Kutish(string yol, HttpStatusCode holat, string kod)
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            var tana = await Json(javob);

            javob.StatusCode.ShouldBe(holat, $"{yol}: {tana}");
            tana["title"]!.GetValue<string>().ShouldBe(kod, yol);
        }

        await Kutish("/api/public/v1/full/yoq_vosita", HttpStatusCode.NotFound, "tashqi.vosita_topilmadi");
        await Kutish("/api/public/v1/full/search_employees?qury=Ali", HttpStatusCode.BadRequest, "tashqi.parametr_notogri");
        await Kutish("/api/public/v1/full/search_employees?page=bir", HttpStatusCode.BadRequest, "tashqi.parametr_notogri");
        await Kutish("/api/public/v1/full/get_employee", HttpStatusCode.BadRequest, "tashqi.parametr_notogri");
        await Kutish("/api/public/v1/full/attendance_day?date=31-12-2026", HttpStatusCode.BadRequest, "tashqi.sorov_notogri");
    }

    [Fact]
    public async Task Har_bir_chaqiruv_MCP_jurnaliga_yoziladi()
    {
        var kalit = await KalitAsync("FullRead");
        using var mijoz = KalitMijozi(kalit.Key);

        using (var javob = await mijoz.GetAsync(new Uri("/api/public/v1/full/overview", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }

        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var yozuv = await kontekst.McpAudit.AsNoTracking()
            .Where(entry => entry.ClientId == "api-key:" + kalit.Prefix)
            .SingleAsync(Ct);

        yozuv.Tool.ShouldBe("api:overview");
        yozuv.Outcome.ShouldBe("ok");
        yozuv.ClientName.ShouldBe("Tashqi API: " + kalit.Name);
    }

    [Fact]
    public async Task Bekor_qilingan_toliq_kalit_401()
    {
        var kalit = await KalitAsync("FullRead");

        using (var admin = await _host.AdminClientAsync())
        using (var bekor = await admin.PostAsync(new Uri($"/api/api-keys/{kalit.Id}/revoke", UriKind.Relative), null, Ct))
        {
            bekor.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var mijoz = KalitMijozi(kalit.Key);
        using var javob = await mijoz.GetAsync(new Uri("/api/public/v1/full/overview", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Notogri_doira_bilan_kalit_yaratilmaydi()
    {
        using var admin = await _host.AdminClientAsync();
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = PublicApiHarness.NoyobNom("Doira"), scope = "Hammasi" },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Toliq_kalitni_faqat_barcha_korish_ruxsati_bor_odam_yaratadi()
    {
        // Faqat «sozlamalarni boshqarish» — kalit yarata oladi, lekin oklad ko'rmaydi.
        var token = await TestAccounts.TokenAsync(_host.Factory, Bonvi.Domain.Identity.Permissions.SettingsManage);
        using var mijoz = _host.BearerClient(token);

        using (var toliq = await mijoz.PostAsJsonAsync(
                   new Uri("/api/api-keys", UriKind.Relative),
                   new { name = PublicApiHarness.NoyobNom("Ortiqcha"), scope = "FullRead" },
                   Ct))
        {
            toliq.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await toliq.Content.ReadAsStringAsync(Ct));
            (await Json(toliq))["title"]!.GetValue<string>().ShouldBe("tashqi.doira_ruxsat_yoq");
        }

        using var oddiy = await mijoz.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = PublicApiHarness.NoyobNom("Oddiy"), scope = "Basic" },
            Ct);

        oddiy.StatusCode.ShouldBe(HttpStatusCode.OK, await oddiy.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Jurnal_yozilmasa_malumot_qaytmaydi()
    {
        var kalit = await KalitAsync("FullRead");
        using var mijoz = KalitMijozi(kalit.Key);

        // Jurnal jadvaliga yozish mumkin bo'lmay qoladi (eski qatorlarga tegmaydi).
        await using (var ulanish = new Npgsql.NpgsqlConnection(_host.ConnectionString))
        {
            await ulanish.OpenAsync(Ct);
            await using var buyruq = new Npgsql.NpgsqlCommand(
                "ALTER TABLE mcp_audit ADD CONSTRAINT ck_sinov_jurnal_yopiq CHECK (false) NOT VALID", ulanish);
            await buyruq.ExecuteNonQueryAsync(Ct);
        }

        try
        {
            using var javob = await mijoz.GetAsync(new Uri("/api/public/v1/full/overview", UriKind.Relative), Ct);
            var tana = await Json(javob);

            javob.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, tana.ToJsonString());
            tana["title"]!.GetValue<string>().ShouldBe("tashqi.jurnal_yozilmadi");
            tana.AsObject().Select(pair => pair.Key).ShouldBeSubsetOf(["type", "title", "status", "detail", "instance", "traceId"]);
        }
        finally
        {
            await using var ulanish = new Npgsql.NpgsqlConnection(_host.ConnectionString);
            await ulanish.OpenAsync(Ct);
            await using var buyruq = new Npgsql.NpgsqlCommand("ALTER TABLE mcp_audit DROP CONSTRAINT ck_sinov_jurnal_yopiq", ulanish);
            await buyruq.ExecuteNonQueryAsync(Ct);
        }
    }

    [Fact]
    public async Task Kalit_va_token_bir_birining_yuzasiga_kira_olmaydi()
    {
        // Kalitsiz — 401.
        using (var kalitsiz = _host.Client(McpFlow.RandomIp()))
        {
            foreach (var yol in new[] { "/api/public/v1/full", "/api/public/v1/full/overview" })
            {
                using var javob = await kalitsiz.GetAsync(new Uri(yol, UriKind.Relative), Ct);
                javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, yol);
            }
        }

        // Super administrator tokeni ham to'liq o'qishni ochmaydi — u faqat kalit bilan.
        using (var admin = await _host.AdminClientAsync())
        using (var javob = await admin.GetAsync(new Uri("/api/public/v1/full/overview", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // To'liq kalit ichki yuzaga va MCP'ga kira olmaydi.
        var kalit = await KalitAsync("FullRead");
        using var mijoz = KalitMijozi(kalit.Key);

        foreach (var yol in new[] { "/api/employees", "/api/api-keys", "/api/mcp/status" })
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, yol);
        }

        using (var mcp = await mijoz.PostAsync(new Uri("/mcp", UriKind.Relative), new StringContent("{}"), Ct))
        {
            mcp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public void Parallel_chegara_bolinma_va_umumiy_boyicha_ishlaydi()
    {
        using var gate = new McpConcurrencyGate();

        gate.TryEnter("api-key:a", out var birinchi).ShouldBeTrue();
        gate.TryEnter("api-key:a", out var ikkinchi).ShouldBeTrue();
        gate.TryEnter("api-key:a", out _).ShouldBeFalse("bitta kalit — ko'pi bilan 2 ta parallel chaqiruv");

        birinchi!.Dispose();
        birinchi.Dispose(); // ikki marta yopish hisobni buzmaydi
        gate.TryEnter("api-key:a", out var uchinchi).ShouldBeTrue();

        // Umumiy chegara (6): yana 4 ta boshqa bo'linma sig'adi, 5-chisi — yo'q.
        var boshqalar = Enumerable.Range(0, 4)
            .Select(i => gate.TryEnter($"grant-{i}", out var joy) ? joy : null)
            .ToList();

        boshqalar.ShouldAllBe(joy => joy != null);
        gate.TryEnter("grant-x", out _).ShouldBeFalse("butun jarayon — ko'pi bilan 6 ta");

        foreach (var joy in boshqalar)
        {
            joy!.Dispose();
        }

        ikkinchi!.Dispose();
        uchinchi!.Dispose();
    }

    // ────────────────────────────────────────────────────────────

    private async Task<ScopedKeyResponse> KalitAsync(string? scope)
    {
        using var admin = await _host.AdminClientAsync();
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = PublicApiHarness.NoyobNom("Toliq"), scope },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        var kalit = await javob.Content.ReadFromJsonAsync<ScopedKeyResponse>(Ct);
        kalit.ShouldNotBeNull();
        return kalit;
    }

    private HttpClient KalitMijozi(string kalit)
    {
        var mijoz = _host.Client(McpFlow.RandomIp());
        mijoz.DefaultRequestHeaders.Add("X-API-Key", kalit);
        return mijoz;
    }

    private static async Task<JsonNode> Json(HttpResponseMessage javob) =>
        JsonNode.Parse(await javob.Content.ReadAsStringAsync(Ct)) ?? new JsonObject();
}
