using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>
/// <c>when</c> sharti (§4.4) — JSON daraxt, matn sifatida hech qachon bajarilmaydi.
/// </summary>
public abstract record BlankCondition;

/// <summary><c>{"field":"f","eq":v}</c> — choice uchun <c>.value</c>, multi uchun «tanlanganlar orasida bor».</summary>
public sealed record BlankEqCondition(string Field, JsonNode? Value) : BlankCondition;

/// <summary><c>{"field":"f","in":[…]}</c>.</summary>
public sealed record BlankInCondition(string Field, IReadOnlyList<JsonNode?> Values) : BlankCondition;

/// <summary><c>{"field":"f","filled":true|false}</c>.</summary>
public sealed record BlankFilledCondition(string Field, bool Filled) : BlankCondition;

/// <summary><c>{"all":[…]}</c>.</summary>
public sealed record BlankAllCondition(IReadOnlyList<BlankCondition> Items) : BlankCondition;

/// <summary><c>{"any":[…]}</c>.</summary>
public sealed record BlankAnyCondition(IReadOnlyList<BlankCondition> Items) : BlankCondition;

/// <summary><c>{"not":{…}}</c>.</summary>
public sealed record BlankNotCondition(BlankCondition Inner) : BlankCondition;

/// <summary>
/// <c>when</c> shartini baholaydi (§4.4).
/// </summary>
/// <remarks>
/// <para>
/// Baholash TURSIZ (<see cref="BlankValues"/>): maydon turini bilmaydi, faqat JSON qiymatni ko'radi —
/// veb ham aynan shunday qiladi va ikkalasi bitta vektor faylidan o'tadi
/// (<c>computed-vectors.json</c>). Choice obyektidan <c>value</c>, ref obyektidan <c>id</c> olinadi;
/// multi massivida <c>eq</c> / <c>in</c> «biror tanlangani mos» degani.
/// </para>
/// <para>
/// Bo'sh qiymat (<c>null</c>, <c>""</c>, <c>[]</c>) <c>eq</c> / <c>in</c> da hech narsaga teng emas.
/// <c>filled</c> faqat bo'shlikka qaraydi: <c>false</c> mantiqiy qiymat ham «to'ldirilgan».
/// </para>
/// </remarks>
public static class BlankConditions
{
    /// <summary>Shartni javoblar ustida baholaydi.</summary>
    public static bool Evaluate(BlankCondition condition, JsonObject answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        return Evaluate(condition, id => answers[id]);
    }

    /// <summary>Shartni baholaydi; <paramref name="lookup"/> maydon id'si bo'yicha saqlangan qiymatni beradi.</summary>
    public static bool Evaluate(BlankCondition condition, Func<string, JsonNode?> lookup)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(lookup);

        return condition switch
        {
            BlankEqCondition eq => Matches(BlankValues.FromNode(lookup(eq.Field)), BlankValues.FromNode(eq.Value)),
            BlankInCondition @in => @in.Values.Any(value => Matches(BlankValues.FromNode(lookup(@in.Field)), BlankValues.FromNode(value))),
            BlankFilledCondition filled => !BlankValues.IsEmpty(lookup(filled.Field)) == filled.Filled,
            BlankAllCondition all => all.Items.All(item => Evaluate(item, lookup)),
            BlankAnyCondition any => any.Items.Any(item => Evaluate(item, lookup)),
            BlankNotCondition not => !Evaluate(not.Inner, lookup),
            _ => throw new InvalidOperationException($"Noma'lum shart: {condition.GetType().Name}"),
        };
    }

    /// <summary>
    /// Qadam sharti hozir bajariladimi — qadam guruhi navbatga kelganda chaqiriladi (dangasa, orkestrator
    /// qarori 2026-11-03): masalan HR-F13 guvohlar qadami tushuntirish xati shu orada to'ldirilgan bo'lsa
    /// o'tkazib yuboriladi. Shartsiz qadam → <c>true</c>.
    /// </summary>
    /// <param name="definition">Ta'rif.</param>
    /// <param name="answers">Saqlangan (tozalangan) javoblar.</param>
    /// <param name="computed">Saqlangan computed qiymatlar (<see cref="BlankComputedEvaluator.ComputeAll"/>).</param>
    /// <param name="stepKey">Qadam id'si.</param>
    public static bool EvaluateStepCondition(BlankDefinition definition, JsonObject answers, JsonObject? computed, string stepKey)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);

        var step = definition.FindStep(stepKey)
            ?? throw new ArgumentException($"Ta'rifda «{stepKey}» qadami yo'q.", nameof(stepKey));

        if (step.When is null)
        {
            return true;
        }

        return Evaluate(step.When, id => definition.FindField(id)?.Type == BlankFieldType.Computed ? computed?[id] : answers[id]);
    }

    /// <summary>JSON shartni o'qiydi; noto'g'ri bo'lsa <see cref="FormatException"/> (testlar va vektorlar uchun).</summary>
    public static BlankCondition Parse(JsonNode? node)
    {
        var reader = new BlankDefinitionReader();
        var condition = reader.ReadCondition(node, "when");

        return reader.Errors.Count == 0 && condition is not null
            ? condition
            : throw new FormatException(string.Join("; ", reader.Errors.Select(error => $"{error.Path}: {error.Message}")));
    }

    /// <summary>Shart murojaat qiladigan maydon id'lari (ichma-ich hammasi).</summary>
    public static IEnumerable<string> ReferencedFields(BlankCondition condition) => condition switch
    {
        BlankEqCondition eq => [eq.Field],
        BlankInCondition @in => [@in.Field],
        BlankFilledCondition filled => [filled.Field],
        BlankAllCondition all => all.Items.SelectMany(ReferencedFields),
        BlankAnyCondition any => any.Items.SelectMany(ReferencedFields),
        BlankNotCondition not => ReferencedFields(not.Inner),
        _ => [],
    };

    private static bool Matches(object? actual, object? expected) => actual switch
    {
        null => false,
        List<object?> selected => selected.Any(item => BlankValues.AreEqual(item, expected)),
        _ => BlankValues.AreEqual(actual, expected),
    };
}
