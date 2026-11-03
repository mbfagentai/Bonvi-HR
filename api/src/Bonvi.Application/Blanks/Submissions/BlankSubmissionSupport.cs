using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Domain.Blanks;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Common;
using DefinitionStepMode = Bonvi.Application.Blanks.Definitions.BlankStepMode;
using StepMode = Bonvi.Domain.Blanks.BlankStepMode;

namespace Bonvi.Application.Blanks.Submissions;

/// <summary>
/// Tahlil qilingan ta'riflar keshi. Nashr qilingan versiya hech qachon o'zgarmaydi (§3.2) — kalit
/// versiya id'si, shuning uchun eskirish yo'q; versiyalar soni kichik (shablon × versiya).
/// </summary>
public sealed class BlankDefinitionCache
{
    private readonly ConcurrentDictionary<Guid, BlankDefinition> _definitions = new();

    public async Task<BlankDefinition?> GetAsync(Guid versionId, IBlankReadModel readModel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readModel);

        if (_definitions.TryGetValue(versionId, out var cached))
        {
            return cached;
        }

        var json = await readModel.GetDefinitionJsonAsync(versionId, cancellationToken).ConfigureAwait(false);
        return json is null ? null : _definitions.GetOrAdd(versionId, _ => BlankDefinitionValidator.Load(json));
    }
}

/// <summary>Holatni hisoblash uchun qadamning kerakli qismi.</summary>
public sealed record BlankStepSnapshot(string StepCode, int OrderNo, BlankStepState State, DateOnly? DueDate);

/// <summary>
/// Saqlash/yuborishdagi sof qoidalar: qaysi bo'lim tahrirlanadi, to'liqlik, muddat, qadam rejasi.
/// </summary>
public static class BlankSubmissionRules
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Hozir tahrirlanadigan bo'limlar (§5): qoralamada — qoralama va «open» bo'limlar (<c>blanks.fill</c>);
    /// kelishuvda — chaqiruvchi bajara oladigan OCHIQ qadamlarning bo'limlari va «open» bo'limlar.
    /// </summary>
    public static IReadOnlySet<string> EditableSections(
        BlankDefinition definition,
        BlankSubmissionStatus status,
        bool canFill,
        IEnumerable<string> actionableOpenSteps)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(actionableOpenSteps);

        var sections = new HashSet<string>(StringComparer.Ordinal);
        switch (status)
        {
            case BlankSubmissionStatus.Draft when canFill:
                sections.UnionWith(definition.DraftSectionIds());
                sections.UnionWith(definition.OpenSectionIds());
                break;
            case BlankSubmissionStatus.InApproval:
                if (canFill)
                {
                    sections.UnionWith(definition.OpenSectionIds());
                }

                foreach (var step in actionableOpenSteps)
                {
                    sections.UnionWith(definition.StepSectionIds(step));
                }

                break;
        }

        return sections;
    }

    /// <summary>Javoblar holati: tozalangan javoblar, computed, to'liqlik (§5.3) va muddat.</summary>
    public static BlankAnswersState BuildState(
        BlankDefinition definition,
        BlankAnswersOutcome outcome,
        IReadOnlyCollection<BlankStepSnapshot> steps)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(steps);

        var active = steps.Where(step => step.State != BlankStepState.Skipped).ToList();
        var open = active.Where(step => step.State == BlankStepState.Open).ToList();
        var lastGroupOpen = active.Count == 0
            ? steps.Count > 0
            : open.Count > 0 && open[0].OrderNo == active.Max(step => step.OrderNo);

        var scope = BlankCompleteness.SectionsInScope(
            definition,
            active.Where(step => step.State is BlankStepState.Open or BlankStepState.Signed).Select(step => step.StepCode),
            lastGroupOpen);
        var missing = BlankCompleteness.Missing(definition, outcome.Answers, scope);

        var due = DueDate(definition, outcome.Answers)
            ?? steps.Where(step => step.State is BlankStepState.Waiting or BlankStepState.Open)
                .Select(step => step.DueDate)
                .Min();

        return new BlankAnswersState(
            outcome.Answers.ToJsonString(Compact),
            outcome.Computed.ToJsonString(Compact),
            new JsonArray([.. missing.Select(path => (JsonNode?)JsonValue.Create(path))]).ToJsonString(Compact),
            missing.Count,
            due);
    }

    /// <summary>
    /// Yuborishdagi qadam rejasi. Shart (<c>when</c>) «dangasa» (§4.11.3): faqat HOZIR ochiladigan guruh
    /// tekshiriladi — guruhning hamma qadami o'tkazib yuborilsa, keyingi guruhga o'tiladi. Keyingi
    /// guruhlarning sharti o'z navbati kelganda (S6) qayta baholanadi.
    /// </summary>
    public static IReadOnlyList<BlankStepPlan> Plan(BlankDefinition definition, JsonObject answers, JsonObject computed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);

        var plan = new List<BlankStepPlan>(definition.Steps.Count);
        var opened = false;

        foreach (var group in definition.Steps.GroupBy(step => step.Order).OrderBy(group => group.Key))
        {
            foreach (var step in group)
            {
                var skipped = !opened && !BlankConditions.EvaluateStepCondition(definition, answers, computed, step.Id);
                plan.Add(new BlankStepPlan(
                    step.Id,
                    step.Order,
                    step.Title,
                    step.Actor,
                    MapMode(step.Mode),
                    skipped,
                    step.Due is { } due && DateOf(answers[due.Field]) is { } start ? start.AddDays(due.Days) : null));
            }

            opened |= plan.Any(item => item.OrderNo == group.Key && !item.Skipped);
        }

        return plan;
    }

    /// <summary>Rejadan yuborishdan keyingi holatni oldindan ko'radi (to'liqlik hisobi uchun).</summary>
    public static IReadOnlyList<BlankStepSnapshot> Preview(IReadOnlyList<BlankStepPlan> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var openOrder = plan.Where(step => !step.Skipped).Select(step => (int?)step.OrderNo).Min();
        return plan.Select(step => new BlankStepSnapshot(
                step.StepCode,
                step.OrderNo,
                step.Skipped ? BlankStepState.Skipped
                    : step.OrderNo == openOrder ? BlankStepState.Open
                    : BlankStepState.Waiting,
                step.DueDate))
            .ToList();
    }

    /// <summary>Saqlangan JSON obyekt.</summary>
    public static JsonObject ParseObject(string json) => JsonNode.Parse(json) as JsonObject ?? [];

    /// <summary>Ma'lumotnoma havolalarini (ref, docRef) tekshirgandan keyin yo'q bo'lsa — xato.</summary>
    public static Error MissingReference(string path) => BlankAnswerErrors.Invalid.At(path);

    private static DateOnly? DueDate(BlankDefinition definition, JsonObject answers)
        => definition.DueDateField is { } field ? DateOf(answers[field]) : null;

    private static DateOnly? DateOf(JsonNode? node)
        => node is JsonValue value
           && value.TryGetValue<string>(out var text)
           && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static StepMode MapMode(DefinitionStepMode mode) => mode switch
    {
        DefinitionStepMode.Electronic => StepMode.Electronic,
        DefinitionStepMode.InPerson => StepMode.InPerson,
        _ => StepMode.Either,
    };
}

