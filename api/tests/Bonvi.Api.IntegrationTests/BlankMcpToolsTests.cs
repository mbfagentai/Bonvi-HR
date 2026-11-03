using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Blankalar MCP vositalari (<c>docs/SPEC-blankalar.md</c> §11): <c>blank_templates</c>, <c>blanks</c>,
/// <c>blank</c>, <c>blank_gaps</c> — urug' to'g'ridan-to'g'ri <see cref="AppDbContext"/> orqali.
/// </summary>
/// <remarks>
/// Har test o'z bo'limini ochadi va so'rovlarni shu bo'lim bilan cheklaydi — boshqa
/// testlarning yozuvlari natijaga aralashmaydi. Shablon yozuvlari (kod bo'yicha yagona)
/// testlar orasida bo'lishiladi.
/// </remarks>
public sealed class BlankMcpToolsTests : IClassFixture<McpHost>
{
    private const string F07Definition = """
        {"schema":1,"code":"HR-F07","version":"1.0","title":"Sinov bahosi","subject":"employee",
         "sections":[
          {"id":"hdr","title":"Xodim","fields":[
            {"id":"employee","type":"ref","refKind":"employee","label":"Xodim F.I.Sh."},
            {"id":"mentor","type":"ref","refKind":"employee","label":"Ustoz","required":true}]},
          {"id":"eval","title":"Baholash","fields":[
            {"id":"intro","type":"static","label":"Izoh","text":"Mezonlar bo'yicha baholang"},
            {"id":"criteria","type":"table","label":"Mezon","columns":[
              {"id":"score","type":"score","label":"Baho","required":true},
              {"id":"note","type":"text","label":"Izoh"}],
             "fixedRows":[{"id":"c1","label":"Ish sifati"},{"id":"c2","label":"Intizom"}]},
            {"id":"avgScore","type":"computed","label":"O'rtacha ball","valueType":"number",
             "expr":{"op":"avg","args":[{"ref":"criteria[*].score"}]}},
            {"id":"evidence","type":"file","label":"Dalillar"},
            {"id":"conclusion","type":"choice","label":"Rahbar xulosasi","required":true,"options":[
              {"value":"passed","label":"O'tdi"},
              {"value":"conditional","label":"Shartli","extra":{"type":"longtext","label":"Reja","required":true}}]}]}],
         "steps":[
          {"id":"signHead","title":"Bo'lim rahbari","order":1,"actor":"departmentHead","mode":"either",
           "decision":{"options":[{"value":"agree","label":"Roziman"},{"value":"object","label":"E'tirozim bor"}],"required":true}},
          {"id":"signHr","title":"HR","order":2,"actor":"hr1","mode":"electronic"}]}
        """;

    private static int _raqam = Random.Shared.Next(100_000, 900_000);

    private readonly McpHost _host;

    public BlankMcpToolsTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Bugun => McpToolContext.Today;

    /// <summary>Urug'langan dunyo: bo'lim, to'rt xodim, ikki blanka va hech qachon chiqmasligi kerak bo'lgan qiymatlar.</summary>
    private sealed record Dunyo(
        Guid BolimId,
        Guid SinovdagiId,
        Guid KetganId,
        Guid YangiId,
        Guid BaholanganId,
        string BaholanganFamiliya,
        Guid F07Id,
        Guid F06Id,
        IReadOnlyList<string> Sirlar);

    [Fact]
    public async Task Shablonlar_maydon_va_qadamlari_bilan_chiqadi()
    {
        await UrugAsync();
        var flow = new McpFlow(_host);
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.BlanksView);

        var natija = await flow.CallAsync(tokens.Access, "blank_templates");

        natija.IsError.ShouldBeFalse(natija.Text);
        var f07 = Json(natija.Text)["items"]!.AsArray().Single(item => item!["code"]!.GetValue<string>() == "HR-F07")!;

        f07["version"]!.GetValue<string>().ShouldBe("1.0");
        f07["subject"]!.GetValue<string>().ShouldBe("Employee");

        var maydonlar = f07["fields"]!.AsArray();
        maydonlar.Select(field => field!["id"]!.GetValue<string>())
            .ShouldBe(["employee", "mentor", "criteria", "avgScore", "evidence", "conclusion"], "static maydon chiqmaydi");

