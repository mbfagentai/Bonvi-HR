using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Api.Startup;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HRMentor dagi o'zgarishni kutish (SPEC-hujjat-manbai §7): birinchi javob bilan sinxronlash,
/// o'zgarmasa hech narsa, reviziya yangilansa darhol sinxronlash, yiqilgan sinxronlashda eski
/// reviziya qolib keyingi qadam qayta urinishi, xatolarning holatlari — va halqaning o'zi.
/// </summary>
/// <remarks>Bitta baza (sinf yig'masi) — ulanmagan holat ham tekshirilgani uchun qadamlar bitta testda.</remarks>
public sealed class HrMentorChangeWatchApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly string Ikki = new('2', 64);
    private static readonly string Uch = new('3', 64);
    private static readonly string Tort = new('4', 64);
    private static readonly string Besh = new('5', 64);

    private readonly BonviApiFactory _factory;

    public HrMentorChangeWatchApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ozgarish_kelishi_bilan_sinxronlaydi_kelmasa_tegmaydi()
    {
        var hrMentor = new HrMentorFake { PlatformAnswer = null };
        await using var fabrika = Fabrika(hrMentor);
        using var admin = await MijozAsync(fabrika);

        // ── Ulanmagan: HRMentor ga hech narsa ketmaydi, reviziya eslab qolinmaydi
        var qadam = await QadamAsync(fabrika, Ikki);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.NotConnected, null));
        hrMentor.ChangesAsked.ShouldBeEmpty();

        // ── Birinchi javob: bizda nima turgani noma'lum — darhol sinxronlash
        await UlaAsync(admin);
        var a = hrMentor.Publish(Guid.NewGuid(), "HR-F01", "Vakansiya so‘rovi", 1, Encoding.UTF8.GetBytes("F01 v1"));
        qadam = await QadamAsync(fabrika, null);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.Synced, hrMentor.Revision));
        hrMentor.PlatformDownloads.ShouldBe(1);
        (await BlankalarAsync(admin)).Items.ShouldHaveSingleItem().VersionNo.ShouldBe(1);

        // ── O'zgarmadi: sinxronlash yo'q
        qadam = await QadamAsync(fabrika, qadam.Since);
        qadam.Outcome.ShouldBe(HrMentorWatchOutcome.Unchanged);
        hrMentor.ReportedHoldings.Count.ShouldBe(1, "hisobot faqat sinxronlashda");

        // ── HR yangi versiya chiqardi: reviziya boshqa — darhol sinxronlash
        var eski = qadam.Since;
        hrMentor.Publish(a.DocumentId, "HR-F01", "Vakansiya so‘rovi", 2, Encoding.UTF8.GetBytes("F01 v2"));
        hrMentor.Revision = Ikki;
        qadam = await QadamAsync(fabrika, eski);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.Synced, Ikki));
        hrMentor.ChangesAsked.Last().ShouldBe(eski);
        (await BlankalarAsync(admin)).Items.ShouldHaveSingleItem().VersionNo.ShouldBe(2);

        // ── Sinxronlash yiqildi: eski reviziya qoladi — keyingi qadam qayta urinadi
        hrMentor.Revision = Uch;
        hrMentor.HoldingsAnswer = HrMentorErrors.ServerError;
        qadam = await QadamAsync(fabrika, Ikki);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.SyncFailed, Ikki, HrMentorErrors.ServerError.Code));
        hrMentor.HoldingsAnswer = null;
        qadam = await QadamAsync(fabrika, qadam.Since);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.Synced, Uch));

        // ── Kutishning o'zi yiqilsa: holat va kod, reviziya o'zgarmaydi, sinxronlash yo'q
        var hisobotlar = hrMentor.ReportedHoldings.Count;
        foreach (var (xato, holat) in new[]
        {
            (HrMentorErrors.PlatformNotReady, HrMentorWatchOutcome.NotReady),
            (HrMentorErrors.PlatformOff, HrMentorWatchOutcome.Off),
            (HrMentorErrors.Unreachable, HrMentorWatchOutcome.Failed),
            (HrMentorErrors.BadResponse, HrMentorWatchOutcome.Failed),
        })
        {
            hrMentor.ChangesAnswer = xato;
            (await QadamAsync(fabrika, Uch)).ShouldBe(new HrMentorWatchStep(holat, Uch, xato.Code));
        }

        hrMentor.ReportedHoldings.Count.ShouldBe(hisobotlar);

        // ── HR saqlagan narsani qabul qila olmadik (doimiy xato): reviziya olinadi — keyingisi ham
        //    soniyalarda keladi; qolgan qadamlar bajarildi, xatoni besh daqiqalik yurish qayta tekshiradi
        hrMentor.ChangesAnswer = null;
        hrMentor.Revision = Tort;
        hrMentor.ServedTexts = "salom";
        qadam = await QadamAsync(fabrika, Uch);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.SyncedWithError, Tort, HrMentorErrors.TextsInvalid.Code));
        hrMentor.ReportedHoldings.Count.ShouldBe(hisobotlar + 1, "blankalar qadami bajarildi");

        // ── Doimiy xato bilan birga o'tkinchisi ham bo'lsa — eski reviziya qoladi, qayta urinadi
        hrMentor.Revision = Besh;
        hrMentor.HoldingsAnswer = HrMentorErrors.ServerError;
        qadam = await QadamAsync(fabrika, Tort);
        qadam.ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.SyncFailed, Tort, HrMentorErrors.TextsInvalid.Code));

        hrMentor.HoldingsAnswer = null;
        hrMentor.ServedTexts = null;
        (await QadamAsync(fabrika, Tort)).ShouldBe(new HrMentorWatchStep(HrMentorWatchOutcome.Synced, Besh));
    }

    private static async Task<HrMentorWatchStep> QadamAsync(WebApplicationFactory<Program> fabrika, string? since)
    {
        await using var scope = fabrika.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<HrMentorChangeWatch>().RunOnceAsync(since, Ct);
    }

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
                    new { key = SystemSettingKeys.HrMentorApiKey, value = "hrm_" + new string('w', 43) },
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<ManbaliBlanksResponse> BlankalarAsync(HttpClient admin)
        => (await admin.GetFromJsonAsync<ManbaliBlanksResponse>(new Uri("/api/blanks", UriKind.Relative), Ct))!;
}

