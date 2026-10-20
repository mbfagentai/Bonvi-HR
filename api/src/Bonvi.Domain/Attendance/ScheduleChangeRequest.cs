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
/// <param name="BreakMinutes">
/// Ayiriladigan tanaffus (daqiqa). <c>null</c> — oynaning o'zi (Verifix formasidagidek);
/// jadvaldan olingan kunda jadvalning o'z qiymati, chunki u yerda uzunlik va oyna alohida
/// (60 daqiqalik tushlik 12:00–15:00 oynasida).
/// </param>
/// <param name="IsFlexible">
/// «Erkin ish jadvali» (Verifix formasidagi belgi, SPEC-erkin-jadval §1 O1); almashtirishda —
/// juft sananing rejasidan.
/// </param>
/// <param name="RequiredMinutes">«Ish vaqti»; <c>null</c> — sukut (smena − tanaffus).</param>
public sealed record ScheduleChangeDayInput(
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    DateOnly? SwapWith = null,
    int? BreakMinutes = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null);

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

    /// <summary>Bitta so'rov sanalarining eng katta davri (birinchisidan oxirgisigacha, kun).</summary>
    /// <remarks>Qayta hisoblash oynasi bilan bir xil — tasdiq bitta oynada qayta hisoblanadi.</remarks>
    public const int MaxSpanDays = AttendanceRules.MaxRecomputeDays;

    /// <summary>Izohning eng katta uzunligi (ustun <c>varchar(2000)</c>).</summary>
    public const int MaxNoteLength = 2000;

    private const int MinYear = 2000;
    private const int MaxYear = 2100;

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

    /// <summary>Parallellik hisoblagichi — har bir holat o'zgarishida +1 (<see cref="AbsenceRequest.Version"/> bilan bir xil qoida).</summary>
    public int Version { get; private set; }

    /// <summary>Kutayotgan so'rov oxirgi marta qachon tahrirlangan (HR yoki xodim), UTC.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>Oxirgi tahrir ilovadan bo'lsa — xodimning ilova hisobi; HR tahririda <c>null</c>.</summary>
    public Guid? EditedByAccountId { get; private set; }

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
    /// <param name="breakRule">
    /// «Kunni o'zgartirish» dagi tanaffus qoidasi (SPEC-sorov-tahriri §4) — yozish lahzasidagi
    /// sozlamadan; <c>null</c> — qoidasiz (eski chaqiruvlar va sinovlar).
    /// </param>
    public static Result<ScheduleChangeRequest> Create(
        Guid employeeId,
        ScheduleChangeKind kind,
        IReadOnlyCollection<ScheduleChangeDayInput> days,
        string? note = null,
        Guid? submittedByAccountId = null,
        RequestBreakRule? breakRule = null)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (employeeId == Guid.Empty)
        {
            return Error.Validation("davomat.sorov_xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        if (NoteTooLong(note) is { } tooLong)
        {
            return tooLong;
        }

        var request = new ScheduleChangeRequest(NewId(), employeeId, kind, Trim(note))
        {
            SubmittedByAccountId = submittedByAccountId,
        };

        var applied = request.ApplyDays(kind, days, breakRule);
        return applied.IsFailure ? applied.Error : request;
    }

    /// <summary>Qaror kutayotgan so'rovni tahrirlaydi.</summary>
    /// <param name="kind">Tur.</param>
    /// <param name="days">Kunlar.</param>
    /// <param name="note">Izoh.</param>
    /// <param name="breakRule">Tanaffus qoidasi — yaratishdagi bilan bir xil (<see cref="Create"/>).</param>
    /// <param name="at">Tahrir lahzasi — berilsa <see cref="EditedAt"/> ga yoziladi.</param>
    /// <param name="editedByAccountId">Ilovadan tahrirlagan xodim hisobi; HR tahririda <c>null</c>.</param>
    public Result Update(
        ScheduleChangeKind kind,
        IReadOnlyCollection<ScheduleChangeDayInput> days,
        string? note,
        RequestBreakRule? breakRule = null,
        DateTimeOffset? at = null,
        Guid? editedByAccountId = null)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (Status != AbsenceRequestStatus.Pending)
        {
            return Error.Conflict("davomat.sorov_qaror_qabul_qilingan", "Qaror qabul qilingan so'rovni tahrirlab bo'lmaydi.");
        }

        if (NoteTooLong(note) is { } tooLong)
        {
            return tooLong;
        }

        var validated = Validate(kind, days, breakRule);
        if (validated.IsFailure)
        {
            return validated;
        }

        Kind = kind;
        Note = Trim(note);
        _days.Clear();
        _days.AddRange(days.OrderBy(day => day.Date).Select(day => ScheduleChangeDay.Create(Id, day)));
        Version++;

        if (at is { } editedAt)
        {
            EditedAt = editedAt;
            EditedByAccountId = editedByAccountId;
        }

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
        Version++;

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
        Version++;

        return Result.Success();
    }

    private Result ApplyDays(
        ScheduleChangeKind kind,
        IReadOnlyCollection<ScheduleChangeDayInput> days,
        RequestBreakRule? breakRule)
    {
        var validated = Validate(kind, days, breakRule);
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

        if (NoteTooLong(decisionNote) is { } tooLong)
        {
            return tooLong;
        }

        Status = status;
        DecidedBy = decidedBy;
        DecidedAt = at;
        DecisionNote = Trim(decisionNote);
        Version++;

        return Result.Success();
    }

    /// <summary>
    /// Sanalar to'plamining shakli: tur, soni, takror, yil va davr — kunlarning ichiga
    /// qaramasdan.
    /// </summary>
    /// <remarks>
    /// ⭐ Ochiq, chunki use case uni rejani o'qishdan OLDIN chaqiradi: almashtirishda
    /// rejalar har bir sana uchun bazadan o'qiladi, va cheklanmagan ro'yxat yoki keng
    /// davr 2 GB serverda xotirani to'ldirardi. Domen tekshiruvi ham shuni chaqiradi —
    /// qoida bitta joyda.
    /// </remarks>
    /// <param name="kind">Tur.</param>
    /// <param name="dates">So'rovning barcha sanalari (almashtirishda juftlarning ikkala tomoni).</param>
    public static Result ValidateDates(ScheduleChangeKind kind, IReadOnlyCollection<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);

        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("davomat.jadval_sorovi_turi", "So'rov turi noto'g'ri.");
        }

        if (dates.Count == 0)
        {
            return Error.Validation("davomat.jadval_sorovi_kun_yoq", "Kamida bitta sana ko'rsatilishi shart.");
        }

        var limit = kind == ScheduleChangeKind.Swap ? MaxSwapPairs * 2 : MaxDays;
        if (dates.Count > limit)
        {
            return Error.Validation("davomat.jadval_sorovi_kop", $"Bitta so'rovda {limit} tadan ko'p sana bo'lishi mumkin emas.");
        }

        if (dates.Distinct().Count() != dates.Count)
        {
            return Error.Validation("davomat.jadval_sorovi_sana_takror", "Bitta sana so'rovda ikki marta bo'lishi mumkin emas.");
        }

        if (dates.Any(date => date.Year is < MinYear or > MaxYear))
        {
            return Error.Validation("davomat.jadval_sorovi_sana", "Sana noto'g'ri.");
        }

        if (dates.Max().DayNumber - dates.Min().DayNumber + 1 > MaxSpanDays)
        {
            return Error.Validation(
                "davomat.jadval_sorovi_davri",
                $"Bitta so'rov sanalari {MaxSpanDays} kunlik davrdan oshmasligi kerak.");
        }

        return Result.Success();
    }

    /// <summary>Kunlar qoidasi — yaratishda ham, tahrirlashda ham bir xil.</summary>
    /// <remarks>
    /// Tanaffus qoidasi (<paramref name="breakRule"/>) har bir kunning MAVJUD qoidalaridan keyin va
    /// faqat «Kunni o'zgartirish» da: almashtirishda kunni server jadvaldan ko'chiradi, xodim
    /// tanaffus tanlamaydi (SPEC-sorov-tahriri §4.2).
    /// </remarks>
    private static Result Validate(
        ScheduleChangeKind kind,
        IReadOnlyCollection<ScheduleChangeDayInput> days,
        RequestBreakRule? breakRule)
    {
        var shape = ValidateDates(kind, [.. days.Select(day => day.Date)]);
        if (shape.IsFailure)
        {
            return shape;
        }

        foreach (var day in days)
        {
            // Smena, tanaffus va erkin kun qoidasi jadval kunidagi bilan AYNAN bir xil — bitta kod.
            var input = DayInput(day);

            var valid = WorkScheduleDay.Validate(input);
            if (valid.IsFailure)
            {
                return valid;
            }

            if (kind == ScheduleChangeKind.Change && breakRule is not null)
            {
                var rule = breakRule.Validate(day);
                if (rule.IsFailure)
                {
                    return rule;
                }
            }
        }

        return Result.Success();
    }

    /// <summary>Tanaffus uzunligi — berilgan bo'lsa o'zi, aks holda oynaning o'zidan.</summary>
    /// <remarks>
    /// <c>TimeOnly</c> ayirmasi yarim tundan o'tib hisoblanadi — tungi smenadagi
    /// 23:30–00:30 tanaffus 60 daqiqa (<c>DayPlan</c> dagi bilan bir xil).
    /// </remarks>
    internal static int BreakMinutes(ScheduleChangeDayInput day)
    {
        if (!day.IsWorking)
        {
            return 0;
        }

        if (day.BreakMinutes is { } given)
        {
            return given;
        }

        return day is { BreakStart: { } start, BreakEnd: { } end } && end != start
            ? (int)(end - start).TotalMinutes
            : 0;
    }

    /// <summary>So'ralgan kun — jadval kuni shaklida, AMALDAGI tanaffus daqiqasi bilan.</summary>
    /// <remarks>
    /// Erkin kun normasining sukuti (smena − tanaffus) shu daqiqadan chiqadi: berilgan
    /// <c>breakMinutes</c>, aks holda oynaning uzunligi (SPEC-erkin-jadval §2.5).
    /// </remarks>
    internal static WorkScheduleDayInput DayInput(ScheduleChangeDayInput day)
        => new(
            IsoDayIndex(day.Date),
            day.IsWorking,
            day.StartTime,
            day.EndTime,
            BreakMinutes(day),
            day.BreakStart,
            day.BreakEnd,
            day.IsFlexible,
            day.RequiredMinutes);

    private static Error? NoteTooLong(string? note)
        => note?.Trim().Length > MaxNoteLength
            ? Error.Validation("davomat.izoh_uzun", $"Izoh {MaxNoteLength} belgidan oshmasligi kerak.")
            : null;

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

    /// <summary>Ayiriladigan tanaffus (daqiqa); dam olish kunida 0.</summary>
    public int BreakMinutes { get; private set; }

    /// <summary>«Erkin ish jadvali» — faqat ish kunida (<see cref="WorkScheduleDay.IsFlexible"/>).</summary>
    public bool IsFlexible { get; private set; }

    /// <summary>«Ish vaqti» (daqiqa) — berilgani yoki sukut; erkin bo'lmagan kunda <c>null</c>.</summary>
    /// <remarks>Sukut ham shu yerda yoziladi: HR tasdiqlagan narsa — u ko'rgan son.</remarks>
    public int? RequiredMinutes { get; private set; }

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
            day.BreakMinutes = ScheduleChangeRequest.BreakMinutes(input);
            day.IsFlexible = input.IsFlexible;
            day.RequiredMinutes = WorkScheduleDay.RequiredFor(ScheduleChangeRequest.DayInput(input));
        }

        return day;
    }
}
