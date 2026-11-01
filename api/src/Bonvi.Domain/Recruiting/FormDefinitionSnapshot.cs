using System.Text.RegularExpressions;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// HRMentor konstruktorida e'lon qilingan anketaning bitta versiyasi (SPEC-anketa-konstruktori §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hech biri o'zgartirilmaydi va o'chirilmaydi.</b> Topshirilgan anketa o'z versiyasini eslaydi
/// (<see cref="CandidateForm.DefinitionVersion"/>, FK) — PDF keyin ham AYNAN o'sha ta'rif bilan
/// chiziladi: imzolangan anketaning savollari HR konstruktorda nimanidir o'zgartirgani uchun
/// o'zgarmasligi kerak.
/// </para>
/// <para>
/// <see cref="Definition"/> — sinxronlashda SHA-256 si va BonviHR tekshiruvchisidan o'tgan JSON
/// (HRMentor yuborgan matn aynan shu, bayt-baytigacha). O'qiydigan tomon unga ishonadi.
/// </para>
/// </remarks>
public sealed partial class FormDefinitionSnapshot : AggregateRoot
{
    /// <summary>Blanka kodining eng katta uzunligi.</summary>
    public const int MaxFormCodeLength = CandidateForm.MaxFormCodeLength;

    /// <summary>Versiya yorlig'ining eng katta uzunligi («4.0»).</summary>
    public const int MaxLabelLength = CandidateForm.MaxFormVersionLength;

    /// <summary>SHA-256 ning o'n oltilik yozuvi — 64 belgi.</summary>
    public const int Sha256Length = 64;

    private FormDefinitionSnapshot(Guid id) : base(id) { }

    private FormDefinitionSnapshot() { }

    /// <summary>Blanka kodi — hozircha faqat <see cref="CandidateForm.HrF02"/>.</summary>
    public string FormCode { get; private set; } = string.Empty;

    /// <summary>HRMentor dagi versiya raqami (1, 2, …).</summary>
    public int Version { get; private set; }

    /// <summary>HR bergan yorliq («4.0») — anketaning <see cref="CandidateForm.FormVersion"/> i.</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Ta'rif (<c>jsonb</c>).</summary>
    public string Definition { get; private set; } = string.Empty;

    /// <summary>Kanonik JSON ning SHA-256 i (kichik harf).</summary>
    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>Versiya olingan lahza.</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Yangi versiya.</summary>
    public static FormDefinitionSnapshot Create(
        string formCode,
        int version,
        string label,
        string definition,
        string sha256,
        DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        if (formCode.Length > MaxFormCodeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(formCode));
        }

        if (label.Length > MaxLabelLength)
        {
            throw new ArgumentOutOfRangeException(nameof(label));
        }

        if (!Sha256Pattern().IsMatch(sha256))
        {
            throw new ArgumentOutOfRangeException(nameof(sha256));
        }

        return new FormDefinitionSnapshot(NewId())
        {
            FormCode = formCode,
            Version = version,
            Label = label,
            Definition = definition,
            Sha256 = sha256,
            ReceivedAt = receivedAt,
        };
    }

    [GeneratedRegex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
