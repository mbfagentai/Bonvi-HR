using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;
using Bonvi.Bot.Telegram;

namespace Bonvi.Bot.Tests;

/// <summary>Questions and vacancy work stay in the private chat (SPEC-kengash-guruhi §1).</summary>
public sealed class GuruhTests
{
    private const long Guruh = -100123;

    private static TelegramUpdate Xabar(string? matn, TelegramContact? kontakt = null, string turi = "supergroup")
        => new(1, new TelegramMessage(1, new TelegramChat(Guruh, turi), matn, kontakt, From: new TelegramUser("aziz", 77)), null);

    [Theory]
    [InlineData("/start")]
    [InlineData("/start abc")]
    [InlineData("/start@Bonvi_HR_bot")]
    [InlineData("Vakansiyalar")]
    [InlineData("Kassir")]
    public async Task Guruhda_bot_javob_bermaydi(string matn)
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(Xabar(matn));

        bot.Sent.ShouldBeEmpty();
        bot.MatchRequests.ShouldBeEmpty();
        bot.LinkRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Guruhda_kontakt_ishlanmaydi()
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(Xabar(null, new TelegramContact("+998901234567", 77)));

        bot.Sent.ShouldBeEmpty();
        bot.MatchRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Guruhda_tugma_bosilsa_faqat_soat_ochiladi()
    {
        using var bot = new BotHarness();
        var update = new TelegramUpdate(
            1,
            null,
            new TelegramCallbackQuery("cb", "lang:latn", new TelegramMessage(1, new TelegramChat(Guruh, "group"), null, null)));

        await bot.HandleAsync(update);

        bot.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Shaxsiy_chatda_start_ishlaydi()
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(new TelegramUpdate(
            1,
            new TelegramMessage(1, new TelegramChat(BotHarness.ChatId, "private"), "/start", null),
            null));

        bot.Sent.ShouldNotBeEmpty();
    }

    private static TelegramUpdate Ulash(string matn, long guruh = Guruh)
        => new(1, new TelegramMessage(1, new TelegramChat(guruh, "supergroup", "Kengash"), matn, null), null);

    [Fact]
    public async Task Togri_kod_guruhni_ulaydi_va_tasdiqlaydi()
    {
        using var bot = new BotHarness { CouncilBindAccepted = true };

        await bot.HandleAsync(Ulash("/kengash_ulash@Bonvi_HR_bot 12345678"));

        bot.CouncilCalls.ShouldHaveSingleItem().ShouldContain("12345678");
        bot.CouncilCalls[0].ShouldContain(Guruh.ToString(System.Globalization.CultureInfo.InvariantCulture));
        bot.Last.Text.ShouldBe(BotTexts.CouncilBound);
    }

    [Fact]
    public async Task Notogri_kod_guruhni_ulamaydi()
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(Ulash("/kengash_ulash 111"));

