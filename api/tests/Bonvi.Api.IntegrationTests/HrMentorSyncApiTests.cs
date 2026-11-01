using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

public sealed record BlankResponse(Guid Id, string Code, string Title, int VersionNo, string FileName, string ContentType, long SizeBytes);

public sealed record BlanksResponse(
    bool Connected,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    int? TextsRevision,
    IReadOnlyList<BlankResponse> Items);

/// <summary>
/// «Blankalar» sahifasi va HRMentor bilan sinxronlash (SPEC-blankalar-hrmentor §3, §6, §8) — soxta
/// HRMentor bilan: katalog bir xil hash uchun bir marta, matnlar reviziyasi saqlanadi, fayllar
/// SHA bilan tekshiriladi, ro'yxatdan chiqqan blanka o'chiriladi, HRMentor o'chsa oxirgi ma'lumot qoladi.
/// </summary>
public sealed class HrMentorSyncApiTests : IClassFixture<BonviApiFactory>
{
    private const string BlanksPath = "/api/blanks";
    private const string SyncPath = "/api/blanks/sync";

    private readonly BonviApiFactory _factory;

    public HrMentorSyncApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Blankalar_yollari_tokensiz_401_ruxsatsiz_403()
    {
        using var mehmon = _factory.CreateClient();
        var fayl = $"{BlanksPath}/{Guid.NewGuid()}/file";

        (await mehmon.GetAsync(new Uri(BlanksPath, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await mehmon.GetAsync(new Uri(fayl, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await mehmon.PostAsync(new Uri(SyncPath, UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var rekruter = await MijozAsync(_factory, Permissions.RecruitingManage);
        (await rekruter.GetAsync(new Uri(BlanksPath, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await rekruter.GetAsync(new Uri(fayl, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var korish = await MijozAsync(_factory, Permissions.EmployeesView);
        (await korish.GetAsync(new Uri(BlanksPath, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await korish.PostAsync(new Uri(SyncPath, UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, "yangilash — faqat settings.manage");

        using var topilmadi = await korish.GetAsync(new Uri(fayl, UriKind.Relative), Ct);
        topilmadi.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await topilmadi.Content.ReadAsStringAsync(Ct)).ShouldContain("blanka.topilmadi");
    }

    [Fact]
    public async Task Sinxronlash_katalog_matnlar_va_blankalarni_oladi_HRMentor_ochsa_oxirgisi_qoladi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

        using var admin = await MijozAsync(fabrika, Permissions.SettingsManage, Permissions.EmployeesView);

        // ── Ulanmagan: «Hozir yangilash» — 400, HRMentor ga hech narsa ketmaydi
        using (var ulanmagan = await admin.PostAsync(new Uri(SyncPath, UriKind.Relative), null, Ct))
        {
            ulanmagan.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ulanmagan.Content.ReadAsStringAsync(Ct)).ShouldContain("hrmentor.not_connected");
        }

        hrMentor.PushedCatalogs.ShouldBeEmpty();

        await UlaAsync(admin);

        // ── HR matnlarni hali saqlamagan (reviziya 0, HRMentor SPEC §18): xato emas, hech narsa saqlanmaydi
        hrMentor.ServedTexts = Xizmat(0, "Anketa (o'rnatilgan)");
        var boshlangich = await SyncAsync(admin);
        boshlangich.LastError.ShouldBeNull("reviziya 0 — yaroqli javob");
        boshlangich.TextsRevision.ShouldBeNull();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var aBirinchi = Encoding.UTF8.GetBytes("HR-F01 v1");
        hrMentor.Publish(a, "HR-F01", "Vakansiya so‘rovi", 1, aBirinchi);
        hrMentor.Publish(b, "HR-F03", "Surishtiruv", 2, Encoding.UTF8.GetBytes("HR-F03 v2"));
        hrMentor.ServedTexts = Xizmat(1, "Anketa (1-reviziya)");

        // ── Birinchi yurish: katalog, matnlar, ikki blanka
        var birinchi = await SyncAsync(admin);
        birinchi.Connected.ShouldBeTrue();
        birinchi.LastError.ShouldBeNull();
        birinchi.LastSuccessAt.ShouldNotBeNull();
        birinchi.TextsRevision.ShouldBe(1);
        birinchi.Items.Select(item => item.Code).ShouldBe(["HR-F01", "HR-F03"], "kod bo'yicha");

        hrMentor.LastConnection!.BaseUrl.ShouldBe(new Uri("https://hrmentor.test"));
        hrMentor.PushedCatalogs.ShouldHaveSingleItem().ShouldBe(HrF02TextCatalog.Current.Json);
        hrMentor.Downloads.ShouldBe(2);

        var aQatori = birinchi.Items.Single(item => item.Code == "HR-F01");
        using (var fayl = await admin.GetAsync(new Uri($"{BlanksPath}/{aQatori.Id}/file", UriKind.Relative), Ct))
        {
            fayl.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await fayl.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(aBirinchi);
            fayl.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
            fayl.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("HR-F01.docx");
        }

        // ── Ikkinchisi — o'zgarish yo'q: katalog qayta yuborilmaydi, fayl qayta yuklanmaydi
        var ikkinchi = await SyncAsync(admin);
        ikkinchi.LastError.ShouldBeNull();
        hrMentor.PushedCatalogs.Count.ShouldBe(1, "katalog hashi o'zgarmagan");
        hrMentor.Downloads.ShouldBe(2);

        // ── A — yangi versiya, B — ro'yxatdan chiqdi, C — fayli ro'yxatdagisiga mos emas
        var aIkkinchi = Encoding.UTF8.GetBytes("HR-F01 v2 — yangi");
        hrMentor.Publish(a, "HR-F01", "Vakansiya so‘rovi", 2, aIkkinchi);
        hrMentor.Remove(b);
        hrMentor.Publish(Guid.NewGuid(), "HR-F05", "Buzuq", 1, Encoding.UTF8.GetBytes("buzuq"), sha256: new string('0', 64));
        hrMentor.ServedTexts = Xizmat(2, "Anketa (2-reviziya)");

        var uchinchi = await SyncAsync(admin);
        uchinchi.LastError.ShouldBe(HrMentorErrors.BlankChecksum.Code, "bitta blanka xatosi qolganini to'xtatmaydi");
        uchinchi.TextsRevision.ShouldBe(2);
        uchinchi.Items.ShouldHaveSingleItem().VersionNo.ShouldBe(2);

        using (var fayl = await admin.GetAsync(new Uri($"{BlanksPath}/{uchinchi.Items[0].Id}/file", UriKind.Relative), Ct))
        {
            (await fayl.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(aIkkinchi);
        }

        using (var eski = await admin.GetAsync(new Uri($"{BlanksPath}/{birinchi.Items.Single(item => item.Code == "HR-F03").Id}/file", UriKind.Relative), Ct))
        {
            eski.StatusCode.ShouldBe(HttpStatusCode.NotFound, "ro'yxatdan chiqqan blanka o'chirildi");
        }

        // ── HRMentor o'chiq: xato yoziladi, oxirgi ma'lumot qoladi
        hrMentor.Down = HrMentorErrors.Unreachable;

        var tortinchi = await SyncAsync(admin);
        tortinchi.LastError.ShouldBe(HrMentorErrors.Unreachable.Code);
        tortinchi.LastSuccessAt.ShouldBe(uchinchi.LastSuccessAt, "muvaffaqiyatsiz yurish oxirgi yaxshi vaqtni o'zgartirmaydi");
        tortinchi.TextsRevision.ShouldBe(2);
        tortinchi.Items.ShouldHaveSingleItem().VersionNo.ShouldBe(2);

        // ── Eski reviziya qaytsa — saqlanmaydi (reviziya faqat oshadi)
        hrMentor.Down = null;
        hrMentor.ServedTexts = Xizmat(1, "Anketa (eski)");
        (await SyncAsync(admin)).TextsRevision.ShouldBe(2);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static async Task<HttpClient> MijozAsync(WebApplicationFactory<Program> factory, params string[] permissions)
    {
        var token = await TestAccounts.TokenAsync(factory, permissions);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task UlaAsync(HttpClient admin)
    {
        using var javob = await admin.PutAsJsonAsync(
            new Uri("/api/settings", UriKind.Relative),
            new
            {
                items = new[]
                {
                    new { key = SystemSettingKeys.HrMentorBaseUrl, value = "https://hrmentor.test" },
                    new { key = SystemSettingKeys.HrMentorApiKey, value = "hrm_" + new string('k', 43) },
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<BlanksResponse> SyncAsync(HttpClient admin)
    {
        using var javob = await admin.PostAsync(new Uri(SyncPath, UriKind.Relative), null, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        return (await javob.Content.ReadFromJsonAsync<BlanksResponse>(Ct))!;
    }

    /// <summary>HRMentor ning <c>GET …/texts</c> javobi — bitta anketa matni o'zgargan.</summary>
    private static string Xizmat(int revision, string title)
        => new JsonObject
        {
            ["code"] = CandidateForm.HrF02,
            ["revision"] = revision,
            ["texts"] = new JsonObject
            {
                ["kiosk.start.title"] = new JsonObject { ["uz-Latn"] = title, ["uz-Cyrl"] = title, ["ru"] = title },
            },
            ["lists"] = new JsonObject(),
        }.ToJsonString();
}
