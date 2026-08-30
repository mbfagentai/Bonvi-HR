using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Sozlamalar ro'yxatining bitta qatori (test uchun).</summary>
public sealed record SettingResponse(
    string Key,
    string Value,
    string Kind,
    string DefaultValue,
    int? Min,
    int? Max,
    int? MaxLength,
    bool IsSet,
    string? Preview);

/// <summary>Botga beriladigan token (test uchun).</summary>
public sealed record BotTokenResponse(string? Token);

/// <summary>
/// Tizim sozlamalari — uchi-uchiga, alohida e'tibor SIRLARGA.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega bu testlar mavjud.</b> Telegram tokeni ekranga chiqarilgach
/// (foydalanuvchi qarori, sabab <see cref="SystemSettingKeys.BotToken"/>
/// izohida) tizimda birinchi marta SAQLANADIGAN sir paydo bo'ldi. Sir
/// bilan bog'liq xatolar jimgina o'tadi: token ochiq matn bo'lib bazaga
/// tushsa ham, javobda qaytib ketsa ham, ekran BIR XIL ishlaydi va
/// hech kim hech narsa sezmaydi. Shu sababli bu yerda ekranning
/// ko'rinishi emas, aynan SHU uchta qoida qadab qo'yilgan:
/// </para>
/// <list type="number">
///   <item>bazada ochiq matn YOTMAYDI;</item>
///   <item>javobda sir QAYTMAYDI — hatto uni qo'ygan odamga ham;</item>
///   <item>tokenni faqat BOT hisobи o'qiy oladi.</item>
/// </list>
/// <para>
/// Testlar bitta bazani baham ko'radi va ular ketma-ket emas, parallel
/// ishlashi mumkin — shu sababli har biri O'Z kalitiga tegadi yoki
/// tegib bo'lgach holatni qaytaradi.
/// </para>
/// </remarks>
public sealed class SettingsApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly Uri SettingsUri = new("/api/settings", UriKind.Relative);
    private static readonly Uri BotSettingsUri = new("/api/settings/bot", UriKind.Relative);
    private static readonly Uri BotTokenUri = new("/api/settings/bot/token", UriKind.Relative);
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);

    /// <summary>Haqiqiy ko'rinishdagi, lekin O'YLAB TOPILGAN token.</summary>
    /// <remarks>
    /// Naqshdan o'tishi shart (<see cref="SystemSettingKeys.TelegramTokenPattern"/>),
    /// aks holda test tekshirmoqchi bo'lgan yo'lgacha yetib bormasdi.
    /// </remarks>
    private const string SampleToken = "8123456789:AAHtestTOKENqiymati_1234567890abcd";

    private readonly BonviApiFactory _factory;

    public SettingsApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_sozlamalar_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(SettingsUri, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tokenni O'QISH — faqat bot hisobiga. Super administrator ham yeta olmaydi.
    /// </summary>
    /// <remarks>
    /// Bu testning eng muhim yarmi — aynan super administratorga rad
    /// javobi. U tokenni QO'YADI, lekin qaytarib o'qiy olmaydi: sir
    /// bir tomonlama harakat qiladi. Kimdir bir kun endpoint'ni
    /// «qulaylik uchun» ruxsat bilan himoyalasa, bu test yiqiladi —
    /// chunki ruxsat super administratorga O'ZI yetib borardi
    /// (<c>SystemRoles.Defaults</c>).
    /// </remarks>
    [Fact]
    public async Task Tokenni_faqat_bot_hisobi_oqiy_oladi()
    {
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        using var adminJavob = await admin.GetAsync(BotTokenUri, TestContext.Current.CancellationToken);
        adminJavob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var botJavob = await bot.GetAsync(BotTokenUri, TestContext.Current.CancellationToken);
        botJavob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(botJavob));
    }

    /// <summary>Bot esa qolgan sozlamalarni O'ZGARTIRA olmaydi.</summary>
    /// <remarks>
    /// Teskari yo'nalishdagi tekshiruv: tokenni o'qiy olish bot hisobiga
    /// boshqa hech qanday yangi huquq bermasligi kerak.
    /// </remarks>
    [Fact]
    public async Task Bot_hisobi_sozlamalarni_ozgartira_olmaydi()
    {
        using var bot = await BotMijozAsync();

        using var oqish = await bot.GetAsync(SettingsUri, TestContext.Current.CancellationToken);
        oqish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var yozish = await bot.PutAsJsonAsync(
            SettingsUri,
            new { items = new[] { new { key = SystemSettingKeys.BotAboutLatin, value = "Bonvi" } } },
            TestContext.Current.CancellationToken);

        yozish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Sir: yozish, o'qish, o'chirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Token saqlanadi, lekin bazaga OCHIQ MATN bo'lib tushmaydi.
    /// </summary>
    /// <remarks>
    /// Zaxira nusxa har kecha olinadi va u fayl bo'lib ko'chiriladi —
    /// aynan shu sababli shifrlash umuman kiritilgan
    /// (<c>ISecretProtector</c> izohi). Test qatorni bazadan
    /// TO'G'RIDAN-TO'G'RI o'qiydi: API orqali tekshirish bu yerda hech
    /// narsani isbotlamasdi, chunki API baribir sirni qaytarmaydi.
    /// </remarks>
    [Fact]
    public async Task Token_bazaga_shifrlangan_holda_yoziladi()
    {
        using var admin = await AdminMijozAsync();

        try
        {
            using var yozish = await TokenniYozAsync(admin, SampleToken);
            yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

            await using var qamrov = _factory.Services.CreateAsyncScope();
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var qator = await kontekst.Set<SystemSetting>()
                .AsNoTracking()
                .SingleAsync(row => row.Key == SystemSettingKeys.BotToken, TestContext.Current.CancellationToken);

            qator.Value.ShouldNotBe(SampleToken);
            qator.Value.ShouldNotContain("AAHtest", Case.Insensitive);
            qator.Value.ShouldStartWith("v1.", Case.Sensitive, "shifrlash formati versiya bilan belgilanadi");
        }
        finally
        {
            await TokenniTozalaAsync(admin);
        }
    }

    /// <summary>
    /// Javobda sir QAYTMAYDI — faqat «qo'yilgan» va niqoblangan ko'rinish.
    /// </summary>
    [Fact]
    public async Task Javobda_token_qiymati_qaytmaydi()
    {
        using var admin = await AdminMijozAsync();

        try
        {
            using var yozish = await TokenniYozAsync(admin, SampleToken);
            yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

            // Javob TANASINING o'zida ham sir bo'lmasligi kerak: `PUT`
            // to'liq ro'yxatni qaytaradi va u ekranga borib tushadi.
            (await MatnAsync(yozish)).ShouldNotContain("AAHtest", Case.Insensitive);

            var token = (await SozlamalarAsync(admin))
                .Single(item => item.Key == SystemSettingKeys.BotToken);

            token.Kind.ShouldBe("Secret");
            token.Value.ShouldBeEmpty();
            token.IsSet.ShouldBeTrue();

            // Niqob: bot identifikatori ochiq (u sir emas), sirning esa
            // faqat oxirgi to'rt belgisi.
            token.Preview.ShouldBe("8123456789:…abcd");
        }
        finally
        {
            await TokenniTozalaAsync(admin);
        }
    }

    /// <summary>Bot tokenni OCHIQ holda oladi — aylanish tugaydi.</summary>
    /// <remarks>
    /// Oldingi ikki test sirning YASHIRINLIGINI tekshiradi. Bu esa
    /// teskarisini: shifrlash to'g'ri teskarilanadimi. Ikkalasi birga
    /// bo'lmasa, «hech kim o'qiy olmaydigan» token ham testdan o'tib
    /// ketardi.
    /// </remarks>
    [Fact]
    public async Task Bot_tokenni_ochiq_holda_oladi()
    {
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        try
        {
            using var yozish = await TokenniYozAsync(admin, SampleToken);
            yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

            using var javob = await bot.GetAsync(BotTokenUri, TestContext.Current.CancellationToken);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

            var olingan = await javob.Content.ReadFromJsonAsync<BotTokenResponse>(
                TestContext.Current.CancellationToken);

            olingan.ShouldNotBeNull();
            olingan.Token.ShouldBe(SampleToken);
        }
        finally
        {
            await TokenniTozalaAsync(admin);
        }
    }

    /// <summary>
    /// Bo'sh qiymat — sirni O'CHIRADI va bazada qator qolmaydi.
    /// </summary>
    /// <remarks>
    /// Matnni bo'shatishdan farqi shu: matnda bo'sh satr YOZILADI
    /// (bot «Kompaniya haqida» tugmasini chizmaydi), sirda esa
    /// yozuvning o'zi olib tashlanadi. Bo'sh shifrlangan qator qolib
    /// ketsa, <c>isSet</c> yolg'on gapirardi.
    /// </remarks>
    [Fact]
    public async Task Bosh_qiymat_tokenni_ochiradi()
    {
        using var admin = await AdminMijozAsync();

        using var yozish = await TokenniYozAsync(admin, SampleToken);
        yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

        using var ochirish = await TokenniYozAsync(admin, string.Empty);
        ochirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ochirish));

        var token = (await SozlamalarAsync(admin))
            .Single(item => item.Key == SystemSettingKeys.BotToken);

        token.IsSet.ShouldBeFalse();
        token.Preview.ShouldBeNull();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        (await kontekst.Set<SystemSetting>()
                .AsNoTracking()
                .AnyAsync(row => row.Key == SystemSettingKeys.BotToken, TestContext.Current.CancellationToken))
            .ShouldBeFalse("bo'sh sir uchun qator qolmasligi kerak");
    }

    /// <summary>
    /// Ko'rinishi noto'g'ri token RAD ETILADI va bazaga tushmaydi.
    /// </summary>
    /// <remarks>
    /// Eng keng tarqalgan xato — tokenni chala nusxa olish. Tekshiruvsiz
    /// u jimgina saqlanardi, ekran «qo'yildi» deb ko'rsatardi va nosozlik
    /// faqat botning jurnalida qolardi.
    /// </remarks>
    [Fact]
    public async Task Notogri_korinishdagi_token_rad_etiladi()
    {
        using var admin = await AdminMijozAsync();

        using var javob = await TokenniYozAsync(admin, "chala-nusxa-olingan-token");

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("sozlama.notogri_format");

        (await SozlamalarAsync(admin))
            .Single(item => item.Key == SystemSettingKeys.BotToken)
            .IsSet.ShouldBeFalse();
    }

    /// <summary>
    /// Token botning UMUMIY sozlamalar javobiga tushmaydi.
    /// </summary>
    /// <remarks>
    /// <c>/api/settings/bot</c> — <c>recruiting.view</c> ostida va bu
    /// ruxsat HR menejerlarida ham bor. Token o'sha javobga qo'shilsa,
    /// uni har bir HR o'qiy olardi. Test aynan shu chegarani qadaydi.
    /// </remarks>
    [Fact]
    public async Task Bot_sozlamalari_javobida_token_yoq()
    {
        using var admin = await AdminMijozAsync();
        using var bot = await BotMijozAsync();

        try
        {
            using var yozish = await TokenniYozAsync(admin, SampleToken);
            yozish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yozish));

            using var javob = await bot.GetAsync(BotSettingsUri, TestContext.Current.CancellationToken);
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

            var matn = await MatnAsync(javob);
            matn.ShouldNotContain("AAHtest", Case.Insensitive);
            matn.ShouldNotContain("token", Case.Insensitive);
        }
        finally
        {
            await TokenniTozalaAsync(admin);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> TokenniYozAsync(HttpClient client, string value)
        => await client.PutAsJsonAsync(
            SettingsUri,
            new { items = new[] { new { key = SystemSettingKeys.BotToken, value } } },
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Tokenni o'chiradi — testlar bir-birining holatini ko'rmasligi uchun.
    /// </summary>
    /// <remarks>
    /// Baza test SINFI uchun bitta, testlar esa parallel ishlaydi. Bu
    /// yerda «tozalash» to'liq izolyatsiya bermaydi va bermasligi ham
    /// kerak emas: tokenga tegadigan testlar bir-birining natijasini
    /// buzmaydi (har biri o'zi yozib, o'zi o'qiydi), tozalash esa
    /// KEYINGI testlar «qoldiq» tokenni ko'rmasligi uchun.
    /// </remarks>
    private static async Task TokenniTozalaAsync(HttpClient client)
    {
        using var _ = await TokenniYozAsync(client, string.Empty);
    }

    private static async Task<IReadOnlyList<SettingResponse>> SozlamalarAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(SettingsUri, TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var royxat = await javob.Content.ReadFromJsonAsync<List<SettingResponse>>(
            TestContext.Current.CancellationToken);

        royxat.ShouldNotBeNull();
        return royxat;
    }

    private async Task<HttpClient> AdminMijozAsync()
        => await KirishAsync(BonviApiFactory.TestAdminLogin, BonviApiFactory.TestAdminPassword);

    private async Task<HttpClient> BotMijozAsync()
        => await KirishAsync(BonviApiFactory.TestBotLogin, BonviApiFactory.TestBotPassword);

    private async Task<HttpClient> KirishAsync(string login, string parol)
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            LoginUri,
            new { login, password = parol },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
