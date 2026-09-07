using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Employees;

/// <summary>Mehnat munosabatining holati.</summary>
public enum EmploymentStatus
{
    /// <summary>Sinov muddatida.</summary>
    Probation = 1,

    /// <summary>Doimiy ishlayapti.</summary>
    Active = 2,

    /// <summary>Ishdan ketgan.</summary>
    Terminated = 3,
}

/// <summary>
/// Mehnat munosabati — <b>xodim va yuridik shaxs orasidagi bog'lanish</b>.
/// </summary>
/// <remarks>
/// <para>
/// Bitta xodimda bir nechta mehnat munosabati bo'lishi mumkin:
/// </para>
/// <list type="bullet">
///   <item>bir vaqtda ikki firmada (o'rindoshlik, 0.5 stavka) —
///         Excel'da «VIKOR + Вело унион 0,5 ставка ўриндош»;</item>
///   <item>ketib, keyin qaytib kelgan — har safar yangisi ochiladi,
///         eskisi tarixda qoladi.</item>
/// </list>
/// <para>
/// Lavozim, bo'lim va oklad shu yerda saqlanadi, chunki ular aynan shu
/// munosabatga tegishli: bir xodim bir firmada omborchi, boshqasida
/// haydovchi bo'lishi mumkin.
/// </para>
/// <para>
/// Har bir o'zgarish <see cref="EmploymentEvent"/> sifatida yoziladi.
/// Joriy holat — hodisalar oqimining natijasi, lekin tez o'qish uchun
/// bu yerda ham saqlanadi.
/// </para>
/// </remarks>
public sealed class Employment : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<EmploymentEvent> _events = [];

    private Employment(
        Guid id,
        Guid employeeId,
        Guid legalEntityId,
        Guid? departmentId,
        Guid? positionId,
        DateOnly hireDate)
        : base(id)
    {
        EmployeeId = employeeId;
        LegalEntityId = legalEntityId;
        DepartmentId = departmentId;
        PositionId = positionId;
        HireDate = hireDate;
        Status = EmploymentStatus.Probation;
        Rate = 1.0m;
        IsPrimary = true;
    }

    /// <summary>EF Core uchun.</summary>
    private Employment() { }

    public Guid EmployeeId { get; private set; }

    public Guid LegalEntityId { get; private set; }

    public Guid? DepartmentId { get; private set; }

    public Guid? PositionId { get; private set; }

    public DateOnly HireDate { get; private set; }

    /// <summary>Ishga qabul buyrug'i raqami.</summary>
    public string? HireOrderNo { get; private set; }

    public DateOnly? HireOrderDate { get; private set; }

    public Money? Salary { get; private set; }

    /// <summary>Stavka: 1.0 — to'liq, 0.5 — yarim (o'rindoshlik).</summary>
    public decimal Rate { get; private set; }

    /// <summary>
    /// Asosiy ish joyimi. Bir xodimda faqat bittasi asosiy bo'lishi kerak —
    /// bu qoida use-case qatlamida tekshiriladi (buning uchun boshqa
    /// munosabatlarni bilish kerak).
    /// </summary>
    public bool IsPrimary { get; private set; }

    public EmploymentStatus Status { get; private set; }

    public DateOnly? TerminationDate { get; private set; }

    public Guid? TerminationReasonId { get; private set; }

    public string? TerminationOrderNo { get; private set; }

    /// <summary>Ishdan bo'shatish haqidagi qo'shimcha izoh.</summary>
    public string? TerminationNote { get; private set; }

    /// <summary>Biriktirilgan xodim (mentor / mas'ul shaxs).</summary>
    public Guid? ResponsibleEmployeeId { get; private set; }

    /// <summary>Ma'lumot tekshirishga muhtoj (ETL dan kelgan).</summary>
    public bool NeedsReview { get; private set; }

    public string? ReviewNote { get; private set; }

    /// <summary>Mehnat yo'lidagi barcha hodisalar — tartiblangan.</summary>
    public IReadOnlyList<EmploymentEvent> Events => _events.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Ishlagan kunlar soni.</summary>
    /// <param name="today">Hisoblash sanasi (hali ishlayotganlar uchun).</param>
    public int WorkedDays(DateOnly today)
        => (TerminationDate ?? today).DayNumber - HireDate.DayNumber;

    /// <summary>
    /// Ishga qabul qiladi va birinchi hodisani yozadi.
    /// </summary>
    /// <remarks>
    /// <paramref name="candidateId"/> — 2-bosqich (rekruting) bilan yagona
    /// bog'lanish nuqtasi: ishga olingan nomzod shu yerdan ishga
    /// qabul HODISASIGA yoziladi. Ixtiyoriy va oxirida turadi — qo'lda
    /// kiritilgan qabul (HR formasi) va Excel'dan ko'chirish uni bermaydi,
    /// ya'ni mavjud chaqiruvlar o'zgarmaydi.
    /// </remarks>
    public static Result<Employment> Hire(
        Guid employeeId,
        Guid legalEntityId,
        DateOnly hireDate,
        Guid? departmentId = null,
        Guid? positionId = null,
        string? orderNo = null,
        DateOnly? orderDate = null,
        Guid? candidateId = null)
    {
        if (employeeId == Guid.Empty)
        {
            return Error.Validation("mehnat.xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        if (legalEntityId == Guid.Empty)
        {
            return Error.Validation("mehnat.yuridik_shaxs_yoq", "Yuridik shaxs ko'rsatilishi shart.");
        }

        var employment = new Employment(NewId(), employeeId, legalEntityId, departmentId, positionId, hireDate)
        {
            HireOrderNo = Normalize(orderNo),
            HireOrderDate = orderDate,
        };

        employment._events.Add(EmploymentEvent.Create(
            employment.Id,
            EmploymentEventType.Hire,
            hireDate,
            orderNo: Normalize(orderNo),
            orderDate: orderDate,
            toDepartmentId: departmentId,
            toPositionId: positionId,
            candidateId: candidateId));

        return employment;
    }

    public Result SetSalary(Money? salary, DateOnly effectiveDate, string? orderNo = null, DateOnly? orderDate = null)
    {
        if (Status == EmploymentStatus.Terminated)
        {
            return Error.Conflict("mehnat.tugagan", "Ishdan ketgan xodimning okladini o'zgartirib bo'lmaydi.");
        }

        var oldSalary = Salary;
        Salary = salary;

        _events.Add(EmploymentEvent.Create(
            Id,
            EmploymentEventType.SalaryChange,
            effectiveDate,
            orderNo: Normalize(orderNo),
            orderDate: orderDate,
            fromSalary: oldSalary,
            toSalary: salary));

        return Result.Success();
    }

    public Result SetRate(decimal rate)
    {
        if (rate is <= 0 or > 2)
        {
            return Error.Validation("mehnat.stavka", "Stavka 0 dan katta va 2 dan oshmasligi kerak.");
        }

        Rate = rate;
        return Result.Success();
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    public void AssignResponsible(Guid? employeeId) => ResponsibleEmployeeId = employeeId;

    /// <summary>Sinov muddati muvaffaqiyatli tugadi — doimiy holatga o'tadi.</summary>
    public Result CompleteProbation(DateOnly at, string? note = null)
    {
        if (Status != EmploymentStatus.Probation)
        {
            return Error.Conflict("mehnat.sinovda_emas", "Xodim sinov muddatida emas.");
        }

        Status = EmploymentStatus.Active;

        _events.Add(EmploymentEvent.Create(
            Id,
            EmploymentEventType.ProbationResult,
            at,
            note: Normalize(note) ?? "Sinov muddati muvaffaqiyatli tugadi"));

        return Result.Success();
    }

    /// <summary>
    /// Lavozim yoki bo'limni o'zgartiradi.
    /// </summary>
    /// <remarks>
    /// Excel'da bu «91.3 Лавозими ўзгарган ходимлар» varag'i edi va u yerda
    /// eski hamda yangi qiymat yonma-yon yozilardi. Bu yerda ular hodisa
    /// ichida saqlanadi — ro'yxat esa hodisalardan hosil qilinadi.
    /// </remarks>
    public Result Transfer(
        DateOnly effectiveDate,
        Guid? newDepartmentId,
        Guid? newPositionId,
        Money? newSalary = null,
        string? orderNo = null,
        DateOnly? orderDate = null,
        string? note = null)
    {
        if (Status == EmploymentStatus.Terminated)
        {
            return Error.Conflict("mehnat.tugagan", "Ishdan ketgan xodimni ko'chirib bo'lmaydi.");
        }

        var fromDepartment = DepartmentId;
        var fromPosition = PositionId;
        var fromSalary = Salary;

        DepartmentId = newDepartmentId ?? DepartmentId;
        PositionId = newPositionId ?? PositionId;

        if (newSalary is not null)
        {
            Salary = newSalary;
        }

        // Lavozim o'zgargan bo'lsa — «Promotion», faqat bo'lim bo'lsa — «Transfer».
        // Ikkalasi ham hisobotda alohida ko'rinadi.
        var type = newPositionId is not null && newPositionId != fromPosition
            ? EmploymentEventType.Promotion
            : EmploymentEventType.Transfer;

        _events.Add(EmploymentEvent.Create(
            Id,
            type,
            effectiveDate,
            orderNo: Normalize(orderNo),
            orderDate: orderDate,
            fromDepartmentId: fromDepartment,
            toDepartmentId: DepartmentId,
            fromPositionId: fromPosition,
            toPositionId: PositionId,
            fromSalary: fromSalary,
            toSalary: Salary,
            note: Normalize(note)));

        return Result.Success();
    }

    /// <summary>Qo'shimcha vazifa berish (Excel: «91.4 Қушимча вазифалар»).</summary>
    public Result AddAdditionalDuty(
        DateOnly effectiveDate,
        string duty,
        Money? extraSalary = null,
        string? orderNo = null,
        DateOnly? orderDate = null)
    {
        var text = Normalize(duty);
        if (text is null)
        {
            return Error.Validation("mehnat.vazifa_bosh", "Qo'shimcha vazifa matni kiritilishi shart.");
        }

        if (Status == EmploymentStatus.Terminated)
        {
            return Error.Conflict("mehnat.tugagan", "Ishdan ketgan xodimga vazifa berib bo'lmaydi.");
        }

        _events.Add(EmploymentEvent.Create(
            Id,
            EmploymentEventType.AdditionalDuty,
            effectiveDate,
            orderNo: Normalize(orderNo),
            orderDate: orderDate,
            toSalary: extraSalary,
            note: text));

        return Result.Success();
    }

    /// <summary>Ishdan bo'shatish.</summary>
    public Result Terminate(
        DateOnly terminationDate,
        Guid? reasonId,
        string? orderNo = null,
        DateOnly? orderDate = null,
        string? note = null)
    {
        if (Status == EmploymentStatus.Terminated)
        {
            return Error.Conflict("mehnat.allaqachon_tugagan", "Bu mehnat munosabati allaqachon yakunlangan.");
        }

        if (terminationDate < HireDate)
        {
            return Error.Validation(
                "mehnat.ketish_sanasi",
                "Ishdan ketish sanasi ishga kirish sanasidan oldin bo'lishi mumkin emas.");
        }

        Status = EmploymentStatus.Terminated;
        TerminationDate = terminationDate;
        TerminationReasonId = reasonId;
        TerminationOrderNo = Normalize(orderNo);
        TerminationNote = Normalize(note);

        _events.Add(EmploymentEvent.Create(
            Id,
            EmploymentEventType.Termination,
            terminationDate,
            orderNo: Normalize(orderNo),
            orderDate: orderDate,
            note: Normalize(note)));

        return Result.Success();
    }

    /// <summary>Ma'lumot tekshirishga muhtoj deb belgilaydi (ETL uchun).</summary>
    public void FlagForReview(string reason)
    {
        NeedsReview = true;
        ReviewNote = string.IsNullOrWhiteSpace(ReviewNote)
            ? reason.Trim()
            : $"{ReviewNote}; {reason.Trim()}";
    }

    public void MarkReviewed()
    {
        NeedsReview = false;
        ReviewNote = null;
    }

    /// <summary>
    /// ETL uchun: ishdan bo'shatish holatini Excel'dagi tarixdan tiklaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Terminate"/> dan farqi bitta: hodisa <c>IsImported = true</c>
    /// bilan yoziladi. Bu farq muhim — ko'chirilgan yozuvda buyruq raqami
    /// bo'lmasligi yoki sana taxminiy bo'lishi mumkin, va uni tizimda
    /// kiritilgan aniq buyruqdan ajratib turish kerak.
    /// </para>
    /// <para>
    /// Holat tekshiruvlari <see cref="Terminate"/> dagidek qoladi: allaqachon
    /// yakunlangan munosabat ikkinchi marta yopilmaydi va ketish sanasi ishga
    /// kirish sanasidan oldin bo'la olmaydi. Excel'da ikkalasi ham uchraydi,
    /// va aynan shular hisobotga tushishi kerak.
    /// </para>
    /// </remarks>
    public Result ImportTermination(
        DateOnly terminationDate,
        Guid? reasonId,
        string? orderNo = null,
        DateOnly? orderDate = null,
        string? note = null)
    {
        if (Status == EmploymentStatus.Terminated)
        {
            return Error.Conflict("mehnat.allaqachon_tugagan", "Bu mehnat munosabati allaqachon yakunlangan.");
        }

        if (terminationDate < HireDate)
        {
            return Error.Validation(
                "mehnat.ketish_sanasi",
                "Ishdan ketish sanasi ishga kirish sanasidan oldin bo'lishi mumkin emas.");
        }

        Status = EmploymentStatus.Terminated;
        TerminationDate = terminationDate;
        TerminationReasonId = reasonId;
        TerminationOrderNo = Normalize(orderNo);
        TerminationNote = Normalize(note);

        _events.Add(EmploymentEvent.Import(
            Id,
            EmploymentEventType.Termination,
            terminationDate,
            orderNo: Normalize(orderNo),
            orderDate: orderDate,
            note: Normalize(note)));

        return Result.Success();
    }

    /// <summary>
    /// ETL uchun: tayyor hodisani to'g'ridan-to'g'ri qo'shadi.
    /// </summary>
    /// <remarks>
    /// Excel'dagi tarix bir necha varaqqa tarqalgan va tartibi buzilgan.
    /// Uni domen metodlari orqali qayta o'ynatib bo'lmaydi (masalan
    /// ishdan ketgandan keyin lavozim o'zgarishi yozilgan bo'lishi mumkin).
    /// Shu sababli migratsiya hodisalarni holatga tegmasdan qo'yadi.
    /// Oddiy ishda bu metod ISHLATILMAYDI.
    /// </remarks>
    public void ImportEvent(EmploymentEvent historicalEvent)
    {
        ArgumentNullException.ThrowIfNull(historicalEvent);
        _events.Add(historicalEvent);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
