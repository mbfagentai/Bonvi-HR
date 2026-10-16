using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Catalog;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Rag'bat va jarima» ro'yxatining Excel eksporti — uchi-uchiga testlar.
/// </summary>
/// <remarks>
/// <see cref="EmployeeExportApiTests"/> dagi talab: eksport EKRANDAGI
/// (<c>GET /api/incentives</c>) bilan bir xil filtr va tartibdagi yozuvlarni
/// beradi. Har bir test o'z belgisi bilan xodim yaratadi va natijani
/// <c>search</c> bilan shu xodimga toraytiradi — bazada boshqa testlarning
/// yozuvlari ham bor.
/// </remarks>
public sealed class IncentiveExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Rag'bat va jarima";

    private readonly BonviApiFactory _factory;

    public IncentiveExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(new Uri("/api/incentives/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Ikkala ruxsat ham shart: ko'rish (<c>incentives.view</c>) VA fayl olib
    /// chiqish (<c>reports.export</c>). Bittasi yetmaydi.
    /// </summary>
    [Theory]
    [InlineData("incentives.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(new Uri("/api/incentives/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Eng muhim test: fayldagi qatorlar xuddi shu filtr bilan chaqirilgan
    /// ekran ro'yxati bilan — soni va TARTIBI bo'yicha — aynan mos; har bir
    /// ustun to'g'ri turda yozilgan.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var (xodim, ragbatId, jarimaId) = await UrugAsync();
        using var client = await MijozAsync("incentives.view", "reports.export");

        var sorov = "?search=" + Uri.EscapeDataString(xodim.FullName);

        var ekran = await client.GetFromJsonAsync<IncentiveListPageResponse>(
            new Uri("/api/incentives" + sorov, UriKind.Relative), Ct);
        ekran!.Items.Select(q => q.Id).ShouldBe([jarimaId, ragbatId], "sukut tartibi — yangidan eskiga");

        using var javob = await client.GetAsync(new Uri("/api/incentives/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_ragbat_jarima_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.Cell(1, 1).GetString().ShouldBe("Sana");
        varaq.LastRowUsed()!.RowNumber().ShouldBe(3, "sarlavha + ikki yozuv");

        // 1-qator — jarima (yangisi): foiz, sabab, buyruqsiz.
        varaq.Cell(2, 1).GetDateTime().ShouldBe(new DateTime(2026, 9, 10));
        varaq.Cell(2, 2).GetString().ShouldBe(xodim.FullName);
        varaq.Cell(2, 3).GetString().ShouldBe("Jarima");
        varaq.Cell(2, 5).IsEmpty().ShouldBeTrue("foizli jarimada summa yo'q");
        varaq.Cell(2, 7).GetDouble().ShouldBe(0.025, 1e-9, "foiz — haqiqiy Excel foizi (2,5 % = 0,025)");
        varaq.Cell(2, 7).Style.NumberFormat.Format.ShouldBe("0.00%");
        varaq.Cell(2, 8).GetString().ShouldBe("Kechikish");
        varaq.Cell(2, 10).IsEmpty().ShouldBeTrue("jarimada buyruq yo'q");

        // 2-qator — rag'bat: summa son, valyuta alohida, buyruq.
        varaq.Cell(3, 3).GetString().ShouldBe("Rag'bat");
        varaq.Cell(3, 5).GetDouble().ShouldBe(1_500_000d);
        varaq.Cell(3, 6).GetString().ShouldBe("UZS");
        varaq.Cell(3, 7).IsEmpty().ShouldBeTrue();
        varaq.Cell(3, 10).GetString().ShouldBe("45-b");
        varaq.Cell(3, 11).GetDateTime().ShouldBe(new DateTime(2026, 9, 2));
    }

    [Fact]
    public async Task Tur_filtri_eksportga_ham_qollanadi()
    {
        var (xodim, _, jarimaId) = await UrugAsync();
        using var client = await MijozAsync("incentives.view", "reports.export");

        using var javob = await client.GetAsync(
            new Uri($"/api/incentives/export?kind=Penalty&search={Uri.EscapeDataString(xodim.FullName)}", UriKind.Relative),
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "faqat jarima");
        varaq.Cell(2, 3).GetString().ShouldBe("Jarima");
        jarimaId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var (xodim, _, _) = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "incentives.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            new Uri($"/api/incentives/export?kind=Award&search={Uri.EscapeDataString(xodim.FullName)}", UriKind.Relative),
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("Incentive");
        qator.Operation.ShouldBe("ExportIncentivesToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(xodim.FullName.Split(' ')[0]);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(1);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("kind").GetString().ShouldBe("Award");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bitta xodim: 02.09 — pul mukofoti 1 500 000 (buyruq 45-b),
    /// 10.09 — jarima 2,5 % (sabab bilan). HTTP orqali: domen qoidalari ishlaydi.
    /// </summary>
    private async Task<(EmployeeResponse Xodim, Guid RagbatId, Guid JarimaId)> UrugAsync()
    {
        using var admin = await AdminMijozAsync();

        using var yaratish = await admin.PostAsJsonAsync(
            new Uri("/api/employees", UriKind.Relative),
            new { lastName = "Эксвортов " + Guid.NewGuid().ToString("N")[..6], firstName = "Ходим" },
            Ct);
        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));
        var xodim = (await yaratish.Content.ReadFromJsonAsync<EmployeeResponse>(Ct))!;

        var (ragbatTuri, jarimaTuri) = await TurlarAsync();

        using var ragbat = await admin.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new
            {
                kind = "Award",
                typeId = ragbatTuri,
                date = new DateOnly(2026, 9, 2),
                amount = 1_500_000m,
                orderNo = "45-b",
                orderDate = new DateOnly(2026, 9, 2),
            },
            Ct);
        ragbat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ragbat));

        using var jarima = await admin.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new { kind = "Penalty", typeId = jarimaTuri, date = new DateOnly(2026, 9, 10), reason = "Kechikish", percent = 2.5m },
            Ct);
        jarima.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jarima));

        return (
            xodim,
            (await ragbat.Content.ReadFromJsonAsync<IncentiveResponse>(Ct))!.Id,
            (await jarima.Content.ReadFromJsonAsync<IncentiveResponse>(Ct))!.Id);
    }

    /// <summary>Pulli rag'bat va pulli jarima turi — to'g'ridan-to'g'ri bazaga.</summary>
    private async Task<(Guid Ragbat, Guid Jarima)> TurlarAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        var ragbat = AwardType.Create("Пул мукофоти " + belgi).Value;
        ragbat.SetHasAmount(true);
        var jarima = PenaltyType.Create("Жарима " + belgi).Value;
        jarima.SetHasAmount(true);

        kontekst.AwardTypes.Add(ragbat);
        kontekst.PenaltyTypes.Add(jarima);
        await kontekst.SaveChangesAsync(Ct);

        return (ragbat.Id, jarima.Id);
    }

    private static async Task<XLWorkbook> KitobAsync(HttpResponseMessage javob)
        => new(new MemoryStream(await javob.Content.ReadAsByteArrayAsync(Ct)));

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));
        return client;
    }

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            Ct);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(Ct);
}
