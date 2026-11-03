using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>Ta'rifdagi bitta xato: qayerda (<c>sections[2].fields[0].type</c>) va nima.</summary>
public sealed record BlankDefinitionError(string Path, string Message);

/// <summary>
/// Ta'rif JSON'ini modelga o'qiydi va SHAKL xatolarini yig'adi: noma'lum kalit, noma'lum tur, noto'g'ri
/// JSON turi, majburiy kalit yo'q. Ma'no tekshiruvi (havolalar, takrorlar, chegaralar) —
/// <see cref="BlankDefinitionValidator"/> da.
/// </summary>
/// <remarks>
/// Birinchi xatoda to'xtamaydi: shablon muallifi hamma xatoni bir ro'yxatda ko'rishi kerak (§4.8).
/// Xato bo'lgan tugun tashlab ketiladi, qolgani o'qilaveradi.
/// </remarks>
internal sealed class BlankDefinitionReader
{
    private static readonly string[] TopKeys =
        ["schema", "code", "version", "title", "subject", "legalStatus", "organization", "retention", "dueDate", "sections", "steps"];

    private static readonly string[] SectionKeys = ["id", "title", "hint", "when", "fill", "fields"];

    private static readonly string[] CommonFieldKeys =
        ["id", "type", "label", "hint", "required", "when", "readOnly", "prefill", "pdfWidth"];

    private static readonly string[] ConditionOperators = ["eq", "in", "filled"];

    private static readonly string[] StepKeys =
        ["id", "title", "order", "actor", "mode", "when", "decision", "canReject", "canReturn", "due"];

    /// <summary>JSON'dagi tur nomi → tur.</summary>
    internal static IReadOnlyDictionary<string, BlankFieldType> TypeNames { get; } = new Dictionary<string, BlankFieldType>(StringComparer.Ordinal)
    {
        ["text"] = BlankFieldType.Text,
        ["longtext"] = BlankFieldType.LongText,
        ["int"] = BlankFieldType.WholeNumber,
        ["percent"] = BlankFieldType.Percent,
        ["money"] = BlankFieldType.Money,
        ["date"] = BlankFieldType.Date,
        ["datetime"] = BlankFieldType.DateTime,
        ["month"] = BlankFieldType.Month,
        ["year"] = BlankFieldType.Year,
        ["dateRange"] = BlankFieldType.DateRange,
        ["yesno"] = BlankFieldType.YesNo,
        ["choice"] = BlankFieldType.Choice,
        ["multi"] = BlankFieldType.Multi,
        ["check"] = BlankFieldType.Check,
        ["score"] = BlankFieldType.Score,
        ["ref"] = BlankFieldType.Ref,
        ["docRef"] = BlankFieldType.DocRef,
        ["order"] = BlankFieldType.Order,
        ["static"] = BlankFieldType.Static,
        ["file"] = BlankFieldType.File,
        ["table"] = BlankFieldType.Table,
        ["computed"] = BlankFieldType.Computed,
    };

    /// <summary>Turga xos qo'shimcha kalitlar (§4.3, §8).</summary>
    private static readonly Dictionary<BlankFieldType, string[]> TypeKeys = new()
    {
        [BlankFieldType.Text] = ["maxLength"],
        [BlankFieldType.LongText] = ["maxLength"],
        [BlankFieldType.WholeNumber] = ["min", "max"],
        [BlankFieldType.Percent] = ["passAt"],
        [BlankFieldType.Money] = ["min", "max"],
        [BlankFieldType.Date] = ["min", "max"],
        [BlankFieldType.DateTime] = ["min", "max"],
        [BlankFieldType.Month] = ["min", "max"],
        [BlankFieldType.Year] = ["min", "max"],
        [BlankFieldType.Choice] = ["options"],
        [BlankFieldType.Multi] = ["options", "maxSelected"],
        [BlankFieldType.Ref] = ["refKind"],
        [BlankFieldType.DocRef] = ["templateCodes"],
        [BlankFieldType.Static] = ["text"],
        [BlankFieldType.File] = ["accept", "maxFiles", "maxSizeMb", "capture"],
        [BlankFieldType.Table] = ["columns", "minRows", "maxRows", "fixedRows"],
        [BlankFieldType.Computed] = ["valueType", "expr"],
    };

    public List<BlankDefinitionError> Errors { get; } = [];

