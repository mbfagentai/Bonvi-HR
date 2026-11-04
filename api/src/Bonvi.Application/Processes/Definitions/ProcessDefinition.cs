using System.Globalization;

namespace Bonvi.Application.Processes.Definitions;

/// <summary>
/// One process template (SPEC-mening-ishlarim §3.1, <c>schema: 1</c>) — read by
/// <see cref="ProcessDefinitionValidator"/> from <c>Processes/Templates/&lt;code&gt;.json</c>.
/// </summary>
/// <param name="Schema">Template format, always <see cref="CurrentSchema"/>.</param>
/// <param name="Code">Process code = file name, e.g. <c>hire-request</c>.</param>
/// <param name="Version">«1.0»; a changed file needs a new version (seeder rule).</param>
/// <param name="Title">UI title.</param>
/// <param name="Source">Regulation the steps come from, e.g. «HR-R02».</param>
/// <param name="SubjectKind">One of <see cref="SubjectKinds"/>.</param>
/// <param name="Board">Board columns, left to right.</param>
/// <param name="Steps">Steps in display order (<c>order_no</c> = index + 1).</param>
public sealed record ProcessDefinition(
    int Schema,
    string Code,
    string Version,
    string Title,
    string Source,
    string SubjectKind,
    IReadOnlyList<ProcessBoardColumn> Board,
    IReadOnlyList<ProcessStep> Steps)
{
    public const int CurrentSchema = 1;

    public static IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "vacancyRequest", "candidate", "employment" };

    public ProcessStep? FindStep(string id) => Steps.FirstOrDefault(step => step.Id == id);
}

public sealed record ProcessBoardColumn(string Key, string Title);

public enum ProcessStepKind
{
    /// <summary>Closes when its completion fact holds (or by «Bajarildi» when <c>manual.allowed</c>).</summary>
    Fact = 1,

    /// <summary>Closes only by a user «Bajarildi».</summary>
    Manual = 2,

    /// <summary>Done by the system from a fact; never a task, no due, no gate.</summary>
    Auto = 3,
}

public sealed record ProcessStep
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Board column key.</summary>
    public required string Column { get; init; }

    /// <summary><see cref="ProcessRoles"/> code.</summary>
    public required string Role { get; init; }

    /// <summary>Holds the step when <see cref="Role"/> is a person role that resolves to no user.</summary>
    public string? FallbackRole { get; init; }

    /// <summary><c>HR-F01</c> … <c>HR-F19</c>, or null when the step works on an existing record.</summary>
    public string? Blank { get; init; }

    /// <summary><c>D1</c> / <c>D2</c> / <c>D3</c> (regulation signature level), or null.</summary>
    public string? Signature { get; init; }

    public required ProcessStepKind Kind { get; init; }

    public IReadOnlyList<string> Prereqs { get; init; } = [];

    /// <summary>Never locks later steps; Skipped (<c>caseEnded</c>) when the case ends.</summary>
    public bool Optional { get; init; }

    /// <summary>False → Skipped (<c>condition</c>); fact unknown → step stays Locked.</summary>
    public ProcessCondition? When { get; init; }

    public ProcessDueRule? Due { get; init; }

    /// <summary>
    /// The step is Skipped (<c>condition</c>) when its due falls on or after this date — the probation rule
    /// «a check-in due within the last five days is not held» (§3.6).
    /// </summary>
    public ProcessDueRule? SkipIfDueOnOrAfter { get; init; }

    /// <summary>Usually zero or one; r02-13 has two (Q01 and Q04).</summary>
    public IReadOnlyList<ProcessGateRef> Gates { get; init; } = [];

    public ProcessCompletion? Completion { get; init; }

    public ProcessManual? Manual { get; init; }

    /// <summary>The holder may mark the step «Kerak emas».</summary>
    public bool Skippable { get; init; }

    /// <summary>In a legacy case the holder may skip it with one click (§7.6).</summary>
    public bool LegacySkippable { get; init; }

    /// <summary>«Nima qilaman?» lines.</summary>
    public IReadOnlyList<string> Guide { get; init; } = [];

    public string? SourceRef { get; init; }

    public required ProcessLink Link { get; init; }

    /// <summary>A fact step whose manual fallback is on — closes on whichever comes first.</summary>
    public bool HasManualFallback => Kind == ProcessStepKind.Fact && Manual is { Allowed: true };
}

/// <summary><c>when</c>: the step applies only while <see cref="Fact"/> has the value <see cref="EqualsValue"/>.</summary>
public sealed record ProcessCondition(string Fact, ProcessScalar EqualsValue);

public enum ProcessDueKind
{
    /// <summary><c>workingDays</c> — §6.10 calendar; negative = before the anchor.</summary>
    WorkingDays = 1,

    /// <summary><c>days</c> — calendar days; negative = before the anchor.</summary>
    CalendarDays = 2,

    /// <summary><c>rule: nextCouncilDay</c> — first council weekday strictly after the anchor (§3.1).</summary>
    NextCouncilDay = 3,
}

/// <param name="From">A <see cref="ProcessAnchors"/> name.</param>
/// <param name="Kind">How <paramref name="Offset"/> is counted.</param>
/// <param name="Offset">Days to add; always 0 for <see cref="ProcessDueKind.NextCouncilDay"/>.</param>
/// <param name="Fallback">Used when <see cref="From"/> is unknown for the case («unknown → …» in §3).</param>
public sealed record ProcessDueRule(string From, ProcessDueKind Kind, int Offset, ProcessDueRule? Fallback = null)
{
    public const string NextCouncilDayRule = "nextCouncilDay";

