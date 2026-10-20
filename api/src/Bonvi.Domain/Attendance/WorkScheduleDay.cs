using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Jadvalning bitta kuni — <see cref="WorkSchedule"/> agregatining ichida.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dam olish kuni ham QATOR bo'lib turadi</b> (<c>is_working = false</c>),
/// ya'ni haftalik jadvalda har doim aynan yettita qator bo'ladi. Sabab:
/// «qator yo'q» va «dam olish kuni» shundagina bitta narsa bo'ladi va
/// tabel «bu kun dam olishmi yoki umuman ta'riflanmaganmi» degan savolni
/// hech qachon berishi kerak emas.
/// </para>
/// <para>
/// ⭐ <b>Tungi smena ustunda EMAS, hisoblanadi</b> — <see cref="CrossesMidnight"/>
/// izohiga qarang.
/// </para>
/// </remarks>
public sealed class WorkScheduleDay : Entity
{
    /// <summary>Tanaffusning eng katta uzunligi (daqiqa).</summary>
    /// <remarks>
    /// Sakkiz soat. Undan kattasi tanaffus emas — bu boshqa smena, va uni
    /// ikkinchi jadval kuni sifatida ta'riflash kerak.
    /// </remarks>
    public const int MaxBreakMinutes = 480;

    /// <summary>Erkin kundagi «Ish vaqti» ning eng kichik qiymati (daqiqa).</summary>
    /// <remarks>Eng kattasi — <see cref="MaxRequiredMinutes"/> (smena − tanaffus), SPEC-erkin-jadval §2.5.</remarks>
    public const int MinRequiredMinutes = 1;

    private WorkScheduleDay(Guid id, Guid workScheduleId, int dayIndex)
        : base(id)
    {
        WorkScheduleId = workScheduleId;
        DayIndex = dayIndex;
    }

    /// <summary>EF Core uchun.</summary>
    private WorkScheduleDay() { }

    public Guid WorkScheduleId { get; private set; }

    /// <summary>
    /// Kun raqami: <see cref="WorkScheduleKind.Weekly"/> uchun ISO hafta kuni
    /// (1 — dushanba … 7 — yakshanba), <see cref="WorkScheduleKind.Cycle"/>
    /// uchun esa 1 dan boshlanadigan sikl kuni.
    /// </summary>
    /// <remarks>
    /// Ikki ma'noli nom ATAYLAB: 2/2 kabi aylanma jadval qo'shilganda shu
    /// ustun o'zgarishsiz qoladi — yangi jadval ham, ma'lumot ko'chirish
    /// ham kerak bo'lmaydi, faqat hal qiluvchi (resolver) va biriktirmadagi
    /// tayanch sana qo'shiladi.
    /// </remarks>
    public int DayIndex { get; private set; }

    /// <summary>Bu kun ish kunimi.</summary>
    public bool IsWorking { get; private set; }

    /// <summary>Smena boshlanishi. Ish kuni bo'lmasa <c>null</c>.</summary>
    public TimeOnly? StartTime { get; private set; }

    /// <summary>Smena tugashi. Ish kuni bo'lmasa <c>null</c>.</summary>
    public TimeOnly? EndTime { get; private set; }

    /// <summary>Haq to'lanmaydigan tanaffus (daqiqa).</summary>
    public int BreakMinutes { get; private set; }

    /// <summary>Tanaffus oynasining boshlanishi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega oyna, faqat uzunlik emas.</b> Tanaffus ishlangan vaqtdan
    /// ayiriladi, lekin faqat AYNAN shu oraliqda: aks holda uyga erta
    /// ketgan odamning tushligi ham ayirilardi, garchi u tushlikda
    /// ishlagan bo'lsa ham. Uzunlik («60 daqiqa») qancha ayirishni
    /// aytadi, oyna esa QAYERDAN ayirishni.
    /// </para>
    /// <para>
    /// Ikkovi ham bo'sh bo'lishi mumkin — o'shanda smenada belgilangan
    /// tanaffus yo'q. Bittasini qoldirib, ikkinchisini bo'sh tashlab
    /// bo'lmaydi: yarim oyna hech narsani ifodalamaydi.
    /// </para>
    /// </remarks>
    public TimeOnly? BreakStart { get; private set; }

    /// <summary>Tanaffus oynasining tugashi.</summary>
    public TimeOnly? BreakEnd { get; private set; }

    /// <summary>
    /// «Erkin ish jadvali» — kun boshlanish/tugash vaqti bilan emas, ishlangan vaqt bilan
    /// baholanadi (SPEC-erkin-jadval §2); boshlanish–tugash faqat yo'l-yo'riq.
    /// </summary>
    /// <remarks>
    /// Faqat ish kunida <c>true</c> bo'ladi: dam olish kunida bayroq yozishda tozalanadi
    /// (<see cref="Apply"/>), tanaffus oynasi bilan bir xil qoida.
    /// </remarks>
    public bool IsFlexible { get; private set; }

