using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>«Blankalar» sahifasi, manbai bilan (SPEC-hujjat-manbai §2).</summary>
public sealed record ManbaliBlanksResponse(
    string? LastError,
    IReadOnlyList<BlankResponse> Items,
    string? Source);

/// <summary>
/// Blankalar HRMentor dagi SHU platformaga biriktirilganlardan olinadi (SPEC-hujjat-manbai §2, §5):
/// platforma yo'li, olingan versiyalar hisoboti, kalit hali bog'lanmagan bo'lsa eski ro'yxat, va
/// boshqa xatoda eski ro'yxatga yashirincha o'tmaslik.
/// </summary>
public sealed class HujjatManbaiSyncApiTests : IClassFixture<BonviApiFactory>
{
    private const string SyncPath = "/api/blanks/sync";

    private readonly BonviApiFactory _factory;

    public HujjatManbaiSyncApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Platforma_royxati_platforma_yolidan_yuklanadi_va_olingan_versiyalar_aytiladi()
    {
        var hrMentor = new HrMentorFake { PlatformAnswer = null };
        await using var fabrika = Fabrika(hrMentor);
        using var admin = await MijozAsync(fabrika);
        await UlaAsync(admin);

        var a = hrMentor.Publish(Guid.NewGuid(), "HR-F01", "Vakansiya so‘rovi", 1, Encoding.UTF8.GetBytes("F01 v1"));
        var b = hrMentor.Publish(Guid.NewGuid(), "HR-F03", "Surishtiruv", 2, Encoding.UTF8.GetBytes("F03 v2"));

        var birinchi = await SyncAsync(admin);
        birinchi.LastError.ShouldBeNull();
        birinchi.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromPlatform);
        birinchi.Items.Select(item => item.Code).ShouldBe(["HR-F01", "HR-F03"]);
        hrMentor.PlatformDownloads.ShouldBe(2);
        hrMentor.Downloads.ShouldBe(0, "eski yo'l ishlatilmaydi");

        var hisobot = hrMentor.ReportedHoldings.ShouldHaveSingleItem();
        hisobot.ShouldBe(
            [new HrMentorHolding(a.DocumentId, a.VersionId), new HrMentorHolding(b.DocumentId, b.VersionId)],
            ignoreOrder: true);

        // ── Yangi versiya va buzuq fayl: hisobotda eskisi emas, faqat bizda turgani
        var aYangi = hrMentor.Publish(a.DocumentId, "HR-F01", "Vakansiya so‘rovi", 2, Encoding.UTF8.GetBytes("F01 v2"));
        hrMentor.Publish(b.DocumentId, "HR-F03", "Surishtiruv", 3, Encoding.UTF8.GetBytes("F03 v3"), sha256: new string('0', 64));

        var ikkinchi = await SyncAsync(admin);
        ikkinchi.LastError.ShouldBe(HrMentorErrors.BlankChecksum.Code);
        hrMentor.ReportedHoldings.Count.ShouldBe(2, "har yurishda bitta hisobot");
        hrMentor.ReportedHoldings.Last().ShouldBe(
            [new HrMentorHolding(aYangi.DocumentId, aYangi.VersionId), new HrMentorHolding(b.DocumentId, b.VersionId)],
            ignoreOrder: true);

        // ── Hisobot yiqilsa — qadam xatosi, blankalar esa saqlangan
        hrMentor.Publish(b.DocumentId, "HR-F03", "Surishtiruv", 3, Encoding.UTF8.GetBytes("F03 v3"));
        hrMentor.HoldingsAnswer = HrMentorErrors.ServerError;

        var uchinchi = await SyncAsync(admin);
        uchinchi.LastError.ShouldBe(HrMentorErrors.ServerError.Code);
        uchinchi.Items.Single(item => item.Code == "HR-F03").VersionNo.ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Kalit_platformaga_boglanmagan_bolsa_eski_royxat_xatosiz(bool keyinBoglanadi)
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = Fabrika(hrMentor);
        using var admin = await MijozAsync(fabrika);
        await UlaAsync(admin);

        hrMentor.Publish(Guid.NewGuid(), "HR-F01", "Vakansiya so‘rovi", 1, Encoding.UTF8.GetBytes("F01 v1"));

