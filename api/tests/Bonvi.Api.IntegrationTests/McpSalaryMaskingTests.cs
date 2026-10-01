using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// AI ulanish — pul summalari chaqiruvchining O'Z ruxsatlari bilan
/// (<c>docs/SPEC-mcp.md</c> §0.2, §7): oklad faqat <c>salary.view</c> bilan,
/// qo'shimcha vazifa haqi (va «jami») faqat <c>salary.extra.view</c> bilan;
/// buyruqlar lentasi faqat <c>employment.view</c> bilan.
/// </summary>
/// <remarks>
/// <see cref="ExtraPayApiTests"/> dagi veb qoidasining MCP'dagi aksi:
/// <c>get_employee</c> va <c>employment_events</c> vositalari bir xil
/// niqoblashi kerak — AI veb-panelda ko'rinmaydigan summani ochmasin.
/// </remarks>
public sealed class McpSalaryMaskingTests : IClassFixture<McpHost>
{
    /// <summary>Oklad — boshqa hech qayerda uchramaydigan son.</summary>
    private const decimal Oklad = 7_345_000m;

    /// <summary>Qo'shimcha vazifa haqi.</summary>
    private const decimal QoshimchaHaq = 1_234_000m;

    private readonly McpHost _host;

    public McpSalaryMaskingTests(McpHost host) => _host = host;

    private static string OkladMatni => Oklad.ToString("0", CultureInfo.InvariantCulture);

    private static string HaqMatni => QoshimchaHaq.ToString("0", CultureInfo.InvariantCulture);

    private static string JamiMatni => (Oklad + QoshimchaHaq).ToString("0", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Salary_view_siz_hech_bir_summa_chiqmaydi()
    {
        var xodimId = await UrugAsync();
        var (karta, buyruqlar) = await KorAsync(
            xodimId, Permissions.McpConnect, Permissions.EmployeesView, Permissions.EmploymentView);

        foreach (var (qayer, matn) in new[] { ("get_employee", Hammasi(karta)), ("employment_events", Hammasi(buyruqlar)) })
        {
            matn.ShouldNotContain(OkladMatni, customMessage: $"{qayer}: oklad ochildi");
            matn.ShouldNotContain(HaqMatni, customMessage: $"{qayer}: qo'shimcha haq ochildi");
            matn.ShouldNotContain(JamiMatni, customMessage: $"{qayer}: jami ochildi");
        }

        var ish = Ish(karta);
        ish["salary"].ShouldBeNull();
        ish["totalPay"].ShouldBeNull();
        Vazifa(ish)["pay"].ShouldBeNull();

        // Lenta bor (employment.view), lekin summalarsiz; sabab aytiladi.
        var lenta = Tugun(karta)["timeline"]?.AsArray();
        lenta.ShouldNotBeNull();
        lenta.ShouldNotBeEmpty();
        Tugun(karta)["sections"]!["omitted"]!.ToJsonString().ShouldContain("salary.view");
    }

    [Fact]
    public async Task Faqat_salary_view_bilan_oklad_korinadi_qoshimcha_haq_yoq()
    {
        var xodimId = await UrugAsync();
        var (karta, buyruqlar) = await KorAsync(
            xodimId, Permissions.McpConnect, Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView);

        karta.Text.ShouldContain(OkladMatni);
        buyruqlar.Text.ShouldContain(OkladMatni);

        foreach (var (qayer, matn) in new[] { ("get_employee", Hammasi(karta)), ("employment_events", Hammasi(buyruqlar)) })
        {
            matn.ShouldNotContain(HaqMatni, customMessage: $"{qayer}: qo'shimcha haq ochildi");

            // «Jami» ham yo'q — undan okladni ayirib qo'shimcha haqni hisoblab olish mumkin edi.
            matn.ShouldNotContain(JamiMatni, customMessage: $"{qayer}: jami ochildi");
        }

        var ish = Ish(karta);
        ish["salary"]!["amount"]!.GetValue<decimal>().ShouldBe(Oklad);
        ish["totalPay"].ShouldBeNull();

        var vazifa = Vazifa(ish);
        vazifa["note"].ShouldNotBeNull("vazifaning o'zi yashirin emas");
        vazifa["pay"].ShouldBeNull();

        Tugun(karta)["sections"]!["omitted"]!.ToJsonString().ShouldContain("salary.extra.view");
    }

    [Fact]
    public async Task Ikkala_ruxsat_bilan_oklad_haq_va_jami_korinadi()
    {
        var xodimId = await UrugAsync();
        var (karta, buyruqlar) = await KorAsync(
            xodimId,
            Permissions.McpConnect,
            Permissions.EmployeesView,
            Permissions.EmploymentView,
            Permissions.SalaryView,
            Permissions.SalaryExtraView);

        var ish = Ish(karta);
        ish["salary"]!["amount"]!.GetValue<decimal>().ShouldBe(Oklad);
        Vazifa(ish)["pay"]!["amount"]!.GetValue<decimal>().ShouldBe(QoshimchaHaq);
        ish["totalPay"]!["amount"]!.GetValue<decimal>().ShouldBe(Oklad + QoshimchaHaq);

        buyruqlar.Text.ShouldContain(OkladMatni);
        buyruqlar.Text.ShouldContain(HaqMatni);
    }

    [Fact]
    public async Task Employment_view_siz_kartada_buyruqlar_lentasi_yoq()
    {
        var xodimId = await UrugAsync();
        var flow = new McpFlow(_host);
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);

        var karta = await flow.CallAsync(tokens.Access, "get_employee", new { id = xodimId.ToString() });
        karta.IsError.ShouldBeFalse(karta.Text);

        var tugun = Tugun(karta);
        tugun["timeline"].ShouldBeNull();
        tugun["sections"]!["omitted"]!.ToJsonString().ShouldContain("employment.view");
        Hammasi(karta).ShouldNotContain(OkladMatni);

        // Buyruqlar jurnali vositasi esa umuman rad etadi.
        var buyruqlar = await flow.CallAsync(tokens.Access, "employment_events", new { employeeId = xodimId.ToString() });
        buyruqlar.IsError.ShouldBeTrue(buyruqlar.Text);
        buyruqlar.Text.ShouldContain("Ruxsat yo'q");
    }

