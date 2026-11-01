using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Forms.Texts;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>Ta'rifdagi bitta muammo: barqaror kod va joy (<c>sections[2].screens[0].fields[3]</c>).</summary>
public sealed record FormIssue(string Code, string Path);

/// <summary>
/// Ta'rifning BonviHR dagi tekshiruvi (SPEC-anketa-konstruktori §5.2) — HRMentor
/// <c>issues.py</c> (§19.5) ning aynan nusxasi, BonviHR profiliga qarshi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Ichki himoya.</b> HRMentor ham shu qoidalar bilan tekshirmasdan e'lon qilmaydi; bu yerda
/// qayta tekshirish — BonviHR faqat o'zi TUSHUNADIGAN narsani chizadi va tekshiradi: noma'lum tur
/// yoki kalit, majburiy bog'lanishning yo'qligi, buzilgan shart — ta'rif saqlanmaydi.
/// </para>
/// <para>
/// Hech qachon istisno tashlamaydi: har qanday JSON ni o'qiydi va topgan hamma muammoni qaytaradi.
/// Tekshiruvdan o'tgan ta'rifni <see cref="FormDefinitionReader"/> turlarga aylantiradi.
/// </para>
/// </remarks>
public sealed partial class FormDefinitionChecker
{
    public const int MaxDefinitionBytes = 524_288;
    public const int MaxSections = 30;
    public const int MaxScreens = 80;
    public const int MaxFields = 400;
    public const int MaxFieldOptions = 200;
    public const int MaxColumns = 12;
    public const int MaxAspects = 10;
    public const int MaxChecklistItems = 50;
    public const int MaxRepeatRows = 20;
    public const int MaxScale = 10;
    public const int MaxText = 300;
    public const int MaxHint = 600;
    public const int MaxLongText = 4_000;
    public const int MinYear = 1900;
    public const int MaxYear = 2100;
    public const int MaxAge = 150;
    public const long MaxNumber = 1_000_000_000;
    public const int MaxLetters = 10;
    public const int MaxOtherRows = 5;
    public const int MaxShortText = 300;
    public const int MaxLongAnswer = 2_000;
    public const int MaxPrintedCode = 20;

    private static readonly string[] PdfRequired = ["printed_code", "title"];
    private static readonly string[] PdfOptional = ["subtitle", "instruction", "candidate_part", "office_part", "footer"];

    private static readonly HashSet<string> DefinitionKeys = Set("schema", "code", "label", "title", "pdf", "sections");
    private static readonly HashSet<string> PdfKeys = Set([.. PdfRequired, .. PdfOptional]);
    private static readonly HashSet<string> SectionKeys = Set("id", "audience", "title", "pdf_title", "hint", "screens");
    private static readonly HashSet<string> ScreenKeys = Set("id", "title", "hint", "fields");
    private static readonly HashSet<string> FieldKeys =
        Set("id", "kind", "label", "hint", "required", "bind", "visible_if", "pdf_label", "pdf_hidden");

    private static readonly Dictionary<FieldKind, HashSet<string>> KindKeys = new()
    {
        [FieldKind.ShortText] = Set("max_length", "min_letters", "same_as"),
        [FieldKind.LongText] = Set("max_length", "min_letters"),
        [FieldKind.Number] = Set("min", "max"),
        [FieldKind.Money] = Set("min", "max"),
        [FieldKind.Date] = Set("min_age", "max_age", "not_before_today"),
        [FieldKind.Year] = Set("min_year", "max_year"),
        [FieldKind.YearRange] = Set("min_year", "max_year"),
        [FieldKind.Phone] = Set("mobile_only"),
        [FieldKind.YesNo] = Set("yes", "no"),
        [FieldKind.Choice] = Set("options", "groups", "style"),
        [FieldKind.MultiChoice] = Set("options", "groups", "min_selected", "max_selected"),
        [FieldKind.Levels] = Set("options", "groups", "aspects", "scale", "required_rows", "other", "print_rows"),
        [FieldKind.Repeat] = Set("columns", "min_rows", "max_rows", "summary", "print_rows"),
        [FieldKind.Period] = Set("allow_current"),
        [FieldKind.Consent] = Set("text"),
        [FieldKind.Info] = Set("text"),
        [FieldKind.Checklist] = Set("items"),
    };

