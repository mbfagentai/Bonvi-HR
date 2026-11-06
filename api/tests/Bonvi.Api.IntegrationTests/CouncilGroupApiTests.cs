using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Bonvi.Domain.Settings;

namespace Bonvi.Api.IntegrationTests;

/// <summary>The council in Telegram: who gets in, whose words are whose (docs/SPEC-kengash-guruhi.md).</summary>
public sealed class CouncilGroupApiTests : IClassFixture<BonviApiFactory>
{
    private const string Roster = "/api/recruiting/council-group";
    private const string BotApi = "/api/recruiting/council-bot";
    private const string App = "/api/council-app";
    private const string BotToken = "12345678:abcdef0123456789abcdef0123456789";
    private const string Header = "X-Telegram-Init-Data";

    private static int _kun;
    private static long _telegramId = 700_000;

    private readonly BonviApiFactory _factory;

    public CouncilGroupApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Royxatni_faqat_sozlama_egasi_boshqaradi()
    {
        using var tokensiz = _factory.CreateClient();
        using (var javob = await tokensiz.GetAsync(new Uri(Roster, UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");
        (string Method, string Path)[] yollar =
        [
            ("GET", Roster),
            ("POST", $"{Roster}/members"),
            ("PUT", $"{Roster}/members/{Guid.NewGuid()}"),
            ("POST", $"{Roster}/members/{Guid.NewGuid()}/unlink"),
            ("POST", $"{Roster}/bind-code"),
            ("PUT", $"{Roster}/mini-app"),
        ];

        foreach (var (method, path) in yollar)
        {
            using var sorov = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative))
            {
                Content = method == "GET" ? null : JsonContent.Create(new { fullName = "X", phone = "901234567", name = "abc" }),
            };
            using var javob = await hr.SendAsync(sorov, Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }
    }

    [Fact]
    public async Task Bot_yollari_odamga_yopiq()
    {
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage", "settings.manage");

        using (var javob = await hr.GetAsync(new Uri($"{BotApi}/state", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        foreach (var yol in new[] { "bind", "migrate", "link", "digest-sent" })
        {
            using var javob = await hr.PostAsJsonAsync(
                new Uri($"{BotApi}/{yol}", UriKind.Relative),
                new { code = "1", chatId = -1, telegramUserId = 1, phone = "901234567", date = "2026-11-07", messageId = 1, signature = "s" },
                Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, yol);
        }
    }

    [Fact]
    public async Task Mini_app_imzosiz_soxta_va_begonani_kiritmaydi()
    {
        await TokenQoyAsync();
        var nomzod = Guid.NewGuid();
        using var mijoz = _factory.CreateClient();

        foreach (var yol in new[] { "/me", "/candidates", $"/candidates/{nomzod}", $"/candidates/{nomzod}/photo", $"/candidates/{nomzod}/forms/{nomzod}/pdf" })
        {
            using (var javob = await mijoz.GetAsync(new Uri(App + yol, UriKind.Relative), Ct))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, yol);
            }

            using var soxta = new HttpRequestMessage(HttpMethod.Get, new Uri(App + yol, UriKind.Relative));
            soxta.Headers.Add(Header, Imzo("999:other", 1));
            using (var javob = await mijoz.SendAsync(soxta, Ct))
            {
                javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, yol);
            }
        }

        var begona = Interlocked.Increment(ref _telegramId);
        (await SorovAsync<JsonElement>(mijoz, begona, "/me")).GetProperty("recognized").GetBoolean().ShouldBeFalse();

        using var taqiq = new HttpRequestMessage(HttpMethod.Get, new Uri($"{App}/candidates", UriKind.Relative));
        taqiq.Headers.Add(Header, Imzo(BotToken, begona));
        using var natija = await mijoz.SendAsync(taqiq, Ct);
        natija.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var yozish = new HttpRequestMessage(HttpMethod.Post, new Uri($"{App}/candidates/{nomzod}/opinions", UriKind.Relative))
        {
            Content = JsonContent.Create(new { decision = "Accept" }),
        };
        yozish.Headers.Add(Header, Imzo(BotToken, begona));
        using var yozishJavobi = await mijoz.SendAsync(yozish, Ct);
        yozishJavobi.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Azo_bogoqlanadi_faol_emas_bolsa_kirolmaydi_raqam_almashsa_uziladi()
    {
        await TokenQoyAsync();
        using var admin = await MijozAsync("settings.manage");
        using var bot = await BotMijozAsync();
        using var mijoz = _factory.CreateClient();

        var telefon = TasodifiyTelefon();
        var (azoId, telegram) = await AzoAsync(admin, bot, "Шеф Тестов", telefon);

        var men = await SorovAsync<JsonElement>(mijoz, telegram, "/me");
        men.GetProperty("recognized").GetBoolean().ShouldBeTrue();
        men.GetProperty("fullName").GetString().ShouldBe("Шеф Тестов");

        var royxat = await admin.GetFromJsonAsync<JsonElement>(new Uri(Roster, UriKind.Relative), Ct);
        var qator = royxat.GetProperty("members").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == azoId);
        qator.GetProperty("linked").GetBoolean().ShouldBeTrue();

        // Bir xil Telegram boshqa a'zoga o'tsa, oldingisidan uziladi
        var boshqaTelefon = TasodifiyTelefon();
        using (var javob = await admin.PostAsJsonAsync(new Uri($"{Roster}/members", UriKind.Relative), new { fullName = "Иккинчи", phone = boshqaTelefon }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }

        using (var javob = await admin.PutAsJsonAsync(
            new Uri($"{Roster}/members/{azoId}", UriKind.Relative), new { fullName = "Шеф Тестов", phone = telefon, isActive = false }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }

        (await SorovAsync<JsonElement>(mijoz, telegram, "/me")).GetProperty("recognized").GetBoolean().ShouldBeFalse();

        using (var javob = await admin.PutAsJsonAsync(
            new Uri($"{Roster}/members/{azoId}", UriKind.Relative), new { fullName = "Шеф Тестов", phone = TasodifiyTelefon(), isActive = true }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }

        (await SorovAsync<JsonElement>(mijoz, telegram, "/me")).GetProperty("recognized").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Royxatda_yoq_raqam_bogoqlanmaydi_va_band_raqam_rad_etiladi()
    {
        using var admin = await MijozAsync("settings.manage");
        using var bot = await BotMijozAsync();

        using (var javob = await bot.PostAsJsonAsync(
            new Uri($"{BotApi}/link", UriKind.Relative), new { telegramUserId = 5, phone = TasodifiyTelefon() }, Ct))
        {
            (await javob.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("linked").GetBoolean().ShouldBeFalse();
        }

        var telefon = TasodifiyTelefon();
        using (var javob = await admin.PostAsJsonAsync(new Uri($"{Roster}/members", UriKind.Relative), new { fullName = "Биринчи", phone = telefon }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var javob = await admin.PostAsJsonAsync(new Uri($"{Roster}/members", UriKind.Relative), new { fullName = "Иккинчи", phone = telefon }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        using (var javob = await admin.PostAsJsonAsync(new Uri($"{Roster}/members", UriKind.Relative), new { fullName = "Хато", phone = "123" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Xulosalar_yigiladi_azo_faqat_ozinikini_koradi_hr_hammasini()
    {
        await TokenQoyAsync();
        using var admin = await MijozAsync("settings.manage");
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");
        using var bot = await BotMijozAsync();
        using var mijoz = _factory.CreateClient();

        var sana = KeyingiKun();
        var nomzod = await ChaqirilganNomzodAsync(hr, sana);
        var (_, birinchi) = await AzoAsync(admin, bot, "Биринчи Аъзо", TasodifiyTelefon());
        var (_, ikkinchi) = await AzoAsync(admin, bot, "Иккинчи Аъзо", TasodifiyTelefon());

        await XulosaAsync(mijoz, birinchi, nomzod, new { decision = "Reserve", comment = "Биринчи фикр" });
        await XulosaAsync(mijoz, birinchi, nomzod, new { decision = "Accept", comment = "Ўзгартирдим" });
        await XulosaAsync(mijoz, ikkinchi, nomzod, new { decision = "Reject" });

        var meniki = await SorovAsync<JsonElement>(mijoz, birinchi, $"/candidates/{nomzod}");
        var yozuvlar = meniki.GetProperty("myOpinions").EnumerateArray().ToList();
        yozuvlar.Select(row => row.GetProperty("comment").GetString()).ShouldBe(["Ўзгартирдим", "Биринчи фикр"]);
        meniki.GetRawText().ShouldNotContain("Иккинчи");

        var ikkinchisi = await SorovAsync<JsonElement>(mijoz, ikkinchi, $"/candidates/{nomzod}");
        ikkinchisi.GetProperty("myOpinions").GetArrayLength().ShouldBe(1);
        ikkinchisi.GetRawText().ShouldNotContain("Ўзгартирдим");

        var majlis = await hr.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/recruiting/council-meetings/{sana:yyyy-MM-dd}", UriKind.Relative), Ct);
        var qator = majlis.GetProperty("candidates").EnumerateArray().Single(row => row.GetProperty("candidateId").GetGuid() == nomzod);
        var fikrlar = qator.GetProperty("opinions").EnumerateArray().ToList();
        fikrlar.Count.ShouldBe(3);
        fikrlar.Select(row => row.GetProperty("memberName").GetString()).Distinct().Count().ShouldBe(2);

        // Xulosalar yakuniy qarorni bermaydi: uni HR kiritadi
        qator.GetProperty("decided").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Shartli_qarorga_izoh_kerak_chaqirilmagan_nomzodga_yozib_bolmaydi()
    {
        await TokenQoyAsync();
        using var admin = await MijozAsync("settings.manage");
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");
        using var bot = await BotMijozAsync();
        using var mijoz = _factory.CreateClient();

        var nomzod = await ChaqirilganNomzodAsync(hr, KeyingiKun());
        var (_, azo) = await AzoAsync(admin, bot, "Аъзо", TasodifiyTelefon());

        using (var javob = await XulosaYuborAsync(mijoz, azo, nomzod, new { decision = "Conditional" }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var javob = await XulosaYuborAsync(mijoz, azo, nomzod, new { }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var javob = await XulosaYuborAsync(mijoz, azo, Guid.NewGuid(), new { decision = "Accept" }))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var korish = new HttpRequestMessage(HttpMethod.Get, new Uri($"{App}/candidates/{Guid.NewGuid()}", UriKind.Relative));
        korish.Headers.Add(Header, Imzo(BotToken, azo));
        using var natija = await mijoz.SendAsync(korish, Ct);
        natija.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Guruh_kod_bilan_bir_marta_ulanadi()
    {
        using var admin = await MijozAsync("settings.manage");
        using var bot = await BotMijozAsync();

        using (var javob = await bot.PutAsJsonAsync(new Uri("/api/settings/bot/username", UriKind.Relative), new { username = "Bonvi_HR_bot" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var berish = await admin.PostAsync(new Uri($"{Roster}/bind-code", UriKind.Relative), null, Ct);
        berish.StatusCode.ShouldBe(HttpStatusCode.OK, await berish.Content.ReadAsStringAsync(Ct));
        var buyruq = (await berish.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("command").GetString()!;
        buyruq.ShouldStartWith("/kengash_ulash@Bonvi_HR_bot ");
        var kod = buyruq.Split(' ')[1];

        using (var javob = await bot.PostAsJsonAsync(new Uri($"{BotApi}/bind", UriKind.Relative), new { code = "00000000", chatId = -100500, title = "X" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var javob = await bot.PostAsJsonAsync(new Uri($"{BotApi}/bind", UriKind.Relative), new { code = kod, chatId = 5, title = "Шахсий чат" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var javob = await bot.PostAsJsonAsync(new Uri($"{BotApi}/bind", UriKind.Relative), new { code = kod, chatId = -100500, title = "Кенгаш" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
        }

        using (var javob = await bot.PostAsJsonAsync(new Uri($"{BotApi}/bind", UriKind.Relative), new { code = kod, chatId = -100600, title = "Бошқа" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var holat = await bot.GetFromJsonAsync<JsonElement>(new Uri($"{BotApi}/state", UriKind.Relative), Ct);
        holat.GetProperty("chatId").GetInt64().ShouldBe(-100500);
        holat.GetProperty("bindOpen").GetBoolean().ShouldBeFalse();

        // Another group's move changes nothing, the bound group's move follows it
        using (var javob = await bot.PostAsJsonAsync(new Uri($"{BotApi}/migrate", UriKind.Relative), new { fromChatId = -1, toChatId = -100700 }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await bot.GetFromJsonAsync<JsonElement>(new Uri($"{BotApi}/state", UriKind.Relative), Ct)).GetProperty("chatId").GetInt64().ShouldBe(-100500);

        using (var javob = await bot.PostAsJsonAsync(new Uri($"{BotApi}/migrate", UriKind.Relative), new { fromChatId = -100500, toChatId = -100700 }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await bot.GetFromJsonAsync<JsonElement>(new Uri($"{BotApi}/state", UriKind.Relative), Ct)).GetProperty("chatId").GetInt64().ShouldBe(-100700);

        using var yangiKod = await admin.PostAsync(new Uri($"{Roster}/bind-code", UriKind.Relative), null, Ct);
        yangiKod.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await bot.GetFromJsonAsync<JsonElement>(new Uri($"{BotApi}/state", UriKind.Relative), Ct)).GetProperty("bindOpen").GetBoolean().ShouldBeTrue();

        var panel = await admin.GetFromJsonAsync<JsonElement>(new Uri(Roster, UriKind.Relative), Ct);
        panel.GetProperty("groupBound").GetBoolean().ShouldBeTrue();
        panel.GetProperty("groupTitle").GetString().ShouldBe("Кенгаш");
    }

    private static string Imzo(string token, long telegramId)
    {
        var maydonlar = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["user"] = "{\"id\":" + telegramId.ToString(CultureInfo.InvariantCulture) + "}",
        };
        var tekshiruv = string.Join('\n', maydonlar.Select(pair => pair.Key + "=" + pair.Value));
        var sir = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(token));
        var xesh = Convert.ToHexStringLower(HMACSHA256.HashData(sir, Encoding.UTF8.GetBytes(tekshiruv)));

        return string.Join('&', maydonlar.Select(pair => pair.Key + "=" + HttpUtility.UrlEncode(pair.Value))) + "&hash=" + xesh;
    }

    private static async Task<T> SorovAsync<T>(HttpClient mijoz, long telegramId, string yol)
    {
        using var sorov = new HttpRequestMessage(HttpMethod.Get, new Uri(App + yol, UriKind.Relative));
        sorov.Headers.Add(Header, Imzo(BotToken, telegramId));
        using var javob = await mijoz.SendAsync(sorov, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        return (await javob.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<HttpResponseMessage> XulosaYuborAsync(HttpClient mijoz, long telegramId, Guid nomzod, object tana)
    {
        using var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri($"{App}/candidates/{nomzod}/opinions", UriKind.Relative))
        {
            Content = JsonContent.Create(tana),
        };
        sorov.Headers.Add(Header, Imzo(BotToken, telegramId));

        return await mijoz.SendAsync(sorov, Ct);
    }

    private static async Task XulosaAsync(HttpClient mijoz, long telegramId, Guid nomzod, object tana)
    {
        using var javob = await XulosaYuborAsync(mijoz, telegramId, nomzod, tana);
        javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<(Guid Id, long Telegram)> AzoAsync(HttpClient admin, HttpClient bot, string ism, string telefon)
    {
        using var qoshish = await admin.PostAsJsonAsync(new Uri($"{Roster}/members", UriKind.Relative), new { fullName = ism, phone = telefon }, Ct);
        qoshish.StatusCode.ShouldBe(HttpStatusCode.OK, await qoshish.Content.ReadAsStringAsync(Ct));
        var id = await qoshish.Content.ReadFromJsonAsync<Guid>(Ct);

        var telegram = Interlocked.Increment(ref _telegramId);
        using var ulash = await bot.PostAsJsonAsync(new Uri($"{BotApi}/link", UriKind.Relative), new { telegramUserId = telegram, phone = "+998" + telefon }, Ct);
        ulash.StatusCode.ShouldBe(HttpStatusCode.OK, await ulash.Content.ReadAsStringAsync(Ct));
        (await ulash.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("linked").GetBoolean().ShouldBeTrue();

        return (id, telegram);
    }

    private static string TasodifiyTelefon()
        => "93" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private static DateOnly KeyingiKun() => new DateOnly(2031, 1, 1).AddDays(Interlocked.Increment(ref _kun));

    private async Task TokenQoyAsync()
    {
        using var admin = await MijozAsync("settings.manage");
        using var javob = await admin.PutAsJsonAsync(
            new Uri("/api/settings", UriKind.Relative),
            new { items = new[] { new { key = SystemSettingKeys.BotToken, value = BotToken } } },
            Ct);
        javob.IsSuccessStatusCode.ShouldBeTrue(await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<Guid> ChaqirilganNomzodAsync(HttpClient mijoz, DateOnly sana)
    {
        using var vakansiya = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = $"Кенгаш гуруҳи {Guid.NewGuid():N}", openings = 1, openedAt = new DateOnly(2026, 3, 1) },
            Ct);
        vakansiya.StatusCode.ShouldBe(HttpStatusCode.OK, await vakansiya.Content.ReadAsStringAsync(Ct));
        var vakansiyaId = (await vakansiya.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var nomzod = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = "Гуруҳов",
                firstName = "Азиз",
                phone = TasodifiyTelefon(),
                birthDate = new DateOnly(1995, 5, 20),
                source = "Manual",
                referralChannelId = ReferralSeeds.Telegram,
            },
            Ct);
        nomzod.StatusCode.ShouldBe(HttpStatusCode.OK, await nomzod.Content.ReadAsStringAsync(Ct));
        var id = (await nomzod.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var taklif = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{id}/interview-invite", UriKind.Relative),
            new { interviewAt = new DateTimeOffset(sana.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(5)) },
            Ct);
        taklif.StatusCode.ShouldBe(HttpStatusCode.OK, await taklif.Content.ReadAsStringAsync(Ct));

        return id;
    }

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return client;
    }

    private async Task<HttpClient> BotMijozAsync()
    {
        var client = _factory.CreateClient();
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestBotLogin, password = BonviApiFactory.TestBotPassword },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        var token = (await javob.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
