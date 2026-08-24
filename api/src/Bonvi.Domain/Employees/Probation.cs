using Bonvi.Domain.Common;

namespace Bonvi.Domain.Employees;

/// <summary>Sinov muddati natijasi.</summary>
public enum ProbationStatus
{
    /// <summary>Davom etmoqda.</summary>
    InProgress = 1,

    /// <summary>Muvaffaqiyatli o'tdi.</summary>
    Passed = 2,

    /// <summary>O'tmadi — ishdan bo'shatildi.</summary>
    Failed = 3,

    /// <summary>Xodim o'zi ketdi (Excel: «91.8 Стажировкадан кетганлар»).</summary>
    LeftEarly = 4,

    /// <summary>Bekor qilindi (masalan muddat qisqartirildi).</summary>
    Cancelled = 5,
}

/// <summary>
/// Sinov muddati (Excel: «91.5 Синов муддатидаги ходимлар»).
/// </summary>
/// <remarks>
/// <para>
/// Standart muddat — 90 kun, lekin lavozimga qarab farq qilishi mumkin
/// (<c>Position.ProbationDays</c>). Mehnat kodeksi chegarasi — 180 kun.
/// </para>
/// <para>
/// HR so'ragan asosiy imkoniyat: muddat tugashiga <b>7 kun</b> qolganda
/// eslatma yuborish. Excel'da bu qo'lda kuzatilardi va shu sababli
/// tez-tez o'tkazib yuborilardi.
/// </para>
/// </remarks>
public sealed class Probation : AggregateRoot, IAuditable
{
    /// <summary>Muddat tugashiga necha kun qolganda ogohlantiriladi.</summary>
    public const int WarningDays = 7;

    private Probation(Guid id, Guid employmentId, DateOnly startDate, DateOnly endDate)
        : base(id)
    {
        EmploymentId = employmentId;
        StartDate = startDate;
        EndDate = endDate;
        Status = ProbationStatus.InProgress;
    }

    /// <summary>EF Core uchun.</summary>
    private Probation() { }

    public Guid EmploymentId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public ProbationStatus Status { get; private set; }

    /// <summary>Natija chiqarilgan sana.</summary>
    public DateOnly? ResolvedAt { get; private set; }

    /// <summary>Xulosa matni — HR yozadi.</summary>
    public string? Conclusion { get; private set; }

    /// <summary>Kim baholadi (xodim).</summary>
    public Guid? ReviewerEmployeeId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<Probation> Start(Guid employmentId, DateOnly startDate, int days = 90)
    {
        if (days is < 1 or > 180)
        {
            return Error.Validation(
                "sinov.muddat",
                "Sinov muddati 1 dan 180 kungacha bo'lishi kerak (mehnat kodeksi chegarasi).");
        }

        return new Probation(NewId(), employmentId, startDate, startDate.AddDays(days));
    }

    /// <summary>Tayyor sanalar bilan yaratadi — ETL uchun.</summary>
    public static Result<Probation> Import(Guid employmentId, DateOnly startDate, DateOnly endDate)
        => endDate < startDate
            ? Error.Validation("sinov.sana", "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.")
            : new Probation(NewId(), employmentId, startDate, endDate);

    /// <summary>Muddat tugashiga qolgan kunlar. Manfiy — muddat o'tib ketgan.</summary>
    public int DaysLeft(DateOnly today) => EndDate.DayNumber - today.DayNumber;

    /// <summary>Ogohlantirish vaqti keldimi.</summary>
    public bool NeedsWarning(DateOnly today)
        => Status == ProbationStatus.InProgress && DaysLeft(today) <= WarningDays;

    public Result Resolve(ProbationStatus status, DateOnly at, string? conclusion = null, Guid? reviewerId = null)
    {
        if (Status != ProbationStatus.InProgress)
        {
            return Error.Conflict("sinov.yakunlangan", "Sinov muddati allaqachon yakunlangan.");
        }

        if (status == ProbationStatus.InProgress)
        {
            return Error.Validation("sinov.natija", "Yakuniy natija ko'rsatilishi kerak.");
        }

        Status = status;
        ResolvedAt = at;
        Conclusion = string.IsNullOrWhiteSpace(conclusion) ? null : conclusion.Trim();
        ReviewerEmployeeId = reviewerId;

        return Result.Success();
    }

    /// <summary>Muddatni uzaytiradi (Excel'da bunday holatlar uchraydi).</summary>
    public Result Extend(DateOnly newEndDate)
    {
        if (Status != ProbationStatus.InProgress)
        {
            return Error.Conflict("sinov.yakunlangan", "Yakunlangan sinov muddatini uzaytirib bo'lmaydi.");
        }

        if (newEndDate <= EndDate)
        {
            return Error.Validation("sinov.uzaytirish", "Yangi sana joriy sanadan keyin bo'lishi kerak.");
        }

        if (newEndDate.DayNumber - StartDate.DayNumber > 180)
        {
            return Error.Validation("sinov.muddat", "Umumiy sinov muddati 180 kundan oshmasligi kerak.");
        }

        EndDate = newEndDate;
        return Result.Success();
    }
}