    public BlankDefinition? ReadDefinition(JsonNode? root)
    {
        if (AsObject(root, "$") is not { } top)
        {
            return null;
        }

        OnlyKeys(top, "$", TopKeys);

        var schema = Int(top, "schema", "$");
        var code = String(top, "code", "$", required: true) ?? string.Empty;
        var version = String(top, "version", "$", required: true) ?? string.Empty;
        var title = String(top, "title", "$", required: true) ?? string.Empty;
        var subject = String(top, "subject", "$", required: true) ?? string.Empty;
        var legalStatus = String(top, "legalStatus", "$", required: false) ?? "final";
        string? dueDateField = null;

        if (schema is null)
        {
            Add("$.schema", "Majburiy kalit yo'q.");
        }

        if (top["dueDate"] is { } dueNode && AsObject(dueNode, "$.dueDate") is { } due)
        {
            OnlyKeys(due, "$.dueDate", "field");
            dueDateField = String(due, "field", "$.dueDate", required: true);
        }

        var sections = new List<BlankSection>();

        if (Array(top, "sections", "$", required: true) is { } sectionNodes)
        {
            for (var i = 0; i < sectionNodes.Count; i++)
            {
                if (ReadSection(sectionNodes[i], $"sections[{i}]") is { } section)
                {
                    sections.Add(section);
                }
            }
        }

        var steps = new List<BlankStep>();

        if (Array(top, "steps", "$", required: false) is { } stepNodes)
        {
            for (var i = 0; i < stepNodes.Count; i++)
            {
                if (ReadStep(stepNodes[i], $"steps[{i}]") is { } step)
                {
                    steps.Add(step);
                }
            }
        }

        return new BlankDefinition(code, version, title, subject, sections, steps)
        {
            Schema = schema ?? 0,
            LegalStatus = legalStatus,
            Organization = String(top, "organization", "$", required: false),
            Retention = String(top, "retention", "$", required: false),
            DueDateField = dueDateField,
        };
    }

    public BlankCondition? ReadCondition(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        if (obj.ContainsKey("all") || obj.ContainsKey("any"))
        {
            var key = obj.ContainsKey("all") ? "all" : "any";
            OnlyKeys(obj, path, key);

            var items = new List<BlankCondition>();

            if (Array(obj, key, path, required: true) is { } list)
            {
                if (list.Count == 0)
                {
                    Add($"{path}.{key}", "Bo'sh ro'yxat.");
                }

                for (var i = 0; i < list.Count; i++)
                {
                    if (ReadCondition(list[i], $"{path}.{key}[{i}]") is { } item)
                    {
                        items.Add(item);
                    }
                }
            }

            return key == "all" ? new BlankAllCondition(items) : new BlankAnyCondition(items);
        }

        if (obj.ContainsKey("not"))
        {
            OnlyKeys(obj, path, "not");
            return ReadCondition(obj["not"], $"{path}.not") is { } inner ? new BlankNotCondition(inner) : null;
        }

        var field = String(obj, "field", path, required: true);
        var operators = ConditionOperators.Where(obj.ContainsKey).ToList();
        OnlyKeys(obj, path, "field", "eq", "in", "filled");

        if (operators.Count != 1)
        {
            Add(path, "Shartda aynan bitta amal bo'lishi kerak: eq, in yoki filled.");
            return null;
        }

        if (field is null)
        {
            return null;
        }

        switch (operators[0])
        {
            case "eq":
                return new BlankEqCondition(field, obj["eq"]?.DeepClone());
            case "in":
                return Array(obj, "in", path, required: true) is { } values
                    ? new BlankInCondition(field, [.. values.Select(value => value?.DeepClone())])
                    : null;
            default:
                return obj["filled"] is JsonValue flag && flag.GetValueKind() is JsonValueKind.True or JsonValueKind.False
                    ? new BlankFilledCondition(field, flag.GetValue<bool>())
                    : Fail<BlankCondition>($"{path}.filled", "true yoki false kutilgan.");
        }
    }

    public BlankExpr? ReadExpr(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        if (obj.ContainsKey("ref"))
        {
            OnlyKeys(obj, path, "ref");
            return String(obj, "ref", path, required: true) is { } reference ? new BlankRefExpr(reference) : null;
        }

        if (obj.ContainsKey("const"))
        {
            OnlyKeys(obj, path, "const");
            return obj["const"] is null or JsonValue
                ? new BlankConstExpr(obj["const"]?.DeepClone())
                : Fail<BlankExpr>($"{path}.const", "Faqat son, matn, mantiq yoki null.");
        }

        var op = String(obj, "op", path, required: true);

        if (op is null)
        {
            return null;
        }

        if (op == "band")
        {
            return ReadBand(obj, path);
        }

        OnlyKeys(obj, path, "op", "args");

        if (!BlankComputedEvaluator.Arity.TryGetValue(op, out var arity))
        {
            return Fail<BlankExpr>($"{path}.op", $"Noma'lum amal: {op}.");
        }

        var args = new List<BlankExpr>();

        if (Array(obj, "args", path, required: true) is { } argNodes)
        {
            var countOk = arity switch
            {
                -1 => argNodes.Count >= 1,
                -2 => argNodes.Count >= 2,
                _ => argNodes.Count == arity,
            };

            if (!countOk)
            {
                Add($"{path}.args", $"«{op}» uchun argumentlar soni noto'g'ri.");
            }

            for (var i = 0; i < argNodes.Count; i++)
            {
                if (ReadExpr(argNodes[i], $"{path}.args[{i}]") is { } arg)
                {
                    args.Add(arg);
                }
            }
        }

        return new BlankOpExpr(op, args);
    }