/// <summary>
/// Halqaning o'zi (<see cref="HrMentorChangeWatcher"/>): ishga tushadi, birinchi javob va yangi
/// reviziya bilan sinxronlaydi, to'xtatilganda so'rov yubormaydi. O'z bazasi bilan.
/// </summary>
public sealed class HrMentorChangeWatcherApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly string Ikki = new('2', 64);

    private readonly BonviApiFactory _factory;

    public HrMentorChangeWatcherApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Halqa_ishga_tushadi_ozgarishni_oladi_va_toxtaydi()
    {
        var hrMentor = new HrMentorFake { PlatformAnswer = null };
        await using var fabrika = Fabrika(hrMentor);
        using var admin = await MijozAsync(fabrika);
        await UlaAsync(admin);

        fabrika.Services.GetServices<IHostedService>().OfType<HrMentorChangeWatcher>()
            .ShouldBeEmpty("Testing da halqa yo'q — testlar uni o'zi boshqaradi");

        var a = hrMentor.Publish(Guid.NewGuid(), "HR-F05", "Tushuntirish xati", 1, Encoding.UTF8.GetBytes("F05 v1"));
        var halqa = new HrMentorChangeWatcher(
            fabrika.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<HrMentorChangeWatcher>.Instance);

        await halqa.StartAsync(Ct);
        try
        {
            await KutAsync(() => !hrMentor.ReportedHoldings.IsEmpty, "birinchi javob bilan sinxronlash");

            hrMentor.Publish(a.DocumentId, "HR-F05", "Tushuntirish xati", 2, Encoding.UTF8.GetBytes("F05 v2"));
            hrMentor.Revision = Ikki;
            await KutAsync(
                () => hrMentor.ReportedHoldings.Any(h => h.Any(x => x.VersionId == hrMentor.Blanks.Single().VersionId)),
                "yangi reviziya bilan sinxronlash");
        }
        finally
        {
            await halqa.StopAsync(Ct);
        }

        var soni = hrMentor.ChangesAsked.Count;
        await Task.Delay(TimeSpan.FromSeconds(1.5), Ct);
        hrMentor.ChangesAsked.Count.ShouldBe(soni, "to'xtagandan keyin so'rov yo'q");
    }

    private static async Task KutAsync(Func<bool> shart, string nima)
    {
        var tugash = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!shart())
        {
            DateTime.UtcNow.ShouldBeLessThan(tugash, nima);
            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }

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
                    new { key = SystemSettingKeys.HrMentorApiKey, value = "hrm_" + new string('v', 43) },
                },
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }
}
