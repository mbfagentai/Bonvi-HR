using System.Text;
using Bonvi.Bot.Conversation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bonvi.Bot.Tests;

/// <summary>
/// «Ish haqida qayerdan bildingiz?» — 20-qadam serverdagi ma'lumotnomadan
/// (SPEC-qayerdan-bilgan §5).
/// </summary>
/// <remarks>
/// <para>
/// Tugmalar endi botda qattiq yozilmagan: HR kanal qo'shsa, bot o'zgarishsiz uni
/// ko'rsatadi. Aniqlik so'raydigan kanalda («Tanishlar orqali») bitta majburiy qo'shimcha
/// savol bor.
/// </para>
/// <para>
/// Deploy paytida yarim qolgan suhbat (Redis'dagi nusxada eski sanoq qiymati) yiqilmasligi
/// shart: nomzod faqat shu bitta savolga qayta javob beradi.
/// </para>
/// </remarks>
public sealed class QayerdanBilganTests
{
    private const BotLanguage Uz = BotLanguage.Latin;

    [Fact]
    public async Task Aniqlik_soraydigan_kanal_majburiy_qoshimcha_savol_beradi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.ReachReferralAsync(bot);
        await bot.PressAsync(BotHarness.Referral(BotHarness.AcquaintanceChannel));

