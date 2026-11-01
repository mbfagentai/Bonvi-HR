using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// HRMentor ning kanonik JSON i (<c>json.dumps(sort_keys=True, separators=(",", ":"),
/// ensure_ascii=False)</c>) — ta'rifning <c>sha256</c> i ikki tomonda bir xil chiqishi uchun.
/// </summary>
/// <remarks>
/// Kalitlar Unicode kod nuqtalari bo'yicha saralanadi (Python <c>str</c> taqqoslashi), matn
/// UTF-8 da qoladi, faqat <c>"</c>, <c>\</c> va U+0020 dan kichik belgilar qochiriladi
/// (<c>\n \r \t \b \f</c>, qolganlari <c>\u00xx</c> kichik harf bilan). Sonlar — butun son
/// matni qanday kelgan bo'lsa shunday (ta'rifda kasr son yo'q; kelsa xesh mos kelmaydi va
/// ta'rif rad etiladi).
/// </remarks>
public static class CanonicalJson
{
    public static string Write(JsonNode? node)
    {
        var builder = new StringBuilder();
        Append(builder, node);
        return builder.ToString();
    }

    public static string Sha256(JsonNode? node)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Write(node))));

    public static int Size(JsonNode? node) => Encoding.UTF8.GetByteCount(Write(node));

    private static void Append(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case null:
                _ = builder.Append("null");
                break;

            case JsonObject value:
                _ = builder.Append('{');
                var first = true;
                foreach (var pair in value.OrderBy(pair => pair.Key, CodePointComparer.Instance))
                {
                    if (!first)
                    {
                        _ = builder.Append(',');
                    }

                    first = false;
                    String(builder, pair.Key);
                    _ = builder.Append(':');
                    Append(builder, pair.Value);
                }

                _ = builder.Append('}');
                break;

            case JsonArray array:
                _ = builder.Append('[');
                for (var index = 0; index < array.Count; index++)
                {
                    if (index > 0)
                    {
                        _ = builder.Append(',');
                    }

                    Append(builder, array[index]);
                }

                _ = builder.Append(']');
                break;

            case JsonValue value:
                switch (value.GetValueKind())
                {
                    case JsonValueKind.String:
                        String(builder, value.GetValue<string>());
                        break;
                    case JsonValueKind.True:
                        _ = builder.Append("true");
                        break;
                    case JsonValueKind.False:
                        _ = builder.Append("false");
                        break;
                    case JsonValueKind.Number:
                        _ = builder.Append(value.ToJsonString());
                        break;
                    default:
                        _ = builder.Append("null");
                        break;
                }

                break;
        }
    }

    private static void String(StringBuilder builder, string text)
    {
        _ = builder.Append('"');
        foreach (var symbol in text)
        {
            _ = symbol switch
            {
                '"' => builder.Append("\\\""),
                '\\' => builder.Append("\\\\"),
                '\n' => builder.Append("\\n"),
                '\r' => builder.Append("\\r"),
                '\t' => builder.Append("\\t"),
                '\b' => builder.Append("\\b"),
                '\f' => builder.Append("\\f"),
                < ' ' => builder.Append("\\u").Append(((int)symbol).ToString("x4", CultureInfo.InvariantCulture)),
                _ => builder.Append(symbol),
            };
        }

        _ = builder.Append('"');
    }

    /// <summary>Kod nuqtalari bo'yicha taqqoslash (UTF-16 birliklari emas).</summary>
    private sealed class CodePointComparer : IComparer<string>
    {
        public static readonly CodePointComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var left = x.EnumerateRunes();
            var right = y.EnumerateRunes();
            while (true)
            {
                var hasLeft = left.MoveNext();
                var hasRight = right.MoveNext();
                if (!hasLeft || !hasRight)
                {
                    return hasLeft.CompareTo(hasRight);
                }

                var compared = left.Current.Value.CompareTo(right.Current.Value);
                if (compared != 0)
                {
                    return compared;
                }
            }
        }
    }
}
