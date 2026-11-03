using System.Text;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Shablon versiyasi — blankaning maydonlari, bo'limlari va imzo qadamlari (JSON ta'rif),
/// SPEC-blankalar §3.2, §4.
/// </summary>
/// <remarks>
/// <para>
/// <b>E'lon qilingan versiya O'ZGARMAYDI.</b> Blanka ochilganda shu versiyaga bog'lanadi va
/// tekshirish, ekran va PDF umrbod shu ta'rifdan quriladi. Ta'rifni tahrirlash usuli
/// ataylab yo'q: o'zgarish — yangi versiya.
/// </para>
/// <para>
/// Shablonda bir vaqtda bittadan ko'p <see cref="BlankTemplateVersionStatus.Published"/> versiya
/// bo'lmaydi (qisman unikal indeks). Yangisini e'lon qilishda eskisi AVVAL
/// <see cref="Retire"/> qilinib saqlanadi, keyin yangisi <see cref="Publish"/> — PostgreSQL
/// unikal indeksni har bir buyruqda tekshiradi va EF bitta saqlashdagi buyruqlar tartibini
/// kafolatlamaydi.
/// </para>
/// </remarks>
public sealed class BlankTemplateVersion : AggregateRoot, IAuditable
{
    /// <summary>Versiya yozuvi uzunligi (<c>1.0</c>).</summary>
    public const int MaxVersionLength = 8;

    /// <summary>Ta'rifning eng katta hajmi (UTF-8 baytlarda) — 256 KB.</summary>
    public const int MaxDefinitionBytes = 256 * 1024;

    /// <summary>SHA-256 ning hex yozuvi uzunligi.</summary>
    public const int Sha256HexLength = 64;

    private BlankTemplateVersion(
        Guid id,
        Guid templateId,
        string version,
        string definition,
        string definitionSha256,
        BlankTemplateSource source)
        : base(id)
    {
        TemplateId = templateId;
        Version = version;
        Definition = definition;
        DefinitionSha256 = definitionSha256;
        Source = source;
        Status = BlankTemplateVersionStatus.Draft;
    }

    private BlankTemplateVersion() { }

    /// <summary>Qaysi shablon.</summary>
    public Guid TemplateId { get; private set; }

    /// <summary>Versiya yozuvi (<c>1.0</c>) — shablon ichida unikal.</summary>
    public string Version { get; private set; } = string.Empty;

    public BlankTemplateVersionStatus Status { get; private set; }

    /// <summary>Ta'rif — JSON (<c>jsonb</c>), SPEC §4.</summary>
    public string Definition { get; private set; } = string.Empty;

    /// <summary>Kanonik JSON'ning SHA-256 (hex, kichik harf) — seeder o'zgarishni shu bilan taniydi.</summary>
    public string DefinitionSha256 { get; private set; } = string.Empty;

    /// <summary>Qayerdan kelgan.</summary>
    public BlankTemplateSource Source { get; private set; }

    /// <summary>E'lon qilingan lahza.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>E'lon qilgan foydalanuvchi (seeder — <c>null</c>).</summary>
    public Guid? PublishedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Yangi versiya — qoralama holatida.</summary>
    /// <param name="templateId">Shablon.</param>
    /// <param name="version">Versiya yozuvi.</param>
    /// <param name="definition">Ta'rif JSON (tekshiruvni Application qiladi).</param>
    /// <param name="definitionSha256">Kanonik JSON xeshi (hex).</param>
    /// <param name="source">Qayerdan kelgan.</param>
    public static Result<BlankTemplateVersion> Create(
        Guid templateId,
        string? version,
        string? definition,
        string? definitionSha256,
        BlankTemplateSource source)
    {
        if (templateId == Guid.Empty)
        {
            return BlankErrors.TemplateNotFound;
        }

        var cleanVersion = version?.Trim();
        if (string.IsNullOrEmpty(cleanVersion) || cleanVersion.Length > MaxVersionLength)
        {
            return BlankErrors.Invalid;
        }

        if (string.IsNullOrWhiteSpace(definition))
        {
            return BlankErrors.InvalidJson;
        }

        if (Encoding.UTF8.GetByteCount(definition) > MaxDefinitionBytes)
        {
            return BlankErrors.TooLarge;
        }

        if (definitionSha256 is not { Length: Sha256HexLength } || !definitionSha256.All(char.IsAsciiHexDigitLower))
        {
            return BlankErrors.Invalid;
        }

        if (!Enum.IsDefined(source))
        {
            return BlankErrors.Invalid;
        }

        return new BlankTemplateVersion(NewId(), templateId, cleanVersion, definition, definitionSha256, source);
    }

    /// <summary>Versiyani e'lon qiladi — faqat qoralamadan.</summary>
    /// <param name="by">Kim (seeder — <c>null</c>).</param>
    /// <param name="at">Lahza.</param>
    public Result Publish(Guid? by, DateTimeOffset at)
    {
        if (Status != BlankTemplateVersionStatus.Draft)
        {
            return BlankErrors.InvalidStatus;
        }

        Status = BlankTemplateVersionStatus.Published;
        PublishedAt = at;
        PublishedBy = by;
        return Result.Success();
    }

    /// <summary>Versiyani eskirgan qiladi — faqat e'lon qilingandan (yangisi chiqqanda).</summary>
    public Result Retire()
    {
        if (Status != BlankTemplateVersionStatus.Published)
        {
            return BlankErrors.InvalidStatus;
        }

        Status = BlankTemplateVersionStatus.Retired;
        return Result.Success();
    }
}