    private BlankBandExpr? ReadBand(JsonObject obj, string path)
    {
        OnlyKeys(obj, path, "op", "value", "bands");

        var value = ReadExpr(obj["value"], $"{path}.value");
        var bands = new List<BlankBand>();

        if (Array(obj, "bands", path, required: true) is { } bandNodes)
        {
            for (var i = 0; i < bandNodes.Count; i++)
            {
                var bandPath = $"{path}.bands[{i}]";

                if (AsObject(bandNodes[i], bandPath) is not { } band)
                {
                    continue;
                }

                OnlyKeys(band, bandPath, "from", "label");

                if (!band.ContainsKey("from"))
                {
                    Add($"{bandPath}.from", "Majburiy kalit yo'q (oxirgi band uchun null).");
                }

                var from = band["from"] is null ? null : Number(band, "from", bandPath);
                bands.Add(new BlankBand(from, String(band, "label", bandPath, required: true) ?? string.Empty));
            }
        }

        return value is null ? null : new BlankBandExpr(value, bands);
    }

    private BlankSection? ReadSection(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, SectionKeys);

        var fill = BlankFill.Draft;

        if (obj["fill"] is { } fillNode && AsObject(fillNode, $"{path}.fill") is { } fillObj)
        {
            OnlyKeys(fillObj, $"{path}.fill", "at", "step");

            fill = String(fillObj, "at", $"{path}.fill", required: true) switch
            {
                "draft" => BlankFill.Draft,
                "open" => new BlankFill(BlankFillAt.Open, null),
                "step" => new BlankFill(BlankFillAt.Step, String(fillObj, "step", $"{path}.fill", required: true)),
                null => BlankFill.Draft,
                var other => Fail<BlankFill>($"{path}.fill.at", $"Noma'lum qiymat: {other}.") ?? BlankFill.Draft,
            };

            if (fill.At != BlankFillAt.Step && fillObj.ContainsKey("step"))
            {
                Add($"{path}.fill.step", "«step» faqat at = step bilan.");
            }
        }

        var fields = new List<BlankField>();

        if (Array(obj, "fields", path, required: true) is { } fieldNodes)
        {
            for (var i = 0; i < fieldNodes.Count; i++)
            {
                if (ReadField(fieldNodes[i], $"{path}.fields[{i}]") is { } field)
                {
                    fields.Add(field);
                }
            }
        }

