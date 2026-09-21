using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>Yo'qlik so'rovining holati.</summary>
public enum AbsenceRequestStatus
{
    /// <summary>Yuborilgan, HR hali qaramagan.</summary>
    Pending = 1,

    /// <summary>Tasdiqlangan — qamragan kunlar «Sababli» bo'ladi.</summary>
    Approved = 2,

    /// <summary>Rad etilgan — kun «Kelmadi» bo'lib qoladi.</summary>
    Rejected = 3,

    /// <summary>Bekor qilingan: xato kiritilgan yoki xodim o'zi qaytarib olgan.</summary>
    Cancelled = 4,
}

/// <summary>
/// Yo'qlik so'rovi: «falon kuni falon sababga ko'ra ishda bo'lmayman»
/// (SPEC-so'rovlar §B2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kerak.</b> Turniket faqat o'tishni biladi: odam kelmagan bo'lsa,
/// tizim «Kelmadi» deydi va sababini bilmaydi. Verifix'da bu ish xodimning
/// ilovasidan yuboriladigan so'rov bilan hal qilinadi va egasi shu yo'lni
/// tanladi (2026-09-19). Birinchi bosqichda so'rovni HR ning o'zi kiritadi
/// — u sababni baribir telefonda eshitadi; ikkinchi bosqichda xodim uni
/// o'z ilovasidan yuboradi (§C) va YOZUV o'zgarmaydi, faqat kim
/// yaratgani boshqacha bo'ladi.
/// </para>
/// <para>
/// <b>Davr ikki shaklda</b> (§A3): bir kun ichidagi VAQT oralig'i
/// (08:30–11:57 — yarim kunlik yo'qlik) yoki bir necha BUTUN kun
/// (12–16-sentabr — kasallik, komandirovka). Vaqt faqat bir kunlik
/// so'rovda bo'lishi mumkin: «12-dan 16-gacha, har kuni 08:30–11:57»
/// degan shakl boshqa savol va u kelganda alohida hal qilinadi.
/// </para>
/// <para>
/// <b>Yozuv o'chirilmaydi</b> (<see cref="ISoftDeletable"/>): rad etilgan
/// so'rov ham tarix — «nega bu kun kelmagan deb turibdi» degan savolga
/// javob aynan shu yerda.
/// </para>
/// </remarks>
public sealed class AbsenceRequest : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>
    /// Eng uzun so'rov — 92 kun.
    /// </summary>
    /// <remarks>
    /// Chegara modulning boshqa joylaridagi bilan AYNAN bir xil
    /// (<c>MaxReportDays</c>, hisobot va grafik davri): bitta tizimda ikki
    /// xil «eng uzun davr» bo'lsa, HR qaysi biri qayerda ishlashini eslab
    /// yurishga majbur bo'lardi.
    /// </remarks>
    public const int MaxDays = 92;

    private AbsenceRequest(
        Guid id,
        Guid employeeId,
        Guid absenceTypeId,
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly? fromTime,
        TimeOnly? toTime,
        string? note)
        : base(id)
    {
        EmployeeId = employeeId;
        AbsenceTypeId = absenceTypeId;
        FromDate = fromDate;
        ToDate = toDate;
        FromTime = fromTime;
        ToTime = toTime;
        Note = note;
        Status = AbsenceRequestStatus.Pending;
    }

    /// <summary>EF Core uchun.</summary>
    private AbsenceRequest() { }

    /// <summary>Kim yo'q bo'ladi.</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>Yo'qlik turi — <c>absence_types</c> ma'lumotnomasidan.</summary>
    public Guid AbsenceTypeId { get; private set; }

    /// <summary>Davr boshi (Toshkent sanasi), shu kun ham kiradi.</summary>
    public DateOnly FromDate { get; private set; }

    /// <summary>Davr oxiri, shu kun ham kiradi. Bir kunlik so'rovda <see cref="FromDate"/> ga teng.</summary>
    public DateOnly ToDate { get; private set; }

    /// <summary>Kun ichidagi boshlanish vaqti — faqat bir kunlik so'rovda.</summary>
    public TimeOnly? FromTime { get; private set; }

    /// <summary>Kun ichidagi tugash vaqti — faqat bir kunlik so'rovda.</summary>
    public TimeOnly? ToTime { get; private set; }

    /// <summary>Xodimning izohi: nega yo'q bo'ladi.</summary>
    public string? Note { get; private set; }

    public AbsenceRequestStatus Status { get; private set; }

    /// <summary>HR ning qarordagi izohi (Verifix'dagi «Примечание руководителя»).</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>Qarorni kim qabul qildi.</summary>
    public Guid? DecidedBy { get; private set; }

    /// <summary>Qaror lahzasi (UTC).</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    // Audit va yumshoq o'chirish maydonlari — infratuzilma to'ldiradi
    // (`Incentive` dagi bilan bir xil kelishuv).
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Butun kunlikmi — vaqt oralig'i berilmaganmi.</summary>
    public bool IsWholeDay => FromTime is null && ToTime is null;

    /// <summary>Qaror qabul qilingan (tasdiqlangan yoki rad etilgan)mi.</summary>
    public bool IsDecided => Status is AbsenceRequestStatus.Approved or AbsenceRequestStatus.Rejected;

    /// <summary>So'rov shu sanani qamraydimi.</summary>
    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;

    public static Result<AbsenceRequest> Create(
        Guid employeeId,
        Guid absenceTypeId,
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly? fromTime = null,
        TimeOnly? toTime = null,
        string? note = null)
    {
        if (employeeId == Guid.Empty)
        {
            return Error.Validation("davomat.sorov_xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        if (absenceTypeId == Guid.Empty)
        {
            return Error.Validation("davomat.sorov_turi_yoq", "Yo'qlik turi tanlanishi shart.");
        }

        var period = ValidatePeriod(fromDate, toDate, fromTime, toTime);
        if (period.IsFailure)
        {
            return period.Error;
        }

        return new AbsenceRequest(
            NewId(),
            employeeId,
            absenceTypeId,
            fromDate,
            toDate,
            fromTime,
            toTime,
            Trim(note));
    }

    /// <summary>Kutayotgan so'rovni tahrirlash — qaror qabul qilinmaguncha.</summary>
    public Result Update(
        Guid absenceTypeId,
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly? fromTime,
        TimeOnly? toTime,
        string? note)
    {
        if (Status != AbsenceRequestStatus.Pending)
        {
            return Error.Conflict("davomat.sorov_qaror_qabul_qilingan", "Qaror qabul qilingan so'rovni tahrirlab bo'lmaydi.");
        }

        if (absenceTypeId == Guid.Empty)
        {
            return Error.Validation("davomat.sorov_turi_yoq", "Yo'qlik turi tanlanishi shart.");
        }

        var period = ValidatePeriod(fromDate, toDate, fromTime, toTime);
        if (period.IsFailure)
        {
            return period.Error;
        }

        AbsenceTypeId = absenceTypeId;
        FromDate = fromDate;
        ToDate = toDate;
        FromTime = fromTime;
        ToTime = toTime;
        Note = Trim(note);

        return Result.Success();
    }

    /// <summary>Tasdiqlash — qamragan kunlar «Sababli» bo'ladi.</summary>
    public Result Approve(Guid decidedBy, DateTimeOffset at, string? decisionNote = null)
        => Decide(AbsenceRequestStatus.Approved, decidedBy, at, decisionNote);

    /// <summary>Rad etish — kunlar «Kelmadi» bo'lib qoladi.</summary>
    public Result Reject(Guid decidedBy, DateTimeOffset at, string? decisionNote = null)
        => Decide(AbsenceRequestStatus.Rejected, decidedBy, at, decisionNote);

    /// <summary>
    /// Bekor qilish.
    /// </summary>
    /// <remarks>
    /// Tasdiqlangan so'rovni ham bekor qilish MUMKIN: xato tasdiqlangan
    /// so'rov kunni jimgina «Sababli» qilib qoldirardi va uni tuzatishning
    /// boshqa yo'li yo'q edi. Bekor qilingandan keyin kun qayta
    /// hisoblanadi (chaqiruvchining mas'uliyati, §B3).
    /// </remarks>
    public Result Cancel(Guid cancelledBy, DateTimeOffset at)
    {
        if (Status == AbsenceRequestStatus.Cancelled)
        {
            return Error.Conflict("davomat.sorov_bekor_qilingan", "So'rov allaqachon bekor qilingan.");
        }

        Status = AbsenceRequestStatus.Cancelled;
        DecidedBy = cancelledBy;
        DecidedAt = at;

        return Result.Success();
    }

    private Result Decide(
        AbsenceRequestStatus status,
        Guid decidedBy,
        DateTimeOffset at,
        string? decisionNote)
    {
        if (Status != AbsenceRequestStatus.Pending)
        {
            return Error.Conflict("davomat.sorov_qaror_qabul_qilingan", "So'rov bo'yicha qaror allaqachon qabul qilingan.");
        }

        Status = status;
        DecidedBy = decidedBy;
        DecidedAt = at;
        DecisionNote = Trim(decisionNote);

        return Result.Success();
    }

    /// <summary>
    /// Davr qoidalari — yaratishda ham, tahrirlashda ham AYNAN bir xil.
    /// </summary>
    /// <remarks>
    /// Vaqt oralig'i faqat BIR KUNLIK so'rovda: ko'p kunlik so'rovga vaqt
    /// berilsa, u «har kuni shu oraliqda» degan uchinchi ma'noni yasardi va
    /// hisoblash uni qanday o'qishini hech kim bilmasdi.
    /// </remarks>
    private static Result ValidatePeriod(
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly? fromTime,
        TimeOnly? toTime)
    {
        if (fromDate > toDate)
        {
            return Error.Validation("davomat.sorov_davri_notogri", "Boshlanish sanasi tugash sanasidan keyin bo'lishi mumkin emas.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > MaxDays)
        {
            return Error.Validation("davomat.sorov_uzun", $"So'rov {MaxDays} kundan uzun bo'lishi mumkin emas.");
        }

        if (fromTime is null != toTime is null)
        {
            return Error.Validation("davomat.sorov_vaqti_notogri", "Vaqt oralig'ining ikkala chegarasi ham ko'rsatilishi shart.");
        }

        if (fromTime is { } start && toTime is { } end)
        {
            if (fromDate != toDate)
            {
                return Error.Validation("davomat.sorov_vaqti_notogri", "Vaqt oralig'i faqat bir kunlik so'rovda ko'rsatiladi.");
            }

            if (start >= end)
            {
                return Error.Validation("davomat.sorov_vaqti_notogri", "Boshlanish vaqti tugash vaqtidan oldin bo'lishi shart.");
            }
        }

        return Result.Success();
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
