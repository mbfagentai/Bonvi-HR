using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Safety;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Baxtsiz hodisalar» jurnalining Excel eksporti (<c>SPEC-excel-eksport.md</c> §2 #11) —
/// uchi-uchiga.
/// </summary>
/// <remarks>
/// Har bir test o'z belgisi bilan jabrlangan xodim yaratadi va ro'yxatni <c>search</c> bilan
/// shu xodimga toraytiradi.
/// </remarks>
public sealed class IncidentExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Baxtsiz hodisalar";
    private const string Eksport = "/api/safety/incidents/export";

    private readonly BonviApiFactory _factory;

    public IncidentExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(Yol(Eksport), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("safety.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(Yol(Eksport), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("safety.view", "reports.export");

        var sorov = $"?search={urug.Belgi}";

        var ekran = await IdlarAsync(client, "/api/safety/incidents" + sorov);
        ekran.ShouldBe([urug.OgirId, urug.YengilId], "hodisa sanasi bo'yicha kamayish");

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_baxtsiz_hodisalar_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        string[] sarlavhalar =
            ["F.I.Sh.", "Sana", "Jabrlanish darajasi", "Joyi", "Xulosa", "Tavsif", "Komissiya xulosasi", "Xulosa sanasi"];
        Enumerable.Range(1, sarlavhalar.Length).Select(ustun => varaq.Cell(1, ustun).GetString()).ShouldBe(sarlavhalar);
        varaq.LastColumnUsed()!.ColumnNumber().ShouldBe(sarlavhalar.Length);
        varaq.LastRowUsed()!.RowNumber().ShouldBe(3);

        // 1-qator — og'ir, yakunlangan (10.04)
        varaq.Cell(2, 1).GetString().ShouldBe(urug.XodimIsmi);
        varaq.Cell(2, 2).GetDateTime().ShouldBe(new DateTime(2026, 4, 10));
        varaq.Cell(2, 3).GetString().ShouldBe("Og'ir");
        varaq.Cell(2, 4).GetString().ShouldBe("Sex 2");
        varaq.Cell(2, 5).GetString().ShouldBe("Yakunlangan");
        varaq.Cell(2, 6).GetString().ShouldBe("Qo'l jarohati");
        varaq.Cell(2, 7).GetString().ShouldBe("Xavfsizlik qoidasi buzilgan");
        varaq.Cell(2, 8).GetDateTime().ShouldBe(new DateTime(2026, 4, 15));

        // 2-qator — yengil, xulosasiz (01.04)
        varaq.Cell(3, 3).GetString().ShouldBe("Yengil");
        varaq.Cell(3, 5).GetString().ShouldBe("Kutilmoqda");
        varaq.Cell(3, 7).IsEmpty().ShouldBeTrue();
        varaq.Cell(3, 8).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task Daraja_filtri_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("safety.view", "reports.export");

        var sorov = $"?search={urug.Belgi}&severity=Minor";

        (await IdlarAsync(client, "/api/safety/incidents" + sorov)).ShouldBe([urug.YengilId]);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2);
        varaq.Cell(2, 3).GetString().ShouldBe("Yengil");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "safety.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(Yol($"{Eksport}?search={urug.Belgi}&severity=Severe"), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("Incident");
        qator.Operation.ShouldBe("ExportIncidentsToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(1);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("severity").GetString().ShouldBe("Severe");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(string Belgi, string XodimIsmi, Guid YengilId, Guid OgirId);

    /// <summary>Bitta xodim, ikki hodisa: 01.04 — yengil, xulosasiz; 10.04 — og'ir, 15.04 da xulosa yozilgan.</summary>
    private async Task<Urug> UrugAsync()
    {
        var belgi = "IN" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create("Ҳодисов", "Анвар", belgi).Value).Value;

        var yengil = Incident.Record(xodim.Id, new DateOnly(2026, 4, 1), "Ombor", "Sirpanib yiqildi", IncidentSeverity.Minor).Value;
        var ogir = Incident.Record(xodim.Id, new DateOnly(2026, 4, 10), "Sex 2", "Qo'l jarohati", IncidentSeverity.Severe).Value;
        ogir.Conclude("Xavfsizlik qoidasi buzilgan", new DateOnly(2026, 4, 15)).IsSuccess.ShouldBeTrue();

        kontekst.Add(xodim);
        kontekst.AddRange(yengil, ogir);
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(belgi, xodim.FullName.Full, yengil.Id, ogir.Id);
    }

    private static async Task<List<Guid>> IdlarAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(Yol(manzil), Ct);

        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return [.. hujjat.RootElement.EnumerateArray().Select(qator => qator.GetProperty("id").GetGuid())];
    }

    private static Uri Yol(string manzil) => new(manzil, UriKind.Relative);

    private static async Task<XLWorkbook> KitobAsync(HttpResponseMessage javob)
        => new(new MemoryStream(await javob.Content.ReadAsByteArrayAsync(Ct)));

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));
        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(Ct);
}
