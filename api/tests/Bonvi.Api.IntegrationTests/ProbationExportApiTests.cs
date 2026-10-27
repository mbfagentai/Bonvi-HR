using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Sinov muddati» ro'yxatining Excel eksporti — uchi-uchiga testlar
/// (<c>SPEC-excel-eksport.md</c> §2 #3).
/// </summary>
/// <remarks>
/// Har bir test o'z belgisi bilan xodimlar yaratadi (belgi otasining ismida) va
/// natijani <c>search</c> bilan shu belgiga toraytiradi.
/// </remarks>
public sealed class ProbationExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Sinov muddati";

    private readonly BonviApiFactory _factory;

    public ProbationExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(new Uri("/api/probations/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Ikkala ruxsat ham shart: <c>employment.view</c> VA <c>reports.export</c>.</summary>
    [Theory]
    [InlineData("employment.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(new Uri("/api/probations/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fayldagi qatorlar ekran ro'yxati bilan — soni va TARTIBI bo'yicha — aynan
    /// mos; «Qolgan kun» — ekrandagi son, faqat davom etayotgan sinovda.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("employment.view", "reports.export");

        var sorov = "?search=" + urug.Belgi;

        var ekran = await client.GetFromJsonAsync<SinovSahifa>(new Uri("/api/probations" + sorov, UriKind.Relative), Ct);
        ekran!.Items.Select(qator => qator.Id).ShouldBe([urug.Otgan, urug.Davomda], "tugash sanasi bo'yicha");

        using var javob = await client.GetAsync(new Uri("/api/probations/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_sinov_muddati_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        Sarlavhalar(varaq).ShouldBe(
            ["F.I.Sh.", "Lavozimi", "Bo'limi", "Boshlangan", "Tugaydi", "Qolgan kun", "Holati", "Xulosa"]);
        Ustun(varaq, 1).ShouldBe(ekran.Items.Select(qator => qator.EmployeeName), "fayl — ekrandagi qatorlar, o'sha tartibda");

        // 1-qator — yakunlangan sinov: qolgan kun yo'q, xulosa bor.
        varaq.Cell(2, 2).GetString().ShouldBe(urug.Lavozim);
        varaq.Cell(2, 3).GetString().ShouldBe(urug.Bolim);
        varaq.Cell(2, 4).GetDateTime().ShouldBe(new DateTime(2026, 5, 1));
        varaq.Cell(2, 5).GetDateTime().ShouldBe(new DateTime(2026, 8, 1));
        varaq.Cell(2, 6).IsEmpty().ShouldBeTrue("yakunlangan sinovda qolgan kun ma'nosiz");
        varaq.Cell(2, 7).GetString().ShouldBe("O'tdi");
        varaq.Cell(2, 8).GetString().ShouldBe("Yaxshi ishladi");

        // 2-qator — davom etayotgan: qolgan kun ekrandagi bilan bir xil SON.
        varaq.Cell(3, 6).GetDouble().ShouldBe(ekran.Items[1].DaysLeft);
        varaq.Cell(3, 7).GetString().ShouldBe("Davom etmoqda");
        varaq.Cell(3, 8).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task Holat_filtri_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("employment.view", "reports.export");

        using var javob = await client.GetAsync(
            new Uri($"/api/probations/export?status=InProgress&search={urug.Belgi}", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "faqat davom etayotgan sinov");
        varaq.Cell(2, 7).GetString().ShouldBe("Davom etmoqda");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "employment.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            new Uri($"/api/probations/export?status=Passed&search={urug.Belgi}", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("Probation");
        qator.Operation.ShouldBe("ExportProbationsToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(1);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("status").GetString().ShouldBe("Passed");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Ro'yxat qatorining testga kerakli qismi.</summary>
    private sealed record SinovQator(Guid Id, string EmployeeName, int DaysLeft);

    private sealed record SinovSahifa(IReadOnlyList<SinovQator> Items, int Total);

    private sealed record Urug(string Belgi, string Lavozim, string Bolim, Guid Otgan, Guid Davomda);

    /// <summary>
    /// Bitta bo'lim va lavozimda ikki xodim: biri sinovdan o'tgan (01.05–01.08,
    /// xulosa bilan), ikkinchisi sinovda (03.08–05.11).
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = "PE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var firma = LegalEntity.Create("Синов Экспорт МЧЖ " + belgi).Value;
        var bolim = Department.Create("Сотув " + belgi).Value;
        var lavozim = Position.Create("Сотувчи " + belgi).Value;
        kontekst.AddRange(firma, bolim, lavozim);

        var otganXodim = Employee.Create(FullName.Create("Синовов", "Анвар", belgi).Value).Value;
        var otganMunosabat = Employment.Hire(otganXodim.Id, firma.Id, new DateOnly(2026, 5, 1), bolim.Id, lavozim.Id).Value;
        var otgan = Probation.Import(otganMunosabat.Id, new DateOnly(2026, 5, 1), new DateOnly(2026, 8, 1)).Value;
        otgan.Resolve(ProbationStatus.Passed, new DateOnly(2026, 7, 30), "Yaxshi ishladi").IsSuccess.ShouldBeTrue();

        var davomdaXodim = Employee.Create(FullName.Create("Синовов", "Ботир", belgi).Value).Value;
        var davomdaMunosabat = Employment.Hire(davomdaXodim.Id, firma.Id, new DateOnly(2026, 8, 3), bolim.Id, lavozim.Id).Value;
        var davomda = Probation.Import(davomdaMunosabat.Id, new DateOnly(2026, 8, 3), new DateOnly(2026, 11, 5)).Value;

        kontekst.AddRange(otganXodim, otganMunosabat, otgan, davomdaXodim, davomdaMunosabat, davomda);
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(belgi, lavozim.Name, bolim.Name, otgan.Id, davomda.Id);
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
