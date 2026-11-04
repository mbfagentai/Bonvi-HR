using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Application.Recruiting.Forms.Dynamic;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

public sealed record BlankFormResponse(int Version, string Label, DateTimeOffset ReceivedAt);

public sealed record BlanksFormResponse(string? LastError, BlankFormResponse? Form);

/// <summary>
/// Konstruktor anketasining sinxronlashi (SPEC-anketa-konstruktori §3, §5.2) — soxta HRMentor bilan:
/// profil xeshi o'zgarsagina yuboriladi; e'lon qilingan versiya SHA-256 va BonviHR tekshiruvidan
/// o'tsagina saqlanadi; eskiroq versiya e'tiborsiz; bir versiya boshqa mazmun bilan kelmaydi;
/// e'lon qaytarib olinsa — o'rnatilgan anketa.
/// </summary>
public sealed class AnketaKonstruktoriSyncApiTests : IClassFixture<BonviApiFactory>
{
    private const string BlanksPath = "/api/hrmentor/blanks";
    private const string SyncPath = "/api/hrmentor/blanks/sync";

    private readonly BonviApiFactory _factory;

    public AnketaKonstruktoriSyncApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Profil_bir_marta_yuboriladi_versiya_faqat_tekshiruvdan_otsa_amalga_kiradi()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

        using var admin = await MijozAsync(fabrika, Permissions.SettingsManage, Permissions.EmployeesView);
        await UlaAsync(admin);

        // ── E'lon yo'q (katalog qabul qilinmagan — 404): profil ketadi, anketa — ichki
        var birinchi = await SyncAsync(admin);
        birinchi.LastError.ShouldBeNull();
        birinchi.Form.ShouldBeNull();

        var profil = JsonNode.Parse(hrMentor.PushedProfiles.ShouldHaveSingleItem())!;
        CanonicalJson.Sha256(profil).ShouldBe(CanonicalJson.Sha256(FormProfile.Json(HrF02Seed.Build())));
        profil["seed"]!["label"]!.GetValue<string>().ShouldBe(CandidateForm.HrF02Version);

        (await SyncAsync(admin)).LastError.ShouldBeNull();
        hrMentor.PushedProfiles.Count.ShouldBe(1, "profil xeshi o'zgarmagan — qayta yuborilmaydi");

        // ── v1 e'lon qilindi
        hrMentor.PublishedDefinition = Elon(1, Tarif("4.0"));
        var v1 = await SyncAsync(admin);
        v1.LastError.ShouldBeNull();
        v1.Form.ShouldNotBeNull();
        (v1.Form.Version, v1.Form.Label).ShouldBe((1, "4.0"));

        // ── Xesh mos emas — rad, amaldagisi qoladi
        hrMentor.PublishedDefinition = Elon(2, Tarif("4.1"), sha256: new string('0', 64));
        var xesh = await SyncAsync(admin);
        xesh.LastError.ShouldBe(HrMentorErrors.DefinitionInvalid.Code);
        xesh.Form!.Version.ShouldBe(1);

        // ── BonviHR tushunmaydigan tur — xesh to'g'ri bo'lsa ham rad
        var notanish = Tarif("4.1");
        Maydon(notanish, "mahalla")["kind"] = "map";
        hrMentor.PublishedDefinition = Elon(2, notanish);
        var tur = await SyncAsync(admin);
        tur.LastError.ShouldBe(HrMentorErrors.DefinitionInvalid.Code);
        tur.Form!.Version.ShouldBe(1);

        // ── O'sha v1 boshqa mazmun bilan — rad (bir versiya, bir mazmun)
        var boshqa = Tarif("4.0");
        Maydon(boshqa, "mahalla")["max_length"] = 120;
        hrMentor.PublishedDefinition = Elon(1, boshqa);
        (await SyncAsync(admin)).LastError.ShouldBe(HrMentorErrors.DefinitionInvalid.Code);

        // ── v2 to'g'ri — amalga kiradi
        hrMentor.PublishedDefinition = Elon(2, Tarif("4.1"));
        var v2 = await SyncAsync(admin);
        v2.LastError.ShouldBeNull();
        (v2.Form!.Version, v2.Form.Label).ShouldBe((2, "4.1"));

        // ── Eskiroq versiya qaytsa — e'tiborsiz (versiyalar faqat o'sadi)
        hrMentor.PublishedDefinition = Elon(1, Tarif("4.0"));
        var eski = await SyncAsync(admin);
        eski.LastError.ShouldBeNull();
        eski.Form!.Version.ShouldBe(2);

