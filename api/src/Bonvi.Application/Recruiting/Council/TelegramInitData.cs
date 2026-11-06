using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Bonvi.Application.Recruiting.Council;

/// <summary>Checks the signed launch data Telegram gives a Mini App, by the steps of its documentation.</summary>
public static class TelegramInitData
{
    /// <summary>How old the launch data may be. A member can keep the app open through a sitting.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>The Telegram user the data was signed for; <c>null</c> when it is forged, stale or malformed.</summary>
    public static long? ReadUserId(string? raw, string? botToken, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(botToken))
        {
            return null;
        }

        var fields = HttpUtility.ParseQueryString(raw);
        var hash = fields["hash"];

        if (string.IsNullOrEmpty(hash) || hash.Length != 64 || !IsHex(hash))
        {
            return null;
        }

        var dataCheck = string.Join(
            '\n',
            fields.AllKeys
                .Where(key => key is not null && key != "hash")
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => key + "=" + fields[key]));

        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheck));

        if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(hash)))
        {
            return null;
        }

        if (!long.TryParse(fields["auth_date"], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds > now.ToUnixTimeSeconds() + 300
            || now.ToUnixTimeSeconds() - seconds > (long)MaxAge.TotalSeconds)
        {
            return null;
        }

        return ReadId(fields["user"]);
    }

    private static long? ReadId(string? userJson)
    {
        if (string.IsNullOrEmpty(userJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(userJson);

            return document.RootElement.TryGetProperty("id", out var id) && id.TryGetInt64(out var value) && value > 0
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsHex(string value) => value.All(char.IsAsciiHexDigit);
}
