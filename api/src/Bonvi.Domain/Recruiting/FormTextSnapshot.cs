using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// HR ning HRMentor da o'zgartirgan anketa matnlarining bitta reviziyasi
/// (SPEC-blankalar-hrmentor §3.2, §7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hech biri o'chirilmaydi.</b> Topshirilgan anketa o'z reviziyasini eslaydi
/// (<see cref="CandidateForm.TextsRevision"/>) va uning PDF'i keyin ham AYNAN o'sha
/// matnlar bilan qayta chiziladi (§5.3) — imzolangan anketaning savollari keyinchalik
/// o'zgarmasligi kerak. Reviziyalar kichik (bir necha KB) va kamdan-kam chiqadi.
/// </para>
/// <para>
/// <see cref="Payload"/> — shakli TEKSHIRILGAN JSON (noma'lum kalitlar, ro'yxatlar va
/// qiymatlar sinxronlashda tashlab yuborilgan), ya'ni o'qiydigan tomon unga ishonadi.
/// </para>
/// </remarks>
public sealed class FormTextSnapshot : AggregateRoot
{
    /// <summary>Blanka kodining eng katta uzunligi.</summary>
    public const int MaxFormCodeLength = 16;

    private FormTextSnapshot(Guid id) : base(id) { }

    private FormTextSnapshot() { }

    /// <summary>Blanka kodi — hozircha faqat <see cref="CandidateForm.HrF02"/>.</summary>
    public string FormCode { get; private set; } = string.Empty;

    /// <summary>HRMentor dagi reviziya raqami (1, 2, …).</summary>
    public int Revision { get; private set; }

    /// <summary>Tekshirilgan matnlar va ro'yxatlar (<c>jsonb</c>).</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Reviziya olingan lahza.</summary>
    public DateTimeOffset FetchedAt { get; private set; }

    /// <summary>Yangi reviziya.</summary>
    public static FormTextSnapshot Create(string formCode, int revision, string payload, DateTimeOffset fetchedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);

        if (formCode.Length > MaxFormCodeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(formCode));
        }

        return new FormTextSnapshot(NewId())
        {
            FormCode = formCode,
            Revision = revision,
            Payload = payload,
            FetchedAt = fetchedAt,
        };
    }
}
