using System.Globalization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Pdf;

/// <summary>A rendered blank PDF.</summary>
/// <param name="Content">PDF bytes.</param>
/// <param name="FileName">Download name: <c>HR-F06-12.pdf</c>, or <c>HR-F06-qoralama.pdf</c> before a number exists.</param>
public sealed record BlankPdfDocument(byte[] Content, string FileName);

/// <summary>
/// Assembles <see cref="BlankPdfModel"/> from a submission (SPEC-blankalar §10): stored answers and
/// computed values, steps with signer names and in-person signature PNGs from storage, live
/// attachment names. Reads; never writes.
/// </summary>
public sealed class BlankPdfModelBuilder
{
    private readonly IBlankReadModel _readModel;
    private readonly BlankSubmissionDetails _details;
    private readonly IFileStorage _storage;
    private readonly IClock _clock;

    public BlankPdfModelBuilder(IBlankReadModel readModel, BlankSubmissionDetails details, IFileStorage storage, IClock clock)
    {
        _readModel = readModel;
        _details = details;
        _storage = storage;
        _clock = clock;
    }

    /// <param name="submission">The aggregate with its steps and attachments loaded.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Result<BlankPdfModel>> BuildAsync(BlankSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var definition = await _details.DefinitionAsync(submission.TemplateVersionId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        // Display names (subject, department, «who clicked») are resolved by the read model in one query.
        var view = await _readModel.GetAsync(submission.Id, cancellationToken).ConfigureAwait(false);
        if (view is null)
        {
            return BlankErrors.NotFound;
        }

        var actedByNames = view.Steps.ToDictionary(step => step.Id, step => step.ActedByName);
        var steps = new List<BlankPdfStep>(submission.Steps.Count);
        foreach (var step in submission.Steps.OrderBy(step => step.OrderNo).ThenBy(step => step.StepCode, StringComparer.Ordinal))
        {
            steps.Add(new BlankPdfStep(step.StepCode, step.Title, step.ActorRole, step.State)
            {
                Decision = step.Decision,
                Note = step.Comment,
                SignerName = step.SignerName ?? actedByNames.GetValueOrDefault(step.Id),
                ActedAt = step.ActedAt,
                Method = step.Method,
                SignaturePng = step.SignatureStorageKey is { } key
                    ? await ReadAsync(key, cancellationToken).ConfigureAwait(false)
                    : null,
            });
        }

        return new BlankPdfModel
        {
            Definition = definition,
            Answers = BlankSubmissionRules.ParseObject(submission.Answers),
            Computed = BlankSubmissionRules.ParseObject(submission.Computed),
            Number = submission.Number,
            Status = submission.Status,
            DocumentDate = submission.SubmittedAt?.TashkentDate(),
            SubjectName = view.Subject.FullName,
            DepartmentName = view.DepartmentName,
            Steps = steps,
            Attachments = submission.Attachments
                .Where(file => file.IsActive)
                .Select(file => new BlankPdfAttachment(file.Id, file.FieldPath, file.FileName, file.Kind, file.SizeBytes))
                .ToList(),
            GeneratedAt = _clock.UtcNow,
        };
    }

    /// <summary>Download name of the submission's PDF.</summary>
    public static string FileNameOf(BlankSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        return submission.Number is { } number
            ? $"{submission.TemplateCode}-{number.ToString(CultureInfo.InvariantCulture)}.pdf"
            : $"{submission.TemplateCode}-qoralama.pdf";
    }

    /// <summary>A missing signature file prints as an empty cell rather than failing the whole PDF.</summary>
    private async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var stream = await _storage.OpenAsync(key, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }
    }
}

/// <summary>§7 PDF endpoint — renders the submission fresh, in any state.</summary>
public sealed class GetBlankPdfQueryHandler
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly BlankPdfModelBuilder _builder;
    private readonly IBlankPdfRenderer _renderer;

    public GetBlankPdfQueryHandler(IBlankSubmissionRepository submissions, BlankPdfModelBuilder builder, IBlankPdfRenderer renderer)
    {
        _submissions = submissions;
        _builder = builder;
        _renderer = renderer;
    }

    public async Task<Result<BlankPdfDocument>> HandleAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _submissions.GetWithChildrenAsync(submissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        var model = await _builder.BuildAsync(submission, cancellationToken).ConfigureAwait(false);
        if (model.IsFailure)
        {
            return model.Error;
        }

        var bytes = await _renderer.RenderAsync(model.Value, cancellationToken).ConfigureAwait(false);
        return new BlankPdfDocument(bytes, BlankPdfModelBuilder.FileNameOf(submission));
    }
}

/// <summary>
/// The final PDF snapshot of an Approved blank (SPEC-blankalar §5, §10): renders, stores via
/// <see cref="IFileStorage"/> and records the key in <c>final_pdf_storage_key</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Caller contract (steps slice).</b> Call AFTER the approval itself is committed, in its own
/// try/catch that logs: a PDF failure must never undo or block the approval — on-demand rendering
/// still works without the snapshot. Rendering and storage exceptions propagate to that catch.
/// </para>
/// <para>
/// Not idempotent by design: a second call replaces the key with a fresh snapshot and deletes the
/// previous file after the commit.
/// </para>
/// </remarks>
public sealed class BlankFinalPdfService
{
    private readonly IBlankSubmissionRepository _submissions;
    private readonly BlankPdfModelBuilder _builder;
    private readonly IBlankPdfRenderer _renderer;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;

    public BlankFinalPdfService(
        IBlankSubmissionRepository submissions,
        BlankPdfModelBuilder builder,
        IBlankPdfRenderer renderer,
        IFileStorage storage,
        IUnitOfWork unitOfWork)
    {
        _submissions = submissions;
        _builder = builder;
        _renderer = renderer;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Renders and stores the final PDF of an Approved submission.</summary>
    /// <returns>The storage key; <c>blanka.topilmadi</c> / <c>blanka.holat_notogri</c> (not Approved) on failure.</returns>
    public async Task<Result<string>> StoreFinalPdfAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _submissions.GetWithChildrenAsync(submissionId, cancellationToken).ConfigureAwait(false);
        if (submission is null)
        {
            return BlankErrors.NotFound;
        }

        if (submission.Status != BlankSubmissionStatus.Approved)
        {
            return BlankErrors.InvalidStatus;
        }

        var model = await _builder.BuildAsync(submission, cancellationToken).ConfigureAwait(false);
        if (model.IsFailure)
        {
            return model.Error;
        }

        var bytes = await _renderer.RenderAsync(model.Value, cancellationToken).ConfigureAwait(false);
        StoredFile stored;
        await using (var content = new MemoryStream(bytes, writable: false))
        {
            stored = await _storage
                .SaveAsync(content, BlankPdfModelBuilder.FileNameOf(submission), "application/pdf", cancellationToken)
                .ConfigureAwait(false);
        }

        var previous = submission.FinalPdfStorageKey;
        var set = submission.SetFinalPdf(stored.Key);
        if (set.IsFailure)
        {
            await _storage.DeleteAsync(stored.Key, CancellationToken.None).ConfigureAwait(false);
            return set.Error;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _storage.DeleteAsync(stored.Key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (previous is not null)
        {
            await _storage.DeleteAsync(previous, CancellationToken.None).ConfigureAwait(false);
        }

        return stored.Key;
    }
}