        // ── HRMentor o'chiq — oxirgi versiya qoladi
        hrMentor.Down = HrMentorErrors.Unreachable;
        var ochiq = await SyncAsync(admin);
        ochiq.LastError.ShouldBe(HrMentorErrors.Unreachable.Code);
        ochiq.Form!.Version.ShouldBe(2);
        hrMentor.Down = null;

        // ── E'lon qaytarib olindi — o'rnatilgan anketa
        hrMentor.PublishedDefinition = """{"code":"HR-F02","published":null}""";
        var qaytarildi = await SyncAsync(admin);
        qaytarildi.LastError.ShouldBeNull();
        qaytarildi.Form.ShouldBeNull();

        // ── Javob shakli buzuq — «tushunarsiz», anketa o'zgarmaydi
        hrMentor.PublishedDefinition = """{"code":"HR-F02"}""";
        var buzuq = await SyncAsync(admin);
        buzuq.LastError.ShouldBe(HrMentorErrors.BadResponse.Code);
        buzuq.Form.ShouldBeNull();
    }

    /// <summary>Alohida baza: versiyalar faqat o'sadi, boshqa testning v7 i v1 ni «eski» qilardi.</summary>
    public sealed class Ulanish : IClassFixture<BonviApiFactory>
    {
        private readonly BonviApiFactory _factory;

        public Ulanish(BonviApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Ulanish_ochirilsa_amaldagi_anketa_ichkisiga_qaytadi()
        {
            var hrMentor = new HrMentorFake { PublishedDefinition = Elon(7, Tarif("5.0")) };
            await using var fabrika = _factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

            using var admin = await MijozAsync(fabrika, Permissions.SettingsManage, Permissions.EmployeesView);
            await UlaAsync(admin);

            (await SyncAsync(admin)).Form!.Version.ShouldBe(7);

            using (var ochir = await admin.PutAsJsonAsync(
                new Uri("/api/settings", UriKind.Relative),
                new { items = new[] { new { key = SystemSettingKeys.HrMentorBaseUrl, value = string.Empty } } },
                Ct))
            {
                ochir.StatusCode.ShouldBe(HttpStatusCode.OK, await ochir.Content.ReadAsStringAsync(Ct));
            }

            using var javob = await admin.GetAsync(new Uri(BlanksPath, UriKind.Relative), Ct);
            (await javob.Content.ReadFromJsonAsync<BlanksFormResponse>(Ct))!.Form.ShouldBeNull("HR matnlari bilan bir qoida");

            await UlaAsync(admin);
            using var yana = await admin.GetAsync(new Uri(BlanksPath, UriKind.Relative), Ct);
            (await yana.Content.ReadFromJsonAsync<BlanksFormResponse>(Ct))!.Form!.Version.ShouldBe(7, "versiya saqlangan edi");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>Urug' — HR yorliq qo'yib e'lon qilgandek.</summary>
    internal static JsonObject Tarif(string yorliq)
    {
        var taRif = JsonNode.Parse(HrF02Seed.Build().ToJsonString())!.AsObject();
        taRif["label"] = yorliq;
        return taRif;
    }

    internal static JsonObject Maydon(JsonObject taRif, string id)
        => taRif["sections"]!.AsArray()
            .SelectMany(section => section!["screens"]!.AsArray())
            .SelectMany(screen => screen!["fields"]!.AsArray())
            .Select(field => field!.AsObject())
            .First(field => field["id"]!.GetValue<string>() == id);

    /// <summary>HRMentor ning <c>GET …/definition</c> javobi.</summary>
    internal static string Elon(int versiya, JsonObject taRif, string? sha256 = null)
        => new JsonObject
        {
            ["code"] = CandidateForm.HrF02,
            ["published"] = new JsonObject
            {
                ["version"] = versiya,
                ["label"] = taRif["label"]!.GetValue<string>(),
                ["published_at"] = "2026-11-01T09:00:00Z",
                ["sha256"] = sha256 ?? CanonicalJson.Sha256(taRif),
                ["definition"] = JsonNode.Parse(taRif.ToJsonString()),
            },
        }.ToJsonString();

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

    private static async Task<BlanksFormResponse> SyncAsync(HttpClient admin)
    {
        using var javob = await admin.PostAsync(new Uri(SyncPath, UriKind.Relative), null, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        return (await javob.Content.ReadFromJsonAsync<BlanksFormResponse>(Ct))!;
    }
}
