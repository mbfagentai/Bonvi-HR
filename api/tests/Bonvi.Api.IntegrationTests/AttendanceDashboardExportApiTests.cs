using System.Globalization;
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
/// «Davomat → Statistika» xodimlar jadvalining Excel eksporti (<c>SPEC-excel-eksport.md</c> §2 #8)
/// — uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// Jadval serverda filtrlanadi (sana, firma/bo'lim/lavozim, holat chipi, qidiruv), ya'ni fayl
/// <c>GET /api/attendance/dashboard</c> ning AYNAN o'sha parametrlari bilan solishtiriladi.
/// </para>
/// <para>
/// Soat haqiqiy, shu sababli faqat vaqtga bog'liq bo'lmagan holatlar ishlatiladi: o'tgan
/// sanadagi o'tishsiz ish kuni — doim «Kelmadi», jadvalsiz odam — «Grafik yo'q». Har bir test
/// o'z bo'limini yaratadi va jadvalni shu bo'lim bilan toraytiradi.
/// </para>
/// </remarks>
public sealed class AttendanceDashboardExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Davomat statistikasi";
    private const string Eksport = "/api/attendance/dashboard/export";

    private readonly BonviApiFactory _factory;

    public AttendanceDashboardExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>O'tgan sana — bugungi soatga bog'liq bo'lmasin (3 kun oldin, Toshkent).</summary>
    private static DateOnly Sana => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5)).DateTime).AddDays(-3);

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    // ────────────────────────────────────────────────────────────
    //  Mazmun
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eksport_ekrandagi_jadval_bilan_bir_xil_va_ustunlari_togri()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("attendance.view", "reports.export");

        var sorov = $"?date={Iso(Sana)}&departmentId={urug.BolimId}";

        var ekran = await JadvalAsync(client, "/api/attendance/dashboard" + sorov + "&pageSize=200");
        ekran.Count.ShouldBe(3);

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain($"Bonvi_davomat_statistika_{Iso(Sana)}", customMessage: "fayl nomida — jadval sanasi");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        string[] sarlavhalar = ["F.I.Sh.", "Lavozim", "Bo'lim", "Kelgan", "Ketgan", "Holat", "Kechikish (daq.)"];
        Enumerable.Range(1, sarlavhalar.Length).Select(ustun => varaq.Cell(1, ustun).GetString()).ShouldBe(sarlavhalar);
        varaq.LastColumnUsed()!.ColumnNumber().ShouldBe(sarlavhalar.Length);
        varaq.LastRowUsed()!.RowNumber().ShouldBe(ekran.Count + 1);

        for (var i = 0; i < ekran.Count; i++)
        {
            var qator = i + 2;
            var kutilgan = ekran[i];

            varaq.Cell(qator, 1).GetString().ShouldBe(kutilgan.FullName, $"{qator}-qator — ekrandagi tartib");
            varaq.Cell(qator, 2).GetString().ShouldBe(urug.LavozimNomi);
            varaq.Cell(qator, 3).GetString().ShouldBe(urug.BolimNomi);
            varaq.Cell(qator, 4).IsEmpty().ShouldBeTrue("o'tish yo'q — kelgan vaqt bo'sh");
            varaq.Cell(qator, 5).IsEmpty().ShouldBeTrue();
            varaq.Cell(qator, 6).GetString().ShouldBe(kutilgan.Status == "Absent" ? "Kelmadi" : "Grafik yo'q");
            varaq.Cell(qator, 7).IsEmpty().ShouldBeTrue("kechikish faqat «Late» da");
        }

        ekran.Single(qator => qator.EmployeeId == urug.JadvalliId).Status.ShouldBe("Absent");
    }

    /// <summary>
    /// Holat chipi, qidiruv va teskari saralash — ekrandagi kabi SERVERDA, faylga ham o'tadi.
    /// </summary>
    [Fact]
    public async Task Holat_qidiruv_va_saralash_eksportga_ham_qollanadi()
    {
        var urug = await UrugAsync();
        using var client = await MijozAsync("attendance.view", "reports.export");

        var holat = $"?date={Iso(Sana)}&departmentId={urug.BolimId}&status=NoSchedule&sort=-name";

        var ekran = await JadvalAsync(client, "/api/attendance/dashboard" + holat);
        ekran.Select(qator => qator.EmployeeId).ShouldBe([urug.UchinchiId, urug.JadvalsizId], "jadvalsiz ikkitasi, Я→А");

        using (var javob = await client.GetAsync(Yol(Eksport + holat), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            using var kitob = await KitobAsync(javob);
            var varaq = kitob.Worksheet(Varaq);

            varaq.LastRowUsed()!.RowNumber().ShouldBe(3);
            varaq.Cell(2, 1).GetString().ShouldBe(ekran[0].FullName);
            varaq.Cell(3, 1).GetString().ShouldBe(ekran[1].FullName);
        }

        var qidiruv = $"?date={Iso(Sana)}&departmentId={urug.BolimId}&search={urug.Belgi}A";

        (await JadvalAsync(client, "/api/attendance/dashboard" + qidiruv))
            .Select(qator => qator.EmployeeId).ShouldBe([urug.JadvalliId]);

        using (var javob = await client.GetAsync(Yol(Eksport + qidiruv), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            using var kitob = await KitobAsync(javob);
            var varaq = kitob.Worksheet(Varaq);

            varaq.LastRowUsed()!.RowNumber().ShouldBe(2, "qidiruv faqat bitta odamni qoldiradi");
            varaq.Cell(2, 6).GetString().ShouldBe("Kelmadi");
        }
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_ismsiz_yoziladi()
    {
        var urug = await UrugAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "attendance.view", "reports.export");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(
            Yol($"{Eksport}?date={Iso(Sana)}&departmentId={urug.BolimId}&status=NoSchedule&search={urug.Belgi}"),
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("AttendanceDay");
        qator.Operation.ShouldBe("ExportAttendanceDashboardToExcel");
        qator.NewValues.ShouldNotBeNull();
        qator.NewValues.ShouldNotContain(urug.Belgi);

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2);

        var filtrlar = tafsilot.RootElement.GetProperty("filters");
        filtrlar.GetProperty("hasSearch").GetBoolean().ShouldBeTrue();
        filtrlar.GetProperty("date").GetString().ShouldBe(Iso(Sana));
        filtrlar.GetProperty("departmentId").GetGuid().ShouldBe(urug.BolimId);
        filtrlar.GetProperty("status").GetString().ShouldBe("NoSchedule");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(
        Guid BolimId,
        string BolimNomi,
        string LavozimNomi,
        string Belgi,
        Guid JadvalliId,
        Guid JadvalsizId,
        Guid UchinchiId);

    private sealed record JadvalQatori(Guid EmployeeId, string FullName, string Status);

    /// <summary>
    /// Bitta bo'lim va lavozimda uch xodim (2025-01-01 dan): A — har kuni 09:00–18:00 jadval
    /// bilan (o'tgan sanada o'tishsiz — «Kelmadi»), B va C — jadvalsiz («Grafik yo'q»).
    /// </summary>
    private async Task<Urug> UrugAsync()
    {
        var belgi = "DS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        Guid bolimId, a, b, c;
        string bolimNomi, lavozimNomi;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create("Статистика МЧЖ " + belgi).Value;
            var bolim = Department.Create("Статистика бўлими " + belgi).Value;
            var lavozim = Position.Create("Оператор " + belgi).Value;

            var xodimlar = new[] { ("Анвар", "A"), ("Ботир", "B"), ("Валижон", "C") }
                .Select(ism => Employee.Create(FullName.Create("Статистиков", ism.Item1, belgi + ism.Item2).Value).Value)
                .ToList();

            kontekst.AddRange(firma, bolim, lavozim);
            kontekst.AddRange(xodimlar);
            kontekst.AddRange(xodimlar.Select(xodim =>
                Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id, lavozim.Id).Value));
            await kontekst.SaveChangesAsync(Ct);

            (bolimId, bolimNomi, lavozimNomi) = (bolim.Id, bolim.Name, lavozim.Name);
            (a, b, c) = (xodimlar[0].Id, xodimlar[1].Id, xodimlar[2].Id);
        }

        using var admin = await AdminMijozAsync();
        await XodimgaJadvalAsync(admin, a, belgi);

        return new Urug(bolimId, bolimNomi, lavozimNomi, belgi, a, b, c);
    }

    /// <summary>Har kuni 09:00–18:00 jadval va uni XODIM pog'onasida biriktirish (HTTP orqali).</summary>
    private static async Task XodimgaJadvalAsync(HttpClient admin, Guid xodimId, string belgi)
    {
        using var jadval = await admin.PostAsJsonAsync(
            Yol("/api/attendance/schedules"),
            new
            {
                name = "Jadval " + belgi,
                graceInMinutes = 10,
                graceOutMinutes = 10,
                note = (string?)null,
                days = Enumerable.Range(1, 7)
                    .Select(kun => new { dayIndex = kun, isWorking = true, startTime = "09:00", endTime = "18:00", breakMinutes = 0 }),
            },
            Ct);
        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));

        using var hujjat = JsonDocument.Parse(await MatnAsync(jadval));
        var jadvalId = hujjat.RootElement.GetProperty("id").GetGuid();

        using var biriktirish = await admin.PostAsJsonAsync(
            Yol("/api/attendance/schedule-assignments"),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            Ct);
        biriktirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirish));
    }

    private static async Task<IReadOnlyList<JadvalQatori>> JadvalAsync(HttpClient client, string manzil)
    {
        using var javob = await client.GetAsync(Yol(manzil), Ct);

        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, matn);

        using var hujjat = JsonDocument.Parse(matn);
        return
        [
            .. hujjat.RootElement.GetProperty("items").EnumerateArray().Select(qator => new JadvalQatori(
                qator.GetProperty("employeeId").GetGuid(),
                qator.GetProperty("fullName").GetString()!,
                qator.GetProperty("status").GetString()!)),
        ];
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

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            Yol("/api/auth/login"),
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
