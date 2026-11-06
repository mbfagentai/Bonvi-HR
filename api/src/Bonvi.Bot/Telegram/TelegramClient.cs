using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Bonvi.Bot.Telegram;

/// <summary>
/// Telegram Bot API bilan ishlaydigan eng kichik mijoz.
/// </summary>
/// <remarks>
/// <para>
/// Botga atigi to'rtta chaqiruv kerak: yangiliklarni olish, xabar
/// yuborish, tugma bosilishiga javob berish va o'zini tekshirish. Shu
/// sababli tayyor kutubxona emas, oddiy <c>HttpClient</c>
/// (izohi <c>Bonvi.Bot.csproj</c> da).
/// </para>
/// <para>
/// <b>Xato tashlanmaydi.</b> Har bir metod muvaffaqiyat/muvaffaqiyatsizlik
/// haqida oddiy qiymat qaytaradi va sababni jurnalga yozadi. Tarmoq
/// uzilishi bot uchun ODATIY hol, istisno emas: halqa keyingi aylanishda
/// qaytadan uriniladi.
/// </para>
/// </remarks>
public sealed partial class TelegramClient
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Telegram «{Method}» ni rad etdi: {Description}")]
    private partial void LogRejected(string method, string description);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Telegram «{Method}» chaqiruvi bajarilmadi")]
    private partial void LogCallFailed(Exception exception, string method);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TelegramToken _token;
    private readonly ILogger<TelegramClient> _logger;

    public TelegramClient(HttpClient http, TelegramToken token, ILogger<TelegramClient> logger)
    {
        _http = http;
        _token = token;
        _logger = logger;
    }

    /// <summary>Token to'g'rimi — ishga tushishda bir marta tekshiriladi.</summary>
    /// <returns>Botning foydalanuvchi nomi, xato bo'lsa <c>null</c>.</returns>
    public async Task<string?> GetMeAsync(CancellationToken cancellationToken)
    {
        var result = await CallAsync<JsonElement>("getMe", null, cancellationToken).ConfigureAwait(false);

        if (result.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return result.TryGetProperty("username", out var username) ? username.GetString() : null;
    }

    /// <summary>
    /// Bot uchun ro'yxatdan o'tgan webhook'ni o'chiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Telegram bitta botga IKKALA usulni birdan bermaydi: webhook faol
    /// bo'lsa <c>getUpdates</c> «Conflict» bilan rad etiladi va bot
    /// birorta ham xabar ololmaydi.
    /// </para>
    /// <para>
    /// Webhook bir marta qo'yilsa Telegram tomonida QOLADI — botni qayta
    /// yaratish ham, tokenni almashtirish ham uni o'chirmaydi. Shu sababli
    /// har ishga tushishda bir marta o'chiriladi: bu botni boshqa
    /// muhitdan (masalan sinov serveridan) ko'chirganda yagona ishonchli
    /// yo'l va webhook'siz botda hech qanday zarari yo'q.
    /// </para>
    /// </remarks>
    public async Task DeleteWebhookAsync(CancellationToken cancellationToken)
        => await CallAsync<JsonElement>(
                "deleteWebhook",

                // Kutib turgan xabarlar TASHLANMAYDI: nomzod bot
                // ishlamayotgan paytda yozgan bo'lishi mumkin va uning
                // xabari yo'qolmasligi kerak.
                new { drop_pending_updates = false },
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Yangiliklarni oladi (long polling).
    /// </summary>
    /// <param name="offset">
    /// Shu raqamdan KEYINGI yangiliklar. Telegram <c>offset</c> berilgan
    /// zahoti undan oldingilarni O'CHIRADI — ya'ni bu bir vaqtning o'zida
    /// «tasdiqlash» ham. Shu sababli u ishlov berilgandan KEYIN oshiriladi:
    /// aks holda bot yiqilganda ishlanmagan xabar yo'qolardi.
    /// </param>
    /// <param name="timeoutSeconds">So'rov necha soniya ochiq turadi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>
    /// Yangiliklar ro'yxati (bo'sh bo'lishi mumkin — kutish vaqti tugadi),
    /// yoki <c>null</c> — chaqiruvning O'ZI yiqildi (tarmoq yoki Telegram
    /// xatosi). Sababi asosiy izohda.
    /// </returns>
    public async Task<IReadOnlyList<TelegramUpdate>?> GetUpdatesAsync(
        long offset,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            offset,
            timeout = timeoutSeconds,

            // Faqat kerakli turlar: qolganlari (kanal xabarlari, tahrirlar)
            // botga aloqasi yo'q va ularni o'qish bekor ish bo'lardi.
            allowed_updates = new[] { "message", "callback_query", "my_chat_member" },
        };

        // ⚠️ `null` — CHAQIRUV YIQILDI, «yangilik yo'q» EMAS. Ikkisini
        // birlashtirish (ilgari `result ?? []` edi) qimmatga tushgan edi:
        // Telegram xatosi (masalan 409 «terminated by other getUpdates
        // request») bo'sh ro'yxatga aylanib, halqa kutish o'rniga darhol
        // aylanardi. Natija — sekundiga yuzlab so'rov, API'ning tezlik
        // cheklovi va xato jurnalining to'lib ketishi (kuniga 400 000+
        // qator). Chaqiruvchi endi farqni ko'radi va orqaga chekinadi.
        return await CallAsync<List<TelegramUpdate>>("getUpdates", body, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Matnli xabar yuboradi.</summary>
    /// <param name="chatId">Suhbat.</param>
    /// <param name="text">Matn.</param>
    /// <param name="buttons">
    /// Xabar ostidagi tugmalar. Har bir ichki ro'yxat — bitta QATOR.
    /// </param>
    /// <param name="requestContact">
    /// <c>true</c> bo'lsa klaviatura o'rniga «raqamni yuborish» tugmasi
    /// chiqadi. Telegram'da bu YAGONA yo'l: raqamni qo'lda yozdirish
    /// xato terishga va begona raqam kiritishga yo'l ochardi.
    /// </param>
    /// <param name="contactButtonText">«Raqamni yuborish» tugmasining matni.</param>
    /// <param name="menu">
    /// Ekran ostida DOIMIY turadigan menyu tugmalari. Bosilganda ular
    /// oddiy matnli xabar bo'lib keladi — <c>buttons</c> dan farqi
    /// shunda: u <c>callback</c> yuboradi va xabarga bog'lanib qoladi.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<bool> SendMessageAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null,
        bool requestContact = false,
        string? contactButtonText = null,
        IReadOnlyList<string>? menu = null,
        CancellationToken cancellationToken = default)
    {
        object? markup = null;

        if (requestContact)
        {
            markup = new
            {
                keyboard = new[]
                {
                    new[] { new { text = contactButtonText ?? "📱", request_contact = true } },
                },
                resize_keyboard = true,
                one_time_keyboard = true,
            };
        }
        else if (buttons is { Count: > 0 })
        {
            markup = InlineMarkup(buttons);
        }
        else if (menu is { Count: > 0 })
        {
            /*
              DOIMIY MENYU — ekran ostida turadigan tugmalar.

              `one_time_keyboard` YO'Q: aynan shu narsa uni doimiy
              qiladi. Nomzod suhbatning istalgan joyida bo'limni
              almashtira oladi va «/start» buyrug'ini eslab qolishi
              shart emas — bu bot bilan birinchi marta ishlayotgan
              odam uchun eng katta farq.

              Ikkitalab qator: to'rtta tugma bitta qatorda telefon
              ekranida siqilib, matni kesilib qolardi.
            */
            markup = new
            {
                keyboard = menu
                    .Select((label, index) => (label, index))
                    .GroupBy(pair => pair.index / 2)
                    .Select(row => row.Select(pair => new { text = pair.label }).ToArray())
                    .ToArray(),
                resize_keyboard = true,
                is_persistent = true,
            };
        }
        else
        {
            // Kontakt klaviaturasi bir marta ishlatilgach OLIB TASHLANADI:
            // aks holda u suhbat oxirigacha ekran ostida osilib turardi.
            markup = new { remove_keyboard = true };
        }

        var body = new
        {
            chat_id = chatId,
            text,

            /*
              ⚠️ Matndagi `<b>` va `<i>` teglari IShLASHI uchun shart.

              Busiz Telegram ularni oddiy matn deb ko'rsatadi va nomzod
              ekranda «<i>Каримов Азиз</i>» degan dasturchi belgilarini
              o'qiydi — bu bir qarashda buzuq bot taassurotini beradi.

              Matnga tashqaridan tushadigan yagona qiymat — vakansiya
              nomi, va u `Escape` dan o'tkaziladi (`BotTexts`): unda `<`
              yoki `&` bo'lsa Telegram butun xabarni rad etardi.
            */
            parse_mode = "HTML",
            reply_markup = markup,
        };

        var (result, _) = await CallWithOutcomeAsync<JsonElement>("sendMessage", body, cancellationToken).ConfigureAwait(false);
        return result.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Xabarnoma (rad etish, suhbat taklifi) — natijasi bilan.
    /// </summary>
    /// <remarks>
    /// <see cref="SendMessageAsync"/> dan farqi: «yuborilmadi» ning SABABI
    /// ajratiladi. Nomzod botni bloklagan bo'lsa (Telegram 403) qayta urinish
    /// foydasiz — bitta urinish yetarli (egasining qarori, 2026-10-05).
    /// Tarmoq uzilishi esa xabar umuman ketmagani — uni keyinroq yuborish
    /// nomzodga takroriy xabar bermaydi.
    /// </remarks>
    public async Task<TelegramDelivery> SendNoticeAsync(
        long chatId,
        string text,
        IReadOnlyList<string> menu,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(menu);

        var body = new
        {
            chat_id = chatId,
            text,
            parse_mode = "HTML",
            reply_markup = new
            {
                keyboard = menu
                    .Select((label, index) => (label, index))
                    .GroupBy(pair => pair.index / 2)
                    .Select(row => row.Select(pair => new { text = pair.label }).ToArray())
                    .ToArray(),
                resize_keyboard = true,
                is_persistent = true,
            },
        };

        var (_, outcome) = await CallWithOutcomeAsync<JsonElement>("sendMessage", body, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>
    /// Xabar ostidagi tugmalar uchun <c>reply_markup</c> yasaydi.
    /// </summary>
    /// <remarks>
    /// Havolali tugmada <c>url</c>, qolganida <c>callback_data</c>
    /// yuboriladi. Telegram ikkalasini BIRGA qabul qilmaydi va
    /// ikkalasi ham yo'q bo'lsa butun xabarni rad etadi — shu sababli
    /// amal tugmasida bo'sh satr emas, har doim qiymat bo'lishi kerak.
    /// </remarks>
    private static object InlineMarkup(IReadOnlyList<IReadOnlyList<InlineButton>> buttons)
        => new
        {
            inline_keyboard = buttons
                .Select(row => row
                    .Select(button => button switch
                    {
                        { WebAppUrl: { Length: > 0 } webApp } => (object)new { text = button.Text, web_app = new { url = webApp } },
                        { Url: { Length: > 0 } url } => new { text = button.Text, url },
                        _ => new { text = button.Text, callback_data = button.CallbackData ?? string.Empty },
                    })
                    .ToArray())
                .ToArray(),
        };

    /// <summary>Posts to a group, with the id the message can later be edited by.</summary>
    public async Task<(TelegramDelivery Outcome, long? MessageId)> PostAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>> buttons,
        CancellationToken cancellationToken)
    {
        var body = new { chat_id = chatId, text, parse_mode = "HTML", reply_markup = InlineMarkup(buttons) };
        var (result, outcome) = await CallWithOutcomeAsync<JsonElement>("sendMessage", body, cancellationToken).ConfigureAwait(false);

        return (outcome, outcome == TelegramDelivery.Delivered && result.TryGetProperty("message_id", out var id) ? id.GetInt64() : null);
    }

    /// <summary>Replaces a posted message's text and buttons, silently.</summary>
    public async Task<TelegramDelivery> EditAsync(
        long chatId,
        long messageId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>> buttons,
        CancellationToken cancellationToken)
    {
        var body = new { chat_id = chatId, message_id = messageId, text, parse_mode = "HTML", reply_markup = InlineMarkup(buttons) };
        var (_, outcome) = await CallWithOutcomeAsync<JsonElement>("editMessageText", body, cancellationToken).ConfigureAwait(false);

        return outcome;
    }

    /// <summary>Leaves a group the bot does not belong in. It may already be out, so the outcome is not read.</summary>
    public async Task LeaveChatAsync(long chatId, CancellationToken cancellationToken)
        => await CallWithOutcomeAsync<JsonElement>("leaveChat", new { chat_id = chatId }, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Telegram'da ALLAQACHON yotgan rasmni yuboradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>file_id</c> — Telegram bir marta yuklangan faylga bergan
    /// belgi. U bilan yuborish bayt uzatmaydi va shu sababli deyarli
    /// bir zumda bajariladi.
    /// </para>
    /// <para>
    /// <c>null</c> qaytishi — belgi eskirgan degani (bot tokeni
    /// almashgan bo'lsa shunday bo'ladi). Chaqiruvchi bunda rasmni
    /// qaytadan yuklaydi.
    /// </para>
    /// </remarks>
    public async Task<bool> SendPhotoByIdAsync(
        long chatId,
        string fileId,
        string? caption,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync<JsonElement>(
                "sendPhoto",
                new
                {
                    chat_id = chatId,
                    photo = fileId,
                    caption,
                    parse_mode = "HTML",
                    reply_markup = buttons is { Count: > 0 } ? InlineMarkup(buttons) : null,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return result.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Rasmni BAYT bilan yuklaydi va Telegram bergan belgini qaytaradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega havola emas.</b> Telegram rasmni URL bilan ham
    /// yubora oladi, lekin u holda manzil INTERNETDAN ochiladigan
    /// bo'lishi kerak. Bizning API esa ichki tarmoqda turadi va
    /// ishlab chiqishda umuman tashqaridan ko'rinmaydi — ya'ni logotip
    /// faqat serverda ishlagan bo'lardi.
    /// </para>
    /// <para>
    /// Qaytgan <c>file_id</c> ni saqlab qo'yish kerak: keyingi
    /// yuborishlar <see cref="SendPhotoByIdAsync"/> bilan ketadi va
    /// bayt boshqa uzatilmaydi.
    /// </para>
    /// </remarks>
    /// <returns>Yuklangan rasmning <c>file_id</c> si, xato bo'lsa <c>null</c>.</returns>
    public async Task<string?> UploadPhotoAsync(
        long chatId,
        Stream content,
        string? caption,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons,
        CancellationToken cancellationToken)
    {
        var path = RelativePath(_token.Value, "sendPhoto");

        try
        {
            using var form = new MultipartFormDataContent();
            using var file = new StreamContent(content);

            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            form.Add(new StringContent(chatId.ToString(CultureInfo.InvariantCulture)), "chat_id");
            form.Add(file, "photo", "logo.jpg");

            if (!string.IsNullOrEmpty(caption))
            {
                form.Add(new StringContent(caption), "caption");
                form.Add(new StringContent("HTML"), "parse_mode");
            }

            if (buttons is { Count: > 0 })
            {
                // «multipart» da murakkab qiymat MATN bo'lib ketadi:
                // Telegram uni JSON deb o'qiydi.
                form.Add(new StringContent(JsonSerializer.Serialize(InlineMarkup(buttons), Json)), "reply_markup");
            }

            using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content
                .ReadFromJsonAsync<TelegramResponse<JsonElement>>(Json, cancellationToken)
                .ConfigureAwait(false);

            if (payload is not { Ok: true })
            {
                LogRejected("sendPhoto", payload?.Description ?? "sababsiz");
                return null;
            }

            /*
              Javobdagi `photo` — BITTA rasmning bir necha o'lchamdagi
              ko'rinishlari. Belgini oxirgisidan olamiz: u eng katta
              nusxa va aynan shu sifatni qayta yuborish kerak.
            */
            return payload.Result.TryGetProperty("photo", out var sizes)
                && sizes.ValueKind == JsonValueKind.Array
                && sizes.GetArrayLength() > 0
                && sizes[sizes.GetArrayLength() - 1].TryGetProperty("file_id", out var id)
                    ? id.GetString()
                    : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCallFailed(ex, "sendPhoto");
            return null;
        }
    }

    /// <summary>
    /// Nomzod yuborgan faylni Telegram'dan YUKLAB OLADI (18-qadam, surat).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Ikki so'rov, bittasi emas.</b> Telegram fayl baytlarini
    /// belgisi bilan bermaydi: avval <c>getFile</c> chaqiriladi va u
    /// <c>file_path</c> qaytaradi, keyin fayl BOSHQA manzildan olinadi
    /// (<c>/file/bot…</c>). Yo'l qisqa muddat yashaydi, shu sababli uni
    /// saqlab qo'yishning ma'nosi yo'q — rasm shu yerda darhol olinadi.
    /// </para>
    /// <para>
    /// <b>Chegara: 20 MB</b> (Telegram Bot API cheklovi). Undan katta
    /// faylni <c>getFile</c> umuman qaytarmaydi — bu nomzod suratida
    /// amalda uchramaydi, chunki Telegram rasmni o'zi siqadi.
    /// </para>
    /// <para>
    /// Xato tashlanmaydi (sinfning umumiy qoidasi): rasm yetib
    /// bormasligi arizani to'xtatmaydi.
    /// </para>
    /// </remarks>
    /// <param name="fileId">Faylning belgisi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Fayl baytlari, olinmasa <c>null</c>.</returns>
    public async Task<byte[]?> DownloadFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var file = await CallAsync<JsonElement>("getFile", new { file_id = fileId }, cancellationToken)
            .ConfigureAwait(false);

        if (file.ValueKind != JsonValueKind.Object
            || !file.TryGetProperty("file_path", out var pathValue)
            || pathValue.GetString() is not { Length: > 0 } remotePath)
        {
            return null;
        }

        try
        {
            using var response = await _http
                .GetAsync(FilePath(_token.Value, remotePath), cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Bu yerda javob JSON emas, shu sababli sabab HOLAT
                // KODIDAN olinadi. Kod matnga aylantiriladi — token
                // manzilda turibdi va manzil jurnalga tushmasligi kerak.
                LogRejected("getFile", ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCallFailed(ex, "getFile");
            return null;
        }
    }

    /// <summary>
    /// Xaritadagi nuqtani yuboradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Manzil MATN bilan berilganda nomzod uni qo'lda navigatorga
    /// ko'chirishi kerak bo'ladi; Telegram'ning o'z nuqtasi esa bir
    /// bosishda yo'l ko'rsatishga o'tadi. Shu sababli kontakt matnidan
    /// keyin alohida xabar bo'lib yuboriladi.
    /// </para>
    /// <para>
    /// Xatolik yutiladi (<c>CallAsync</c> ning umumiy qoidasi): xarita
    /// bormasa ham kontakt matni allaqachon yetib borgan va nomzod
    /// manzilni biladi.
    /// </para>
    /// </remarks>
    public async Task<bool> SendLocationAsync(
        long chatId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync<JsonElement>(
                "sendLocation",
                new { chat_id = chatId, latitude, longitude },
                cancellationToken)
            .ConfigureAwait(false);

        return result.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Tugma bosilishiga javob beradi — Telegram'dagi «soat» belgisini o'chiradi.
    /// </summary>
    /// <remarks>
    /// Javob berilmasa tugma bir necha soniya «yuklanmoqda» holatida
    /// qolib ketadi va foydalanuvchi uni qayta bosadi.
    /// </remarks>
    public async Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken)
        => await CallAsync<JsonElement>(
                "answerCallbackQuery",
                new { callback_query_id = callbackId },
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<T?> CallAsync<T>(string method, object? body, CancellationToken cancellationToken)
        => (await CallWithOutcomeAsync<T>(method, body, cancellationToken).ConfigureAwait(false)).Result;

    /// <summary>
    /// <see cref="CallAsync{T}"/> ning o'zi — natija turi bilan
    /// (<see cref="TelegramDelivery"/>).
    /// </summary>
    private async Task<(T? Result, TelegramDelivery Outcome)> CallWithOutcomeAsync<T>(
        string method,
        object? body,
        CancellationToken cancellationToken)
    {
        /*
          ⚠️ TOKEN MANZIL ICHIDA — Telegram API shunday qurilgan.

          Ilgari u `HttpClient.BaseAddress` ga bir marta yozilardi. Endi
          token ish paytida almashishi mumkin (`TelegramToken` izohi),
          shu sababli manzil HAR CHAQIRUVDA yig'iladi. Narxi — bitta
          satr biriktirish; evaziga tokenni almashtirish uchun
          konteynerni qayta ishga tushirish kerak emas.

          `HttpClient` ning o'zi o'zgarmaydi: ulanish havzasi (connection
          pool) manzil bazasiga emas, HOSTGA bog'langan va u o'sha-o'sha
          `api.telegram.org` bo'lib qolaveradi.

          Token jurnalga tushmaydi: `AddHttpClient` ning so'rov
          jurnallari `Program.cs` da butunlay o'chirilgan (sababi
          o'sha yerda), bu yerdagi xabarlar esa faqat METOD nomini
          yozadi.
        */
        var path = RelativePath(_token.Value, method);

        try
        {
            using var response = body is null
                ? await _http.GetAsync(path, cancellationToken).ConfigureAwait(false)
                : await _http.PostAsJsonAsync(path, body, Json, cancellationToken)
                    .ConfigureAwait(false);

            var payload = await response.Content
                .ReadFromJsonAsync<TelegramResponse<T>>(Json, cancellationToken)
                .ConfigureAwait(false);

            if (payload is { Ok: true })
            {
                return (payload.Result, TelegramDelivery.Delivered);
            }

            // The message already reads like this, so what the caller wanted is true.
            if (method == "editMessageText" && payload?.Description?.Contains("message is not modified", StringComparison.Ordinal) == true)
            {
                return (default, TelegramDelivery.Delivered);
            }

            LogRejected(method, payload?.Description ?? "sababsiz");

            // 4xx (429 dan tashqari) — Telegram aniq «yo'q» dedi; 429 va 5xx —
            // vaqtincha. Javobsiz yoki tushunarsiz javob ham vaqtincha.
            var code = payload?.ErrorCode ?? (int)response.StatusCode;
            var rejected = payload is not null && code is >= 400 and < 500 and not 429;

            return (default, rejected ? TelegramDelivery.Rejected : TelegramDelivery.Unreachable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // To'xtatish — xato emas, yuqoriga uzatiladi.
            throw;
        }
        catch (Exception ex)
        {
            // Tarmoq uzilishi va vaqt tugashi bot uchun odatiy hol
            // (sinf izohiga qarang): halqa keyingi aylanishda qayta uriniladi.
            LogCallFailed(ex, method);
            return (default, TelegramDelivery.Unreachable);
        }
    }

    /// <summary>
    /// Chaqiruvning nisbiy yo'li: <c>/bot&lt;token&gt;/&lt;metod&gt;</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Boshidagi qiya chiziq MAJBURIY va u shunchaki bezak emas.</b>
    /// Telegram tokeni <c>8123456789:AAH…</c> ko'rinishida — ichida IKKI
    /// NUQTA bor. Qiya chiziqsiz <c>Uri</c> uni sxema ajratgichi deb
    /// o'qiydi va <c>bot8123456789</c> ni protokol nomi deb hisoblaydi:
    /// natijada <c>UriKind.Relative</c> so'ralgan joyda «A relative URI
    /// cannot be created…» istisnosi tashlanadi.
    /// <para>
    /// Bu xato ishga tushirishda darhol chiqdi va shu sababli metod
    /// alohida ajratilgan: uni <c>TelegramClientTests</c> qadab turadi.
    /// Boshida qiya chiziq bo'lsa, ikki nuqta birinchi <c>/</c> dan
    /// KEYIN qoladi — sxema esa faqat birinchi bo'lakda izlanadi.
    /// </para>
    /// <para>
    /// Manzil bazasida yo'l yo'q (faqat <c>https://api.telegram.org/</c>),
    /// ya'ni «ildizdan» hisoblanadigan yo'l to'g'ri natija beradi.
    /// </para>
    /// </remarks>
    public static Uri RelativePath(string token, string method)
        => new($"/bot{token}/{method}", UriKind.Relative);

    /// <summary>
    /// Yuklab olinadigan faylning yo'li: <c>/file/bot&lt;token&gt;/&lt;yo'l&gt;</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Bu BOSHQA manzil</b> va uni <see cref="RelativePath"/> bilan
    /// aralashtirib bo'lmaydi: chaqiruvlar <c>/bot…</c> dan, fayllar esa
    /// <c>/file/bot…</c> dan olinadi. Boshidagi qiya chiziq shu yerda ham
    /// MAJBURIY — sabab <see cref="RelativePath"/> izohida (tokendagi
    /// ikki nuqta sxema ajratgichi deb o'qiladi).
    /// <para>
    /// <c>file_path</c> ichida ham qiya chiziq bo'ladi
    /// («photos/file_12.jpg») va u ATAYLAB kodlanmaydi: bu yo'lning
    /// bo'lagi, qiymat emas.
    /// </para>
    /// </remarks>
    public static Uri FilePath(string token, string filePath)
        => new($"/file/bot{token}/{filePath}", UriKind.Relative);
}
