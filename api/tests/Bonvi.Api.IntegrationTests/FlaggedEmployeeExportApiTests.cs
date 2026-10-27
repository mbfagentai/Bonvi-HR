using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Belgi ro'yxatlarining (rezerv, qora ro'yxat, nazorat, qobiliyatli) Excel
/// eksporti — uchi-uchiga testlar (<c>SPEC-excel-eksport.md</c> §2 #5).
/// </summary>
/// <remarks>
/// Har bir test o'z belgisi bilan xodimlar yaratadi (belgi otasining ismida) va
/// natijani <c>search</c> bilan shu belgiga toraytiradi.
/// </remarks>
public sealed class FlaggedEmployeeExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Varaq nomi belgi turidan
    private const string Varaq = "Qora ro'yxat";

    private readonly BonviApiFactory _factory;

    public FlaggedEmployeeExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/employees/flagged/export?type=Blacklist", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Ikkala ruxsat ham shart: <c>lists.view</c> VA <c>reports.export</c>.</summary>
    [Theory]
    [InlineData("lists.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(
            new Uri("/api/employees/flagged/export?type=Blacklist", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tarix bilan (<c>onlyActive=false</c>): fayldagi qatorlar ekran ro'yxati
    /// bilan — soni va TARTIBI bo'yicha — aynan mos; holat «Faol» / «Yopilgan».
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("lists.view", "reports.export");

        var sorov = $"?type=Blacklist&onlyActive=false&search={urug.Belgi}";

        var ekran = await client.GetFromJsonAsync<List<BelgiQator>>(
            new Uri("/api/employees/flagged" + sorov, UriKind.Relative), Ct);
        ekran.ShouldNotBeNull();
        ekran.Select(qator => qator.Id).ShouldBe([urug.Faol, urug.Yopilgan], "yangi belgi tepada");

        using var javob = await client.GetAsync(new Uri("/api/employees/flagged/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_qora_royxat_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        Sarlavhalar(varaq).ShouldBe(["F.I.Sh.", "Sabab", "Qo'yilgan", "Yopilgan", "Belgi", "Holati"]);
        Ustun(varaq, 1).ShouldBe(ekran.Select(qator => qator.EmployeeName), "fayl — ekrandagi qatorlar, o'sha tartibda");

        // 1-qator — kuchdagi belgi
        varaq.Cell(2, 2).GetString().ShouldBe("O'g'irlik");
        varaq.Cell(2, 3).GetDateTime().ShouldBe(new DateTime(2026, 9, 5));
        varaq.Cell(2, 4).IsEmpty().ShouldBeTrue();
        varaq.Cell(2, 5).GetString().ShouldBe("Qora ro'yxat");
        varaq.Cell(2, 6).GetString().ShouldBe("Faol");

        // 2-qator — yopilgan belgi
        varaq.Cell(3, 4).GetDateTime().ShouldBe(new DateTime(2026, 8, 31));
        varaq.Cell(3, 6).GetString().ShouldBe("Yopilgan");
    }

    /// <summary>Sukut — faqat kuchdagilar, ekrandagi kabi.</summary>
    [Fact]
    public async Task Sukut_boyicha_faqat_kuchdagi_belgilar_eksport_qilinadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("lists.view", "reports.export");

        using var javob = await client.GetAsync(
            new Uri($"/api/employees/flagged/export?type=Blacklist&search={urug.Belgi}", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "yopilgan belgi sukut ro'yxatda yo'q");
        varaq.Cell(2, 6).GetString().ShouldBe("Faol");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "lists.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            new Uri($"/api/employees/flagged/export?type=Blacklist&onlyActive=false&search={urug.Belgi}", UriKind.Relative),
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("EmployeeFlag");
        qator.Operation.ShouldBe("ExportFlaggedEmployeesToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2);
        var filtrlar = tafsilot.RootElement.GetProperty("filters");
        filtrlar.GetProperty("type").GetString().ShouldBe("Blacklist");
        filtrlar.GetProperty("onlyActive").GetBoolean().ShouldBeFalse();
        filtrlar.GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Ro'yxat qatorining testga kerakli qismi.</summary>
    private sealed record BelgiQator(Guid Id, string EmployeeName);

    private sealed record Urug(string Belgi, Guid Faol, Guid Yopilgan);

    /// <summary>
    /// Ikki xodim qora ro'yxatda: biri 10.08 dan 31.08 gacha (yopilgan), ikkinchisi
    /// 05.09 dan (kuchda).
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = "FE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var yopilganXodim = Employee.Create(FullName.Create("Белгиев", "Анвар", belgi).Value).Value;
        var yopilgan = EmployeeFlag.Raise(
            yopilganXodim.Id,
            EmployeeFlagType.Blacklist,
            new DateOnly(2026, 8, 10),
            reason: "Ishga kelmay qo'ydi",
            endDate: new DateOnly(2026, 8, 31)).Value;

        var faolXodim = Employee.Create(FullName.Create("Белгиев", "Ботир", belgi).Value).Value;
        var faol = EmployeeFlag.Raise(faolXodim.Id, EmployeeFlagType.Blacklist, new DateOnly(2026, 9, 5), reason: "O'g'irlik").Value;

        kontekst.AddRange(yopilganXodim, faolXodim);
        kontekst.AddRange(yopilgan, faol);
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(belgi, faol.Id, yopilgan.Id);
    }

    private static List<string> Sarlavhalar(IXLWorksheet varaq)
        => [.. Enumerable.Range(1, varaq.LastColumnUsed()!.ColumnNumber()).Select(ustun => varaq.Cell(1, ustun).GetString())];

    /// <summary>Ustunning ma'lumot qatorlari (sarlavhasiz).</summary>
    private static List<string> Ustun(IXLWorksheet varaq, int ustun)
        => [.. Enumerable.Range(2, varaq.LastRowUsed()!.RowNumber() - 1).Select(qator => varaq.Cell(qator, ustun).GetString())];

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
