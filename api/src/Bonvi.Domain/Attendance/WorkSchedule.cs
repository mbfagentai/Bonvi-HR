using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Jadval kunining tavsifi — domenga kiruvchi qiymat.
/// </summary>
/// <remarks>
/// Yettita kun bitta amalda keladi va ularning har birida oltita maydon
/// bor. Ularni alohida parametrlar bilan uzatish 42 ta argument degani
/// bo'lardi; qiymat yozuvi esa chaqiruv joyida nima nimaga tegishli
/// ekanini o'qiladigan qiladi.
/// </remarks>
/// <param name="DayIndex">ISO hafta kuni (1 — dushanba … 7 — yakshanba).</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Smena boshlanishi — ish kunida majburiy.</param>
/// <param name="EndTime">Smena tugashi — ish kunida majburiy.</param>
/// <param name="BreakMinutes">Haq to'lanmaydigan tanaffus (daqiqa).</param>
/// <param name="BreakStart">Tanaffus oynasining boshlanishi.</param>
/// <param name="BreakEnd">Tanaffus oynasining tugashi.</param>
/// <param name="IsFlexible">
/// «Erkin ish jadvali» (SPEC-erkin-jadval §2): kechikish va erta ketish yo'q, kun ishlangan
/// vaqt bilan baholanadi. Faqat ish kunida — dam olish kunida jimgina tozalanadi.
/// </param>
/// <param name="RequiredMinutes">
/// «Ish vaqti» — erkin kunning normasi (daqiqa). <c>null</c> — sukut: smena − tanaffus.
/// Erkin bo'lmagan kunda e'tiborga olinmaydi.
/// </param>
public readonly record struct WorkScheduleDayInput(
    int DayIndex,
    bool IsWorking,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int BreakMinutes = 0,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null);

