using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Processes.Definitions;

/// <summary>One problem in a template: where (<c>steps[3].due.from</c>) and what.</summary>
public sealed record ProcessDefinitionError(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}

/// <summary>
/// Reads template JSON into <see cref="ProcessDefinition"/> and collects SHAPE errors: unknown key, unknown
/// enum value (kind, input type, gate mode, due rule), wrong JSON type, missing required key. Meaning
/// (references, duplicates, catalogues) is checked by <see cref="ProcessDefinitionValidator"/>.
/// </summary>
/// <remarks>
/// Does not stop at the first error — the author sees every problem in one run. A broken node is dropped and the
/// rest is still read. A key whose value is JSON <c>null</c> counts as absent. Mirrors
/// <c>Blanks.Definitions.BlankDefinitionReader</c>.
/// </remarks>
internal sealed class ProcessDefinitionReader
{
    private static readonly string[] TopKeys = ["schema", "code", "version", "title", "source", "subjectKind", "board", "steps"];

    private static readonly string[] BoardKeys = ["key", "title"];

    private static readonly string[] StepKeys =
    [
        "id", "title", "column", "role", "fallbackRole", "blank", "signature", "kind", "prereqs", "optional", "when",
        "due", "skipIfDueOnOrAfter", "gate", "completion", "manual", "skippable", "legacySkippable", "guide", "sourceRef",
        "link",
    ];

    private static readonly string[] ConditionKeys = ["fact", "equals"];

    private static readonly string[] DueKeys = ["from", "workingDays", "days", "rule", "fallback"];

    private static readonly string[] GateKeys = ["code", "mode"];

    private static readonly string[] CompletionKeys = ["fact", "args"];

    private static readonly string[] CompletionArgKeys = ["equals", "in", "allOf", "legacyAnyOf", "manualValue", "valueField", "byValue"];

    private static readonly string[] ManualKeys = ["allowed", "inputs"];

    private static readonly string[] InputKeys = ["id", "label", "type", "required", "requiredWhen", "options", "min", "max"];

    private static readonly string[] RequiredWhenKeys = ["input", "fact", "equals"];

    private static readonly string[] OptionKeys = ["value", "label"];

    private static readonly string[] LinkKeys = ["screen", "args"];

    internal static IReadOnlyDictionary<string, ProcessStepKind> KindNames { get; } =
        new Dictionary<string, ProcessStepKind>(StringComparer.Ordinal)
        {
            ["fact"] = ProcessStepKind.Fact,
            ["manual"] = ProcessStepKind.Manual,
            ["auto"] = ProcessStepKind.Auto,
        };

    internal static IReadOnlyDictionary<string, ProcessInputType> InputTypeNames { get; } =
        new Dictionary<string, ProcessInputType>(StringComparer.Ordinal)
        {
            ["text"] = ProcessInputType.Text,
            ["longtext"] = ProcessInputType.LongText,
            ["date"] = ProcessInputType.Date,
            ["int"] = ProcessInputType.WholeNumber,
            ["choice"] = ProcessInputType.Choice,
            ["checklist"] = ProcessInputType.Checklist,
        };

    internal static IReadOnlyDictionary<string, ProcessGateMode> GateModeNames { get; } =
        new Dictionary<string, ProcessGateMode>(StringComparer.Ordinal)
        {
            ["block"] = ProcessGateMode.Block,
            ["warn"] = ProcessGateMode.Warn,
        };

    public List<ProcessDefinitionError> Errors { get; } = [];

