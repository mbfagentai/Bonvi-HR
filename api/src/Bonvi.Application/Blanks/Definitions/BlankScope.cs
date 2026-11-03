using System.Globalization;
using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>
/// Bitta javoblar to'plami ustida ko'rinish (<c>when</c>) va hisoblash (<c>computed</c>) — dangasa va eslab qoluvchi.
/// </summary>
/// <remarks>
/// <para>
/// Havolalar OLDINGA ham ketishi mumkin (bo'lim keyingi bo'limdagi maydonga qaraydi), shuning uchun
/// hujjat tartibida bir o'tish yetmaydi: har qiymat birinchi so'ralganda hisoblanadi. Yashirin maydon
/// qiymati — <c>null</c> (§4.4: yashirin maydon «yo'q»). Tsikllarni ta'rif tekshiruvi rad etadi; bu
/// yerdagi <see cref="inProgress"/> faqat himoya — tsiklda qiymat <c>null</c> / ko'rinmas bo'ladi.
/// </para>
/// <para>
/// Jadval ustunining <c>when</c> va <c>expr</c> idagi havola avval SHU QATORNING ustuniga, u bo'lmasa
/// yuqori darajadagi maydonga qaraydi (orkestrator qarori, 2026-11-03).
/// </para>
/// </remarks>
internal sealed class BlankScope(BlankDefinition definition, JsonObject answers)
{
    private readonly Dictionary<string, object?> memo = new(StringComparer.Ordinal);
    private readonly HashSet<string> inProgress = new(StringComparer.Ordinal);

    public bool SectionVisible(BlankSection section)
        => section.When is null || Memo($"s:{section.Id}", () => BlankConditions.Evaluate(section.When, TopNode), false) is true;

    /// <summary>Yuqori darajadagi maydon ko'rinadimi (bo'limi ham hisobga olinadi).</summary>
    public bool FieldVisible(BlankField field)
        => Memo($"f:{field.Id}", () => definition.SectionOf(field.Id) is { } section
            && SectionVisible(section)
            && (field.When is null || BlankConditions.Evaluate(field.When, TopNode)), false) is true;

    /// <summary>Yuqori darajadagi maydon qiymati (yashirin → <c>null</c>, computed → hisoblangan).</summary>
    public object? Value(string id)
    {
        if (definition.FindField(id) is not { } field || !FieldVisible(field))
        {
            return null;
        }

        return field.Type == BlankFieldType.Computed
            ? Memo($"v:{id}", () => Coerce(field, BlankComputedEvaluator.Eval(field.Expr!, path => Resolve(path, null, -1))), null)
            : BlankValues.FromNode(answers[id]);
    }

    /// <summary>Jadval qatoridagi ustun ko'rinadimi.</summary>
    public bool CellVisible(BlankField table, int row, BlankField column)
        => FieldVisible(table)
            && (column.When is null
                || Memo($"cv:{table.Id}[{row}].{column.Id}", () => BlankConditions.Evaluate(column.When, id => RowNode(table, row, id)), false) is true);

    /// <summary>Jadval katagining qiymati (yashirin → <c>null</c>, computed → hisoblangan).</summary>
    public object? CellValue(BlankField table, int row, BlankField column)
    {
        if (!CellVisible(table, row, column))
        {
            return null;
        }

        return column.Type == BlankFieldType.Computed
            ? Memo($"c:{table.Id}[{row}].{column.Id}", () => Coerce(column, BlankComputedEvaluator.Eval(column.Expr!, path => Resolve(path, table, row))), null)
            : Row(table, row) is { } cells ? BlankValues.FromNode(cells[column.Id]) : null;
    }

    /// <summary>Ko'rinadigan hamma computed qiymat: <c>avgScore</c>, <c>tests[0].passed</c>; <c>null</c> yozilmaydi.</summary>
    public JsonObject ComputeAll()
    {
        var result = new JsonObject();

        foreach (var field in definition.Fields)
        {
            if (field.Type == BlankFieldType.Computed)
            {
                Put(result, field.Id, Value(field.Id));
            }

            if (field.Type == BlankFieldType.Table && answers[field.Id] is JsonArray rows)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    foreach (var column in field.Columns.Where(column => column.Type == BlankFieldType.Computed))
                    {
                        Put(result, $"{field.Id}[{i}].{column.Id}", CellValue(field, i, column));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Shart uchun yuqori darajadagi qiymat (JSON ko'rinishida).</summary>
    public JsonNode? TopNode(string id)
    {
        if (definition.FindField(id) is not { } field || !FieldVisible(field))
        {
            return null;
        }

        return field.Type == BlankFieldType.Computed ? BlankValues.ToNode(Value(id)) : answers[id];
    }

    private JsonNode? RowNode(BlankField table, int row, string id)
    {
        if (table.FindColumn(id) is not { } column)
        {
            return TopNode(id);
        }

        if (!CellVisible(table, row, column))
        {
            return null;
        }

        return column.Type == BlankFieldType.Computed
            ? BlankValues.ToNode(CellValue(table, row, column))
            : Row(table, row)?[column.Id];
    }

    private JsonObject? Row(BlankField table, int row)
        => answers[table.Id] is JsonArray rows && row < rows.Count ? rows[row] as JsonObject : null;

    private object? Resolve(string path, BlankField? table, int row)
    {
        if (table?.FindColumn(path) is { } sibling)
        {
            return CellValue(table, row, sibling);
        }

        var reference = new BlankRefExpr(path);

        if (!reference.IsColumnSet)
        {
            return Value(path);
        }

        if (definition.FindField(reference.Head) is not { } target
            || target.FindColumn(reference.Column!) is not { } column
            || !FieldVisible(target)
            || answers[target.Id] is not JsonArray rows)
        {
            return new List<object?>();
        }

        return Enumerable.Range(0, rows.Count).Select(i => CellValue(target, i, column)).ToList();
    }

    private object? Memo(string key, Func<object?> compute, object? onCycle)
    {
        if (memo.TryGetValue(key, out var known))
        {
            return known;
        }

        if (!inProgress.Add(key))
        {
            return onCycle;
        }

        var value = compute();
        inProgress.Remove(key);
        memo[key] = value;
        return value;
    }

    private static void Put(JsonObject result, string path, object? value)
    {
        if (BlankValues.ToNode(value) is { } node)
        {
            result[path] = node;
        }
    }

    /// <summary><c>valueType</c> ga mos kelmagan natija → <c>null</c> (text esa son va mantiqni matnga aylantiradi).</summary>
    private static object? Coerce(BlankField field, object? value) => field.ValueType switch
    {
        "number" => value as decimal?,
        "bool" => value as bool?,
        "date" => BlankValues.TryDate(value, out var date) ? BlankValues.FormatDate(date) : null,
        "text" => value switch
        {
            string text => text,
            decimal number => BlankValues.Normalize(number).ToString(CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            _ => null,
        },
        _ => null,
    };
}
