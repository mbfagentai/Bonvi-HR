using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Rad etish xabari va suhbat taklifi — har nomzodga BITTA urinish
/// (egasining qarori, 2026-10-05: «1 marta borsa yetadi, qayta-qayta
/// jo'natishi shart emas»).
/// </summary>
/// <remarks>
/// Ilgari Telegram rad etgan xabar (nomzod botni bloklagan) belgilanmasdi va
/// bot uni har ~30 soniyada qayta urardi — kuniga ~6000 marta. Navbat eng
/// eskisidan 20 tadan olinardi, ya'ni 20 dan ortiq bloklagan nomzod yig'ilsa
/// YANGI rad xabarlari umuman ketmay qolardi.
/// </remarks>
public sealed class XabarnomaTests
{
    private const long Oddiy = 5001;
    private const long Bloklagan = 5002;

    private static RejectionNotice Rad(long chat, string ism = "Каримов Азиз")
        => new(Guid.NewGuid(), chat, "uz-Latn", ism, "Kassir");

    private static InterviewNotice Taklif(long chat)
        => new(Guid.NewGuid(), chat, "uz-Latn", "Каримов Азиз", "Kassir", new DateTimeOffset(2026, 10, 7, 5, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Rad_xabari_yetkazilsa_bir_marta_belgilanadi()
    {
        using var bot = new BotHarness();
        var xabar = Rad(Oddiy);
        bot.PendingRejections.Add(xabar);

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Oddiy]);
        bot.Marked.ShouldBe([("rejection", xabar.CandidateId, true)]);
        bot.Last.Text.ShouldBe(BotTexts.RejectionNotice(xabar.FullName));
    }

