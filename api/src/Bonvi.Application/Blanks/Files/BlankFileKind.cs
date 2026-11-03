namespace Bonvi.Application.Blanks.Files;

/// <summary>The real type of a blank attachment, detected from its CONTENT (SPEC-blankalar §8).</summary>
/// <remarks>
/// Never derived from the client's file name or <c>Content-Type</c>: those are only hints and are
/// ignored. The stored extension and MIME type come from this value (<see cref="BlankFileKinds"/>).
/// </remarks>
public enum BlankFileKind
{
    Pdf = 1,
    Jpeg = 2,
    Png = 3,
    Webp = 4,
    Docx = 5,
    Xlsx = 6,
    Pptx = 7,
}

/// <summary>
/// The <c>accept</c> key of a file field (SPEC-blankalar §8): which groups of kinds the field takes.
/// </summary>
/// <remarks>
/// <c>any</c> means «all three groups», not «any binary» — arbitrary files are never accepted.
/// </remarks>
[Flags]
public enum BlankFileAccept
{
    None = 0,

    /// <summary><c>pdf</c> — <see cref="BlankFileKind.Pdf"/>.</summary>
    Pdf = 1,

    /// <summary><c>image</c> — JPEG, PNG, WebP.</summary>
    Image = 2,

    /// <summary><c>office</c> — DOCX, XLSX, PPTX (macro-free OOXML only).</summary>
    Office = 4,

    /// <summary><c>any</c> — every group above.</summary>
    Any = Pdf | Image | Office,
}

/// <summary>Canonical facts about each <see cref="BlankFileKind"/> — kept in one place.</summary>
public static class BlankFileKinds
{
    /// <summary>The group a kind belongs to.</summary>
    public static BlankFileAccept GroupOf(BlankFileKind kind) => kind switch
    {
        BlankFileKind.Pdf => BlankFileAccept.Pdf,
        BlankFileKind.Jpeg or BlankFileKind.Png or BlankFileKind.Webp => BlankFileAccept.Image,
        BlankFileKind.Docx or BlankFileKind.Xlsx or BlankFileKind.Pptx => BlankFileAccept.Office,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blank file kind."),
    };

    /// <summary>Whether a field with <paramref name="accept"/> takes a file of <paramref name="kind"/>.</summary>
    public static bool IsAccepted(BlankFileKind kind, BlankFileAccept accept) => (accept & GroupOf(kind)) != 0;

    /// <summary>The MIME type stored and served for the kind.</summary>
    public static string ContentTypeFor(BlankFileKind kind) => kind switch
    {
        BlankFileKind.Pdf => "application/pdf",
        BlankFileKind.Jpeg => "image/jpeg",
        BlankFileKind.Png => "image/png",
        BlankFileKind.Webp => "image/webp",
        BlankFileKind.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        BlankFileKind.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        BlankFileKind.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blank file kind."),
    };

    /// <summary>The extension (with the dot) given to the stored and downloaded file.</summary>
    public static string ExtensionFor(BlankFileKind kind) => kind switch
    {
        BlankFileKind.Pdf => ".pdf",
        BlankFileKind.Jpeg => ".jpg",
        BlankFileKind.Png => ".png",
        BlankFileKind.Webp => ".webp",
        BlankFileKind.Docx => ".docx",
        BlankFileKind.Xlsx => ".xlsx",
        BlankFileKind.Pptx => ".pptx",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown blank file kind."),
    };
}
