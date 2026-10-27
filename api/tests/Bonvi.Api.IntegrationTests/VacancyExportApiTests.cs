using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Vakansiyalar» ro'yxatining Excel eksporti — uchi-uchiga testlar
/// (<c>SPEC-excel-eksport.md</c> §2 #2).
/// </summary>
/// <remarks>
/// Har bir test o'z belgisi bilan vakansiyalar ochadi va natijani <c>search</c>
/// bilan shu belgiga toraytiradi — bazada boshqa testlarning vakansiyalari ham bor.
/// </remarks>
public sealed class VacancyExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Vakansiyalar";

    private readonly BonviApiFactory _factory;

    public VacancyExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(new Uri("/api/recruiting/vacancies/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Ikkala ruxsat ham shart: <c>recruiting.view</c> VA <c>reports.export</c>.</summary>
    [Theory]
    [InlineData("recruiting.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(new Uri("/api/recruiting/vacancies/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fayldagi qatorlar ekran ro'yxati bilan — soni va TARTIBI bo'yicha — aynan
    /// mos; «To'ldirildi» ikkita SON ustuni bo'lib yoziladi.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("recruiting.view", "reports.export");

        var sorov = "?search=" + urug.Belgi;

        var ekran = await client.GetFromJsonAsync<VacancyPageResponse>(
            new Uri("/api/recruiting/vacancies" + sorov, UriKind.Relative), Ct);
        ekran!.Items.Select(qator => qator.Id).ShouldBe([urug.Ochiq, urug.Yopiq], "sukut tartibi — ochilgan sana, yangidan eskiga");

        using var javob = await client.GetAsync(
            new Uri("/api/recruiting/vacancies/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_vakansiyalar_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        Sarlavhalar(varaq).ShouldBe(["Vakansiya nomi", "Holati", "To'ldirildi", "O'rinlar soni", "Nomzodlar", "Ochilgan"]);
        Ustun(varaq, 1).ShouldBe(ekran.Items.Select(qator => qator.Title), "fayl — ekrandagi qatorlar, o'sha tartibda");

        // 1-qator — ochiq, bitta nomzodli: sonlar haqiqiy Excel sonlari.
        varaq.Cell(2, 2).GetString().ShouldBe("Ochiq");
        varaq.Cell(2, 3).GetDouble().ShouldBe(0d);
        varaq.Cell(2, 4).GetDouble().ShouldBe(3d);
        varaq.Cell(2, 5).GetDouble().ShouldBe(1d);
        varaq.Cell(2, 6).GetDateTime().ShouldBe(new DateTime(2026, 9, 1));

        // 2-qator — yopilgan
        varaq.Cell(3, 2).GetString().ShouldBe("Yopilgan");
        varaq.Cell(3, 4).GetDouble().ShouldBe(1d);
        varaq.Cell(3, 6).GetDateTime().ShouldBe(new DateTime(2026, 8, 1));
    }

    [Fact]
    public async Task Holat_filtri_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("recruiting.view", "reports.export");

        using var javob = await client.GetAsync(
            new Uri($"/api/recruiting/vacancies/export?status=Closed&search={urug.Belgi}", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "faqat yopilgan vakansiya");
        varaq.Cell(2, 2).GetString().ShouldBe("Yopilgan");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "recruiting.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            new Uri($"/api/recruiting/vacancies/export?status=Open&search={urug.Belgi}", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("Vacancy");
        qator.Operation.ShouldBe("ExportVacanciesToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(1);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("status").GetString().ShouldBe("Open");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(string Belgi, Guid Ochiq, Guid Yopiq);

    /// <summary>
    /// Ikki vakansiya: ochiq (2026-09-01, 3 o'rin, 1 nomzod) va yopilgan
    /// (2026-08-01, 1 o'rin).
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Faqat harf: qidiruv matnidagi raqamlar boshqa ustunlarga tegmasin
        var belgi = "VE" + new string(Guid.NewGuid().ToString("N")[..8].Select(belgisi => (char)('A' + (belgisi % 16))).ToArray());

        var ochiq = Vacancy.Open("Эксвортчи " + belgi, new DateOnly(2026, 9, 1), openings: 3).Value;
        var yopiq = Vacancy.Open("Омборчи " + belgi, new DateOnly(2026, 8, 1), openings: 1).Value;
        yopiq.Close(new DateOnly(2026, 9, 20)).IsSuccess.ShouldBeTrue();

        var nomzod = Candidate.Register(FullName.Create("Вакансиев", "Анвар", belgi).Value, ochiq.Id).Value;

        kontekst.AddRange(ochiq, yopiq);
        kontekst.Add(nomzod);
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(belgi, ochiq.Id, yopiq.Id);
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