        var jadval = maydonlar.Single(field => field!["id"]!.GetValue<string>() == "criteria")!;
        jadval["section"]!.GetValue<string>().ShouldBe("Baholash");
        jadval["columns"]!.AsArray().Select(column => column!["label"]!.GetValue<string>()).ShouldBe(["Baho", "Izoh"]);

        maydonlar.Single(field => field!["id"]!.GetValue<string>() == "mentor")!["required"]!.GetValue<bool>().ShouldBeTrue();
        f07["steps"]!.AsArray().Select(step => step!["actor"]!.GetValue<string>()).ShouldBe(["departmentHead", "hr1"]);
    }

    [Fact]
    public async Task Royxat_filtrlar_va_joriy_qadam_bilan_ishlaydi()
    {
        var dunyo = await UrugAsync();
        var flow = new McpFlow(_host);
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.BlanksView);

        var hammasi = Json((await flow.CallAsync(tokens.Access, "blanks", new { departmentId = dunyo.BolimId })).Text);
        hammasi["total"]!.GetValue<int>().ShouldBe(2);

        var f07 = Json((await flow.CallAsync(tokens.Access, "blanks", new { departmentId = dunyo.BolimId, templateCode = "hr-f07" })).Text);
        var qator = f07["items"]!.AsArray().ShouldHaveSingleItem()!;
        qator["id"]!.GetValue<Guid>().ShouldBe(dunyo.F07Id);
        qator["status"]!.GetValue<string>().ShouldBe("Approved");
        qator["templateTitle"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        qator["subject"]!["fullName"]!.GetValue<string>().ShouldContain(dunyo.BaholanganFamiliya);
        qator["missingCount"]!.GetValue<int>().ShouldBe(1);
        qator["completedAt"]!.GetValue<string>().ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$");

        var muddatiOtgan = Json((await flow.CallAsync(tokens.Access, "blanks", new { departmentId = dunyo.BolimId, overdue = true })).Text);
        var f06 = muddatiOtgan["items"]!.AsArray().ShouldHaveSingleItem()!;
        f06["id"]!.GetValue<Guid>().ShouldBe(dunyo.F06Id);
        f06["overdue"]!.GetValue<bool>().ShouldBeTrue();
        f06["dueDate"]!.GetValue<string>().ShouldBe(Bugun.AddDays(-3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        var chala = Json((await flow.CallAsync(tokens.Access, "blanks", new { departmentId = dunyo.BolimId, incomplete = true })).Text);
        chala["items"]!.AsArray().ShouldHaveSingleItem()!["id"]!.GetValue<Guid>().ShouldBe(dunyo.F07Id);

        var xodim = Json((await flow.CallAsync(tokens.Access, "blanks", new { employeeId = dunyo.YangiId })).Text);
        xodim["items"]!.AsArray().ShouldHaveSingleItem()!["id"]!.GetValue<Guid>().ShouldBe(dunyo.F06Id);

        var notogri = await flow.CallAsync(tokens.Access, "blanks", new { status = "Yozilgan" });
        notogri.IsError.ShouldBeTrue(notogri.Text);
        notogri.Text.ShouldContain("InApproval");
    }

    [Fact]
    public async Task Blanka_javoblari_yorliqlangan_fayl_faqat_soni_bilan()
    {
        var dunyo = await UrugAsync();
        var flow = new McpFlow(_host);
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.BlanksView);

        var natija = await flow.CallAsync(tokens.Access, "blank", new { id = dunyo.F07Id.ToString() });

        natija.IsError.ShouldBeFalse(natija.Text);
        var blanka = Json(natija.Text);

        blanka["status"]!.GetValue<string>().ShouldBe("Approved");
        blanka["templateVersion"]!.GetValue<string>().ShouldBe("1.0");

        var javoblar = blanka["answers"]!.AsArray();
        JsonNode Javob(string id) => javoblar.Single(item => item!["field"]!.GetValue<string>() == id)!;

        Javob("mentor")["value"]!.GetValue<string>().ShouldBe("Ustozov Ustoz", "havola — ism");
        Javob("mentor")["section"]!.GetValue<string>().ShouldBe("Xodim");
        Javob("conclusion")["label"]!.GetValue<string>().ShouldBe("Rahbar xulosasi");
        Javob("conclusion")["value"]!.GetValue<string>().ShouldBe("Shartli (Reja: Excel kursi)");

        var fayl = Javob("evidence")["value"]!;
        fayl["attachments"]!.GetValue<int>().ShouldBe(1);
        fayl["kinds"]!.AsArray().Select(kind => kind!.GetValue<string>()).ShouldBe(["Pdf"]);

        var qatorlar = Javob("criteria")["value"]!.AsArray();
        qatorlar.Count.ShouldBe(2);
        qatorlar[0]!["row"]!.GetValue<string>().ShouldBe("Ish sifati");
        qatorlar[0]!["cells"]!.AsArray().Select(cell => cell!["label"]!.GetValue<string>()).ShouldBe(["Baho", "Izoh"]);
        qatorlar[1]!["row"]!.GetValue<string>().ShouldBe("Intizom");

        blanka["computed"]!.AsArray().ShouldHaveSingleItem()!["label"]!.GetValue<string>().ShouldBe("O'rtacha ball");

        var yoq = blanka["missing"]!.AsArray().ShouldHaveSingleItem()!;
        yoq["label"]!.GetValue<string>().ShouldBe("Mezon — 2-qator: Baho");

        var qadamlar = blanka["steps"]!.AsArray();
        qadamlar.Count.ShouldBe(2);
        qadamlar[0]!["decision"]!.GetValue<string>().ShouldBe("Roziman");
        qadamlar[0]!["signerName"]!.GetValue<string>().ShouldBe("Rahbarov Rahbar");
        qadamlar[0]!["method"]!.GetValue<string>().ShouldBe("InPerson");
        qadamlar[0]!["state"]!.GetValue<string>().ShouldBe("Signed");
        qadamlar[1]!["method"]!.GetValue<string>().ShouldBe("Electronic");

        var topilmadi = await flow.CallAsync(tokens.Access, "blank", new { id = Guid.NewGuid().ToString() });
        topilmadi.IsError.ShouldBeTrue(topilmadi.Text);
        topilmadi.Text.ShouldContain("topilmadi");
    }

    [Fact]
    public async Task Boshliqlar_qoidalar_boyicha_topiladi()
    {
        var dunyo = await UrugAsync();
        var flow = new McpFlow(_host);
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.BlanksView);

        var natija = await flow.CallAsync(
            tokens.Access,
            "blank_gaps",
            new { departmentId = dunyo.BolimId, from = Iso(Bugun.AddDays(-100)), to = Iso(Bugun) });

        natija.IsError.ShouldBeFalse(natija.Text);
        var qoidalar = Json(natija.Text)["rules"]!.AsArray();

        JsonArray Qoida(string nom) =>
            qoidalar.Single(item => item!["rule"]!.GetValue<string>() == nom)!["items"]!.AsArray();

        IEnumerable<Guid> Xodimlar(string nom) => Qoida(nom).Select(item => item!["employeeId"]!.GetValue<Guid>());

        IEnumerable<Guid> Blankalar(string nom) => Qoida(nom).Select(item => item!["id"]!.GetValue<Guid>());

        Xodimlar("hired_without_f06").ShouldBe([dunyo.SinovdagiId], "yangisida HR-F06 bor, ketgan va baholangan — oraliqdan tashqarida");
        Xodimlar("probation_ending_without_f07").ShouldBe([dunyo.SinovdagiId], "baholanganda HR-F07 bor");
        Qoida("probation_ending_without_f07").Single()!["daysLeft"]!.GetValue<int>().ShouldBeInRange(0, 5);
        Xodimlar("terminated_without_f08").ShouldBe([dunyo.KetganId]);
        Xodimlar("terminated_without_approved_f09").ShouldBe([dunyo.KetganId]);
        Blankalar("f07_approved_probation_open").ShouldBe([dunyo.F07Id]);
        Blankalar("overdue").ShouldBe([dunyo.F06Id]);
        Blankalar("incomplete_approved").ShouldBe([dunyo.F07Id]);
    }

    [Fact]
    public async Task Hech_bir_blanka_vositasi_fayl_kaliti_nomi_yoki_imzoni_chiqarmaydi()
    {
        var dunyo = await UrugAsync();
        var flow = new McpFlow(_host);
        var tokens = await flow.ConnectAdminAsync();

        var chaqiruvlar = new (string Tool, object Args)[]
        {
            ("blank_templates", new { }),
            ("blanks", new { departmentId = dunyo.BolimId }),
            ("blank", new { id = dunyo.F07Id.ToString() }),
            ("blank", new { id = dunyo.F06Id.ToString() }),
            ("blank_gaps", new { departmentId = dunyo.BolimId, from = Iso(Bugun.AddDays(-100)) }),
        };

        foreach (var (vosita, args) in chaqiruvlar)
        {
            var natija = await flow.CallAsync(tokens.Access, vosita, args);

            natija.IsError.ShouldBeFalse($"{vosita}: {natija.Text}");
            TekshirSizmaydi(vosita, natija.Raw, dunyo.Sirlar);
            TaqiqlanganNomYoq(vosita, Json(natija.Text));
        }
    }

    [Fact]
    public async Task Toliq_kalit_blankalarni_oqiydi_lekin_fayl_va_imzosiz()
    {
        var dunyo = await UrugAsync();
        var kalit = await KalitAsync("FullRead");
        using var mijoz = _host.Client(McpFlow.RandomIp());
        mijoz.DefaultRequestHeaders.Add("X-API-Key", kalit.Key);

        foreach (var yol in new[]
                 {
                     "/api/public/v1/full/blank_templates",
                     $"/api/public/v1/full/blanks?departmentId={dunyo.BolimId}&incomplete=true",
                     $"/api/public/v1/full/blank?id={dunyo.F07Id}",
                     $"/api/public/v1/full/blank_gaps?departmentId={dunyo.BolimId}&from={Iso(Bugun.AddDays(-100))}",
                 })
        {
            using var javob = await mijoz.GetAsync(new Uri(yol, UriKind.Relative), Ct);
            var matn = await javob.Content.ReadAsStringAsync(Ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.OK, $"{yol}: {matn}");
            TekshirSizmaydi(yol, matn, dunyo.Sirlar);
        }

        using var karta = await mijoz.GetAsync(new Uri($"/api/public/v1/full/blank?id={dunyo.F07Id}", UriKind.Relative), Ct);
        var kartaMatni = await karta.Content.ReadAsStringAsync(Ct);
        kartaMatni.ShouldContain("Ustozov Ustoz");
        kartaMatni.ShouldContain(dunyo.BaholanganFamiliya);
    }

    [Fact]
    public async Task Blanka_ruxsati_yoq_foydalanuvchi_rad_etiladi()
    {
        var dunyo = await UrugAsync();
        var flow = new McpFlow(_host);

        // Xodimlarni ko'radi, lekin «Blankalar» bo'limini — yo'q.
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);

        var chaqiruvlar = new (string Tool, object Args)[]
        {
            ("blank_templates", new { }),
            ("blanks", new { }),
            ("blank", new { id = dunyo.F07Id.ToString() }),
            ("blank_gaps", new { }),
        };

        foreach (var (vosita, args) in chaqiruvlar)
        {
            var natija = await flow.CallAsync(tokens.Access, vosita, args);

            natija.IsError.ShouldBeTrue($"{vosita} rad etishi kerak edi: {natija.Text}");
            natija.Text.ShouldContain("Ruxsat yo'q", customMessage: vosita);
            natija.Text.ShouldNotContain("Ustozov", customMessage: vosita);
        }
    }

    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bo'lim + to'rt xodim: sinovi tugayotgan (F06 ham, F07 ham yo'q), oraliqda ketgan (F08/F09 yo'q),
    /// yangi (F06 qoralamasi — muddati o'tgan), baholangan (F07 tasdiqlangan, chala, sinov yopilmagan).
    /// </summary>
    private async Task<Dunyo> UrugAsync()
    {
        var belgi = McpHost.Belgi();
        var faylNomi = $"maxfiy-dalolatnoma-{belgi}.pdf";
        var faylKaliti = $"blanks/{belgi}/attachment-store.pdf";
        var imzoKaliti = $"blanks/{belgi}/signature-store.png";
        var pdfKaliti = $"blanks/{belgi}/final-store.pdf";
        var familiya = "Баҳоланган" + belgi;

        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var foydalanuvchi = await db.Users.IgnoreQueryFilters().Select(user => user.Id).FirstAsync(Ct);

        var (f06Shablon, f06Versiya) = await ShablonAsync(db, "HR-F06", Oddiy("HR-F06"));
        var (f07Shablon, f07Versiya) = await ShablonAsync(db, "HR-F07", F07Definition);
        await ShablonAsync(db, "HR-F08", Oddiy("HR-F08"));
        await ShablonAsync(db, "HR-F09", Oddiy("HR-F09"));

        var firma = LegalEntity.Create("Blanka sinov MChJ " + belgi).Value;
        var bolim = Department.Create("Blanka bo'limi " + belgi).Value;
        db.AddRange(firma, bolim);

        Employee Xodim(string ism) => Employee.Create(FullName.Create(ism, "Ходим", "Тестович").Value, new DateOnly(1990, 1, 1)).Value;

        var sinovdagi = Xodim("Синовдаги" + belgi);
        var ketgan = Xodim("Кетган" + belgi);
        var yangi = Xodim("Янги" + belgi);
        var baholangan = Xodim(familiya);
        db.AddRange(sinovdagi, ketgan, yangi, baholangan);

        var sinovdagiIsh = Employment.Hire(sinovdagi.Id, firma.Id, Bugun.AddDays(-87), bolim.Id).Value;
        var ketganIsh = Employment.Hire(ketgan.Id, firma.Id, Bugun.AddDays(-200), bolim.Id).Value;
        ketganIsh.Terminate(Bugun.AddDays(-5), reasonId: null).IsSuccess.ShouldBeTrue();
        var yangiIsh = Employment.Hire(yangi.Id, firma.Id, Bugun.AddDays(-20), bolim.Id).Value;
        var baholanganIsh = Employment.Hire(baholangan.Id, firma.Id, Bugun.AddDays(-150), bolim.Id).Value;
        db.AddRange(sinovdagiIsh, ketganIsh, yangiIsh, baholanganIsh);

        db.AddRange(
            Probation.Start(sinovdagiIsh.Id, Bugun.AddDays(-87)).Value,
            Probation.Start(baholanganIsh.Id, Bugun.AddDays(-150)).Value);

        // Yangi xodim: HR-F06 qoralamasi, muddati 3 kun oldin o'tgan.
        var f06 = BlankSubmission.Create(
            f06Shablon,
            f06Versiya,
            new BlankSubject(yangi.Id, yangiIsh.Id, DepartmentId: bolim.Id),
            new BlankAnswersState("""{"note":"Reja tuzilmoqda"}""", "{}", "[]", 0, Bugun.AddDays(-3))).Value;

        // Baholangan xodim: HR-F07 — ikki qadam imzolangan, tasdiqlangan, lekin bitta majburiy katak bo'sh.
        var javoblar = new JsonObject
        {
            ["employee"] = new JsonObject { ["id"] = baholangan.Id.ToString(), ["label"] = familiya + " Ходим" },
            ["mentor"] = new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["label"] = "Ustozov Ustoz" },
            ["criteria"] = new JsonArray(
                new JsonObject { ["_row"] = "c1", ["score"] = 4, ["note"] = "Yaxshi ishlaydi" },
                new JsonObject { ["_row"] = "c2", ["note"] = "Ba'zan kechikadi" }),
            ["conclusion"] = new JsonObject { ["value"] = "conditional", ["extra"] = "Excel kursi" },
            ["evidence"] = new JsonArray(Guid.NewGuid().ToString()),
        };

        var holat = new BlankAnswersState(javoblar.ToJsonString(), """{"avgScore":4}""", """["criteria[1].score"]""", 1, Bugun.AddDays(-60));

        var f07 = BlankSubmission.Create(
            f07Shablon,
            f07Versiya,
            new BlankSubject(baholangan.Id, baholanganIsh.Id, DepartmentId: bolim.Id),
            holat).Value;

        f07.AddAttachment(
                new BlankAttachmentFile("evidence", BlankAttachmentKind.Pdf, faylNomi, "application/pdf", 1024, new string('a', 64), faylKaliti),
                foydalanuvchi,
                DateTimeOffset.UtcNow)
            .IsSuccess.ShouldBeTrue();

        var vaqt = DateTimeOffset.UtcNow;
        f07.Submit(
                Interlocked.Increment(ref _raqam),
                [
                    new BlankStepPlan("signHead", 1, "Bo'lim rahbari", BlankActorRoles.DepartmentHead, BlankStepMode.Either),
                    new BlankStepPlan("signHr", 2, "HR", BlankActorRoles.Hr1, BlankStepMode.Electronic),
                ],
                holat,
                vaqt)
            .IsSuccess.ShouldBeTrue();

        Guid Qadam(string kod) => f07.Steps.Single(step => step.StepCode == kod).Id;

        f07.SignStep(
                Qadam("signHead"),
                new BlankStepAct(BlankSignMethod.InPerson, foydalanuvchi, SignerName: "Rahbarov Rahbar", SignatureStorageKey: imzoKaliti, Decision: "agree", Comment: "Rozi"),
                null,
                vaqt)
            .IsSuccess.ShouldBeTrue();
        f07.SignStep(Qadam("signHr"), new BlankStepAct(BlankSignMethod.Electronic, foydalanuvchi), null, vaqt).IsSuccess.ShouldBeTrue();
        f07.Status.ShouldBe(BlankSubmissionStatus.Approved);
        f07.SetFinalPdf(pdfKaliti).IsSuccess.ShouldBeTrue();

        db.AddRange(f06, f07);
        await db.SaveChangesAsync(Ct);

        return new Dunyo(
            bolim.Id,
            sinovdagi.Id,
            ketgan.Id,
            yangi.Id,
            baholangan.Id,
            familiya,
            f07.Id,
            f06.Id,
            [faylNomi, faylKaliti, imzoKaliti, pdfKaliti, $"dalolatnoma-{belgi}", "attachment-store", "signature-store", "final-store", "answersPatch"]);
    }

    /// <summary>Shablon kod bo'yicha yagona — bor bo'lsa o'shani oladi, yo'q bo'lsa yaratib e'lon qiladi.</summary>
    private static async Task<(BlankTemplate Template, BlankTemplateVersion Version)> ShablonAsync(
        AppDbContext db,
        string code,
        string definition)
    {
        var template = await db.BlankTemplates.FirstOrDefaultAsync(item => item.Code == code, Ct);

        if (template is null)
        {
            template = BlankTemplate.Create(code, "Sinov " + code, BlankSubjectKind.Employee).Value;
            db.Add(template);
        }

        template.Activate();

        var version = await db.BlankTemplateVersions
            .FirstOrDefaultAsync(item => item.TemplateId == template.Id && item.Status == BlankTemplateVersionStatus.Published, Ct);

        if (version is null)
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definition)));
            version = BlankTemplateVersion.Create(template.Id, "1.0", definition, sha, BlankTemplateSource.Seed).Value;
            version.Publish(null, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
            db.Add(version);
        }

        await db.SaveChangesAsync(Ct);
        return (template, version);
    }

    private static string Oddiy(string code) =>
        $$"""
        {"schema":1,"code":"{{code}}","version":"1.0","title":"Sinov","subject":"employee",
         "sections":[{"id":"s","title":"Asosiy","fields":[{"id":"note","type":"text","label":"Izoh"}]}],"steps":[]}
        """;

    private static void TekshirSizmaydi(string qayer, string matn, IReadOnlyList<string> sirlar)
    {
        foreach (var sir in sirlar)
        {
            matn.ShouldNotContain(sir, Case.Insensitive, $"{qayer}: «{sir}» chiqmasligi kerak");
        }
    }

    private static void TaqiqlanganNomYoq(string qayer, JsonNode? tugun)
    {
        switch (tugun)
        {
            case JsonObject obyekt:
                foreach (var (nom, qiymat) in obyekt)
                {
                    McpToolContext.IsForbiddenName(nom).ShouldBeFalse($"{qayer}: «{nom}» xossasi chiqmasligi kerak");
                    TaqiqlanganNomYoq(qayer, qiymat);
                }

                break;
            case JsonArray massiv:
                foreach (var element in massiv)
                {
                    TaqiqlanganNomYoq(qayer, element);
                }

                break;
        }
    }

    private async Task<ScopedKeyResponse> KalitAsync(string scope)
    {
        using var admin = await _host.AdminClientAsync();
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = PublicApiHarness.NoyobNom("Blanka"), scope },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        var kalit = await javob.Content.ReadFromJsonAsync<ScopedKeyResponse>(Ct);
        kalit.ShouldNotBeNull();
        return kalit;
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static JsonNode Json(string text) => JsonNode.Parse(text) ?? new JsonObject();
}
