using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Domain.Safety;

/// <summary>Baxtsiz hodisada jabrlanish darajasi.</summary>
/// <remarks>
/// To'rt bosqich — mehnat muhofazasi amaliyotida qabul qilingan standart
/// tasnif. Excel'dagi 4 ta yozuvda faqat «Ўрта оғир» va «Ўлим» uchradi;
/// «Yengil»/«Og'ir» kelajakdagi yozuvlar uchun oldindan qo'shilgan.
/// </remarks>
public enum IncidentSeverity
{
    /// <summary>Yengil — tibbiy yordamsiz yoki qisqa davolanish bilan tugaydi.</summary>
    Minor = 1,

    /// <summary>O'rtacha og'ir (Excel: «Ўрта оғир»).</summary>
    Moderate = 2,

    /// <summary>Og'ir — uzoq davolanish yoki nogironlik.</summary>
    Severe = 3,

    /// <summary>O'lim (Excel: «Ўлим»).</summary>
    Fatal = 4,
}

/// <summary>
/// Ish paytida ro'y bergan baxtsiz hodisa (Excel: «Бахтсиз ходисалар руйхати»).
/// </summary>
/// <remarks>
/// <para>
/// <b>13 ustunli manba.</b> Excel varag'ining 3-qatoridagi sarlavha
/// aynan 13 ta ustundan iborat: №, Сана, Вақти, Ходимнинг ФИШ, Лавозими,
/// Ходиса жойи, Ходиса тавсифи, Сабаби, Чора тадбир, Жабрланиш даражаси,
/// Тиббий ёрдам кўрсатилганми, Комиссия хулосаси, Масъул шахс имзоси.
/// «№» saqlanmaydi (baza o'zi identifikator beradi); qolgan 12 ustun
/// quyidagi maydonlarga mos keladi (tibbiy yordam ustuni ikkiga —
/// bayroq va izohga — ajratilgan, shu bilan 13 ta domen maydoni hosil
/// bo'ladi).
/// </para>
/// <para>
/// <b>Nega xodimga, mehnat munosabatiga emas bog'langan.</b> Voqea
/// SHAXS bilan bog'liq fakt — <see cref="Incentive"/> bilan bir
/// xil mantiq: xodim boshqa bo'limga o'tsa yoki ishdan ketib qaytib kelsa
/// ham, tarix o'zgarmasligi kerak.
/// </para>
/// <para>
/// <b>Lavozim — voqea vaqtidagi HOLAT, joriy emas.</b> <see cref="PositionId"/>
/// ma'lumotnomaga ishora qiladi, lekin qiymati hodisa sodir bo'lgan
/// paytdagi lavozimni saqlaydi — xodim keyinroq boshqa lavozimga
/// o'tkazilsa ham, bu yozuv o'zgarmaydi (<see cref="EmploymentEvent"/>
/// dagi «From»/«To» qiymatlar bilan bir xil falsafa: tarix qayta
/// yozilmaydi).
/// </para>
/// <para>
/// <b>Komиssiya xulosasi kechikishi mumkin.</b> Voqea ro'y berganda HR
/// asosiy tafsilotlarni darhol yozadi, lekin komissiya tekshiruvi vaqt
/// oladi — shu sababli <see cref="CommissionConclusion"/> yaratishda
/// MAJBURIY emas, alohida <see cref="Conclude"/> metodi orqali keyinroq
/// qo'shiladi (xuddi <see cref="Probation.Resolve"/> kabi:
/// yaratish va yakunlash ikki xil vaqtda sodir bo'ladi).
/// </para>
/// <para>
/// <b>«Масъul шахс имзоси» — <see cref="ResponsibleEmployeeId"/>.</b>
/// Qog'ozdagi qo'lda imzo raqamli tizimda ma'noga ega emas; uning o'rniga
/// mas'ul xodimga ishora saqlanadi — xuddi <c>EmployeeFlag.ResponsibleEmployeeId</c>
/// va <c>Employment.ResponsibleEmployeeId</c> da qo'llanilgan naqsh.
/// </para>
/// </remarks>
public sealed class Incident : AggregateRoot, IAuditable, ISoftDeletable
{
    private Incident(
        Guid id,
        Guid employeeId,
        DateOnly incidentDate,
        string location,
        string description,
        IncidentSeverity severity)
        : base(id)
    {
        EmployeeId = employeeId;
        IncidentDate = incidentDate;
        Location = location;
        Description = description;
        Severity = severity;
    }

    /// <summary>EF Core uchun.</summary>
    private Incident() { }

    public Guid EmployeeId { get; private set; }

    /// <summary>Hodisa sanasi (Excel: «Сана»).</summary>
    public DateOnly IncidentDate { get; private set; }

    /// <summary>Smena/vaqt tavsifi — masalan «Тунги смена 02.00–03.00» (Excel: «Вақти»).</summary>
    public string? Shift { get; private set; }

    /// <summary>Hodisa vaqtidagi lavozim — SNAPSHOT (Excel: «Лавозими»).</summary>
    public Guid? PositionId { get; private set; }

    /// <summary>Hodisa joyi — erkin matn, bo'limdan aniqroq (Excel: «Ходиса жойи»).</summary>
    public string Location { get; private set; } = string.Empty;

