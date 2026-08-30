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
    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Error,
        Message = "Telegram token QABUL QILINMADI. Bot kutmoqda — «Tizim sozlamalari» ekranida "
            + "tokenni to'g'rilang, bot o'zi qayta ulanadi.")]
    private partial void LogBadToken();

    [LoggerMessage(
        EventId = 3207,
        Level = LogLevel.Warning,
        Message = "Bot hisobi SOZLANMAGAN — {Missing} berilmagan. «.env» faylini to'ldiring: "
            + "BOOTSTRAP__BOTLOGIN, BOOTSTRAP__BOTPASSWORD. Bu ikkisi tizim sozlamalariga "
            + "KO'CHIRILMAYDI: bot aynan shular bilan tizimga kirib tokenni oladi. "
            + "Tizimning qolgan qismi bundan ta'sirlanmaydi.")]
    private partial void LogNotConfigured(string missing);

    [LoggerMessage(
        EventId = 3208,
        Level = LogLevel.Warning,
        Message = "Telegram tokeni QO'YILMAGAN — bot kutmoqda. «Tizim sozlamalari» ekranida token "
            + "kiriting (yoki «.env» dagi TELEGRAM__BOTTOKEN ni to'ldiring). Qayta ishga "
            + "tushirish shart emas.")]
    private partial void LogTokenMissing();

    [LoggerMessage(
        EventId = 3209,
        Level = LogLevel.Information,
        Message = "Telegram tokeni almashtirildi — bot yangi token bilan ulanmoqda")]
    private partial void LogTokenChanged();

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
    private const string LanguageRussianData = "lang:rus";


    private readonly TelegramClient _telegram;
    private readonly TelegramToken _token;
    private readonly BonviClient _bonvi;
    private readonly ConversationStore _conversations;
    private readonly BotOptions _options;
    private readonly ILogger<BotWorker> _logger;

    private long _offset;

    /// <summary>Tizimdan olingan sozlama va u qachon o'qilgani.</summary>
    /// <remarks>
    /// Har `/start` da so'rov yuborish kerak emas — matn kamdan-kam
    /// o'zgaradi. Besh daqiqalik kesh: administrator ekranda
    /// o'zgartirsa, u shu muddat ichida botga yetib boradi va nomzod
    /// buni sezmaydi ham.
    /// </remarks>
    private BotSettings? _settings;
    private DateTimeOffset _settingsReadAt;

    /// <summary>Hozir ISHLAYOTGAN token. <c>null</c> — bot ulanmagan.</summary>
    /// <remarks>
    /// Bu «tizimda yozilgan token» emas, «Telegram qabul qilgan va shu
    /// bilan so'rov ketayotgan token». Ikkalasi farq qilganda bot qayta
    /// ulanadi (<c>EnsureTokenAsync</c>).
    /// </remarks>
    private string? _activeToken;

    /// <summary>Tizimdan oxirgi marta muvaffaqiyatli o'qilgan token.</summary>
    /// <remarks>
    /// <c>null</c> ikki xil ma'no tashiydi va ular
    /// <see cref="_tokenEverRead"/> bilan ajratiladi: hali o'qilmagan,
    /// yoki o'qildi-yu ekranda qo'yilmagan.
    /// </remarks>
    private string? _settingsToken;
    private bool _tokenEverRead;
    private DateTimeOffset _tokenReadAt;

    /// <summary>Telegram rad etgan token — xato JURNALGA bir marta tushsin uchun.</summary>
    /// <remarks>
    /// Halqa har 15 soniyada aylanadi. Bu maydonsiz noto'g'ri token
    /// bir kechada minglab bir xil satr yozib, jurnal saqlash
    /// chegarasini (<c>LOG_MAX_FILE_COUNT</c>) yeb qo'yardi va haqiqiy
    /// xatolarni surib chiqarardi. Qiymatning O'ZI hech qachon
    /// jurnalga tushmaydi — u faqat «bu haqda aytganmidim?» degan
    /// savolga javob berish uchun xotirada turadi.
    /// </remarks>
    private string? _complainedAbout;

    public BotWorker(
        TelegramClient telegram,
        TelegramToken token,
        BonviClient bonvi,
        ConversationStore conversations,
        IOptions<BotOptions> options,
        ILogger<BotWorker> logger)
    {
        _telegram = telegram;
        _token = token;
        _bonvi = bonvi;
        _conversations = conversations;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Token yo'q yoki yaroqsiz bo'lganda qayta urinish oralig'i.</summary>
    /// <remarks>
    /// Qisqa ATAYLAB: bu holatda odam ekran oldida turib tokenni endi
    /// kiritgan bo'ladi va botning tirilishini kutadi. 15 soniya —
    /// «darhol» bilan «API ni bekorga urish» orasidagi o'rta yo'l.
    /// </remarks>
    private static readonly TimeSpan TokenRetryDelay = TimeSpan.FromSeconds(15);

    /// <summary>Ishlab turgan bot tokenni qayta o'qish oralig'i.</summary>
    /// <remarks>
    /// Bot ishlayotganda shoshilishning hojati yo'q, lekin bir daqiqa
    /// «matn» sozlamasidagi besh daqiqadan qisqa: matn eskirsa nomzod
    /// bir marta eski matnni o'qiydi, token eskirsa esa bot BEGONA
    /// hisobga xabar yuborishda davom etadi — masalan token o'g'irlangan
    /// deb bekor qilingan bo'lsa.
    /// </remarks>
    private static readonly TimeSpan TokenRefresh = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        /*
          ⭐ ISHGA TUSHISH TARTIBI O'ZGARDI: avval BONVI, keyin TELEGRAM.

          Ilgari teskari edi — token `.env` dan kelardi va u bilan darhol
          Telegram'ga ulanish mumkin edi. Endi token tizim sozlamalarida
          turadi (`SystemSettingKeys.BotToken`), ya'ni uni bilish uchun
          avval Bonvi API ga kirish kerak.

          Shundan kelib chiqib «sozlanmagan» tushunchasi ham toraydi: bot
          hisobining logini va paroli bo'lmasa haqiqatan hech nima qilib
          bo'lmaydi va jarayon to'xtaydi. Token esa endi to'xtash sababi
          EMAS — u har lahzada ekranda paydo bo'lishi mumkin.
        */
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Login)) missing.Add("bot hisobi logini");
        if (string.IsNullOrWhiteSpace(_options.Password)) missing.Add("bot hisobi paroli");

        if (missing.Count > 0)
        {
            LogNotConfigured(string.Join(", ", missing));
            return;
        }

        if (!await _bonvi.SignInAsync(stoppingToken).ConfigureAwait(false))
        {
            // Bu YOMON, lekin halokat emas: API keyinroq ko'tarilishi
            // mumkin va birinchi so'rovda qayta kiriladi.
            LogApiUnavailable();
        }

        var lastCleanup = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            /*
              Har aylanishda tekshiriladi: token bormi, o'zgardimi.
              Yo'q yoki yaroqsiz bo'lsa — kutib, qaytadan uriniladi.

              NEGA `return` emas (ilgari shunday edi): tokenni endi ekrandan
              qo'yish mumkin va bot buni sezishi kerak. `return` bilan
              «tokenni kiritdim, nega ishlamayapti?» degan savolning
              yagona javobi «konteynerni qayta ishga tushiring» bo'lardi —
              ya'ni butun o'zgarishning ma'nosi yo'qolardi.
            */
            if (!await EnsureTokenAsync(stoppingToken).ConfigureAwait(false))
            {
                await Task.Delay(TokenRetryDelay, stoppingToken).ConfigureAwait(false);
                continue;
            }

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

        if (data is LanguageCyrillicData or LanguageLatinData or LanguageRussianData)
        {
            var language = data switch
            {
                LanguageCyrillicData => BotLanguage.Cyrillic,
                LanguageRussianData => BotLanguage.Russian,
                _ => BotLanguage.Latin,
            };

            /*
              Yarim to'ldirilgan ariza SAQLANADI: til almashtirish
              arizani bekor qilish emas. Nomzod ismini yozib bo'lib
              tilni almashtirsa, u ismni qaytadan yozishi kerak
              bo'lmasligi kerak — shu sababli mavjud holat ustiga
              faqat til yoziladi.
            */
            _conversations.Save(
                chat.Id,
                state is null
                    ? new ConversationState { Step = ConversationStep.Vacancy, Language = language }
                    : state with { Language = language });

            await SendWithMenuAsync(chat.Id, BotTexts.Greeting(language), language, cancellationToken)
                .ConfigureAwait(false);

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
            await AskLanguageAsync(chatId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var state = _conversations.Find(chatId);

        /*
          MENYU QADAMDAN OLDIN tekshiriladi va bu tartib ataylab.

          Doimiy menyu tugmasi oddiy MATN bo'lib keladi. Agar avval
          qadam ishlansa, F.I.Sh. kutilayotgan paytda bosilgan
          «📞 Kontaktlar / Manzil» tugmasi NOMZODNING ISMI sifatida
          o'qilardi va arizaga shu yozilardi. Ya'ni tugmani bosgan odam
          arizadan chiqolmay qolardi — MBF ning botida bu yo'l bor va
          u yerda ham menyu qadamdan ustun turadi.
        */
        var action = BotMenu.Resolve(message.Text);

        if (action != MenuAction.None)
        {
            await HandleMenuAsync(chatId, state, action, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state is null)
        {
            // Bot qayta ishga tushgan yoki suhbat eskirgan. Til noma'lum,
            // shu sababli darhol til so'raladi — «/start» ni eslashga
            // majburlashdan ko'ra bir qadam kam.
            await AskLanguageAsync(chatId, cancellationToken).ConfigureAwait(false);
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

            await SendWithMenuAsync(
                    chatId,
                    BotTexts.AlreadyApplied(state.Language, state.VacancyTitle, existing.AppliedAt, existing.StageName),
                    state.Language,
                    cancellationToken)
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

        /*
          Menyu bilan: telefon so'ralganda Telegram uni kontakt tugmasi
          bilan almashtirgan edi. Menyusiz nomzod ariza tugagach bo'sh
          ekran oldida qolardi va ikkinchi vakansiyaga ariza berish
          uchun `/start` yozishi kerak bo'lardi.
        */
        await SendWithMenuAsync(
                chatId,
                BotTexts.Accepted(state.Language, state.VacancyTitle),
                state.Language,
                cancellationToken)
            .ConfigureAwait(false);

        LogApplication(state.VacancyTitle);
    }

    /// <summary>
    /// Ishchi token borligini va DOLZARBLIGINI ta'minlaydi.
    /// </summary>
    /// <returns>Bot xabar qabul qilishga tayyormi.</returns>
    /// <remarks>
    /// <para>
    /// Uch holat: token yo'q, token o'zgarmagan, token yangi. Faqat
    /// uchinchisida Telegram bilan qo'l berishiladi
    /// (<c>deleteWebhook</c> + <c>getMe</c>) — ya'ni odatiy aylanishda
    /// bu metod hech qanday tarmoq so'rovi qilmaydi.
    /// </para>
    /// <para>
    /// <b>Xato holatida ham jurnal tinch.</b> Halqa 15 soniyada
    /// aylanadi, shu sababli har bir shikoyat token bo'yicha BIR MARTA
    /// yoziladi (<see cref="_complainedAbout"/>).
    /// </para>
    /// </remarks>
    private async Task<bool> EnsureTokenAsync(CancellationToken cancellationToken)
    {
        // Bot ulanmagan bo'lsa kesh chetlab o'tiladi: odam ekranda
        // tokenni endigina kiritgan bo'lishi mumkin va uni bir daqiqa
        // kutishga majburlashning sababi yo'q.
        var desired = await DesiredTokenAsync(force: _activeToken is null, cancellationToken)
            .ConfigureAwait(false);

        if (desired is null)
        {
            if (_complainedAbout != string.Empty)
            {
                LogTokenMissing();
                _complainedAbout = string.Empty;
            }

            _activeToken = null;
            return false;
        }

        if (desired == _activeToken)
        {
            return true;
        }

        if (_activeToken is not null)
        {
            LogTokenChanged();
        }

        _token.Value = desired;

        // Webhook faol bo'lsa `getUpdates` umuman ishlamaydi (izohi
        // `TelegramClient.DeleteWebhookAsync` da). Har YANGI tokenda
        // takrorlanadi: webhook bot tomonida yozilgan bo'ladi va yangi
        // token boshqa botga tegishli bo'lishi mumkin.
        await _telegram.DeleteWebhookAsync(cancellationToken).ConfigureAwait(false);

        var username = await _telegram.GetMeAsync(cancellationToken).ConfigureAwait(false);

        if (username is null)
        {
            if (_complainedAbout != desired)
            {
                LogBadToken();
                _complainedAbout = desired;
            }

            _activeToken = null;
            return false;
        }

        /*
          Yangi token — yangi hisob bo'lishi mumkin (sinovdan ishchi
          botga o'tish). Eski hisobning `offset` i yangisida ma'nosiz:
          u boshqa botning yangilik raqamlari edi. Nolga qaytarish
          xavfsiz — Telegram allaqachon TASDIQLANGAN yangiliklarni
          qaytarmaydi, ya'ni eski xabarlar ikkinchi marta kelmaydi.
        */
        _offset = 0;
        _activeToken = desired;
        _complainedAbout = null;

        LogStarted(username);
        return true;
    }

    /// <summary>
    /// Bot ISHLATISHI kerak bo'lgan token: tizimdagisi, bo'lmasa <c>.env</c> dagisi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Ustuvorlik: EKRAN &gt; <c>.env</c>.</b> Ekrandagi qiymat
    /// ataylab, hozir va aniq bir odam tomonidan qo'yilgan; <c>.env</c>
    /// esa o'rnatishda bir marta yozilgan va odatda unutiladi. Teskarisi
    /// bo'lsa, ekranda tokenni almashtirgan odam hech qanday natija
    /// ko'rmasdi va sababini topa olmasdi.
    /// </para>
    /// <para>
    /// <b>So'rov bajarilmasa ESKI qiymat qoladi</b> (matn sozlamasidagi
    /// bilan bir xil qoida): API ga borib kelolmaslik ishlab turgan
    /// botni to'xtatishga sabab emas.
    /// </para>
    /// </remarks>
    private async Task<string?> DesiredTokenAsync(bool force, CancellationToken cancellationToken)
    {
        if (force || !_tokenEverRead || DateTimeOffset.UtcNow - _tokenReadAt >= TokenRefresh)
        {
            var fresh = await _bonvi.GetBotTokenAsync(cancellationToken).ConfigureAwait(false);

            if (fresh is not null)
            {
                _settingsToken = string.IsNullOrWhiteSpace(fresh.Token) ? null : fresh.Token;
                _tokenEverRead = true;
                _tokenReadAt = DateTimeOffset.UtcNow;
            }
        }

        if (_settingsToken is not null)
        {
            return _settingsToken;
        }

        return string.IsNullOrWhiteSpace(_options.Token) ? null : _options.Token;
    }

    /// <summary>
    /// Sozlamani tizimdan oladi (besh daqiqalik kesh bilan).
    /// </summary>
    /// <remarks>
    /// So'rov bajarilmasa ESKI qiymat qoladi: sozlama uchun API ga
    /// borib kelolmaslik nomzodning arizasini to'xtatishga sabab emas.
    /// </remarks>
    private async Task<BotSettings?> SettingsAsync(CancellationToken cancellationToken)
    {
        if (_settings is not null && DateTimeOffset.UtcNow - _settingsReadAt < TimeSpan.FromMinutes(5))
        {
            return _settings;
        }

        var fresh = await _bonvi.GetBotSettingsAsync(cancellationToken).ConfigureAwait(false);

        if (fresh is not null)
        {
            _settings = fresh;
            _settingsReadAt = DateTimeOffset.UtcNow;
        }

        return _settings;
    }

    /// <summary>
    /// Tanlangan tildagi kompaniya matni — sozlanmagan bo'lsa <c>null</c>.
    /// </summary>
    private static string? About(BotSettings? settings, BotLanguage language)
        => Section(settings?.AboutCyrillic, settings?.AboutLatin, settings?.AboutRussian, language);

    /// <summary>Tanlangan tildagi kontakt matni — sozlanmagan bo'lsa <c>null</c>.</summary>
    private static string? Contacts(BotSettings? settings, BotLanguage language)
        => Section(settings?.ContactsCyrillic, settings?.ContactsLatin, settings?.ContactsRussian, language);

    /// <summary>
    /// Uch tildan keraklisini tanlaydi.
    /// </summary>
    /// <remarks>
    /// Tanlangan til bo'sh bo'lsa KIRILLGA tushadi: bitta matn yozib
    /// qo'yish uchtasini majburlashdan foydaliroq. Kirill tayanch til
    /// sifatida olingan, chunki tizimdagi ma'lumotning deyarli
    /// hammasi shu yozuvda.
    /// </remarks>
    private static string? Section(
        string? cyrillic,
        string? latin,
        string? russian,
        BotLanguage language)
    {
        var chosen = language switch
        {
            BotLanguage.Latin => latin,
            BotLanguage.Russian => russian,
            _ => cyrillic,
        };

        var text = string.IsNullOrWhiteSpace(chosen) ? cyrillic : chosen;

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
    /// <summary>Til tanlash tugmalarini ko'rsatadi.</summary>
    private async Task AskLanguageAsync(long chatId, CancellationToken cancellationToken)
        => await _telegram
            .SendMessageAsync(
                chatId,
                BotTexts.ChooseLanguage,
                [
                    [new InlineButton(BotTexts.LanguageCyrillic, LanguageCyrillicData)],
                    [new InlineButton(BotTexts.LanguageLatin, LanguageLatinData)],
                    [new InlineButton(BotTexts.LanguageRussian, LanguageRussianData)],
                ],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Xabarni DOIMIY MENYU bilan birga yuboradi.
    /// </summary>
    /// <remarks>
    /// Menyu har safar qayta yuboriladi va bu ortiqcha emas: telefon
    /// so'ralganda Telegram menyuni kontakt tugmasi bilan ALMASHTIRADI
    /// (bir chatda bir vaqtda bitta klaviatura bo'ladi). Ariza
    /// tugagach menyu o'zi qaytmasa, nomzod bo'sh ekran oldida
    /// qolardi.
    /// </remarks>
    private async Task SendWithMenuAsync(
        long chatId,
        string text,
        BotLanguage language,
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);

        await _telegram
            .SendMessageAsync(
                chatId,
                text,
                menu: MenuLabels(settings, language),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sozlamaga qarab menyu tugmalarini yig'adi.</summary>
    private static List<string> MenuLabels(BotSettings? settings, BotLanguage language)
        => BotMenu.Labels(
            language,
            hasAbout: About(settings, language) is not null,
            hasContacts: Contacts(settings, language) is not null);

    private async Task HandleMenuAsync(
        long chatId,
        ConversationState? state,
        MenuAction action,
        CancellationToken cancellationToken)
    {
        var language = state?.Language ?? BotLanguage.Cyrillic;

        if (action == MenuAction.Language)
        {
            await AskLanguageAsync(chatId, cancellationToken).ConfigureAwait(false);
            return;
        }

        /*
          Menyudan kelgan har qanday amal yarim qolgan ARIZANI bekor
          qiladi: nomzod «Kontaktlar» ni ochib, keyin telefon raqamini
          yuborsa, u eski vakansiyaga yozilib ketardi. Til bundan
          mustasno (yuqorida) — u arizaning davomi.
        */
        _conversations.Save(
            chatId,
            new ConversationState { Step = ConversationStep.Vacancy, Language = language });

        switch (action)
        {
            case MenuAction.Apply:
                await ShowVacanciesAsync(chatId, language, cancellationToken).ConfigureAwait(false);
                break;

            case MenuAction.About:
                await SendSectionAsync(chatId, language, About, cancellationToken).ConfigureAwait(false);
                break;

            case MenuAction.Contacts:
                await SendSectionAsync(chatId, language, Contacts, cancellationToken).ConfigureAwait(false);
                await SendLocationAsync(chatId, cancellationToken).ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Sozlamadagi matnli bo'limni ko'rsatadi.
    /// </summary>
    /// <remarks>
    /// Bo'lim bo'sh bo'lsa tugma umuman chizilmagan bo'lardi
    /// (<see cref="MenuLabels"/>), lekin HR matnni bot ochiq turganda
    /// o'chirishi mumkin va eski tugma nomzod ekranida qolib ketadi.
    /// Shunda bo'sh xabar o'rniga menyu qayta ko'rsatiladi.
    /// </remarks>
    private async Task SendSectionAsync(
        long chatId,
        BotLanguage language,
        Func<BotSettings?, BotLanguage, string?> read,
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
        var text = read(settings, language);

        await _telegram
            .SendMessageAsync(
                chatId,
                text ?? BotTexts.Restart(language),
                menu: MenuLabels(settings, language),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Bosh ofisning xaritadagi nuqtasi — sozlangan bo'lsa.</summary>
    private async Task SendLocationAsync(long chatId, CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);

        if (MapPoint.TryParse(settings?.Location) is not { } point)
        {
            return;
        }

        await _telegram
            .SendLocationAsync(chatId, point.Latitude, point.Longitude, cancellationToken)
            .ConfigureAwait(false);
    }


    private async Task ShowVacanciesAsync(
        long chatId,
        BotLanguage language,
        CancellationToken cancellationToken)
    {
        var vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);

        if (vacancies.Count == 0)
        {
            _conversations.Forget(chatId);

            await SendWithMenuAsync(chatId, BotTexts.NoVacancies(language), language, cancellationToken)
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