    /// <summary>
    /// «Ish vaqti» — erkin kunning normasi (daqiqa); erkin bo'lmagan kunda <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Berilmasa server sukutni o'zi yozadi — <see cref="MaxRequiredMinutes"/>
    /// (08:30–18:00, 120 daqiqa tanaffus → 450, ya'ni 7:30).
    /// </remarks>
    public int? RequiredMinutes { get; private set; }

    /// <summary>
    /// Smena yarim tundan o'tadimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Bu USTUN emas, hisoblanadigan xossa</b> — taklif qilingan
    /// shaklda «crosses-midnight bayrog'i» ustun edi.
    /// </para>
    /// <para>
    /// Sabab: saqlangan bayroq o'zi tasvirlayotgan ikki vaqtga ZID bo'lib
    /// qolishi mumkin. Kimdir <c>end_time</c> ni 18:00 dan 02:00 ga
    /// o'zgartirib, bayroqqa tegmagan birinchi kuniyoq tabel manfiy
    /// oraliq hisoblab, −16 soatni MA'LUMOT sifatida ko'rsatardi.
    /// Hisoblangan xossa esa hech qachon zid bo'la olmaydi.
    /// </para>
    /// </remarks>
    public bool CrossesMidnight
        => IsWorking && StartTime is { } start && EndTime is { } end && end <= start;

    /// <summary>
    /// Kun tavsifining to'g'riligini tekshiradi — yozishdan OLDIN.
    /// </summary>
    /// <remarks>
    /// Tekshiruv yaratish va tahrirdan ajratilgan: <see cref="WorkSchedule"/>
    /// avval yettala kunni tekshiradi, keyin ularni qo'llaydi. Shunda
    /// oltinchi kundagi xato birinchi beshtasini allaqachon o'zgartirib
    /// bo'lgan agregatni qoldirmaydi.
    /// </remarks>
    internal static Result Validate(WorkScheduleDayInput input)
    {
        if (!input.IsWorking)
        {
            // Dam olish kunida vaqtlar ma'nosiz — ular TOZALANADI
            // (`Apply` ga qarang), ya'ni bu yerda tekshiradigan narsa yo'q.
            return Result.Success();
        }

        if (input.StartTime is not { } start || input.EndTime is not { } end)
        {
            return Error.Validation(
                "davomat.jadval_vaqt_bosh",
                "Ish kunida boshlanish va tugash vaqti ko'rsatilishi shart.");
        }

        if (start == end)
        {
            // Nol uzunlik va 24 soat orasida farq yo'q — ikkalasi ham
            // haqiqiy smena emas.
            return Error.Validation(
                "davomat.jadval_vaqt_teng",
                "Smenaning boshlanishi va tugashi bir xil bo'lishi mumkin emas.");
        }

        if (input.BreakMinutes is < 0 or > MaxBreakMinutes)
        {
            return Error.Validation(
                "davomat.jadval_tanaffus_uzunligi",
                $"Tanaffus 0 dan {MaxBreakMinutes} daqiqagacha bo'lishi kerak.");
        }

        var window = ValidateBreakWindow(input.BreakStart, input.BreakEnd, start, end);
        if (window.IsFailure)
        {
            return window;
        }

        // Erkin kun qoidasi OXIRGI (SPEC-erkin-jadval §2.5): avval smena va tanaffus to'g'ri
        // bo'lishi kerak — norma ularning ayirmasidan chiqadi.
        return ValidateRequired(input, start, end);
    }

    /// <summary>
    /// Erkin kundagi «Ish vaqti» ning eng katta qiymati: smena uzunligi − tanaffus (daqiqa).
    /// </summary>
    /// <remarks>
    /// Norma jadvalning o'z oynasiga sig'ishi kerak. Smena uzunligi <c>TimeOnly</c> ayirmasi —
    /// yarim tundan o'tib hisoblanadi (20:00–08:00 = 720), tanaffus oynasi tekshiruvidagi
    /// bilan bir xil o'q. Pastki chegara 0: tanaffus smenadan uzun bo'lsa erkin kun mumkin emas.
    /// </remarks>
    /// <param name="start">Smena boshlanishi.</param>
    /// <param name="end">Smena tugashi.</param>
    /// <param name="breakMinutes">Ayiriladigan tanaffus (daqiqa).</param>
    public static int MaxRequiredMinutes(TimeOnly start, TimeOnly end, int breakMinutes)
        => Math.Max(0, (int)(end - start).TotalMinutes - breakMinutes);

