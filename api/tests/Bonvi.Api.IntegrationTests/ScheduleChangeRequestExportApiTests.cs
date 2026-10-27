using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «So'rovlar → Jadval o'zgarishi» ro'yxatining Excel eksporti (<c>SPEC-excel-eksport.md</c> §2 #7)
/// — uchi-uchiga.
/// </summary>
/// <remarks>
/// Bitta qator — bitta so'rov (ekrandagidek), kunlari esa katak ichida qatorma-qator. Har bir
/// test o'z xodimini yaratadi va ro'yxatni <c>employeeId</c> bilan toraytiradi.
/// </remarks>
public sealed class ScheduleChangeRequestExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Jadval so'rovlari";
    private const string Eksport = "/api/attendance/schedule-requests/export";

    private readonly BonviApiFactory _factory;

    public ScheduleChangeRequestExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(Yol(Eksport), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("attendance.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(Yol(Eksport), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// «/schedule-requests/export» «/schedule-requests/{id:guid}» ga tushib qolmaydi — 404
    /// «so'rov topilmadi» emas, fayl.
    /// </summary>
    [Fact]
    public async Task Eksport_manzili_bitta_sorov_marshrutiga_tushmaydi()
    {
        using var client = await MijozAsync("attendance.view", "reports.export");

        using var javob = await client.GetAsync(Yol(Eksport + $"?employeeId={Guid.NewGuid()}"), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_kunlari_qatorma_qator()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("attendance.view", "reports.export");

        var sorov = $"?employeeId={urug.XodimId}";

        var ekran = await IdlarAsync(client, "/api/attendance/schedule-requests" + sorov);
        ekran.Count.ShouldBe(2);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_jadval_sorovlari_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.Cell(1, 4).GetString().ShouldBe("Sanalar");
        varaq.Cell(1, 5).GetString().ShouldBe("Ish vaqti");
        varaq.Cell(1, 6).GetString().ShouldBe("Tanaffus");
        varaq.LastColumnUsed()!.ColumnNumber().ShouldBe(12, "oxirgisi «Ilovadan»");
        varaq.LastRowUsed()!.RowNumber().ShouldBe(ekran.Count + 1);

        // Tartib — ekrandagining o'zi: har qatorning mazmuni o'sha o'rindagi so'rovniki
        ekran.ShouldBe([urug.OzgartirishId, urug.AlmashtirishId], ignoreOrder: true);
        var ozgartirish = ekran.IndexOf(urug.OzgartirishId) + 2;
        var almashtirish = ekran.IndexOf(urug.AlmashtirishId) + 2;

        varaq.Cell(ozgartirish, 1).GetString().ShouldBe(urug.XodimIsmi);
        varaq.Cell(ozgartirish, 3).GetString().ShouldBe("Kunni o'zgartirish");
        varaq.Cell(ozgartirish, 4).GetString().ShouldBe("15.10.2026\n16.10.2026\n17.10.2026");
        varaq.Cell(ozgartirish, 5).GetString().ShouldBe("08:30–18:00\nDam olish kuni\n22:00–06:00 (ertasi kuni)");
        varaq.Cell(ozgartirish, 6).GetString().ShouldBe("12:00–13:00\n—\n—");
        varaq.Cell(ozgartirish, 7).GetString().ShouldBe("Tanaffusni surish");
        varaq.Cell(ozgartirish, 8).GetString().ShouldBe("Kutilmoqda");

        varaq.Cell(almashtirish, 3).GetString().ShouldBe("Kun almashtirish");
        varaq.Cell(almashtirish, 4).GetString().ShouldBe("05.10.2026 ⇄ 25.10.2026");
        varaq.Cell(almashtirish, 5).IsEmpty().ShouldBeTrue("almashtirishda vaqt — sanalarning o'z rejasi");
        varaq.Cell(almashtirish, 8).GetString().ShouldBe("Rad etilgan");
        varaq.Cell(almashtirish, 9).GetString().ShouldBe("Grafik bo'yicha emas");
        varaq.Cell(almashtirish, 10).IsEmpty().ShouldBeFalse("qaror qilgan HR ismi");
        varaq.Cell(almashtirish, 11).DataType.ShouldBe(XLDataType.DateTime);
    }

    [Fact]
    public async Task Holat_filtri_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("attendance.view", "reports.export");

        var sorov = $"?employeeId={urug.XodimId}&status=Pending";

        (await IdlarAsync(client, "/api/attendance/schedule-requests" + sorov)).ShouldBe([urug.OzgartirishId]);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2);
        varaq.Cell(2, 3).GetString().ShouldBe("Kunni o'zgartirish");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "attendance.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            Yol($"{Eksport}?employeeId={urug.XodimId}&search={Uri.EscapeDataString(urug.XodimIsmi)}&from=2026-10-01"),
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("ScheduleChangeRequest");
        qator.Operation.ShouldBe("ExportScheduleChangeRequestsToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("from").GetString().ShouldBe("2026-10-01");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(Guid XodimId, string XodimIsmi, string Belgi, Guid OzgartirishId, Guid AlmashtirishId);

    /// <summary>
    /// Bitta xodim, ikkita so'rov: «Kunni o'zgartirish» uch kunga (ish kuni tanaffus bilan,
    /// dam olish kuni, tungi smena) — kutmoqda; «Kun almashtirish» 05 ↔ 25.10 — rad etilgan.
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        var belgi = "SR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var qarorchi = await TestAccounts.CreateAsync(_factory, "attendance.requests.manage");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create("Жадвалов", "Ботир", belgi).Value).Value;

        var ozgartirish = ScheduleChangeRequest.Create(
            xodim.Id,
            ScheduleChangeKind.Change,
            [
                new ScheduleChangeDayInput(
                    new DateOnly(2026, 10, 15), IsWorking: true,
                    new TimeOnly(8, 30), new TimeOnly(18, 0), new TimeOnly(12, 0), new TimeOnly(13, 0)),
                new ScheduleChangeDayInput(new DateOnly(2026, 10, 16), IsWorking: false),
                new ScheduleChangeDayInput(
                    new DateOnly(2026, 10, 17), IsWorking: true, new TimeOnly(22, 0), new TimeOnly(6, 0)),
            ],
            "Tanaffusni surish").Value;

        var almashtirish = ScheduleChangeRequest.Create(
            xodim.Id,
            ScheduleChangeKind.Swap,
            [
                new ScheduleChangeDayInput(new DateOnly(2026, 10, 5), IsWorking: false, SwapWith: new DateOnly(2026, 10, 25)),
                new ScheduleChangeDayInput(new DateOnly(2026, 10, 25), IsWorking: false, SwapWith: new DateOnly(2026, 10, 5)),
            ]).Value;

        kontekst.Add(xodim);
        kontekst.AddRange(ozgartirish, almashtirish);
        await kontekst.SaveChangesAsync(Ct);

        almashtirish.Reject(qarorchi.UserId, DateTimeOffset.UtcNow, "Grafik bo'yicha emas").IsSuccess.ShouldBeTrue();
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(xodim.Id, xodim.FullName.Full, belgi, ozgartirish.Id, almashtirish.Id);
    }

    private static async Task<List<Guid>> IdlarAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(Yol(manzil), Ct);

        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return [.. hujjat.RootElement.GetProperty("items").EnumerateArray().Select(qator => qator.GetProperty("id").GetGuid())];
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
