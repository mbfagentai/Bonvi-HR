using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>Jadval so'rovining turi (Verifix «Тип запроса»).</summary>
public enum ScheduleChangeKind
{
    /// <summary>«Kunni o'zgartirish» — bir yoki bir necha sanaga yangi kun (ish kuni yoki dam olish).</summary>
    Change = 1,

    /// <summary>«Kun almashtirish» — ikki sananing rejasi o'rin almashadi.</summary>
    Swap = 2,
}

/// <summary>So'ralgan bitta kun — tekshirishdan oldingi tavsif.</summary>
/// <param name="Date">Sana (Toshkent).</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish — ish kunida shart.</param>
/// <param name="EndTime">Tugash — boshlanishdan oldin bo'lsa tungi smena.</param>
/// <param name="BreakStart">Tanaffus boshi (ixtiyoriy, oxiri bilan birga).</param>
/// <param name="BreakEnd">Tanaffus oxiri.</param>
/// <param name="SwapWith">Almashtirishda juftining sanasi (faqat ko'rsatish uchun).</param>
public sealed record ScheduleChangeDayInput(
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    DateOnly? SwapWith = null);

/// <summary>
/// Jadvalni o'zgartirish so'rovi — Verifix «Запрос на изменение рабочего графика»
/// (SPEC-verifikatsiya-jadval-sorovi §C).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nima uchun.</b> Verifix'da MBF bu so'rovdan 13 mingdan ortiq foydalangan,
/// ko'pincha bitta kun tanaffusini ko'chirish uchun («tushlik va juma namoziga uyga
/// bordim» — tanaffus 12:23–14:23). Tasdiqlangan so'rovning kuni xodimning O'SHA
/// sanadagi rejasining o'rniga turadi — jadvalning o'zi o'zgarmaydi.
/// </para>
/// <para>
/// Holatlar, qaror va bekor qilish qoidalari <see cref="AbsenceRequest"/> bilan AYNAN
/// bir xil (shu sababli holat turi ham o'sha): HR uchun ikki xil so'rov ikki xil
/// mantiqda ishlasa, qaysi tugma nima qilishini eslab qolishga to'g'ri kelardi.
/// </para>
/// <para>
/// Kunlar so'rovning O'ZIDA aniq qiymat bilan saqlanadi, almashtirishda ham:
/// ikki sananing rejasi yaratish paytida suratga olinadi. Keyin jadval o'zgarsa
/// ham, HR tasdiqlagan narsa — u ko'rgan narsa.
/// </para>
/// </remarks>
public sealed class ScheduleChangeRequest : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>«Kunni o'zgartirish» da eng ko'p sana.</summary>
    public const int MaxDays = 31;

    /// <summary>«Kun almashtirish» da eng ko'p juft.</summary>
    public const int MaxSwapPairs = 15;

    private readonly List<ScheduleChangeDay> _days = [];

    private ScheduleChangeRequest(Guid id, Guid employeeId, ScheduleChangeKind kind, string? note)
        : base(id)
    {
        EmployeeId = employeeId;
        Kind = kind;
        Note = note;
        Status = AbsenceRequestStatus.Pending;
    }

    /// <summary>Faqat EF Core uchun.</summary>
    private ScheduleChangeRequest() { }

    public Guid EmployeeId { get; private set; }

    public ScheduleChangeKind Kind { get; private set; }

    public string? Note { get; private set; }

    public AbsenceRequestStatus Status { get; private set; }

    public string? DecisionNote { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Xodim ilovadan yuborgan bo'lsa — uning hisobi.</summary>
    public Guid? SubmittedByAccountId { get; private set; }

    /// <summary>Xodim ilovadan bekor qilgan bo'lsa — uning hisobi.</summary>
    public Guid? CancelledByAccountId { get; private set; }

    public IReadOnlyList<ScheduleChangeDay> Days => _days.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Yangi so'rov.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="kind">Tur.</param>
    /// <param name="days">Kunlar — almashtirishda server suratga olgan ikkala tomon.</param>
    /// <param name="note">Xodim izohi.</param>
    /// <param name="submittedByAccountId">Ilovadan yuborgan xodim hisobi.</param>
    public static Result<ScheduleChangeRequest> Create(
        Guid employeeId,
        ScheduleChangeKind kind,
        IReadOnlyCollection<ScheduleChangeDayInput> days,
        string? note = null,
        Guid? submittedByAccountId = null)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (employeeId == Guid.Empty)
        {
            return Error.Validation("davomat.sorov_xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        var request = new ScheduleChangeRequest(NewId(), employeeId, kind, Trim(note))
        {
            SubmittedByAccountId = submittedByAccountId,
        };

        var applied = request.ApplyDays(kind, days);
        return applied.IsFailure ? applied.Error : request;
    }

    /// <summary>Qaror kutayotgan so'rovni tahrirlaydi.</summary>
    public Result Update(ScheduleChangeKind kind, IReadOnlyCollection<ScheduleChangeDayInput> days, string? note)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (Status != AbsenceRequestStatus.Pending)
        {
            return Error.Conflict("davomat.sorov_qaror_qabul_qilingan", "Qaror qabul qilingan so'rovni tahrirlab bo'lmaydi.");
        }

        var validated = Validate(kind, days);
        if (validated.IsFailure)
        {
            return validated;
        }

        Kind = kind;
        Note = Trim(note);
        _days.Clear();
        _days.AddRange(days.OrderBy(day => day.Date).Select(day => ScheduleChangeDay.Create(Id, day)));

        return Result.Success();
    }

    public Result Approve(Guid decidedBy, DateTimeOffset at, string? decisionNote = null)
        => Decide(AbsenceRequestStatus.Approved, decidedBy, at, decisionNote);

    public Result Reject(Guid decidedBy, DateTimeOffset at, string? decisionNote = null)
        => Decide(AbsenceRequestStatus.Rejected, decidedBy, at, decisionNote);

    /// <summary>HR bekor qiladi — tasdiqlanganini ham (xato tasdiqlangan bo'lsa).</summary>
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

    /// <summary>Xodim o'zi qaytarib oladi — faqat qaror kutayotgan bo'lsa.</summary>
    public Result CancelBySubmitter(Guid accountId, DateTimeOffset at)
    {
        if (Status == AbsenceRequestStatus.Cancelled)
        {
            return Error.Conflict("davomat.sorov_bekor_qilingan", "So'rov allaqachon bekor qilingan.");
        }

        if (Status != AbsenceRequestStatus.Pending)
        {
            return Error.Conflict(
                "davomat.sorov_qaror_qabul_qilingan",
                "So'rov bo'yicha qaror qabul qilingan — uni endi faqat HR o'zgartira oladi.");
        }

        Status = AbsenceRequestStatus.Cancelled;
        DecidedAt = at;
        CancelledByAccountId = accountId;

        return Result.Success();
    }

    private Result ApplyDays(ScheduleChangeKind kind, IReadOnlyCollection<ScheduleChangeDayInput> days)
    {
        var validated = Validate(kind, days);
        if (validated.IsFailure)
        {
            return validated;
        }

        _days.AddRange(days.OrderBy(day => day.Date).Select(day => ScheduleChangeDay.Create(Id, day)));
        return Result.Success();
    }

    private Result Decide(AbsenceRequestStatus status, Guid decidedBy, DateTimeOffset at, string? decisionNote)
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

    /// <summary>Kunlar qoidasi — yaratishda ham, tahrirlashda ham bir xil.</summary>
    private static Result Validate(ScheduleChangeKind kind, IReadOnlyCollection<ScheduleChangeDayInput> days)
    {
        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("davomat.jadval_sorovi_turi", "So'rov turi noto'g'ri.");
        }

        if (days.Count == 0)
        {
            return Error.Validation("davomat.jadval_sorovi_kun_yoq", "Kamida bitta sana ko'rsatilishi shart.");
        }

        var limit = kind == ScheduleChangeKind.Swap ? MaxSwapPairs * 2 : MaxDays;
        if (days.Count > limit)
        {
            return Error.Validation("davomat.jadval_sorovi_kop", $"Bitta so'rovda {limit} tadan ko'p sana bo'lishi mumkin emas.");
        }

        if (days.Select(day => day.Date).Distinct().Count() != days.Count)
        {
            return Error.Validation("davomat.jadval_sorovi_sana_takror", "Bitta sana so'rovda ikki marta bo'lishi mumkin emas.");
        }

        foreach (var day in days)
        {
            // Smena va tanaffus qoidasi jadval kunidagi bilan AYNAN bir xil — bitta kod.
            var input = new WorkScheduleDayInput(
                IsoDayIndex(day.Date),
                day.IsWorking,
                day.StartTime,
                day.EndTime,
                BreakMinutes(day),
                day.BreakStart,
                day.BreakEnd);

            var valid = WorkScheduleDay.Validate(input);
            if (valid.IsFailure)
            {
                return valid;
            }
        }

        return Result.Success();
    }

    /// <summary>Tanaffus uzunligi — oynaning o'zidan (alohida son so'ralmaydi).</summary>
    /// <remarks>
    /// <c>TimeOnly</c> ayirmasi yarim tundan o'tib hisoblanadi — tungi smenadagi
    /// 23:30–00:30 tanaffus 60 daqiqa (<c>DayPlan</c> dagi bilan bir xil).
    /// </remarks>
    internal static int BreakMinutes(ScheduleChangeDayInput day)
        => day is { IsWorking: true, BreakStart: { } start, BreakEnd: { } end } && end != start
            ? (int)(end - start).TotalMinutes
            : 0;

    private static int IsoDayIndex(DateOnly date)
        => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