    /// <summary>Tekshirilgan tavsifdan yangi kun yasaydi.</summary>
    internal static WorkScheduleDay Create(Guid workScheduleId, WorkScheduleDayInput input)
    {
        var day = new WorkScheduleDay(NewId(), workScheduleId, input.DayIndex);
        day.Apply(input);
        return day;
    }

    /// <summary>
    /// Mavjud kunni tekshirilgan tavsif bilan qayta yozadi.
    /// </summary>
    /// <remarks>
    /// Kun ALMASHTIRILMAYDI, o'zgartiriladi: yettita qator har tahrirda
    /// o'chirilib qayta yozilsa, ularning identifikatorlari o'zgarib
    /// turardi va bunga hech qanday ehtiyoj yo'q.
    /// </remarks>
    internal void Apply(WorkScheduleDayInput input)
    {
        IsWorking = input.IsWorking;

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
        RequiredMinutes = RequiredFor(input);
    }

    /// <summary>
    /// Saqlanadigan «Ish vaqti»: erkin ish kunida berilgani yoki sukut, aks holda <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Yillik to'r katagi va jadval so'rovi kuni ham shu metoddan oladi — sukut uch joyda
    /// uch xil hisoblanmasin. Erkin bo'lmagan kunga yuborilgan qiymat tashlanadi (§2.4).
    /// </remarks>
    /// <param name="input">Tekshirilgan kun tavsifi.</param>
    internal static int? RequiredFor(WorkScheduleDayInput input)
        => input is { IsWorking: true, IsFlexible: true, StartTime: { } start, EndTime: { } end }
            ? input.RequiredMinutes ?? MaxRequiredMinutes(start, end, input.BreakMinutes)
            : null;

    /// <summary>Erkin kun normasi 1 … (smena − tanaffus) oralig'idami — SPEC-erkin-jadval §2.5.</summary>
    /// <remarks>
    /// Berilmagan norma sukutga (eng kattasiga) teng, ya'ni u ham shu yerdan o'tadi: tanaffus
    /// smenaga teng yoki undan uzun bo'lsa sukut 0 bo'ladi va erkin kun rad etiladi.
    /// </remarks>
    private static Result ValidateRequired(WorkScheduleDayInput input, TimeOnly start, TimeOnly end)
    {
        if (!input.IsFlexible)
        {
            return Result.Success();
        }

        var max = MaxRequiredMinutes(start, end, input.BreakMinutes);
        var required = input.RequiredMinutes ?? max;

        return required < MinRequiredMinutes || required > max
            ? Error.Validation(
                "davomat.jadval_erkin_vaqti",
                "Erkin jadvalda ish vaqti 1 daqiqadan smena − tanaffusgacha bo'lishi kerak.")
            : Result.Success();
    }

    /// <summary>
    /// Tanaffus oynasi smenaning ICHIDA yotadimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Taqqoslash soat ciferblatida emas, <b>smenaning o'z o'qida</b>
    /// bajariladi: har bir vaqt smena boshlanishidan qancha o'tganiga
    /// aylantiriladi. Tungi smena (20:00–08:00) uchun bu yagona to'g'ri
    /// yo'l — 01:00 ciferblatda 20:00 dan OLDIN, smena o'qida esa undan
    /// besh soat KEYIN.
    /// </para>
    /// <para>
    /// <c>TimeOnly</c> ayirmasi aynan shu ishni qiladi: natija har doim
    /// [0, 24 soat) oralig'ida va yarim tundan o'tib hisoblanadi.
    /// </para>
    /// </remarks>
    private static Result ValidateBreakWindow(
        TimeOnly? breakStart,
        TimeOnly? breakEnd,
        TimeOnly shiftStart,
        TimeOnly shiftEnd)
    {
        if (breakStart is null && breakEnd is null)
        {
            return Result.Success();
        }

        if (breakStart is not { } windowStart || breakEnd is not { } windowEnd)
        {
            return Error.Validation(
                "davomat.jadval_tanaffus_toliq_emas",
                "Tanaffus oynasining boshlanishi va tugashi birga ko'rsatilishi kerak.");
        }

        var shiftLength = shiftEnd - shiftStart;
        var fromShiftStart = windowStart - shiftStart;
        var toShiftStart = windowEnd - shiftStart;

        if (fromShiftStart >= toShiftStart || toShiftStart > shiftLength)
        {
            return Error.Validation(
                "davomat.jadval_tanaffus_oynasi",
                "Tanaffus oynasi smena ichida bo'lishi va uzunligi noldan katta bo'lishi kerak.");
        }

        return Result.Success();
    }
}
