using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>Hisoblash ifodasi (§4.5) — JSON daraxt, matn sifatida bajarilmaydi.</summary>
public abstract record BlankExpr;

/// <summary><c>{"ref":"fieldId"}</c> yoki <c>{"ref":"table[*].col"}</c> (massiv).</summary>
public sealed record BlankRefExpr(string Path) : BlankExpr
{
    /// <summary><c>table[*].col</c> shaklidami.</summary>
    public bool IsColumnSet => Path.Contains("[*].", StringComparison.Ordinal);

    /// <summary><c>table[*].col</c> → <c>table</c>; oddiy havolada butun yo'l.</summary>
    public string Head => IsColumnSet ? Path[..Path.IndexOf("[*].", StringComparison.Ordinal)] : Path;

    /// <summary><c>table[*].col</c> → <c>col</c>; oddiy havolada <c>null</c>.</summary>
    public string? Column => IsColumnSet ? Path[(Path.IndexOf("[*].", StringComparison.Ordinal) + 4)..] : null;
}

/// <summary><c>{"const":x}</c>.</summary>
public sealed record BlankConstExpr(JsonNode? Value) : BlankExpr;

/// <summary><c>{"op":"…","args":[…]}</c>.</summary>
public sealed record BlankOpExpr(string Op, IReadOnlyList<BlankExpr> Args) : BlankExpr;

/// <summary><c>{"op":"band","value":x,"bands":[…]}</c> — birinchi <c>value ≥ from</c> bandning yorlig'i.</summary>
public sealed record BlankBandExpr(BlankExpr Value, IReadOnlyList<BlankBand> Bands) : BlankExpr;

/// <summary>Band: <c>from = null</c> — qolgan hamma qiymat.</summary>
public sealed record BlankBand(decimal? From, string Label);

/// <summary>
/// Hisoblanadigan maydonlarni baholaydi (§4.5). Server har saqlashda hisoblaydi; veb (TS) ham xuddi shu
/// vektorlardan o'tadi (<c>computed-vectors.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// Qoidalar: agregatlar (avg/sum/count/min/max) bo'sh va son bo'lmagan qiymatni TASHLAYDI; hech narsa
/// qolmasa avg/min/max → <c>null</c>, sum/count → 0. Qolgan amallarda biror kirish <c>null</c> (yoki
/// noto'g'ri turda) bo'lsa natija <c>null</c>. Nolga bo'lish → <c>null</c>. Sana — <c>yyyy-MM-dd</c> matn.
/// </para>
/// <para>
/// Ish kuni — dushanba–juma, bayramlarsiz (ASSUMPTIONS 2026-11-03). <c>round</c> — yarmi noldan uzoqqa.
/// </para>
/// </remarks>
public static class BlankComputedEvaluator
{
    /// <summary>Ma'lum amallar va argumentlar soni (<c>-1</c> — kamida bitta, <c>-2</c> — kamida ikkita).</summary>
    internal static IReadOnlyDictionary<string, int> Arity { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["avg"] = -1, ["sum"] = -1, ["count"] = -1, ["min"] = -1, ["max"] = -1,
        ["add"] = 2, ["sub"] = 2, ["mul"] = 2, ["div"] = 2, ["round"] = 2,
        ["addDays"] = 2, ["addMonths"] = 2, ["addWorkingDays"] = 2, ["diffDays"] = 2, ["diffWorkingDays"] = 2,
        ["gte"] = 2, ["gt"] = 2, ["lte"] = 2, ["lt"] = 2, ["eq"] = 2,
        ["and"] = -2, ["or"] = -2, ["not"] = 1, ["if"] = 3,
    };

    /// <summary>Ifodani baholaydi.</summary>
    /// <param name="expr">Ifoda.</param>
    /// <param name="resolve">
    /// Havola qiymati: <c>"fieldId"</c> → saqlangan qiymat, <c>"table[*].col"</c> → ustun qiymatlari massivi.
    /// </param>
    public static JsonNode? Evaluate(BlankExpr expr, Func<string, JsonNode?> resolve)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(resolve);

