using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Processes.Inputs;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Commands;

/// <summary>A manager's close without required data: what the audit keeps.</summary>
public sealed record ProcessMissingOverride(string Reason, IReadOnlyList<ProcessAutoItem> Items);

/// <summary>What validated inputs become.</summary>
/// <param name="Payload">The canonical payload; <c>null</c> when there is nothing to store.</param>
/// <param name="MissingOverride">Set only when a manager's reason let required missing data through.</param>
public sealed record ProcessManualOutcome(string? Payload, ProcessMissingOverride? MissingOverride);

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
/// <b>Data-backed items</b> (<see cref="ProcessInputSources"/>): the client's mark is dropped and the system's stands, so a
/// missing document can't be ticked. The payload stays flat and readable as before, and two root keys say what the marks came
/// from: <see cref="AutoSourcesKey"/> and, on a manager's close, <see cref="MissingOverrideKey"/>. They are not input ids, so a
/// request naming one is refused, and no template may use one (<c>ProcessInputSourcesTests</c>).
/// </para>
/// <para>
/// <b>Canonical</b>: keys sorted, no spacing, text trimmed. <c>jsonb</c> re-spaces what it stores, so
/// <c>evidence_sha256</c> is the hash of this form, reproducible from the stored payload (§6.3). Non-ASCII stays in the
/// default encoder's <c>\uXXXX</c> form, which stored hashes depend on: leave the encoder alone.
/// </para>
/// </remarks>
public static class ProcessManualInputs
{
    /// <summary>Payload root key: <c>{input: {item: source token}}</c> of the marks the system set.</summary>
    public const string AutoSourcesKey = "autoSources";

    /// <summary>Payload root key: <c>{reason, items: [{input, item}]}</c>, a manager closing without the required data.</summary>
    public const string MissingOverrideKey = "missingOverride";

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
        var outcome = Validate(manual, inputs, factOf, autoItems: null, missingReason: null, canManage: false);

