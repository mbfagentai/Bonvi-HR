using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Recruiting.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Infrastructure.Security;

/// <summary>
/// <see cref="IQuestionnaireBotCheck"/> — Cloudflare Turnstile (SPEC-planshet-anketa §2.3.2).
/// </summary>
/// <remarks>
/// <para>
/// Tokenni Cloudflare'ning <c>siteverify</c> manziliga yuboradi: <c>secret</c>, <c>response</c>
/// (token), <c>remoteip</c> (mijozning haqiqiy IP'si, bo'lsa) va <c>idempotency_key</c>
/// (yuborish identifikatori — o'sha yuborishni qayta tekshirish «ishlatilgan» deb rad etilmasin).
/// </para>
/// <para>
/// <b>YOPIQ-SUKUT.</b> Javob 5 soniyada kelmasa, Cloudflare'ga ulanib bo'lmasa, javob 2xx bo'lmasa
/// yoki o'qilmasa — <c>false</c> (egasi maksimal himoya so'ragan). Nomzodning qoralamasi saqlanadi,
/// u faqat qayta yuboradi.
/// </para>
/// <para>
/// ⚠️ <b>Token va maxfiy kalit jurnalga TUSHMAYDI.</b> Jurnalda faqat natija va Cloudflare'ning xato
/// kodlari (<c>invalid-input-response</c>, <c>timeout-or-duplicate</c> …). <c>HttpClient</c> ning
/// o'z so'rov jurnali o'chirilgan (ro'yxatga olishda) — tana u yerga baribir yozilmasdi, lekin har
/// anketaga to'rt qator yangi hech narsa bermasdi.
/// </para>
/// </remarks>
public sealed partial class TurnstileBotCheck : IQuestionnaireBotCheck
{
    /// <summary>Nomli <c>HttpClient</c> — <c>IHttpClientFactory</c> dagi nomi.</summary>
    public const string HttpClientName = "turnstile";

    /// <summary>Token chegarasi: Cloudflare tokeni 2048 belgigacha. Uzunrog'i Cloudflare'ga yuborilmaydi.</summary>
    public const int MaxTokenLength = 2048;

    private const int MaxLoggedCodes = 5;
    private const int MaxLoggedCodeLength = 40;

    private readonly HttpClient _http;
    private readonly TurnstileOptions _options;
    private readonly ILogger<TurnstileBotCheck> _logger;

    public TurnstileBotCheck(HttpClient http, IOptions<TurnstileOptions> options, ILogger<TurnstileBotCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Cloudflare'ning tekshiruv manzili.</summary>
    public static Uri SiteverifyUrl { get; } = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    /// <summary>Javob kutish chegarasi — undan keyin anketa rad etiladi (yopiq-sukut).</summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public string? SiteKey => _options.Enabled ? _options.SiteKey.Trim() : null;

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(
        string? token,
        string? clientIp,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            LogRejected("token-yoq");
            return false;
        }

        if (token.Length > MaxTokenLength)
        {
            LogRejected("token-uzun");
            return false;
        }

        var fields = new List<KeyValuePair<string, string>>(4)
        {
            new("secret", _options.SecretKey.Trim()),
            new("response", token),
            new("idempotency_key", idempotencyKey.ToString("D", CultureInfo.InvariantCulture)),
        };

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            fields.Add(new("remoteip", clientIp));
        }

        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var response = await _http.PostAsync(SiteverifyUrl, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogUnavailableStatus((int)response.StatusCode);
                return false;
            }

            var answer = await response.Content
                .ReadFromJsonAsync<SiteverifyResponse>(cancellationToken)
                .ConfigureAwait(false);

            if (answer is { Success: true })
            {
                return true;
            }

            var codes = SafeCodes(answer?.ErrorCodes);

            // Maxfiy kalit rad etilsa HAMMA anketa rad etiladi — bu nomzod emas, sozlama xatosi
            if (codes.Contains("secret", StringComparison.Ordinal))
            {
                LogSecretRejected(codes);
            }
            else
            {
                LogRejected(codes);
            }

            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout — so'rovchining o'zi bekor qilmagan
            LogUnavailable("vaqt tugadi");
            return false;
        }
        catch (HttpRequestException exception)
        {
            LogUnavailable(exception.HttpRequestError.ToString());
            return false;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            LogUnavailable("javob o'qilmadi");
            return false;
        }
    }

    /// <summary>
    /// Cloudflare kodlari jurnalga — faqat kichik harf, raqam va «-», ko'pi bilan beshta: javob
    /// tashqaridan keladi va jurnal qatorini buzmasin.
    /// </summary>
    private static string SafeCodes(IReadOnlyList<string>? codes)
    {
        if (codes is null || codes.Count == 0)
        {
            return "kodsiz";
        }

        var safe = codes
            .Take(MaxLoggedCodes)
            .Select(code => code.Length <= MaxLoggedCodeLength && code.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
                ? code
                : "?");

        return string.Join(',', safe);
    }

    [LoggerMessage(
        EventId = 4704,
        Level = LogLevel.Information,
        Message = "Bot tekshiruvi: token rad etildi ({Codes}) — anketa qabul qilinmadi")]
    private partial void LogRejected(string codes);

    [LoggerMessage(
        EventId = 4705,
        Level = LogLevel.Error,
        Message = "Bot tekshiruvi: Cloudflare maxfiy kalitni rad etdi ({Codes}) — Turnstile__SecretKey ni tekshiring, hozir HAMMA anketa rad etilmoqda")]
    private partial void LogSecretRejected(string codes);

    [LoggerMessage(
        EventId = 4706,
        Level = LogLevel.Warning,
        Message = "Bot tekshiruvi: Cloudflare javob bermadi ({Reason}) — anketa rad etildi (yopiq-sukut), nomzod qayta yuboradi")]
    private partial void LogUnavailable(string reason);

    [LoggerMessage(
        EventId = 4707,
        Level = LogLevel.Warning,
        Message = "Bot tekshiruvi: Cloudflare HTTP {Status} qaytardi — anketa rad etildi (yopiq-sukut), nomzod qayta yuboradi")]
    private partial void LogUnavailableStatus(int status);

    /// <summary><c>siteverify</c> javobining kerakli qismi.</summary>
    /// <param name="Success">Token haqiqiy.</param>
    /// <param name="ErrorCodes">Rad etish sabablari.</param>
    private sealed record SiteverifyResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error-codes")] IReadOnlyList<string>? ErrorCodes);
}
