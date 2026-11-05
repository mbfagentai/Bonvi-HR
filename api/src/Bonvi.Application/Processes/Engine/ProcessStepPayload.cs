using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Processes.Engine;

/// <summary>The JSON of <c>process_case_steps.payload</c>: fact values written by the pass, manual inputs read by it.</summary>
public static class ProcessStepPayload
{
    /// <summary><c>{"value":"…"}</c>, or <c>null</c> for a fact with no value.</summary>
    public static string? ForFactValue(string? value)
        => value is null ? null : new JsonObject { ["value"] = value }.ToJsonString();

    /// <summary><c>{"daysToClose":n}</c> (§3.3 r02-14).</summary>
    public static string ForDaysToClose(int days) => new JsonObject { ["daysToClose"] = days }.ToJsonString();

    /// <summary>
    /// Same JSON regardless of formatting. PostgreSQL <c>jsonb</c> hands the text back re-spaced, so a plain
    /// string comparison would report a change on every pass and wake the SSE signal (§7.4 p.6).
    /// </summary>
    public static bool AreEqual(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left == right;
        }

        return left == right || JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right));
    }

    /// <summary>The offer step's manual input (§1.5): the start date while HR-F05 is not served.</summary>
    public const string StartDateInput = "startDate";

    /// <summary>One input of a manual payload as a date; absent or not <c>yyyy-MM-dd</c> → <c>null</c>.</summary>
    public static DateOnly? ReadDateInput(string? payload, string inputId)
        => DateOnly.TryParseExact(ReadInput(payload, inputId), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>One input of a manual payload as text (string or number); absent, null or malformed → <c>null</c>.</summary>
    public static string? ReadInput(string? payload, string inputId)
    {
        ArgumentNullException.ThrowIfNull(inputId);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(payload) is JsonObject root && root[inputId] is JsonValue value
                ? value.ToString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
