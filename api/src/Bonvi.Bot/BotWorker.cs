using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;
using Bonvi.Bot.Telegram;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Bot;

/// <summary>
/// Botning asosiy halqasi: Telegram'dan xabar oladi, javob yozadi va
/// tugallangan arizani API ga yuboradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Uch qadam, ortiqchasi yo'q</b> (foydalanuvchi qarori): til →
/// vakansiya → F.I.Sh. → telefon. Har bir qo'shimcha savol arizani
/// tashlab ketish ehtimolini oshiradi, qolgan ma'lumotni esa HR
/// telefon suhbatida baribir so'raydi.
/// </para>
/// <para>
/// <b>Xato halqani to'xtatmaydi.</b> Bitta suhbatdagi muammo qolganlarga
/// tegmasligi kerak: istisno tutiladi, jurnalga yoziladi va bot ishlashda
/// davom etadi.
/// </para>
/// </remarks>
public sealed partial class BotWorker : BackgroundService
{
    [LoggerMessage(EventId = 3201, Level = LogLevel.Error, Message = "Telegram token qabul qilinmadi — bot to'xtatildi")]
    private partial void LogBadToken();

    [LoggerMessage(
        EventId = 3207,
        Level = LogLevel.Warning,
        Message = "Bot SOZLANMAGAN — {Missing} berilmagan. «.env» faylini to'ldiring: TELEGRAM__BOTTOKEN, "
            + "BOOTSTRAP__BOTLOGIN, BOOTSTRAP__BOTPASSWORD. Tizimning qolgan qismi bundan ta'sirlanmaydi.")]
    private partial void LogNotConfigured(string missing);

    [LoggerMessage(
        EventId = 3202,
        Level = LogLevel.Warning,
        Message = "Bonvi API ga kirib bo'lmadi — birinchi arizada qayta uriniladi")]
    private partial void LogApiUnavailable();

