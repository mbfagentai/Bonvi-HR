using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// HRMentor kutubxonasidagi bitta blankaning shu yerdagi nusxasi (SPEC-blankalar-hrmentor §3, §7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Egasi HRMentor.</b> Bu qator — oyna: uni faqat sinxronlash vazifasi yozadi
/// (<c>HrMentorSyncJob</c>), HR esa faqat ko'radi va yuklab oladi. Shu sababli
/// <see cref="IAuditable"/> EMAS: har besh daqiqalik yangilanish audit jurnalini
/// «kim o'zgartirdi — hech kim» degan yozuvlar bilan to'ldirardi, haqiqiy o'zgarish
/// tarixi esa HRMentor ning o'z versiyalarida turibdi.
/// </para>
/// <para>
/// Kalit — <see cref="DocumentId"/> (HRMentor dagi hujjat), versiya esa almashadi:
/// yangi versiya chiqsa shu qator yangilanadi va eski fayl o'chiriladi.
/// </para>
/// </remarks>
public sealed class HrBlank : AggregateRoot
{
    /// <summary>Blanka kodining eng katta uzunligi («HR-F02»).</summary>
    public const int MaxCodeLength = 32;

    /// <summary>Nom va fayl nomining eng katta uzunligi.</summary>
    public const int MaxTitleLength = 300;

    /// <summary>MIME turining eng katta uzunligi.</summary>
    public const int MaxContentTypeLength = 150;

    /// <summary>Saqlash kalitining eng katta uzunligi.</summary>
    public const int MaxFileKeyLength = 500;

    /// <summary>SHA-256 — 64 ta o'n oltilik belgi.</summary>
    public const int Sha256Length = 64;

    private HrBlank(Guid id) : base(id) { }

    private HrBlank() { }

    /// <summary>HRMentor dagi hujjat identifikatori.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Blanka kodi (HR-F01 …); HRMentor da kodsiz bo'lsa — bo'sh satr.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Blankaning nomi.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Joriy versiyaning identifikatori.</summary>
    public Guid VersionId { get; private set; }

    /// <summary>Joriy versiyaning raqami.</summary>
    public int VersionNo { get; private set; }

    /// <summary>Versiya chop etilgan lahza.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Asl fayl nomi.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>MIME turi.</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Fayl hajmi (bayt).</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Faylning SHA-256 xeshi (kichik harf, o'n oltilik).</summary>
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>Faylning saqlashdagi kaliti (<c>IFileStorage</c>).</summary>
    public string FileKey { get; private set; } = string.Empty;

    /// <summary>Shu versiya yuklab olingan lahza.</summary>
    public DateTimeOffset SyncedAt { get; private set; }

    /// <summary>Yangi blanka — birinchi marta ko'rilgan hujjat.</summary>
    public static HrBlank Create(Guid documentId, HrBlankVersion version, string fileKey, DateTimeOffset syncedAt)
    {
        var blank = new HrBlank(NewId()) { DocumentId = documentId };
        blank.Apply(version, fileKey, syncedAt);
        return blank;
    }

    /// <summary>
    /// Yangi versiya yoki o'zgargan nom. Eski fayl kaliti qaytariladi — uni chaqiruvchi
    /// o'chiradi (qator saqlangandan KEYIN: saqlash yiqilsa eski fayl kerak bo'lib qoladi).
    /// </summary>
    public string Replace(HrBlankVersion version, string fileKey, DateTimeOffset syncedAt)
    {
        var old = FileKey;
        Apply(version, fileKey, syncedAt);
        return old;
    }

    /// <summary>Faylga tegmaydigan o'zgarish — nom yoki kod (versiya o'sha).</summary>
    public void Rename(string code, string title)
    {
        Code = Clip(code, MaxCodeLength);
        Title = Clip(title, MaxTitleLength);
    }

    private void Apply(HrBlankVersion version, string fileKey, DateTimeOffset syncedAt)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);

        Rename(version.Code, version.Title);
        VersionId = version.VersionId;
        VersionNo = version.VersionNo;
        PublishedAt = version.PublishedAt;
        FileName = Clip(version.FileName, MaxTitleLength);
        ContentType = Clip(version.ContentType, MaxContentTypeLength);
        SizeBytes = version.SizeBytes;
        Sha256 = version.Sha256;
        FileKey = fileKey;
        SyncedAt = syncedAt;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}

/// <summary>Blankaning HRMentor ro'yxatidagi bitta versiyasi — fayl ma'lumotlari bilan.</summary>
public sealed record HrBlankVersion(
    string Code,
    string Title,
    Guid VersionId,
    int VersionNo,
    DateTimeOffset? PublishedAt,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256);
