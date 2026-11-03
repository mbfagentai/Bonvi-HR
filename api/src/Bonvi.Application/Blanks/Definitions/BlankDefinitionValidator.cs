using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>Ta'rif tekshiruvi natijasi: model (faqat xatosiz bo'lsa) va xatolar ro'yxati.</summary>
public sealed record BlankDefinitionValidation(BlankDefinition? Definition, IReadOnlyList<BlankDefinitionError> Errors)
{
    public bool IsValid => Definition is not null && Errors.Count == 0;
}

/// <summary>
/// Shablon ta'rifining yagona tekshiruvi (SPEC-blankalar §4.8) — seeder va kelajakdagi konstruktor chaqiradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Hamma xato birdaniga qaytadi</b> (<see cref="BlankDefinitionError"/> ro'yxati, yo'li bilan):
/// shablon muallifi bitta yurishda hammasini tuzatadi. Anketa tekshiruvidan farqi shu — u yerda
/// foydalanuvchiga birinchi xato yetarli edi.
/// </para>
/// <para>
/// Havolalar oldinga ham ketishi mumkin (bo'lim keyingi qadamga, shart keyingi maydonga) — ikki
/// o'tish: avval hamma id yig'iladi, keyin havolalar tekshiriladi. Rad etiladigani: noma'lum nishon va
/// computed / shart bog'liqliklaridagi TSIKL (orkestrator qarori, 2026-11-03).
/// </para>
/// </remarks>
public static partial class BlankDefinitionValidator
{
    /// <summary>Maydonlar (jadval ustunlari bilan) chegarasi.</summary>
    public const int MaxFields = 500;

    public const int MaxSteps = 30;

    public const int MaxTextLength = 500;

    public const int MaxLongTextLength = 4000;

    public const decimal MaxMoney = 1_000_000_000_000m;

    public const int MaxTableRows = 200;

    private static readonly string[] Subjects = ["employee", "candidate", "none"];

    private static readonly string[] LegalStatuses = ["final", "draft"];

    private static readonly string[] RefKinds = ["employee", "candidate", "department", "position", "direction"];

    private static readonly string[] ValueTypes = ["number", "date", "bool", "text"];

    private static readonly string[] FileAccepts = ["pdf", "image", "office", "any"];

    private static readonly string[] Captures = ["environment", "user"];

    private static readonly BlankFieldType[] ExtraTypes =
        [BlankFieldType.Text, BlankFieldType.LongText, BlankFieldType.Date, BlankFieldType.WholeNumber, BlankFieldType.Ref, BlankFieldType.DocRef];

    /// <summary>JSON ta'rifni o'qiydi va tekshiradi.</summary>
    /// <param name="json">Ta'rif matni.</param>
    /// <param name="expectedCode">Shablon qatoridagi / fayl nomidagi kod (berilsa, teng bo'lishi shart).</param>
    /// <param name="expectedVersion">Kutilgan versiya (berilsa, teng bo'lishi shart).</param>
    public static BlankDefinitionValidation Validate(string json, string? expectedCode = null, string? expectedVersion = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException error)
        {
            return new BlankDefinitionValidation(null, [new BlankDefinitionError("$", $"JSON o'qilmadi: {error.Message}")]);
        }

        var reader = new BlankDefinitionReader();
        var definition = reader.ReadDefinition(root);

        // Shakl xatosi bo'lsa ma'no tekshiruvi qilinmaydi — yarim o'qilgan model soxta xatolar ko'paytirardi
        if (reader.Errors.Count > 0 || definition is null)
        {
            return new BlankDefinitionValidation(null, reader.Errors);
        }

