using System.Text.Json.Nodes;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Application.Blanks.Steps;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Submissions;

/// <summary>Yangi blanka — qoralama (§7 #7).</summary>
/// <param name="TemplateCode">Shablon kodi.</param>
/// <param name="EmployeeId">Sub'yekt xodim (shablon turi Employee bo'lsa).</param>
/// <param name="CandidateId">Sub'yekt nomzod (shablon turi Candidate bo'lsa).</param>
/// <param name="ClientRequestId">Takroriy yaratishdan himoya: shu id bilan ikkinchi so'rov — o'sha blanka (200).</param>
public sealed record CreateBlankSubmissionCommand(
    string? TemplateCode,
    Guid? EmployeeId,
    Guid? CandidateId,
    Guid? ClientRequestId);

/// <summary>Yaratish natijasi.</summary>
/// <param name="Submission">Blanka.</param>
/// <param name="Created">Yangi yaratildimi (201) yoki takroriy so'rov (200).</param>
public sealed record CreateBlankSubmissionOutcome(BlankSubmissionDetailDto Submission, bool Created);

/// <summary>Javoblarni saqlash (§7 #9) — tahrirlanadigan bo'limlar to'liq almashadi, boshqalari e'tiborsiz.</summary>
public sealed record SaveBlankAnswersCommand(Guid SubmissionId, int ExpectedVersion, JsonNode? Answers);

/// <summary>Yuborish (§7 #10).</summary>
public sealed record SubmitBlankSubmissionCommand(Guid SubmissionId, int ExpectedVersion);

/// <summary>Bekor qilish (§7 #11).</summary>
public sealed record CancelBlankSubmissionCommand(Guid SubmissionId, int ExpectedVersion, string? Reason);

