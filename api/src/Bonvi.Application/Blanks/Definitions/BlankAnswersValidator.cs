using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>Javoblar tekshiruvi sozlamalari.</summary>
/// <param name="Today">Toshkent bo'yicha bugun (<c>today+N</c> chegaralari uchun).</param>
/// <param name="RequiredSections">
/// Majburiylik tekshiriladigan bo'limlar: saqlashda — bo'sh (faqat tur va chegara), topshirishda —
/// qoralama bo'limlari, qadam imzolanganda — shu qadam bo'limlari (§5).
/// </param>
public sealed record BlankAnswersOptions(DateOnly Today, IReadOnlySet<string>? RequiredSections = null);

/// <summary>
/// Javoblardagi tashqi havola — mavjudligini chaqiruvchi bazadan tekshiradi (dvigatel bazani ko'rmaydi).
/// </summary>
/// <param name="Path">Maydon yo'li (<c>mentor</c>, <c>evidence</c>, <c>t[0].doc</c>) — fayl uchun bu <c>field_path</c>.</param>
/// <param name="Kind">ref turi (<c>employee</c>…), <c>docRef</c> yoki <c>file</c>.</param>
/// <param name="Id">Havola id'si (fayl uchun — attachment id).</param>
/// <param name="TemplateCode">Faqat docRef: maqsad shablon kodi.</param>
public sealed record BlankAnswerReference(string Path, string Kind, Guid Id, string? TemplateCode = null);

/// <summary>Tekshiruv natijasi: TOZALANGAN javoblar, hisoblangan qiymatlar va tashqi havolalar.</summary>
public sealed record BlankAnswersOutcome(JsonObject Answers, JsonObject Computed, IReadOnlyList<BlankAnswerReference> References);

/// <summary>
/// Blanka javoblarining SERVER tekshiruvi (SPEC-blankalar §4.3, §4.4, §5).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Birinchi xato qaytadi</b> (anketa tekshiruvi kabi): forma o'zi ham shu qoidalarni ushlaydi,
/// server xatosi — nodir kelishmovchilik; xato tafsilotida maydon yo'li (<c>questions[0].date</c>).
/// </para>
/// <para>
/// Natija — TOZALANGAN nusxa: matn qirqilgan (trim), bo'sh qiymat yo'q, static / computed kalitlar va
/// <c>when</c> i yolg'on bo'lgan (yashirin) maydonlar TASHLANGAN, choice qisqa yozuvi (<c>"stop"</c>)
/// <c>{"value":"stop"}</c> ga aylangan. Bazaga faqat shu boradi.
/// </para>
/// <para>
/// Uzun matn QIRQILMAYDI — chegaradan oshsa xato: imzolanadigan hujjatdan so'z jim yo'qolmasin.
/// Variant qo'shimchasi (extra) tanlanmagan variantda tashlanadi.
/// </para>
/// </remarks>
public static partial class BlankAnswersValidator
{
    /// <summary>ref yorlig'i (ism nusxasi) chegarasi.</summary>
    public const int MaxRefLabelLength = 300;

    /// <summary>Buyruq / hujjat raqami chegarasi.</summary>
    public const int MaxNumberLength = 50;

    /// <summary>int maydonning sukut chegarasi (JS sonida aniq qoladi).</summary>
    public const decimal MaxInteger = 1_000_000_000_000m;

    /// <summary>Javoblarni tekshiradi va tozalangan nusxasini, computed qiymatlarni qaytaradi.</summary>
    public static Result<BlankAnswersOutcome> Validate(BlankDefinition definition, JsonNode? answers, BlankAnswersOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);

        if (answers is not null and not JsonObject)
        {
            return BlankAnswerErrors.Unreadable;
        }

        var walker = new Walker(definition, options.Today, options.RequiredSections ?? new HashSet<string>(), lenient: false);

