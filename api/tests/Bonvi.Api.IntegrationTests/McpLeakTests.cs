using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Bonvi.Api.Mcp;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// ⭐ AI ulanishning ENG MUHIM testi: hech bir vosita pasport ma'lumotini,
/// faylni (nomi, kaliti, izohi) yoki sirni chiqarmaydi (<c>docs/SPEC-mcp.md</c> §0, §4, §10).
/// </summary>
/// <remarks>
/// <para>
/// Bazaga atayin TANIB OLINADIGAN qiymatlar yoziladi — JSHSHIR, pasport seriyasi,
/// hujjat va rezyume fayl nomlari, rasm kalitlari, Telegram chat raqami — va
/// keyin <c>tools/list</c> dagi HAR BIR vosita chaqiriladi: bo'sh argument
/// bilan va (sxemasida bo'lsa) urug'langan xodim/nomzod identifikatori bilan.
/// Javobning XOM tanasida birorta qiymat uchramasligi va hech bir JSON
/// xossasi nomi «taqiqlangan» (<see cref="McpToolContext.IsForbiddenName"/>)
/// bo'lmasligi shart.
/// </para>
/// <para>
/// Chaqiruvchi — super administrator (barcha ruxsatlar, jumladan oklad):
/// eng ko'p ma'lumot ko'radigan hisob, ya'ni eng keng chiqish.
/// </para>
/// <para>
/// Vositalar ro'yxati qo'lda yozilmaydi — keyin qo'shilgan vosita ham shu
/// testdan o'tadi.
/// </para>
/// </remarks>
public sealed class McpLeakTests : IClassFixture<McpHost>
{
    private readonly McpHost _host;

    public McpLeakTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Urug'langan dunyo: identifikatorlar va hech qachon chiqmasligi kerak bo'lgan qiymatlar.</summary>
    private sealed record Dunyo(
        Guid LegalEntityId,
        Guid DepartmentId,
        Guid EmployeeId,
        string EmployeeLastName,
        Guid VacancyId,
        Guid CandidateId,
        string CandidateLastName,
        IReadOnlyDictionary<string, string> Sirlar);

    [Fact]
    public async Task Hech_bir_vosita_pasport_fayl_yoki_sirni_chiqarmaydi()
    {
        var dunyo = await UrugAsync();

        var flow = new McpFlow(_host);
        var tokens = await flow.ConnectAdminAsync();

        var sirlar = new Dictionary<string, string>(dunyo.Sirlar, StringComparer.Ordinal)
        {
            ["access token"] = tokens.Access,
            ["refresh token"] = tokens.Refresh,
        };

        // Bazadagi xeshlar ham — parol va token xeshlari hech qachon chiqmaydi.
        await using (var store = _host.Factory.Services.GetRequiredService<Bonvi.Infrastructure.Mcp.McpDatabase>().CreateStoreContext())
        {
            var parol = await store.Users.AsNoTracking().IgnoreQueryFilters()
                .Select(user => user.PasswordHash)
                .ToListAsync(Ct);

            foreach (var (xesh, i) in parol.Select((xesh, i) => (xesh, i)))
            {
                sirlar[$"parol xeshi {i}"] = xesh;
            }

            var tokenXeshlari = await store.McpTokens.AsNoTracking().Select(token => token.TokenHash).ToListAsync(Ct);

            foreach (var (xesh, i) in tokenXeshlari.Select((xesh, i) => (xesh, i)))
            {
                sirlar[$"token xeshi {i}"] = xesh;
            }
        }

        var tools = await flow.ToolsAsync(tokens.Access);
        tools.Count.ShouldBe(McpToolsTests.KoddagiNomlar().Count);

        var korilganMatnlar = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var tool in tools)
        {
            var nom = McpFlow.NameOf(tool!);

            // 1) Bo'sh argument bilan.
            var (bosh, boshTaxmin) = McpFlow.BuildArgs(tool!, (_, _) => null);
            var birinchi = await flow.CallAsync(tokens.Access, nom, bosh);
            Tekshir(nom + " {}", birinchi, sirlar);
            Yoz($"{nom} {{}} → {(birinchi.IsError ? "XATO: " + birinchi.Text : $"ok, {birinchi.Text.Length} belgi")}");

            if (boshTaxmin == 0)
            {
                // Majburiy argumenti yo'q vosita super administratorga albatta ishlaydi.
                birinchi.IsError.ShouldBeFalse($"{nom} {{}}: {birinchi.Text}");
            }

            // 2) Urug'langan identifikatorlar va qidiruv matni bilan.
            var (tanlangan, taxmin) = McpFlow.BuildArgs(tool!, (xossa, _) => Tanla(nom, xossa, dunyo));

            if (tanlangan.Count > 0)
            {
                var ikkinchi = await flow.CallAsync(tokens.Access, nom, tanlangan);
                Tekshir($"{nom} {tanlangan.ToJsonString()}", ikkinchi, sirlar);
                Yoz($"{nom} {tanlangan.ToJsonString()} → {(ikkinchi.IsError ? "XATO: " + ikkinchi.Text : $"ok, {ikkinchi.Text.Length} belgi")}");

                if (taxmin == 0)
                {
                    ikkinchi.IsError.ShouldBeFalse($"{nom} {tanlangan.ToJsonString()}: {ikkinchi.Text}");
                }

                korilganMatnlar[nom] = ikkinchi.Text;
            }
            else
            {
                korilganMatnlar[nom] = birinchi.Text;
            }
        }

