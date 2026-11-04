namespace Bonvi.Application.Blanks.Files;

/// <summary>Why a blank attachment was refused by <see cref="IBlankFileInspector"/>.</summary>
/// <remarks>The error code each value maps to is named in brackets (SPEC-blankalar §7 error table).</remarks>
public enum BlankFileRejection
{
    /// <summary>
    /// Not one of the accepted kinds: unknown signature, renamed executable, damaged image, a ZIP
    /// that is not a Word/Excel/PowerPoint document, a template (.dotx) and so on [<c>blanka.fayl_turi</c>].
    /// </summary>
    Unrecognized = 1,

    /// <summary>Macro-enabled OOXML (.docm/.xlsm/.pptm, <c>vbaProject.bin</c>) [<c>blanka.fayl_makro</c>].</summary>
    MacroEnabled = 2,

    /// <summary>
    /// OLE compound file: legacy .doc/.xls/.ppt or a password-protected .docx/.xlsx (Office wraps
    /// encrypted OOXML in the same container) [<c>blanka.fayl_eski_format</c>].
    /// </summary>
    LegacyOrEncrypted = 3,

    /// <summary>
    /// The archive unpacks to more than the limits allow — a zip bomb or an absurd document
    /// [<c>blanka.fayl_katta</c>].
    /// </summary>
    ArchiveTooLarge = 4,
}

/// <summary>Result of <see cref="IBlankFileInspector.Inspect"/>: what the file is, or why it was refused.</summary>
public sealed record BlankFileInspection
{
    private BlankFileInspection(BlankFileKind? kind, BlankFileRejection? rejection)
    {
        Kind = kind;
        Rejection = rejection;
    }

    /// <summary>Detected kind. <c>null</c> when rejected.</summary>
    public BlankFileKind? Kind { get; }

    /// <summary>Why the file was refused. <c>null</c> when accepted.</summary>
    public BlankFileRejection? Rejection { get; }

    /// <summary>Canonical MIME type of the detected kind. <c>null</c> when rejected.</summary>
    public string? ContentType => Kind is { } kind ? BlankFileKinds.ContentTypeFor(kind) : null;

    /// <summary>Safe extension (with the dot) of the detected kind. <c>null</c> when rejected.</summary>
    public string? Extension => Kind is { } kind ? BlankFileKinds.ExtensionFor(kind) : null;

    public bool IsAccepted => Kind is not null;

    public static BlankFileInspection Accepted(BlankFileKind kind) => new(kind, null);

    public static BlankFileInspection Rejected(BlankFileRejection rejection) => new(null, rejection);
}

/// <summary>
/// Content inspector for blank attachments (SPEC-blankalar §8) — PDF, images and macro-free OOXML.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>IFileContentInspector</c>.</b> That port serves employee documents and knows
/// only PDF/JPEG/PNG/WebP; it stays unchanged. Blank fields may also take Word/Excel/PowerPoint,
/// which needs a look inside the ZIP container — that lives here, and the four existing kinds are
/// delegated to the existing inspector so both modules keep one rule for them.
/// </para>
/// <para>
/// <b>Security stance.</b> There is no antivirus in the stack, so the rule is narrow: no macros,
/// no legacy or encrypted Office formats, hard caps on what an archive may unpack to, and files are
/// served as attachments with <c>nosniff</c>. The file is never modified and never extracted to disk.
/// </para>
/// <para>
/// The caller enforces the upload size (<c>DocumentLimits.MaxBytes</c> / the field's
/// <c>maxSizeMb</c>) and the field's <c>accept</c> (<see cref="BlankFileKinds.IsAccepted"/>)
/// before or after this check; the inspector only answers «what is this file».
/// </para>
/// </remarks>
public interface IBlankFileInspector
{
    /// <summary>Inspects the uploaded bytes. Never throws for bad content — returns a rejection.</summary>
    BlankFileInspection Inspect(byte[] content);
}