/// <summary>
/// Ish jadvali — «odam qachon kutiladi» degan savolning javobi.
/// </summary>
/// <remarks>
/// <para>
/// Agregat ildizi; kunlar (<see cref="WorkScheduleDay"/>) uning ichida
/// yashaydi va o'z repozitoriysiga ega emas — <c>Employment</c> va
/// <c>EmploymentEvent</c> dagi bilan aynan bir xil shakl.
/// </para>
/// <para>
/// ⚠️ <b>Jadval «kim kirishga haqli» degani EMAS.</b> U odam qachon
/// KUTILISHINI aytadi; ruxsat esa butunlay boshqa fakt va u kelajakda
/// alohida agregat bo'ladi (SPEC §2.13). Shu sababli bu yerda eshik
/// bilan bog'liq birorta maydon yo'q.
/// </para>
/// <para>
/// v1 da faqat haftalik naqsh ishlaydi. <see cref="WorkScheduleKind.Cycle"/>
/// rad etiladi — bu kamchilik, qayta yozish emas: 2/2 qo'shilganda
/// <see cref="WorkScheduleDay.DayIndex"/> ning ma'nosi allaqachon
/// ikki xil o'qishga tayyor.
/// </para>
/// </remarks>
public sealed class WorkSchedule : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>Haftalik jadvalda har doim shuncha kun bo'ladi.</summary>
    public const int WeeklyDayCount = 7;

    /// <summary>Yon berishning eng katta qiymati (daqiqa).</summary>
    /// <remarks>
    /// Ikki soat. Undan kattasi yon berish emas — bu boshqa smena, va
    /// uni jadvalning o'zida ko'rsatish kerak, aks holda tabel
    /// «kechikish» ni umuman hisoblamay qo'yardi.
    /// </remarks>
    public const int MaxGraceMinutes = 120;

    private readonly List<WorkScheduleDay> _days = [];

    private WorkSchedule(Guid id, string name, WorkScheduleKind kind)
        : base(id)
    {
        Name = name;
        Kind = kind;
        GraceInMinutes = AttendanceRules.DefaultGraceInMinutes;
        GraceOutMinutes = AttendanceRules.DefaultGraceOutMinutes;
        IsActive = true;
    }

    /// <summary>EF Core uchun.</summary>
    private WorkSchedule() => Name = string.Empty;

    /// <summary>Jadval nomi — «Ofis 9–18», «Tungi smena 20–08».</summary>
    public string Name { get; private set; }

    public WorkScheduleKind Kind { get; private set; }

    /// <summary>Sikl uzunligi (kun). Haftalik jadvalda <c>null</c>.</summary>
    /// <remarks>
    /// v1 da har doim <c>null</c>: aylanma jadvallar hali yo'q. Ustun
    /// oldindan qo'yilgan, chunki u o'sha paytda bo'sh qoldirilsa ham
    /// ma'lumot ko'chirishni talab qilmaydi.
    /// </remarks>
    public int? CycleLengthDays { get; private set; }

    /// <summary>Kelishdagi yon berish (daqiqa) — shu ichida kechikish hisoblanmaydi.</summary>
    public int GraceInMinutes { get; private set; }

    /// <summary>Ketishdagi yon berish (daqiqa).</summary>
    public int GraceOutMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Jadval kunlari — haftalik jadvalda har doim yettita.</summary>
    public IReadOnlyList<WorkScheduleDay> Days => _days.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Yangi jadval yaratadi — yettala kuni bilan birga.</summary>
    public static Result<WorkSchedule> Create(
        string? name,
        IReadOnlyCollection<WorkScheduleDayInput> days,
        WorkScheduleKind kind = WorkScheduleKind.Weekly,
        int graceInMinutes = AttendanceRules.DefaultGraceInMinutes,
        int graceOutMinutes = AttendanceRules.DefaultGraceOutMinutes,
        string? note = null)
    {
        var trimmedName = Trim(name);
        if (trimmedName is null)
        {
            return Error.Validation("davomat.jadval_nom_bosh", "Jadval nomi kiritilishi shart.");
        }

        if (kind != WorkScheduleKind.Weekly)
        {
            return Error.Validation(
                "davomat.jadval_turi_qollanmaydi",
                "Hozircha faqat haftalik jadval qo'llab-quvvatlanadi.");
        }

        var check = Result.Combine(ValidateGrace(graceInMinutes, graceOutMinutes), ValidateDays(days));
        if (check.IsFailure)
        {
            return check.Error;
        }

        var schedule = new WorkSchedule(NewId(), trimmedName, kind)
        {
            GraceInMinutes = graceInMinutes,
            GraceOutMinutes = graceOutMinutes,
            Note = Trim(note),
        };

        foreach (var day in days.OrderBy(item => item.DayIndex))
        {
            schedule._days.Add(WorkScheduleDay.Create(schedule.Id, day));
        }

        return schedule;
    }

    /// <summary>
    /// Jadvalni qayta yozadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Turi O'ZGARMAYDI: haftalikni aylanmaga aylantirish — boshqa jadval,
    /// tuzatish emas, va unga biriktirilgan xodimlarning tabeli bir
    /// kechada boshqacha bo'lib qolardi.
    /// </para>
    /// <para>
    /// ⚠️ Tahrir O'TGAN kunlarni ham o'zgartiradi: jadval versiyalanmaydi.
    /// Shu sababli qayta hisoblash bu amaldan keyin qo'lda ishga
    /// tushiriladi (SPEC §5.8 — «to'rtta qonuniy tuzatish»dan biri).
    /// </para>
    /// </remarks>
    public Result Update(
        string? name,
        IReadOnlyCollection<WorkScheduleDayInput> days,
        int graceInMinutes,
        int graceOutMinutes,
        string? note = null)
    {
        var trimmedName = Trim(name);
        if (trimmedName is null)
        {
            return Error.Validation("davomat.jadval_nom_bosh", "Jadval nomi kiritilishi shart.");
        }

        var check = Result.Combine(ValidateGrace(graceInMinutes, graceOutMinutes), ValidateDays(days));
        if (check.IsFailure)
        {
            return check.Error;
        }

        Name = trimmedName;
        GraceInMinutes = graceInMinutes;
        GraceOutMinutes = graceOutMinutes;
        Note = Trim(note);

        foreach (var input in days)
        {
            var day = _days.FirstOrDefault(item => item.DayIndex == input.DayIndex);

            if (day is null)
            {
                _days.Add(WorkScheduleDay.Create(Id, input));
                continue;
            }

            day.Apply(input);
        }

        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static Result ValidateGrace(int graceInMinutes, int graceOutMinutes)
        => graceInMinutes is < 0 or > MaxGraceMinutes || graceOutMinutes is < 0 or > MaxGraceMinutes
            ? Error.Validation(
                "davomat.jadval_yon_berish",
                $"Yon berish 0 dan {MaxGraceMinutes} daqiqagacha bo'lishi kerak.")
            : Result.Success();

    /// <summary>
    /// Yettala kunni BIRGA tekshiradi.
    /// </summary>
    /// <remarks>
    /// Kunlar to'plami aynan 1…7 bo'lishi kerak. Faqat «yettita qator
    /// bormi» deb sanash yetmaydi: {1,2,3,4,5,6,9} ham yettita, lekin
    /// unda dushanba topiladi-yu, yakshanba topilmaydi va hal qiluvchi
    /// jimgina «jadval yo'q» deb javob berardi.
    /// </remarks>
    private static Result ValidateDays(IReadOnlyCollection<WorkScheduleDayInput> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        var indexes = days.Select(day => day.DayIndex).ToList();

        if (indexes.Distinct().Count() != indexes.Count)
        {
            return Error.Validation("davomat.jadval_kun_takror", "Bitta kun ikki marta ko'rsatilgan.");
        }

        if (indexes.Count != WeeklyDayCount || indexes.Exists(index => index is < 1 or > WeeklyDayCount))
        {
            return Error.Validation(
                "davomat.jadval_kun_yetishmaydi",
                "Haftalik jadvalda 1 dan 7 gacha bo'lgan yettala kun ko'rsatilishi shart.");
        }

        foreach (var day in days)
        {
            var result = WorkScheduleDay.Validate(day);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
