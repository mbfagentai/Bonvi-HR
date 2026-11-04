using Bonvi.Domain.Blanks;

namespace Bonvi.Application.Blanks.Files;

/// <summary>
/// The detected <see cref="BlankFileKind"/> and the stored <see cref="BlankAttachmentKind"/> are the
/// same seven kinds with the same numbers (SPEC-blankalar §3.5, §8); this is the one place that converts.
/// </summary>
/// <remarks>
/// An explicit switch rather than a numeric cast: adding a kind to only one enum must fail here
/// (and in the unit test) instead of silently storing a wrong <c>kind</c>.
/// </remarks>
public static class BlankAttachmentKindMapping
{
    /// <summary>Detected kind → the value stored in <c>blank_attachments.kind</c>.</summary>
    public static BlankAttachmentKind ToAttachmentKind(this BlankFileKind kind) => kind switch
    {
        BlankFileKind.Pdf => BlankAttachmentKind.Pdf,
        BlankFileKind.Jpeg => BlankAttachmentKind.Jpeg,
        BlankFileKind.Png => BlankAttachmentKind.Png,
        BlankFileKind.Webp => BlankAttachmentKind.Webp,
        BlankFileKind.Docx => BlankAttachmentKind.Docx,
        BlankFileKind.Xlsx => BlankAttachmentKind.Xlsx,
        BlankFileKind.Pptx => BlankAttachmentKind.Pptx,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blank file kind."),
    };

    /// <summary>Stored kind → the file kind (content type, extension, accept group).</summary>
    public static BlankFileKind ToFileKind(this BlankAttachmentKind kind) => kind switch
    {
        BlankAttachmentKind.Pdf => BlankFileKind.Pdf,
        BlankAttachmentKind.Jpeg => BlankFileKind.Jpeg,
        BlankAttachmentKind.Png => BlankFileKind.Png,
        BlankAttachmentKind.Webp => BlankFileKind.Webp,
        BlankAttachmentKind.Docx => BlankFileKind.Docx,
        BlankAttachmentKind.Xlsx => BlankFileKind.Xlsx,
        BlankAttachmentKind.Pptx => BlankFileKind.Pptx,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blank attachment kind."),
    };
}