    [LoggerMessage(EventId = 3203, Level = LogLevel.Information, Message = "Bot ishga tushdi: @{Username}")]
    private partial void LogStarted(string username);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Error, Message = "Yangilikka ishlov berilmadi: {UpdateId}")]
    private partial void LogUpdateFailed(Exception exception, long updateId);

    [LoggerMessage(EventId = 3205, Level = LogLevel.Debug, Message = "{Count} ta eskirgan suhbat unutildi")]
    private partial void LogExpired(int count);

    [LoggerMessage(EventId = 3206, Level = LogLevel.Information, Message = "Yangi ariza: «{Vacancy}»")]
    private partial void LogApplication(string vacancy);

    /// <summary>Tugma ma'lumotidagi ajratgich: «v:<c>guid</c>».</summary>
    private const string VacancyPrefix = "v:";

    private const string LanguageCyrillicData = "lang:cyrl";
    private const string LanguageLatinData = "lang:latn";

    private const string MenuApplyData = "menu:apply";
    private const string MenuAboutData = "menu:about";

    private readonly TelegramClient _telegram;
    private readonly BonviClient _bonvi;
    private readonly ConversationStore _conversations;
    private readonly BotOptions _options;
    private readonly ILogger<BotWorker> _logger;

    private long _offset;

    public BotWorker(
        TelegramClient telegram,
        BonviClient bonvi,
        ConversationStore conversations,
        IOptions<BotOptions> options,
        ILogger<BotWorker> logger)
    {
        _telegram = telegram;
        _bonvi = bonvi;
        _conversations = conversations;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        /*
          Sozlanmagan bot — XATO EMAS, holat: tizim botsiz ham to'liq
          ishlaydi. Shu sababli jarayon yiqilmaydi va istisno tashlamaydi
          (`BotOptions.Token` izohi): u sababni bir marta yozib, to'xtaydi.
          `BackgroundService` dan chiqish qolgan konteynerga tegmaydi.
        */
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Token)) missing.Add("Telegram tokeni");
        if (string.IsNullOrWhiteSpace(_options.Login)) missing.Add("bot hisobi logini");
        if (string.IsNullOrWhiteSpace(_options.Password)) missing.Add("bot hisobi paroli");

        if (missing.Count > 0)
        {
            LogNotConfigured(string.Join(", ", missing));
            return;
        }

        // Webhook faol bo'lsa `getUpdates` umuman ishlamaydi (izohi
        // `TelegramClient.DeleteWebhookAsync` da).
        await _telegram.DeleteWebhookAsync(stoppingToken).ConfigureAwait(false);

        var username = await _telegram.GetMeAsync(stoppingToken).ConfigureAwait(false);

        if (username is null)
        {
            // Token noto'g'ri bo'lsa halqani aylantirishning ma'nosi yo'q:
            // har so'rov bir xil xato bilan qaytardi va jurnal to'lib ketardi.
            LogBadToken();
            return;
        }

        if (!await _bonvi.SignInAsync(stoppingToken).ConfigureAwait(false))
        {
            // Bu YOMON, lekin halokat emas: API keyinroq ko'tarilishi
            // mumkin va birinchi so'rovda qayta kiriladi.
            LogApiUnavailable();
        }

        LogStarted(username);

        var lastCleanup = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var updates = await _telegram
                .GetUpdatesAsync(_offset, _options.PollTimeoutSeconds, stoppingToken)
                .ConfigureAwait(false);

            foreach (var update in updates)
            {
                try
                {
                    await HandleAsync(update, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogUpdateFailed(ex, update.UpdateId);
                }

                // Tasdiqlash faqat ishlov berilgandan KEYIN (izohi
                // `TelegramClient.GetUpdatesAsync` da).
                _offset = update.UpdateId + 1;
            }

            if (DateTimeOffset.UtcNow - lastCleanup > TimeSpan.FromMinutes(10))
            {
                var removed = _conversations.RemoveExpired();
                lastCleanup = DateTimeOffset.UtcNow;

                if (removed > 0)
                {
                    LogExpired(removed);
                }
            }
        }
    }

    private async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { } callback)
        {
            await HandleCallbackAsync(callback, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (update.Message is { } message)
        {
            await HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleCallbackAsync(TelegramCallbackQuery callback, CancellationToken cancellationToken)
    {
        // Birinchi navbatda tugmaning «soatini» o'chiramiz — javob
        // kechiksa ham foydalanuvchi bosish qabul qilinganini ko'radi.
        await _telegram.AnswerCallbackAsync(callback.Id, cancellationToken).ConfigureAwait(false);

        if (callback.Message?.Chat is not { } chat || callback.Data is not { } data)
        {
            return;
        }

        var state = _conversations.Find(chat.Id);

        if (data is LanguageCyrillicData or LanguageLatinData)
        {
            var language = data == LanguageCyrillicData ? BotLanguage.Cyrillic : BotLanguage.Latin;

            _conversations.Save(chat.Id, new ConversationState
            {
                Step = ConversationStep.Vacancy,
                Language = language,
            });

            await _telegram
                .SendMessageAsync(chat.Id, BotTexts.Greeting(language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            /*
              Kompaniya matni sozlanmagan bo'lsa MENYU umuman
              ko'rsatilmaydi va nomzod to'g'ridan-to'g'ri vakansiyalarni
              ko'radi: bitta tugmali menyu qadamni bekorga uzaytirardi
              (izohi `BotOptions.AboutCyrillic` da).
            */
            if (About(language) is null)
            {
                await ShowVacanciesAsync(chat.Id, language, cancellationToken).ConfigureAwait(false);
                return;
            }

            await ShowMenuAsync(chat.Id, language, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (data is MenuApplyData or MenuAboutData)
        {
            var language = state?.Language ?? BotLanguage.Cyrillic;

            if (data == MenuAboutData)
            {
                await _telegram
                    .SendMessageAsync(
                        chat.Id,
                        (About(language) ?? string.Empty) + BotTexts.AfterAbout(language),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Menyu QAYTA ko'rsatiladi: matnni o'qigan odam odatda
                // shu yerdayoq ariza topshirmoqchi bo'ladi va uni
                // `/start` yozishga majburlash ortiqcha to'siq.
                await ShowMenuAsync(chat.Id, language, cancellationToken).ConfigureAwait(false);
                return;
            }

            await ShowVacanciesAsync(chat.Id, language, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!data.StartsWith(VacancyPrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (state is null)
        {
            // Bot qayta ishga tushgan yoki suhbat eskirgan: eski tugma
            // hamon ekranda turibdi va bosilishi mumkin.
            await _telegram
                .SendMessageAsync(chat.Id, BotTexts.Restart(BotLanguage.Cyrillic), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (!Guid.TryParse(data[VacancyPrefix.Length..], out var vacancyId))
        {
            return;
        }

        var vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);
        var chosen = vacancies.FirstOrDefault(v => v.Id == vacancyId);

        if (chosen is null)
        {
            // Vakansiya tugmani bosgunicha yopilgan bo'lishi mumkin.
            await ShowVacanciesAsync(chat.Id, state.Language, cancellationToken).ConfigureAwait(false);
            return;
        }

        _conversations.Save(chat.Id, state with
        {
            Step = ConversationStep.Name,
            VacancyId = chosen.Id,
            VacancyTitle = chosen.Title,
        });

        await _telegram
            .SendMessageAsync(chat.Id, BotTexts.AskName(state.Language), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        // `/start` HAR QANDAY qadamda suhbatni boshidan boshlaydi —
        // adashib qolgan odam uchun yagona ishonchli chiqish yo'li.
        if (message.Text?.StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true)
        {
            _conversations.Save(chatId, new ConversationState { Step = ConversationStep.Language });

            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.ChooseLanguage,
                    [
                        [new InlineButton(BotTexts.LanguageCyrillic, LanguageCyrillicData)],
                        [new InlineButton(BotTexts.LanguageLatin, LanguageLatinData)],
                    ],
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var state = _conversations.Find(chatId);

        if (state is null)
        {
            await _telegram
                .SendMessageAsync(chatId, BotTexts.Restart(BotLanguage.Cyrillic), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        switch (state.Step)
        {
            case ConversationStep.Name:
                await HandleNameAsync(chatId, state, message.Text, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.Phone:
                await HandlePhoneAsync(chatId, state, message, cancellationToken).ConfigureAwait(false);
                break;

            default:
                // Til yoki vakansiya kutilyapti — javob tugma bilan beriladi.
                await _telegram
                    .SendMessageAsync(chatId, BotTexts.Restart(state.Language), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleNameAsync(
        long chatId,
        ConversationState state,
        string? text,
        CancellationToken cancellationToken)
    {
        var name = NameParser.TryParse(text);

        if (name is null)
        {
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.NameNotUnderstood(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        _conversations.Save(chatId, state with { Step = ConversationStep.Phone, Name = name });

        await _telegram
            .SendMessageAsync(
                chatId,
                BotTexts.AskPhone(state.Language),
                requestContact: true,
                contactButtonText: BotTexts.PhoneButton(state.Language),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task HandlePhoneAsync(
        long chatId,
        ConversationState state,
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Contact is not { } contact)
        {
            // Raqam qo'lda yozilgan: qabul qilmaymiz. Terish xatosi
            // butun arizani qidirib bo'lmaydigan qilib qo'yardi va HR
            // nomzodga umuman qo'ng'iroq qila olmasdi.
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.PhoneButtonOnly(state.Language),
                    requestContact: true,
                    contactButtonText: BotTexts.PhoneButton(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        // Telegram'da BOSHQA odamning kontaktini ham yuborib bo'ladi
        // (izohi `TelegramContact` da). Bunda ariza begona raqam bilan
        // yozilib, HR noto'g'ri odamga qo'ng'iroq qilardi.
        if (contact.UserId is null || contact.UserId != chatId)
        {
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.OwnPhoneOnly(state.Language),
                    requestContact: true,
                    contactButtonText: BotTexts.PhoneButton(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (state.Name is not { } name)
        {
            await _telegram
                .SendMessageAsync(chatId, BotTexts.Restart(state.Language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var phone = contact.PhoneNumber;

        var existing = await _bonvi
            .FindActiveApplicationAsync(phone, state.VacancyId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _conversations.Forget(chatId);

            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.AlreadyApplied(state.Language, state.VacancyTitle, existing.AppliedAt, existing.StageName),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var created = await _bonvi
            .RegisterCandidateAsync(
                state.VacancyId,
                name.LastName,
                name.FirstName,
                name.MiddleName,
                phone,
                cancellationToken)
            .ConfigureAwait(false);

        if (!created)
        {
            // Suhbat SAQLANADI: nomzod /start bosmasdan qayta urinib
            // ko'rishi uchun raqamni yana yuborsa kifoya.
            await _telegram
                .SendMessageAsync(chatId, BotTexts.Failed(state.Language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        _conversations.Forget(chatId);

        await _telegram
            .SendMessageAsync(
                chatId,
                BotTexts.Accepted(state.Language, state.VacancyTitle),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        LogApplication(state.VacancyTitle);
    }

    /// <summary>
    /// Tanlangan tildagi kompaniya matni — sozlanmagan bo'lsa <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Lotin matni bo'sh bo'lsa kirillcha ishlatiladi: bitta matn yozib
    /// qo'yish ikkitasini majburlashdan foydaliroq (izohi
    /// <c>BotOptions.AboutLatin</c> da).
    /// </remarks>
    private string? About(BotLanguage language)
    {
        var text = language == BotLanguage.Latin && !string.IsNullOrWhiteSpace(_options.AboutLatin)
            ? _options.AboutLatin
            : _options.AboutCyrillic;

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        /*
          `.env` va docker-compose yangi QATORNI uzata olmaydi: ular
          qiymatni bir satrda saqlaydi va `\n` ikkita BELGI bo'lib keladi
          (teskari chiziq va «n»). Shu sababli u shu yerda haqiqiy qator
          ko'chishiga aylantiriladi — aks holda kompaniya matni Telegram'da
          bitta uzun satr bo'lib chiqardi.
        */
        return text
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Bosh menyu: ariza topshirish yoki kompaniya haqida.</summary>
    private async Task ShowMenuAsync(long chatId, BotLanguage language, CancellationToken cancellationToken)
        => await _telegram
            .SendMessageAsync(
                chatId,
                BotTexts.MenuPrompt(language),
                [
                    [new InlineButton(BotTexts.ApplyButton(language), MenuApplyData)],
                    [new InlineButton(BotTexts.AboutButton(language), MenuAboutData)],
                ],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    private async Task ShowVacanciesAsync(
        long chatId,
        BotLanguage language,
        CancellationToken cancellationToken)
    {
        var vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);

        if (vacancies.Count == 0)
        {
            _conversations.Forget(chatId);

            await _telegram
                .SendMessageAsync(chatId, BotTexts.NoVacancies(language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        // Har bir vakansiya ALOHIDA qatorda: nomlar uzun («2 линия
        // назратчиси») va ikkitasi bir qatorga sig'maydi.
        var buttons = vacancies
            .Select(vacancy => (IReadOnlyList<InlineButton>)
                [new InlineButton(vacancy.Title, VacancyPrefix + vacancy.Id.ToString("N"))])
            .ToList();

        await _telegram
            .SendMessageAsync(
                chatId,
                BotTexts.ChooseVacancy(language),
                buttons,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