        var errors = new Check(definition).Run(expectedCode, expectedVersion);
        return new BlankDefinitionValidation(errors.Count == 0 ? definition : null, errors);
    }

    /// <summary>
    /// E'lon qilingan (avval tekshirilgan) ta'rifni yuklaydi; endi yaroqsiz bo'lsa — dastur xatosi.
    /// </summary>
    public static BlankDefinition Load(string json)
    {
        var result = Validate(json);

        return result.IsValid
            ? result.Definition!
            : throw new InvalidOperationException(
                "Blanka ta'rifi yaroqsiz: " + string.Join("; ", result.Errors.Select(error => $"{error.Path}: {error.Message}")));
    }

    [GeneratedRegex("^[a-z][A-Za-z0-9]{0,47}$", RegexOptions.CultureInvariant)]
    internal static partial Regex IdPattern();

    [GeneratedRegex("^[a-z0-9_]{1,32}$", RegexOptions.CultureInvariant)]
    internal static partial Regex OptionValuePattern();

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    /// <summary>Bitta tekshiruv yurishi.</summary>
    private sealed class Check(BlankDefinition definition)
    {
        private readonly List<BlankDefinitionError> errors = [];

        public List<BlankDefinitionError> Run(string? expectedCode, string? expectedVersion)
        {
            CheckHeader(expectedCode, expectedVersion);
            CheckIds();

            for (var s = 0; s < definition.Sections.Count; s++)
            {
                CheckSection(definition.Sections[s], $"sections[{s}]");
            }

            for (var i = 0; i < definition.Steps.Count; i++)
            {
                CheckStep(definition.Steps[i], $"steps[{i}]");
            }

            CheckCycles();
            return errors;
        }

        private void Add(string path, string message) => errors.Add(new BlankDefinitionError(path, message));

        private void CheckHeader(string? expectedCode, string? expectedVersion)
        {
            if (definition.Schema != 1)
            {
                Add("$.schema", "Faqat 1.");
            }

            if (expectedCode is not null && definition.Code != expectedCode)
            {
                Add("$.code", $"Kod «{expectedCode}» bo'lishi kerak.");
            }

            if (expectedVersion is not null && definition.Version != expectedVersion)
            {
                Add("$.version", $"Versiya «{expectedVersion}» bo'lishi kerak.");
            }

            if (!Subjects.Contains(definition.Subject, StringComparer.Ordinal))
            {
                Add("$.subject", "employee, candidate yoki none.");
            }

            if (!LegalStatuses.Contains(definition.LegalStatus, StringComparer.Ordinal))
            {
                Add("$.legalStatus", "final yoki draft.");
            }

            if (definition.DueDateField is { } due)
            {
                RequireDateField(due, "$.dueDate.field");
            }

            if (definition.Sections.Count == 0)
            {
                Add("$.sections", "Kamida bitta bo'lim.");
            }

            if (definition.Steps.Count > MaxSteps)
            {
                Add("$.steps", $"Ko'pi bilan {MaxSteps} qadam.");
            }

            var total = definition.Fields.Sum(field => 1 + field.Columns.Count);

            if (total > MaxFields)
            {
                Add("$.sections", $"Ko'pi bilan {MaxFields} maydon (hozir {total}).");
            }
        }

        /// <summary>Birinchi o'tish: id shakli va takrorlar.</summary>
        private void CheckIds()
        {
            Unique(definition.Sections.Select((section, i) => (section.Id, $"sections[{i}].id")));
            Unique(definition.Steps.Select((step, i) => (step.Id, $"steps[{i}].id")));
            Unique(definition.Sections.SelectMany((section, s) => section.Fields.Select((field, f) => (field.Id, $"sections[{s}].fields[{f}].id"))));
        }

        private void Unique(IEnumerable<(string Id, string Path)> items)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (id, path) in items)
            {
                if (!IdPattern().IsMatch(id))
                {
                    Add(path, "Id shakli: ^[a-z][A-Za-z0-9]{0,47}$.");
                }
                else if (!seen.Add(id))
                {
                    Add(path, $"Takroriy id: {id}.");
                }
            }
        }

        private void CheckSection(BlankSection section, string path)
        {
            if (section.Fill.At == BlankFillAt.Step && section.Fill.Step is { } stepId && definition.FindStep(stepId) is null)
            {
                Add($"{path}.fill.step", $"Noma'lum qadam: {stepId}.");
            }

            if (section.When is not null)
            {
                CheckCondition(section.When, $"{path}.when", null);
            }

            if (section.Fields.Count == 0)
            {
                Add($"{path}.fields", "Kamida bitta maydon.");
            }

            for (var f = 0; f < section.Fields.Count; f++)
            {
                CheckField(section.Fields[f], $"{path}.fields[{f}]", null);
            }
        }

        private void CheckField(BlankField field, string path, BlankField? table)
        {
            if (field.Label is null && field.Type is not (BlankFieldType.Static or BlankFieldType.Table))
            {
                Add($"{path}.label", "Majburiy kalit yo'q.");
            }

            if (field.When is not null)
            {
                CheckCondition(field.When, $"{path}.when", table);
            }

            if (field.Prefill is { } prefill && !BlankPrefillKeys.All.Contains(prefill))
            {
                Add($"{path}.prefill", $"Noma'lum manba: {prefill}.");
            }

            if (field.PdfWidth is <= 0)
            {
                Add($"{path}.pdfWidth", "Musbat son.");
            }

            if (field.Required && field.Type is BlankFieldType.Static or BlankFieldType.Computed)
            {
                Add($"{path}.required", "Bu turda majburiylik bo'lmaydi.");
            }

            switch (field.Type)
            {
                case BlankFieldType.Text:
                    Range(field.MaxLength, 1, MaxTextLength, $"{path}.maxLength");
                    break;
                case BlankFieldType.LongText:
                    Range(field.MaxLength, 1, MaxLongTextLength, $"{path}.maxLength");
                    break;
                case BlankFieldType.WholeNumber:
                    NumberBounds(field, path, decimal.MinValue, decimal.MaxValue);
                    break;
                case BlankFieldType.Money:
                    NumberBounds(field, path, 0, MaxMoney);
                    break;
                case BlankFieldType.Year:
                    NumberBounds(field, path, 1900, 2100);
                    break;
                case BlankFieldType.Date or BlankFieldType.DateTime or BlankFieldType.Month:
                    DateBound(field.Min, $"{path}.min");
                    DateBound(field.Max, $"{path}.max");
                    break;
                case BlankFieldType.Percent when field.PassAt is < 0 or > 100:
                    Add($"{path}.passAt", "0–100.");
                    break;
                case BlankFieldType.Choice or BlankFieldType.Multi:
                    CheckOptions(field.Options, path);
                    Range(field.MaxSelected, 1, Math.Max(1, field.Options.Count), $"{path}.maxSelected");
                    break;
                case BlankFieldType.Ref when !RefKinds.Contains(field.RefKind, StringComparer.Ordinal):
                    Add($"{path}.refKind", "employee, candidate, department, position yoki direction.");
                    break;
                case BlankFieldType.DocRef when field.TemplateCodes.Count == 0:
                    Add($"{path}.templateCodes", "Kamida bitta shablon kodi.");
                    break;
                case BlankFieldType.Static:
                    CheckStatic(field, path);
                    break;
                case BlankFieldType.File:
                    CheckFile(field, path);
                    break;
                case BlankFieldType.Table:
                    CheckTable(field, path, table);
                    break;
                case BlankFieldType.Computed:
                    if (!ValueTypes.Contains(field.ValueType, StringComparer.Ordinal))
                    {
                        Add($"{path}.valueType", "number, date, bool yoki text.");
                    }

                    if (field.Expr is null)
                    {
                        Add($"{path}.expr", "Majburiy kalit yo'q.");
                    }
                    else
                    {
                        CheckExpr(field.Expr, $"{path}.expr", table);
                    }

                    if (field.Prefill is not null)
                    {
                        Add($"{path}.prefill", "Hisoblanadigan maydon oldindan to'ldirilmaydi.");
                    }

                    break;
            }
        }

        private void Range(int? value, int min, int max, string path)
        {
            if (value is { } number && (number < min || number > max))
            {
                Add(path, $"{min}–{max} oralig'ida.");
            }
        }

        private void NumberBounds(BlankField field, string path, decimal floor, decimal ceiling)
        {
            foreach (var (bound, key) in new[] { (field.Min, "min"), (field.Max, "max") })
            {
                if (bound is null)
                {
                    continue;
                }

                if (bound.Number is not { } number)
                {
                    Add($"{path}.{key}", "Son kutilgan.");
                }
                else if (number < floor || number > ceiling)
                {
                    Add($"{path}.{key}", "Ruxsat etilgan chegaradan tashqarida.");
                }
            }

            if (field.Min?.Number is { } min && field.Max?.Number is { } max && min > max)
            {
                Add($"{path}.min", "min > max.");
            }
        }

        private void DateBound(BlankBound? bound, string path)
        {
            if (bound is not null && bound.ResolveDate(new DateOnly(2000, 1, 1)) is null)
            {
                Add(path, "today, today+N, today-N yoki yyyy-MM-dd.");
            }
        }

        private void CheckOptions(IReadOnlyList<BlankOption> options, string path)
        {
            if (options.Count == 0)
            {
                Add($"{path}.options", "Kamida bitta variant.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var optionPath = $"{path}.options[{i}]";

                if (!OptionValuePattern().IsMatch(option.Value))
                {
                    Add($"{optionPath}.value", "Qiymat shakli: ^[a-z0-9_]{1,32}$.");
                }
                else if (!seen.Add(option.Value))
                {
                    Add($"{optionPath}.value", $"Takroriy qiymat: {option.Value}.");
                }

                if (option.Extra is not { } extra)
                {
                    continue;
                }

                if (!ExtraTypes.Contains(extra.Type))
                {
                    Add($"{optionPath}.extra.type", "text, longtext, date, int, ref yoki docRef.");
                }

                if ((extra.Type == BlankFieldType.Ref) != (extra.RefKind is not null)
                    || (extra.RefKind is not null && !RefKinds.Contains(extra.RefKind, StringComparer.Ordinal)))
                {
                    Add($"{optionPath}.extra.refKind", "refKind faqat ref uchun va majburiy.");
                }

                if ((extra.Type == BlankFieldType.DocRef) != (extra.TemplateCodes.Count > 0))
                {
                    Add($"{optionPath}.extra.templateCodes", "templateCodes faqat docRef uchun va majburiy.");
                }
            }
        }

        private void CheckStatic(BlankField field, string path)
        {
            if (string.IsNullOrWhiteSpace(field.Text))
            {
                Add($"{path}.text", "Majburiy kalit yo'q.");
                return;
            }

            foreach (Match match in PlaceholderPattern().Matches(field.Text))
            {
                if (definition.FindField(match.Groups[1].Value) is null)
                {
                    Add($"{path}.text", $"Noma'lum o'rinbosar: {match.Value}.");
                }
            }
        }

        private void CheckFile(BlankField field, string path)
        {
            if (field.Accept.Count == 0 || field.Accept.Any(kind => !FileAccepts.Contains(kind, StringComparer.Ordinal)))
            {
                Add($"{path}.accept", "pdf, image, office, any dan kamida bittasi.");
            }

            Range(field.MaxFiles, 1, 20, $"{path}.maxFiles");
            Range(field.MaxSizeMb, 1, 20, $"{path}.maxSizeMb");

            if (field.Capture is { } capture && !Captures.Contains(capture, StringComparer.Ordinal))
            {
                Add($"{path}.capture", "environment yoki user.");
            }
        }

        private void CheckTable(BlankField field, string path, BlankField? parent)
        {
            if (parent is not null)
            {
                Add($"{path}.type", "Jadval ichida jadval bo'lmaydi.");
                return;
            }

            if (field.Columns.Count == 0)
            {
                Add($"{path}.columns", "Kamida bitta ustun.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var c = 0; c < field.Columns.Count; c++)
            {
                var column = field.Columns[c];
                var columnPath = $"{path}.columns[{c}]";

                if (!IdPattern().IsMatch(column.Id))
                {
                    Add($"{columnPath}.id", "Id shakli: ^[a-z][A-Za-z0-9]{0,47}$.");
                }
                else if (!seen.Add(column.Id))
                {
                    Add($"{columnPath}.id", $"Takroriy ustun: {column.Id}.");
                }

                if (column.Type == BlankFieldType.Static)
                {
                    Add($"{columnPath}.type", "Ustun static bo'lmaydi.");
                }

                CheckField(column, columnPath, field);
            }

            Range(field.MinRows, 0, MaxTableRows, $"{path}.minRows");
            Range(field.MaxRows, 1, MaxTableRows, $"{path}.maxRows");

            if (field.MinRows is { } min && field.MaxRows is { } max && min > max)
            {
                Add($"{path}.minRows", "minRows > maxRows.");
            }

            if (field.FixedRows.Count == 0)
            {
                return;
            }

            if (field.MinRows is not null || field.MaxRows is not null)
            {
                Add($"{path}.fixedRows", "fixedRows bilan minRows / maxRows berilmaydi.");
            }

            Range(field.FixedRows.Count, 1, MaxTableRows, $"{path}.fixedRows");
            var rowIds = new HashSet<string>(StringComparer.Ordinal);

            for (var r = 0; r < field.FixedRows.Count; r++)
            {
                var row = field.FixedRows[r];
                var rowPath = $"{path}.fixedRows[{r}]";

                if (!IdPattern().IsMatch(row.Id))
                {
                    Add($"{rowPath}.id", "Id shakli: ^[a-z][A-Za-z0-9]{0,47}$.");
                }
                else if (!rowIds.Add(row.Id))
                {
                    Add($"{rowPath}.id", $"Takroriy qator: {row.Id}.");
                }

                foreach (var columnId in row.Cells.Keys.Where(columnId => field.FindColumn(columnId) is null))
                {
                    Add($"{rowPath}.cells.{columnId}", "Noma'lum ustun.");
                }
            }
        }

        private void CheckStep(BlankStep step, string path)
        {
            if (step.Order < 1)
            {
                Add($"{path}.order", "Kamida 1.");
            }

            if (step.ActorField is { } actorField)
            {
                if (definition.FindField(actorField) is not { Type: BlankFieldType.Ref, RefKind: "employee" })
                {
                    Add($"{path}.actor", $"«{actorField}» — xodimga havola (ref employee) maydoni emas.");
                }
            }
            else if (!BlankActors.OfficeRoles.Contains(step.Actor) && !BlankActors.PersonRoles.Contains(step.Actor))
            {
                Add($"{path}.actor", $"Noma'lum rol: {step.Actor}.");
            }

            if (step.When is not null)
            {
                CheckCondition(step.When, $"{path}.when", null);
            }

            if (step.Decision is { } decision)
            {
                CheckOptions(decision.Options, $"{path}.decision");
            }

            if (step.Due is { } due)
            {
                RequireDateField(due.Field, $"{path}.due.field");

                if (due.Days is < -3650 or > 3650)
                {
                    Add($"{path}.due.days", "-3650–3650.");
                }
            }
        }

        private void RequireDateField(string id, string path)
        {
            if (definition.FindField(id) is not { Type: BlankFieldType.Date })
            {
                Add(path, $"«{id}» — sana (date) maydoni emas.");
            }
        }

        private void CheckCondition(BlankCondition condition, string path, BlankField? table)
        {
            foreach (var id in BlankConditions.ReferencedFields(condition).Distinct(StringComparer.Ordinal))
            {
                var target = table?.FindColumn(id) ?? definition.FindField(id);

                if (target is null)
                {
                    Add(path, $"Noma'lum maydon: {id}.");
                }
                else if (target.Type == BlankFieldType.Static)
                {
                    Add(path, $"«{id}» qiymatsiz (static).");
                }
            }
        }

        private void CheckExpr(BlankExpr expr, string path, BlankField? table)
        {
            foreach (var reference in BlankComputedEvaluator.References(expr))
            {
                if (table?.FindColumn(reference.Path) is { } sibling)
                {
                    if (sibling.Type == BlankFieldType.Static)
                    {
                        Add(path, $"«{reference.Path}» qiymatsiz (static).");
                    }

                    continue;
                }

                var field = definition.FindField(reference.Head);

                if (field is null || field.Type == BlankFieldType.Static)
                {
                    Add(path, $"Noma'lum havola: {reference.Path}.");
                }
                else if (reference.IsColumnSet && (field.Type != BlankFieldType.Table || field.FindColumn(reference.Column!) is null))
                {
                    Add(path, $"Noma'lum jadval ustuni: {reference.Path}.");
                }
            }

            CheckBands(expr, path);
        }

        private void CheckBands(BlankExpr expr, string path)
        {
            switch (expr)
            {
                case BlankBandExpr band:
                    for (var i = 0; i < band.Bands.Count; i++)
                    {
                        var from = band.Bands[i].From;
                        var isLast = i == band.Bands.Count - 1;

                        if ((from is null && !isLast)
                            || (i > 0 && from is { } current && band.Bands[i - 1].From is { } previous && current >= previous))
                        {
                            Add($"{path}.bands[{i}]", "Bandlar kamayish tartibida, null faqat oxirida.");
                        }
                    }

                    CheckBands(band.Value, path);
                    break;
                case BlankOpExpr op:
                    foreach (var arg in op.Args)
                    {
                        CheckBands(arg, path);
                    }

                    break;
            }
        }

        // ── Tsikllar: computed va shart bog'liqliklari bitta grafda ──

        /// <summary>
        /// Tugun: yuqori maydon — <c>id</c>, jadval ustuni — <c>table.col</c>. Qirra: maydon o'z bo'limi
        /// shartiga, o'z shartiga va (computed bo'lsa) ifodasiga murojaat qilgan maydonlarga; ustun esa
        /// yana o'z jadvaliga.
        /// </summary>
        private void CheckCycles()
        {
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var field in definition.Fields)
            {
                var deps = new List<string>();
                var section = definition.SectionOf(field.Id)!;

                AddConditionDeps(deps, section.When, null);
                AddConditionDeps(deps, field.When, null);
                AddExprDeps(deps, field.Expr, null);
                graph[field.Id] = deps;

                foreach (var column in field.Columns)
                {
                    var columnDeps = new List<string> { field.Id };
                    AddConditionDeps(columnDeps, column.When, field);
                    AddExprDeps(columnDeps, column.Expr, field);
                    graph[$"{field.Id}.{column.Id}"] = columnDeps;
                }
            }

            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new List<string>();

            foreach (var node in graph.Keys)
            {
                Visit(node, graph, state, stack);
            }
        }

        private void Visit(string node, Dictionary<string, List<string>> graph, Dictionary<string, int> state, List<string> stack)
        {
            // 0 — ko'rilmagan, 1 — yo'lda, 2 — tugagan
            if (state.GetValueOrDefault(node) == 2)
            {
                return;
            }

            if (state.GetValueOrDefault(node) == 1)
            {
                var cycle = stack.Skip(stack.IndexOf(node)).Append(node);
                Add("$", $"Tsikl: {string.Join(" → ", cycle)}.");
                return;
            }

            state[node] = 1;
            stack.Add(node);

            foreach (var next in graph.GetValueOrDefault(node) ?? [])
            {
                if (graph.ContainsKey(next))
                {
                    Visit(next, graph, state, stack);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }

        private static void AddConditionDeps(List<string> deps, BlankCondition? condition, BlankField? table)
        {
            if (condition is null)
            {
                return;
            }

            deps.AddRange(BlankConditions.ReferencedFields(condition)
                .Select(id => table?.FindColumn(id) is not null ? $"{table.Id}.{id}" : id));
        }

        private static void AddExprDeps(List<string> deps, BlankExpr? expr, BlankField? table)
        {
            if (expr is null)
            {
                return;
            }

            foreach (var reference in BlankComputedEvaluator.References(expr))
            {
                if (table?.FindColumn(reference.Path) is not null)
                {
                    deps.Add($"{table.Id}.{reference.Path}");
                }
                else if (reference.IsColumnSet)
                {
                    deps.Add(reference.Head);
                    deps.Add($"{reference.Head}.{reference.Column}");
                }
                else
                {
                    deps.Add(reference.Path);
                }
            }
        }
    }
}
