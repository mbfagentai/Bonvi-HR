using Bonvi.Application.Processes.Commands;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Processes;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Processes.Inputs;

/// <summary>What the system says about one data-backed checklist item.</summary>
public enum ProcessAutoItemState
{
    /// <summary>The data exists: green, not asked, cannot be claimed or denied by the client.</summary>
    Known,

    /// <summary>The data does not exist: HR fills it in where it lives; no tick can stand in for it.</summary>
    Missing,

    /// <summary>The subject's data says the item does not apply to this person.</summary>
    NotApplicable,
}

/// <summary>Where a known item was found. The screen words it in the viewer's language.</summary>
public enum ProcessAutoSource
{
    EmployeeCard,
    EmployeeDocument,
    Employment,
    CandidateCard,
    CandidateForm,
    CaseStep,
}

/// <summary>Where «To'ldirish» goes for a missing item.</summary>
public enum ProcessInputFillKind
{
    /// <summary>Upload on the employee card's documents tab, with the type chosen.</summary>
    EmployeeDocument,

    /// <summary>The employee card's overview, whose edit dialog holds the field.</summary>
    EmployeeCard,

    /// <summary>The candidate card: no employee exists yet.</summary>
    CandidateCard,

    /// <summary>Another step of the same case.</summary>
    CaseStep,
}

/// <summary>Why an item is not asked: the one reason there is so far.</summary>
public enum ProcessAutoNotApplicableReason
{
    Female,
}

/// <summary>Where «To'ldirish» goes.</summary>
public sealed record ProcessInputFill(
    ProcessInputFillKind Kind,
    Guid? EmployeeId,
    Guid? CandidateId,
    Guid? DocumentTypeId,
    string? DocumentTypeName,
    string? StepCode);

/// <summary>One data-backed item. No value of the data itself is ever in here: only that it exists.</summary>
/// <param name="Input">The checklist input's id.</param>
/// <param name="Value">The option's value.</param>
/// <param name="Label">The option's label, for the refusal's missing list.</param>
/// <param name="State">The state.</param>
/// <param name="SourceToken">The stored source: <c>employee.passport</c>, <c>doc:&lt;type name&gt;</c>, <c>step:mm</c>…</param>
/// <param name="Source">Known: where it was found.</param>
/// <param name="SourceDetail">Known: the document type's or the step's name.</param>
/// <param name="Reason">NotApplicable: why.</param>
/// <param name="Fill">Missing: where to fill it in.</param>
public sealed record ProcessAutoItem(
    string Input,
    string Value,
    string Label,
    ProcessAutoItemState State,
    string? SourceToken,
    ProcessAutoSource? Source,
    string? SourceDetail,
    ProcessAutoNotApplicableReason? Reason,
    ProcessInputFill? Fill)
{
    /// <summary>The mark the system stores for it; a missing item has none, and no client mark can stand in.</summary>
    public string? Mark => State switch
    {
        ProcessAutoItemState.Known => ProcessManualInputs.ChecklistDone,
        ProcessAutoItemState.NotApplicable => ProcessManualInputs.ChecklistNotNeeded,
        _ => null,
    };
}

/// <summary>A step's data-backed items by input id. An option with no item here is an ordinary manual tick.</summary>
public sealed record ProcessStepAutoItems(IReadOnlyDictionary<string, IReadOnlyList<ProcessAutoItem>> ByInput)
{
    /// <summary>The input's items, or none.</summary>
    public IReadOnlyList<ProcessAutoItem> Of(string inputId) => ByInput.GetValueOrDefault(inputId) ?? [];
}

/// <summary>What the subject's records hold, as yes/no: PINFL and passport values never leave the reader.</summary>
public sealed record ProcessSubjectData
{
    public Guid? EmployeeId { get; init; }

    public Guid? CandidateId { get; init; }

    public bool HasPassport { get; init; }

    public bool HasPinfl { get; init; }

    public bool HasPhoto { get; init; }

    public bool HasMehnatUzUrl { get; init; }

    public Gender? Gender { get; init; }

    public bool HasHireOrder { get; init; }

    public bool CandidateHasPhoto { get; init; }

    public bool CandidateHasForm { get; init; }

    /// <summary>The names, normalised, of the document types the employee holds a live file of.</summary>
    public IReadOnlySet<string> DocumentTypes { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Active document types by normalised name, so «To'ldirish» can choose the type.</summary>
    public IReadOnlyDictionary<string, Guid> ActiveDocumentTypes { get; init; } = new Dictionary<string, Guid>(StringComparer.Ordinal);
}

/// <summary>
/// Which checklist item the system can answer, and from what (owner, 2026-11-05: «xodimda bor ma'lumotni avtomatik olishi
/// kerak», and a missing one is filled in, not marked).
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed in code by process, step, input and item, not in the template JSON</b>: a case reads its pinned template
/// version, so sources in the JSON would reach only cases started after a new version. Option values and step codes never
/// changed across versions; <c>ProcessInputSourcesTests</c> checks every key against the embedded templates.
/// </para>
/// <para>
/// <b>Document types are matched by normalised name</b>, the only stable key: <c>document_types</c> has no code by the
/// owner's rule and its ids differ per database. A renamed type reads as missing, which fails towards asking, never towards a
/// false green; the same test holds the names to the seed file.
/// </para>
/// </remarks>
public static class ProcessInputSources
{
    private const string Passport = "employee.passport";
    private const string Pinfl = "employee.pinfl";
    private const string Photo = "employee.photo";
    private const string MehnatUz = "employee.mehnatUz";
    private const string HireOrder = "employment.hireOrder";
    private const string CandidatePhoto = "candidate.photo";
    private const string CandidateForm = "candidate.form";
    private const string DocumentPrefix = "doc:";
    private const string StepPrefix = "step:";

