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
}
