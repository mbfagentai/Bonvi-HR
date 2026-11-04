using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Steps;

/// <summary>
/// Elektron tasdiqlash (§7 #12): lavozim roli — «blanks.approve» (+ biriktirilgan bo'lsa shu foydalanuvchi);
/// shaxs roli — <c>users.employee_id</c> aniqlangan xodimga teng (§6.2).
/// </summary>
public sealed class ApproveBlankStepCommandHandler
{
    private readonly BlankStepActions _actions;

    public ApproveBlankStepCommandHandler(BlankStepActions actions) => _actions = actions;

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        ApproveBlankStepCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Act);

        var loaded = await _actions
            .LoadAsync(command.SubmissionId, command.StepCode, command.Act.ExpectedVersion, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var context = loaded.Value;
        if (!context.Ability.Electronic)
        {
            return BlankErrors.StepForbidden;
        }

        var prepared = await _actions.PrepareAsync(context, command.Act, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return prepared.Error;
        }

        // Shaxs rolida imzolovchi — aniqlangan xodim (= chaqiruvchining xodimi); lavozim rolida — chaqiruvchining xodimi (bo'lsa).
        var signer = BlankActorRoles.IsOfficeRole(context.Step.ActorRole)
            ? context.Caller.EmployeeId
            : await _actions.ResolvePersonAsync(context, cancellationToken).ConfigureAwait(false);

        var act = new BlankStepAct(
            BlankSignMethod.Electronic,
            context.Caller.UserId,
            signer,
            await _actions.EmployeeNameAsync(signer, cancellationToken).ConfigureAwait(false),
            Decision: prepared.Value.Decision,
            Comment: prepared.Value.Comment,
            AnswersPatch: prepared.Value.AnswersPatch);

        return await _actions.SignAsync(context, prepared.Value, act, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// HR qurilmasida qo'lda imzo (§7 #13): operator — «blanks.fill»; imzo PNG oq fonda qayta chiziladi
/// (anketa qoidasi: ≤ 300 KB, ≤ 1200×600); guvohda ism majburiy.
/// </summary>
public sealed class SignBlankStepInPersonCommandHandler
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly BlankStepActions _actions;
    private readonly IFileContentInspector _inspector;
    private readonly IImageProcessor _images;
    private readonly IFileStorage _storage;

    public SignBlankStepInPersonCommandHandler(
        BlankStepActions actions,
        IFileContentInspector inspector,
        IImageProcessor images,
        IFileStorage storage)
    {
        _actions = actions;
        _inspector = inspector;
        _images = images;
        _storage = storage;
    }

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        SignBlankStepInPersonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payload = ReadPayload(command.Payload);
        if (payload is null)
        {
            return BlankErrors.InvalidJson;
        }

        var version = payload.ExpectedVersion.GetValueOrDefault();
        var loaded = await _actions
            .LoadAsync(command.SubmissionId, command.StepCode, version, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var context = loaded.Value;
        if (!context.Ability.InPerson)
        {
            return BlankErrors.StepForbidden;
        }

        var isWitness = context.Step.ActorRole == BlankActorRoles.Witness;
        var typedName = string.IsNullOrWhiteSpace(payload.SignerName) ? null : payload.SignerName.Trim();
        if (isWitness && typedName is null)
        {
            return BlankErrors.SignerRequired;
        }

        var prepared = await _actions
            .PrepareAsync(
                context,
                new BlankStepActInput(version, payload.Decision, payload.DecisionExtra, payload.Comment, payload.Answers),
                cancellationToken)
            .ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return prepared.Error;
        }

        var signature = await ReadSignatureAsync(command, cancellationToken).ConfigureAwait(false);
        if (signature.IsFailure)
        {
            return signature.Error;
        }

        var signer = await _actions.ResolvePersonAsync(context, cancellationToken).ConfigureAwait(false);
        var signerName = typedName ?? await _actions.EmployeeNameAsync(signer, cancellationToken).ConfigureAwait(false);

        using var stream = new MemoryStream(signature.Value, writable: false);
        var stored = await _storage.SaveAsync(stream, "imzo.png", "image/png", cancellationToken).ConfigureAwait(false);

        var act = new BlankStepAct(
            BlankSignMethod.InPerson,
            context.Caller.UserId,
            signer,
            signerName,
            stored.Key,
            prepared.Value.Decision,
            prepared.Value.Comment,
            prepared.Value.AnswersPatch);

        Result<BlankSubmissionDetailDto> result;
        try
        {
            result = await _actions.SignAsync(context, prepared.Value, act, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _storage.DeleteAsync(stored.Key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (result.IsFailure)
        {
            await _storage.DeleteAsync(stored.Key, CancellationToken.None).ConfigureAwait(false);
        }

        return result;
    }

    private static InPersonPayload? ReadPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InPersonPayload>(json, PayloadOptions) is { ExpectedVersion: not null } payload
                ? payload
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Mazmun bo'yicha PNG (sarlavhaga ishonilmaydi), so'ng oq fonda qayta chiziladi — anketa imzosi bilan aynan bir
    /// qoida (<see cref="SubmitQuestionnaireFormCommandHandler"/>): shaffoflik/metadata yo'qoladi, katta rasm kichrayadi.
    /// </summary>
    private async Task<Result<byte[]>> ReadSignatureAsync(SignBlankStepInPersonCommand command, CancellationToken cancellationToken)
    {
        if (command.Signature is null || command.SignatureSizeBytes <= 0)
        {
            return BlankErrors.SignatureRequired;
        }

        if (command.SignatureSizeBytes > SubmitQuestionnaireFormCommandHandler.MaxSignatureBytes)
        {
            return BlankErrors.SignatureInvalid;
        }

        using var buffer = new MemoryStream();
        await command.Signature.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();

        if (bytes.Length is 0 or > (int)SubmitQuestionnaireFormCommandHandler.MaxSignatureBytes
            || _inspector.Detect(bytes) != DetectedFileKind.Png)
        {
            return BlankErrors.SignatureInvalid;
        }

        using var source = new MemoryStream(bytes, writable: false);
        var flat = await _images
            .ToFlatPngAsync(
                source,
                SubmitQuestionnaireFormCommandHandler.MaxSignatureWidth,
                SubmitQuestionnaireFormCommandHandler.MaxSignatureHeight,
                cancellationToken)
            .ConfigureAwait(false);
        if (flat is null)
        {
            return BlankErrors.SignatureInvalid;
        }

        await using (flat.Content.ConfigureAwait(false))
        {
            using var output = new MemoryStream();
            await flat.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return output.ToArray();
        }
    }

    private sealed record InPersonPayload(
        int? ExpectedVersion,
        string? Decision,
        JsonNode? DecisionExtra,
        string? Comment,
        JsonNode? Answers,
        string? SignerName);
}

/// <summary>
/// Qoralamaga qaytarish (§7 #14): qadam bajaruvchisi (elektron huquqi bilan), qadamda <c>canReturn</c>.
/// Qadam qatorlari o'chadi (amallari audit jurnalida qoladi), raqam saqlanadi, izoh <c>return_comment</c> ga.
/// </summary>
public sealed class ReturnBlankStepCommandHandler
{
    private readonly BlankStepActions _actions;

    public ReturnBlankStepCommandHandler(BlankStepActions actions) => _actions = actions;

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        ReturnBlankStepCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _actions
            .LoadAsync(command.SubmissionId, command.StepCode, command.ExpectedVersion, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var context = loaded.Value;
        if (!context.Ability.Electronic)
        {
            return BlankErrors.StepForbidden;
        }

        if (!context.DefinitionStep.CanReturn)
        {
            return BlankErrors.ReturnNotAllowed;
        }

        var returned = context.Submission.ReturnToDraft(context.Step.Id, command.Comment);
        return returned.IsFailure
            ? returned.Error
            : await _actions.SaveAsync(context, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Rad etish (§7 #15): qadam bajaruvchisi (elektron huquqi bilan), qadamda <c>canReject</c>; blanka — Rejected.</summary>
public sealed class RejectBlankStepCommandHandler
{
    private readonly BlankStepActions _actions;

    public RejectBlankStepCommandHandler(BlankStepActions actions) => _actions = actions;

    public async Task<Result<BlankSubmissionDetailDto>> HandleAsync(
        RejectBlankStepCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await _actions
            .LoadAsync(command.SubmissionId, command.StepCode, command.ExpectedVersion, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var context = loaded.Value;
        if (!context.Ability.Electronic)
        {
            return BlankErrors.StepForbidden;
        }

        if (!context.DefinitionStep.CanReject)
        {
            return BlankErrors.RejectNotAllowed;
        }

        var signer = BlankActorRoles.IsOfficeRole(context.Step.ActorRole)
            ? context.Caller.EmployeeId
            : await _actions.ResolvePersonAsync(context, cancellationToken).ConfigureAwait(false);

        var rejected = context.Submission.Reject(context.Step.Id, context.Caller.UserId, signer, command.Comment, _actions.Now);
        return rejected.IsFailure
            ? rejected.Error
            : await _actions.SaveAsync(context, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Qadam imzosi rasmi (§7 #16) — faqat qo'lda imzolangan qadamda bor.</summary>
public sealed class GetBlankStepSignatureQueryHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly IFileStorage _storage;

    public GetBlankStepSignatureQueryHandler(IBlankSubmissionRepository submissions, IFileStorage storage)
    {
        _submissions = submissions;
        _storage = storage;
    }

    public async Task<Result<BlankStepSignatureFile>> HandleAsync(
        Guid submissionId,
        string stepCode,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissions.GetWithChildrenAsync(submissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        var step = submission.Steps.FirstOrDefault(item => string.Equals(item.StepCode, stepCode, StringComparison.Ordinal));
        if (step is null)
        {
            return BlankErrors.StepNotFound;
        }

        if (step.SignatureStorageKey is not { } key
            || await _storage.OpenAsync(key, cancellationToken).ConfigureAwait(false) is not { } content)
        {
            return BlankErrors.FileNotFound;
        }

        return new BlankStepSignatureFile(content);
    }
}
