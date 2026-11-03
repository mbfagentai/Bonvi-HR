using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;
using DefinitionStep = Bonvi.Application.Blanks.Definitions.BlankStep;

namespace Bonvi.Application.Blanks.Steps;

/// <summary>Ochiq qadam va uning atrofi — har qadam amali shundan boshlanadi.</summary>
public sealed record BlankStepContext(
    BlankSubmission Submission,
    BlankSubmissionStep Step,
    BlankDefinition Definition,
    DefinitionStep DefinitionStep,
    BlankCaller Caller,
    BlankStepAbility Ability);

/// <summary>Tekshirilgan imzo ma'lumoti — saqlashdan oldin (planshet imzosi fayli hali yozilmagan).</summary>
public sealed record BlankPreparedAct(
    BlankAnswersOutcome Outcome,
    bool AnswersChanged,
    string? Decision,
    string? Comment,
    string? AnswersPatch);

/// <summary>
/// Qadam amallarining umumiy yadrosi (SPEC §5, §6.2): yuklash → tekshirish → domen → to'liqlik (§5.3) → COMMIT
/// → tasdiqlangan bo'lsa <see cref="IBlankApprovedHook"/>.
/// </summary>
/// <remarks>
/// Tartib: avval 404/409 (blanka, versiya, holat, qadam), keyin ruxsat — eskirgan sahifadan bosilgan tugma
/// «ruxsat yo'q» emas, «o'zgargan» deb javob olsin.
/// </remarks>
public sealed class BlankStepActions
{
    /// <summary><c>answers_patch</c> ichida qaror qo'shimchasining kaliti — maydon id'si «$» bilan boshlanmaydi.</summary>
    public const string DecisionExtraKey = "$decisionExtra";

