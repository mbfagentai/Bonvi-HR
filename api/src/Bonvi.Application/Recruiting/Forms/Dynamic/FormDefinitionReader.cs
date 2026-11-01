using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks;
using Bonvi.Application.Recruiting.Forms.Texts;
using Bonvi.Domain.Common;
using static Bonvi.Application.Recruiting.Forms.Dynamic.FormDefinitionChecker;

namespace Bonvi.Application.Recruiting.Forms.Dynamic;

/// <summary>
/// Ta'rif JSON ini o'qiydi: avval <see cref="FormDefinitionChecker"/> (BonviHR profiliga qarshi),
/// keyin turlarga. Muammosi bor ta'rif hech qachon <see cref="FormDefinition"/> bo'lmaydi.
/// </summary>
public static class FormDefinitionReader
{
    /// <summary>JSON matnni o'qiydi: sarlavha (<c>schema</c>, <c>code</c>, <c>label</c>), hajm va muammolar.</summary>
    public static Result<FormDefinition> Read(string json)
        => Parse(json) is { } node ? Read(node) : HrMentorErrors.DefinitionInvalid.At("definition");

    /// <summary>Tekshiradi va o'qiydi. Xato tafsilotida — birinchi muammoning kodi va joyi.</summary>
    public static Result<FormDefinition> Read(JsonObject definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (CanonicalJson.Size(definition) > MaxDefinitionBytes)
        {
            return HrMentorErrors.DefinitionInvalid.At("definition");
        }

        if (Long(definition["schema"]) != FormProfile.Schema)
        {
            return HrMentorErrors.DefinitionInvalid.At("schema");
        }

        if (Str(definition["code"]) != FormProfile.Code)
        {
            return HrMentorErrors.DefinitionInvalid.At("code");
        }

        if (Str(definition["label"]) is not { } label || !LabelPattern().IsMatch(label))
        {
            return HrMentorErrors.DefinitionInvalid.At("label");
        }

        var issues = Check(definition, FormProfile.ByKey);
        if (issues.Count > 0)
        {
            return HrMentorErrors.DefinitionInvalid.At($"{issues[0].Code} {issues[0].Path}");
        }

        return Build(definition, label);
    }

    /// <summary>JSON obyekt yoki <c>null</c> (buzilgan, takroriy kalitli yoki obyekt emas).</summary>
    public static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            // JsonObject takroriy kalitni faqat birinchi murojaatda sezadi — shu yerda hammasini ochamiz
            var node = JsonNode.Parse(json) as JsonObject;
            _ = CanonicalJson.Size(node);
            return node;
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static FormDefinition Build(JsonObject definition, string label)
    {
        var pdf = definition["pdf"]!.AsObject();
        var sections = definition["sections"]!.AsArray()
            .Select(node =>
            {
                var section = node!.AsObject();
                return new FormSection(
                    Str(section["id"])!,
                    Str(section["audience"]) == "office",
                    Text(section["title"])!,
                    Optional(section["pdf_title"]),
                    Text(section["hint"]),
                    [.. section["screens"]!.AsArray().Select(screenNode =>
                    {
                        var screen = screenNode!.AsObject();
                        return new FormScreen(
                            Str(screen["id"])!,
                            Text(screen["title"]),
                            Text(screen["hint"]),
                            [.. screen["fields"]!.AsArray().Select(field => Field(field!.AsObject()))]);
                    })]);
            })
            .ToList();

        return new FormDefinition(
            FormProfile.Code,
            label,
            Text(definition["title"])!,
            new FormPdf(
                Str(pdf["printed_code"])!.Trim(),
                Str(pdf["title"])!.Trim(),
                Optional(pdf["subtitle"]),
                Optional(pdf["instruction"]),
                Optional(pdf["candidate_part"]),
                Optional(pdf["office_part"]),
                Optional(pdf["footer"])),
            sections);
    }