public sealed class CreateBlankSubmissionCommandHandler
{
    private readonly IBlankTemplateRepository _templates;
    private readonly IBlankTemplateVersionRepository _versions;
    private readonly IBlankSubmissionRepository _submissions;
    private readonly IBlankReadModel _readModel;
    private readonly IBlankPrefillSource _prefill;
    private readonly BlankActorResolver _actors;
    private readonly BlankSubmissionDetails _details;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBlankSubmissionCommandHandler(
        IBlankTemplateRepository templates,
        IBlankTemplateVersionRepository versions,
        IBlankSubmissionRepository submissions,
        IBlankReadModel readModel,
        IBlankPrefillSource prefill,
        BlankActorResolver actors,
        BlankSubmissionDetails details,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _templates = templates;
        _versions = versions;
        _submissions = submissions;
        _readModel = readModel;
        _prefill = prefill;
        _actors = actors;
        _details = details;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateBlankSubmissionOutcome>> HandleAsync(
        CreateBlankSubmissionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ClientRequestId is { } requestId
            && await ExistingAsync(requestId, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return existing;
        }

        var template = string.IsNullOrWhiteSpace(command.TemplateCode)
            ? null
            : await _templates.GetByCodeAsync(command.TemplateCode.Trim(), cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        if (!template.IsActive)
        {
            return BlankErrors.TemplateInactive;
        }

        var version = await _versions.GetPublishedAsync(template.Id, cancellationToken).ConfigureAwait(false);
        var definition = version is null
            ? null
            : await _details.DefinitionAsync(version.Id, cancellationToken).ConfigureAwait(false);
        if (version is null || definition is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        var subject = await SubjectAsync(template.SubjectKind, command, cancellationToken).ConfigureAwait(false);
        if (subject.IsFailure)
        {
            return subject.Error;
        }

        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        var today = _clock.Today;
        var values = await _prefill
            .ResolveAsync(
                new BlankPrefillContext(
                    definition.Subject,
                    subject.Value.EmployeeId,
                    subject.Value.CandidateId,
                    caller.EmployeeId,
                    today),
                BlankPrefill.KeysOf(definition),
                cancellationToken)
            .ConfigureAwait(false);

        var outcome = Prefilled(definition, BlankPrefill.Apply(definition, values), today);
        var state = BlankSubmissionRules.BuildState(definition, outcome, []);

        var created = BlankSubmission.Create(template, version, subject.Value, state, command.ClientRequestId);
        if (created.IsFailure)
        {
            return created.Error;
        }

        _submissions.Add(created.Value);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (command.ClientRequestId is { } racedId && _fault.IsDuplicateBlankRequest(exception))
        {
            // Bir xil so'rov parallel keldi — g'olib yaratgan blanka qaytariladi.
            _submissions.Remove(created.Value);
            return await ExistingAsync(racedId, cancellationToken).ConfigureAwait(false) is { } raced
                ? raced
                : BlankErrors.NotFound;
        }

        var detail = await _details.GetAsync(created.Value.Id, cancellationToken).ConfigureAwait(false);
        return detail.IsFailure ? detail.Error : new CreateBlankSubmissionOutcome(detail.Value, Created: true);
    }

    /// <summary>
    /// Oldindan to'ldirilgan javoblar. Manba qiymati maydon chegarasiga sig'masa (masalan juda uzun
    /// lavozim nomi) — o'sha maydon bo'sh qoldiriladi, blanka ochilishi to'xtamaydi.
    /// </summary>
    private static BlankAnswersOutcome Prefilled(BlankDefinition definition, JsonObject answers, DateOnly today)
    {
        var options = new BlankAnswersOptions(today);
        for (var attempt = 0; attempt <= definition.Fields.Count; attempt++)
        {
            var validated = BlankAnswersValidator.Validate(definition, answers, options);
            if (validated.IsSuccess)
            {
                return validated.Value;
            }

            var path = validated.Error.Details.TryGetValue("field", out var at) ? at as string : null;
            if (path is null || !answers.Remove(path.Split('[', '.')[0]))
            {
                break;
            }
        }

        return BlankAnswersValidator.Validate(definition, new JsonObject(), options).Value;
    }

    private async Task<Result<BlankSubject>> SubjectAsync(
        BlankSubjectKind kind,
        CreateBlankSubmissionCommand command,
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case BlankSubjectKind.Employee when command.EmployeeId is { } employeeId && command.CandidateId is null:
                var employee = await _readModel.FindEmployeeSubjectAsync(employeeId, cancellationToken).ConfigureAwait(false);
                return employee is null
                    ? BlankErrors.EmployeeNotFound
                    : new BlankSubject(employee.EmployeeId, employee.EmploymentId, DepartmentId: employee.DepartmentId);

            case BlankSubjectKind.Candidate when command.CandidateId is { } candidateId && command.EmployeeId is null:
                var candidate = await _readModel.FindCandidateSubjectAsync(candidateId, cancellationToken).ConfigureAwait(false);
                return candidate.Found
                    ? new BlankSubject(CandidateId: candidateId, DepartmentId: candidate.DepartmentId)
                    : BlankErrors.CandidateNotFound;

            case BlankSubjectKind.None when command.EmployeeId is null && command.CandidateId is null:
                return BlankSubject.None;

            default:
                return BlankErrors.SubjectMismatch;
        }
    }

    private async Task<CreateBlankSubmissionOutcome?> ExistingAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (await _readModel.FindIdByClientRequestAsync(requestId, cancellationToken).ConfigureAwait(false) is not { } id)
        {
            return null;
        }

        var detail = await _details.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return detail.IsSuccess ? new CreateBlankSubmissionOutcome(detail.Value, Created: false) : null;
    }
}

public sealed class SaveBlankAnswersCommandHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly IBlankReadModel _readModel;
    private readonly BlankActorResolver _actors;
    private readonly BlankSubmissionDetails _details;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SaveBlankAnswersCommandHandler(
        IBlankSubmissionRepository submissions,
        IBlankReadModel readModel,
        BlankActorResolver actors,
        BlankSubmissionDetails details,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissions = submissions;
        _readModel = readModel;
        _actors = actors;
        _details = details;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        SaveBlankAnswersCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var submission = await _submissions.GetWithChildrenAsync(command.SubmissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        var definition = await _details.DefinitionAsync(submission.TemplateVersionId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        if (submission.CheckVersion(command.ExpectedVersion) is { IsFailure: true } stale)
        {
            return stale.Error;
        }

        if (submission.Status is not (BlankSubmissionStatus.Draft or BlankSubmissionStatus.InApproval))
        {
            return BlankErrors.InvalidStatus;
        }

        if (command.Answers is not (null or JsonObject))
        {
            return BlankErrors.InvalidJson;
        }

        var before = BlankSubmissionRules.ParseObject(submission.Answers);
        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        var actionable = submission.Status == BlankSubmissionStatus.InApproval
            ? await _details.ActionableAsync(
                    caller,
                    submission.Steps.Where(step => step.State == BlankStepState.Open)
                        .Select(step => (step.StepCode, step.ActorRole, step.Mode)),
                    submission.EmployeeId,
                    submission.DepartmentId,
                    before,
                    cancellationToken)
                .ConfigureAwait(false)
            : [];

        var editable = BlankSubmissionRules.EditableSections(
            definition,
            submission.Status,
            caller.CanFill,
            actionable.Select(step => step.StepCode));
        if (editable.Count == 0)
        {
            return BlankErrors.Forbidden;
        }

        var merged = Merge(definition, before, command.Answers as JsonObject ?? [], editable);
        if (merged.IsFailure)
        {
            return merged.Error;
        }

        var validated = BlankAnswersValidator.Validate(definition, merged.Value, new BlankAnswersOptions(_clock.Today));
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        if (await _readModel.FirstMissingReferenceAsync(validated.Value.References, submission.Id, cancellationToken)
                .ConfigureAwait(false) is { } missingPath)
        {
            return BlankSubmissionRules.MissingReference(missingPath);
        }

        var state = BlankSubmissionRules.BuildState(
            definition,
            validated.Value,
            submission.Steps.Select(step => new BlankStepSnapshot(step.StepCode, step.OrderNo, step.State, step.DueDate)).ToList());

        var saved = submission.SaveAnswers(state);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleBlankWrite(exception))
        {
            return BlankErrors.Stale;
        }

        return await _details.GetAsync(submission.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tahrirlanadigan bo'lim maydonlari so'rovdagisiga TO'LIQ almashadi (yuborilmagani o'chadi);
    /// boshqa maydon so'rovda bo'lsa-yu QIYMATI o'zgargan bo'lsa — 409 <c>blanka.maydon_tahrirlanmaydi</c>
    /// (o'zgarmagan nusxa e'tiborsiz: forma butun javoblarni qaytarib yuboradi).
    /// </summary>
    internal static Result<JsonObject> Merge(
        BlankDefinition definition,
        JsonObject before,
        JsonObject incoming,
        IReadOnlySet<string> editable)
    {
        var overlay = before.DeepClone().AsObject();
        foreach (var (key, value) in incoming)
        {
            overlay[key] = value?.DeepClone();
        }

        foreach (var field in BlankAnswersValidator.ChangedFields(before, overlay))
        {
            if (definition.SectionOf(field) is { } section && !editable.Contains(section.Id))
            {
                return BlankErrors.FieldNotEditable;
            }
        }

        var merged = before.DeepClone().AsObject();
        foreach (var field in definition.Fields.Where(field => definition.SectionOf(field.Id) is { } section && editable.Contains(section.Id)))
        {
            merged.Remove(field.Id);
            if (incoming[field.Id] is { } value)
            {
                merged[field.Id] = value.DeepClone();
            }
        }

        // Ta'rifda yo'q kalitlar validatorga yetib boradi — u ularni rad etadi.
        foreach (var (key, value) in incoming.Where(pair => definition.FindField(pair.Key) is null))
        {
            merged[key] = value?.DeepClone();
        }

        return merged;
    }
}

public sealed class SubmitBlankSubmissionCommandHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly IBlankNumberCounter _numbers;
    private readonly IBlankReadModel _readModel;
    private readonly BlankActorResolver _actors;
    private readonly BlankSubmissionDetails _details;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlankApprovedHook _approved;

    public SubmitBlankSubmissionCommandHandler(
        IBlankSubmissionRepository submissions,
        IBlankNumberCounter numbers,
        IBlankReadModel readModel,
        BlankActorResolver actors,
        BlankSubmissionDetails details,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork,
        IBlankApprovedHook approved)
    {
        _submissions = submissions;
        _numbers = numbers;
        _readModel = readModel;
        _actors = actors;
        _details = details;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _approved = approved;
    }

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        SubmitBlankSubmissionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        if (!caller.CanFill)
        {
            return BlankErrors.Forbidden;
        }

        Result submitted;
        try
        {
            // Raqam hisoblagichi qatori COMMIT gacha qulflanadi — raqamlar ketma-ket va bo'shliqsiz (§3.7).
            submitted = await _unitOfWork
                .ExecuteInTransactionAsync(token => SubmitAsync(command, token), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleBlankWrite(exception))
        {
            return BlankErrors.Stale;
        }

        if (submitted.IsFailure)
        {
            return submitted.Error;
        }

        // No steps (or all skipped) → Approved at submit: the final PDF hook runs after the commit, as for step actions.
        var detail = await _details.GetAsync(command.SubmissionId, cancellationToken).ConfigureAwait(false);
        if (detail.IsSuccess && detail.Value.Status == nameof(BlankSubmissionStatus.Approved))
        {
            await _approved.OnApprovedAsync(command.SubmissionId, cancellationToken).ConfigureAwait(false);
        }

        return detail;
    }

    private async Task<Result> SubmitAsync(SubmitBlankSubmissionCommand command, CancellationToken cancellationToken)
    {
        var submission = await _submissions.GetWithChildrenAsync(command.SubmissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        if (submission.CheckVersion(command.ExpectedVersion) is { IsFailure: true } stale)
        {
            return stale;
        }

        if (submission.Status != BlankSubmissionStatus.Draft)
        {
            return BlankErrors.InvalidStatus;
        }

        var definition = await _details.DefinitionAsync(submission.TemplateVersionId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        var validated = BlankAnswersValidator.Validate(
            definition,
            BlankSubmissionRules.ParseObject(submission.Answers),
            new BlankAnswersOptions(_clock.Today, definition.DraftSectionIds()));
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        if (await _readModel.FirstMissingReferenceAsync(validated.Value.References, submission.Id, cancellationToken)
                .ConfigureAwait(false) is { } missingPath)
        {
            return BlankSubmissionRules.MissingReference(missingPath);
        }

        var plan = BlankSubmissionRules.Plan(definition, validated.Value.Answers, validated.Value.Computed);
        var state = BlankSubmissionRules.BuildState(definition, validated.Value, BlankSubmissionRules.Preview(plan));

        int? number = submission.Number is null
            ? await _numbers.NextAsync(submission.TemplateCode, cancellationToken).ConfigureAwait(false)
            : null;

        var result = submission.Submit(number, plan, state, _clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed class CancelBlankSubmissionCommandHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly BlankActorResolver _actors;
    private readonly BlankSubmissionDetails _details;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelBlankSubmissionCommandHandler(
        IBlankSubmissionRepository submissions,
        BlankActorResolver actors,
        BlankSubmissionDetails details,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissions = submissions;
        _actors = actors;
        _details = details;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        CancelBlankSubmissionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var submission = await _submissions.GetByIdAsync(command.SubmissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        if (submission.CheckVersion(command.ExpectedVersion) is { IsFailure: true } stale)
        {
            return stale.Error;
        }

        // Qoralama: o'zinikini — blanks.fill, boshqanikini — blanks.manage; kelishuvdagini — faqat manage (§5).
        var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
        var allowed = submission.Status switch
        {
            BlankSubmissionStatus.Draft => caller.CanManage || (caller.CanFill && submission.CreatedBy == caller.UserId),
            BlankSubmissionStatus.InApproval => caller.CanManage,
            _ => true, // yakuniy holat — domen «holat_notogri» qaytaradi
        };
        if (!allowed)
        {
            return BlankErrors.Forbidden;
        }

        var cancelled = submission.Cancel(command.Reason, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleBlankWrite(exception))
        {
            return BlankErrors.Stale;
        }

        return await _details.GetAsync(submission.Id, cancellationToken).ConfigureAwait(false);
    }
}
