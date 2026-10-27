using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Qayta aloqa» jurnalining Excel eksporti (<c>SPEC-excel-eksport.md</c> §2 #10) — uchi-uchiga.
/// </summary>
/// <remarks>
/// Har bir test o'z belgisi bilan ikki ketgan xodimni yaratadi va ro'yxatni <c>search</c> bilan
/// shularga toraytiradi (lotin belgisi — kirill «yig'ish» qoidasi unga tegmaydi).
/// </remarks>
public sealed class ExitInterviewExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Qayta aloqa";
    private const string Eksport = "/api/exit-interviews/export";

    private readonly BonviApiFactory _factory;

    public ExitInterviewExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(Yol(Eksport), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("feedback.view")]
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
        using var client = await MijozAsync("feedback.view", "reports.export");

        var sorov = $"?search={urug.Belgi}";

        var ekran = await IdlarAsync(client, "/api/exit-interviews" + sorov);
        ekran.ShouldBe([urug.KeyingiId, urug.OldingiId], "suhbat sanasi bo'yicha kamayish");

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_qayta_aloqa_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        string[] sarlavhalar =
            ["F.I.Sh.", "Bo'limi", "Lavozimi", "Ketgan sana", "Suhbat sanasi", "Tavsiya qilarmidi", "Ketish sababi"];
        Enumerable.Range(1, sarlavhalar.Length).Select(ustun => varaq.Cell(1, ustun).GetString()).ShouldBe(sarlavhalar);
        varaq.LastColumnUsed()!.ColumnNumber().ShouldBe(sarlavhalar.Length);
        varaq.LastRowUsed()!.RowNumber().ShouldBe(3);

        // 1-qator — keyingi suhbat (05.03): bo'lim va lavozim munosabatdan, tavsiya — «Ha»
        varaq.Cell(2, 1).GetString().ShouldBe(urug.KeyingiIsmi);
        varaq.Cell(2, 2).GetString().ShouldBe(urug.BolimNomi);
        varaq.Cell(2, 3).GetString().ShouldBe(urug.LavozimNomi);
        varaq.Cell(2, 4).GetDateTime().ShouldBe(new DateTime(2026, 2, 27));
        varaq.Cell(2, 5).GetDateTime().ShouldBe(new DateTime(2026, 3, 5));
        varaq.Cell(2, 6).GetString().ShouldBe("Ha");
        varaq.Cell(2, 7).GetString().ShouldBe("Maosh past");

        // 2-qator — bo'limsiz munosabat: ekrandagidek firma nomi, javobsiz tavsiya — bo'sh
        varaq.Cell(3, 1).GetString().ShouldBe(urug.OldingiIsmi);
        varaq.Cell(3, 2).GetString().ShouldBe(urug.FirmaNomi);
        varaq.Cell(3, 6).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task Sana_oraligi_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("feedback.view", "reports.export");

        var sorov = $"?search={urug.Belgi}&from=2026-03-02&to=2026-03-31";

        (await IdlarAsync(client, "/api/exit-interviews" + sorov)).ShouldBe([urug.KeyingiId]);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2);
        varaq.Cell(2, 1).GetString().ShouldBe(urug.KeyingiIsmi);
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "feedback.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(Yol($"{Eksport}?search={urug.Belgi}&from=2026-01-01"), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("ExitInterview");
        qator.Operation.ShouldBe("ExportExitInterviewsToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("from").GetString().ShouldBe("2026-01-01");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(
        string Belgi,
        string FirmaNomi,
        string BolimNomi,
        string LavozimNomi,
        Guid OldingiId,
        string OldingiIsmi,
        Guid KeyingiId,
        string KeyingiIsmi);

    /// <summary>
    /// Ikki ketgan xodim: «oldingi» — bo'limsiz, suhbat 01.03, tavsiyasiz; «keyingi» — bo'lim
    /// va lavozim bilan, 27.02 da ketgan, suhbat 05.03, tavsiya «Ha».
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        var belgi = "EI" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Кетиш МЧЖ " + belgi).Value;
        var bolim = Department.Create("Омбор " + belgi).Value;
        var lavozim = Position.Create("Юкчи " + belgi).Value;

        var oldingi = Employee.Create(FullName.Create("Кетганов", "Анвар", belgi).Value).Value;
        var keyingi = Employee.Create(FullName.Create("Кетганов", "Ботир", belgi).Value).Value;

        var m1 = Employment.Hire(oldingi.Id, firma.Id, new DateOnly(2025, 1, 10)).Value;
        m1.Terminate(new DateOnly(2026, 2, 20), reasonId: null).IsSuccess.ShouldBeTrue();

        var m2 = Employment.Hire(keyingi.Id, firma.Id, new DateOnly(2025, 3, 1), bolim.Id, lavozim.Id).Value;
        m2.Terminate(new DateOnly(2026, 2, 27), reasonId: null).IsSuccess.ShouldBeTrue();

        var s1 = ExitInterview.Record(m1.Id, new DateOnly(2026, 3, 1), "Uyga yaqin ish").Value;
        var s2 = ExitInterview.Record(m2.Id, new DateOnly(2026, 3, 5), "Maosh past", RecommendationAnswer.Yes).Value;

        kontekst.AddRange(firma, bolim, lavozim);
        kontekst.AddRange(oldingi, keyingi);
        kontekst.AddRange(m1, m2);
        kontekst.AddRange(s1, s2);
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(belgi, firma.Name, bolim.Name, lavozim.Name, s1.Id, oldingi.FullName.Full, s2.Id, keyingi.FullName.Full);
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
