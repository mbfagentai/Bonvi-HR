using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Api.Common;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HR ning HRMentor dagi anketa matnlari onlayn anketada va PDF'da (SPEC-blankalar-hrmentor §5):
/// seans faqat ulanish yoqilganda va faqat anketa matnlarini beradi, yuborilgan anketa qaysi
/// reviziya bilan to'ldirilganini saqlaydi, PDF keyin ham o'sha so'zlar bilan qayta chiziladi.
/// </summary>
/// <remarks>Sinf o'z bazasida — ulanish sozlamasi va reviziyalar boshqa testlarga tegmaydi.</remarks>
public sealed class HrMentorTextsApiTests : IClassFixture<BonviApiFactory>
{
    private const string SessionPath = "/api/kiosk/questionnaire/session";
    private const string SubmitPath = "/api/kiosk/questionnaire";

    private static int _ipSanogi;
    private static int _telefonSanogi;

    private readonly BonviApiFactory _factory;

    public HrMentorTextsApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Bugun => _factory.Services.GetRequiredService<IClock>().Today;

    [Fact]
    public async Task Matnlar_seansga_ulanish_yoqilganda_va_reviziya_bilan_keladi_yuborish_ularni_saqlaydi()
    {
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        using var mehmon = Mehmon();

        // ── Reviziya bor, lekin ulanish o'chiq — o'rnatilgan matnlar
        await ReviziyaAsync(1, "1. NOMZOD HAQIDA (R1)", "KOMPAS-3D");
        (await SeansAsync(mehmon)).TryGetPropertyValue("texts", out var yoq).ShouldBeTrue();
        yoq.ShouldBeNull("ulanish o'chiq — HR matnlari berilmaydi");

        await SozlaAsync(admin, "https://hrmentor.test", "hrm_" + new string('k', 43));

        // ── Ulanish yoqildi — oxirgi reviziya; PDF matnlari nomzodga ketmaydi
        var seans = await SeansAsync(mehmon);
        var matnlar = seans["texts"]!.AsObject();
        matnlar["revision"]!.GetValue<int>().ShouldBe(1);
        matnlar["strings"]![FormTexts.Latin]!["kiosk.start.title"]!.GetValue<string>().ShouldBe("Anketa (R1)");
        matnlar.ToJsonString().ShouldNotContain("\"pdf.", Case.Sensitive, "blanka matnlari faqat serverda");
        matnlar.ToJsonString().ShouldNotContain("NOMZOD HAQIDA");
        matnlar["lists"]![HrF02TextCatalog.ComputerList]!["options"]!.AsArray()
            .Select(option => option!["value"]!.GetValue<string>())
            .ShouldContain("x-kompas");

        // ── HR qo'shgan dastur bilan, 1-reviziyada to'ldirildi
        var birinchi = await YuborAsync(mehmon, "1", new Dictionary<string, int> { ["x-kompas"] = 4, ["windows"] = 3 });
        birinchi.TextsRevision.ShouldBe(1);

        // ── HR matnni yana o'zgartirdi — eski anketa PDF'i baribir 1-reviziya so'zlari bilan
        await ReviziyaAsync(2, "1. NOMZOD HAQIDA (R2)", "KOMPAS 3D v22");

        var eskiPdf = await PdfMatniAsync(birinchi);
        eskiPdf.ShouldContain("1. NOMZOD HAQIDA (R1)");
        eskiPdf.ShouldNotContain("(R2)");
        eskiPdf.ShouldContain("KOMPAS-3D 4", Case.Sensitive);

        // ── Reviziyasiz (eski planshet) va bazada yo'q reviziya — amaldagisi
        (await YuborAsync(mehmon, null, new Dictionary<string, int> { ["windows"] = 2 })).TextsRevision.ShouldBe(2);
        (await YuborAsync(mehmon, "99", new Dictionary<string, int> { ["windows"] = 2 })).TextsRevision.ShouldBe(2);

        var ikkinchi = await YuborAsync(mehmon, "2", new Dictionary<string, int> { ["x-kompas"] = 2 });
        var yangiPdf = await PdfMatniAsync(ikkinchi);
        yangiPdf.ShouldContain("1. NOMZOD HAQIDA (R2)");
        yangiPdf.ShouldContain("KOMPAS 3D v22 2", Case.Sensitive);

        // ── Ro'yxatda yo'q dastur — rad etiladi
        using (var notanish = await YuborishAsync(mehmon, "2", new Dictionary<string, int> { ["x-notanish"] = 3 }))
        {
            notanish.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await notanish.Content.ReadAsStringAsync(Ct));
        }

        // ── Ulanish o'chirildi — seans yana o'rnatilgan matnlar bilan, eski PDF esa o'z reviziyasida
        await SozlaAsync(admin, string.Empty, string.Empty);