        return outcome.IsFailure ? outcome.Error : Result.Success(outcome.Value.Payload);
    }

    /// <summary>The canonical payload with the data-backed items settled, and what a manager's close let through.</summary>
    /// <param name="manual">The step's <c>manual</c>; <c>null</c> — the step asks for nothing.</param>
    /// <param name="inputs">The request's inputs; absent and JSON <c>null</c> are the same.</param>
    /// <param name="factOf">A fact of the case, for <c>requiredWhen.fact</c>.</param>
    /// <param name="autoItems">The step's data-backed items; <c>null</c> — it has none.</param>
    /// <param name="missingReason">Why it closes without required data. Read only when some is missing.</param>
    /// <param name="canManage">Whether the viewer may close it so.</param>
    public static Result<ProcessManualOutcome> Validate(
        ProcessManual? manual,
        JsonElement? inputs,
        Func<string, FactValue> factOf,
        ProcessStepAutoItems? autoItems,
        string? missingReason,
        bool canManage)
    {
        ArgumentNullException.ThrowIfNull(factOf);

        if (inputs is { } body && !IsReadable(body))
        {
            return ProcessErrors.Invalid;
        }

        var definitions = manual?.Inputs ?? [];

        var given = ReadGiven(inputs);
        if (given is null || given.Keys.Any(id => definitions.All(input => input.Id != id)))
        {
            return ProcessErrors.Invalid;
        }

        var values = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!given.TryGetValue(definition.Id, out var element) || element.ValueKind == JsonValueKind.Null)
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

        var autoSources = SettleAutoItems(definitions, autoItems, values);

        if (definitions.Any(definition => IsRequired(definition, values, factOf) && !IsAnswered(definition, values, MissingOf(definition, autoItems))))
        {
            return ProcessErrors.Invalid;
        }

        var missing = definitions
            .Where(definition => IsRequired(definition, values, factOf))
            .SelectMany(definition => MissingOf(definition, autoItems))
            .ToList();

        // Added after the required checks, which read input values only.
        if (autoSources.Count > 0)
        {
            values[AutoSourcesKey] = autoSources;
        }

        if (missing.Count == 0)
        {
            return new ProcessManualOutcome(values.Count == 0 ? null : ToJson(values), null);
        }

        var reason = missingReason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            return ProcessErrors.MissingData(missing.Select(item => (item.Input, item.Value, item.Label)));
        }

        if (!canManage)
        {
            return ProcessErrors.Forbidden;
        }

        if (reason.Length > ProcessCase.MaxCancelReasonLength || ProcessFreeText.HasNul(reason))
        {
            return ProcessErrors.Invalid;
        }

        values[MissingOverrideKey] = OverrideOf(reason, missing);

        return new ProcessManualOutcome(ToJson(values), new ProcessMissingOverride(reason, missing));
    }

    /// <summary>§6.3: SHA-256 of the canonical payload, lower-case hex.</summary>
    public static string EvidenceOf(string canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    /// <summary>
    /// A lone surrogate or broken UTF-8 parses, and the body binds without being read, yet <see cref="JsonElement.GetString"/>
    /// and <see cref="JsonProperty.Name"/> throw on it. Checked once, up front, so what follows reads freely and such a
    /// request is refused as a wrong input rather than ending in a 500.
    /// </summary>
    private static bool IsReadable(JsonElement element)
    {
        try
        {
            ReadAllText(element);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ReadAllText(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                _ = element.GetString();
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    _ = property.Name;
                    ReadAllText(property.Value);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ReadAllText(item);
                }

                break;
        }
    }

    /// <summary>
    /// The keys of the request, those with a null value too: such a value reads as absent, but its key still has to be
    /// one the step asks for. <c>null</c> when it is not an object or repeats a key.
    /// </summary>
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

            given[property.Name] = property.Value;
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
        if (text.Length > maxLength || ProcessFreeText.HasNul(text))
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
            value = ObjectOf(items);
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

    /// <summary>
    /// A required checklist counts only with every item ticked or marked; anything else — once it has a value. A missing
    /// data-backed item has no mark and can't get one: whether it may stay so is the caller's next question, not this one.
    /// </summary>
    private static bool IsAnswered(ProcessManualInput definition, SortedDictionary<string, JsonNode> values, List<ProcessAutoItem> missing)
    {
        if (definition.Type != ProcessInputType.Checklist)
        {
            return values.ContainsKey(definition.Id);
        }

        var settled = missing.Count + (values.TryGetValue(definition.Id, out var value) ? value.AsObject().Count : 0);

        return settled > 0 && settled == definition.Options.Count;
    }

    private static List<ProcessAutoItem> MissingOf(ProcessManualInput definition, ProcessStepAutoItems? autoItems)
        => autoItems is null ? [] : [.. autoItems.Of(definition.Id).Where(item => item.State == ProcessAutoItemState.Missing)];

    /// <summary>Puts the system's marks over the client's. Returns <c>{input: {item: source token}}</c> of those it set.</summary>
    private static JsonObject SettleAutoItems(IReadOnlyList<ProcessManualInput> definitions, ProcessStepAutoItems? autoItems, SortedDictionary<string, JsonNode> values)
    {
        var sources = new JsonObject();
        if (autoItems is null)
        {
            return sources;
        }

        // Ordinal order, as every other object in the payload: this is what the evidence hash is taken over.
        foreach (var definition in definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal))
        {
            var items = autoItems.Of(definition.Id);
            if (items.Count == 0)
            {
                continue;
            }

            var marks = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (values.TryGetValue(definition.Id, out var given))
            {
                foreach (var (item, mark) in given.AsObject())
                {
                    marks[item] = mark!.GetValue<string>();
                }
            }

            var itemSources = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                marks.Remove(item.Value);

                if (item.Mark is { } mark)
                {
                    marks[item.Value] = mark;
                    itemSources[item.Value] = item.SourceToken ?? string.Empty;
                }
            }

            values.Remove(definition.Id);
            if (marks.Count > 0)
            {
                values[definition.Id] = ObjectOf(marks);
            }

            if (itemSources.Count > 0)
            {
                sources[definition.Id] = ObjectOf(itemSources);
            }
        }

        return sources;
    }

    private static JsonObject OverrideOf(string reason, List<ProcessAutoItem> missing)
    {
        var items = new JsonArray();
        foreach (var item in missing)
        {
            items.Add(new JsonObject { ["input"] = item.Input, ["item"] = item.Value });
        }

        return new JsonObject { ["items"] = items, ["reason"] = reason };
    }

    private static JsonObject ObjectOf(SortedDictionary<string, string> entries)
    {
        var result = new JsonObject();
        foreach (var (name, entry) in entries)
        {
            result[name] = entry;
        }

        return result;
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
