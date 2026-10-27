using System.Globalization;
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
/// «Audit jurnali»ning Excel eksporti (<c>SPEC-excel-eksport.md</c> §2 #9) — uchi-uchiga.
/// </summary>
/// <remarks>
/// <para>
/// Fayl <c>GET /api/audit</c> bilan AYNAN bir xil qatorlarni beradi — ko'rinish qoidasi bilan
/// birga: xavfsizlik hodisalari faqat <c>users.manage</c> bilan, shaxsiy maydon ruxsatsiz «•••».
/// </para>
/// <para>
/// Har bir test o'z bajaruvchisi yoki yozuvi bilan toraytiradi (<c>actor</c>, <c>entityId</c>):
/// bazada boshqa testlarning jurnal qatorlari ham bor.
/// </para>
/// </remarks>
public sealed class AuditExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Varaq = "Audit jurnali";
    private const string Eksport = "/api/audit/export";

    private readonly BonviApiFactory _factory;

    public AuditExportApiTests(BonviApiFactory factory) => _factory = factory;

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
    [InlineData("audit.view")]
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
    /// Bitta bajaruvchining ikki xodim kartasi: fayldagi qatorlar ekrandagi bilan soni va
    /// tartibi bo'yicha mos; kim, rol, amal va yozuv turi — ekrandagi matn bilan.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_jurnal_bilan_bir_xil_va_matnlari_ozbekcha()
    {
        var (muallif, ismlar) = await IkkiXodimAsync();
        using var client = await MijozAsync("audit.view", "reports.export", "employees.view");

        var sorov = $"?actor={muallif.UserId}&entity=Employee";

        var ekran = await RoyxatAsync(client, "/api/audit" + sorov);
        ekran.Count.ShouldBe(2);
        ekran.Select(qator => qator.GetProperty("entityLabel").GetString()).ShouldBe([ismlar[1], ismlar[0]], "eng yangisi birinchi");

        using var javob = await client.GetAsync(Yol(Eksport + sorov), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);
        (javob.Content.Headers.ContentDisposition?.FileNameStar ?? javob.Content.Headers.ContentDisposition?.FileName)
            .ShouldNotBeNull().ShouldContain("Bonvi_audit_");

        using var kitob = await KitobAsync(javob);
        var varaq = kitob.Worksheet(Varaq);

        string[] sarlavhalar = ["Vaqt", "Kim", "Roli", "Amal", "Nima", "Yozuv turi", "O'zgarishlar"];
        Enumerable.Range(1, sarlavhalar.Length).Select(ustun => varaq.Cell(1, ustun).GetString()).ShouldBe(sarlavhalar);
        varaq.LastColumnUsed()!.ColumnNumber().ShouldBe(sarlavhalar.Length, "IP ro'yxat qatorida yo'q — faylda ham");
        varaq.LastRowUsed()!.RowNumber().ShouldBe(3);

        for (var i = 0; i < ekran.Count; i++)
        {
            var qator = i + 2;
            var kutilgan = ekran[i];
            var bajaruvchi = kutilgan.GetProperty("actor");

            kutilgan.GetProperty("operation").GetString().ShouldBe("CreateEmployee");

            var vaqt = kutilgan.GetProperty("occurredAt").GetDateTimeOffset().ToOffset(TimeSpan.FromHours(5)).DateTime;
            varaq.Cell(qator, 1).GetDateTime().ShouldBe(vaqt, TimeSpan.FromSeconds(1), "vaqt — Toshkent vaqtida");
            varaq.Cell(qator, 2).GetString().ShouldBe(bajaruvchi.GetProperty("name").GetString());
            varaq.Cell(qator, 3).GetString().ShouldBe(bajaruvchi.GetProperty("roleName").GetString());
            varaq.Cell(qator, 4).GetString().ShouldBe("Yaratildi · Xodim kartasini ochish");
            varaq.Cell(qator, 5).GetString().ShouldBe(kutilgan.GetProperty("entityLabel").GetString());
            varaq.Cell(qator, 6).GetString().ShouldBe("Xodim kartasi");
            varaq.Cell(qator, 7).GetString().ShouldNotBeNullOrWhiteSpace("yaratilgan maydonlar nomi");
        }
    }

    /// <summary>
    /// Shaxsiy maydon (manzil) — <c>employees.personal.view</c> siz ekranda ham, faylda ham
    /// faqat «•••»; ruxsat bilan — qiymati.
    /// </summary>
    [Fact]
    public async Task Yashirilgan_maydon_faylda_ham_yashirin()
    {
        var (xodimId, manzil) = await ManzilTahririAsync();
        var sorov = $"?entity=Employee&entityId={xodimId}&action=Update";

        using var oddiy = await MijozAsync("audit.view", "reports.export", "employees.view");

        var ekran = (await RoyxatAsync(oddiy, "/api/audit" + sorov)).ShouldHaveSingleItem();
        var ozgarish = ekran.GetProperty("changes").EnumerateArray().ShouldHaveSingleItem();
        ozgarish.GetProperty("field").GetString().ShouldBe("Address");
        ozgarish.GetProperty("masked").GetBoolean().ShouldBeTrue();

        using (var javob = await oddiy.GetAsync(Yol(Eksport + sorov), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            using var kitob = await KitobAsync(javob);

            kitob.Worksheet(Varaq).Cell(2, 7).GetString().ShouldBe("Manzil: •••");
            kitob.Worksheet(Varaq).Cell(2, 4).GetString().ShouldStartWith("O'zgartirildi");
        }

        using var shaxsiy = await MijozAsync("audit.view", "reports.export", "employees.view", "employees.personal.view");

        using (var javob = await shaxsiy.GetAsync(Yol(Eksport + sorov), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            using var kitob = await KitobAsync(javob);

            kitob.Worksheet(Varaq).Cell(2, 7).GetString().ShouldBe($"Manzil: — → {manzil}");
        }
    }

    /// <summary>
    /// «Kirish va xavfsizlik» — ro'yxatdagi qoida: <c>users.manage</c> siz «kind=security»
    /// «o'zgarishlar» bo'lib ishlaydi va kirish hodisasi faylga TUSHMAYDI.
    /// </summary>
    [Fact]
    public async Task Xavfsizlik_hodisalari_users_manage_siz_faylga_tushmaydi()
    {
        var hisob = await TestAccounts.CreateAsync(_factory);
        await KirishAsync(hisob.Login, TestAccounts.Password);

        var sorov = $"?kind=security&actor={hisob.UserId}";

        using (var ruxsatsiz = await MijozAsync("audit.view", "reports.export"))
        {
            (await RoyxatAsync(ruxsatsiz, "/api/audit" + sorov)).ShouldBeEmpty();

            using var javob = await ruxsatsiz.GetAsync(Yol(Eksport + sorov), Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            using var kitob = await KitobAsync(javob);

            kitob.Worksheet(Varaq).LastRowUsed()!.RowNumber().ShouldBe(1, "faqat sarlavha");
        }

        using var boshqaruvchi = await MijozAsync("audit.view", "reports.export", "users.manage");

        (await RoyxatAsync(boshqaruvchi, "/api/audit" + sorov)).ShouldHaveSingleItem()
            .GetProperty("action").GetString().ShouldBe("Login");

        using (var javob = await boshqaruvchi.GetAsync(Yol(Eksport + sorov), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            using var kitob = await KitobAsync(javob);
            var varaq = kitob.Worksheet(Varaq);

            varaq.LastRowUsed()!.RowNumber().ShouldBe(2);
            // Ekrandagidek: amal turi va endpoint nomi («Login») bo'yicha biznes amali
            varaq.Cell(2, 4).GetString().ShouldBe("Tizimga kirdi · Tizimga kirish");
        }
    }

    [Fact]
    public async Task Eksport_jurnalga_filtr_va_sanoq_bilan_yoziladi()
    {
        var (muallif, _) = await IkkiXodimAsync();
        var hisob = await TestAccounts.CreateAsync(_factory, "audit.view", "reports.export", "employees.view");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hisob.AccessToken);

        using var javob = await client.GetAsync(Yol($"{Eksport}?actor={muallif.UserId}&entity=Employee"), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var qator = (await kontekst.AuditLogs.AsNoTracking()
                .Where(log => log.Action == AuditAction.Export && log.UserId == hisob.UserId)
                .ToListAsync(Ct))
            .ShouldHaveSingleItem();

        qator.EntityName.ShouldBe("AuditLog");
        qator.Operation.ShouldBe("ExportAuditEntriesToExcel");
        qator.NewValues.ShouldNotBeNull();

        using var tafsilot = JsonDocument.Parse(qator.NewValues);
        tafsilot.RootElement.GetProperty("rows").GetInt32().ShouldBe(2);
        tafsilot.RootElement.GetProperty("filters").GetProperty("entity").GetString().ShouldBe("Employee");
        tafsilot.RootElement.GetProperty("filters").GetProperty("actor").GetString()
            .ShouldBe(muallif.UserId.ToString("D", CultureInfo.InvariantCulture));
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// O'z rolidagi bajaruvchi HTTP orqali ikkita xodim kartasini ochadi — ketma-ket, ya'ni
    /// jurnalda ikkinchisi yangiroq.
    /// </summary>
    private async Task<(TestAccount Muallif, string[] Ismlar)> IkkiXodimAsync()
    {
        var belgi = "AU" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var muallif = await TestAccounts.CreateAsync(_factory, "employees.view", "employees.create");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", muallif.AccessToken);

        var ismlar = new List<string>();

        foreach (var ism in new[] { "Биринчи", "Иккинчи" })
        {
            using var yaratish = await client.PostAsJsonAsync(
                Yol("/api/employees"),
                new { lastName = "Журналов", firstName = ism, middleName = belgi },
                Ct);
            yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

            using var hujjat = JsonDocument.Parse(await MatnAsync(yaratish));
            ismlar.Add(hujjat.RootElement.GetProperty("fullName").GetString()!);
        }

        return (muallif, [.. ismlar]);
    }

    /// <summary>Xodim kartasi va undan keyingi ALOHIDA saqlashda manzil tahriri (bitta maydon).</summary>
    private async Task<(Guid XodimId, string Manzil)> ManzilTahririAsync()
    {
        var belgi = "AM" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var manzil = "Тошкент, Юнусобод " + belgi;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var xodim = Employee.Create(FullName.Create("Манзилов", "Ходим", belgi).Value).Value;
        kontekst.Add(xodim);
        await kontekst.SaveChangesAsync(Ct);

        xodim.SetAddress(manzil);
        await kontekst.SaveChangesAsync(Ct);

        return (xodim.Id, manzil);
    }

    private async Task KirishAsync(string login, string parol)
    {
        using var client = _factory.CreateClient();
        using var javob = await client.PostAsJsonAsync(Yol("/api/auth/login"), new { login, password = parol }, Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

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