/// <summary>
/// Blanka ko'rinishi + chaqiruvchining imkoniyatlari (<c>can</c>) — hamma o'zgartiruvchi amal javobi shu.
/// </summary>
public sealed class BlankSubmissionDetails
{
    private readonly IBlankReadModel _readModel;
    private readonly BlankActorResolver _actors;
    private readonly BlankDefinitionCache _definitions;

    public BlankSubmissionDetails(IBlankReadModel readModel, BlankActorResolver actors, BlankDefinitionCache definitions)
    {
        _readModel = readModel;
        _actors = actors;
        _definitions = definitions;
    }

    public Task<BlankDefinition?> DefinitionAsync(Guid versionId, CancellationToken cancellationToken = default)
        => _definitions.GetAsync(versionId, _readModel, cancellationToken);

    /// <summary>Chaqiruvchi bajara oladigan ochiq qadamlar.</summary>
    public async Task<IReadOnlyList<(string StepCode, BlankStepAbility Ability)>> ActionableAsync(
        BlankCaller caller,
        IEnumerable<(string StepCode, string ActorRole, StepMode Mode)> openSteps,
        Guid? subjectEmployeeId,
        Guid? departmentId,
        JsonObject answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openSteps);

        var result = new List<(string, BlankStepAbility)>();
        foreach (var step in openSteps)
        {
            var ability = await _actors
                .AbilityAsync(caller, step.ActorRole, step.Mode, subjectEmployeeId, departmentId, answers, cancellationToken)
                .ConfigureAwait(false);
            if (ability.Any)
            {
                result.Add((step.StepCode, ability));
            }
        }

        return result;
    }

    public async Task<Result<BlankSubmissionDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _readModel.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (dto is null)
        {
            return BlankErrors.NotFound;
        }

        var definition = await DefinitionAsync(dto.TemplateVersionId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        var status = Enum.Parse<BlankSubmissionStatus>(dto.Status);
        var answers = BlankSubmissionRules.ParseObject(dto.Answers.GetRawText());

        var actionable = status == BlankSubmissionStatus.InApproval
            ? await ActionableAsync(
                    caller,
                    dto.Steps.Where(step => step.State == nameof(BlankStepState.Open))
                        .Select(step => (step.StepCode, step.ActorRole, Enum.Parse<StepMode>(step.Mode))),
                    dto.Subject.Kind == nameof(BlankSubjectKind.Employee) ? dto.Subject.Id : null,
                    dto.DepartmentId,
                    answers,
                    cancellationToken)
                .ConfigureAwait(false)
            : [];

        var editable = BlankSubmissionRules.EditableSections(
            definition,
            status,
            caller.CanFill,
            actionable.Select(step => step.StepCode));

        var isDraft = status == BlankSubmissionStatus.Draft;
        var canCancel = (isDraft && (caller.CanManage || (caller.CanFill && dto.CreatedBy == caller.UserId)))
            || (status == BlankSubmissionStatus.InApproval && caller.CanManage);

        return dto with
        {
            Can = new BlankCanDto(
                editable.Count > 0,
                isDraft && caller.CanFill,
                canCancel,
                definition.Sections.Where(section => editable.Contains(section.Id)).Select(section => section.Id).ToList(),
                actionable.Select(step => new BlankActionableStepDto(step.StepCode, step.Ability.Electronic, step.Ability.InPerson)).ToList()),
        };
    }
}
