using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «So'rovlar → Yo'qlik» ro'yxatining Excel eksporti (<c>SPEC-excel-eksport.md</c> §2 #6) —
/// uchi-uchiga.
/// </summary>
/// <remarks>
/// <see cref="IncentiveExportApiTests"/> naqshi: fayldagi qatorlar xuddi shu filtr bilan
/// chaqirilgan EKRAN ro'yxati (<c>GET /api/attendance/requests</c>) bilan — soni va tartibi
/// bo'yicha — aynan mos. Har bir test o'z xodimini yaratadi va ro'yxatni <c>employeeId</c>
/// bilan shu xodimga toraytiradi — bazada boshqa testlarning so'rovlari ham bor.
/// </remarks>
public sealed class AbsenceRequestExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Yo'qlik so'rovlari";
    private const string Eksport = "/api/attendance/requests/export";

    private readonly BonviApiFactory _factory;

    public AbsenceRequestExportApiTests(BonviApiFactory factory) => _factory = factory;

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

    /// <summary>Ikkala ruxsat ham shart: ko'rish (<c>attendance.view</c>) VA fayl olib chiqish.</summary>
    [Theory]
    [InlineData("attendance.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(Yol(Eksport), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fayldagi qatorlar ekran ro'yxati bilan soni va TARTIBI bo'yicha mos; davr uch ustunda
    /// (haqiqiy sanalar va vaqt yoki «Butun kun»), qaror — alohida ustunlarda.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("attendance.view", "reports.export");

        var sorov = $"?employeeId={urug.XodimId}";

        var ekran = await RoyxatAsync(client, "/api/attendance/requests" + sorov);
        ekran.Count.ShouldBe(3);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_yoqlik_sorovlari_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.Cell(1, 1).GetString().ShouldBe("Xodim");
        varaq.Cell(1, 6).GetString().ShouldBe("Vaqt");
        varaq.LastColumnUsed()!.ColumnNumber().ShouldBe(12, "oxirgisi «Ilovadan»");
        varaq.Cell(1, 12).GetString().ShouldBe("Ilovadan");
        varaq.LastRowUsed()!.RowNumber().ShouldBe(ekran.Count + 1, "sarlavha + ekrandagi har bir so'rov");

        // Tartib — ekrandagining o'zi (yangisi birinchi): har qatorning boshlanish sanasi
        // o'sha o'rindagi ekran yozuviniki bilan teng.
        for (var i = 0; i < ekran.Count; i++)
        {
            var qator = i + 2;
            varaq.Cell(qator, 1).GetString().ShouldBe(ekran[i].GetProperty("employeeName").GetString());
            DateOnly.FromDateTime(varaq.Cell(qator, 4).GetDateTime())
                .ShouldBe(DateOnly.Parse(ekran[i].GetProperty("fromDate").GetString()!, CultureInfo.InvariantCulture));
        }

        var vaqtli = QatorRaqami(ekran, urug.VaqtliId);
        varaq.Cell(vaqtli, 3).GetString().ShouldBe(urug.TurNomi);
        varaq.Cell(vaqtli, 5).GetDateTime().ShouldBe(new DateTime(2026, 10, 5));
        varaq.Cell(vaqtli, 6).GetString().ShouldBe("08:30 – 11:57");
        varaq.Cell(vaqtli, 7).GetString().ShouldBe("Shifokorga");
        varaq.Cell(vaqtli, 8).GetString().ShouldBe("Kutilmoqda");
        varaq.Cell(vaqtli, 10).IsEmpty().ShouldBeTrue("qaror hali yo'q");
        varaq.Cell(vaqtli, 12).GetString().ShouldBe("Yo'q", "HR kiritgan — ilovadan emas");

        var kopKunlik = QatorRaqami(ekran, urug.KopKunlikId);
        varaq.Cell(kopKunlik, 4).GetDateTime().ShouldBe(new DateTime(2026, 10, 10));
        varaq.Cell(kopKunlik, 5).GetDateTime().ShouldBe(new DateTime(2026, 10, 12));
        varaq.Cell(kopKunlik, 6).GetString().ShouldBe("Butun kun");

        var tasdiqlangan = QatorRaqami(ekran, urug.TasdiqlanganId);
        var ekrandagi = ekran.Single(qator => qator.GetProperty("id").GetGuid() == urug.TasdiqlanganId);
        varaq.Cell(tasdiqlangan, 8).GetString().ShouldBe("Tasdiqlangan");
        varaq.Cell(tasdiqlangan, 9).GetString().ShouldBe("Hujjat ko'rildi");
        varaq.Cell(tasdiqlangan, 10).GetString().ShouldBe(ekrandagi.GetProperty("decidedByName").GetString());
        varaq.Cell(tasdiqlangan, 11).DataType.ShouldBe(XLDataType.DateTime, "qaror lahzasi — haqiqiy Excel vaqti");
    }

    /// <summary>Ekrandagi ikkinchi jadval («qaror qabul qilinganlar») — <c>decided=true</c> faylga ham o'tadi.</summary>
    [Fact]
    public async Task Holat_filtri_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("attendance.view", "reports.export");

        var sorov = $"?employeeId={urug.XodimId}&decided=true";

        var ekran = await RoyxatAsync(client, "/api/attendance/requests" + sorov);
        ekran.Select(qator => qator.GetProperty("id").GetGuid()).ShouldBe([urug.TasdiqlanganId]);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "faqat qaror qabul qilingani");
        varaq.Cell(2, 8).GetString().ShouldBe("Tasdiqlangan");
    }

    /// <summary>Teskari oraliq — ro'yxatdagi bilan bir xil xato, fayl yo'q.</summary>
    [Fact]
    public async Task Teskari_oraliq_400_qaytaradi()
    {
        using var client = await MijozAsync("attendance.view", "reports.export");

        using var javob = await client.GetAsync(Yol(Eksport + "?from=2026-10-02&to=2026-10-01"), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("davomat.davr_notogri");
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "attendance.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            Yol($"{Eksport}?employeeId={urug.XodimId}&status=Pending&search={Uri.EscapeDataString(urug.XodimIsmi)}"),
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("AbsenceRequest");
        qator.Operation.ShouldBe("ExportAbsenceRequestsToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2, "kutayotgan ikkitasi");

        var filtrlar = tafsilot.RootElement.GetProperty("filters");
        filtrlar.GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        filtrlar.GetProperty("status").GetString().ShouldBe("Pending");
        filtrlar.GetProperty("employeeId").GetGuid().ShouldBe(urug.XodimId);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(
        Guid XodimId,
        string XodimIsmi,
        string Belgi,
        string TurNomi,
        Guid VaqtliId,
        Guid KopKunlikId,
        Guid TasdiqlanganId);

    /// <summary>
    /// Bitta xodim, uchta so'rov: 05.10 08:30–11:57 (izoh bilan), 10–12.10 butun kunlar va
    /// 20.10 — HR tasdiqlagan (qaror izohi bilan).
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        var belgi = "AR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var qarorchi = await TestAccounts.CreateAsync(_factory, "attendance.requests.manage");

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create("Сўровов", "Анвар", belgi).Value).Value;
        var tur = AbsenceType.Create("Шахсий иш " + belgi).Value;

        var vaqtli = AbsenceRequest.Create(
            xodim.Id, tur.Id, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5),
            new TimeOnly(8, 30), new TimeOnly(11, 57), "Shifokorga").Value;
        var kopKunlik = AbsenceRequest.Create(xodim.Id, tur.Id, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 12)).Value;
        var tasdiqlangan = AbsenceRequest.Create(xodim.Id, tur.Id, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20)).Value;

        kontekst.AddRange(xodim, tur);
        kontekst.AddRange(vaqtli, kopKunlik, tasdiqlangan);
        await kontekst.SaveChangesAsync(Ct);

        tasdiqlangan.Approve(qarorchi.UserId, DateTimeOffset.UtcNow, "Hujjat ko'rildi").IsSuccess.ShouldBeTrue();
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(xodim.Id, xodim.FullName.Full, belgi, tur.Name, vaqtli.Id, kopKunlik.Id, tasdiqlangan.Id);
    }

    /// <summary>So'rov fayldagi qaysi qatorda — ekrandagi o'rnidan (sarlavha 1-qator).</summary>
    private static int QatorRaqami(IReadOnlyList<JsonElement> ekran, Guid id)
        => ekran.Select((qator, indeks) => (qator, indeks)).Single(juft => juft.qator.GetProperty("id").GetGuid() == id).indeks + 2;

    private static async Task<IReadOnlyList<JsonElement>> RoyxatAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(Yol(manzil), Ct);

        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return [.. hujjat.RootElement.GetProperty("items").EnumerateArray().Select(qator => qator.Clone())];
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