        var eski = await SyncAsync(admin);
        eski.LastError.ShouldBeNull("o'tish davri xato emas");
        eski.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromLegacy);
        eski.Items.ShouldHaveSingleItem().Code.ShouldBe("HR-F01");
        hrMentor.Downloads.ShouldBe(1);
        hrMentor.ReportedHoldings.ShouldBeEmpty("eski yo'lda platforma yo'q — hisobot ham yo'q");

        if (!keyinBoglanadi)
        {
            return;
        }

        // ── HR kalitni bog'ladi: shu fayl qayta yuklanmaydi, manba almashadi, hisobot ketadi
        hrMentor.PlatformAnswer = null;

        var yangi = await SyncAsync(admin);
        yangi.LastError.ShouldBeNull();
        yangi.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromPlatform);
        hrMentor.PlatformDownloads.ShouldBe(0, "o'sha versiya — fayl o'zgarmagan");
        hrMentor.ReportedHoldings.ShouldHaveSingleItem().Count.ShouldBe(1);
    }

    [Fact]
    public async Task Bosh_platforma_birinchi_otishda_blankalarni_ochirmaydi_ishlab_turganida_esa_HR_qarori()
    {
        var hrMentor = new HrMentorFake();
        await using var fabrika = Fabrika(hrMentor);
        using var admin = await MijozAsync(fabrika);
        await UlaAsync(admin);

        hrMentor.Publish(Guid.NewGuid(), "HR-F01", "Vakansiya so‘rovi", 1, Encoding.UTF8.GetBytes("F01 v1"));
        (await SyncAsync(admin)).Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromLegacy);

        // ── HR «platform:sync» ruxsatini papkadan OLDIN berdi: platforma javob beradi, lekin bo'sh
        hrMentor.PlatformAnswer = null;
        hrMentor.PlatformEmpty = true;

        var bosh = await SyncAsync(admin);
        bosh.LastError.ShouldBeNull();
        bosh.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromLegacy, "platforma hali ishga tushmagan");
        bosh.Items.ShouldHaveSingleItem("bo'sh ro'yxat bilan birinchi o'tish blankalarni o'chirmaydi").Code.ShouldBe("HR-F01");
        hrMentor.ReportedHoldings.ShouldBeEmpty();

        // ── Papka biriktirildi — platformaga o'tadi
        hrMentor.PlatformEmpty = false;

        var ulandi = await SyncAsync(admin);
        ulandi.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromPlatform);
        ulandi.Items.ShouldHaveSingleItem();

        // ── Ishlab turgan platformadan HR hammasini olib tashladi — bu HR qarori, blankalar ketadi
        hrMentor.PlatformEmpty = true;

        var olindi = await SyncAsync(admin);
        olindi.LastError.ShouldBeNull();
        olindi.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromPlatform);
        olindi.Items.ShouldBeEmpty();
        hrMentor.ReportedHoldings.Last().ShouldBeEmpty("bizda endi hech narsa yo'q");
    }

    [Theory]
    [InlineData("hrmentor.platform_off")]
    [InlineData("hrmentor.unauthorized")]
    [InlineData("hrmentor.forbidden")]
    [InlineData("hrmentor.unreachable")]
    public async Task Boshqa_xatoda_eski_royxatga_otilmaydi_oxirgi_nusxa_qoladi(string kod)
    {
        var hrMentor = new HrMentorFake { PlatformAnswer = null };
        await using var fabrika = Fabrika(hrMentor);
        using var admin = await MijozAsync(fabrika);
        await UlaAsync(admin);

        hrMentor.Publish(Guid.NewGuid(), "HR-F01", "Vakansiya so‘rovi", 1, Encoding.UTF8.GetBytes("F01 v1"));
        (await SyncAsync(admin)).Items.ShouldHaveSingleItem();

        hrMentor.PlatformAnswer = new Error(kod, "test");

        var keyin = await SyncAsync(admin);
        keyin.LastError.ShouldBe(kod);
        keyin.Items.ShouldHaveSingleItem("oxirgi yaxshi nusxa o'chmaydi").Code.ShouldBe("HR-F01");
        keyin.Source.ShouldBe(SystemSettingKeys.HrMentorBlanksFromPlatform, "manba faqat muvaffaqiyatli ro'yxatda yoziladi");
        hrMentor.Downloads.ShouldBe(0, "eski /blanks ga yashirincha o'tilmaydi");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private WebApplicationFactory<Program> Fabrika(HrMentorFake hrMentor)
        => _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHrMentorClient>(hrMentor)));

    private static async Task<HttpClient> MijozAsync(WebApplicationFactory<Program> factory)
    {
        var token = await TestAccounts.TokenAsync(factory, Permissions.SettingsManage, Permissions.EmployeesView);
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
                    new { key = SystemSettingKeys.HrMentorApiKey, value = "hrm_" + new string('p', 43) },
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<ManbaliBlanksResponse> SyncAsync(HttpClient admin)
    {
        using var javob = await admin.PostAsync(new Uri(SyncPath, UriKind.Relative), null, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        return (await javob.Content.ReadFromJsonAsync<ManbaliBlanksResponse>(Ct))!;
    }
}