    private const string PassportCopy = "Паспорт нусхаси";
    private const string Diploma = "Маълумот тўғрисида ҳужжат (диплом)";
    private const string WorkBook = "Меҳнат дафтарчаси";
    private const string Military = "Ҳарбий ҳисоб варақаси";
    private const string Medical = "Тиббий кўрик хулосаси";
    private const string Application = "Ариза";
    private const string Contract = "Меҳнат шартномаси";
    private const string Order = "Буйруқ";
    private const string Form = "Анкета";
    private const string FormAndCouncil = "Анкета + кенгаш хулосаси";
    private const string FormCouncilAndTest = "Анкета + кенгаш хулосаси + тест жавоблари";

    private const string DocsStep = "r01-2";
    private const string SafetyStep = "mm";

    /// <summary>One item: any of its sources is enough; <see cref="FillType"/> <c>null</c> sends HR to the card's fields.</summary>
    private sealed record ItemRule(string Item, string[] Sources, string? FillType = null, string? FillStep = null, bool MenOnly = false);

    private static readonly ItemRule[] DocsRules =
    [
        new("passport", [Passport, Doc(PassportCopy)]),
        new("pinfl", [Pinfl]),
        new("diploma", [Doc(Diploma)], Diploma),
        new("workBook", [Doc(WorkBook), MehnatUz], WorkBook),
        new("military", [Doc(Military)], Military, MenOnly: true),
        new("medical", [Doc(Medical)], Medical),
        new("photo", [Photo, CandidatePhoto]),
        new("application", [Doc(Application)], Application),
        new("councilMinutes", [Doc(FormAndCouncil), Doc(FormCouncilAndTest)], FormAndCouncil),
    ];

    private static readonly ItemRule[] FileRules =
    [
        new("contract", [Doc(Contract)], Contract),
        new("hireOrder", [HireOrder, Doc(Order)], Order),
        new("formF02", [CandidateForm, Doc(Form), Doc(FormAndCouncil), Doc(FormCouncilAndTest)], Form),
        new("docCopies", [Step(DocsStep)], FillStep: DocsStep),
        new("medical", [Doc(Medical)], Medical),
        new("safetyMark", [Step(SafetyStep)], FillStep: SafetyStep),
    ];

    private static readonly Dictionary<(string Process, string Step, string Input), ItemRule[]> Rules = new()
    {
        [(Engine.ProcessCodes.HireFormalise, DocsStep, "docs")] = DocsRules,
        [(Engine.ProcessCodes.HireFormalise, "r01-7", "items")] = FileRules,
    };

    /// <summary>Whether any input of this step has data-backed items: without one, no query is spent.</summary>
    public static bool Covers(string processCode, ProcessStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.Manual is { } manual && manual.Inputs.Any(input => Rules.ContainsKey((processCode, step.Id, input.Id)));
    }

    /// <summary>Every key of the map, for the test holding it to the templates.</summary>
    public static IEnumerable<(string Process, string Step, string Input, string Item)> Keys
        => Rules.SelectMany(rule => rule.Value.Select(item => (rule.Key.Process, rule.Key.Step, rule.Key.Input, item.Item)));

    /// <summary>Every document type name the map reads or fills, for the test holding it to the seed file.</summary>
    public static IEnumerable<string> DocumentTypeNames
        => Rules.Values
            .SelectMany(rules => rules)
            .SelectMany(rule => rule.Sources.Where(source => source.StartsWith(DocumentPrefix, StringComparison.Ordinal)).Select(source => source[DocumentPrefix.Length..]))
            .Distinct(StringComparer.Ordinal);

    /// <summary>The one form document type names are compared in.</summary>
    public static string NormaliseTypeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return name.Trim().ToLowerInvariant();
    }

    /// <summary>The step's items.</summary>
    /// <param name="processCode">The case's process.</param>
    /// <param name="step">The step's definition in the case's pinned version.</param>
    /// <param name="definition">That version.</param>
    /// <param name="data">The subject's records.</param>
    /// <param name="stepStates">The case's steps by code.</param>
    public static ProcessStepAutoItems Resolve(
        string processCode,
        ProcessStep step,
        ProcessDefinition definition,
        ProcessSubjectData data,
        IReadOnlyDictionary<string, ProcessStepState> stepStates)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(stepStates);

        var byInput = new Dictionary<string, IReadOnlyList<ProcessAutoItem>>(StringComparer.Ordinal);