    private const int MaxDecisionExtraLength = 2000;

    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly IBlankSubmissionRepository _submissions;
    private readonly IBlankReadModel _readModel;
    private readonly IEmployeeRepository _employees;
    private readonly BlankActorResolver _actors;
    private readonly BlankSubmissionDetails _details;
    private readonly IBlankApprovedHook _approved;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public BlankStepActions(
        IBlankSubmissionRepository submissions,
        IBlankReadModel readModel,
        IEmployeeRepository employees,
        BlankActorResolver actors,
        BlankSubmissionDetails details,
        IBlankApprovedHook approved,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissions = submissions;
        _readModel = readModel;
        _employees = employees;
        _actors = actors;
        _details = details;
        _approved = approved;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public DateTimeOffset Now => _clock.UtcNow;

    public async Task<Result<BlankStepContext>> LoadAsync(
        Guid submissionId,
        string stepCode,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var submission = await _submissions.GetWithChildrenAsync(submissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        if (submission.CheckVersion(expectedVersion) is { IsFailure: true } stale)
        {
            return stale.Error;
        }

        if (submission.Status != BlankSubmissionStatus.InApproval)
        {
            return BlankErrors.InvalidStatus;
        }

        var step = submission.Steps.FirstOrDefault(item => string.Equals(item.StepCode, stepCode, StringComparison.Ordinal));
        if (step is null)
        {
            return BlankErrors.StepNotFound;
        }

        if (step.State != BlankStepState.Open)
        {
            return BlankErrors.StepNotOpen;
        }

        var definition = await _details.DefinitionAsync(submission.TemplateVersionId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        if (definition.FindStep(step.StepCode) is not { } definitionStep)
        {
            return BlankErrors.StepNotFound;
        }

        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        var ability = await _actors
            .AbilityAsync(
                caller,
                step.ActorRole,
                step.Mode,
                submission.EmployeeId,
                submission.DepartmentId,
                BlankSubmissionRules.ParseObject(submission.Answers),
                cancellationToken)
            .ConfigureAwait(false);

        return new BlankStepContext(submission, step, definition, definitionStep, caller, ability);
    }

    /// <summary>Shaxs rolidagi imzolovchi (xodimning o'zi, boshliq, <c>field:</c>); lavozim roli va guvohda <c>null</c>.</summary>
    public Task<Guid?> ResolvePersonAsync(BlankStepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _actors.ResolvePersonAsync(
            context.Step.ActorRole,
            context.Submission.EmployeeId,
            context.Submission.DepartmentId,
            BlankSubmissionRules.ParseObject(context.Submission.Answers),
            cancellationToken);
    }

    public async Task<string?> EmployeeNameAsync(Guid? employeeId, CancellationToken cancellationToken)
        => employeeId is { } id
            ? (await _employees.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.FullName.Full
            : null;

    /// <summary>
    /// Javoblar (faqat qadamning o'z bo'limlari, to'liq almashtirish — #9 qoidasi), shu bo'limlarning majburiy
    /// maydonlari, qaror va qo'shimchasi tekshiriladi; huquqiy iz (<c>answers_patch</c>) yig'iladi.
    /// </summary>
    public async Task<Result<BlankPreparedAct>> PrepareAsync(
        BlankStepContext context,
        BlankStepActInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);

        if (input.Answers is not (null or JsonObject))
        {
            return BlankErrors.InvalidJson;
        }

        var definition = context.Definition;
        var stepSections = definition.StepSectionIds(context.Step.StepCode);
        var before = BlankSubmissionRules.ParseObject(context.Submission.Answers);

        var answers = before;
        if (input.Answers is JsonObject incoming)
        {
            var merged = SaveBlankAnswersCommandHandler.Merge(definition, before, incoming, stepSections);
            if (merged.IsFailure)
            {
                return merged.Error;
            }

            answers = merged.Value;
        }

        var validated = BlankAnswersValidator.Validate(definition, answers, new BlankAnswersOptions(_clock.Today, stepSections));
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        if (await _readModel.FirstMissingReferenceAsync(validated.Value.References, context.Submission.Id, cancellationToken)
                .ConfigureAwait(false) is { } missingPath)
        {
            return BlankSubmissionRules.MissingReference(missingPath);
        }

        var decided = Decide(context.DefinitionStep, input.Decision, input.DecisionExtra);
        if (decided.IsFailure)
        {
            return decided.Error;
        }

        var patch = new JsonObject();
        foreach (var field in definition.Fields.Where(field => definition.SectionOf(field.Id) is { } section && stepSections.Contains(section.Id)))
        {
            if ((validated.Value.Answers[field.Id] ?? validated.Value.Computed[field.Id]) is { } value)
            {
                patch[field.Id] = value.DeepClone();
            }
        }

        if (decided.Value.Extra is { } extra)
        {
            patch[DecisionExtraKey] = extra;
        }

        return new BlankPreparedAct(
            validated.Value,
            BlankAnswersValidator.ChangedFields(before, validated.Value.Answers).Any(),
            decided.Value.Decision,
            string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim(),
            patch.Count == 0 ? null : patch.ToJsonString(Compact));
    }

    /// <summary>Imzolaydi, keyingi guruhni ochadi (sharti bajarilmagan qadam — Skipped), saqlaydi.</summary>
    public async Task<Result<BlankSubmissionDetailDto>> SignAsync(
        BlankStepContext context,
        BlankPreparedAct prepared,
        BlankStepAct act,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(prepared);

        var submission = context.Submission;
        var definition = context.Definition;
        var outcome = prepared.Outcome;

        var signed = submission.SignStep(
            context.Step.Id,
            act,
            prepared.AnswersChanged ? State(definition, outcome, submission) : null,
            _clock.UtcNow,
            stepCode => BlankConditions.EvaluateStepCondition(definition, outcome.Answers, outcome.Computed, stepCode));
        if (signed.IsFailure)
        {
            return signed.Error;
        }

        // Qadamlar grafi siljidi — «missing» endi yangi ochilgan bo'limlarni ham qamraydi (§5.3).
        var refreshed = submission.RefreshCompleteness(State(definition, outcome, submission));
        if (refreshed.IsFailure)
        {
            return refreshed.Error;
        }

        return await CommitAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Qaytarish / rad etishdan keyin: to'liqlikni yangilab saqlaydi.</summary>
    public async Task<Result<BlankSubmissionDetailDto>> SaveAsync(BlankStepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Saqlangan javoblar avval tekshirilgan; bugungi sana chegarasi o'zgarib qolgan bo'lsa — eski holat qoladi.
        var validated = BlankAnswersValidator.Validate(
            context.Definition,
            BlankSubmissionRules.ParseObject(context.Submission.Answers),
            new BlankAnswersOptions(_clock.Today));
        if (validated.IsSuccess)
        {
            context.Submission.RefreshCompleteness(State(context.Definition, validated.Value, context.Submission));
        }

        return await CommitAsync(context.Submission, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<BlankSubmissionDetailDto>> CommitAsync(BlankSubmission submission, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleBlankWrite(exception))
        {
            return BlankErrors.Stale;
        }

        if (submission.Status == BlankSubmissionStatus.Approved)
        {
            await _approved.OnApprovedAsync(submission.Id, cancellationToken).ConfigureAwait(false);
        }

        return await _details.GetAsync(submission.Id, cancellationToken).ConfigureAwait(false);
    }

    private static BlankAnswersState State(BlankDefinition definition, BlankAnswersOutcome outcome, BlankSubmission submission)
        => BlankSubmissionRules.BuildState(
            definition,
            outcome,
            submission.Steps.Select(step => new BlankStepSnapshot(step.StepCode, step.OrderNo, step.State, step.DueDate)).ToList());

    private static Result<(string? Decision, JsonNode? Extra)> Decide(DefinitionStep step, string? decision, JsonNode? extra)
    {
        var chosen = string.IsNullOrWhiteSpace(decision) ? null : decision.Trim();
        var hasExtra = extra is not null && !(extra is JsonValue value && value.TryGetValue<string>(out var text) && string.IsNullOrWhiteSpace(text));

        if (step.Decision is not { } rule)
        {
            return chosen is null && !hasExtra ? (null, null) : BlankAnswerErrors.Invalid.At("decision");
        }

        if (chosen is null)
        {
            if (rule.Required)
            {
                return BlankErrors.DecisionRequired.At("decision");
            }

            return hasExtra ? BlankAnswerErrors.Invalid.At("decisionExtra") : (null, null);
        }

        var option = rule.Options.FirstOrDefault(item => string.Equals(item.Value, chosen, StringComparison.Ordinal));
        if (option is null)
        {
            return BlankAnswerErrors.Invalid.At("decision");
        }

        if (option.Extra is not { } extraRule)
        {
            return hasExtra ? BlankAnswerErrors.Invalid.At("decisionExtra") : (chosen, null);
        }

        if (!hasExtra)
        {
            return extraRule.Required ? BlankErrors.DecisionRequired.At("decisionExtra") : (chosen, null);
        }

        return CleanExtra(extraRule, extra!) is { } clean
            ? (chosen, clean)
            : BlankAnswerErrors.Invalid.At("decisionExtra");
    }

    private static JsonValue? CleanExtra(BlankOptionExtra rule, JsonNode extra)
    {
        if (extra is not JsonValue value)
        {
            return null;
        }

        if (rule.Type is BlankFieldType.WholeNumber)
        {
            return value.TryGetValue<long>(out var number) ? JsonValue.Create(number) : null;
        }

        return value.TryGetValue<string>(out var text) && text.Trim().Length <= MaxDecisionExtraLength
            ? JsonValue.Create(text.Trim())
            : null;
    }
}