    public ProcessDefinition? ReadDefinition(JsonNode? root)
    {
        if (AsObject(root, "$") is not { } top)
        {
            return null;
        }

        OnlyKeys(top, "$", TopKeys);

        var schema = Int(top, "schema", "$");

        if (schema is null)
        {
            Add("$.schema", "Required key is missing.");
        }

        var code = String(top, "code", "$", required: true) ?? string.Empty;
        var version = String(top, "version", "$", required: true) ?? string.Empty;
        var title = String(top, "title", "$", required: true) ?? string.Empty;
        var source = String(top, "source", "$", required: true) ?? string.Empty;
        var subjectKind = String(top, "subjectKind", "$", required: true) ?? string.Empty;

        var board = new List<ProcessBoardColumn>();

        if (Array(top, "board", "$", required: true) is { } columns)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var path = $"board[{i}]";

                if (AsObject(columns[i], path) is { } column)
                {
                    OnlyKeys(column, path, BoardKeys);
                    board.Add(new ProcessBoardColumn(
                        String(column, "key", path, required: true) ?? string.Empty,
                        String(column, "title", path, required: true) ?? string.Empty));
                }
            }
        }

        var steps = new List<ProcessStep>();

        if (Array(top, "steps", "$", required: true) is { } stepNodes)
        {
            for (var i = 0; i < stepNodes.Count; i++)
            {
                if (ReadStep(stepNodes[i], $"steps[{i}]") is { } step)
                {
                    steps.Add(step);
                }
            }
        }

        return new ProcessDefinition(schema ?? 0, code, version, title, source, subjectKind, board, steps);
    }

    private ProcessStep? ReadStep(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, StepKeys);

        var kindName = String(obj, "kind", path, required: true);
        ProcessStepKind? kind = null;

        if (kindName is not null)
        {
            if (KindNames.TryGetValue(kindName, out var known))
            {
                kind = known;
            }
            else
            {
                Add($"{path}.kind", $"Unknown kind «{kindName}» (fact, manual, auto).");
            }
        }

        var link = Present(obj, "link") is { } linkNode
            ? ReadLink(linkNode, $"{path}.link")
            : Fail<ProcessLink>($"{path}.link", "Required key is missing.");

        var step = new ProcessStep
        {
            Id = String(obj, "id", path, required: true) ?? string.Empty,
            Title = String(obj, "title", path, required: true) ?? string.Empty,
            Column = String(obj, "column", path, required: true) ?? string.Empty,
            Role = String(obj, "role", path, required: true) ?? string.Empty,
            FallbackRole = String(obj, "fallbackRole", path, required: false),
            Blank = String(obj, "blank", path, required: false),
            Signature = String(obj, "signature", path, required: false),
            Kind = kind ?? ProcessStepKind.Fact,
            Prereqs = Strings(obj, "prereqs", path),
            Optional = Bool(obj, "optional", path),
            When = Present(obj, "when") is { } when ? ReadCondition(when, $"{path}.when") : null,
            Due = Present(obj, "due") is { } due ? ReadDue(due, $"{path}.due") : null,
            SkipIfDueOnOrAfter = Present(obj, "skipIfDueOnOrAfter") is { } cutoff
                ? ReadDue(cutoff, $"{path}.skipIfDueOnOrAfter")
                : null,
            Gates = Present(obj, "gate") is { } gate ? ReadGates(gate, $"{path}.gate") : [],
            Completion = Present(obj, "completion") is { } completion ? ReadCompletion(completion, $"{path}.completion") : null,
            Manual = Present(obj, "manual") is { } manual ? ReadManual(manual, $"{path}.manual") : null,
            Skippable = Bool(obj, "skippable", path),
            LegacySkippable = Bool(obj, "legacySkippable", path),
            Guide = Strings(obj, "guide", path),
            SourceRef = String(obj, "sourceRef", path, required: false),
            Link = link ?? new ProcessLink(ProcessScreens.None, new Dictionary<string, string>()),
        };

        return kind is null ? null : step;
    }

    private ProcessCondition? ReadCondition(JsonNode node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, ConditionKeys);
        var fact = String(obj, "fact", path, required: true);
        var value = Scalar(obj, "equals", path, required: true);

        return fact is null || value is null ? null : new ProcessCondition(fact, value);
    }

    private ProcessDueRule? ReadDue(JsonNode node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, DueKeys);
        var from = String(obj, "from", path, required: true);
        var workingDays = Int(obj, "workingDays", path);
        var days = Int(obj, "days", path);
        var rule = String(obj, "rule", path, required: false);
        var fallback = Present(obj, "fallback") is { } fallbackNode ? ReadDue(fallbackNode, $"{path}.fallback") : null;

        var given = (workingDays is null ? 0 : 1) + (days is null ? 0 : 1) + (rule is null ? 0 : 1);

        if (given != 1)
        {
            return Fail<ProcessDueRule>(path, "Exactly one of workingDays, days or rule is required.");
        }

        if (rule is not null && rule != ProcessDueRule.NextCouncilDayRule)
        {
            return Fail<ProcessDueRule>($"{path}.rule", $"Unknown rule «{rule}» (nextCouncilDay).");
        }

        if (from is null)
        {
            return null;
        }

        return workingDays is { } wd ? new ProcessDueRule(from, ProcessDueKind.WorkingDays, wd, fallback)
            : days is { } d ? new ProcessDueRule(from, ProcessDueKind.CalendarDays, d, fallback)
            : new ProcessDueRule(from, ProcessDueKind.NextCouncilDay, 0, fallback);
    }

    /// <summary><c>gate</c> is one object, or an array when a step has several gates (r02-13: Q01 + Q04).</summary>
    private List<ProcessGateRef> ReadGates(JsonNode node, string path)
    {
        var result = new List<ProcessGateRef>();

        if (node is JsonArray items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (ReadGate(items[i], $"{path}[{i}]") is { } gate)
                {
                    result.Add(gate);
                }
            }
        }
        else if (ReadGate(node, path) is { } gate)
        {
            result.Add(gate);
        }

        return result;
    }

    private ProcessGateRef? ReadGate(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, GateKeys);
        var code = String(obj, "code", path, required: true);
        var modeName = String(obj, "mode", path, required: true);

        if (modeName is not null && !GateModeNames.ContainsKey(modeName))
        {
            return Fail<ProcessGateRef>($"{path}.mode", $"Unknown mode «{modeName}» (block, warn).");
        }

        return code is null || modeName is null ? null : new ProcessGateRef(code, GateModeNames[modeName]);
    }

    private ProcessCompletion? ReadCompletion(JsonNode node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, CompletionKeys);
        var fact = String(obj, "fact", path, required: true);

        if (fact is null)
        {
            return null;
        }

        if (Present(obj, "args") is not { } argsNode)
        {
            return new ProcessCompletion { Fact = fact };
        }

        var argsPath = $"{path}.args";

        if (AsObject(argsNode, argsPath) is not { } args)
        {
            return null;
        }

        OnlyKeys(args, argsPath, CompletionArgKeys);

        var inValues = new List<ProcessScalar>();

        if (Array(args, "in", argsPath, required: false) is { } inNodes)
        {
            for (var i = 0; i < inNodes.Count; i++)
            {
                if (ToScalar(inNodes[i], $"{argsPath}.in[{i}]") is { } value)
                {
                    inValues.Add(value);
                }
            }
        }

        var byValue = new Dictionary<string, ProcessCompletion>(StringComparer.Ordinal);

        if (Present(args, "byValue") is { } byValueNode && AsObject(byValueNode, $"{argsPath}.byValue") is { } branches)
        {
            foreach (var (value, branchNode) in branches)
            {
                var branchPath = $"{argsPath}.byValue.{value}";

                if (branchNode is null)
                {
                    Add(branchPath, "Object expected.");
                }
                else if (ReadCompletion(branchNode, branchPath) is { } branch)
                {
                    byValue[value] = branch;
                }
            }
        }

        return new ProcessCompletion
        {
            Fact = fact,
            EqualsValue = Scalar(args, "equals", argsPath, required: false),
            InValues = inValues,
            AllOf = Strings(args, "allOf", argsPath),
            LegacyAnyOf = Strings(args, "legacyAnyOf", argsPath),
            ManualValue = String(args, "manualValue", argsPath, required: false),
            ValueField = String(args, "valueField", argsPath, required: false),
            ByValue = byValue,
        };
    }

    private ProcessManual? ReadManual(JsonNode node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, ManualKeys);
        var inputs = new List<ProcessManualInput>();

        if (Array(obj, "inputs", path, required: false) is { } inputNodes)
        {
            for (var i = 0; i < inputNodes.Count; i++)
            {
                if (ReadInput(inputNodes[i], $"{path}.inputs[{i}]") is { } input)
                {
                    inputs.Add(input);
                }
            }
        }

        return new ProcessManual(Bool(obj, "allowed", path), inputs);
    }

    private ProcessManualInput? ReadInput(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, InputKeys);
        var typeName = String(obj, "type", path, required: true);

        if (typeName is not null && !InputTypeNames.ContainsKey(typeName))
        {
            Add($"{path}.type", $"Unknown input type «{typeName}» (text, longtext, date, int, choice, checklist).");
            return null;
        }

        var options = new List<ProcessOption>();

        if (Array(obj, "options", path, required: false) is { } optionNodes)
        {
            for (var i = 0; i < optionNodes.Count; i++)
            {
                var optionPath = $"{path}.options[{i}]";

                if (AsObject(optionNodes[i], optionPath) is { } option)
                {
                    OnlyKeys(option, optionPath, OptionKeys);
                    options.Add(new ProcessOption(
                        String(option, "value", optionPath, required: true) ?? string.Empty,
                        String(option, "label", optionPath, required: true) ?? string.Empty));
                }
            }
        }

        ProcessRequiredWhen? requiredWhen = null;

        if (Present(obj, "requiredWhen") is { } whenNode && AsObject(whenNode, $"{path}.requiredWhen") is { } when)
        {
            var whenPath = $"{path}.requiredWhen";
            OnlyKeys(when, whenPath, RequiredWhenKeys);

            var value = Scalar(when, "equals", whenPath, required: true);
            requiredWhen = value is null
                ? null
                : new ProcessRequiredWhen(
                    String(when, "input", whenPath, required: false),
                    String(when, "fact", whenPath, required: false),
                    value);
        }

        if (typeName is null)
        {
            return null;
        }

        return new ProcessManualInput
        {
            Id = String(obj, "id", path, required: true) ?? string.Empty,
            Label = String(obj, "label", path, required: true) ?? string.Empty,
            Type = InputTypeNames[typeName],
            Required = Bool(obj, "required", path),
            RequiredWhen = requiredWhen,
            Options = options,
            Min = Int(obj, "min", path),
            Max = Int(obj, "max", path),
        };
    }

    private ProcessLink? ReadLink(JsonNode node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, LinkKeys);
        var screen = String(obj, "screen", path, required: true);
        var args = new Dictionary<string, string>(StringComparer.Ordinal);

        if (Present(obj, "args") is { } argsNode && AsObject(argsNode, $"{path}.args") is { } argsObj)
        {
            foreach (var (key, _) in argsObj)
            {
                if (String(argsObj, key, $"{path}.args", required: true) is { } value)
                {
                    args[key] = value;
                }
            }
        }

        return screen is null ? null : new ProcessLink(screen, args);
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    private void Add(string path, string message) => Errors.Add(new ProcessDefinitionError(path, message));

    private T? Fail<T>(string path, string message)
    {
        Add(path, message);
        return default;
    }

    private static JsonNode? Present(JsonObject obj, string key)
        => obj.TryGetPropertyValue(key, out var node) ? node : null;

    private JsonObject? AsObject(JsonNode? node, string path)
        => node as JsonObject ?? Fail<JsonObject>(path, "Object expected.");

    private void OnlyKeys(JsonObject obj, string path, string[] allowed)
    {
        foreach (var (key, _) in obj)
        {
            if (!allowed.Contains(key, StringComparer.Ordinal))
            {
                Add($"{path}.{key}", "Unknown key.");
            }
        }
    }

    private string? String(JsonObject obj, string key, string path, bool required)
    {
        if (Present(obj, key) is not { } node)
        {
            return required ? Fail<string>($"{path}.{key}", "Required key is missing.") : null;
        }

        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return Fail<string>($"{path}.{key}", "String expected.");
        }

        var text = value.GetValue<string>();
        return string.IsNullOrWhiteSpace(text) ? Fail<string>($"{path}.{key}", "Must not be empty.") : text;
    }

    private bool Bool(JsonObject obj, string key, string path)
    {
        if (Present(obj, key) is not { } node)
        {
            return false;
        }

        if (node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetValue<bool>();
        }

        Add($"{path}.{key}", "true or false expected.");
        return false;
    }

    private int? Int(JsonObject obj, string key, string path)
    {
        if (Present(obj, key) is not { } node)
        {
            return null;
        }

        return node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number)
            ? number
            : Fail<int?>($"{path}.{key}", "Whole number expected.");
    }

    private ProcessScalar? Scalar(JsonObject obj, string key, string path, bool required)
    {
        if (!obj.TryGetPropertyValue(key, out var node))
        {
            return required ? Fail<ProcessScalar>($"{path}.{key}", "Required key is missing.") : null;
        }

        return ToScalar(node, $"{path}.{key}");
    }

    private ProcessScalar? ToScalar(JsonNode? node, string path)
    {
        if (node is JsonValue value)
        {
            switch (value.GetValueKind())
            {
                case JsonValueKind.String:
                    return ProcessScalar.Of(value.GetValue<string>());
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return ProcessScalar.Of(value.GetValue<bool>());
                case JsonValueKind.Number when value.TryGetValue<decimal>(out var number):
                    return ProcessScalar.Of(number);
            }
        }

        return Fail<ProcessScalar>(path, "Text, number or true/false expected.");
    }

    private JsonArray? Array(JsonObject obj, string key, string path, bool required)
    {
        if (Present(obj, key) is not { } node)
        {
            return required ? Fail<JsonArray>($"{path}.{key}", "Required key is missing.") : null;
        }

        return node as JsonArray ?? Fail<JsonArray>($"{path}.{key}", "Array expected.");
    }

    private List<string> Strings(JsonObject obj, string key, string path)
    {
        var result = new List<string>();

        if (Array(obj, key, path, required: false) is not { } items)
        {
            return result;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is JsonValue value && value.GetValueKind() == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetValue<string>()))
            {
                result.Add(value.GetValue<string>());
            }
            else
            {
                Add($"{path}.{key}[{i}]", "Non-empty string expected.");
            }
        }

        return result;
    }
}