    /// <summary>Hodisa tavsifi (Excel: «Ходиса тавсифи»).</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Sababi (Excel: «Сабаби»).</summary>
    public string? Cause { get; private set; }

    /// <summary>Ko'rilgan chora-tadbir (Excel: «Чора тадбир»).</summary>
    public string? CorrectiveAction { get; private set; }

    /// <summary>Jabrlanish darajasi (Excel: «Жабрланиш даражаси»).</summary>
    public IncidentSeverity Severity { get; private set; }

    /// <summary>Tibbiy yordam ko'rsatildimi (Excel: «Тиббий ёрдам кўрсатилганми»).</summary>
    public bool MedicalAidProvided { get; private set; }

    /// <summary>Tibbiy yordam tafsiloti — masalan «Тез тиббий ёрдамга олиб борилди».</summary>
    public string? MedicalAidNote { get; private set; }

    /// <summary>Komissiya xulosasi (Excel: «Комиссия хулосаси»). Keyinroq to'ldirilishi mumkin.</summary>
    public string? CommissionConclusion { get; private set; }

    /// <summary>Komissiya xulosasi chiqarilgan sana.</summary>
    public DateOnly? ConcludedAt { get; private set; }

    /// <summary>Mas'ul xodim — qog'ozdagi imzo o'rnini bosadi (Excel: «Масъул шахс имзоси»).</summary>
    public Guid? ResponsibleEmployeeId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<Incident> Record(
        Guid employeeId,
        DateOnly incidentDate,
        string? location,
        string? description,
        IncidentSeverity severity,
        string? shift = null,
        Guid? positionId = null,
        string? cause = null,
        string? correctiveAction = null,
        bool medicalAidProvided = false,
        string? medicalAidNote = null,
        Guid? responsibleEmployeeId = null)
    {
        if (employeeId == Guid.Empty)
        {
            return Error.Validation("baxtsiz_hodisa.xodim_yoq", "Xodim ko'rsatilishi shart.");
        }

        var trimmedLocation = Trim(location);
        if (trimmedLocation is null)
        {
            return Error.Validation("baxtsiz_hodisa.joy_bosh", "Hodisa joyi kiritilishi shart.");
        }

        var trimmedDescription = Trim(description);
        if (trimmedDescription is null)
        {
            return Error.Validation("baxtsiz_hodisa.tavsif_bosh", "Hodisa tavsifi kiritilishi shart.");
        }

        return new Incident(NewId(), employeeId, incidentDate, trimmedLocation, trimmedDescription, severity)
        {
            Shift = Trim(shift),
            PositionId = positionId,
            Cause = Trim(cause),
            CorrectiveAction = Trim(correctiveAction),
            MedicalAidProvided = medicalAidProvided,
            MedicalAidNote = Trim(medicalAidNote),
            ResponsibleEmployeeId = responsibleEmployeeId,
        };
    }

    /// <summary>
    /// Kiritish xatosini tuzatadi — hodisa tafsilotlari.
    /// </summary>
    /// <remarks>
    /// <see cref="EmployeeId"/> O'ZGARMAYDI — <c>Incentive.Update</c> dagi
    /// bilan bir xil qoida: noto'g'ri xodimga yozilgan bo'lsa, bu tuzatish
    /// emas, boshqa fakt. Komissiya xulosasi ham bu yerdan O'ZGARMAYDI —
    /// u faqat <see cref="Conclude"/> orqali qo'yiladi/tuzatiladi, chunki
    /// xulosa alohida vakolatli qadam.
    /// </remarks>
    public Result Update(
        DateOnly incidentDate,
        string? location,
        string? description,
        IncidentSeverity severity,
        string? shift = null,
        Guid? positionId = null,
        string? cause = null,
        string? correctiveAction = null,
        bool medicalAidProvided = false,
        string? medicalAidNote = null,
        Guid? responsibleEmployeeId = null)
    {
        var trimmedLocation = Trim(location);
        if (trimmedLocation is null)
        {
            return Error.Validation("baxtsiz_hodisa.joy_bosh", "Hodisa joyi kiritilishi shart.");
        }

        var trimmedDescription = Trim(description);
        if (trimmedDescription is null)
        {
            return Error.Validation("baxtsiz_hodisa.tavsif_bosh", "Hodisa tavsifi kiritilishi shart.");
        }

        IncidentDate = incidentDate;
        Location = trimmedLocation;
        Description = trimmedDescription;
        Severity = severity;
        Shift = Trim(shift);
        PositionId = positionId;
        Cause = Trim(cause);
        CorrectiveAction = Trim(correctiveAction);
        MedicalAidProvided = medicalAidProvided;
        MedicalAidNote = Trim(medicalAidNote);
        ResponsibleEmployeeId = responsibleEmployeeId;

        return Result.Success();
    }

    /// <summary>Komissiya xulosasini yozadi (yoki tuzatadi).</summary>
    public Result Conclude(string? conclusion, DateOnly at)
    {
        var trimmed = Trim(conclusion);
        if (trimmed is null)
        {
            return Error.Validation("baxtsiz_hodisa.xulosa_bosh", "Komissiya xulosasi kiritilishi shart.");
        }

        CommissionConclusion = trimmed;
        ConcludedAt = at;

        return Result.Success();
    }

    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
