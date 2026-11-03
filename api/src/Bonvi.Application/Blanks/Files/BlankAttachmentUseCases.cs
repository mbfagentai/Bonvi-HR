using System.Security.Cryptography;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Files;

/// <summary>Attach a file to a file field (SPEC-blankalar §7 #17, §8).</summary>
/// <param name="SubmissionId">Blank.</param>
/// <param name="FieldPath"><c>evidence</c> or <c>handover[2].doc</c>.</param>
/// <param name="Content">Uploaded bytes (read once, bounded by the field limit).</param>
/// <param name="FileName">Client file name — display only; the extension comes from the detected kind.</param>
/// <param name="SizeBytes">Declared size (multipart part length) — checked before reading.</param>
public sealed record UploadBlankAttachmentCommand(
    Guid SubmissionId,
    string? FieldPath,
    Stream Content,
    string? FileName,
    long SizeBytes);

/// <summary>Remove an attachment (§7 #18).</summary>
public sealed record RemoveBlankAttachmentCommand(Guid SubmissionId, Guid AttachmentId, int ExpectedVersion);

/// <summary>An attachment ready to stream (§7 #19).</summary>
/// <param name="Content">File stream — the caller disposes it.</param>
/// <param name="ContentType">MIME type of the detected kind.</param>
/// <param name="FileName">Stored display name.</param>
/// <param name="Inline">PDF and images open in the browser; Office files always download.</param>
public sealed record BlankAttachmentContent(Stream Content, string ContentType, string FileName, bool Inline);

