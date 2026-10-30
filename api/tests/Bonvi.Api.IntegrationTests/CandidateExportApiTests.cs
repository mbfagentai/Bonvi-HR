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
/// «Nomzodlar» ro'yxatining Excel eksporti — uchi-uchiga testlar
/// (<c>SPEC-excel-eksport.md</c> §2 #1).
/// </summary>
/// <remarks>
/// <see cref="IncentiveExportApiTests"/> dagi talab: fayl EKRANDAGI
/// (<c>GET /api/recruiting/candidates</c>) bilan bir xil filtr va tartibdagi
/// yozuvlarni beradi. Har bir test o'z vakansiyasini ochadi va natijani
/// <c>vacancyId</c> bilan shu vakansiyaga toraytiradi — bazada boshqa
/// testlarning nomzodlari ham bor.
/// </remarks>
public sealed class CandidateExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Nomzodlar";

    private readonly BonviApiFactory _factory;

    public CandidateExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(new Uri("/api/recruiting/candidates/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Ikkala ruxsat ham shart: <c>recruiting.view</c> VA <c>reports.export</c>.</summary>
    [Theory]
    [InlineData("recruiting.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(new Uri("/api/recruiting/candidates/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fayldagi qatorlar xuddi shu filtr bilan chaqirilgan ekran ro'yxati bilan —
    /// soni va TARTIBI bo'yicha — aynan mos; sukut ro'yxatda yashirilgan rad
    /// etilgan nomzod faylda ham yo'q.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("recruiting.view", "reports.export");

        var sorov = $"?vacancyId={urug.VakansiyaId}";

        var ekran = await client.GetFromJsonAsync<NomzodSahifa>(
            new Uri("/api/recruiting/candidates" + sorov, UriKind.Relative), Ct);
        ekran!.Items.Select(qator => qator.Id).ShouldBe([urug.Yangi, urug.Eski], "sukut tartibi — yangidan eskiga");

        using var javob = await client.GetAsync(
            new Uri("/api/recruiting/candidates/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_nomzodlar_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        Sarlavhalar(varaq).ShouldBe(
        [
            "F.I.Sh.", "Vakansiya", "Bosqich", "Holati", "Manba", "Telefon raqami", "Kiritilgan",
            "Keyingi aloqa", "Yopilgan sana", "Sabab", "Qayerdan bilgan", "Anketa to'ldirgan",
        ]);

        Ustun(varaq, 1).ShouldBe(ekran.Items.Select(qator => qator.FullName), "fayl — ekrandagi qatorlar, o'sha tartibda");

        // 1-qator — yangi nomzod: telefon ekrandagidek formatlangan, vaqt TOSHKENTDA.
        varaq.Cell(2, 2).GetString().ShouldBe(urug.VakansiyaNomi);
        varaq.Cell(2, 4).GetString().ShouldBe("Jarayonda");
        varaq.Cell(2, 5).GetString().ShouldBe("Qo'lda");
        varaq.Cell(2, 6).GetString().ShouldBe("+998 90 111 22 33");
        varaq.Cell(2, 7).GetDateTime().ShouldBe(new DateTime(2026, 10, 10, 10, 0, 0), "05:00 UTC = 10:00 Toshkent");
        varaq.Cell(2, 9).IsEmpty().ShouldBeTrue("jarayondagi nomzod yopilmagan");
        varaq.Cell(2, 12).IsEmpty().ShouldBeTrue("kartaga keyin onlayn anketa qo'shilmagan");

        // 2-qator — eski nomzod: telefonsiz, UTC kuni Toshkentda ertasiga o'tadi.
        varaq.Cell(3, 6).IsEmpty().ShouldBeTrue();
        varaq.Cell(3, 7).GetDateTime().ShouldBe(new DateTime(2026, 10, 10, 0, 30, 0), "19:30 UTC = ertasi 00:30 Toshkent");
    }

    /// <summary>
    /// «Rad etilganlar» bo'limi: yopilgan sana va sabab — shu bo'limning
    /// ma'lumoti, faylda ham bor.
    /// </summary>
    [Fact]
    public async Task Rad_etilganlar_bolimi_sana_va_sabab_bilan_eksport_qilinadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("recruiting.view", "reports.export");

        var sorov = $"?vacancyId={urug.VakansiyaId}&rejected=true";

        var ekran = await client.GetFromJsonAsync<NomzodSahifa>(
            new Uri("/api/recruiting/candidates" + sorov, UriKind.Relative), Ct);
        ekran!.Items.Select(qator => qator.Id).ShouldBe([urug.RadEtilgan]);

        using var javob = await client.GetAsync(
            new Uri("/api/recruiting/candidates/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        Ustun(varaq, 1).ShouldBe(ekran.Items.Select(qator => qator.FullName));
        varaq.Cell(2, 4).GetString().ShouldBe("Rad etilgan");
        varaq.Cell(2, 9).GetDateTime().ShouldBe(new DateTime(2026, 9, 15));
        varaq.Cell(2, 10).GetString().ShouldBe("Тажрибаси етарли эмас");
    }

    [Fact]
    public async Task Notogri_holat_filtri_400_qaytaradi_fayl_emas()
    {
        using var client = await MijozAsync("recruiting.view", "reports.export");

        using var javob = await client.GetAsync(
            new Uri("/api/recruiting/candidates/export?status=Paused", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("nomzod.holat_notogri");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "recruiting.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            new Uri(
                $"/api/recruiting/candidates/export?vacancyId={urug.VakansiyaId}&search={urug.Belgi}",
                UriKind.Relative),
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("Candidate");
        qator.Operation.ShouldBe("ExportCandidatesToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2);
        var filtrlar = tafsilot.RootElement.GetProperty("filters");
        filtrlar.GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        filtrlar.GetProperty("vacancyId").GetGuid().ShouldBe(urug.VakansiyaId);
        filtrlar.TryGetProperty("rejected", out _).ShouldBeFalse("o'chiq bayroq yozilmaydi");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Ro'yxat qatorining testga kerakli qismi.</summary>
    private sealed record NomzodQator(Guid Id, string FullName);

    private sealed record NomzodSahifa(IReadOnlyList<NomzodQator> Items, int Total);

    private sealed record Urug(
        Guid VakansiyaId,
        string VakansiyaNomi,
        string Belgi,
        Guid Yangi,
        Guid Eski,
        Guid RadEtilgan);

    /// <summary>
    /// O'z vakansiyasi: ikki jarayondagi nomzod (yangisi telefon bilan) va bitta
    /// rad etilgan (15-sentyabr, sabab bilan).
    /// </summary>
    /// <remarks>
    /// <c>created_at</c> ni interseptor saqlashda «hozir» qilib qo'yadi — tartib
    /// aniq bo'lishi uchun u saqlangandan keyin <c>UPDATE</c> bilan o'rnatiladi
    /// (<c>ListDateRangeApiTests</c> dagi usul).
    /// </remarks>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        // Belgi otasining ismida — qidiruv shu bilan toraytiriladi (`ListDateRangeApiTests` usuli)
        var belgi = "NE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var vakansiya = Vacancy.Open("Эксвортчи " + belgi, new DateOnly(2026, 8, 1), openings: 3).Value;

        var yangi = Candidate.Register(
            FullName.Create("Эксвортов", "Анвар", belgi).Value,
            vakansiya.Id,
            phone: PhoneNumber.Create("901112233").Value).Value;
        var eski = Candidate.Register(FullName.Create("Эксвортов", "Ботир", belgi).Value, vakansiya.Id).Value;

        var rad = Candidate.Register(FullName.Create("Эксвортов", "Дилшод", belgi).Value, vakansiya.Id).Value;
        rad.Reject(new DateOnly(2026, 9, 15), "Тажрибаси етарли эмас").IsSuccess.ShouldBeTrue();

        kontekst.Add(vakansiya);
        kontekst.AddRange(yangi, eski, rad);
        await kontekst.SaveChangesAsync(Ct);

        await YaratilganAsync(kontekst, yangi.Id, new DateTimeOffset(2026, 10, 10, 5, 0, 0, TimeSpan.Zero));
        await YaratilganAsync(kontekst, eski.Id, new DateTimeOffset(2026, 10, 9, 19, 30, 0, TimeSpan.Zero));

        return new Urug(vakansiya.Id, vakansiya.Title, belgi, yangi.Id, eski.Id, rad.Id);
    }

    private static Task<int> YaratilganAsync(AppDbContext kontekst, Guid nomzodId, DateTimeOffset lahza)
        => kontekst.Candidates
            .Where(nomzod => nomzod.Id == nomzodId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(nomzod => nomzod.CreatedAt, lahza), Ct);

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
