using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Infrastructure.Mcp;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>«AI ulanish» holati (test uchun) — <c>GET /api/mcp/status</c>.</summary>
public sealed record McpStatusResponse(string State, string? Reason, bool Enabled, string ServerUrl);

/// <summary>
/// AI ulanish (MCP) testlari uchun xost — o'z vaqtinchalik bazasi va MCP
/// sozlamalari bilan, MCP YOQILGAN holda (<c>docs/SPEC-mcp.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega umumiy <see cref="BonviApiFactory"/> yetmaydi.</b> Unda
/// <c>Mcp:DbPassword</c> yo'q — MCP «NotConfigured» va har bir manzil 404/503.
/// Bu yerda o'sha baza ustida sozlamalari qo'shilgan hosil xost ochiladi
/// (<see cref="AttendanceIngestHost"/> bilan bir xil naqsh).
/// </para>
/// <para>
/// <b>Rol nomi — <see cref="DbRole"/>, hech qachon <c>bonvi_mcp</c> emas.</b>
/// PostgreSQL roli BAZAGA emas, butun klasterga tegishli: dev roli ishlatilsa,
/// testlar dev API'ning parolini almashtirib yuborardi. Ulanishlar chegarasi
/// ham klaster bo'yicha (prod'da 8) — parallel test sinflarining har biri o'z
/// havzasini ochadi, shu sababli bu yerda u ko'tariladi.
/// </para>
/// <para>
/// Har bir test sinfi o'z nusxasini oladi (<c>IClassFixture</c>): o'chirgichni
/// o'chirib-yoqadigan test boshqa sinfning ulanishlarini yiqitmasin.
/// </para>
/// </remarks>
public sealed class McpHost : IAsyncLifetime
{
    /// <summary>Testlarning o'qish roli — dev'dagi <c>bonvi_mcp</c> dan ALOHIDA.</summary>
    public const string DbRole = "bonvi_mcp_test";

    /// <summary>O'qish rolining test paroli (hamma sinf uchun bir xil — rol klasterda bitta).</summary>
    public const string DbPassword = "Mcp_Sinov_Parol_2026";

    /// <summary>
    /// O'qish rolini tayyorlash (xost ishga tushishi va «Qayta tekshirish») shu
    /// jarayonda BIR VAQTDA bittadan.
    /// </summary>
    /// <remarks>
    /// Rol klasterda bitta, har sinf esa o'z xostida uni qayta tayyorlaydi
    /// (<c>ALTER ROLE</c>, <c>GRANT</c>). Olti xost bir vaqtda kelganda PostgreSQL
    /// «tuple concurrently updated» (<c>XX000</c>) beradi va ilovaning 5 urinishi
    /// ham yetmay, xost «Faulty» bo'lib qolardi. Bu faqat testdagi holat: prod'da
    /// rolni bitta jarayon tayyorlaydi.
    /// </remarks>
    public static readonly SemaphoreSlim ProvisionLock = new(1, 1);

    private readonly BonviApiFactory _baza = new();

    /// <summary>MCP sozlangan xost.</summary>
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Asosiy (egasi) ulanish satri — shu sinfning vaqtinchalik bazasi.</summary>
    public string ConnectionString => _baza.ConnectionString;

    /// <summary>O'qish roli bilan, havzasiz ulanish satri — testning o'zi SQL yuborishi uchun.</summary>
    public string ReadOnlyConnectionString => new NpgsqlConnectionStringBuilder(_baza.ConnectionString)
    {
        Username = DbRole,
        Password = DbPassword,
        Pooling = false,
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _baza.InitializeAsync().ConfigureAwait(false);

        Factory = _baza.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Mcp:DbPassword", DbPassword);
            builder.UseSetting("Mcp:DbRole", DbRole);
            builder.UseSetting("Mcp:DbConnectionLimit", "100");
        });

        // Xost shu yerda ko'tariladi: migratsiya, urug'lar va o'qish rolini
        // tayyorlash (`DatabaseInitializer` → `McpRuntime.CheckAsync`).
        await ProvisionLock.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

        try
        {
            _ = Factory.Services;
        }
        finally
        {
            ProvisionLock.Release();
        }

        using var admin = await AdminClientAsync().ConfigureAwait(false);
        var holat = await SetEnabledAsync(admin, enabled: true).ConfigureAwait(false);

        holat.State.ShouldBe("On", $"MCP yoqilmadi: {holat.State} — {holat.Reason}");
    }

    public async ValueTask DisposeAsync()
    {
        // Hosil qilingan xost asosiy fabrika bilan birga yopiladi.
        await _baza.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Yo'naltirishga ERGASHMAYDIGAN mijoz, o'z soxta IP'si bilan.</summary>
    /// <remarks>
    /// TestServer'da mijoz IP'si yo'q, IP bo'yicha cheklovlar esa yoqiq
    /// (<c>mcp-authorize</c>, <c>mcp-register</c>, <c>login</c>). Har bir oqim
    /// o'z <c>X-Forwarded-For</c> ini oladi — testlar bir-birining chegarasini yemasin.
    /// </remarks>
    public HttpClient Client(string ip)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("bonvi-mcp-tests/1.0");
        return client;
    }

    /// <summary>Boshlang'ich super administrator (barcha ruxsatlar, jumladan <c>mcp.connect</c>) mijozi.</summary>
    public async Task<HttpClient> AdminClientAsync()
    {
        var client = Client(McpFlow.RandomIp());

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>Faqat shu JWT bilan mijoz (ruxsati cheklangan hisob uchun).</summary>
    public HttpClient BearerClient(string accessToken)
    {
        var client = Client(McpFlow.RandomIp());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>O'chirgich — <c>PUT /api/mcp/enabled</c>.</summary>
    public static async Task<McpStatusResponse> SetEnabledAsync(HttpClient admin, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(admin);

        using var javob = await admin.PutAsJsonAsync(
            new Uri("/api/mcp/enabled", UriKind.Relative),
            new { enabled },
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));

        var holat = await javob.Content.ReadFromJsonAsync<McpStatusResponse>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        holat.ShouldNotBeNull();
        return holat;
    }

    /// <summary>Bazaga to'g'ridan-to'g'ri qarash — so'rovning o'z qamrovida, asosiy ulanish bilan.</summary>
    public async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> amal)
    {
        ArgumentNullException.ThrowIfNull(amal);

        await using var qamrov = Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        return await amal(kontekst).ConfigureAwait(false);
    }

    /// <summary><c>mcp_*</c> jadvallarini o'qish — MCP'ning o'z yozish konteksti bilan.</summary>
    public async Task<T> StoreAsync<T>(Func<AppDbContext, Task<T>> amal)
    {
        ArgumentNullException.ThrowIfNull(amal);

        await using var kontekst = Factory.Services.GetRequiredService<McpDatabase>().CreateStoreContext();
        return await amal(kontekst).ConfigureAwait(false);
    }

    /// <summary>Noyob belgi — testlar bir bazada ishlaydi.</summary>
    public static string Belgi() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..10];
}
