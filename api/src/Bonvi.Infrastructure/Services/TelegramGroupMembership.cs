using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Recruiting.Council;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// Asks Telegram's <c>getChatMember</c>. Answers are kept a minute: every photo and PDF of a sitting
/// passes through here, and being removed from the group may take that long to count.
/// </summary>
public sealed partial class TelegramGroupMembership : ITelegramGroupMembership
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);
    private static readonly Uri TelegramApi = new("https://api.telegram.org/");

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TelegramGroupMembership> _logger;

    public TelegramGroupMembership(HttpClient http, IMemoryCache cache, ILogger<TelegramGroupMembership> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TelegramGroupStatus> CheckAsync(string botToken, long chatId, long userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(botToken);

        var key = (nameof(TelegramGroupMembership), chatId, userId);

        if (_cache.TryGetValue(key, out TelegramGroupStatus known))
        {
            return known;
        }

        var status = await AskAsync(botToken, chatId, userId, cancellationToken).ConfigureAwait(false);

        // Not kept when unknown: the next request should simply ask again.
        if (status != TelegramGroupStatus.Unavailable)
        {
            _cache.Set(key, status, CacheFor);
        }

        return status;
    }

    private async Task<TelegramGroupStatus> AskAsync(string botToken, long chatId, long userId, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"bot{botToken}/getChatMember?chat_id={chatId}&user_id={userId}");

        try
        {
            using var response = await _http.GetAsync(new Uri(TelegramApi, path), cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

                return IsInGroup(body.GetProperty("result")) ? TelegramGroupStatus.Member : TelegramGroupStatus.NotMember;
            }

            LogRefused((int)response.StatusCode);

            // 400: Telegram knows no such participant. Anything else says nothing about the person.
            return response.StatusCode == System.Net.HttpStatusCode.BadRequest
                ? TelegramGroupStatus.NotMember
                : TelegramGroupStatus.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException
            && !cancellationToken.IsCancellationRequested)
        {
            // Only the type: a request's message could carry the address, and the token is in it.
            LogFailed(ex.GetType().Name);

            return TelegramGroupStatus.Unavailable;
        }
    }

    private static bool IsInGroup(JsonElement member)
    {
        var status = member.GetProperty("status").GetString();

        return status is "creator" or "administrator" or "member"
            || (status == "restricted" && member.TryGetProperty("is_member", out var isMember) && isMember.ValueKind == JsonValueKind.True);
    }

    [LoggerMessage(EventId = 4801, Level = LogLevel.Warning, Message = "Telegram refused getChatMember for the council group: HTTP {Status}. Is the bot an admin there?")]
    private partial void LogRefused(int status);

    [LoggerMessage(EventId = 4802, Level = LogLevel.Warning, Message = "Could not ask Telegram about the council group: {Error}")]
    private partial void LogFailed(string error);
}
