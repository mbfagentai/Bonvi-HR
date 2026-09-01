using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Employees;

/// <summary>Mehnat yo'lidagi hodisa turi.</summary>
public enum EmploymentEventType
{
    /// <summary>Ishga qabul (Excel: «91.2 Янги ишга келганлар»).</summary>
    Hire = 1,

    /// <summary>Bo'lim o'zgarishi (Excel: «91.3»).</summary>
    Transfer = 2,

    /// <summary>Lavozim o'zgarishi (Excel: «91.3»).</summary>
    Promotion = 3,

    /// <summary>Qo'shimcha vazifa (Excel: «91.4 Қушимча вазифалар»).</summary>
    AdditionalDuty = 4,

    /// <summary>Oklad o'zgarishi.</summary>
    SalaryChange = 5,

    /// <summary>Sinov muddati natijasi (Excel: «91.5», «91.8»).</summary>
    ProbationResult = 6,

    /// <summary>Ishdan bo'shatish (Excel: «91.6 Ишдан кетган ходимлар»).</summary>
    Termination = 7,

    /// <summary>Qayta ishga qabul.</summary>
    Rehire = 8,
}

/// <summary>
/// Mehnat yo'lidagi hodisa — <b>o'zgartirib bo'lmaydigan fakt</b>.
/// </summary>
/// <remarks>
/// <para>
/// Bu tizimning yuragi. Excel'da bitta o'zgarish 8 ta varaqqa qo'lda
/// ko'chirilardi: umumiy ro'yxat, yangi kelganlar, lavozimi o'zgarganlar,
/// arxiv va boshqalar. Bu yerda esa faqat <b>bitta</b> hodisa yoziladi,
/// qolgan barcha ro'yxatlar undan hosil qilinadi.
/// </para>
/// <para>
/// Yozuv o'zgartirilmaydi: o'zgartiruvchi metodlari yo'q, barcha qiymat
/// konstruktorda beriladi. Xato bo'lsa — tuzatuvchi yangi hodisa yoziladi,
/// eskisi tarixda qoladi. HR tizimida bu majburiy: buyruqlar bekor
/// qilinmaydi, ular ustiga yangi buyruq chiqariladi.
/// </para>
/// <para>
/// Har bir hodisada <c>OrderNo</c> bor — HR aytdi: «buyruq nomeri
/// hujjati uchun kerak», va qidiruv aynan shu bo'yicha ketadi.
/// </para>
/// </remarks>
public sealed class EmploymentEvent : Entity
{
    private EmploymentEvent(
        Guid id,
        Guid employmentId,
        EmploymentEventType type,
        DateOnly effectiveDate)
        : base(id)
    {
        EmploymentId = employmentId;
        Type = type;
        EffectiveDate = effectiveDate;
    }

    /// <summary>EF Core uchun.</summary>
    private EmploymentEvent() { }

    public Guid EmploymentId { get; private set; }

    public EmploymentEventType Type { get; private set; }

    /// <summary>Hodisa kuchga kirgan sana (buyruq sanasi emas).</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>Buyruq raqami — qidiruv shu bo'yicha ketadi.</summary>
    public string? OrderNo { get; private set; }

    public DateOnly? OrderDate { get; private set; }

    // ── Oldingi holat ──
    public Guid? FromDepartmentId { get; private set; }
    public Guid? FromPositionId { get; private set; }
    public Money? FromSalary { get; private set; }

    // ── Yangi holat ──
    public Guid? ToDepartmentId { get; private set; }
    public Guid? ToPositionId { get; private set; }
    public Money? ToSalary { get; private set; }

    /// <summary>Izoh. Qo'shimcha vazifada — vazifaning matni.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Ishga qabul rekrutingdan kelgan bo'lsa — o'sha nomzod (2-bosqich).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>«Bu xodim qayerdan keldi?» degan savolning javobi shu yerda.</b>
    /// Maydon <c>PLAN.md</c> ning 8-bo'limida oldindan rejalashtirilgan va
    /// aynan shu joyda turgani ATAYLAB: ishga qabul — o'zgarmas FAKT, uning
    /// kelib chiqishi ham shu faktning bir qismi. Nomzodda saqlansa
    /// («men falon xodimga aylandim»), ikkita nozik holat buzilardi:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     odam ketib qaytib kelsa, ikkinchi qabul ham AYNI nomzod yozuviga
    ///     ishora qilib qolardi va qaysi biri qaysi qabul ekani yo'qolardi;
    ///   </item>
    ///   <item>
    ///     nomzod yozuvi yumshoq o'chirilsa, xodimning kelib chiqishi
    ///     ham ko'rinmay qolardi — holbuki fakt o'zgarmagan.
    ///   </item>
    /// </list>
    /// <para>
    /// Ikkinchi (va amaliy) foydasi: hodisa bilan BIR TRANZAKSIYADA
    /// yoziladi, ya'ni «xodim yaratildi, lekin bog'lanish yozilmadi»
    /// holati umuman bo'lmaydi. Taklifni qabul qilish oqimi shu tufayli
    /// takroriy urinishga chidamli — batafsil
    /// <c>AcceptJobOfferCommandHandler</c> izohida.
    /// </para>
    /// <para>
    /// Domen qatlami rekrutingni BILMAYDI: bu shunchaki identifikator,
    /// navigatsiya xossasi emas — agregatlar bir-birini identifikator
    /// orqali biladi degan umumiy qoidaga to'liq mos.
    /// </para>
    /// </remarks>
    public Guid? CandidateId { get; private set; }

