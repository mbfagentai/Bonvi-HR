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
    private readonly ILogger<TelegramClient> _logger;

    public TelegramClient(HttpClient http, ILogger<TelegramClient> logger)
    {
        _http = http;
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
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<bool> SendMessageAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons = null,
        bool requestContact = false,
        string? contactButtonText = null,
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
            reply_markup = markup,
        };

        var result = await CallAsync<JsonElement>("sendMessage", body, cancellationToken).ConfigureAwait(false);
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
        try
        {
            using var response = body is null
                ? await _http.GetAsync(new Uri(method, UriKind.Relative), cancellationToken).ConfigureAwait(false)
                : await _http.PostAsJsonAsync(new Uri(method, UriKind.Relative), body, Json, cancellationToken)
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
}
