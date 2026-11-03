namespace Bonvi.Application.Blanks.Pdf;

/// <summary>
/// Generic, definition-driven PDF of one blank submission (SPEC-blankalar §10).
/// </summary>
/// <remarks>
/// One renderer for every template: sections, fields and steps come from the definition carried by
/// <see cref="BlankPdfModel"/>, never from per-template code. The caller builds the model (answers,
/// computed values, materialized steps with signer names and signature PNGs, attachment metadata);
/// the renderer reads nothing from the database or storage.
/// </remarks>
public interface IBlankPdfRenderer
{
    /// <summary>Renders the submission as an A4 PDF document.</summary>
    /// <returns>The PDF bytes.</returns>
    Task<byte[]> RenderAsync(BlankPdfModel model, CancellationToken cancellationToken = default);
}