        // Skan ma'nosiz bo'lmasin: xodim va nomzod kartalari URUG'LANGAN odamni ko'rsatgan
        // (ya'ni sirlar haqiqatan shu yozuvlar yonidan qidirildi).
        foreach (var (nom, ism) in new[]
                 {
                     ("get_employee", dunyo.EmployeeLastName),
                     ("search_employees", dunyo.EmployeeLastName),
                     ("get_candidate", dunyo.CandidateLastName),
                     ("search_candidates", dunyo.CandidateLastName),
                 })
        {
            korilganMatnlar.ShouldContainKey(nom);
            korilganMatnlar[nom].ShouldContain(ism, customMessage: $"{nom} urug'langan yozuvni ko'rsatmadi: {korilganMatnlar[nom]}");
        }
    }

    // ────────────────────────────────────────────────────────────

    /// <summary>Qaysi vosita qanday chaqirilgani — test chiqishida ko'rinadi.</summary>
    private static void Yoz(string satr) => TestContext.Current.TestOutputHelper?.WriteLine(satr);

    private static void Tekshir(string qayer, McpCallResult natija, IReadOnlyDictionary<string, string> sirlar)
    {
        natija.Status.ShouldBe(HttpStatusCode.OK, $"{qayer}: {natija.Raw}");

        foreach (var (nima, qiymat) in sirlar)
        {
            natija.Raw.ShouldNotContain(qiymat, Case.Insensitive, $"{qayer}: «{nima}» sizib chiqdi");
            natija.Text.ShouldNotContain(qiymat, Case.Insensitive, $"{qayer}: «{nima}» sizib chiqdi");
        }

        foreach (var xossa in McpFlow.PropertyNames(McpFlow.TryParse(natija.Text)))
        {
            McpToolContext.IsForbiddenName(xossa).ShouldBeFalse($"{qayer}: taqiqlangan xossa «{xossa}»");
        }
    }

    /// <summary>Sxemadagi xossaga urug'langan qiymat: identifikator yoki qidiruv matni.</summary>
    private static JsonValue? Tanla(string vosita, string xossa, Dunyo dunyo)
    {
        var n = xossa.ToLowerInvariant().Replace("_", string.Empty, StringComparison.Ordinal);
        var nomzodVositasi = vosita.Contains("candidate", StringComparison.Ordinal);

        Guid? id = n switch
        {
            // «…Id» — identifikator; «employee», «department» kabi satr — «identifikator yoki nomi».
            "employeeid" or "employee" => dunyo.EmployeeId,
            "candidateid" or "candidate" => dunyo.CandidateId,
            "vacancyid" or "vacancy" => dunyo.VacancyId,
            "legalentityid" or "legalentity" => dunyo.LegalEntityId,
            "departmentid" or "department" => dunyo.DepartmentId,
            // `blank` — blanka identifikatori; bu dunyoda blanka yo'q (blankalar sizmasligi
            // o'z urug'i bilan BlankMcpToolsTests da tekshiriladi), shuning uchun taxmin qilinadi.
            // `process_case` — the same: its own seed is in ProcessMcpToolsTests.
            "id" when vosita is "blank" or "process_case" => null,
            "id" => nomzodVositasi ? dunyo.CandidateId
                : vosita.Contains("vacanc", StringComparison.Ordinal) ? dunyo.VacancyId
                : dunyo.EmployeeId,
            _ => null,
        };

        if (id is { } qiymat)
        {
            return JsonValue.Create(qiymat.ToString());
        }

        if (n is "text" or "query" or "search" or "q")
        {
            return JsonValue.Create(nomzodVositasi ? dunyo.CandidateLastName : dunyo.EmployeeLastName);
        }

        return null;
    }

    /// <summary>
    /// Bitta xodim (JSHSHIR, pasport, telefon, manzil, rasm, hujjat, tekshiruv fayli
    /// bilan) va bitta nomzod (rasm, Telegram, rezyume fayli bilan).
    /// </summary>
    private async Task<Dunyo> UrugAsync()
    {
        var belgi = McpHost.Belgi();
        var raqam = Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

        // Har bir sir o'z tasodifiy qismi bilan — xodim nomida uchramasin.
        string Sir(string asos) => $"{asos}{McpHost.Belgi()}";

        var jshshir = "3150588" + raqam;
        var pasport = "KX9" + Random.Shared.Next(100_000, 999_999).ToString(CultureInfo.InvariantCulture);
        var xodimRasmi = Sir("mcpsir/xodim-rasm-") + ".jpg";
        var hujjatKaliti = Sir("mcpsir/hujjat-") + ".pdf";
        var hujjatNomi = Sir("MaxfiyHujjat_");
        var hujjatIzohi = Sir("MaxfiyIzoh_");
        var tekshiruvKaliti = Sir("mcpsir/tekshiruv-") + ".pdf";
        var tekshiruvNomi = Sir("SudlanganlikMalumotnomasi_");
        var nomzodRasmi = Sir("mcpsir/nomzod-rasm-") + ".jpg";
        var rezyumeKaliti = Sir("mcpsir/rezyume-") + ".pdf";
        var rezyumeNomi = Sir("Rezyume_");
        var telegram = 7_000_000_000L + Random.Shared.Next(100_000_000, 999_999_999);

        var xodimFamiliyasi = "Сирсизов" + belgi;
        var nomzodFamiliyasi = "Номзодов" + belgi;

        await using var qamrov = _host.Factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("MCP sinov MChJ " + belgi).Value;
        var bolim = Department.Create("MCP bo'limi " + belgi).Value;

        var xodim = Employee.Create(
            FullName.Create(xodimFamiliyasi, "Ходим", "Тестович").Value,
            new DateOnly(1988, 5, 15)).Value;

        xodim.SetPinfl(Pinfl.FromTrusted(jshshir)).IsSuccess.ShouldBeTrue();
        xodim.SetPassport(PassportSeries.Create(pasport).Value);
        xodim.SetPhones(PhoneNumber.Create("931112233").Value, PhoneNumber.Create("+998 94 222 33 44").Value);
        xodim.SetAddress("Toshkent, Chilonzor 1-kvartal");
        xodim.SetPhoto(xodimRasmi);

        var ishga = Employment.Hire(xodim.Id, firma.Id, McpToolContext.Today.AddDays(-40), bolim.Id).Value;

        var turi = DocumentType.Create("MCP hujjat turi " + belgi).Value;
        var hujjat = EmployeeDocument.Attach(xodim.Id, turi.Id, hujjatKaliti, hujjatNomi + ".pdf", "application/pdf", 1234, hujjatIzohi).Value;
        var tekshiruv = EmployeeCheckFile.Attach(xodim.Id, tekshiruvKaliti, tekshiruvNomi + ".pdf", "application/pdf", 2345).Value;

        var vakansiya = Vacancy.Open("MCP vakansiya " + belgi, McpToolContext.Today.AddDays(-10), 2).Value;
        var nomzod = Candidate.Register(
            FullName.Create(nomzodFamiliyasi, "Номзод", null).Value,
            vakansiya.Id,
            CandidateSource.Manual,
            new DateOnly(1995, 3, 1),
            PhoneNumber.Create("935556677").Value,
            "Samarqand",
            "Sotuvchi",
            "Nomzod izohi").Value;

        nomzod.SetPhoto(nomzodRasmi);
        nomzod.SetTelegram(telegram, "uz");
        nomzod.AttachFile(rezyumeKaliti, rezyumeNomi + ".pdf", "application/pdf", 3456).IsSuccess.ShouldBeTrue();

        var bosqich = await kontekst.PipelineStages.AsNoTracking().Select(stage => (Guid?)stage.Id).FirstOrDefaultAsync(Ct);

        if (bosqich is { } bosqichId)
        {
            nomzod.RecordStage(bosqichId, McpToolContext.Today.AddDays(-2), "Suhbat yaxshi o'tdi").IsSuccess.ShouldBeTrue();
        }

        kontekst.AddRange(firma, bolim, xodim, turi, vakansiya);
        kontekst.AddRange(ishga, hujjat, tekshiruv, nomzod);
        await kontekst.SaveChangesAsync(Ct);

        return new Dunyo(
            firma.Id,
            bolim.Id,
            xodim.Id,
            xodimFamiliyasi,
            vakansiya.Id,
            nomzod.Id,
            nomzodFamiliyasi,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["JSHSHIR"] = jshshir,
                ["pasport"] = pasport,
                ["pasport raqami"] = pasport[2..],
                ["xodim rasmi kaliti"] = xodimRasmi,
                ["hujjat kaliti"] = hujjatKaliti,
                ["hujjat nomi"] = hujjatNomi,
                ["hujjat izohi"] = hujjatIzohi,
                ["tekshiruv fayli kaliti"] = tekshiruvKaliti,
                ["tekshiruv fayli nomi"] = tekshiruvNomi,
                ["nomzod rasmi kaliti"] = nomzodRasmi,
                ["rezyume kaliti"] = rezyumeKaliti,
                ["rezyume nomi"] = rezyumeNomi,
                ["Telegram chat"] = telegram.ToString(CultureInfo.InvariantCulture),
                ["administrator logini"] = BonviApiFactory.TestAdminLogin,
            });
    }
}