/// <summary>Jadval so'rovining bitta kuni.</summary>
public sealed class ScheduleChangeDay : Entity
{
    private ScheduleChangeDay(Guid id, Guid requestId, DateOnly date)
        : base(id)
    {
        RequestId = requestId;
        Date = date;
    }

    /// <summary>Faqat EF Core uchun.</summary>
    private ScheduleChangeDay() { }

    public Guid RequestId { get; private set; }

    public DateOnly Date { get; private set; }

    public bool IsWorking { get; private set; }

    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    public TimeOnly? BreakStart { get; private set; }

    public TimeOnly? BreakEnd { get; private set; }

    /// <summary>Almashtirishda juftining sanasi; «kunni o'zgartirish» da <c>null</c>.</summary>
    public DateOnly? SwapWith { get; private set; }

    /// <summary>Tanaffus uzunligi (daqiqa) — oynadan.</summary>
    public int BreakMinutes => IsWorking && BreakStart is { } start && BreakEnd is { } end && end != start
        ? (int)(end - start).TotalMinutes
        : 0;

    internal static ScheduleChangeDay Create(Guid requestId, ScheduleChangeDayInput input)
    {
        var day = new ScheduleChangeDay(NewId(), requestId, input.Date)
        {
            IsWorking = input.IsWorking,
            SwapWith = input.SwapWith,
        };

        if (input.IsWorking)
        {
            day.StartTime = input.StartTime;
            day.EndTime = input.EndTime;
            day.BreakStart = input.BreakStart;
            day.BreakEnd = input.BreakEnd;
        }

        return day;
    }
}
