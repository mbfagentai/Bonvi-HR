using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Xodimlar ro'yxatining Excel eksporti — uchi-uchiga testlar.
/// </summary>
/// <remarks>
/// Bu testlarning asosiy maqsadi — talabning o'zi: eksport EKRANDAGI
/// (<c>GET /api/employees</c>) bilan bir xil filtr, bir xil saralash, bir
/// xil qatorlarni berishi. Shu sababli asosiy test ikkalasini bir xil
/// parametr bilan chaqirib, natijalarni TAQQOSLAYDI — alohida-alohida
/// tekshirish emas.
/// </remarks>
public sealed class EmployeeExportApiTests : IClassFixture<BonviApiFactory>
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly BonviApiFactory _factory;

    public EmployeeExportApiTests(BonviApiFactory factory) => _factory = factory;

    private static string Belgi() => "EX" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/reports/employees/export", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Excel_fayl_togri_content_type_va_sarlavha_bilan_qaytadi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            new Uri("/api/reports/employees/export", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        javob.Content.Headers.ContentType?.MediaType.ShouldBe(ExcelContentType);

        var fileName = javob.Content.Headers.ContentDisposition?.FileNameStar
            ?? javob.Content.Headers.ContentDisposition?.FileName;

        fileName.ShouldNotBeNull();
        fileName.ShouldContain("Bonvi_xodimlar_");
        fileName.ShouldContain(".xlsx");

        var baytlar = await javob.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var kitob = new XLWorkbook(new MemoryStream(baytlar));

        kitob.Worksheets.Contains("Xodimlar").ShouldBeTrue();
        var varaq = kitob.Worksheet("Xodimlar");
        varaq.Cell(1, 1).GetString().ShouldBe("F.I.O.");
    }

    /// <summary>
    /// Eng muhim test: Excel'dagi qatorlar, SHU BIR XIL filtr va saralash
    /// bilan chaqirilgan ekran ro'yxati bilan — nomi va TARTIBI bo'yicha —
    /// AYNAN mos keladi.
    /// </summary>
    [Fact]
    public async Task Eksport_ekrandagi_royxat_bilan_bir_xil_qatorlarni_beradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;

            // Ataylab alifbo bo'yicha TESKARI tartibda yaratiladi — agar
            // ikkalasi ham «name» bo'yicha o'sish tartibida to'g'ri
            // saralasa, bu holat yaratish tartibi bilan yashirinib qolmaydi.
            var uchinchi = Employee.Create(FullName.Create($"{belgi}Uchinchi", "Botir", null).Value).Value;
            var birinchi = Employee.Create(FullName.Create($"{belgi}Birinchi", "Anvar", null).Value).Value;
            var ikkinchi = Employee.Create(FullName.Create($"{belgi}Ikkinchi", "Davron", null).Value).Value;

            var m1 = Employment.Hire(uchinchi.Id, firma.Id, new DateOnly(2021, 1, 1)).Value;
            m1.CompleteProbation(new DateOnly(2021, 1, 1)).IsSuccess.ShouldBeTrue();
            var m2 = Employment.Hire(birinchi.Id, firma.Id, new DateOnly(2021, 2, 1)).Value;
            m2.CompleteProbation(new DateOnly(2021, 2, 1)).IsSuccess.ShouldBeTrue();
            var m3 = Employment.Hire(ikkinchi.Id, firma.Id, new DateOnly(2021, 3, 1)).Value;
            m3.CompleteProbation(new DateOnly(2021, 3, 1)).IsSuccess.ShouldBeTrue();

            kontekst.Add(firma);
            kontekst.AddRange(uchinchi, birinchi, ikkinchi);
            kontekst.AddRange(m1, m2, m3);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var sorov = $"?search={Uri.EscapeDataString(belgi)}&sort=name";

        // ── Ekran ────────────────────────────────────────────────────
        using var ekranJavob = await client.GetAsync(
            new Uri($"/api/employees{sorov}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        ekranJavob.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var ekranHujjat = JsonDocument.Parse(
            await ekranJavob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var ekranIsmlari = ekranHujjat.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("fullName").GetString())
            .ToList();

        ekranIsmlari.Count.ShouldBe(3);

        // ── Eksport ──────────────────────────────────────────────────
        using var eksportJavob = await client.GetAsync(
            new Uri($"/api/reports/employees/export{sorov}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        eksportJavob.StatusCode.ShouldBe(HttpStatusCode.OK);

        var baytlar = await eksportJavob.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var kitob = new XLWorkbook(new MemoryStream(baytlar));
        var varaq = kitob.Worksheet("Xodimlar");

        List<string> eksportIsmlari = [];
        var qator = 2;
        while (!varaq.Cell(qator, 1).IsEmpty())
        {
            eksportIsmlari.Add(varaq.Cell(qator, 1).GetString());
            qator++;
        }

        // Aynan bir xil ismlar, aynan bir xil tartibda.
        eksportIsmlari.ShouldBe(ekranIsmlari!);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }
}
