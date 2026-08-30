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
    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
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
            allowed_updates = new[] { "message", "callback_query" },
        };

        var result = await CallAsync<List<TelegramUpdate>>("getUpdates", body, cancellationToken)
            .ConfigureAwait(false);

        return result ?? [];
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
            markup = new
            {
                inline_keyboard = buttons
                    .Select(row => row
                        .Select(button => new { text = button.Text, callback_data = button.CallbackData })
                        .ToArray())
                    .ToArray(),
            };
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

        var result = await CallAsync<JsonElement>("sendMessage", body, cancellationToken).ConfigureAwait(false);
        return result.ValueKind == JsonValueKind.Object;
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
                return payload.Result;
            }

            LogRejected(method, payload?.Description ?? "sababsiz");

            return default;
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
            return default;
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
}