        return BlankValues.ToNode(Eval(expr, path => BlankValues.FromNode(resolve(path))));
    }

    /// <summary>Ifodani oddiy javoblar obyekti ustida baholaydi (<c>table[*].col</c> qatorlardan yig'iladi).</summary>
    public static JsonNode? Evaluate(BlankExpr expr, JsonObject answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        return Evaluate(expr, path => ResolvePlain(answers, path));
    }

    /// <summary>JSON ifodani o'qiydi; noto'g'ri bo'lsa <see cref="FormatException"/>.</summary>
    public static BlankExpr Parse(JsonNode? node)
    {
        var reader = new BlankDefinitionReader();
        var expr = reader.ReadExpr(node, "expr");

        return reader.Errors.Count == 0 && expr is not null
            ? expr
            : throw new FormatException(string.Join("; ", reader.Errors.Select(error => $"{error.Path}: {error.Message}")));
    }

    /// <summary>
    /// Ta'rifdagi KO'RINADIGAN hamma hisoblanadigan maydonni hisoblaydi. Natija kalitlari — yo'llar:
    /// <c>avgScore</c>, jadval ustuni uchun <c>tests[0].pass</c>. <c>null</c> natija yozilmaydi.
    /// </summary>
    /// <param name="definition">Ta'rif.</param>
    /// <param name="answers">TOZALANGAN javoblar (yashirin maydonlar allaqachon tashlangan).</param>
    public static JsonObject ComputeAll(BlankDefinition definition, JsonObject answers)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);

        return new BlankScope(definition, answers).ComputeAll();
    }

    /// <summary>Ifoda murojaat qiladigan havolalar (ichma-ich hammasi).</summary>
    public static IEnumerable<BlankRefExpr> References(BlankExpr expr) => expr switch
    {
        BlankRefExpr reference => [reference],
        BlankOpExpr op => op.Args.SelectMany(References),
        BlankBandExpr band => References(band.Value),
        _ => [],
    };

    internal static object? Eval(BlankExpr expr, Func<string, object?> resolve) => expr switch
    {
        BlankRefExpr reference => resolve(reference.Path),
        BlankConstExpr constant => BlankValues.FromNode(constant.Value),
        BlankBandExpr band => Band(Eval(band.Value, resolve), band.Bands),
        BlankOpExpr op => Apply(op.Op, [.. op.Args.Select(arg => Eval(arg, resolve))]),
        _ => null,
    };

    private static JsonNode? ResolvePlain(JsonObject answers, string path)
    {
        var reference = new BlankRefExpr(path);

        if (!reference.IsColumnSet)
        {
            return answers[path]?.DeepClone();
        }

        if (answers[reference.Head] is not JsonArray rows)
        {
            return new JsonArray();
        }

        return new JsonArray([.. rows.Select(row => row is JsonObject cells ? cells[reference.Column!]?.DeepClone() : null)]);
    }

    private static string? Band(object? value, IReadOnlyList<BlankBand> bands)
    {
        if (value is not decimal number)
        {
            return null;
        }

        return bands.FirstOrDefault(band => band.From is not { } from || number >= from)?.Label;
    }

    private static object? Apply(string op, object?[] args)
    {
        try
        {
            return op switch
            {
                "avg" or "sum" or "count" or "min" or "max" => Aggregate(op, args),
                "add" => Numbers(args, (a, b) => a + b),
                "sub" => Numbers(args, (a, b) => a - b),
                "mul" => Numbers(args, (a, b) => a * b),
                "div" => Numbers(args, (a, b) => b == 0 ? null : a / b),
                "round" => Numbers(args, (a, b) => b == decimal.Truncate(b) && b is >= 0 and <= 10
                    ? Math.Round(a, (int)b, MidpointRounding.AwayFromZero)
                    : null),
                "addDays" => DateAndCount(args, (date, n) => date.AddDays(n)),
                "addMonths" => DateAndCount(args, (date, n) => date.AddMonths(n)),
                "addWorkingDays" => DateAndCount(args, AddWorkingDays),
                "diffDays" => TwoDates(args, (from, to) => to.DayNumber - from.DayNumber),
                "diffWorkingDays" => TwoDates(args, DiffWorkingDays),
                "gte" => Compare(args, sign => sign >= 0),
                "gt" => Compare(args, sign => sign > 0),
                "lte" => Compare(args, sign => sign <= 0),
                "lt" => Compare(args, sign => sign < 0),
                "eq" => args[0] is null || args[1] is null ? null : BlankValues.AreEqual(args[0], args[1]),
                "and" => args.All(arg => arg is bool) ? args.All(arg => (bool)arg!) : null,
                "or" => args.All(arg => arg is bool) ? args.Any(arg => (bool)arg!) : null,
                "not" => args[0] is bool flag ? !flag : null,
                "if" => args[0] is bool condition ? (condition ? args[1] : args[2]) : null,
                _ => null,
            };
        }
        catch (Exception error) when (error is OverflowException or ArgumentOutOfRangeException)
        {
            // Juda katta son yoki sana chegarasidan chiqish — hisoblab bo'lmaydi, maydon «—» bo'ladi
            return null;
        }
    }

    private static object? Aggregate(string op, object?[] args)
    {
        var numbers = Flatten(args).OfType<decimal>().ToList();

        return op switch
        {
            "count" => (decimal)Flatten(args).Count(value => value is not null),
            "sum" => numbers.Sum(),
            _ when numbers.Count == 0 => null,
            "avg" => numbers.Sum() / numbers.Count,
            "min" => numbers.Min(),
            _ => numbers.Max(),
        };
    }

    private static IEnumerable<object?> Flatten(IEnumerable<object?> values)
    {
        foreach (var value in values)
        {
            if (value is List<object?> list)
            {
                foreach (var item in Flatten(list))
                {
                    yield return item;
                }
            }
            else
            {
                yield return value;
            }
        }
    }

    private static decimal? Numbers(object?[] args, Func<decimal, decimal, decimal?> apply)
        => args[0] is decimal a && args[1] is decimal b ? apply(a, b) : null;

    private static string? DateAndCount(object?[] args, Func<DateOnly, int, DateOnly> apply)
    {
        if (!BlankValues.TryDate(args[0], out var date) || args[1] is not decimal n || n != decimal.Truncate(n))
        {
            return null;
        }

        return BlankValues.FormatDate(apply(date, (int)n));
    }

    private static decimal? TwoDates(object?[] args, Func<DateOnly, DateOnly, int> apply)
        => BlankValues.TryDate(args[0], out var from) && BlankValues.TryDate(args[1], out var to)
            ? (decimal)apply(from, to)
            : null;

    private static bool? Compare(object?[] args, Func<int, bool> test)
    {
        if (args[0] is decimal a && args[1] is decimal b)
        {
            return test(a.CompareTo(b));
        }

        if (BlankValues.TryDate(args[0], out var left) && BlankValues.TryDate(args[1], out var right))
        {
            return test(left.CompareTo(right));
        }

        return null;
    }

    private static bool IsWorkingDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>n ish kuni oldinga (manfiy — orqaga); n = 0 → o'sha sana.</summary>
    private static DateOnly AddWorkingDays(DateOnly date, int n)
    {
        var step = n < 0 ? -1 : 1;

        for (var left = Math.Abs(n); left > 0;)
        {
            date = date.AddDays(step);

            if (IsWorkingDay(date))
            {
                left--;
            }
        }

        return date;
    }

    /// <summary>(from; to] oralig'idagi ish kunlari; to &lt; from bo'lsa manfiy.</summary>
    private static int DiffWorkingDays(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            return -DiffWorkingDays(to, from);
        }

        var count = 0;

        for (var day = from.AddDays(1); day <= to; day = day.AddDays(1))
        {
            if (IsWorkingDay(day))
            {
                count++;
            }
        }

        return count;
    }
}
