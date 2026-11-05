using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Commands;

/// <summary>
/// §10 #10: what «Bajarildi» sends, checked against the step's <c>manual.inputs</c> and turned into the step's payload.
/// The aggregate enforces state rules only and the gates look at facts only, so a required input is enforced here and
/// nowhere else: an override on Q03 lifts the JSHSHIR block, not the r01-9 <c>payment</c> input.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wire form is the stored form</b>: a flat JSON object keyed by input id. <c>text</c>, <c>longtext</c>,
/// <c>choice</c> — a string; <c>date</c> — <c>yyyy-MM-dd</c>; <c>int</c> — a number; <c>checklist</c> — an object from
/// item value to <see cref="ChecklistDone"/> or <see cref="ChecklistNotNeeded"/>. The pass reads these values back
/// (<see cref="Engine.ProcessStepPayload"/>).
/// </para>
/// <para>
/// <b>Canonical</b>: keys sorted, no spacing, text trimmed. <c>jsonb</c> re-spaces what it stores, so
/// <c>evidence_sha256</c> is the hash of this form, reproducible from the stored payload (§6.3).
/// </para>
/// </remarks>
public static class ProcessManualInputs
{
    /// <summary>A checklist item ticked.</summary>
    public const string ChecklistDone = "done";

    /// <summary>A checklist item marked «yo'q / kerak emas».</summary>
    public const string ChecklistNotNeeded = "na";

    /// <summary>Longest <c>text</c> input.</summary>
    public const int MaxTextLength = 1000;

    /// <summary>Longest <c>longtext</c> input.</summary>
    public const int MaxLongTextLength = 4000;

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The canonical payload; <c>null</c> when nothing was given and nothing is required.</summary>
    /// <param name="manual">The step's <c>manual</c>; <c>null</c> — the step asks for nothing.</param>
    /// <param name="inputs">The request's inputs; absent and JSON <c>null</c> are the same.</param>
    /// <param name="factOf">A fact of the case, for <c>requiredWhen.fact</c>.</param>
    public static Result<string?> Validate(ProcessManual? manual, JsonElement? inputs, Func<string, FactValue> factOf)
    {
        ArgumentNullException.ThrowIfNull(factOf);

        var definitions = manual?.Inputs ?? [];

        var given = ReadGiven(inputs);
        if (given is null || given.Keys.Any(id => definitions.All(input => input.Id != id)))
        {
            return ProcessErrors.Invalid;
        }

        var values = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!given.TryGetValue(definition.Id, out var element))
            {
                continue;
            }

            if (!TryNormalise(definition, element, out var value))
            {
                return ProcessErrors.Invalid;
            }

            if (value is not null)
            {
                values[definition.Id] = value;
            }
        }

        if (definitions.Any(definition => IsRequired(definition, values, factOf) && !IsAnswered(definition, values)))
        {
            return ProcessErrors.Invalid;
        }

        return Result.Success(values.Count == 0 ? null : ToJson(values));
    }

    /// <summary>§6.3: SHA-256 of the canonical payload, lower-case hex.</summary>
    public static string EvidenceOf(string canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    /// <summary>The keys of the request, nulls dropped; <c>null</c> when it is not an object or repeats a key.</summary>
    private static Dictionary<string, JsonElement>? ReadGiven(JsonElement? inputs)
    {
        var given = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (inputs is not { } root || root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return given;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                return null;
            }

            if (property.Value.ValueKind != JsonValueKind.Null)
            {
                given[property.Name] = property.Value;
            }
        }

        return given;
    }

    /// <summary><c>false</c> — the value is wrong; <c>true</c> with a <c>null</c> value — blank text, the same as absent.</summary>
    private static bool TryNormalise(ProcessManualInput definition, JsonElement element, out JsonNode? value)
    {
        value = null;

        switch (definition.Type)
        {
            case ProcessInputType.Text:
                return TryText(element, MaxTextLength, out value);

            case ProcessInputType.LongText:
                return TryText(element, MaxLongTextLength, out value);

            case ProcessInputType.Date:
                if (element.ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(element.GetString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    value = JsonValue.Create(date.ToString(DateFormat, CultureInfo.InvariantCulture));
                    return true;
                }

                return false;

            case ProcessInputType.WholeNumber:
                if (element.ValueKind == JsonValueKind.Number
                    && element.TryGetInt32(out var number)
                    && (definition.Min is not { } min || number >= min)
                    && (definition.Max is not { } max || number <= max))
                {
                    value = JsonValue.Create(number);
                    return true;
                }

                return false;

            case ProcessInputType.Choice:
                if (element.ValueKind == JsonValueKind.String && definition.Options.Any(option => option.Value == element.GetString()))
                {
                    value = JsonValue.Create(element.GetString());
                    return true;
                }

                return false;

            case ProcessInputType.Checklist:
                return TryChecklist(definition, element, out value);

            default:
                return false;
        }
    }

    private static bool TryText(JsonElement element, int maxLength, out JsonNode? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString()!.Trim();
        if (text.Length > maxLength)
        {
            return false;
        }

        if (text.Length > 0)
        {
            value = JsonValue.Create(text);
        }

        return true;
    }

    private static bool TryChecklist(ProcessManualInput definition, JsonElement element, out JsonNode? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var items = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateObject())
        {
            var known = definition.Options.Any(option => option.Value == item.Name);
            var mark = item.Value.ValueKind == JsonValueKind.String ? item.Value.GetString() : null;

            if (!known || mark is not (ChecklistDone or ChecklistNotNeeded) || !items.TryAdd(item.Name, mark))
            {
                return false;
            }
        }

        if (items.Count > 0)
        {
            var checklist = new JsonObject();
            foreach (var (name, mark) in items)
            {
                checklist[name] = mark;
            }

            value = checklist;
        }

        return true;
    }

    private static bool IsRequired(ProcessManualInput definition, SortedDictionary<string, JsonNode> values, Func<string, FactValue> factOf)
    {
        if (definition.Required)
        {
            return true;
        }

        if (definition.RequiredWhen is not { } when)
        {
            return false;
        }

        if (when.Input is { } input)
        {
            return values.TryGetValue(input, out var other) && when.EqualsValue.ToString() == ScalarText(other);
        }

        return when.Fact is { } fact && factOf(fact) is { Holds: true, Value: { } factValue } && when.EqualsValue.ToString() == factValue;
    }

    /// <summary>A required checklist counts only with every item ticked or marked; anything else — once it has a value.</summary>
    private static bool IsAnswered(ProcessManualInput definition, SortedDictionary<string, JsonNode> values)
    {
        if (!values.TryGetValue(definition.Id, out var value))
        {
            return false;
        }

        return definition.Type != ProcessInputType.Checklist || value.AsObject().Count == definition.Options.Count;
    }

    /// <summary>A checklist has no scalar form: nothing compares equal to it.</summary>
    private static string? ScalarText(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        return value.GetValueKind() == JsonValueKind.Number
            ? value.GetValue<int>().ToString(CultureInfo.InvariantCulture)
            : value.GetValue<string>();
    }

    private static string ToJson(SortedDictionary<string, JsonNode> values)
    {
        var root = new JsonObject();
        foreach (var (id, value) in values)
        {
            root[id] = value;
        }

        return root.ToJsonString();
    }
}
