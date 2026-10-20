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

    /// <summary>Izohning eng katta uzunligi (ustun <c>varchar(2000)</c>) — xodim izohi ham, HR qarori izohi ham.</summary>
    /// <remarks>
    /// Ilgari uzun izoh bazaga urilib 500 berardi; endi u 400 <c>davomat.izoh_uzun</c> —
    /// jadval so'rovidagi bilan bir xil son va bir xil kod.
    /// </remarks>
    public const int MaxNoteLength = 2000;

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

    /// <summary>
    /// So'rovni XODIMNING O'ZI ilovadan yuborgan bo'lsa — uning ilova hisobi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Nega <see cref="CreatedBy"/> emas: u <c>users</c> jadvalining
    /// identifikatori (HR foydalanuvchisi) va audit uni HR tokenidan to'ldiradi.
    /// Xodim tokenida bunday identifikator yo'q (xodim hisobi ataylab
    /// <c>users</c> da emas) va ilgari ilovadan kelgan so'rovda
    /// <see cref="CreatedBy"/> shunchaki <c>null</c> qolardi — «kim yubordi?»
    /// degan savol (SPEC-verifikatsiya-sorovlar §B2) javobsiz edi. Ikki xil
    /// identifikator fazosini bitta ustunga qo'shish esa har o'qishni «bu
    /// foydalanuvchimi yoki xodimmi?» degan taxminga majburlardi.
    /// </para>
    /// <para>
    /// <c>null</c> — so'rovni HR kiritgan (<see cref="CreatedBy"/> da).
    /// </para>
    /// </remarks>
    public Guid? SubmittedByAccountId { get; private set; }

    /// <summary>So'rovni XODIMNING O'ZI ilovadan bekor qilgan bo'lsa — uning ilova hisobi.</summary>
    /// <remarks>
    /// Xodim faqat qaror KUTAYOTGAN so'rovni bekor qila oladi
    /// (<see cref="CancelBySubmitter"/>), ya'ni HR ning qaror yozuvi
    /// (<see cref="DecidedBy"/>, <see cref="DecidedAt"/>, <see cref="DecisionNote"/>)
    /// hech qachon ilova tomonidan ustidan yozilmaydi.
    /// </remarks>
    public Guid? CancelledByAccountId { get; private set; }

    /// <summary>
    /// Parallellik hisoblagichi — har bir holat o'zgarishida +1 (EF concurrency token,
    /// SPEC-sorov-tahriri §3.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega kerak.</b> Xodim kutayotgan so'rovni ilovadan o'zgartira oladi, HR esa shu
    /// paytda uni tasdiqlashi mumkin. Hisoblagichsiz ikkinchi yozuv birinchisini jimgina ustidan
    /// yozardi: HR ko'rmagan davr tasdiqlangan bo'lib qolardi. Endi bazadagi <c>UPDATE</c>
    /// <c>version</c> bo'yicha shartli, kechikkan yozuv esa 409 <c>davomat.sorov_ozgargan</c>
    /// oladi; mijoz ko'rgan <c>version</c> ham tekshiriladi.
    /// </para>
    /// <para>
    /// Qo'lda hech qachon o'zgartirilmaydi — faqat shu agregatning holat o'zgartiruvchi
    /// metodlari (<see cref="Update"/>, <see cref="Approve"/>, <see cref="Reject"/>,
    /// <see cref="Cancel"/>, <see cref="CancelBySubmitter"/>).
    /// </para>
    /// </remarks>
    public int Version { get; private set; }

    /// <summary>Kutayotgan so'rov oxirgi marta qachon tahrirlangan (HR yoki xodim), UTC; tahrirlanmagan — <c>null</c>.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>
    /// Oxirgi tahrir ilovadan bo'lsa — xodimning ilova hisobi; HR tahrirlagan yoki hech kim
    /// tahrirlamagan bo'lsa <c>null</c> (<see cref="SubmittedByAccountId"/> bilan bir xil fazo).
    /// </summary>
    public Guid? EditedByAccountId { get; private set; }

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

    /// <summary>Yangi so'rov — «Kutilmoqda» holatida.</summary>
    /// <param name="employeeId">Kim yo'q bo'ladi.</param>
    /// <param name="absenceTypeId">Yo'qlik turi.</param>
    /// <param name="fromDate">Davr boshi.</param>
    /// <param name="toDate">Davr oxiri.</param>
    /// <param name="fromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda.</param>
    /// <param name="toTime">Kun ichidagi tugash — faqat bir kunlik so'rovda.</param>
    /// <param name="note">Xodimning izohi.</param>
    /// <param name="submittedByAccountId">
    /// Ilovadan yuborilgan bo'lsa — xodimning ilova hisobi
    /// (<see cref="SubmittedByAccountId"/>); HR kiritganda <c>null</c>.
    /// </param>
    public static Result<AbsenceRequest> Create(
        Guid employeeId,
        Guid absenceTypeId,
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly? fromTime = null,
        TimeOnly? toTime = null,
        string? note = null,
        Guid? submittedByAccountId = null)
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

        if (NoteTooLong(note) is { } tooLong)
        {
            return tooLong;
        }

        return new AbsenceRequest(
            NewId(),
            employeeId,
            absenceTypeId,
            fromDate,
            toDate,
            fromTime,
            toTime,
            Trim(note))
        {
            SubmittedByAccountId = submittedByAccountId,
        };
    }

    /// <summary>Kutayotgan so'rovni tahrirlash — qaror qabul qilinmaguncha.</summary>
    /// <param name="absenceTypeId">Yo'qlik turi.</param>
    /// <param name="fromDate">Davr boshi.</param>
    /// <param name="toDate">Davr oxiri.</param>
    /// <param name="fromTime">Kun ichidagi boshlanish.</param>
    /// <param name="toTime">Kun ichidagi tugash.</param>
    /// <param name="note">Izoh.</param>
    /// <param name="at">
    /// Tahrir lahzasi — berilsa <see cref="EditedAt"/> ga yoziladi (HR va ilova yo'li beradi).
    /// </param>
    /// <param name="editedByAccountId">Ilovadan tahrirlagan xodim hisobi; HR tahririda <c>null</c>.</param>
    public Result Update(
        Guid absenceTypeId,
        DateOnly fromDate,
        DateOnly toDate,
        TimeOnly? fromTime,
        TimeOnly? toTime,
        string? note,
        DateTimeOffset? at = null,
        Guid? editedByAccountId = null)
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

        if (NoteTooLong(note) is { } tooLong)
        {
            return tooLong;
        }

        AbsenceTypeId = absenceTypeId;
        FromDate = fromDate;
        ToDate = toDate;
        FromTime = fromTime;
        ToTime = toTime;
        Note = Trim(note);
        Version++;

        if (at is { } editedAt)
        {
            EditedAt = editedAt;
            EditedByAccountId = editedByAccountId;
        }

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
    /// <param name="cancelledBy">Bekor qilgan HR foydalanuvchisi.</param>
    /// <param name="at">Lahza.</param>
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

    /// <summary>
    /// Xodimning O'ZI ilovadan qaytarib oladi — faqat qaror KUTAYOTGAN so'rovni.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Faqat <see cref="AbsenceRequestStatus.Pending"/>.</b> HR tasdiqlagan
    /// yoki rad etgan so'rov HR ning qarori: uni xodim o'z ilovasidan bekor
    /// qila olsa, (a) rad etilgan so'rov «bekor qilingan» bo'lib, HR ning
    /// javobi jimgina yo'qolardi va (b) tasdiqlangan so'rov kunlarni
    /// «Sababli» dan qaytarib, tabelni HR bilmagan holda o'zgartirardi
    /// (UX §A.3: «cancel while pending»). Qaror yozuvi
    /// (<see cref="DecidedBy"/>, <see cref="DecisionNote"/>) shu sababli ilova
    /// yo'lida hech qachon ustidan yozilmaydi.
    /// </para>
    /// <para>
    /// Kim bekor qilgani <see cref="CancelledByAccountId"/> da (xodimning ilova
    /// hisobi); <see cref="DecidedBy"/> bo'sh qoladi — u HR foydalanuvchisining
    /// identifikatori va unga xodim hisobini yozish ikki fazoni aralashtirardi
    /// (ilgari bu yerga <c>Guid.Empty</c> tushardi).
    /// </para>
    /// </remarks>
    /// <param name="accountId">Xodimning ilova hisobi.</param>
    /// <param name="at">Lahza.</param>
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

    private static Error? NoteTooLong(string? note)
        => note?.Trim().Length > MaxNoteLength
            ? Error.Validation("davomat.izoh_uzun", $"Izoh {MaxNoteLength} belgidan oshmasligi kerak.")
            : null;

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
