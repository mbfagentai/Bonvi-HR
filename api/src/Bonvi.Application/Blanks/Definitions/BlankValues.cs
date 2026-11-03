using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>
/// Shart va hisoblash uchun JSON qiymatni TURSIZ o'qish — veb (TS) baholovchisi ham aynan shu qoidada.
/// </summary>
/// <remarks>
/// Qiymat: son → <see cref="decimal"/>, matn → <see cref="string"/> (bo'sh → <c>null</c>), mantiq →
/// <see cref="bool"/>, massiv → <see cref="List{T}"/>. Obyektdan <c>value</c> (choice) yoki <c>id</c>
/// (ref) olinadi. Sana — <c>yyyy-MM-dd</c> bilan boshlanadigan matn, kerak bo'lgan amalda o'qiladi.
/// </remarks>
internal static class BlankValues
{
    public static object? FromNode(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonArray array:
                return array.Select(FromNode).ToList();
            case JsonObject obj:
                if (obj.TryGetPropertyValue("value", out var value))
                {
                    return FromNode(value);
                }

                return obj.TryGetPropertyValue("id", out var id) ? FromNode(id) : null;
            case JsonValue scalar:
                return scalar.GetValueKind() switch
                {
                    JsonValueKind.Number => TryNumber(scalar, out var number) ? Normalize(number) : null,
                    JsonValueKind.String => scalar.GetValue<string>() is { } text && !string.IsNullOrWhiteSpace(text) ? text : null,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null,
                };
            default:
                return null;
        }
    }

    public static JsonNode? ToNode(object? value) => value switch
    {
        decimal number => JsonValue.Create(Normalize(number)),
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        _ => null,
    };

    /// <summary>Bo'shmi: <c>null</c>, bo'sh/bo'shliq matn, bo'sh massiv yoki xossasiz obyekt (§5.3).</summary>
    public static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        JsonValue scalar => scalar.GetValueKind() switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(scalar.GetValue<string>()),
            _ => false,
        },
        _ => false,
    };

    /// <summary>
    /// JSON son → decimal. Matn orqali: xotirada yaratilgan <c>JsonValue&lt;long&gt;</c> ning
    /// <c>TryGetValue&lt;decimal&gt;</c> i turni o'girmaydi va <c>false</c> qaytaradi.
    /// </summary>
    public static bool TryNumber(JsonValue value, out decimal number)
    {
        number = 0;
        return value.GetValueKind() == JsonValueKind.Number
            && decimal.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    public static bool TryDate(object? value, out DateOnly date)
    {
        date = default;
        return value is string text
            && text.Length >= 10
            && DateOnly.TryParseExact(text.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Teng qiymatlar: son sonday, matn ordinal, mantiq mantiqday; turi har xil → teng emas.</summary>
    public static bool AreEqual(object? left, object? right) => (left, right) switch
    {
        (decimal a, decimal b) => a == b,
        (string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
        (bool a, bool b) => a == b,
        _ => false,
    };

    /// <summary>Oxirgi nollarni olib tashlaydi (3.50 → 3.5) — JSON'da bir xil ko'rinsin.</summary>
    public static decimal Normalize(decimal value) => value / 1.000000000000000000000000000000000m;
}