    [Fact]
    public async Task Botni_bloklaganga_bitta_urinish_va_yetkazilmadi_deb_belgilanadi()
    {
        using var bot = new BotHarness();
        bot.BlockedChats.Add(Bloklagan);
        var xabar = Rad(Bloklagan);
        bot.PendingRejections.Add(xabar);

        await bot.NoticesAsync();
        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Bloklagan], "qayta-qayta yuborilmaydi");
        bot.Marked.ShouldBe([("rejection", xabar.CandidateId, false)]);
        bot.PendingRejections.ShouldBeEmpty("navbatdan chiqadi");
    }

    [Fact]
    public async Task Bloklaganlar_navbatni_tiqib_qoymaydi()
    {
        using var bot = new BotHarness();

        // Navbat chegarasidan ko'p bloklagan nomzod — eng eskilari
        for (var i = 0; i < BotHarness.NoticeBatch + 5; i++)
        {
            bot.BlockedChats.Add(9000 + i);
            bot.PendingRejections.Add(Rad(9000 + i));
        }

        var yangi = Rad(Oddiy);
        bot.PendingRejections.Add(yangi);

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.Marked.ShouldContain(("rejection", yangi.CandidateId, true), "yangi nomzod javobini oladi");
        bot.SendAttempts.Count.ShouldBe(BotHarness.NoticeBatch + 6, "har biriga aynan bitta urinish");
        bot.PendingRejections.ShouldBeEmpty();
    }

    [Fact]
    public async Task Telegramga_yetib_bolmasa_xabar_navbatda_qoladi_va_keyin_bir_marta_ketadi()
    {
        using var bot = new BotHarness();
        var xabar = Rad(Oddiy);
        bot.PendingRejections.Add(xabar);

        bot.TelegramUnreachable = true;
        await bot.NoticesAsync();

        bot.Marked.ShouldBeEmpty("xabar ketmagan — belgilanmaydi");
        bot.PendingRejections.ShouldContain(xabar);

        bot.TelegramUnreachable = false;
        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.Marked.ShouldBe([("rejection", xabar.CandidateId, true)]);
        bot.Sent.Count(yuborilgan => yuborilgan.Text == BotTexts.RejectionNotice(xabar.FullName)).ShouldBe(1);
    }

    [Fact]
    public async Task Invitation_carries_the_map_point()
    {
        using var bot = new BotHarness { Location = "40.50603882, 70.93108974" };
        bot.PendingInterviews.Add(Taklif(Oddiy));

        await bot.NoticesAsync();

        bot.LocationsSent.ShouldBe([Oddiy]);
    }

    [Fact]
    public async Task Reminder_goes_once_with_the_map_point()
    {
        using var bot = new BotHarness { Location = "40.50603882, 70.93108974" };
        var eslatma = Taklif(Oddiy);
        bot.PendingReminders.Add(eslatma);

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Oddiy]);
        bot.LocationsSent.ShouldBe([Oddiy]);
        bot.Marked.ShouldBe([("reminder", eslatma.CandidateId, true)]);
        bot.Last.Text.ShouldContain("Eslatma");
        bot.Last.Text.ShouldContain("07.10.2026, 10:00", customMessage: "Tashkent time");
    }

    [Fact]
    public async Task Reminder_to_a_blocked_chat_is_one_attempt_without_a_point()
    {
        using var bot = new BotHarness { Location = "40.50603882, 70.93108974" };
        bot.BlockedChats.Add(Bloklagan);
        var eslatma = Taklif(Bloklagan);
        bot.PendingReminders.Add(eslatma);

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Bloklagan]);
        bot.LocationsSent.ShouldBeEmpty();
        bot.Marked.ShouldBe([("reminder", eslatma.CandidateId, false)]);
    }

    [Fact]
    public async Task Reminder_stays_queued_while_telegram_is_unreachable()
    {
        using var bot = new BotHarness { TelegramUnreachable = true };
        bot.PendingReminders.Add(Taklif(Oddiy));

        await bot.NoticesAsync();

        bot.Marked.ShouldBeEmpty();
        bot.PendingReminders.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Start_with_a_valid_token_links_and_confirms_instead_of_asking_language()
    {
        using var bot = new BotHarness { ValidLinkToken = "0123456789abcdef0123456789abcdef", LinkedLanguage = "ru" };

        await bot.StartAsync("/start 0123456789abcdef0123456789abcdef", "someone");

        bot.LinkRequests.Count.ShouldBe(1);
        bot.Last.Text.ShouldBe(BotTexts.TelegramLinked(BotLanguage.Russian));
        bot.MatchRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Start_with_an_old_token_starts_as_usual()
    {
        using var bot = new BotHarness();

        await bot.StartAsync("/start 0123456789abcdef0123456789abcdef", null);

        bot.LinkRequests.Count.ShouldBe(1);
        bot.Last.Text.ShouldBe(BotTexts.ChooseLanguage);
    }

    [Fact]
    public async Task Plain_start_offers_the_username_and_goes_on_as_usual()
    {
        using var bot = new BotHarness();

        await bot.StartAsync("/start", "Azizbek_K");

        bot.LinkRequests.ShouldBeEmpty();
        bot.MatchRequests.ShouldHaveSingleItem().ShouldContain("Azizbek_K");
        bot.Last.Text.ShouldBe(BotTexts.ChooseLanguage);
    }

    [Fact]
    public async Task Start_without_a_username_asks_the_server_nothing()
    {
        using var bot = new BotHarness();

        await bot.StartAsync("/start", null);

        bot.MatchRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Suhbat_taklifi_bloklaganga_bir_marta_va_joylashuvsiz()
    {
        using var bot = new BotHarness();
        bot.BlockedChats.Add(Bloklagan);
        var taklif = Taklif(Bloklagan);
        bot.PendingInterviews.Add(taklif);

        await bot.NoticesAsync();
        await bot.NoticesAsync();

        bot.SendAttempts.ShouldBe([Bloklagan]);
        bot.Marked.ShouldBe([("interview", taklif.CandidateId, false)]);
        bot.LocationsSent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Xabarnoma_menyusida_yopiq_umumiy_anketa_tugmasi_yoq()
    {
        using var bot = new BotHarness { GeneralPoolOpen = false };
        bot.PendingRejections.Add(Rad(Oddiy));

        await bot.NoticesAsync();

        var menyu = bot.Last.Menu.ShouldNotBeNull();
        menyu.ShouldContain(BotTexts.VacanciesButton(BotLanguage.Latin));
        menyu.ShouldNotContain(BotTexts.QuestionnaireButton(BotLanguage.Latin));
    }
}