    private static readonly HashSet<string> OptionKeys = Set("value", "label", "group", "pdf_label");
    private static readonly HashSet<string> LabelledKeys = Set("id", "label");
    private static readonly HashSet<string> ConditionKeys = Set("field", "is", "in");
    private static readonly HashSet<string> SameAsKeys = Set("field", "label");
    private static readonly HashSet<string> OtherKeys = Set("max", "label");
    private static readonly HashSet<string> ScaleKeys = Set("min", "max");
    private static readonly HashSet<string> ChecklistItemKeys = Set("label", "auto", "note");
    private static readonly HashSet<string> ChecklistAuto = Set("photo", "signature");
    private static readonly HashSet<string> ChoiceStyles = Set("tiles", "list");
    private static readonly HashSet<string> ConsentPlaceholders = Set("fullName");

    private readonly IReadOnlyDictionary<string, FormBinding> _bindings;
    private readonly List<FormIssue> _issues = [];
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Seen> _seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Usage>> _uses = new(StringComparer.Ordinal);
    private readonly List<string> _useOrder = [];
    private readonly Dictionary<FieldKind, int> _system = [];
    private (int Index, string Path)? _signatureAt;
    private int _screens;
    private int _fields;
    private int _index;

    private FormDefinitionChecker(IReadOnlyDictionary<string, FormBinding> bindings) => _bindings = bindings;

    /// <summary>Ta'rifning hamma muammosi — forma tartibida.</summary>
    public static IReadOnlyList<FormIssue> Check(JsonObject definition, IReadOnlyDictionary<string, FormBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(bindings);

        var checker = new FormDefinitionChecker(bindings);
        checker.Run(definition);
        return checker._issues;
    }