        return new BlankSection(
            String(obj, "id", path, required: true) ?? string.Empty,
            String(obj, "title", path, required: true) ?? string.Empty,
            String(obj, "hint", path, required: false),
            obj["when"] is { } when ? ReadCondition(when, $"{path}.when") : null,
            fill,
            fields);
    }

    private BlankField? ReadField(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        var typeName = String(obj, "type", path, required: true);

        if (typeName is null)
        {
            return null;
        }

        if (!TypeNames.TryGetValue(typeName, out var type))
        {
            return Fail<BlankField>($"{path}.type", $"Noma'lum maydon turi: {typeName}.");
        }

        OnlyKeys(obj, path, [.. CommonFieldKeys, .. TypeKeys.GetValueOrDefault(type, [])]);

        var columns = new List<BlankField>();

        if (type == BlankFieldType.Table && Array(obj, "columns", path, required: true) is { } columnNodes)
        {
            for (var i = 0; i < columnNodes.Count; i++)
            {
                if (ReadField(columnNodes[i], $"{path}.columns[{i}]") is { } column)
                {
                    columns.Add(column);
                }
            }
        }

        return new BlankField
        {
            Id = String(obj, "id", path, required: true) ?? string.Empty,
            Type = type,
            Label = String(obj, "label", path, required: false),
            Hint = String(obj, "hint", path, required: false),
            Required = Bool(obj, "required", path),
            When = obj["when"] is { } when ? ReadCondition(when, $"{path}.when") : null,
            ReadOnly = Bool(obj, "readOnly", path),
            Prefill = String(obj, "prefill", path, required: false),
            PdfWidth = Number(obj, "pdfWidth", path),
            MaxLength = Int(obj, "maxLength", path),
            Min = Bound(obj, "min", path),
            Max = Bound(obj, "max", path),
            PassAt = Number(obj, "passAt", path),
            Options = obj.ContainsKey("options") ? ReadOptions(obj, path) : [],
            MaxSelected = Int(obj, "maxSelected", path),
            RefKind = String(obj, "refKind", path, required: false),
            TemplateCodes = Strings(obj, "templateCodes", path),
            Text = String(obj, "text", path, required: false),
            Columns = columns,
            MinRows = Int(obj, "minRows", path),
            MaxRows = Int(obj, "maxRows", path),
            FixedRows = obj.ContainsKey("fixedRows") ? ReadFixedRows(obj, path) : [],
            ValueType = String(obj, "valueType", path, required: false),
            Expr = obj.ContainsKey("expr") ? ReadExpr(obj["expr"], $"{path}.expr") : null,
            Accept = Strings(obj, "accept", path),
            MaxFiles = Int(obj, "maxFiles", path),
            MaxSizeMb = Int(obj, "maxSizeMb", path),
            Capture = String(obj, "capture", path, required: false),
        };
    }

    public List<BlankOption> ReadOptions(JsonObject owner, string path)
    {
        var options = new List<BlankOption>();

        if (Array(owner, "options", path, required: true) is not { } nodes)
        {
            return options;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var optionPath = $"{path}.options[{i}]";

            if (AsObject(nodes[i], optionPath) is not { } obj)
            {
                continue;
            }

            OnlyKeys(obj, optionPath, "value", "label", "extra");

            BlankOptionExtra? extra = null;

            if (obj["extra"] is { } extraNode && AsObject(extraNode, $"{optionPath}.extra") is { } extraObj)
            {
                var extraPath = $"{optionPath}.extra";
                OnlyKeys(extraObj, extraPath, "type", "label", "required", "refKind", "templateCodes");

                var typeName = String(extraObj, "type", extraPath, required: true);

                if (typeName is not null && TypeNames.TryGetValue(typeName, out var type))
                {
                    extra = new BlankOptionExtra(
                        type,
                        String(extraObj, "label", extraPath, required: true) ?? string.Empty,
                        Bool(extraObj, "required", extraPath),
                        String(extraObj, "refKind", extraPath, required: false),
                        Strings(extraObj, "templateCodes", extraPath));
                }
                else if (typeName is not null)
                {
                    Add($"{extraPath}.type", $"Noma'lum tur: {typeName}.");
                }
            }

            options.Add(new BlankOption(
                String(obj, "value", optionPath, required: true) ?? string.Empty,
                String(obj, "label", optionPath, required: true) ?? string.Empty,
                extra));
        }

        return options;
    }

    private List<BlankFixedRow> ReadFixedRows(JsonObject owner, string path)
    {
        var rows = new List<BlankFixedRow>();

        if (Array(owner, "fixedRows", path, required: true) is not { } nodes)
        {
            return rows;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var rowPath = $"{path}.fixedRows[{i}]";

            if (AsObject(nodes[i], rowPath) is not { } obj)
            {
                continue;
            }

            OnlyKeys(obj, rowPath, "id", "label", "cells");

            var cells = new Dictionary<string, BlankFixedCell>(StringComparer.Ordinal);

            if (obj["cells"] is { } cellsNode && AsObject(cellsNode, $"{rowPath}.cells") is { } cellsObj)
            {
                foreach (var (columnId, cellNode) in cellsObj)
                {
                    var cellPath = $"{rowPath}.cells.{columnId}";

                    if (AsObject(cellNode, cellPath) is { } cell)
                    {
                        OnlyKeys(cell, cellPath, "readOnly", "text");
                        cells[columnId] = new BlankFixedCell(Bool(cell, "readOnly", cellPath), String(cell, "text", cellPath, required: false));
                    }
                }
            }

            rows.Add(new BlankFixedRow(
                String(obj, "id", rowPath, required: true) ?? string.Empty,
                String(obj, "label", rowPath, required: true) ?? string.Empty,
                cells));
        }

        return rows;
    }

    private BlankStep? ReadStep(JsonNode? node, string path)
    {
        if (AsObject(node, path) is not { } obj)
        {
            return null;
        }

        OnlyKeys(obj, path, StepKeys);

        var mode = String(obj, "mode", path, required: true) switch
        {
            "electronic" => BlankStepMode.Electronic,
            "inPerson" => BlankStepMode.InPerson,
            "either" => BlankStepMode.Either,
            null => BlankStepMode.Electronic,
            var other => Fail<BlankStepMode?>($"{path}.mode", $"Noma'lum usul: {other}.") ?? BlankStepMode.Electronic,
        };

        BlankDecision? decision = null;

        if (obj["decision"] is { } decisionNode && AsObject(decisionNode, $"{path}.decision") is { } decisionObj)
        {
            OnlyKeys(decisionObj, $"{path}.decision", "options", "required");
            decision = new BlankDecision(ReadOptions(decisionObj, $"{path}.decision"), Bool(decisionObj, "required", $"{path}.decision"));
        }

        BlankDue? due = null;

        if (obj["due"] is { } dueNode && AsObject(dueNode, $"{path}.due") is { } dueObj)
        {
            OnlyKeys(dueObj, $"{path}.due", "field", "days");
            var days = Int(dueObj, "days", $"{path}.due");

            if (days is null)
            {
                Add($"{path}.due.days", "Majburiy kalit yo'q.");
            }

            due = new BlankDue(String(dueObj, "field", $"{path}.due", required: true) ?? string.Empty, days ?? 0);
        }

        var order = Int(obj, "order", path);

        if (order is null)
        {
            Add($"{path}.order", "Majburiy kalit yo'q.");
        }

        return new BlankStep(
            String(obj, "id", path, required: true) ?? string.Empty,
            String(obj, "title", path, required: true) ?? string.Empty,
            order ?? 0,
            String(obj, "actor", path, required: true) ?? string.Empty,
            mode,
            obj["when"] is { } when ? ReadCondition(when, $"{path}.when") : null,
            decision,
            Bool(obj, "canReject", path),
            obj.ContainsKey("canReturn") ? Bool(obj, "canReturn", path) : true,
            due);
    }

    // ── Kichik o'quvchilar: har biri xatoni yo'li bilan yozadi va null qaytaradi ──

    private void Add(string path, string message) => Errors.Add(new BlankDefinitionError(path, message));

    private T? Fail<T>(string path, string message)
    {
        Add(path, message);
        return default;
    }

    private JsonObject? AsObject(JsonNode? node, string path)
        => node as JsonObject ?? Fail<JsonObject>(path, "Obyekt kutilgan.");

    private void OnlyKeys(JsonObject obj, string path, params string[] allowed)
    {
        foreach (var (key, _) in obj)
        {
            if (!allowed.Contains(key, StringComparer.Ordinal))
            {
                Add($"{path}.{key}", "Noma'lum kalit.");
            }
        }
    }

    private string? String(JsonObject obj, string key, string path, bool required)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return required ? Fail<string>($"{path}.{key}", "Majburiy kalit yo'q.") : null;
        }

        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return Fail<string>($"{path}.{key}", "Matn kutilgan.");
        }

        var text = value.GetValue<string>();
        return required && string.IsNullOrWhiteSpace(text) ? Fail<string>($"{path}.{key}", "Bo'sh bo'lmasligi kerak.") : text;
    }

    private bool Bool(JsonObject obj, string key, string path)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return false;
        }

        if (node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetValue<bool>();
        }

        Add($"{path}.{key}", "true yoki false kutilgan.");
        return false;
    }

    private decimal? Number(JsonObject obj, string key, string path)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return null;
        }

        return node is JsonValue value && BlankValues.TryNumber(value, out var number)
            ? number
            : Fail<decimal?>($"{path}.{key}", "Son kutilgan.");
    }

    private int? Int(JsonObject obj, string key, string path)
    {
        var number = Number(obj, key, path);

        if (number is null)
        {
            return null;
        }

        return number == decimal.Truncate(number.Value) && number is >= int.MinValue and <= int.MaxValue
            ? (int)number.Value
            : Fail<int?>($"{path}.{key}", "Butun son kutilgan.");
    }

    private BlankBound? Bound(JsonObject obj, string key, string path)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return null;
        }

        return node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? new BlankBound(null, value.GetValue<string>())
            : Number(obj, key, path) is { } number ? new BlankBound(number, null) : null;
    }

    private JsonArray? Array(JsonObject obj, string key, string path, bool required)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return required ? Fail<JsonArray>($"{path}.{key}", "Majburiy kalit yo'q.") : null;
        }

        return node as JsonArray ?? Fail<JsonArray>($"{path}.{key}", "Massiv kutilgan.");
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
            if (items[i] is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                result.Add(value.GetValue<string>());
            }
            else
            {
                Add($"{path}.{key}[{i}]", "Matn kutilgan.");
            }
        }

        return result;
    }
}
