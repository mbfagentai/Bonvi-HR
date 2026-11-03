using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;
using Bonvi.Domain.Blanks;

namespace Bonvi.Application.Blanks.Pdf;

/// <summary>
/// Everything <see cref="IBlankPdfRenderer"/> prints — plain data, assembled by the caller.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Answers"/> are the stored (cleaned) answers keyed by field id; <see cref="Computed"/> is the
/// stored computed object keyed by path (<c>avgScore</c>, <c>testResults[0].testPassed</c>) — the renderer
/// prints them, it does not re-evaluate expressions.
/// </para>
/// <para>
/// <see cref="Steps"/> are the materialized submission steps. When empty (a Draft has none) the
/// signatures block is printed from the definition's steps with blank name/date cells, like the paper form.
/// </para>
/// </remarks>
public sealed record BlankPdfModel
{
    /// <summary>Parsed definition of the submission's template version.</summary>
    public required BlankDefinition Definition { get; init; }

    /// <summary>Stored answers (<c>blank_submissions.answers</c>).</summary>
    public required JsonObject Answers { get; init; }

    /// <summary>Stored computed values (<c>blank_submissions.computed</c>).</summary>
    public JsonObject Computed { get; init; } = new();

    /// <summary>Submission number; <c>null</c> before the first submit.</summary>
    public int? Number { get; init; }

    /// <summary>Submission status, printed in the provenance line.</summary>
    public BlankSubmissionStatus Status { get; init; } = BlankSubmissionStatus.Draft;

    /// <summary>Document date in the info row («Sana») — usually the Tashkent date of the first submit.</summary>
    public DateOnly? DocumentDate { get; init; }

    /// <summary>Subject display name (employee / candidate full name); ignored for <c>subject: none</c>.</summary>
    public string? SubjectName { get; init; }

    /// <summary>Department display name for the info row.</summary>
    public string? DepartmentName { get; init; }

    /// <summary>Materialized steps in display order.</summary>
    public IReadOnlyList<BlankPdfStep> Steps { get; init; } = [];

    /// <summary>Live (not removed) attachments of the submission.</summary>
    public IReadOnlyList<BlankPdfAttachment> Attachments { get; init; } = [];

    /// <summary>Moment of rendering («chop etildi»), printed in Tashkent time.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>One row of the «Imzolar» table.</summary>
/// <param name="StepId">Definition step id — resolves the decision label.</param>
/// <param name="Title">Step title (snapshot from the step row).</param>
/// <param name="ActorRole">Actor role code (<c>hr1</c>, <c>departmentHead</c>, <c>field:mentor</c>).</param>
/// <param name="State">Step state.</param>
public sealed record BlankPdfStep(string StepId, string Title, string ActorRole, BlankStepState State)
{
    /// <summary>Display override for the actor column; <c>null</c> → derived from <see cref="ActorRole"/>.</summary>
    public string? ActorLabel { get; init; }

    /// <summary>Decision option value (resolved to its label from the definition).</summary>
    public string? Decision { get; init; }

    /// <summary>Decision extra text or the step comment, printed after the decision.</summary>
    public string? Note { get; init; }

    /// <summary>Signer full name (typed name for witnesses).</summary>
    public string? SignerName { get; init; }

    /// <summary>When the step was signed / rejected.</summary>
    public DateTimeOffset? ActedAt { get; init; }

    /// <summary>How the step was signed; <c>null</c> while not signed.</summary>
    public BlankSignMethod? Method { get; init; }

    /// <summary>In-person signature image (PNG); printed instead of «Elektron tasdiqlandi».</summary>
    public byte[]? SignaturePng { get; init; }
}

/// <summary>Attachment metadata — the file itself is never embedded, only listed.</summary>
/// <param name="Id">Attachment id (as stored in the file field's answer).</param>
/// <param name="FieldPath">Field path the file belongs to (<c>evidence</c>, <c>tasks[2].file</c>).</param>
/// <param name="FileName">Original file name.</param>
/// <param name="Kind">Detected file kind.</param>
/// <param name="SizeBytes">File size.</param>
public sealed record BlankPdfAttachment(Guid Id, string FieldPath, string FileName, BlankAttachmentKind Kind, long SizeBytes);