    private static FormField Field(JsonObject raw)
    {
        var kind = FieldKinds.TryParse(Str(raw["kind"]), out var parsed)
            ? parsed
            : throw new InvalidOperationException("Tekshirilmagan ta'rif: noma'lum tur");
        var scale = ScaleOf(raw["scale"]);
        var maxYear = raw["max_year"];

        return new FormField
        {
            Id = Str(raw["id"])!,
            Kind = kind,
            Label = Text(raw["label"]),
            Hint = Text(raw["hint"]),
            Required = IsTrue(raw["required"]),
            Bind = Str(raw["bind"]),
            VisibleIf = raw["visible_if"] is JsonObject condition
                ? new FormCondition(
                    Str(condition["field"])!,
                    IsBool(condition["is"]) ? IsTrue(condition["is"]) : null,
                    condition["in"] is JsonArray chosen ? [.. chosen.Select(value => Str(value)!)] : null)
                : null,
            PdfLabel = Optional(raw["pdf_label"]),
            PdfHidden = IsTrue(raw["pdf_hidden"]),
            MaxLength = (int?)Long(raw["max_length"]),
            MinLetters = (int)(Long(raw["min_letters"]) ?? 0),
            SameAs = raw["same_as"] is JsonObject sameAs ? new FormSameAs(Str(sameAs["field"])!, Text(sameAs["label"])!) : null,
            Min = Long(raw["min"]),
            Max = Long(raw["max"]),
            MinAge = (int?)Long(raw["min_age"]),
            MaxAge = (int?)Long(raw["max_age"]),
            NotBeforeToday = IsTrue(raw["not_before_today"]),
            MinYear = (int?)Long(raw["min_year"]),
            MaxYear = (int?)Long(maxYear),
            MaxYearToday = Str(maxYear) == "today",
            MobileOnly = !raw.ContainsKey("mobile_only") || IsTrue(raw["mobile_only"]),
            Yes = Text(raw["yes"]),
            No = Text(raw["no"]),
            Options = raw["options"] is JsonArray options
                ? [.. options.Select(option => option!.AsObject()).Select(option => new FormOption(
                    Str(option["value"])!,
                    Text(option["label"])!,
                    Str(option["group"]),
                    Optional(option["pdf_label"])))]
                : [],
            Groups = Labelled(raw["groups"]),
            Style = Str(raw["style"]),
            MinSelected = (int?)Long(raw["min_selected"]),
            MaxSelected = (int?)Long(raw["max_selected"]),
            Aspects = Labelled(raw["aspects"]),
            ScaleMin = scale?.Min ?? 0,
            ScaleMax = scale?.Max ?? 0,
            RequiredRows = Strings(raw["required_rows"]),
            Other = raw["other"] is JsonObject other ? new FormLevelsOther((int)Long(other["max"])!.Value, Text(other["label"])!) : null,
            PrintedRows = kind == FieldKind.Levels ? Strings(raw["print_rows"]) : [],
            Columns = raw["columns"] is JsonArray columns ? [.. columns.Select(column => Field(column!.AsObject()))] : [],
            MinRows = (int)(Long(raw["min_rows"]) ?? 0),
            MaxRows = (int?)Long(raw["max_rows"]),
            Summary = Optional(raw["summary"]),
            PrintRowCount = kind == FieldKind.Repeat ? (int)(Long(raw["print_rows"]) ?? 0) : 0,
            AllowCurrent = IsTrue(raw["allow_current"]),
            Text = Text(raw["text"]),
            Items = raw["items"] is JsonArray items
                ? [.. items.Select(item => item!.AsObject()).Select(item => new FormChecklistItem(
                    Str(item["label"])!.Trim(),
                    Str(item["auto"]),
                    Optional(item["note"])))]
                : [],
        };
    }

    private static List<FormLabelled> Labelled(JsonNode? raw)
        => raw is JsonArray array
            ? [.. array.Select(item => item!.AsObject()).Select(item => new FormLabelled(Str(item["id"])!, Text(item["label"])!))]
            : [];

    private static List<string> Strings(JsonNode? raw)
        => raw is JsonArray array ? [.. array.Select(item => Str(item)!)] : [];

    /// <summary>Text — tekshiruvchi lotinni kafolatlaydi; qolgan tillar bo'sh bo'lsa lotin.</summary>
    private static FormText? Text(JsonNode? raw)
    {
        if (raw is not JsonObject text)
        {
            return null;
        }

        var latin = Clean(Str(text[FormTexts.Latin])) ?? string.Empty;
        return new FormText(
            latin,
            Clean(Str(text[FormTexts.Cyrillic])) ?? latin,
            Clean(Str(text[FormTexts.Russian])) ?? latin);
    }

    private static string? Optional(JsonNode? raw) => Clean(Str(raw));

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    /// <summary>Saqlangan ta'rifni qayta o'qish (tekshirilgan, o'zgarmas nusxa).</summary>
    public static FormDefinition? Stored(string json)
        => Read(json) is { IsSuccess: true } read ? read.Value : null;
}