    [GeneratedRegex(@"^[a-z][A-Za-z0-9_-]{1,39}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex IdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,39}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex OptionValuePattern();

    [GeneratedRegex(@"^[0-9A-Za-z.]{1,8}\z", RegexOptions.CultureInvariant)]
    internal static partial Regex LabelPattern();

    private void Add(string code, string path) => _issues.Add(new FormIssue(code, path));

    // ── butun ta'rif ──────────────────────────────────────────────────

    private void Run(JsonObject definition)
    {
        Keys(definition, DefinitionKeys, string.Empty);
        Text(definition["title"], "title", MaxText);
        Pdf(definition["pdf"]);

        var sections = definition["sections"] as JsonArray;
        if (sections is null || sections.Count == 0)
        {
            Add("forms.section_empty", "sections");
            sections = [];
        }

        if (sections.Count > MaxSections)
        {
            Add("forms.limit", "sections");
        }

        var officeSeen = false;
        for (var s = 0; s < sections.Count; s++)
        {
            var path = $"sections[{s}]";
            if (sections[s] is not JsonObject section)
            {
                Add("forms.shape_invalid", path);
                continue;
            }

            bool? office = Str(section["audience"]) switch
            {
                "candidate" => false,
                "office" => true,
                _ => null,
            };

            if (office is null)
            {
                Add("forms.audience_invalid", path);
            }
            else if (office == true)
            {
                officeSeen = true;
            }
            else if (officeSeen)
            {
                Add("forms.order_invalid", path);
            }

            Section(section, path, office);
        }

        if (_screens > MaxScreens)
        {
            Add("forms.limit", "sections");
        }

        if (_fields > MaxFields)
        {
            Add("forms.limit", "sections");
        }

        CheckBindings();
        CheckSignature();
    }

    private void Pdf(JsonNode? raw)
    {
        var pdf = raw as JsonObject ?? [];
        Keys(pdf, PdfKeys, "pdf");
        if (raw is not null && raw is not JsonObject)
        {
            Add("forms.shape_invalid", "pdf");
        }

        foreach (var key in PdfRequired)
        {
            Line(pdf[key], $"pdf.{key}", key == "printed_code" ? MaxPrintedCode : MaxText, optional: false);
        }

        foreach (var key in PdfOptional)
        {
            Line(pdf[key], $"pdf.{key}", MaxHint);
        }
    }

    private void Section(JsonObject section, string path, bool? office)
    {
        Keys(section, SectionKeys, path);
        Id(section["id"], path, _ids);
        Text(section["title"], $"{path}.title", MaxText);
        Line(section["pdf_title"], $"{path}.pdf_title", MaxText);
        Text(section["hint"], $"{path}.hint", MaxHint, multiline: true, optional: true);

        if (section["screens"] is not JsonArray { Count: > 0 } screens)
        {
            Add("forms.section_empty", path);
            return;
        }

        for (var c = 0; c < screens.Count; c++)
        {
            var screenPath = $"{path}.screens[{c}]";
            _screens++;
            if (screens[c] is not JsonObject screen)
            {
                Add("forms.shape_invalid", screenPath);
                continue;
            }

            Keys(screen, ScreenKeys, screenPath);
            Id(screen["id"], screenPath, _ids);
            Text(screen["title"], $"{screenPath}.title", MaxText, optional: true);
            Text(screen["hint"], $"{screenPath}.hint", MaxHint, multiline: true, optional: true);

            if (screen["fields"] is not JsonArray { Count: > 0 } fields)
            {
                Add("forms.screen_empty", screenPath);
                continue;
            }

            for (var f = 0; f < fields.Count; f++)
            {
                Field(fields[f], $"{screenPath}.fields[{f}]", office, columnIds: null);
            }
        }
    }

    // ── bitta maydon ──────────────────────────────────────────────────

    private void Field(JsonNode? raw, string path, bool? office, HashSet<string>? columnIds)
    {
        _fields++;
        if (raw is not JsonObject field)
        {
            Add("forms.shape_invalid", path);
            return;
        }

        Id(field["id"], path, columnIds ?? _ids);
        if (!IsString(field["kind"]) || !FieldKinds.TryParse(Str(field["kind"]), out var kind))
        {
            Add("forms.kind_unknown", path);
            return;
        }

        var known = new HashSet<string>(FieldKeys, StringComparer.Ordinal);
        if (KindKeys.TryGetValue(kind, out var own))
        {
            known.UnionWith(own);
        }

        Keys(field, known, path);
        Common(field, path, kind, office, isColumn: columnIds is not null);
        var values = KindCheck(field, path, kind, office);
        if (columnIds is null)
        {
            Record(field, path, kind, office, values);
        }
    }

    private void Common(JsonObject field, string path, FieldKind kind, bool? office, bool isColumn)
    {
        var allowed = isColumn ? FieldKinds.Column : office == true ? FieldKinds.Office : FieldKinds.Candidate;
        if (!allowed.Contains(kind))
        {
            Add("forms.kind_not_allowed", path);
        }

        Text(field["label"], $"{path}.label", MaxText, optional: kind == FieldKind.Info);
        Text(field["hint"], $"{path}.hint", MaxHint, multiline: true, optional: true);
        Line(field["pdf_label"], $"{path}.pdf_label", MaxText);
        Bool(field, "required", path);
        Bool(field, "pdf_hidden", path);

        if (isColumn)
        {
            foreach (var key in new[] { "bind", "visible_if" })
            {
                if (field.ContainsKey(key))
                {
                    Add("forms.shape_invalid", $"{path}.{key}");
                }
            }
        }
        else if (field.ContainsKey("visible_if"))
        {
            Condition(field["visible_if"], $"{path}.visible_if", office);
        }
    }

    private void Record(JsonObject field, string path, FieldKind kind, bool? office, IReadOnlyList<string> values)
    {
        _index++;
        if (FieldKinds.System.Contains(kind))
        {
            _system[kind] = _system.GetValueOrDefault(kind) + 1;
            if (_system[kind] > 1)
            {
                Add("forms.kind_repeated", path);
            }
        }

        if (kind == FieldKind.Signature && _signatureAt is null)
        {
            _signatureAt = (_index, path);
        }

        if (field.ContainsKey("bind"))
        {
            Use(field, path, kind, office, values);
        }

        if (Str(field["id"]) is { } id && IsString(field["id"]) && !_seen.ContainsKey(id))
        {
            _seen[id] = new Seen(kind, office, values);
        }
    }

    // ── turlarning o'z kalitlari; qaytgan variant qiymatlari shart va bog'lanishga ──

    private List<string> KindCheck(JsonObject field, string path, FieldKind kind, bool? office)
    {
        switch (kind)
        {
            case FieldKind.ShortText:
                Int(field, "max_length", path, 1, MaxShortText);
                Int(field, "min_letters", path, 0, MaxLetters);
                if (field.ContainsKey("same_as"))
                {
                    SameAs(field["same_as"], $"{path}.same_as");
                }

                break;
            case FieldKind.LongText:
                Int(field, "max_length", path, 1, MaxLongAnswer);
                Int(field, "min_letters", path, 0, MaxLetters);
                break;
            case FieldKind.Number:
                Range(field, path, "min", "max", -MaxNumber, MaxNumber);
                break;
            case FieldKind.Money:
                Range(field, path, "min", "max", 0, MaxNumber);
                break;
            case FieldKind.Date:
                Range(field, path, "min_age", "max_age", 0, MaxAge);
                Bool(field, "not_before_today", path);
                break;
            case FieldKind.Year or FieldKind.YearRange:
                Years(field, path);
                break;
            case FieldKind.YesNo:
                Text(field["yes"], $"{path}.yes", MaxText, optional: true);
                Text(field["no"], $"{path}.no", MaxText, optional: true);
                break;
            case FieldKind.Choice:
                if (field["style"] is { } style && !(IsString(style) && ChoiceStyles.Contains(Str(style)!)))
                {
                    Add("forms.range_invalid", $"{path}.style");
                }

                return Options(field, path);
            case FieldKind.MultiChoice:
                var values = Options(field, path);
                var low = Int(field, "min_selected", path, 0, MaxFieldOptions);
                var high = Int(field, "max_selected", path, 1, MaxFieldOptions);
                if (low is not null && ((high is not null && low > high) || low > values.Count))
                {
                    Add("forms.range_invalid", path);
                }

                return values;
            case FieldKind.Levels:
                return Levels(field, path);
            case FieldKind.Repeat:
                Repeat(field, path, office);
                break;
            case FieldKind.Period:
                Bool(field, "allow_current", path);
                break;
            case FieldKind.Phone:
                Bool(field, "mobile_only", path);
                break;
            case FieldKind.Consent:
                Text(field["text"], $"{path}.text", MaxLongText, multiline: true);
                if (field["text"] is JsonObject consent
                    && consent.Any(pair => IsString(pair.Value)
                        && !FormTexts.PlaceholdersOf(Str(pair.Value)!).IsSubsetOf(ConsentPlaceholders)))
                {
                    Add("forms.text_invalid", $"{path}.text");
                }

                break;
            case FieldKind.Info:
                Text(field["text"], $"{path}.text", MaxLongText, multiline: true);
                break;
            case FieldKind.Checklist:
                Checklist(field, path);
                break;
            default:
                // month, email, telegram va tizim turlarining o'z kaliti yo'q
                break;
        }

        return [];
    }

    private List<string> Options(JsonObject field, string path)
    {
        var groupIds = Groups(field["groups"], $"{path}.groups");
        var values = new List<string>();
        if (field["options"] is not JsonArray { Count: > 0 } options)
        {
            Add("forms.options_empty", path);
            return values;
        }

        if (options.Count > MaxFieldOptions)
        {
            Add("forms.limit", path);
        }

        for (var k = 0; k < options.Count; k++)
        {
            var at = $"{path}.options[{k}]";
            if (options[k] is not JsonObject option)
            {
                Add("forms.shape_invalid", at);
                continue;
            }

            Keys(option, OptionKeys, at);
            var value = Str(option["value"]);
            if (!IsString(option["value"]) || !OptionValuePattern().IsMatch(value!))
            {
                Add("forms.option_invalid", at);
            }
            else if (values.Contains(value!))
            {
                Add("forms.option_duplicate", at);
            }
            else
            {
                values.Add(value!);
            }

            Text(option["label"], $"{at}.label", MaxText);
            Line(option["pdf_label"], $"{at}.pdf_label", MaxText);
            if (option["group"] is { } group && !(IsString(group) && groupIds.Contains(Str(group)!)))
            {
                Add("forms.option_invalid", at);
            }
        }

        return values;
    }

    private HashSet<string> Groups(JsonNode? raw, string path)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (raw is null)
        {
            return ids;
        }

        if (raw is not JsonArray groups)
        {
            Add("forms.shape_invalid", path);
            return ids;
        }

        for (var g = 0; g < groups.Count; g++)
        {
            var at = $"{path}[{g}]";
            if (groups[g] is not JsonObject group)
            {
                Add("forms.shape_invalid", at);
                continue;
            }

            Keys(group, LabelledKeys, at);
            Id(group["id"], at, ids);
            Text(group["label"], $"{at}.label", MaxText);
        }

        return ids;
    }

    private List<string> Levels(JsonObject field, string path)
    {
        var values = Options(field, path);
        Aspects(field["aspects"], $"{path}.aspects");

        var scale = field["scale"];
        if (ScaleOf(scale) is null)
        {
            Add("forms.range_invalid", $"{path}.scale");
        }
        else if (scale is JsonObject scaleObject)
        {
            Keys(scaleObject, ScaleKeys, $"{path}.scale");
        }

        foreach (var key in new[] { "required_rows", "print_rows" })
        {
            var rows = field[key];
            if (rows is null)
            {
                continue;
            }

            if (rows is not JsonArray array || array.Any(row => !IsString(row) || !values.Contains(Str(row)!)))
            {
                Add("forms.option_invalid", $"{path}.{key}");
            }
        }

        var other = field["other"];
        if (other is not null)
        {
            if (other is not JsonObject otherObject)
            {
                Add("forms.shape_invalid", $"{path}.other");
            }
            else
            {
                Keys(otherObject, OtherKeys, $"{path}.other");
                Int(otherObject, "max", $"{path}.other", 1, MaxOtherRows, required: true);
                Text(otherObject["label"], $"{path}.other.label", MaxText);
            }
        }

        return values;
    }

    private void Aspects(JsonNode? raw, string path)
    {
        if (raw is not JsonArray { Count: > 0 } aspects)
        {
            Add("forms.options_empty", path);
            return;
        }

        if (aspects.Count > MaxAspects)
        {
            Add("forms.limit", path);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var a = 0; a < aspects.Count; a++)
        {
            var at = $"{path}[{a}]";
            if (aspects[a] is not JsonObject aspect)
            {
                Add("forms.shape_invalid", at);
                continue;
            }

            Keys(aspect, LabelledKeys, at);
            Id(aspect["id"], at, ids);
            Text(aspect["label"], $"{at}.label", MaxText);
        }
    }

    private void Repeat(JsonObject field, string path, bool? office)
    {
        var columnIds = new HashSet<string>(StringComparer.Ordinal);
        if (field["columns"] is not JsonArray { Count: > 0 } columns)
        {
            Add("forms.columns_empty", path);
        }
        else
        {
            if (columns.Count > MaxColumns)
            {
                Add("forms.limit", path);
            }

            for (var k = 0; k < columns.Count; k++)
            {
                Field(columns[k], $"{path}.columns[{k}]", office, columnIds);
            }
        }

        Range(field, path, "min_rows", "max_rows", 0, MaxRepeatRows);
        if (IsInt(field["max_rows"]) && Long(field["max_rows"]) == 0)
        {
            Add("forms.range_invalid", path);
        }

        Int(field, "print_rows", path, 0, MaxRepeatRows);

        var summary = field["summary"];
        if (summary is not null)
        {
            Line(summary, $"{path}.summary", MaxText);
            var names = IsString(summary) ? SummaryNames(Str(summary)!) : [];
            if (!names.IsSubsetOf(columnIds))
            {
                Add("forms.reference_invalid", $"{path}.summary");
            }
        }
    }

    private void SameAs(JsonNode? raw, string path)
    {
        var target = raw is JsonObject value && Str(value["field"]) is { } id ? _seen.GetValueOrDefault(id) : null;
        if (raw is not JsonObject sameAs || target is null || target.Kind != FieldKind.ShortText || target.Office != false)
        {
            Add("forms.reference_invalid", path);
            return;
        }

        Keys(sameAs, SameAsKeys, path);
        Text(sameAs["label"], $"{path}.label", MaxText);
    }

    private void Years(JsonObject field, string path)
    {
        var low = Int(field, "min_year", path, MinYear, MaxYear);
        var highRaw = field["max_year"];
        if (highRaw is null || (IsString(highRaw) && Str(highRaw) == "today"))
        {
            return;
        }

        var high = Int(field, "max_year", path, MinYear, MaxYear);
        if (low is not null && high is not null && low > high)
        {
            Add("forms.range_invalid", path);
        }
    }

    private void Checklist(JsonObject field, string path)
    {
        var atItems = $"{path}.items";
        if (field["items"] is not JsonArray { Count: > 0 } items)
        {
            Add("forms.options_empty", atItems);
            return;
        }

        if (items.Count > MaxChecklistItems)
        {
            Add("forms.limit", atItems);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var at = $"{atItems}[{i}]";
            if (items[i] is not JsonObject item)
            {
                Add("forms.shape_invalid", at);
                continue;
            }

            Keys(item, ChecklistItemKeys, at);
            Line(item["label"], $"{at}.label", MaxText, optional: false);
            Line(item["note"], $"{at}.note", MaxText);
            if (item["auto"] is { } auto && !(IsString(auto) && ChecklistAuto.Contains(Str(auto)!)))
            {
                Add("forms.range_invalid", $"{at}.auto");
            }
        }
    }

    // ── shartlar va bog'lanishlar ─────────────────────────────────────

    private void Condition(JsonNode? raw, string path, bool? office)
    {
        if (office != false || raw is not JsonObject condition || condition.ContainsKey("is") == condition.ContainsKey("in"))
        {
            Add("forms.condition_invalid", path);
            return;
        }

        Keys(condition, ConditionKeys, path);
        var target = Str(condition["field"]) is { } id && IsString(condition["field"]) ? _seen.GetValueOrDefault(id) : null;
        if (target is null || target.Office != false)
        {
            Add("forms.condition_invalid", path);
            return;
        }

        if (condition.ContainsKey("is"))
        {
            if (target.Kind != FieldKind.YesNo || !IsBool(condition["is"]))
            {
                Add("forms.condition_invalid", path);
            }

            return;
        }

        if (target.Kind is not (FieldKind.Choice or FieldKind.MultiChoice) || condition["in"] is not JsonArray chosen)
        {
            Add("forms.condition_invalid", path);
            return;
        }

        if (chosen.Count == 0 || chosen.Any(value => !IsString(value) || !target.Values.Contains(Str(value)!)))
        {
            Add("forms.condition_invalid", path);
        }
    }

    private void Use(JsonObject field, string path, FieldKind kind, bool? office, IReadOnlyList<string> values)
    {
        if (!IsString(field["bind"]))
        {
            Add("forms.binding_unknown", $"{path}.bind");
            return;
        }

        var key = Str(field["bind"])!;
        if (!_uses.TryGetValue(key, out var uses))
        {
            uses = [];
            _uses[key] = uses;
            _useOrder.Add(key);
        }

        uses.Add(new Usage(
            path,
            kind,
            office,
            kind == FieldKind.Consent || IsTrue(field["required"]),
            field.ContainsKey("visible_if"),
            values,
            ScaleOf(field["scale"]),
            _index));
    }

    private void CheckBindings()
    {
        foreach (var key in _useOrder)
        {
            var uses = _uses[key];
            if (!_bindings.TryGetValue(key, out var binding))
            {
                foreach (var use in uses)
                {
                    Add("forms.binding_unknown", $"{use.Path}.bind");
                }

                continue;
            }

            foreach (var use in uses.Skip(1))
            {
                Add("forms.binding_duplicate", use.Path);
            }

            foreach (var use in uses)
            {
                CheckUse(binding, use);
            }
        }

        foreach (var binding in _bindings.Values)
        {
            if (binding.Required && !_uses.ContainsKey(binding.Key))
            {
                Add("forms.binding_missing", "sections");
            }
        }
    }

    private void CheckUse(FormBinding binding, Usage use)
    {
        if (!binding.Kinds.Contains(use.Kind))
        {
            Add("forms.binding_kind", use.Path);
        }

        if (binding.Values is { } allowed && FieldKinds.WithOptions.Contains(use.Kind))
        {
            foreach (var value in use.Values)
            {
                if (!allowed.Contains(value))
                {
                    Add("forms.binding_values", use.Path);
                }
            }
        }

        if (binding.Scale is { } scale && use.Kind == FieldKind.Levels && use.Scale != scale)
        {
            Add("forms.binding_values", use.Path);
        }

        if (use.Office != false || (binding.Required && (use.Conditional || !use.Required)))
        {
            Add("forms.binding_conditional", use.Path);
        }
    }

    private void CheckSignature()
    {
        if (_signatureAt is not { } signature)
        {
            return;
        }

        foreach (var key in FormProfile.NameBindings)
        {
            if (_uses.TryGetValue(key, out var uses) && uses.Count > 0 && uses[0].Index > signature.Index)
            {
                Add("forms.signature_before_name", signature.Path);
            }
        }
    }

    // ── asoslar ───────────────────────────────────────────────────────

    private void Keys(JsonObject raw, HashSet<string> known, string path)
    {
        foreach (var pair in raw)
        {
            if (!known.Contains(pair.Key))
            {
                Add("forms.key_unknown", path.Length > 0 ? $"{path}.{pair.Key}" : pair.Key);
            }
        }
    }

    private void Id(JsonNode? raw, string path, HashSet<string> scope)
    {
        if (!IsString(raw) || !IdPattern().IsMatch(Str(raw)!))
        {
            Add("forms.id_invalid", path);
        }
        else if (!scope.Add(Str(raw)!))
        {
            Add("forms.id_duplicate", path);
        }
    }

    /// <summary>Text: lotin bor; har til — oddiy matn, chegarada.</summary>
    private void Text(JsonNode? raw, string path, int maxLength, bool multiline = false, bool optional = false)
    {
        if (raw is null)
        {
            if (!optional)
            {
                Add("forms.text_missing", path);
            }

            return;
        }

        if (raw is not JsonObject text)
        {
            Add("forms.text_invalid", path);
            return;
        }

        var latin = text[FormTexts.Latin];
        if (!IsString(latin) || string.IsNullOrWhiteSpace(Str(latin)))
        {
            Add("forms.text_missing", path);
        }

        foreach (var pair in text)
        {
            if (!FormTexts.Languages.Contains(pair.Key) || !IsString(pair.Value))
            {
                Add("forms.text_invalid", path);
            }
            else if (!string.IsNullOrWhiteSpace(Str(pair.Value)) && CleanText(Str(pair.Value)!, multiline, maxLength) is null)
            {
                Add("forms.text_invalid", path);
            }
        }
    }

    /// <summary>Bosma matn (faqat lotin).</summary>
    private void Line(JsonNode? raw, string path, int maxLength, bool optional = true)
    {
        if (raw is null || (IsString(raw) && string.IsNullOrWhiteSpace(Str(raw))))
        {
            if (!optional)
            {
                Add("forms.text_missing", path);
            }

            return;
        }

        if (!IsString(raw) || CleanText(Str(raw)!, multiline: false, maxLength) is null)
        {
            Add("forms.text_invalid", path);
        }
    }

    private long? Int(JsonObject raw, string key, string path, long low, long high, bool required = false)
    {
        var value = raw[key];
        if (value is null)
        {
            if (required)
            {
                Add("forms.range_invalid", $"{path}.{key}");
            }

            return null;
        }

        if (!IsInt(value) || Long(value) is not { } number || number < low || number > high)
        {
            Add("forms.range_invalid", $"{path}.{key}");
            return null;
        }

        return number;
    }

    private void Range(JsonObject raw, string path, string lowKey, string highKey, long lo, long hi)
    {
        var low = Int(raw, lowKey, path, lo, hi);
        var high = Int(raw, highKey, path, lo, hi);
        if (low is not null && high is not null && low > high)
        {
            Add("forms.range_invalid", path);
        }
    }

    private void Bool(JsonObject raw, string key, string path)
    {
        if (raw.ContainsKey(key) && !IsBool(raw[key]))
        {
            Add("forms.shape_invalid", $"{path}.{key}");
        }
    }

    /// <summary>
    /// HRMentor <c>clean_text</c>: qirqilgan, <c>\n</c> qatorli; ko'rsatib bo'lmasa <c>null</c> —
    /// bo'sh, uzun, bir qatorli matnda qator, yoki Cc / Cf / Cs / Co / Cn toifasidagi belgi.
    /// </summary>
    public static string? CleanText(string text, bool multiline, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        var value = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (value.Length == 0 || value.EnumerateRunes().Count() > maxLength)
        {
            return null;
        }

        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                if (!multiline)
                {
                    return null;
                }

                continue;
            }

            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned)
            {
                return null;
            }
        }

        return value;
    }

    internal static bool IsString(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String;

    internal static bool IsBool(JsonNode? node)
        => node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False;

    internal static bool IsTrue(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.True;

    internal static bool IsInt(JsonNode? node) => Long(node) is not null;

    internal static string? Str(JsonNode? node) => IsString(node) ? node!.GetValue<string>() : null;

    /// <summary>Butun son (JSON da kasrsiz yozilgan) yoki <c>null</c>.</summary>
    internal static long? Long(JsonNode? node)
    {
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        var raw = value.ToJsonString();
        return raw.All(symbol => char.IsAsciiDigit(symbol) || symbol == '-')
            && long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                ? number
                : null;
    }

    /// <summary>levels shkalasi (min, max) 0..10 ichida, yoki <c>null</c>.</summary>
    internal static (int Min, int Max)? ScaleOf(JsonNode? raw)
    {
        if (raw is not JsonObject scale || Long(scale["min"]) is not { } low || Long(scale["max"]) is not { } high)
        {
            return null;
        }

        return low >= 0 && low < high && high <= MaxScale ? ((int)low, (int)high) : null;
    }

    /// <summary>repeat <c>summary</c> satridagi <c>{ustun}</c> nomlari.</summary>
    internal static HashSet<string> SummaryNames(string summary)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in summary.Split('{').Skip(1))
        {
            var close = part.IndexOf('}', StringComparison.Ordinal);
            if (close >= 0)
            {
                _ = names.Add(part[..close].Trim());
            }
        }

        return names;
    }

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private sealed record Seen(FieldKind Kind, bool? Office, IReadOnlyList<string> Values);

    private sealed record Usage(
        string Path,
        FieldKind Kind,
        bool? Office,
        bool Required,
        bool Conditional,
        IReadOnlyList<string> Values,
        (int Min, int Max)? Scale,
        int Index);
}