        (await SeansAsync(mehmon))["texts"].ShouldBeNull();
        (await YuborAsync(mehmon, null, new Dictionary<string, int> { ["windows"] = 1 })).TextsRevision.ShouldBeNull();
        (await PdfMatniAsync(birinchi)).ShouldContain("1. NOMZOD HAQIDA (R1)", Case.Sensitive, "PDF — anketa to'ldirilgan so'zlar bilan");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Sinxronlash saqlaganidek reviziya: katalogdan o'tgan (<see cref="HrF02TextCatalog.Shape"/>) payload.</summary>
    private async Task ReviziyaAsync(int revision, string sarlavha, string dastur)
    {
        var served = new JsonObject
        {
            ["code"] = CandidateForm.HrF02,
            ["revision"] = revision,
            ["texts"] = new JsonObject
            {
                ["kiosk.start.title"] = Tillar($"Anketa (R{revision})"),
                ["pdf.personal.title"] = new JsonObject { [FormTexts.Latin] = sarlavha },
            },
            ["lists"] = new JsonObject
            {
                [HrF02TextCatalog.ComputerList] = new JsonObject
                {
                    ["options"] = new JsonArray(
                        Variant("windows", "Windows", "os"),
                        Variant("msWord", "MS Word", "office"),
                        Variant("x-kompas", dastur, "special")),
                },
            },
        };

        var shaped = HrF02TextCatalog.Current.Shape(served.ToJsonString());
        shaped.IsSuccess.ShouldBeTrue(shaped.IsFailure ? shaped.Error.ToString() : null);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        db.FormTextSnapshots.Add(FormTextSnapshot.Create(CandidateForm.HrF02, shaped.Value.Revision, shaped.Value.PayloadJson, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(Ct);
    }

    private static async Task SozlaAsync(HttpClient admin, string baseUrl, string apiKey)
    {
        using var javob = await admin.PutAsJsonAsync(
            new Uri("/api/settings", UriKind.Relative),
            new
            {
                items = new[]
                {
                    new { key = SystemSettingKeys.HrMentorBaseUrl, value = baseUrl },
                    new { key = SystemSettingKeys.HrMentorApiKey, value = apiKey },
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<JsonObject> SeansAsync(HttpClient mehmon)
    {
        using var javob = await mehmon.GetAsync(new Uri(SessionPath, UriKind.Relative), Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        return JsonNode.Parse(await javob.Content.ReadAsStringAsync(Ct))!.AsObject();
    }

    /// <summary>Yuboradi va bazadagi anketani qaytaradi.</summary>
    private async Task<CandidateForm> YuborAsync(HttpClient mehmon, string? textsRevision, Dictionary<string, int> dasturlar)
    {
        using var javob = await YuborishAsync(mehmon, textsRevision, dasturlar);
        javob.StatusCode.ShouldBe(HttpStatusCode.Created, await javob.Content.ReadAsStringAsync(Ct));

        var natija = await javob.Content.ReadFromJsonAsync<KioskSubmittedResponse>(Ct);

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var db = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.CandidateForms.AsNoTracking().SingleAsync(form => form.Number == natija!.FormNumber, Ct);
    }

    private async Task<HttpResponseMessage> YuborishAsync(HttpClient mehmon, string? textsRevision, Dictionary<string, int> dasturlar)
    {
        var javoblar = QuestionnaireSamples.Minimal(Bugun, Telefon()) with { ComputerSkills = dasturlar };

        using var forma = new MultipartFormDataContent
        {
            { new StringContent(QuestionnaireFormJson.Write(javoblar)), "answers" },
            { new StringContent(Guid.NewGuid().ToString()), "clientSubmissionId" },
            { new StringContent("uz-Latn"), "language" },
            { new StringContent(string.Empty), "website" },
        };

        if (textsRevision is not null)
        {
            forma.Add(new StringContent(textsRevision), "textsRevision");
        }

        var rasm = new ByteArrayContent(QuestionnaireSamples.Photo());
        rasm.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        forma.Add(rasm, "photo", "selfi.jpg");

        var imzo = new ByteArrayContent(QuestionnaireSamples.Signature());
        imzo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        forma.Add(imzo, "signature", "imzo.png");

        return await mehmon.PostAsync(new Uri(SubmitPath, UriKind.Relative), forma, Ct);
    }

    private async Task<string> PdfMatniAsync(CandidateForm anketa)
    {
        var token = await TestAccounts.TokenAsync(_factory, Permissions.RecruitingView);
        using var hr = _factory.CreateClient();
        hr.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var pdf = await hr.GetAsync(new Uri($"/api/recruiting/candidates/{anketa.CandidateId}/forms/{anketa.Id}/pdf", UriKind.Relative), Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync(Ct));

        return QuestionnairePdfRendererTests.Matn(await pdf.Content.ReadAsByteArrayAsync(Ct));
    }

    /// <summary>Ochiq formaning mijozi — har test o'z IP'si bilan (yuborish chegarasi IP bo'yicha).</summary>
    private HttpClient Mehmon()
    {
        var son = Interlocked.Increment(ref _ipSanogi);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ClientIp.HeaderName, string.Create(CultureInfo.InvariantCulture, $"198.18.{son / 250 % 250}.{(son % 250) + 1}"));

        return client;
    }

    private static string Telefon()
        => "+99893"
           + Random.Shared.Next(10_000, 99_999).ToString(CultureInfo.InvariantCulture)
           + (Interlocked.Increment(ref _telefonSanogi) % 100).ToString("D2", CultureInfo.InvariantCulture);

    private static JsonObject Tillar(string text)
        => new() { [FormTexts.Latin] = text, [FormTexts.Cyrillic] = text, [FormTexts.Russian] = text };

    private static JsonObject Variant(string value, string latin, string group)
        => new() { ["value"] = value, ["group"] = group, ["label"] = Tillar(latin), ["hidden"] = false };
}