        bot.Last.Text.ShouldBe(BotTexts.AskReferralDetail(Uz, "Tanishlar orqali"));
        bot.Last.Buttons.ShouldBeEmpty("«O'tkazib yuborish» yo'q — server aniqlikni majburiy qiladi");
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.ReferralDetail);

        // Bo'sh javob — javob emas: savol qayta beriladi
        await bot.TextAsync("   ");

        bot.Last.Text.ShouldBe(BotTexts.AskReferralDetail(Uz, "Tanishlar orqali"));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.ReferralDetail);

        await bot.TextAsync("  Rustamov Bahodir, omborchi ");

        bot.Last.Text.ShouldBe(BotTexts.Offer(Uz, "https://mbf.uz/oferta"));

        await bot.PressAsync("offer");

        // Xulosada — kartadagi shakl: «kanal — aniqlik»
        bot.Last.Text.ShouldContain("Tanishlar orqali — Rustamov Bahodir, omborchi");

        await bot.PressAsync("submit");

        var body = bot.RegisterBody.ShouldNotBeNull().RootElement;
        body.GetProperty("referralChannelId").GetGuid().ShouldBe(BotHarness.AcquaintanceChannel);
        body.GetProperty("referralDetail").GetString().ShouldBe("Rustamov Bahodir, omborchi");
    }

    [Fact]
    public async Task Aniqlik_soramaydigan_kanalda_qoshimcha_savol_yoq()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.ReachReferralAsync(bot);
        await bot.PressAsync(BotHarness.Referral(BotHarness.OlxChannel));

        bot.Last.Text.ShouldBe(BotTexts.Offer(Uz, "https://mbf.uz/oferta"));

        var holat = bot.State.ShouldNotBeNull();
        holat.ReferralChannelId.ShouldBe(BotHarness.OlxChannel);
        holat.ReferralChannelName.ShouldBe("OLX.uz");
        holat.ReferralDetail.ShouldBeNull();
    }

    [Fact]
    public async Task Aniqlik_200_belgidan_uzun_bolsa_qirqiladi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.ReachReferralAsync(bot);
        await bot.PressAsync(BotHarness.Referral(BotHarness.AcquaintanceChannel));
        await bot.TextAsync(new string('a', 250));

        bot.State.ShouldNotBeNull().ReferralDetail!.Length.ShouldBe(Answers.MaxReferralDetailLength, "server uzun matnni rad etardi");
    }

    [Fact]
    public async Task Kanal_almashtirilsa_eski_aniqlik_qolmaydi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.ReachReferralAsync(bot);
        await bot.PressAsync(BotHarness.Referral(BotHarness.AcquaintanceChannel));
        await bot.TextAsync("Rustamov Bahodir");
        await bot.PressAsync("offer");

        // Yuborishda server kanalni rad etdi — savol qayta so'raladi va boshqa kanal tanlanadi
        bot.RegisterPlan.Enqueue("qayerdan.topilmadi");
        await bot.PressAsync("submit");
        await bot.PressAsync(BotHarness.Referral(BotHarness.TelegramChannel));

        var holat = bot.State.ShouldNotBeNull();
        holat.ReferralChannelId.ShouldBe(BotHarness.TelegramChannel);
        holat.ReferralDetail.ShouldBeNull("«Telegram — Rustamov» degan qarama-qarshi qator chiqmasin");
    }

    [Fact]
    public async Task Royxat_kelmasa_qayta_urinish_tugmasi_chiziladi()
    {
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        bot.ReferralChannelsSucceed = false;

        await AnketaOqimiTests.ReachReferralAsync(bot);

        bot.Last.Text.ShouldBe(BotTexts.ReferralUnavailable(Uz));
        bot.Last.Buttons.ShouldBe(["src:retry"]);
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.ReferralSource, "javoblar joyida, qadam o'zgarmaydi");

        bot.ReferralChannelsSucceed = true;
        await bot.PressAsync("src:retry");

        bot.Last.Text.ShouldBe(BotTexts.AskReferralSource(Uz));
        bot.HasButton(BotHarness.Referral(BotHarness.TelegramChannel)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("src:Telegram")]
    [InlineData("src:0199f0a0-5e1d-7c00-8000-0000000000ff")]
    public async Task Eski_tugma_yoki_faolsizlantirilgan_kanal_savolni_yangi_tugmalar_bilan_qayta_chizadi(string tugma)
    {
        // «src:Telegram» — deploydan oldingi xabardagi tugma; GUID — shu orada HR faolsizlantirgan kanal
        using var bot = AnketaOqimiTests.New(withVacancy: false);

        await AnketaOqimiTests.ReachReferralAsync(bot);
        var oldin = bot.Sent.Count;

        await bot.PressAsync(tugma);

        bot.Sent.Count.ShouldBe(oldin + 1);
        bot.Last.Text.ShouldBe(BotTexts.AskReferralSource(Uz));
        bot.HasButton(BotHarness.Referral(BotHarness.TelegramChannel)).ShouldBeTrue();

        var holat = bot.State.ShouldNotBeNull();
        holat.Step.ShouldBe(ConversationStep.ReferralSource);
        holat.ReferralChannelId.ShouldBeNull();
    }

    /// <summary>
    /// ⭐ Deploy paytida yarim qolgan suhbat: Redis'dagi nusxa ESKI shaklda («referralSource»
    /// sanoq qiymati bilan, tasdiq qadamida). Bot yiqilmaydi va anketani boshidan boshlatmaydi —
    /// faqat shu savol qayta so'raladi, oferta esa qayta so'ralmaydi.
    /// </summary>
    [Fact]
    public async Task Deploydan_oldingi_suhbat_yiqilmaydi_faqat_shu_savol_qayta_soraladi()
    {
        var kesh = new XotiraKeshi();
        kesh.Set(
            $"conversation:{BotHarness.ChatId}",
            Encoding.UTF8.GetBytes(EskiHolat),
            new DistributedCacheEntryOptions());

        var nusxa = new RedisConversationBackup(kesh, NullLogger<RedisConversationBackup>.Instance);

        using var bot = AnketaOqimiTests.New(withVacancy: false, nusxa);

        var tiklangan = bot.State.ShouldNotBeNull("eski nusxa o'qiladi — noma'lum kalit tashlanadi");
        tiklangan.Step.ShouldBe(ConversationStep.Confirm);
        tiklangan.Name.ShouldNotBeNull().LastName.ShouldBe("Karimov");
        tiklangan.ReferralChannelId.ShouldBeNull();

        await bot.PressAsync("submit");

        bot.RegisterVacancyIds.ShouldBeEmpty("chala ariza serverga ketmaydi");
        bot.Last.Text.ShouldBe(BotTexts.AskReferralSource(Uz));

        await bot.PressAsync(BotHarness.Referral(BotHarness.TelegramChannel));

        // Oferta allaqachon qabul qilingan — to'g'ridan-to'g'ri tasdiq
        bot.Last.Text.ShouldContain(BotTexts.ConfirmQuestion(Uz));
        bot.State.ShouldNotBeNull().Step.ShouldBe(ConversationStep.Confirm);

        await bot.PressAsync("submit");

        bot.Last.Text.ShouldBe(BotTexts.AcceptedToGeneralPool(Uz));
        bot.RegisterBody.ShouldNotBeNull().RootElement.GetProperty("referralChannelId").GetGuid()
            .ShouldBe(BotHarness.TelegramChannel);
    }

    /// <summary>Deploydan oldingi bot Redis'ga shunday yozgan (sanoqlar — matn).</summary>
    private const string EskiHolat = """
        {
          "step": "Confirm",
          "language": "Latin",
          "vacancyId": null,
          "vacancyTitle": "",
          "name": { "lastName": "Karimov", "firstName": "Aziz", "middleName": null },
          "birthDate": "1990-10-10",
          "address": "Rishton tumani",
          "phone": "+998901234567",
          "education": "HigherComplete",
          "maritalStatus": "Married",
          "russianLevel": "Advanced",
          "hasDriverLicense": false,
          "expectedSalary": "From2To3Million",
          "referralSource": "Telegram",
          "offerAcceptedAt": "2026-10-26T10:00:00+05:00",
          "phoneTypingAllowed": false,
          "updatedAt": "2099-01-01T00:00:00+00:00"
        }
        """;

    /// <summary>Redis o'rnidagi lug'at — nusxaning haqiqiy o'quvchisi (<see cref="RedisConversationBackup"/>) sinaladi.</summary>
    private sealed class XotiraKeshi : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _qiymatlar = [];

        public byte[]? Get(string key) => _qiymatlar.GetValueOrDefault(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _qiymatlar.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _qiymatlar[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