    // ────────────────────────────────────────────────────────────

    /// <summary>Shu ruxsatli hisob nomidan karta va buyruqlar jurnali (xom matn).</summary>
    private async Task<(McpCallResult Karta, McpCallResult Buyruqlar)> KorAsync(Guid xodimId, params string[] ruxsatlar)
    {
        var flow = new McpFlow(_host);
        var (_, tokens) = await flow.ConnectNewAsync(ruxsatlar);

        var karta = await flow.CallAsync(tokens.Access, "get_employee", new { id = xodimId.ToString() });
        karta.Status.ShouldBe(HttpStatusCode.OK);
        karta.IsError.ShouldBeFalse(karta.Text);

        var buyruqlar = await flow.CallAsync(tokens.Access, "employment_events", new { employeeId = xodimId.ToString() });
        buyruqlar.Status.ShouldBe(HttpStatusCode.OK);
        buyruqlar.IsError.ShouldBeFalse(buyruqlar.Text);

        return (karta, buyruqlar);
    }

    private static JsonNode Tugun(McpCallResult natija)
    {
        var tugun = McpFlow.TryParse(natija.Text);
        tugun.ShouldNotBeNull(natija.Text);
        return tugun;
    }

    /// <summary>Matn VA xom tana birga — summa qaysi birida bo'lmasin, topilsin.</summary>
    private static string Hammasi(McpCallResult natija) => natija.Text + "\n" + natija.Raw;

    /// <summary>Kartadagi yagona mehnat munosabati.</summary>
    private static JsonNode Ish(McpCallResult karta)
        => Tugun(karta)["employments"]!.AsArray().ShouldHaveSingleItem()!;

    /// <summary>Munosabatdagi yagona faol qo'shimcha vazifa.</summary>
    private static JsonNode Vazifa(JsonNode ish)
        => ish["activeDuties"]!.AsArray().ShouldHaveSingleItem()!;

    /// <summary>
    /// Xodim: oklad (buyruq bilan, «SalaryChange») va haqli qo'shimcha vazifa — ikkalasi so'nggi
    /// 30 kun ichida (buyruqlar jurnalining sukut oralig'i).
    /// </summary>
    private async Task<Guid> UrugAsync()
    {
        var belgi = McpHost.Belgi();
        var bugun = McpToolContext.Today;

        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Oklad MChJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Окладов" + belgi, "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, bugun.AddDays(-20)).Value;

        munosabat.SetSalary(Money.Create(Oklad).Value, bugun.AddDays(-20), "OK-" + belgi).IsSuccess.ShouldBeTrue();
        munosabat.AddAdditionalDuty(bugun.AddDays(-10), "Omborga mas'ul", Money.Create(QoshimchaHaq).Value, "QH-" + belgi)
            .IsSuccess.ShouldBeTrue();

        kontekst.AddRange(firma, xodim);
        kontekst.Add(munosabat);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return xodim.Id;
    }
}
