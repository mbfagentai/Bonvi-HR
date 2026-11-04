using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Blanka maydoniga biriktirilgan fayl (<c>blank_attachments</c>), SPEC-blankalar §3.5, §8.
/// </summary>
/// <remarks>
/// <para>
/// Blankasiz ma'noga ega emas — faqat <see cref="BlankSubmission.AddAttachment"/> orqali
/// yaratiladi. Fayl turi, hajmi va soni chegaralari MAYDONGA bog'liq (ta'rifda) — ularni
/// Application tekshiradi; bu yerda faqat yozuvning o'zi.
/// </para>
/// <para>
/// Olib tashlash — belgi (<see cref="RemovedAt"/>), qator qoladi: kim nima yuklagani va olib
/// tashlagani jurnalda ko'rinib tursin. Diskdagi fayl saqlashdan keyin o'chiriladi.
/// </para>
/// </remarks>
public sealed class BlankAttachment : Entity
{
    /// <summary>Maydon yo'li uzunligi (<c>handover[2].doc</c>).</summary>
    public const int MaxFieldPathLength = 200;

    /// <summary>Asl fayl nomi uzunligi.</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>MIME turi uzunligi.</summary>
    public const int MaxContentTypeLength = 100;

    private BlankAttachment(
        Guid id,
        Guid submissionId,
        BlankAttachmentFile file,
        Guid uploadedBy,
        DateTimeOffset uploadedAt)
        : base(id)
    {
        SubmissionId = submissionId;
        FieldPath = file.FieldPath;
        Kind = file.Kind;
        FileName = file.FileName;
        ContentType = file.ContentType;
        SizeBytes = file.SizeBytes;
        ContentSha256 = file.ContentSha256;
        StorageKey = file.StorageKey;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
    }

    private BlankAttachment() { }

    public Guid SubmissionId { get; private set; }

    /// <summary>Qaysi maydonga (<c>evidence</c>, <c>handover[2].doc</c>).</summary>
    public string FieldPath { get; private set; } = string.Empty;

    /// <summary>Tarkibidan aniqlangan tur.</summary>
    public BlankAttachmentKind Kind { get; private set; }

    /// <summary>Asl nomi (tozalangan) — AI'ga chiqmaydi.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Aniqlangan turdan — mijoz aytganidan emas.</summary>
    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Tarkib xeshi (hex).</summary>
    public string ContentSha256 { get; private set; } = string.Empty;

    /// <summary>Fayl ombori kaliti — AI'ga va jurnalga chiqmaydi.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>Yuklagan foydalanuvchi.</summary>
    public Guid UploadedBy { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>Olib tashlangan lahza.</summary>
    public DateTimeOffset? RemovedAt { get; private set; }

    /// <summary>Olib tashlagan foydalanuvchi.</summary>
    public Guid? RemovedBy { get; private set; }

    /// <summary>Olib tashlanmagan.</summary>
    public bool IsActive => RemovedAt is null;

    internal static Result<BlankAttachment> Create(
        Guid submissionId,
        BlankAttachmentFile file,
        Guid uploadedBy,
        DateTimeOffset uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(file.FieldPath) || file.FieldPath.Length > MaxFieldPathLength
            || string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > MaxFileNameLength
            || string.IsNullOrWhiteSpace(file.ContentType) || file.ContentType.Length > MaxContentTypeLength
            || file.ContentSha256 is not { Length: BlankTemplateVersion.Sha256HexLength }
            || string.IsNullOrWhiteSpace(file.StorageKey) || file.StorageKey.Length > BlankSubmissionStep.MaxStorageKeyLength
            || !Enum.IsDefined(file.Kind))
        {
            return BlankErrors.Invalid;
        }

        if (file.SizeBytes <= 0)
        {
            return BlankErrors.FileEmpty;
        }

        if (uploadedBy == Guid.Empty)
        {
            return BlankErrors.UserNotFound;
        }

        return new BlankAttachment(NewId(), submissionId, file, uploadedBy, uploadedAt);
    }

    internal void Remove(Guid removedBy, DateTimeOffset at)
    {
        RemovedAt = at;
        RemovedBy = removedBy;
    }
}

/// <summary>Tekshirilgan va omborga yozilgan fayl — <see cref="BlankSubmission.AddAttachment"/> kiritmasi.</summary>
/// <param name="FieldPath">Maydon yo'li.</param>
/// <param name="Kind">Aniqlangan tur.</param>
/// <param name="FileName">Tozalangan asl nom.</param>
/// <param name="ContentType">Turdan kelgan MIME.</param>
/// <param name="SizeBytes">Hajm.</param>
/// <param name="ContentSha256">Tarkib xeshi (hex).</param>
/// <param name="StorageKey">Ombor kaliti.</param>
public sealed record BlankAttachmentFile(
    string FieldPath,
    BlankAttachmentKind Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentSha256,
    string StorageKey);
