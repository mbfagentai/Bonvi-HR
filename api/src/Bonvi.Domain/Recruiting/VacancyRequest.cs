using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>Vakansiya so'rovining holati.</summary>
public enum VacancyRequestStatus
{
    /// <summary>Yangi — HR qarorini kutmoqda.</summary>
    Pending = 1,

    /// <summary>Tasdiqlangan — HR vakansiya ochdi (<see cref="VacancyRequest.VacancyId"/>).</summary>
    Approved = 2,

    /// <summary>Rad etilgan — sababi <see cref="VacancyRequest.DecisionNote"/> da.</summary>
    Rejected = 3,

    /// <summary>Qaytarib olingan (fikridan qaytdi yoki xato kiritilgan).</summary>
    Cancelled = 4,
}

/// <summary>
/// Bo'lim boshlig'ining «yangi vakansiya kerak» degan so'rovi (docs/SPEC-vakansiya-sorovi.md).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida yozuv, vakansiyaning o'zi emas.</b> So'rov — bu ehtiyoj, vakansiya — e'lon.
/// Boshliq «ikkita payvandchi kerak» deydi, HR esa nom, o'rin soni va izohni o'zi aniqlab
/// e'lon ochadi yoki rad etadi. Ikkalasi bitta yozuvda bo'lsa, rad etilgan ehtiyoj vakansiyalar
/// ro'yxatini ifloslantirardi va bot uni ochiq e'lon deb ko'rsatib yuborishi mumkin edi.
/// </para>
/// <para>
/// So'rov HR panelidagi «Vakansiya so'rovlari» menyusida kiritiladi (egasi, 2026-10-30: xodim
/// ilovasiga tegilmaydi) — 3–4 maydon. Tahrir yo'q: noto'g'ri kiritilgan so'rov bekor qilinib,
/// yangisi kiritiladi. Faqat <see cref="VacancyRequestStatus.Pending"/> holati o'zgaradi.
/// </para>
/// </remarks>
public sealed class VacancyRequest : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>Nomning eng katta uzunligi — vakansiya nomi bilan bir xil.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Izoh va rad sababi chegarasi.</summary>
    public const int MaxNoteLength = 2000;

    private VacancyRequest(
        Guid id,
        Guid departmentId,
        Guid requestedByEmployeeId,
        string title,
        int openings,
        DateOnly? neededBy,
        string? note)
        : base(id)
    {
        DepartmentId = departmentId;
        RequestedByEmployeeId = requestedByEmployeeId;
        Title = title;
        Openings = openings;
        NeededBy = neededBy;
        Note = note;
        Status = VacancyRequestStatus.Pending;
    }

    private VacancyRequest() { }

    /// <summary>Qaysi bo'lim uchun.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>So'ragan xodim — odatda bo'lim boshlig'i.</summary>
    public Guid RequestedByEmployeeId { get; private set; }

    /// <summary>Lavozim — boshliq qanday atasa shunday.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Nechta odam kerak.</summary>
    public int Openings { get; private set; }

    /// <summary>Qachondan kerak (ixtiyoriy).</summary>
    public DateOnly? NeededBy { get; private set; }

    /// <summary>Sabab va talablar (ixtiyoriy).</summary>
    public string? Note { get; private set; }

    public VacancyRequestStatus Status { get; private set; }

    /// <summary>HR qarori sanasi.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Qaror qabul qilgan HR foydalanuvchisi.</summary>
    public Guid? DecidedBy { get; private set; }

    /// <summary>Rad etish sababi.</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>Tasdiqlanganda ochilgan vakansiya.</summary>
    public Guid? VacancyId { get; private set; }

    /// <summary>Bekor qilingan lahza.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Yangi so'rov.</summary>
    /// <param name="departmentId">Bo'lim.</param>
    /// <param name="requestedByEmployeeId">Boshliq.</param>
    /// <param name="title">Lavozim.</param>
    /// <param name="openings">Nechta odam (1..<see cref="Vacancy.MaxOpenings"/>).</param>
    /// <param name="neededBy">Qachondan kerak.</param>
    /// <param name="note">Sabab va talablar.</param>
    public static Result<VacancyRequest> Create(
        Guid departmentId,
        Guid requestedByEmployeeId,
        string? title,
        int openings,
        DateOnly? neededBy,
        string? note)
    {
        if (departmentId == Guid.Empty)
        {
            return VacancyRequestErrors.DepartmentRequired;
        }

        if (requestedByEmployeeId == Guid.Empty)
        {
            return VacancyRequestErrors.RequesterRequired;
        }

        // Bo'shliqlar vakansiya nomidagi qoida bilan yig'iladi: so'rov tasdiqlanganda nom
        // vakansiya formasiga aynan shu ko'rinishda tushadi.
        var normalized = Vacancy.NormalizeTitle(title);
        if (normalized is null)
        {
            return VacancyRequestErrors.TitleRequired;
        }

        if (normalized.Length > MaxTitleLength)
        {
            return VacancyRequestErrors.TitleTooLong;
        }

        if (openings is < 1 or > Vacancy.MaxOpenings)
        {
            return VacancyRequestErrors.OpeningsOutOfRange;
        }

        var cleanNote = Clean(note);
        if (cleanNote is { Length: > MaxNoteLength })
        {
            return VacancyRequestErrors.NoteTooLong;
        }

        return new VacancyRequest(
            NewId(),
            departmentId,
            requestedByEmployeeId,
            normalized,
            openings,
            neededBy,
            cleanNote);
    }

    /// <summary>HR vakansiya ochdi — so'rov tasdiqlandi.</summary>
    /// <param name="vacancyId">Ochilgan vakansiya.</param>
    /// <param name="decidedBy">HR foydalanuvchisi.</param>
    /// <param name="at">Qaror lahzasi.</param>
    public Result Approve(Guid vacancyId, Guid decidedBy, DateTimeOffset at)
    {
        if (Status != VacancyRequestStatus.Pending)
        {
            return VacancyRequestErrors.AlreadyDecided;
        }

        if (vacancyId == Guid.Empty)
        {
            return Error.Validation("vakansiya_sorovi.vakansiya_yoq", "Ochilgan vakansiya ko'rsatilishi shart.");
        }

        Status = VacancyRequestStatus.Approved;
        VacancyId = vacancyId;
        DecidedBy = decidedBy;
        DecidedAt = at;
        return Result.Success();
    }

    /// <summary>HR rad etdi — sabab majburiy (boshliqqa aytiladi).</summary>
    /// <param name="reason">Sabab.</param>
    /// <param name="decidedBy">HR foydalanuvchisi.</param>
    /// <param name="at">Qaror lahzasi.</param>
    public Result Reject(string? reason, Guid decidedBy, DateTimeOffset at)
    {
        if (Status != VacancyRequestStatus.Pending)
        {
            return VacancyRequestErrors.AlreadyDecided;
        }

        var clean = Clean(reason);
        if (clean is null)
        {
            return VacancyRequestErrors.ReasonRequired;
        }

        if (clean.Length > MaxNoteLength)
        {
            return VacancyRequestErrors.NoteTooLong;
        }

        Status = VacancyRequestStatus.Rejected;
        DecisionNote = clean;
        DecidedBy = decidedBy;
        DecidedAt = at;
        return Result.Success();
    }

    /// <summary>So'rov qaytarib olindi (boshliq fikridan qaytdi) — faqat qaror qilinmasidan oldin.</summary>
    /// <param name="at">Lahza.</param>
    public Result Cancel(DateTimeOffset at)
    {
        if (Status != VacancyRequestStatus.Pending)
        {
            return VacancyRequestErrors.AlreadyDecided;
        }

        Status = VacancyRequestStatus.Cancelled;
        CancelledAt = at;
        return Result.Success();
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Vakansiya so'rovi xatolari — kodlari tarjimada (<c>web</c>, <c>app</c>).</summary>
public static class VacancyRequestErrors
{
    public static readonly Error NotFound =
        Error.NotFound("vakansiya_sorovi.topilmadi", "Vakansiya so'rovi topilmadi.");

    public static readonly Error DepartmentRequired =
        Error.Validation("vakansiya_sorovi.bolim_kerak", "Bo'lim ko'rsatilishi shart.");

    public static readonly Error TitleRequired =
        Error.Validation("vakansiya_sorovi.nom_kerak", "Lavozim nomi ko'rsatilishi shart.");

    public static readonly Error TitleTooLong =
        Error.Validation("vakansiya_sorovi.nom_uzun", "Lavozim nomi juda uzun.");

    public static readonly Error OpeningsOutOfRange =
        Error.Validation("vakansiya_sorovi.soni_notogri", "Odamlar soni 1 dan 100 gacha bo'lishi kerak.");

    public static readonly Error NoteTooLong =
        Error.Validation("vakansiya_sorovi.izoh_uzun", "Izoh juda uzun.");

    public static readonly Error ReasonRequired =
        Error.Validation("vakansiya_sorovi.sabab_kerak", "Rad etish sababi ko'rsatilishi shart.");

    public static readonly Error AlreadyDecided =
        Error.Conflict("vakansiya_sorovi.qaror_qilingan", "So'rov bo'yicha qaror allaqachon qabul qilingan.");

    public static readonly Error DepartmentNotFound =
        Error.NotFound("vakansiya_sorovi.bolim_topilmadi", "Bo'lim topilmadi.");

    public static readonly Error RequesterNotFound =
        Error.NotFound("vakansiya_sorovi.boshliq_topilmadi", "So'rovchi xodim topilmadi.");

    public static readonly Error RequesterRequired =
        Error.Validation("vakansiya_sorovi.boshliq_yoq", "So'rovchi xodim ko'rsatilishi shart.");
}