        try
        {
            var cleaned = walker.Walk(answers as JsonObject ?? []);
            return new BlankAnswersOutcome(cleaned, BlankComputedEvaluator.ComputeAll(definition, cleaned), walker.References);
        }
        catch (AnswerFailure failure)
        {
            return failure.Error;
        }
    }

    /// <summary>Qaysi yuqori darajadagi maydonlar o'zgargan (saqlashda tahrirlanmaydigan bo'lim tekshiruvi uchun).</summary>
    public static IReadOnlySet<string> ChangedFields(JsonObject? before, JsonObject? after)
    {
        var keys = (before?.Select(pair => pair.Key) ?? []).Union(after?.Select(pair => pair.Key) ?? [], StringComparer.Ordinal);

        return keys.Where(key => !JsonNode.DeepEquals(before?[key], after?[key])).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Majburiy, ko'rinadigan, bo'sh maydonlar yo'llari (<see cref="BlankCompleteness"/> uchun).</summary>
    internal static List<string> CollectMissing(BlankDefinition definition, JsonObject answers, IReadOnlySet<string> sections)
    {
        var walker = new Walker(definition, DateOnly.MinValue, sections, lenient: true);
        walker.Walk(answers);
        return walker.Missing;
    }

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2}(\.[0-9]{1,7})?)?(Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimePattern();

    [GeneratedRegex("^[0-9]{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant)]
    private static partial Regex MonthPattern();

    /// <summary>Tekshiruvni to'xtatadigan birinchi xato — faqat shu sinf ichida ushlanadi.</summary>
    private sealed class AnswerFailure(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }

    /// <summary>
    /// Bitta yurish. <c>lenient</c> rejimda (to'liqlik hisobi — saqlangan javoblar ustida) turdagi xato
    /// to'xtatmaydi (qiymat «to'ldirilgan» hisoblanadi), majburiy bo'sh maydon esa ro'yxatga yoziladi.
    /// </summary>
    /// <remarks>
    /// Istisno bilan to'xtatish ATAYLAB (anketa tekshiruvidagi sabab bilan): har turda «xato bo'lsa qaytar»
    /// zanjiri kodni bir necha barobar uzaytirardi. Istisno sinf tashqarisiga chiqmaydi.
    /// </remarks>
    private sealed class Walker(BlankDefinition definition, DateOnly today, IReadOnlySet<string> requiredSections, bool lenient)
    {
        public List<string> Missing { get; } = [];

        public List<BlankAnswerReference> References { get; } = [];

        private BlankScope scope = null!;

        public JsonObject Walk(JsonObject source)
        {
            foreach (var (key, _) in source)
            {
                if (definition.FindField(key) is null && !lenient)
                {
                    Invalid(key);
                }
            }

            // Ko'rinish XOM javoblar ustida: shart oldinga ham qaraydi, bir o'tishda tozalab bo'lmaydi
            scope = new BlankScope(definition, source);
            var cleaned = new JsonObject();

            foreach (var section in definition.Sections)
            {
                var required = requiredSections.Contains(section.Id);

                foreach (var field in section.Fields)
                {
                    if (field.Type is BlankFieldType.Static or BlankFieldType.Computed || !scope.FieldVisible(field))
                    {
                        continue;
                    }

                    if (CleanField(field, source[field.Id], field.Id, required) is { } value)
                    {
                        cleaned[field.Id] = value;
                    }
                }
            }

            return cleaned;
        }

        private JsonNode? CleanField(BlankField field, JsonNode? raw, string path, bool requiredScope)
        {
            JsonNode? value = null;

            if (!BlankValues.IsEmpty(raw))
            {
                try
                {
                    value = Clean(field, raw!, path, requiredScope);
                }
                catch (AnswerFailure) when (lenient)
                {
                    value = raw!.DeepClone();
                }
            }

            if (requiredScope && field.Required && IsEmptyTyped(field, value))
            {
                MissingAt(path);
            }

            return value;
        }

        private JsonNode? Clean(BlankField field, JsonNode raw, string path, bool requiredScope) => field.Type switch
        {
            BlankFieldType.Text => CleanText(raw, path, field.EffectiveMaxLength, multiline: false),
            BlankFieldType.LongText => CleanText(raw, path, field.EffectiveMaxLength, multiline: true),
            BlankFieldType.WholeNumber => Whole(raw, path, field.Min?.Number ?? -MaxInteger, field.Max?.Number ?? MaxInteger),
            BlankFieldType.Money => Whole(raw, path, field.Min?.Number ?? 0, field.Max?.Number ?? BlankDefinitionValidator.MaxMoney),
            BlankFieldType.Year => Whole(raw, path, field.Min?.Number ?? 1900, field.Max?.Number ?? 2100),
            BlankFieldType.Score => Whole(raw, path, 1, 5),
            BlankFieldType.Percent => CleanPercent(raw, path),
            BlankFieldType.Date => JsonValue.Create(BlankValues.FormatDate(CheckDateBounds(field, ParseDate(raw, path), path))),
            BlankFieldType.DateTime => CleanDateTime(field, raw, path),
            BlankFieldType.Month => CleanMonth(field, raw, path),
            BlankFieldType.DateRange => CleanDateRange(raw, path),
            BlankFieldType.YesNo or BlankFieldType.Check => raw is JsonValue flag && flag.GetValueKind() is JsonValueKind.True or JsonValueKind.False
                ? JsonValue.Create(flag.GetValue<bool>())
                : Invalid(path),
            BlankFieldType.Choice => CleanChoice(field, raw, path, requiredScope),
            BlankFieldType.Multi => CleanMulti(field, raw, path, requiredScope),
            BlankFieldType.Ref => CleanRef(field, raw, path),
            BlankFieldType.DocRef => CleanDocRef(field, raw, path),
            BlankFieldType.Order => CleanOrder(raw, path),
            BlankFieldType.File => CleanFiles(field, raw, path),
            BlankFieldType.Table => CleanTable(field, raw, path, requiredScope),
            _ => null,
        };

        // ── Turlar ──

        private static JsonValue? CleanText(JsonNode raw, string path, int limit, bool multiline)
        {
            var text = Str(raw, path).Trim();

            if (text.Length == 0)
            {
                return null;
            }

            if (QuestionnaireFormValidator.HasForbiddenCharacter(text, multiline))
            {
                Invalid(path);
            }

            if (text.Length > limit)
            {
                Invalid(path, limit);
            }

            return JsonValue.Create(text);
        }

        private static JsonValue Whole(JsonNode raw, string path, decimal min, decimal max)
        {
            var number = Num(raw, path);

            if (number != decimal.Truncate(number) || number < min || number > max)
            {
                Invalid(path);
            }

            return JsonValue.Create((long)number);
        }

        private static JsonValue CleanPercent(JsonNode raw, string path)
        {
            var number = Num(raw, path);

            // ≤ 1 kasr raqam: 72.5 o'tadi, 72.55 — yo'q
            if (number is < 0 or > 100 || number * 10 != decimal.Truncate(number * 10))
            {
                Invalid(path);
            }

            return JsonValue.Create(BlankValues.Normalize(number));
        }

        private static DateOnly ParseDate(JsonNode raw, string path)
            => DateOnly.TryParseExact(Str(raw, path), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : Invalid<DateOnly>(path);

        private DateOnly CheckDateBounds(BlankField field, DateOnly date, string path)
        {
            if ((field.Min?.ResolveDate(today) is { } min && date < min) || (field.Max?.ResolveDate(today) is { } max && date > max))
            {
                Invalid(path);
            }

            return date;
        }

        private JsonValue CleanDateTime(BlankField field, JsonNode raw, string path)
        {
            var text = Str(raw, path).Trim();

            if (!DateTimePattern().IsMatch(text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
            {
                return Invalid<JsonValue>(path);
            }

            CheckDateBounds(field, DateOnly.FromDateTime(moment.DateTime), path);
            return JsonValue.Create(text);
        }

        private JsonValue CleanMonth(BlankField field, JsonNode raw, string path)
        {
            var text = Str(raw, path).Trim();

            if (!MonthPattern().IsMatch(text))
            {
                return Invalid<JsonValue>(path);
            }

            var month = (int.Parse(text[..4], CultureInfo.InvariantCulture) * 12) + int.Parse(text[5..], CultureInfo.InvariantCulture);

            if ((field.Min?.ResolveDate(today) is { } min && month < (min.Year * 12) + min.Month)
                || (field.Max?.ResolveDate(today) is { } max && month > (max.Year * 12) + max.Month))
            {
                Invalid(path);
            }

            return JsonValue.Create(text);
        }

        private static JsonObject? CleanDateRange(JsonNode raw, string path)
        {
            var obj = Obj(raw, path, "from", "to");
            var result = new JsonObject();
            DateOnly? from = null;
            DateOnly? to = null;

            if (!BlankValues.IsEmpty(obj["from"]))
            {
                from = ParseDate(obj["from"]!, $"{path}.from");
                result["from"] = BlankValues.FormatDate(from.Value);
            }

            if (!BlankValues.IsEmpty(obj["to"]))
            {
                to = ParseDate(obj["to"]!, $"{path}.to");
                result["to"] = BlankValues.FormatDate(to.Value);
            }

            if (from > to)
            {
                Invalid($"{path}.to");
            }

            return result.Count == 0 ? null : result;
        }

        private JsonObject CleanChoice(BlankField field, JsonNode raw, string path, bool requiredScope)
        {
            JsonNode? extraRaw = null;
            string value;

            if (raw is JsonObject obj)
            {
                Obj(obj, path, "value", "extra");
                value = obj["value"] is { } node ? Str(node, $"{path}.value") : Invalid<string>($"{path}.value");
                extraRaw = obj["extra"];
            }
            else
            {
                value = Str(raw, path);
            }

            var option = field.Options.FirstOrDefault(candidate => candidate.Value == value) ?? Invalid<BlankOption>(path);
            var result = new JsonObject { ["value"] = option.Value };

            if (option.Extra is { } extra && CleanField(extra.AsField("extra"), extraRaw, $"{path}.extra", requiredScope) is { } extraValue)
            {
                result["extra"] = extraValue;
            }

            return result;
        }

        private JsonArray? CleanMulti(BlankField field, JsonNode raw, string path, bool requiredScope)
        {
            if (raw is not JsonArray items)
            {
                return Invalid<JsonArray>(path);
            }

            var result = new JsonArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < items.Count; i++)
            {
                var item = CleanChoice(field, items[i] ?? Invalid<JsonNode>($"{path}[{i}]"), $"{path}[{i}]", requiredScope);

                if (!seen.Add(item["value"]!.GetValue<string>()))
                {
                    Invalid($"{path}[{i}]");
                }

                result.Add(item);
            }

            if (field.MaxSelected is { } limit && result.Count > limit)
            {
                Invalid(path, limit);
            }

            return result.Count == 0 ? null : result;
        }

        private JsonObject CleanRef(BlankField field, JsonNode raw, string path)
        {
            var obj = Obj(raw, path, "id", "label");
            var id = Id(obj["id"], $"{path}.id");
            var result = new JsonObject { ["id"] = id.ToString() };

            if (!BlankValues.IsEmpty(obj["label"]) && CleanText(obj["label"]!, $"{path}.label", MaxRefLabelLength, multiline: false) is { } label)
            {
                result["label"] = label;
            }

            References.Add(new BlankAnswerReference(path, field.RefKind!, id));
            return result;
        }

        private JsonObject CleanDocRef(BlankField field, JsonNode raw, string path)
        {
            var obj = Obj(raw, path, "submissionId", "templateCode", "number");
            var id = Id(obj["submissionId"], $"{path}.submissionId");
            var code = obj["templateCode"] is { } codeNode ? Str(codeNode, $"{path}.templateCode") : Invalid<string>($"{path}.templateCode");

            if (!field.TemplateCodes.Contains(code, StringComparer.Ordinal))
            {
                Invalid($"{path}.templateCode");
            }

            var result = new JsonObject { ["submissionId"] = id.ToString(), ["templateCode"] = code };

            if (!BlankValues.IsEmpty(obj["number"]) && CleanText(obj["number"]!, $"{path}.number", MaxNumberLength, multiline: false) is { } number)
            {
                result["number"] = number;
            }

            References.Add(new BlankAnswerReference(path, "docRef", id, code));
            return result;
        }

        private static JsonObject? CleanOrder(JsonNode raw, string path)
        {
            var obj = Obj(raw, path, "number", "date");
            var result = new JsonObject();

            if (!BlankValues.IsEmpty(obj["number"]) && CleanText(obj["number"]!, $"{path}.number", MaxNumberLength, multiline: false) is { } number)
            {
                result["number"] = number;
            }

            if (!BlankValues.IsEmpty(obj["date"]))
            {
                result["date"] = BlankValues.FormatDate(ParseDate(obj["date"]!, $"{path}.date"));
            }

            return result.Count == 0 ? null : result;
        }

        private JsonArray? CleanFiles(BlankField field, JsonNode raw, string path)
        {
            if (raw is not JsonArray items)
            {
                return Invalid<JsonArray>(path);
            }

            var seen = new HashSet<Guid>();
            var result = new JsonArray();

            for (var i = 0; i < items.Count; i++)
            {
                var id = Id(items[i], $"{path}[{i}]");

                if (!seen.Add(id))
                {
                    Invalid($"{path}[{i}]");
                }

                References.Add(new BlankAnswerReference(path, "file", id));
                result.Add(id.ToString());
            }

            if (result.Count > field.EffectiveMaxFiles)
            {
                Invalid(path, field.EffectiveMaxFiles);
            }

            return result.Count == 0 ? null : result;
        }

        private JsonArray? CleanTable(BlankField table, JsonNode raw, string path, bool requiredScope)
        {
            if (raw is not JsonArray rows)
            {
                return Invalid<JsonArray>(path);
            }

            var fixedRows = table.FixedRows;

            if (fixedRows.Count > 0 ? rows.Count != fixedRows.Count : rows.Count > table.EffectiveMaxRows)
            {
                Invalid(path, table.EffectiveMaxRows);
            }

            var result = new JsonArray();

            for (var i = 0; i < rows.Count; i++)
            {
                var rowPath = $"{path}[{i}]";

                if (rows[i] is not JsonObject cells)
                {
                    return Invalid<JsonArray>(rowPath);
                }

                var fixedRow = fixedRows.Count > 0 ? fixedRows[i] : null;

                foreach (var (key, value) in cells)
                {
                    var known = key == "_row"
                        ? fixedRow is not null && (BlankValues.IsEmpty(value) || BlankValues.AreEqual(BlankValues.FromNode(value), fixedRow.Id))
                        : table.FindColumn(key) is not null;

                    if (!known)
                    {
                        Invalid($"{rowPath}.{key}");
                    }
                }

                var clean = new JsonObject();

                if (fixedRow is not null)
                {
                    clean["_row"] = fixedRow.Id;
                }
                else if (table.Columns.All(column => BlankValues.IsEmpty(cells[column.Id])))
                {
                    // Erkin jadvalning butunlay bo'sh qatori — forma qo'shib qo'ygan, hali yozilmagan
                    continue;
                }

                foreach (var column in table.Columns)
                {
                    if (column.Type == BlankFieldType.Computed
                        || (fixedRow?.Cells.GetValueOrDefault(column.Id) is { ReadOnly: true })
                        || !scope.CellVisible(table, i, column))
                    {
                        continue;
                    }

                    if (CleanField(column, cells[column.Id], $"{rowPath}.{column.Id}", requiredScope) is { } value)
                    {
                        clean[column.Id] = value;
                    }
                }

                result.Add(clean);
            }

            return result.Count == 0 ? null : result;
        }

        /// <summary>Bo'shmi (§5.3): null, belgilanmagan check, yarim sana oralig'i / buyruq, minRows dan kam jadval.</summary>
        private static bool IsEmptyTyped(BlankField field, JsonNode? value) => value switch
        {
            null => true,
            _ => field.Type switch
            {
                BlankFieldType.Check => value.GetValue<bool>() is false,
                BlankFieldType.DateRange => value["from"] is null || value["to"] is null,
                BlankFieldType.Order => value["number"] is null || value["date"] is null,
                BlankFieldType.Table when field.FixedRows.Count > 0 => value.AsArray().All(row => row!.AsObject().Count <= 1),
                BlankFieldType.Table => value.AsArray().Count < Math.Max(1, field.MinRows ?? 0),
                _ => false,
            },
        };

        // ── Kichik o'quvchilar ──

        private static string Str(JsonNode node, string path)
            => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : Invalid<string>(path);

        private static decimal Num(JsonNode node, string path)
            => node is JsonValue value && BlankValues.TryNumber(value, out var number)
                ? number
                : Invalid<decimal>(path);

        private static Guid Id(JsonNode? node, string path)
            => node is JsonValue value && value.GetValueKind() == JsonValueKind.String && Guid.TryParse(value.GetValue<string>(), out var id) && id != Guid.Empty
                ? id
                : Invalid<Guid>(path);

        private static JsonObject Obj(JsonNode node, string path, params string[] keys)
        {
            if (node is not JsonObject obj)
            {
                return Invalid<JsonObject>(path);
            }

            foreach (var (key, _) in obj)
            {
                if (!keys.Contains(key, StringComparer.Ordinal))
                {
                    Invalid($"{path}.{key}");
                }
            }

            return obj;
        }

        private void MissingAt(string path)
        {
            if (lenient)
            {
                Missing.Add(path);
                return;
            }

            throw new AnswerFailure(BlankAnswerErrors.Required.At(path));
        }

        private static JsonNode? Invalid(string path, int? limit = null)
            => throw new AnswerFailure(BlankAnswerErrors.Invalid.At(path, limit));

        private static T Invalid<T>(string path) => throw new AnswerFailure(BlankAnswerErrors.Invalid.At(path));
    }
}
