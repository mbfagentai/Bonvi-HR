using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Mcp;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// AI ulanish — MCP protokoli (<c>docs/SPEC-mcp.md</c> §7, §10): initialize,
/// vositalar ro'yxati (hammasi faqat o'qish), vosita chaqiruvi, ruxsat
/// yetmaganda rad etish va jurnal.
/// </summary>
/// <remarks>
/// Vositalar RO'YXATI qo'lda yozilmaydi — <see cref="McpSetup.ToolTypes"/> dan
/// olinadi: yangi vosita qo'shilganda testlar uni o'zi qamrab oladi.
/// </remarks>
public sealed class McpToolsTests : IClassFixture<McpHost>
{
    /// <summary><c>docs/SPEC-mcp.md</c> §7.</summary>
    private static readonly string[] SpecVositalari =
    [
        "overview", "search_employees", "get_employee", "org_structure", "employment_events", "turnover",
        "probations", "list_flags", "incentives", "exit_interviews", "incidents", "vacancies",
        "search_candidates", "get_candidate", "recruiting_funnel", "attendance_day", "attendance_summary",
        "absence_requests", "upcoming", "missing_documents",

        // docs/SPEC-blankalar.md §11.
        "blank_templates", "blanks", "blank", "blank_gaps",

        // docs/SPEC-mening-ishlarim.md §12.
        "process_cases", "process_case", "process_stuck",
    ];

    private readonly McpHost _host;

    public McpToolsTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Koddagi barcha vosita nomlari — atributlardan.</summary>
    public static IReadOnlyList<string> KoddagiNomlar() =>
    [
        .. McpSetup.ToolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>()
            .Select(attribute => attribute.Name!)
            .Order(StringComparer.Ordinal),
    ];