        bot.Last.Text.ShouldBe(BotTexts.CouncilBindFailed);
    }

    [Fact]
    public async Task Boshqa_guruhdan_chiqib_ketadi_kengash_guruhida_jim_turadi()
    {
        using var bot = new BotHarness { CouncilState = "{\"chatId\":" + Guruh + "}" };
        await bot.NoticesAsync();

        await bot.HandleAsync(Xabar("/start"));
        bot.TelegramCalls.ShouldBeEmpty();

        await bot.HandleAsync(new TelegramUpdate(1, null, null, new TelegramChatMemberUpdate(new TelegramChat(-999, "group"))));

        var chiqish = bot.TelegramCalls.ShouldHaveSingleItem();
        chiqish.Method.ShouldBe("leaveChat");
        chiqish.Body.ShouldContain("-999");
    }

    [Fact]
    public async Task Kod_ochiq_bolsa_yangi_guruhdan_chiqib_ketmaydi()
    {
        using var bot = new BotHarness { CouncilState = "{\"chatId\":" + Guruh + "}" };
        await bot.NoticesAsync();

        // The code was issued after the last round, so only a fresh read knows it.
        bot.CouncilState = "{\"chatId\":" + Guruh + ",\"bindOpen\":true}";
        await bot.HandleAsync(new TelegramUpdate(1, null, null, new TelegramChatMemberUpdate(new TelegramChat(-999, "group"))));

        bot.TelegramCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Server_javob_bermasa_guruhdan_chiqib_ketmaydi()
    {
        using var bot = new BotHarness { CouncilState = "{\"chatId\":" + Guruh + "}" };
        await bot.NoticesAsync();

        bot.CouncilState = "not json";
        await bot.HandleAsync(new TelegramUpdate(1, null, null, new TelegramChatMemberUpdate(new TelegramChat(-999, "group"))));

        bot.TelegramCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Superguruhga_otsa_chiqib_ketmaydi_va_yangi_raqam_saqlanadi()
    {
        const long Yangi = -100999;
        using var bot = new BotHarness { CouncilState = "{\"chatId\":" + Guruh + "}" };
        await bot.NoticesAsync();

        await bot.HandleAsync(new TelegramUpdate(1, new TelegramMessage(1, new TelegramChat(Yangi, "supergroup"), null, null, MigrateFromChatId: Guruh), null));

        bot.TelegramCalls.ShouldBeEmpty();
        var sorov = bot.CouncilCalls.ShouldHaveSingleItem();
        sorov.ShouldContain(Guruh.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sorov.ShouldContain(Yangi.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // The same news from the old chat's side, and a following message in the new chat, change nothing else.
        await bot.HandleAsync(new TelegramUpdate(2, new TelegramMessage(2, new TelegramChat(Guruh, "group"), null, null, MigrateToChatId: Yangi), null));
        await bot.HandleAsync(new TelegramUpdate(3, new TelegramMessage(3, new TelegramChat(Yangi, "supergroup"), "salom", null), null));

        bot.TelegramCalls.ShouldBeEmpty();
        bot.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Belgi_saqlanmasa_ikkinchi_xabar_ketmaydi_belgi_qayta_yuboriladi()
    {
        using var bot = new BotHarness { CouncilState = Holat(null, null, BittaNomzod), CouncilMarkFails = true };

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Guruh]);
        bot.CouncilCalls.Count.ShouldBe(2);

        bot.CouncilMarkFails = false;
        await bot.NoticesAsync();
        bot.CouncilCalls.Count.ShouldBe(3);
        bot.SendAttempts.ShouldBe([Guruh]);
    }

    [Fact]
    public async Task Ozgarmagan_tahrir_yangi_xabar_ochmaydi()
    {
        using var bot = new BotHarness { CouncilState = Holat(null, 55, BittaNomzod), EditNotModified = true };

        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBeEmpty();
        bot.CouncilCalls.ShouldHaveSingleItem();
    }

    private static readonly Guid Nomzod = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static string Holat(string? miniApp = null, long? xabar = null, string royxat = "[]")
    {
        var nom = miniApp is null ? "null" : "\"" + miniApp + "\"";
        var raqam = xabar is null ? "null" : xabar.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return "{\"chatId\":" + Guruh + ",\"botUsername\":\"Bonvi_HR_bot\",\"miniAppName\":" + nom
            + ",\"digest\":{\"date\":\"2026-11-07\",\"isToday\":false,\"messageId\":" + raqam
            + ",\"signature\":\"s1\",\"rows\":" + royxat + "}}";
    }

    private static string BittaNomzod => $$"""[{"candidateId":"{{Nomzod}}","name":"Каримов Азиз","at":"2026-11-07T05:00:00Z"}]""";

    [Fact]
    public async Task Jamlama_bitta_xabar_har_nomzodga_tugma_mini_app_bilan()
    {
        using var bot = new BotHarness { CouncilState = Holat("kengash", null, BittaNomzod) };

        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Guruh]);
        bot.Last.Text.ShouldContain("Эртага кенгаш — 7 ноябрь");
        bot.CouncilCalls.ShouldHaveSingleItem().ShouldContain("s1");

        var tugmalar = BotWorker.CouncilButtons(
            new CouncilDigest(new DateOnly(2026, 11, 7), false, null, "s1", [new CouncilDigestRow(Nomzod, "Каримов Азиз", new DateTimeOffset(2026, 11, 7, 5, 0, 0, TimeSpan.Zero))]),
            "Bonvi_HR_bot",
            "kengash");

        tugmalar.Count.ShouldBe(2);
        tugmalar[0][0].Text.ShouldBe("Каримов Азиз · 10:00");
        tugmalar[0][0].Url.ShouldBe($"https://t.me/Bonvi_HR_bot/kengash?startapp={Nomzod:N}");
        tugmalar[1][0].Url.ShouldBe("https://t.me/Bonvi_HR_bot/kengash");
    }

    [Fact]
    public void Mini_app_nomi_bolmasa_tugma_shaxsiy_chatga_olib_boradi()
    {
        var tugmalar = BotWorker.CouncilButtons(
            new CouncilDigest(new DateOnly(2026, 11, 7), false, null, "s1", [new CouncilDigestRow(Nomzod, "Каримов Азиз", DateTimeOffset.UnixEpoch)]),
            "Bonvi_HR_bot",
            null);

        tugmalar[0][0].Url.ShouldBe($"https://t.me/Bonvi_HR_bot?start=km_{Nomzod:N}");
        tugmalar[1][0].Url.ShouldBe("https://t.me/Bonvi_HR_bot?start=km_all");
    }

    [Fact]
    public async Task Royxat_ozgarsa_yangi_xabar_emas_tahrir()
    {
        using var bot = new BotHarness { CouncilState = Holat(null, 55, BittaNomzod) };

        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBeEmpty();
        bot.TelegramCalls.ShouldHaveSingleItem().Method.ShouldBe("editMessageText");
        bot.CouncilCalls.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Xabar_ochirilgan_bolsa_qayta_yuboriladi()
    {
        using var bot = new BotHarness { CouncilState = Holat(null, 55, BittaNomzod), EditRefused = true };

        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Guruh]);
        bot.CouncilCalls.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Guruh_rad_etsa_har_aylanishda_urilmaydi()
    {
        using var bot = new BotHarness { CouncilState = Holat(null, null, BittaNomzod) };
        bot.BlockedChats.Add(Guruh);

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Guruh]);
        bot.CouncilCalls.ShouldBeEmpty();
    }

    private const long Azo = BotHarness.ChatId;

    private static TelegramUpdate Shaxsiy(string? matn, TelegramContact? kontakt = null, long kim = Azo)
        => new(1, new TelegramMessage(1, new TelegramChat(Azo, "private"), matn, kontakt, From: new TelegramUser("azo", kim)), null);

    [Fact]
    public async Task Azo_raqamini_yuborsa_taniladi_va_mini_app_tugmasi_chiqadi()
    {
        using var bot = new BotHarness { CouncilLinkAnswer = """{"linked":true,"fullName":"Shef"}""" };

        await bot.HandleAsync(Shaxsiy("/start kengash"));
        bot.Last.RequestsContact.ShouldBeTrue();
        bot.MatchRequests.ShouldBeEmpty();
        bot.LinkRequests.ShouldBeEmpty();

        await bot.HandleAsync(Shaxsiy(null, new TelegramContact("+998901234567", Azo)));

        bot.CouncilCalls.ShouldHaveSingleItem().ShouldContain("998901234567");
        bot.Last.Text.ShouldBe(BotTexts.CouncilLinked("Shef"));
        bot.State.ShouldBeNull();
    }

    [Fact]
    public async Task Birovning_raqami_qabul_qilinmaydi()
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(Shaxsiy("/start kengash"));
        await bot.HandleAsync(Shaxsiy(null, new TelegramContact("+998901234567", 999)));

        bot.CouncilCalls.ShouldBeEmpty();
        bot.Last.Text.ShouldBe(BotTexts.CouncilContactNotOwn);
    }

    [Fact]
    public async Task Royxatda_yoq_raqam_aytiladi()
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(Shaxsiy("/start kengash"));
        await bot.HandleAsync(Shaxsiy(null, new TelegramContact("+998901234567", Azo)));

        bot.Last.Text.ShouldBe(BotTexts.CouncilContactUnknown);
    }

    [Fact]
    public async Task Nomzod_tugmasi_shaxsiy_chatda_ochish_tugmasini_beradi()
    {
        using var bot = new BotHarness();

        await bot.HandleAsync(Shaxsiy($"/start km_{Nomzod:N}"));

        bot.Last.Text.ShouldBe(BotTexts.CouncilOpenText);
        bot.LinkRequests.ShouldBeEmpty();
    }
}
