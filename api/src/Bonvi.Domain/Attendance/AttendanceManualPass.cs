using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// HR qo'lda qo'shgan belgining dalili — kim, qachon va NEGA (SPEC-qolda-belgi §A).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Belgining o'zi — oddiy <see cref="AttendancePass"/></b> («HR (qo'lda)» qurilmasida),
/// ya'ni tabel, taxta va hisobotlar uchun yangi qoida kerak emas. Bu qator faqat dalil:
/// telefon belgisining <see cref="MobileCheckIn"/> qatori bilan bir xil naqsh.
/// </para>
/// <para>
/// <b>Identifikatori — o'tishning identifikatori</b> (1:1). Alohida kalit va
/// <c>pass_id</c> ustuni ikki xil narsani bildirmasdi, faqat qidiruvni ikki
/// pog'onali qilardi.
/// </para>
/// <para>
/// <c>IAuditable</c> — audit jurnali (<c>AuditCatalog</c>) qo'shish va o'chirishni shu
/// qator orqali yozadi: o'tishlar jadvalining o'zi auditdan tashqarida.
/// </para>
/// </remarks>
public sealed class AttendanceManualPass : AggregateRoot, IAuditable
{
    /// <summary>Izohning eng ko'p uzunligi.</summary>
    public const int NoteMaxLength = 500;

    private AttendanceManualPass(Guid passId, Guid employeeId, DateTimeOffset occurredAt, PassDirection direction, string note)
        : base(passId)
    {
        EmployeeId = employeeId;
        OccurredAt = occurredAt.ToUniversalTime();
        Direction = direction;
        Note = note;
    }

    /// <summary>EF Core uchun.</summary>
    private AttendanceManualPass() => Note = string.Empty;

    /// <summary>Xodim — o'tishdagi bilan bir xil, audit jurnalining «kim haqida» ustuni uchun.</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>Belgi lahzasi (UTC) — o'tishdagining nusxasi.</summary>
    /// <remarks>
    /// Nusxa ataylab: audit jurnali shu qatorni yozadi va «qaysi vaqtni qo'shgan / o'chirgan»
    /// degan savolga o'tishlar jadvalisiz (u o'chirilgan bo'lishi mumkin) javob berishi kerak.
    /// </remarks>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Kirish yoki chiqish — o'tishdagining nusxasi (<see cref="OccurredAt"/> bilan bir xil sabab).</summary>
    public PassDirection Direction { get; private set; }

    /// <summary>HR yozgan sabab.</summary>
    public string Note { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public Guid? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <inheritdoc />
    public Guid? UpdatedBy { get; set; }

    /// <summary>Saqlangan o'tishning dalilini yaratadi.</summary>
    /// <param name="passId">Qo'shilgan o'tish.</param>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="occurredAt">Belgi lahzasi.</param>
    /// <param name="direction">Kirish yoki chiqish.</param>
    /// <param name="note">Sabab — majburiy.</param>
    public static Result<AttendanceManualPass> Create(
        Guid passId,
        Guid employeeId,
        DateTimeOffset occurredAt,
        PassDirection direction,
        string? note)
    {
        if (direction is not (PassDirection.In or PassDirection.Out))
        {
            return Error.Validation("davomat.yonalish_notogri", "Yo'nalish «Kirish» yoki «Chiqish» bo'lishi kerak.");
        }

        var trimmed = note?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Error.Validation("davomat.belgi_izoh_bosh", "Izoh majburiy — nega qo'lda qo'shilayotganini yozing.");
        }

        if (trimmed.Length > NoteMaxLength)
        {
            return Error.Validation(
                "davomat.belgi_izoh_uzun",
                $"Izoh {NoteMaxLength} belgidan oshmasin.");
        }

        return new AttendanceManualPass(passId, employeeId, occurredAt, direction, trimmed);
    }
}