/// <summary>
/// #17 — checks the field (exists, is a file field, its section editable for the caller), the field
/// limits (size, count, kind by CONTENT), stores the bytes, adds the attachment and links its id into
/// the field's answer, so completeness (<c>missing</c>) sees the file at once.
/// </summary>
public sealed class UploadBlankAttachmentCommandHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly BlankSubmissionDetails _details;
    private readonly BlankFileAccess _access;
    private readonly IBlankFileInspector _inspector;
    private readonly IFileStorage _storage;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UploadBlankAttachmentCommandHandler(
        IBlankSubmissionRepository submissions,
        BlankSubmissionDetails details,
        BlankFileAccess access,
        IBlankFileInspector inspector,
        IFileStorage storage,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissions = submissions;
        _details = details;
        _access = access;
        _inspector = inspector;
        _storage = storage;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BlankAttachmentDto>> HandleAsync(
        UploadBlankAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Cheap checks first: nothing is read from the request body until the field accepts a file.
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

        if (submission.Status is not (BlankSubmissionStatus.Draft or BlankSubmissionStatus.InApproval))
        {
            return BlankErrors.InvalidStatus;
        }

        var path = BlankFilePath.Resolve(definition, command.FieldPath);
        if (path is null)
        {
            return BlankAnswerErrors.Invalid.At(command.FieldPath?.Trim() ?? string.Empty);
        }

        var editable = await _access.EditableSectionsAsync(submission, definition, cancellationToken).ConfigureAwait(false);
        if (editable.Count == 0)
        {
            return BlankErrors.Forbidden;
        }

        if (definition.SectionOf(path.RootFieldId) is not { } section || !editable.Contains(section.Id))
        {
            return BlankErrors.FieldNotEditable;
        }

        if (command.SizeBytes > path.MaxBytes)
        {
            return BlankErrors.FileTooLarge;
        }

        if (submission.Attachments.Count(file => file.IsActive && file.FieldPath == path.Path) >= path.Field.EffectiveMaxFiles)
        {
            return BlankErrors.FileCount;
        }

        var bytes = await ReadBoundedAsync(command.Content, path.MaxBytes, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return BlankErrors.FileTooLarge;
        }

        if (bytes.Length == 0)
        {
            return BlankErrors.FileEmpty;
        }

        var inspection = _inspector.Inspect(bytes);
        if (inspection.Kind is not { } kind)
        {
            return BlankFileAccess.ErrorFor(inspection.Rejection ?? BlankFileRejection.Unrecognized);
        }

        if (!BlankFileKinds.IsAccepted(kind, path.Accept))
        {
            return BlankErrors.FileKind;
        }

        if (await _access.UserIdAsync(cancellationToken).ConfigureAwait(false) is not { } userId)
        {
            return BlankErrors.Forbidden;
        }

        var fileName = BlankFileAccess.SafeFileName(command.FileName, kind);
        var contentType = BlankFileKinds.ContentTypeFor(kind);
        StoredFile stored;
        await using (var toSave = new MemoryStream(bytes, writable: false))
        {
            // The storage name is built from the detected kind only — the client name never reaches a path.
            stored = await _storage
                .SaveAsync(toSave, "blanka" + BlankFileKinds.ExtensionFor(kind), contentType, cancellationToken)
                .ConfigureAwait(false);
        }

        var added = submission.AddAttachment(
            new BlankAttachmentFile(
                path.Path,
                kind.ToAttachmentKind(),
                fileName,
                contentType,
                bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                stored.Key),
            userId,
            _clock.UtcNow);
        if (added.IsFailure)
        {
            await _storage.DeleteAsync(stored.Key, cancellationToken).ConfigureAwait(false);
            return added.Error;
        }

        var answers = BlankSubmissionRules.ParseObject(submission.Answers);
        var linked = path.Link(answers, added.Value.Id)
            ? submission.SaveAnswers(BlankFileAccess.StateAfterFileChange(submission, definition, answers))
            : Result.Failure(BlankAnswerErrors.Invalid.At(path.Path));
        if (linked.IsFailure)
        {
            await _storage.DeleteAsync(stored.Key, cancellationToken).ConfigureAwait(false);
            return linked.Error;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleBlankWrite(exception))
        {
            await _storage.DeleteAsync(stored.Key, CancellationToken.None).ConfigureAwait(false);
            return BlankErrors.Stale;
        }
        catch
        {
            // No orphan file on disk when the row was not written.
            await _storage.DeleteAsync(stored.Key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var attachment = added.Value;
        return new BlankAttachmentDto(
            attachment.Id,
            attachment.FieldPath,
            attachment.Kind.ToString(),
            attachment.FileName,
            attachment.SizeBytes,
            attachment.UploadedAt);
    }

    /// <summary>Reads at most <paramref name="limit"/> bytes; <c>null</c> when the stream is longer.</summary>
    private static async Task<byte[]?> ReadBoundedAsync(Stream content, long limit, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

/// <summary>
/// #18 — marks the attachment removed and unlinks it from the answer while its field is editable;
/// the file is deleted from disk only after the commit.
/// </summary>
public sealed class RemoveBlankAttachmentCommandHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly BlankSubmissionDetails _details;
    private readonly BlankFileAccess _access;
    private readonly IFileStorage _storage;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveBlankAttachmentCommandHandler(
        IBlankSubmissionRepository submissions,
        BlankSubmissionDetails details,
        BlankFileAccess access,
        IFileStorage storage,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _submissions = submissions;
        _details = details;
        _access = access;
        _storage = storage;
        _fault = fault;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(RemoveBlankAttachmentCommand command, CancellationToken cancellationToken = default)
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

        var attachment = submission.Attachments.FirstOrDefault(file => file.Id == command.AttachmentId && file.IsActive);
        if (attachment is null)
        {
            return BlankErrors.FileNotFound;
        }

        if (submission.CheckVersion(command.ExpectedVersion) is { IsFailure: true } stale)
        {
            return stale.Error;
        }

        if (submission.Status is not (BlankSubmissionStatus.Draft or BlankSubmissionStatus.InApproval))
        {
            return BlankErrors.InvalidStatus;
        }

        var editable = await _access.EditableSectionsAsync(submission, definition, cancellationToken).ConfigureAwait(false);
        if (editable.Count == 0)
        {
            return BlankErrors.Forbidden;
        }

        var path = BlankFilePath.Resolve(definition, attachment.FieldPath);
        if (path is null || definition.SectionOf(path.RootFieldId) is not { } section || !editable.Contains(section.Id))
        {
            return BlankErrors.FieldNotEditable;
        }

        if (await _access.UserIdAsync(cancellationToken).ConfigureAwait(false) is not { } userId)
        {
            return BlankErrors.Forbidden;
        }

        var removed = submission.RemoveAttachment(attachment.Id, userId, _clock.UtcNow);
        if (removed.IsFailure)
        {
            return removed.Error;
        }

        var answers = BlankSubmissionRules.ParseObject(submission.Answers);
        path.Unlink(answers, attachment.Id);
        var saved = submission.SaveAnswers(BlankFileAccess.StateAfterFileChange(submission, definition, answers));
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

        // After the commit: a failed delete leaves an unreferenced file, never a row without its file.
        await _storage.DeleteAsync(attachment.StorageKey, CancellationToken.None).ConfigureAwait(false);
        return Result.Success();
    }
}

/// <summary>#19 — streams a live attachment with its detected content type.</summary>
public sealed class GetBlankAttachmentFileQueryHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly IFileStorage _storage;

    public GetBlankAttachmentFileQueryHandler(IBlankSubmissionRepository submissions, IFileStorage storage)
    {
        _submissions = submissions;
        _storage = storage;
    }

    public async Task<Result<BlankAttachmentContent>> HandleAsync(
        Guid submissionId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissions.GetWithChildrenAsync(submissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        var attachment = submission.Attachments.FirstOrDefault(file => file.Id == attachmentId && file.IsActive);
        if (attachment is null)
        {
            return BlankErrors.FileNotFound;
        }

        var stream = await _storage.OpenAsync(attachment.StorageKey, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return BlankErrors.FileNotFound;
        }

        var kind = attachment.Kind.ToFileKind();
        return new BlankAttachmentContent(
            stream,
            BlankFileKinds.ContentTypeFor(kind),
            attachment.FileName,
            Inline: BlankFileKinds.GroupOf(kind) != BlankFileAccept.Office);
    }
}
