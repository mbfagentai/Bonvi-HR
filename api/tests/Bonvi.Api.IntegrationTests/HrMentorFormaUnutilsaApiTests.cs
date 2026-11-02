using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HRMentor formani unutsa (bazasi tiklangan yoki qayta ulangan) — katalog va profil xeshi
/// «yuborilgan» desa ham qayta yuboriladi. 2026-11-02 da lokal tiklashda topildi: xesh o'zgarmagani
/// uchun BonviHR hech narsa yubormas, matnlar 404 ni esa «hali qabul qilinmagan» deb jim o'tkazib
/// yuborardi — konstruktor HR-F02 ni hech qachon ko'rmasdi.
/// </summary>
public sealed class HrMentorFormaUnutilsaApiTests : IClassFixture<BonviApiFactory>
{
    private const string SyncPath = "/api/blanks/sync";

    private readonly BonviApiFactory _factory;

    public HrMentorFormaUnutilsaApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HRMentor_formani_unutsa_katalog_va_profil_shu_yurishda_qayta_yuboriladi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

        using var admin = await MijozAsync(fabrika, Permissions.SettingsManage, Permissions.EmployeesView);
        await UlaAsync(admin);

        // ── Birinchi yurish: katalog va profil ketadi
        (await SyncAsync(admin)).LastError.ShouldBeNull();
        hrMentor.PushedCatalogs.Count.ShouldBe(1);
        hrMentor.PushedProfiles.Count.ShouldBe(1);

        // ── HRMentor biladi, xesh o'zgarmagan — hech narsa qayta ketmaydi
        (await SyncAsync(admin)).LastError.ShouldBeNull();
        hrMentor.PushedCatalogs.Count.ShouldBe(1, "xesh o'zgarmagan, HRMentor formani biladi");
        hrMentor.PushedProfiles.Count.ShouldBe(1, "xesh o'zgarmagan, HRMentor formani biladi");

        // ── HRMentor bazasi tiklandi: forma yo'q, xesh esa «yuborilgan» deydi
        hrMentor.Forget();
        hrMentor.ServedTexts = Xizmat(1, "Anketa (tiklangandan keyin)");
        var tiklangan = await SyncAsync(admin);
        tiklangan.LastError.ShouldBeNull();
        hrMentor.PushedCatalogs.Count.ShouldBe(2, "forma yo'q — katalog qayta yuboriladi");
        hrMentor.PushedProfiles.Count.ShouldBe(2, "katalog bilan profil ham qayta yuboriladi");
        tiklangan.TextsRevision.ShouldBe(1, "matnlar qayta yuborishdan keyin shu yurishda olinadi");

        // ── Endi biladi — yana jim
        (await SyncAsync(admin)).LastError.ShouldBeNull();
        hrMentor.PushedCatalogs.Count.ShouldBe(2);
        hrMentor.PushedProfiles.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Katalog_shu_yurishda_yuborilgan_bolsa_404_qayta_yuborishga_olib_bormaydi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

        using var admin = await MijozAsync(fabrika, Permissions.SettingsManage, Permissions.EmployeesView);
        await UlaAsync(admin);
        (await SyncAsync(admin)).LastError.ShouldBeNull();

        // ── HRMentor o'chiq — yuborish yiqiladi, xato qayd etiladi, aylanma yo'q
        hrMentor.Forget();
        hrMentor.Down = HrMentorErrors.Unreachable;
        var ochiq = await SyncAsync(admin);
        ochiq.LastError.ShouldBe(HrMentorErrors.Unreachable.Code);
        hrMentor.PushedCatalogs.Count.ShouldBe(1);

        // ── Qaytdi — keyingi yurish formani tiklaydi
        hrMentor.Down = null;
        (await SyncAsync(admin)).LastError.ShouldBeNull();
        hrMentor.PushedCatalogs.Count.ShouldBe(2);
        hrMentor.PushedProfiles.Count.ShouldBe(2);
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
