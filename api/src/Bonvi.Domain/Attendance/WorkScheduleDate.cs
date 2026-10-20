using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>Bitta SANA uchun kiritiladigan qiymat (yillik to'r katagi).</summary>
/// <param name="Date">Toshkent sanasi.</param>
/// <param name="IsWorking">Ish kunimi.</param>
/// <param name="StartTime">Boshlanish — ish kunida majburiy.</param>
/// <param name="EndTime">Tugash — ish kunida majburiy.</param>
/// <param name="BreakMinutes">Ishlangan vaqtdan ayiriladigan tanaffus.</param>
/// <param name="BreakStart">Tanaffus oynasining boshi.</param>
/// <param name="BreakEnd">Tanaffus oynasining oxiri.</param>
/// <param name="IsFlexible">«Erkin ish jadvali» — haftalik kundagi bilan bir xil ma'no.</param>
/// <param name="RequiredMinutes">«Ish vaqti»; <c>null</c> — sukut (smena − tanaffus).</param>
public readonly record struct WorkScheduleDateInput(
    DateOnly Date,
    bool IsWorking,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int BreakMinutes = 0,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null);

/// <summary>
/// Jadvalning ANIQ SANAGA yozilgan kuni — yillik to'rning katagi
/// (SPEC-jadval-to'ri §B1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu haftalik naqshning USTIDAGI qatlam.</b> Sanasi yozilgan kun
/// naqshni bekor qiladi, yozilmagani esa naqshdan keladi
/// (<c>DayPlan.For</c>). Shu sababli yangi jadval uchun 365 katakni
/// to'ldirish shart emas va yil almashganda hech kim «jadvalsiz» bo'lib
/// qolmaydi — Verifix'da jadval YILGA bog'langani uchun aynan shunday
/// bo'lardi.
/// </para>
/// <para>
/// <b>Nega jadvalning ichidagi to'plam emas, alohida yozuv.</b> Bir
/// jadvalda bir necha yillik sana bo'lishi mumkin (26 jadval × 365 kun),
/// hisoblash esa har safar faqat SO'RALGAN oraliqni o'qiydi. To'plam
/// bo'lsa, EF har jadval bilan birga barcha sanalarni tortib kelardi.
/// </para>
/// <para>
/// Qoidalar haftalik kun bilan AYNAN bir xil (<see cref="WorkScheduleDay.Validate"/>):
/// ish kunida ikkala vaqt ham shart, tanaffus oynasi smena ichida bo'ladi,
/// yarim tundan o'tadigan smena ruxsat etiladi.
/// </para>
/// </remarks>
public sealed class WorkScheduleDate : Entity, IAuditable
{
    private WorkScheduleDate(Guid id, Guid workScheduleId, DateOnly date)
        : base(id)
    {
        WorkScheduleId = workScheduleId;
        Date = date;
    }

    /// <summary>EF Core uchun.</summary>
    private WorkScheduleDate() { }

    public Guid WorkScheduleId { get; private set; }

    /// <summary>Toshkent sanasi — jadval bilan birga YAGONA.</summary>
    public DateOnly Date { get; private set; }

    public bool IsWorking { get; private set; }

    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    public int BreakMinutes { get; private set; }

    public TimeOnly? BreakStart { get; private set; }

    public TimeOnly? BreakEnd { get; private set; }

    /// <summary>«Erkin ish jadvali» — faqat ish kunida (<see cref="WorkScheduleDay.IsFlexible"/>).</summary>
    public bool IsFlexible { get; private set; }

    /// <summary>«Ish vaqti» (daqiqa); erkin bo'lmagan katakda <c>null</c>.</summary>
    public int? RequiredMinutes { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Kiritilgan qiymatni tekshiradi — haftalik kun bilan bir xil qoida.</summary>
    public static Result Validate(WorkScheduleDateInput input)
        => WorkScheduleDay.Validate(AsDayInput(input));

    public static Result<WorkScheduleDate> Create(Guid workScheduleId, WorkScheduleDateInput input)
    {
        if (workScheduleId == Guid.Empty)
        {
            return Error.Validation("davomat.jadval_yoq", "Jadval ko'rsatilishi shart.");
        }

        var check = Validate(input);
        if (check.IsFailure)
        {
            return check.Error;
        }

        var date = new WorkScheduleDate(NewId(), workScheduleId, input.Date);
        date.Apply(input);

        return date;
    }

    /// <summary>Mavjud katakni yangi qiymat bilan almashtiradi.</summary>
    public Result Update(WorkScheduleDateInput input)
    {
        var check = Validate(input);
        if (check.IsFailure)
        {
            return check.Error;
        }

        Apply(input);

        return Result.Success();
    }

    private void Apply(WorkScheduleDateInput input)
    {
        IsWorking = input.IsWorking;

        // Dam olish kunida vaqtlar TOZALANADI — haftalik kundagi qoidaning
        // o'zi: saqlab qo'yilgan vaqt keyin «nega dam olish kunining
        // soati bor?» degan savolni tug'dirardi. Erkinlik ham.
        if (!input.IsWorking)
        {
            StartTime = null;
            EndTime = null;
            BreakMinutes = 0;
            BreakStart = null;
            BreakEnd = null;
            IsFlexible = false;
            RequiredMinutes = null;
            return;
        }

        StartTime = input.StartTime;
        EndTime = input.EndTime;
        BreakMinutes = input.BreakMinutes;
        BreakStart = input.BreakStart;
        BreakEnd = input.BreakEnd;
        IsFlexible = input.IsFlexible;
        RequiredMinutes = WorkScheduleDay.RequiredFor(AsDayInput(input));
    }

    /// <summary>Katakni haftalik kun shakliga o'giradi — tekshiruv ham, norma sukuti ham bitta kod.</summary>
    private static WorkScheduleDayInput AsDayInput(WorkScheduleDateInput input)
        => new(
            // Kun indeksi bu yerda ma'nosiz: tekshiruv uni o'qimaydi
            // (u haftalik to'plamning butunligini tekshirishda ishlatiladi).
            DayIndex: 1,
            input.IsWorking,
            input.StartTime,
            input.EndTime,
            input.BreakMinutes,
            input.BreakStart,
            input.BreakEnd,
            input.IsFlexible,
            input.RequiredMinutes);
}