    /// <summary>Kim kiritdi. ETL ko'chirgan bo'lsa — <c>null</c>.</summary>
    public Guid? RecordedBy { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>Excel'dan ko'chirilganmi — tarixiy yozuvni ajratish uchun.</summary>
    public bool IsImported { get; private set; }

    internal static EmploymentEvent Create(
        Guid employmentId,
        EmploymentEventType type,
        DateOnly effectiveDate,
        string? orderNo = null,
        DateOnly? orderDate = null,
        Guid? fromDepartmentId = null,
        Guid? toDepartmentId = null,
        Guid? fromPositionId = null,
        Guid? toPositionId = null,
        Money? fromSalary = null,
        Money? toSalary = null,
        string? note = null,
        Guid? recordedBy = null,
        Guid? candidateId = null)
        => new(NewId(), employmentId, type, effectiveDate)
        {
            CandidateId = candidateId,
            OrderNo = orderNo,
            OrderDate = orderDate,
            FromDepartmentId = fromDepartmentId,
            ToDepartmentId = toDepartmentId,
            FromPositionId = fromPositionId,
            ToPositionId = toPositionId,
            FromSalary = fromSalary,
            ToSalary = toSalary,
            Note = note,
            RecordedBy = recordedBy,
            RecordedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// Excel'dan ko'chirilgan tarixiy hodisa.
    /// </summary>
    /// <remarks>
    /// <c>IsImported = true</c> qo'yiladi. Bu muhim: ko'chirilgan yozuvda
    /// buyruq raqami yo'q yoki sana taxminiy bo'lishi mumkin, va uni
    /// tizimda kiritilgan aniq yozuvdan ajratib turish kerak.
    /// </remarks>
    public static EmploymentEvent Import(
        Guid employmentId,
        EmploymentEventType type,
        DateOnly effectiveDate,
        string? orderNo = null,
        DateOnly? orderDate = null,
        Guid? fromDepartmentId = null,
        Guid? toDepartmentId = null,
        Guid? fromPositionId = null,
        Guid? toPositionId = null,
        Money? fromSalary = null,
        Money? toSalary = null,
        string? note = null)
    {
        var created = Create(
            employmentId, type, effectiveDate, orderNo, orderDate,
            fromDepartmentId, toDepartmentId, fromPositionId, toPositionId,
            fromSalary, toSalary, note);

        created.IsImported = true;
        return created;
    }

    /// <summary>
    /// Hodisani nomzod yozuviga bog'laydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega keyin bog'lash kerak.</b> Odatiy oqimda bog'lanish ishga
    /// qabul paytida yoziladi (<c>candidateId</c> konstruktorga beriladi)
    /// — nomzod avval keladi, xodim keyin. Excel'dan ko'chirishda tartib
    /// TESKARI: xodimlar allaqachon ko'chirilgan, kengash xulosasi esa
    /// keyingi qadamda o'qiladi. Boshqacha aytganda, bu metod tarixni
    /// tiklash uchun, kundalik ish uchun emas.
    /// </para>
    /// <para>
    /// <b>Ustidan yozilmaydi.</b> Bog'lanish allaqachon bo'lsa amal
    /// jimgina rad etiladi: tizimda kiritilgan haqiqiy bog'lanish
    /// ko'chirishdagi ism bo'yicha topilgan taxminiy mosligidan
    /// ishonchliroq va uni almashtirish ma'lumot yo'qotish bo'lardi.
    /// </para>
    /// </remarks>
    /// <returns>Bog'lanish shu chaqiruvda yozildimi.</returns>
    public bool LinkCandidate(Guid candidateId)
    {
        if (candidateId == Guid.Empty || CandidateId is not null)
        {
            return false;
        }

        CandidateId = candidateId;
        return true;
    }
}
