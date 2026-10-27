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
/// «Buyruqlar» ro'yxatining Excel eksporti — uchi-uchiga testlar
/// (<c>SPEC-excel-eksport.md</c> §2 #4).
/// </summary>
/// <remarks>
/// <para>
/// Urug' <see cref="EmploymentEventsApiTests"/> dagi bilan bir xil shaklda:
/// qabul → ko'chirish → oklad o'zgarishi, hammasi o'z buyruq raqami belgisi
/// bilan; ro'yxat <c>search</c> bilan shu belgiga toraytiriladi.
/// </para>
/// <para>
/// Eng muhim tekshiruv — oklad: fayl ro'yxatdagi <c>salary.view</c> qoidasini
/// chetlab o'tmasligi kerak.
/// </para>
/// </remarks>
public sealed class EmploymentEventExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Buyruqlar";

    private readonly BonviApiFactory _factory;

    public EmploymentEventExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(new Uri("/api/employments/events/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Ikkala ruxsat ham shart: <c>employment.view</c> VA <c>reports.export</c>.</summary>
    [Theory]
    [InlineData("employment.view")]
    [InlineData("reports.export")]
    public async Task Bitta_ruxsat_bilan_403_qaytaradi(string ruxsat)
    {
        using var client = await MijozAsync(ruxsat);

        using var javob = await client.GetAsync(new Uri("/api/employments/events/export", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fayldagi qatorlar ekran ro'yxati bilan — soni va TARTIBI bo'yicha — aynan
    /// mos; «Tafsilot» — «eski → yangi», bitta qatorda; izoh alohida ustunda.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("employment.view", "reports.export", "salary.view");

        var sorov = "?search=" + urug.Belgi;

        var ekran = await client.GetFromJsonAsync<EmploymentEventPageResponse>(
            new Uri("/api/employments/events" + sorov, UriKind.Relative), Ct);
        ekran!.Items.Select(qator => qator.Type).ShouldBe(["SalaryChange", "Transfer", "Hire"], "kuchga kirish sanasi bo'yicha, yangidan eskiga");

        using var javob = await client.GetAsync(new Uri("/api/employments/events/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_buyruqlar_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        Sarlavhalar(varaq).ShouldBe(
        [
            "F.I.Sh.", "Turi", "Kuchga kirish", "Buyruq", "Buyruq sanasi", "Tafsilot", "Lavozimi", "Bo'limi", "Izoh",
        ]);
        Ustun(varaq, 4).ShouldBe(ekran.Items.Select(qator => qator.OrderNo ?? string.Empty), "fayl — ekrandagi qatorlar, o'sha tartibda");
        Ustun(varaq, 1).ShouldAllBe(ism => ism == urug.Ism);

        // 1-qator — oklad o'zgarishi: summa ruxsat bilan ko'rinadi.
        varaq.Cell(2, 2).GetString().ShouldBe("Oklad o'zgarishi");
        varaq.Cell(2, 3).GetDateTime().ShouldBe(new DateTime(2024, 9, 1));
        varaq.Cell(2, 6).GetString().ShouldBe("Oylik ish haqi: 4 500 000 so'm → 5 200 000 so'm");
        varaq.Cell(2, 7).IsEmpty().ShouldBeTrue("oklad hodisasida lavozim yo'q");

        // 2-qator — ko'chirish: lavozim ham, bo'lim ham «eski → yangi».
        varaq.Cell(3, 2).GetString().ShouldBe("Bo'lim o'zgarishi");
        varaq.Cell(3, 5).GetDateTime().ShouldBe(new DateTime(2024, 5, 28));
        varaq.Cell(3, 6).GetString().ShouldBe(
            $"Lavozimi: Sotuvchi {urug.Belgi} → Omborchi {urug.Belgi}; Bo'limi: Sotuv {urug.Belgi} → Ombor {urug.Belgi}");
        varaq.Cell(3, 7).GetString().ShouldBe("Omborchi " + urug.Belgi);
        varaq.Cell(3, 8).GetString().ShouldBe("Ombor " + urug.Belgi);
        varaq.Cell(3, 9).GetString().ShouldBe("Omborga o'tkazildi");

        // 3-qator — qabul
        varaq.Cell(4, 2).GetString().ShouldBe("Ishga qabul");
    }

    /// <summary>
    /// ⭐ <c>salary.view</c> siz fayl ham okladni ko'rsatmaydi — ekrandagi qoida
    /// faylga ham tegishli.
    /// </summary>
    [Fact]
    public async Task Oklad_ruxsatisiz_faylda_summa_yoq()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("employment.view", "reports.export");

        var sorov = "?search=" + urug.Belgi;

        var ekran = await client.GetFromJsonAsync<EmploymentEventPageResponse>(
            new Uri("/api/employments/events" + sorov, UriKind.Relative), Ct);
        ekran!.Items[0].ToSalary.ShouldBeNull("ro'yxat ham yashiradi");

        using var javob = await client.GetAsync(new Uri("/api/employments/events/export" + sorov, UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        varaq.Cell(2, 2).GetString().ShouldBe("Oklad o'zgarishi");
        varaq.Cell(2, 6).IsEmpty().ShouldBeTrue("summa yo'q — tafsilot ham bo'sh");

        foreach (var qator in Enumerable.Range(2, varaq.LastRowUsed()!.RowNumber() - 1))
        {
            varaq.Row(qator).CellsUsed().ShouldAllBe(katak => !katak.GetString().Contains("so'm", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "employment.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            new Uri($"/api/employments/events/export?type=Transfer&search={urug.Belgi}", UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("EmploymentEvent");
        qator.Operation.ShouldBe("ExportEmploymentEventsToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(1);
        tafsilot.RootElement.GetProperty("filters").GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        tafsilot.RootElement.GetProperty("filters").GetProperty("type").GetString().ShouldBe("Transfer");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(string Belgi, string Ism);

    /// <summary>
    /// Bitta xodim, uchta buyruq: 15.01.2024 — qabul (Sotuv, Sotuvchi),
    /// 01.06.2024 — ko'chirish (Omborga, izoh bilan), 01.09.2024 — oklad 4,5 → 5,2 mln.
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = "BE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var firma = LegalEntity.Create("Buyruq Eksport MChJ " + belgi).Value;
        var sotuv = Department.Create("Sotuv " + belgi).Value;
        var ombor = Department.Create("Ombor " + belgi).Value;
        var sotuvchi = Position.Create("Sotuvchi " + belgi).Value;
        var omborchi = Position.Create("Omborchi " + belgi).Value;

        var xodim = Employee.Create(FullName.Create("Буйруқов", "Ходим", belgi).Value).Value;

        var munosabat = Employment.Hire(
            xodim.Id,
            firma.Id,
            new DateOnly(2024, 1, 15),
            sotuv.Id,
            sotuvchi.Id,
            orderNo: belgi + "-1").Value;

        munosabat.ImportEvent(EmploymentEvent.Import(
            munosabat.Id,
            EmploymentEventType.Transfer,
            new DateOnly(2024, 6, 1),
            orderNo: belgi + "-2",
            orderDate: new DateOnly(2024, 5, 28),
            fromDepartmentId: sotuv.Id,
            toDepartmentId: ombor.Id,
            fromPositionId: sotuvchi.Id,
            toPositionId: omborchi.Id,
            note: "Omborga o'tkazildi"));

        munosabat.ImportEvent(EmploymentEvent.Import(
            munosabat.Id,
            EmploymentEventType.SalaryChange,
            new DateOnly(2024, 9, 1),
            orderNo: belgi + "-3",
            fromSalary: Money.Create(4_500_000m).Value,
            toSalary: Money.Create(5_200_000m).Value));

        kontekst.AddRange(firma, sotuv, ombor, sotuvchi, omborchi, xodim);
        kontekst.Add(munosabat);
        await kontekst.SaveChangesAsync(Ct);

        return new Urug(belgi, xodim.FullName.Full);
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
