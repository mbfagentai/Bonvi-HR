using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>Ochilgan ish o'rnining holati.</summary>
public enum VacancyStatus
{
    /// <summary>Ochiq — nomzod qabul qilinadi.</summary>
    Open = 1,

    /// <summary>
    /// To'xtatilgan — vaqtincha yopilgan, lekin bekor qilinmagan.
    /// </summary>
    /// <remarks>
    /// Nega <see cref="Closed"/> dan alohida: «byudjet kutilyapti» va
    /// «o'rin to'ldi» ikki xil fakt. Ular bir holatga yig'ilsa, HR nima
    /// uchun yopilganini faqat izohdan taxmin qilardi va «qайta ochish
    /// kerakmi?» degan savolga javob yo'qolardi.
    /// </remarks>
    Paused = 2,

    /// <summary>Yopilgan — ish o'rni yakunlangan (to'ldirilgan yoki bekor qilingan).</summary>
    Closed = 3,
}

/// <summary>
/// Vakansiya — ochilgan ish o'rni.
/// </summary>
/// <remarks>
/// <para>
/// <b>Voronkaning boshlanish nuqtasi.</b> Har bir nomzod aynan bitta
/// vakansiyaga tegishli: «bu odam nimaga kelgan» degan savolga javob
/// bo'lishi kerak. Vakansiyasiz nomzod — bu shunchaki telefon raqami
/// yozilgan qog'oz, u hech qanday hisobotga tushmaydi.
/// </para>
/// <para>
/// <b>Lavozim, bo'lim va firma — MAVJUD ma'lumotnomalardan.</b> Rekruting
/// o'zining «lavozim» ro'yxatini yaratmaydi: nomzod ishga olinganda
/// <c>HireEmployeeCommand</c> ga aynan shu identifikatorlar uzatiladi va
/// ular birinchi bosqichdagi ma'lumotnomalarga to'g'ri tushishi shart. Agar
/// bu yerda erkin matn bo'lganida, ishga qabul paytida uni qo'lda
/// ma'lumotnomaga moslashtirish kerak bo'lardi — Excel'dagi 228 ta
/// lavozim aynan shundan tug'ilgan.
/// </para>
/// <para>
/// <b>Nega erkin sarlavha (title) YO'Q.</b> Vakansiyaning nomi — bu
/// lavozim nomi. Alohida sarlavha maydoni bo'lganida «Сотувчи (шошилинч!)»
/// kabi yozuvlar paydo bo'lardi va ro'yxatda bir xil lavozim uch xil nom
/// bilan ko'rinardi. Shoshilinchlik va boshqa tafsilotlar
/// <see cref="Note"/> ga tushadi.
/// </para>
/// <para>
/// <b>Nega o'rin to'lganda O'ZI yopilmaydi.</b> <see cref="Openings"/> —
/// reja, fakt emas: HR bitta vakansiyaga ikki kishi olishi ham, bittasini
/// olib qolganini keyingi oyga qoldirishi ham mumkin. Tizim o'zi yopib
/// qo'ysa, HR buni sezmasdan yangi nomzodni kirita olmay qolardi. O'rin
/// nechtasi to'lgani ro'yxatda ko'rsatiladi (<c>HiredCount</c>), yopish
/// qarori esa odamniki.
/// </para>
/// </remarks>
public sealed class Vacancy : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>Bitta vakansiyada eng ko'p nechta o'rin bo'lishi mumkin.</summary>
    /// <remarks>
    /// Chegara kiritish xatosiga qarshi: «5» o'rniga «55» yozilishi oson va
    /// bunday yozuv hisobotda «55 ta odam kerak» bo'lib ko'rinardi. Bonvi'da
    /// eng katta ommaviy qabul — zavod sexiga 20-30 kishi, shu sababli 100
    /// yetarlicha keng, lekin ma'nosiz qiymatni to'sadi.
    /// </remarks>
    public const int MaxOpenings = 100;

    private Vacancy(
        Guid id,
        Guid positionId,
        Guid legalEntityId,
        Guid? departmentId,
        int openings,
        DateOnly openedAt)
        : base(id)
    {
        PositionId = positionId;
        LegalEntityId = legalEntityId;
        DepartmentId = departmentId;
        Openings = openings;
        OpenedAt = openedAt;
        Status = VacancyStatus.Open;
    }

    /// <summary>EF Core uchun.</summary>
    private Vacancy() { }

    /// <summary>Qaysi lavozimga — <c>positions</c> ma'lumotnomasidan.</summary>
    public Guid PositionId { get; private set; }

    /// <summary>Qaysi yuridik shaxsga — <c>legal_entities</c> ma'lumotnomasidan.</summary>
    public Guid LegalEntityId { get; private set; }

    /// <summary>Qaysi bo'limga — <c>departments</c> ma'lumotnomasidan. Bo'sh bo'lishi mumkin.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Nechta odam kerak.</summary>
    public int Openings { get; private set; }

    /// <summary>Vakansiya ochilgan sana.</summary>
    public DateOnly OpenedAt { get; private set; }

    /// <summary>Yopilgan sana. <see cref="VacancyStatus.Closed"/> bo'lmaguncha bo'sh.</summary>
    public DateOnly? ClosedAt { get; private set; }

    public VacancyStatus Status { get; private set; }

    /// <summary>Vakansiya uchun mas'ul xodim (rekruter).</summary>
    /// <remarks>
    /// Xodimga ishora, foydalanuvchi hisobiga emas — <c>Incident</c> va
    /// <c>EmployeeFlag</c> dagi bilan bir xil naqsh: mas'ul odam tizimga
    /// kirmasligi ham mumkin (xodimlarga login berilmaydi).
    /// </remarks>
    public Guid? ResponsibleEmployeeId { get; private set; }

    /// <summary>Erkin izoh: shoshilinchlik, talablar, yopish sababi.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Nomzod qabul qilinadigan holatdami.</summary>
    /// <remarks>
    /// To'xtatilgan vakansiyaga ham nomzod kiritilmaydi: to'xtatishning
    /// butun ma'nosi — «hozircha kerak emas». Aks holda to'xtatish faqat
    /// ro'yxatdagi rangni o'zgartirardi.
    /// </remarks>
    public bool AcceptsCandidates => Status == VacancyStatus.Open;

    public static Result<Vacancy> Open(
        Guid positionId,
        Guid legalEntityId,
        DateOnly openedAt,
        Guid? departmentId = null,
        int openings = 1,
        Guid? responsibleEmployeeId = null,
        string? note = null)
    {
        if (positionId == Guid.Empty)
        {
            return Error.Validation("vakansiya.lavozim_yoq", "Lavozim ko'rsatilishi shart.");
        }

        if (legalEntityId == Guid.Empty)
        {
            return Error.Validation("vakansiya.yuridik_shaxs_yoq", "Yuridik shaxs ko'rsatilishi shart.");
        }

        var checkedOpenings = ValidateOpenings(openings);
        if (checkedOpenings.IsFailure)
        {
            return checkedOpenings.Error;
        }

        return new Vacancy(NewId(), positionId, legalEntityId, departmentId, openings, openedAt)
        {
            ResponsibleEmployeeId = responsibleEmployeeId,
            Note = Trim(note),
        };
    }

    /// <summary>
    /// Vakansiya tafsilotlarini tuzatadi.
    /// </summary>
    /// <remarks>
    /// <b>Yuridik shaxs va lavozim BU YERDA ham o'zgaradi</b> —
    /// <c>Incident.Update</c> dagi qoidadan farqli. Sabab: baxtsiz hodisada
    /// xodim almashtirilsa, bu boshqa FAKT bo'lardi; vakansiya esa fakt
    /// emas, REJA — u to'ldirilishidan oldin qayta ko'rib chiqilishi odatiy
    /// hol («sotuvchi emas, katta sotuvchi kerak ekan»). Holat esa bu
    /// yerdan o'zgarmaydi: uning o'z amallari bor
    /// (<see cref="Pause"/> / <see cref="Resume"/> / <see cref="Close"/>).
    /// </remarks>
    public Result Update(
        Guid positionId,
        Guid legalEntityId,
        DateOnly openedAt,
        Guid? departmentId = null,
        int openings = 1,
        Guid? responsibleEmployeeId = null,
        string? note = null)
    {
        if (positionId == Guid.Empty)
        {
            return Error.Validation("vakansiya.lavozim_yoq", "Lavozim ko'rsatilishi shart.");
        }

        if (legalEntityId == Guid.Empty)
        {
            return Error.Validation("vakansiya.yuridik_shaxs_yoq", "Yuridik shaxs ko'rsatilishi shart.");
        }

        var checkedOpenings = ValidateOpenings(openings);
        if (checkedOpenings.IsFailure)
        {
            return checkedOpenings.Error;
        }

        PositionId = positionId;
        LegalEntityId = legalEntityId;
        DepartmentId = departmentId;
        Openings = openings;
        OpenedAt = openedAt;
        ResponsibleEmployeeId = responsibleEmployeeId;
        Note = Trim(note);

        return Result.Success();
    }

    /// <summary>Vaqtincha to'xtatadi.</summary>
    public Result Pause(string? reason = null)
    {
        if (Status == VacancyStatus.Closed)
        {
            return Error.Conflict("vakansiya.yopiq", "Yopilgan vakansiyani to'xtatib bo'lmaydi.");
        }

        Status = VacancyStatus.Paused;
        Note = Trim(reason) ?? Note;

        return Result.Success();
    }

    /// <summary>To'xtatilgan yoki yopilgan vakansiyani qayta ochadi.</summary>
    /// <remarks>
    /// Yopilgan vakansiyani qayta ochish ATAYLAB mumkin: o'rin to'ldi deb
    /// yopilgan vakansiyaga olingan odam bir haftada ketib qolishi Bonvi'da
    /// odatiy hol («91.8 Стажировкадан кетганлар» — 231 yozuv). Bunda yangi
    /// vakansiya ochish butun nomzodlar tarixini ikkiga bo'lib yuborardi.
    /// <see cref="ClosedAt"/> tozalanadi: vakansiya yana ochiq, ya'ni
    /// «qachon yopilgan» degan savolning javobi yo'q.
    /// </remarks>
    public Result Resume()
    {
        Status = VacancyStatus.Open;
        ClosedAt = null;

        return Result.Success();
    }

    /// <summary>Vakansiyani yopadi.</summary>
    public Result Close(DateOnly closedAt, string? reason = null)
    {
        if (Status == VacancyStatus.Closed)
        {
            return Error.Conflict("vakansiya.allaqachon_yopiq", "Bu vakansiya allaqachon yopilgan.");
        }

        if (closedAt < OpenedAt)
        {
            return Error.Validation(
                "vakansiya.yopish_sanasi",
                "Yopilish sanasi ochilish sanasidan oldin bo'lishi mumkin emas.");
        }

        Status = VacancyStatus.Closed;
        ClosedAt = closedAt;
        Note = Trim(reason) ?? Note;

        return Result.Success();
    }

    private static Result ValidateOpenings(int openings)
        => openings is < 1 or > MaxOpenings
            ? Error.Validation(
                "vakansiya.orin_soni",
                $"Kerakli odamlar soni 1 dan {MaxOpenings} gacha bo'lishi kerak.")
            : Result.Success();

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
