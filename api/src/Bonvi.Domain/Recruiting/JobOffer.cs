using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Recruiting;

/// <summary>Ish taklifining holati.</summary>
public enum JobOfferStatus
{
    /// <summary>Yuborildi — javob kutilmoqda.</summary>
    Sent = 1,

    /// <summary>Qabul qilindi — nomzod xodimga aylanadi.</summary>
    Accepted = 2,

    /// <summary>Rad etildi — nomzod taklifni qabul qilmadi.</summary>
    Declined = 3,

    /// <summary>Muddati o'tdi — javob kelmadi.</summary>
    Expired = 4,
}

/// <summary>
/// Ish taklifi (Job Offer) — voronkaning oxirgi rasmiy qadami.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida agregat, yana bir bosqich emas.</b> Voronkaning qolgan
/// bosqichlari faqat «o'tdi / o'tmadi» ni saqlaydi; taklifda esa SHARTLAR
/// bor — oklad, ish boshlash sanasi, javob muddati. Ular
/// <see cref="CandidateStageEntry.Conclusion"/> ga erkin matn bo'lib
/// yozilsa, «qancha oklad taklif qilgandik?» degan savolga javob faqat
/// o'qib topilardi va u <c>HireEmployeeCommand</c> ga avtomatik ko'chmasdi.
/// Aynan shu ko'chish — taklifning butun ma'nosi.
/// </para>
/// <para>
/// <b>Nega taklif rad etilganda nomzod O'ZI yopilmaydi.</b> Rad javob
/// muzokaraning oxiri emas: HR ko'pincha oklani ko'tarib ikkinchi taklif
/// yuboradi. Nomzodni avtomatik «voz kechgan» qilib qo'yish bu yo'lni
/// yopardi. Shu sababli faol taklif BITTA bo'ladi (use-case qoidasi), ammo
/// vaqt o'tib bir nechtasi bo'lishi mumkin — va ularning ketma-ketligi
/// muzokara tarixini ko'rsatadi.
/// </para>
/// <para>
/// <b>Nega oklad MAJBURIY.</b> Summasiz «taklif» — bu og'zaki suhbat, uni
/// bosqich xulosasi bilan yozish kerak. Rasmiy taklif esa doim aniq
/// summa bilan beriladi va aynan shu summa ishga qabul buyrug'iga tushadi.
/// </para>
/// </remarks>
public sealed class JobOffer : AggregateRoot, IAuditable, ISoftDeletable
{
    private JobOffer(
        Guid id,
        Guid candidateId,
        Guid vacancyId,
        Money salary,
        DateOnly startDate,
        DateOnly sentAt)
        : base(id)
    {
        CandidateId = candidateId;
        VacancyId = vacancyId;
        Salary = salary;
        StartDate = startDate;
        SentAt = sentAt;
        Status = JobOfferStatus.Sent;
    }

    /// <summary>EF Core uchun.</summary>
    private JobOffer() => Salary = null!;

    public Guid CandidateId { get; private set; }

    /// <summary>
    /// Qaysi vakansiyaga — nomzodning vakansiyasidan NUSXA emas, TASDIQ.
    /// </summary>
    /// <remarks>
    /// Nomzod boshqa vakansiyaga o'tkazilishi mumkin
    /// (<c>Candidate.Reassign</c>), taklif esa aniq bir o'ringa beriladi va
    /// o'sha o'rinning lavozimi/firmasi bilan ishga qabul qilinadi. Shu
    /// sababli taklif o'z vakansiyasini saqlaydi: keyinchalik nomzod
    /// ko'chirilsa ham, «nimaga taklif qilingan edi» degan fakt o'zgarmaydi.
    /// </remarks>
    public Guid VacancyId { get; private set; }

    /// <summary>Taklif qilingan oylik ish haqi.</summary>
    public Money Salary { get; private set; }

    /// <summary>Ish boshlash sanasi — ishga qabul buyrug'ining sanasi bo'ladi.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Taklif yuborilgan sana.</summary>
    public DateOnly SentAt { get; private set; }

    /// <summary>Javob kutiladigan oxirgi sana. Bo'sh — muddat qo'yilmagan.</summary>
    /// <remarks>
    /// Ixtiyoriy: HR ba'zan «o'ylab ko'ring» deb muddatsiz taklif qiladi.
    /// Muddat qo'yilganda ham tizim O'ZI «muddati o'tdi» deb belgilamaydi —
    /// buni HR qiladi (<see cref="MarkExpired"/>). Sabab: muddat o'tgan kuni
    /// nomzod qo'ng'iroq qilib rozi bo'lishi mumkin va avtomatik yopilgan
    /// taklif buni qabul qila olmasdi.
    /// </remarks>
    public DateOnly? ExpiresAt { get; private set; }