    /// <summary>This rule and its fallbacks, outermost first.</summary>
    public IEnumerable<ProcessDueRule> Chain()
    {
        for (var rule = this; rule is not null; rule = rule.Fallback)
        {
            yield return rule;
        }
    }
}

public enum ProcessGateMode
{
    Block = 1,
    Warn = 2,
}

public sealed record ProcessGateRef(string Code, ProcessGateMode Mode);

/// <summary>
/// <c>completion</c>: <c>{ "fact", "args" }</c>. The step closes when <see cref="Fact"/> holds and its value
/// passes <see cref="EqualsValue"/> / <see cref="InValues"/>, and every <see cref="AllOf"/> fact holds too.
/// </summary>
/// <remarks>
/// <c>args</c> keys (all optional):
/// <list type="bullet">
/// <item><c>equals</c> / <c>in</c> — the fact value must equal / be one of.</item>
/// <item><c>allOf</c> — more fact names that must hold as well (AND); done at = the latest of them.</item>
/// <item><c>legacyAnyOf</c> — in a legacy case any of these facts closes the step too (pipeline stages, C5).</item>
/// <item><c>manualValue</c> — id of the manual input that stands in for the fact value when the step is closed
/// by «Bajarildi» (§1.5: the manual r02-10 decision counts as the F04 decision).</item>
/// <item><c>valueField</c> — blank field whose answer is the fact value (p03-7: computed <c>day7Pass</c>).</item>
/// <item><c>byValue</c> — switch on the fact value: value → the completion to use for that branch (r02-11).</item>
/// </list>
/// </remarks>
public sealed record ProcessCompletion
{
    public required string Fact { get; init; }

    public ProcessScalar? EqualsValue { get; init; }

    public IReadOnlyList<ProcessScalar> InValues { get; init; } = [];

    public IReadOnlyList<string> AllOf { get; init; } = [];

    public IReadOnlyList<string> LegacyAnyOf { get; init; } = [];

    public string? ManualValue { get; init; }

    public string? ValueField { get; init; }

    public IReadOnlyDictionary<string, ProcessCompletion> ByValue { get; init; } =
        new Dictionary<string, ProcessCompletion>(StringComparer.Ordinal);

    /// <summary>Every fact name this completion reads, branches included.</summary>
    public IEnumerable<string> FactNames()
    {
        yield return Fact;

        foreach (var name in AllOf.Concat(LegacyAnyOf))
        {
            yield return name;
        }

        foreach (var name in ByValue.Values.SelectMany(branch => branch.FactNames()))
        {
            yield return name;
        }
    }
}

/// <summary><c>manual</c>: «Bajarildi» is allowed and asks for these inputs.</summary>
public sealed record ProcessManual(bool Allowed, IReadOnlyList<ProcessManualInput> Inputs);

/// <summary>Manual input types; JSON names <c>text</c>, <c>longtext</c>, <c>date</c>, <c>int</c>, <c>choice</c>, <c>checklist</c>.</summary>
public enum ProcessInputType
{
    Text = 1,
    LongText = 2,
    Date = 3,
    WholeNumber = 4,
    Choice = 5,

    /// <summary>Fixed items; when required each must be ticked or marked «yo'q / kerak emas».</summary>
    Checklist = 6,
}

public sealed record ProcessManualInput
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required ProcessInputType Type { get; init; }

    public bool Required { get; init; }

    /// <summary>Required only while this holds (r01-3 <c>basis</c> for a fixed-term contract).</summary>
    public ProcessRequiredWhen? RequiredWhen { get; init; }

    /// <summary>Choice options or checklist items.</summary>
    public IReadOnlyList<ProcessOption> Options { get; init; } = [];

    public int? Min { get; init; }

    public int? Max { get; init; }
}

/// <summary>
/// <c>requiredWhen</c>: exactly one of <see cref="Input"/> (another input of the same step) or
/// <see cref="Fact"/> (a §7.2 fact of the case) must have the value <see cref="EqualsValue"/>.
/// </summary>
public sealed record ProcessRequiredWhen(string? Input, string? Fact, ProcessScalar EqualsValue);

public sealed record ProcessOption(string Value, string Label);

/// <summary><c>link</c>: deep-link screen (§11.6) and its args.</summary>
public sealed record ProcessLink(string Screen, IReadOnlyDictionary<string, string> Args);

/// <summary>A JSON scalar from a template: text, number or true/false.</summary>
public sealed record ProcessScalar
{
    private ProcessScalar(string? text, decimal? number, bool? flag)
    {
        Text = text;
        Number = number;
        Flag = flag;
    }

    public string? Text { get; }

    public decimal? Number { get; }

    public bool? Flag { get; }

    public static ProcessScalar Of(string text) => new(text, null, null);

    public static ProcessScalar Of(decimal number) => new(null, number, null);

    public static ProcessScalar Of(bool flag) => new(null, null, flag);

    /// <summary>Compares with a fact value (string, bool or any integral / decimal number).</summary>
    public bool Matches(object? value) => value switch
    {
        null => false,
        string text => Text == text,
        bool flag => Flag == flag,
        int or long or short or byte or decimal or double or float =>
            Number == Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        _ => false,
    };

    /// <summary>Text as is, number invariant, flag <c>true</c>/<c>false</c> — the key form used by <c>byValue</c>.</summary>
    public override string ToString()
        => Text ?? (Number is { } number ? number.ToString(CultureInfo.InvariantCulture) : Flag is true ? "true" : "false");
}