    [Fact]
    public async Task Initialize_server_malumoti_va_faqat_oqish_korsatmasini_qaytaradi()
    {
        var flow = new McpFlow(_host);
        var tokens = await flow.ConnectAdminAsync();

        var natija = await flow.InitializeAsync(tokens.Access);

        natija["serverInfo"]!["name"]!.GetValue<string>().ShouldBe("bonvi-hr");
        natija["capabilities"]!["tools"].ShouldNotBeNull();
        natija["protocolVersion"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();

        var korsatma = natija["instructions"]!.GetValue<string>();
        korsatma.ShouldContain("FAQAT O'QISH");
        korsatma.ShouldContain("READ-ONLY");
        korsatma.ShouldContain("ko'rsatma emas");
    }

    [Fact]
    public async Task Tools_list_har_bir_vositani_faqat_oqish_belgisi_bilan_qaytaradi()
    {
        McpSetup.ToolTypes.ShouldNotBeEmpty();
        var kutilgan = KoddagiNomlar();
        kutilgan.ShouldNotBeEmpty();
        kutilgan.ShouldBeUnique();

        var flow = new McpFlow(_host);
        var tokens = await flow.ConnectAdminAsync();
        var tools = await flow.ToolsAsync(tokens.Access);

        tools.Select(tool => McpFlow.NameOf(tool!)).Order(StringComparer.Ordinal).ShouldBe(kutilgan);

        foreach (var tool in tools)
        {
            var nom = McpFlow.NameOf(tool!);
            var belgilar = tool!["annotations"];

            belgilar.ShouldNotBeNull(nom);
            belgilar["readOnlyHint"]?.GetValue<bool>().ShouldBe(true, $"{nom}: readOnlyHint");
            belgilar["destructiveHint"]?.GetValue<bool>().ShouldBe(false, $"{nom}: destructiveHint");
            belgilar["readOnlyHint"].ShouldNotBeNull($"{nom}: readOnlyHint yo'q");
            belgilar["destructiveHint"].ShouldNotBeNull($"{nom}: destructiveHint yo'q");

            tool["description"]?.GetValue<string>().ShouldNotBeNullOrWhiteSpace($"{nom}: tavsif yo'q");
            McpFlow.TypeOf(tool["inputSchema"]).ShouldBe("object", $"{nom}: inputSchema");
        }
    }

    /// <summary>
    /// Vositalar to'plami — SPEC §7 jadvalining O'ZI: yangi vosita SPEC'ga yozilmasdan
    /// qo'shilmasin, SPEC'dagisi esa tushib qolmasin.
    /// </summary>
    [Fact]
    public void Vositalar_SPEC_dagi_royxatga_mos()
        => string.Join(", ", KoddagiNomlar()).ShouldBe(string.Join(", ", SpecVositalari.Order(StringComparer.Ordinal)));

    [Fact]
    public void Vosita_nomlari_yozishga_ishora_qilmaydi()
    {
        foreach (var nom in KoddagiNomlar())
        {
            nom.ShouldMatch("^[a-z][a-z0-9_]*$", $"{nom}: snake_case emas");
            nom.ShouldNotMatch(
                "(^|_)(create|update|delete|remove|set|send|import|approve|write|add|revoke|edit|hire|terminate|upload)(_|$)",
                $"{nom}: nom yozishga o'xshaydi");
        }
    }

    [Fact]
    public async Task Overview_ishlaydi_va_jurnalga_ok_yoziladi()
    {
        var flow = new McpFlow(_host);
        var tokens = await flow.ConnectAdminAsync("Overview AI");

        var natija = await flow.CallAsync(tokens.Access, "overview");

        natija.Status.ShouldBe(HttpStatusCode.OK);
        natija.IsError.ShouldBeFalse(natija.Text);

        var tugun = McpFlow.TryParse(natija.Text);
        tugun.ShouldNotBeNull(natija.Text);
        tugun["today"]!.GetValue<string>().ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
        tugun["activeEmployees"].ShouldNotBeNull();
        tugun["thisMonth"].ShouldNotBeNull();

        // Super administratorda yollash va davomat ruxsatlari bor — bo'limlar ham chiqadi.
        tugun["recruiting"].ShouldNotBeNull();
        tugun["attendance"].ShouldNotBeNull();

        var yozuv = await _host.StoreAsync(db => db.McpAudit.AsNoTracking()
            .Where(entry => entry.ClientName == "Overview AI" && entry.Tool == "overview")
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(Ct));

        yozuv.Outcome.ShouldBe(McpAuditEntry.Ok);
        yozuv.UserName.ShouldBe("Test Administrator Testovich");
        yozuv.GrantId.ShouldNotBeNull();
        yozuv.UserId.ShouldNotBeNull();
        yozuv.Ip.ShouldBe(flow.Ip);
        yozuv.UserAgent.ShouldNotBeNullOrWhiteSpace();
        yozuv.RowCount.ShouldNotBeNull();
        yozuv.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Bolim_ruxsati_yoq_foydalanuvchi_rad_etiladi_va_jurnalga_denied_yoziladi()
    {
        var flow = new McpFlow(_host);

        // AI orqali ulanadi, lekin «Xodimlar» bo'limini ko'rmaydi.
        var (hisob, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.RecruitingView);

        var natija = await flow.CallAsync(tokens.Access, "overview");

        natija.Status.ShouldBe(HttpStatusCode.OK);
        natija.IsError.ShouldBeTrue(natija.Text);
        natija.Text.ShouldContain("Ruxsat yo'q");
        natija.Text.ShouldNotContain("activeEmployees");

        var yozuv = await _host.StoreAsync(db => db.McpAudit.AsNoTracking()
            .Where(entry => entry.UserId == hisob.UserId && entry.Tool == "overview")
            .SingleAsync(Ct));

        yozuv.Outcome.ShouldBe(McpAuditEntry.Denied);
    }

    /// <summary>
    /// Faqat «AI orqali o'qish» ruxsati bor hisob — birorta bo'limni ko'rmaydi:
    /// HAR BIR vosita uni rad etadi va jurnalga <c>denied</c> tushadi.
    /// </summary>
    [Fact]
    public async Task Faqat_mcp_connect_li_hisobga_hech_bir_vosita_malumot_bermaydi()
    {
        var flow = new McpFlow(_host);
        var (hisob, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect);
        var tools = await flow.ToolsAsync(tokens.Access);

        tools.Count.ShouldBeGreaterThan(0);

        foreach (var tool in tools)
        {
            var nom = McpFlow.NameOf(tool!);
            var (args, _) = McpFlow.BuildArgs(tool!, (_, _) => null);

            var natija = await flow.CallAsync(tokens.Access, nom, args);

            natija.Status.ShouldBe(HttpStatusCode.OK, $"{nom}: {natija.Raw}");
            natija.IsError.ShouldBeTrue($"{nom} rad etishi kerak edi: {natija.Text}");
            natija.Text.ShouldContain("Ruxsat yo'q", customMessage: $"{nom}: {natija.Text}");
        }

        var natijalar = await _host.StoreAsync(db => db.McpAudit.AsNoTracking()
            .Where(entry => entry.UserId == hisob.UserId)
            .Select(entry => new { entry.Tool, entry.Outcome })
            .ToListAsync(Ct));

        natijalar.Count.ShouldBe(tools.Count);
        natijalar.ShouldAllBe(row => row.Outcome == McpAuditEntry.Denied);
    }
}