    public JobOfferStatus Status { get; private set; }

    /// <summary>Javob kelgan sana.</summary>
    public DateOnly? RespondedAt { get; private set; }

    /// <summary>Javob izohi: nega rad etildi, qanday shart qo'yildi.</summary>
    public string? ResponseNote { get; private set; }

    /// <summary>Taklif haqidagi ichki izoh.</summary>
    public string? Note { get; private set; }

    /// <summary>Javob kutilyaptimi.</summary>
    public bool IsPending => Status == JobOfferStatus.Sent;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<JobOffer> Send(
        Guid candidateId,
        Guid vacancyId,
        Money salary,
        DateOnly startDate,
        DateOnly sentAt,
        DateOnly? expiresAt = null,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(salary);

        if (candidateId == Guid.Empty)
        {
            return Error.Validation("taklif.nomzod_yoq", "Nomzod ko'rsatilishi shart.");
        }

        if (vacancyId == Guid.Empty)
        {
            return Error.Validation("taklif.vakansiya_yoq", "Vakansiya ko'rsatilishi shart.");
        }

        if (startDate < sentAt)
        {
            return Error.Validation(
                "taklif.boshlash_sanasi",
                "Ish boshlash sanasi taklif yuborilgan sanadan oldin bo'lishi mumkin emas.");
        }

        if (expiresAt is { } deadline && deadline < sentAt)
        {
            return Error.Validation(
                "taklif.muddat_sanasi",
                "Javob muddati taklif yuborilgan sanadan oldin bo'lishi mumkin emas.");
        }

        return new JobOffer(NewId(), candidateId, vacancyId, salary, startDate, sentAt)
        {
            ExpiresAt = expiresAt,
            Note = Trim(note),
        };
    }

    /// <summary>Taklif shartlarini tuzatadi — faqat javob kelmaguncha.</summary>
    /// <remarks>
    /// Javob kelgandan keyin shartlar qulflanadi: qabul qilingan taklifning
    /// okladi keyin o'zgartirilsa, xodim kartasidagi oklad bilan taklifdagi
    /// summa jimgina farq qilib qolardi va «nima va'da qilingan edi» degan
    /// savolga javob yo'qolardi. Yangi shart — yangi taklif.
    /// </remarks>
    public Result Update(Money salary, DateOnly startDate, DateOnly? expiresAt = null, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(salary);

        if (!IsPending)
        {
            return AlreadyAnswered();
        }

        if (startDate < SentAt)
        {
            return Error.Validation(
                "taklif.boshlash_sanasi",
                "Ish boshlash sanasi taklif yuborilgan sanadan oldin bo'lishi mumkin emas.");
        }

        if (expiresAt is { } deadline && deadline < SentAt)
        {
            return Error.Validation(
                "taklif.muddat_sanasi",
                "Javob muddati taklif yuborilgan sanadan oldin bo'lishi mumkin emas.");
        }

        Salary = salary;
        StartDate = startDate;
        ExpiresAt = expiresAt;
        Note = Trim(note);

        return Result.Success();
    }

    /// <summary>Nomzod taklifni qabul qildi.</summary>
    public Result Accept(DateOnly at, string? note = null) => Respond(JobOfferStatus.Accepted, at, note);

    /// <summary>Nomzod taklifni rad etdi.</summary>
    public Result Decline(DateOnly at, string? note = null) => Respond(JobOfferStatus.Declined, at, note);

    /// <summary>Javob kelmadi — muddati o'tdi.</summary>
    public Result MarkExpired(DateOnly at, string? note = null) => Respond(JobOfferStatus.Expired, at, note);

    private Result Respond(JobOfferStatus status, DateOnly at, string? note)
    {
        if (!IsPending)
        {
            return AlreadyAnswered();
        }

        if (at < SentAt)
        {
            return Error.Validation(
                "taklif.javob_sanasi",
                "Javob sanasi taklif yuborilgan sanadan oldin bo'lishi mumkin emas.");
        }

        Status = status;
        RespondedAt = at;
        ResponseNote = Trim(note);

        return Result.Success();
    }

    private Error AlreadyAnswered() => Error.Conflict(
        "taklif.javob_berilgan",
        $"Taklifga allaqachon javob berilgan (holati: {Status}).");

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