        foreach (var input in step.Manual?.Inputs ?? [])
        {
            if (input.Type != ProcessInputType.Checklist || !Rules.TryGetValue((processCode, step.Id, input.Id), out var rules))
            {
                continue;
            }

            var items = new List<ProcessAutoItem>();
            foreach (var option in input.Options)
            {
                if (Array.Find(rules, rule => rule.Item == option.Value) is { } rule
                    && ResolveItem(rule, input.Id, option.Value, option.Label, definition, data, stepStates) is { } item)
                {
                    items.Add(item);
                }
            }

            byInput[input.Id] = items;
        }

        return new ProcessStepAutoItems(byInput);
    }

    /// <summary><c>null</c>: no card exists yet that could hold the item, so it stays an ordinary tick for now.</summary>
    private static ProcessAutoItem? ResolveItem(
        ItemRule rule,
        string input,
        string value,
        string label,
        ProcessDefinition definition,
        ProcessSubjectData data,
        IReadOnlyDictionary<string, ProcessStepState> stepStates)
    {
        var readable = rule.Sources.Where(source => IsReadable(source, data)).ToList();
        if (readable.Count == 0)
        {
            return null;
        }

        if (rule.MenOnly && data.Gender == Gender.Female)
        {
            return new ProcessAutoItem(input, value, label, ProcessAutoItemState.NotApplicable, "employee.gender", null, null, ProcessAutoNotApplicableReason.Female, null);
        }

        foreach (var source in readable)
        {
            if (Holds(source, data, stepStates))
            {
                return new ProcessAutoItem(
                    input,
                    value,
                    label,
                    ProcessAutoItemState.Known,
                    source,
                    SourceKind(source),
                    SourceDetail(source, definition),
                    null,
                    null);
            }
        }

        return new ProcessAutoItem(input, value, label, ProcessAutoItemState.Missing, null, null, null, null, FillOf(rule, data));
    }

    private static bool IsReadable(string source, ProcessSubjectData data)
    {
        if (source.StartsWith(StepPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        return source.StartsWith("candidate.", StringComparison.Ordinal) ? data.CandidateId is not null : data.EmployeeId is not null;
    }

    private static bool Holds(string source, ProcessSubjectData data, IReadOnlyDictionary<string, ProcessStepState> stepStates)
    {
        if (source.StartsWith(DocumentPrefix, StringComparison.Ordinal))
        {
            return data.DocumentTypes.Contains(NormaliseTypeName(source[DocumentPrefix.Length..]));
        }

        if (source.StartsWith(StepPrefix, StringComparison.Ordinal))
        {
            return stepStates.GetValueOrDefault(source[StepPrefix.Length..]) == ProcessStepState.Done;
        }

        return source switch
        {
            Passport => data.HasPassport,
            Pinfl => data.HasPinfl,
            Photo => data.HasPhoto,
            MehnatUz => data.HasMehnatUzUrl,
            HireOrder => data.HasHireOrder,
            CandidatePhoto => data.CandidateHasPhoto,
            CandidateForm => data.CandidateHasForm,
            _ => false,
        };
    }

    private static ProcessAutoSource SourceKind(string source)
    {
        if (source.StartsWith(DocumentPrefix, StringComparison.Ordinal))
        {
            return ProcessAutoSource.EmployeeDocument;
        }

        if (source.StartsWith(StepPrefix, StringComparison.Ordinal))
        {
            return ProcessAutoSource.CaseStep;
        }

        return source switch
        {
            HireOrder => ProcessAutoSource.Employment,
            CandidatePhoto => ProcessAutoSource.CandidateCard,
            CandidateForm => ProcessAutoSource.CandidateForm,
            _ => ProcessAutoSource.EmployeeCard,
        };
    }

    private static string? SourceDetail(string source, ProcessDefinition definition)
    {
        if (source.StartsWith(DocumentPrefix, StringComparison.Ordinal))
        {
            return source[DocumentPrefix.Length..];
        }

        return source.StartsWith(StepPrefix, StringComparison.Ordinal)
            ? definition.Steps.FirstOrDefault(step => step.Id == source[StepPrefix.Length..])?.Title
            : null;
    }

    private static ProcessInputFill? FillOf(ItemRule rule, ProcessSubjectData data)
    {
        if (rule.FillStep is { } step)
        {
            return new ProcessInputFill(ProcessInputFillKind.CaseStep, null, null, null, null, step);
        }

        if (data.EmployeeId is not { } employeeId)
        {
            return data.CandidateId is { } candidateId
                ? new ProcessInputFill(ProcessInputFillKind.CandidateCard, null, candidateId, null, null, null)
                : null;
        }

        if (rule.FillType is not { } type)
        {
            return new ProcessInputFill(ProcessInputFillKind.EmployeeCard, employeeId, null, null, null, null);
        }

        Guid? typeId = data.ActiveDocumentTypes.TryGetValue(NormaliseTypeName(type), out var id) ? id : null;

        return new ProcessInputFill(ProcessInputFillKind.EmployeeDocument, employeeId, null, typeId, type, null);
    }

    private static string Doc(string name) => DocumentPrefix + name;

    private static string Step(string code) => StepPrefix + code;
}
