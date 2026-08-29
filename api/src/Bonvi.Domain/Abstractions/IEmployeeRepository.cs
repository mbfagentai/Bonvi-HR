using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Abstractions;

/// <summary>Xodimlar uchun saqlash porti.</summary>
public interface IEmployeeRepository : IRepository<Employee>
{
    /// <summary>JSHSHIR bo'yicha topadi — asosiy identifikator.</summary>
    Task<Employee?> FindByPinflAsync(Pinfl pinfl, CancellationToken cancellationToken = default);

    /// <summary>Pasport bo'yicha topadi (JSHSHIR bo'lmaganda zaxira yo'l).</summary>
    Task<Employee?> FindByPassportAsync(PassportSeries passport, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ism va tug'ilgan sana bo'yicha topadi — oxirgi zaxira yo'l.
    /// </summary>
    /// <remarks>
    /// Excel'da 3 ta xodimda JSHSHIR ham, pasport ham yo'q. Dublikat
    /// yaratmaslik uchun ular shu yo'l bilan taqqoslanadi.
    /// </remarks>
    Task<IReadOnlyList<Employee>> FindByNameAndBirthDateAsync(
        FullName fullName,
        DateOnly? birthDate,
        CancellationToken cancellationToken = default);

    Task<bool> PinflExistsAsync(Pinfl pinfl, Guid? exceptId = null, CancellationToken cancellationToken = default);
}

/// <summary>Mehnat munosabatlari uchun saqlash porti.</summary>
public interface IEmploymentRepository : IRepository<Employment>
{
    /// <summary>Hodisalari bilan birga yuklaydi.</summary>
    Task<Employment?> GetWithEventsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Xodimning barcha mehnat munosabatlari (tarix bilan).</summary>
    Task<IReadOnlyList<Employment>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimning hozir faol munosabatlari.</summary>
    Task<IReadOnlyList<Employment>> GetActiveByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nomzoddan tug'ilgan mehnat munosabati (2-bosqich bog'lanishi).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ishga qabul HODISASIDAGI <c>CandidateId</c> bo'yicha qidiriladi
    /// (sababi <see cref="EmploymentEvent.CandidateId"/> izohida). Ikki
    /// joyda kerak: nomzod kartasida «u qaysi xodimga aylandi» havolasi va
    /// taklifni qabul qilish oqimining TAKRORGA CHIDAMLILIGI — agar birinchi
    /// urinishda xodim yaratilib, nomzod holati yozilmay qolgan bo'lsa,
    /// ikkinchi urinish shu metod orqali «allaqachon ishga olingan» ekanini
    /// ko'radi va ikkinchi xodim kartasini YARATMAYDI.
    /// </para>
    /// <para>
    /// <c>null</c> — bu nomzoddan hech kim ishga olinmagan.
    /// </para>
    /// </remarks>
    Task<Employment?> FindByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xodimning barcha faol stavkalari yig'indisi.
    /// </summary>
    /// <remarks>
    /// Biznes qoidasi: yig'indi 1.5 dan oshsa ogohlantiriladi.
    /// Bir odam bir vaqtda ikki firmada to'liq stavkada ishlay olmaydi.
    /// </remarks>
    Task<decimal> GetTotalActiveRateAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

/// <summary>Sinov muddatlari uchun saqlash porti.</summary>
public interface IProbationRepository : IRepository<Probation>
{
    Task<Probation?> GetByEmploymentAsync(Guid employmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Muddati tugashiga oz qolganlar — kunlik eslatma vazifasi uchun.
    /// </summary>
    Task<IReadOnlyList<Probation>> GetEndingSoonAsync(
        DateOnly today,
        int withinDays,
        CancellationToken cancellationToken = default);
}

/// <summary>Xodim belgilari uchun saqlash porti.</summary>
public interface IEmployeeFlagRepository : IRepository<EmployeeFlag>
{
    /// <summary>
    /// Xodimning BARCHA belgilari — tarix bilan birga.
    /// </summary>
    /// <remarks>
    /// «Faqat kuchdagilar» filtri BU YERDA ATAYLAB yo'q. U bor edi, lekin
    /// hech kim ishlatmasdi va shu tufayli sezilmay ikkita xato yig'ib
    /// oldi: kuchdagilikni <c>&gt;=</c> bilan hisoblardi (qolgan hamma
    /// joyda <c>&gt;</c> — sabab <c>EmployeeFlag.IsInForceAfter</c> da) va
    /// sanani <c>IClock</c> dan emas, <c>DateTime.UtcNow</c> dan olardi
    /// (ya'ni testlarda qotirilgan «bugun» ga bo'ysunmasdi). Kartaga
    /// baribir butun tarix kerak, filtr esa mijozda —
    /// <c>flagInForce</c>.
    /// </remarks>
    Task<IReadOnlyList<EmployeeFlag>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xodimda shu turdagi faol belgi bormi.
    /// </summary>
    /// <remarks>
    /// Ishdan bo'shatish va qayta ishga olishda tekshiriladi:
    /// qora ro'yxat yoki nazorat bo'lsa — ogohlantirish chiqadi.
    /// </remarks>
    Task<bool> HasActiveFlagAsync(
        Guid employeeId,
        EmployeeFlagType type,
        DateOnly on,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xodimning shu sanadan KEYIN ham kuchda qoladigan belgilari —
    /// <b>o'zgartirish uchun</b> (kuzatiladigan holda).
    /// </summary>
    /// <remarks>
    /// <para>
    /// «Bir vaqtda atigi bitta belgi» qoidasi shu ro'yxatga tayanadi:
    /// yangi belgi qo'yilganda bu yerdan qaytgan yozuvlar yopiladi
    /// (<c>EmployeeFlag.Supersede</c>).
    /// </para>
    /// <para>
    /// <b>Nega <see cref="GetByEmployeeAsync"/> yaramaydi.</b> U ko'rsatish
    /// uchun va yozuvlarni <c>AsNoTracking</c> bilan o'qiydi — undan olingan
    /// obyektni o'zgartirish bazaga umuman yetib bormaydi. Shart ham
    /// boshqacha: bu yerda «bugun kuchda» emas, «shu kundan KEYIN ham
    /// kuchda» tekshiriladi (sababi <c>EmployeeFlag.IsInForceAfter</c>
    /// izohida).
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<EmployeeFlag>> GetInForceAsync(
        Guid employeeId,
        DateOnly on,
        CancellationToken cancellationToken = default);
}

/// <summary>Qobiliyatlar profili uchun saqlash porti.</summary>
/// <remarks>
/// Bitta xodimda bitta profil — shu sababli <c>IReferenceRepository&lt;T&gt;</c>
/// kabi ro'yxat emas, faqat xodim bo'yicha qidiruv kerak.
/// </remarks>
public interface IEmployeeCompetencyRepository : IRepository<EmployeeCompetency>
{
    Task<EmployeeCompetency?> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

/// <summary>Rag'bat va jarima yozuvlari uchun saqlash porti.</summary>
public interface IIncentiveRepository : IRepository<Incentive>
{
    /// <summary>Xodimning barcha rag'bat/jarima yozuvlari, sana bo'yicha kamayish tartibida.</summary>
    Task<IReadOnlyList<Incentive>> GetByEmployeeAsync(
        Guid employeeId,
        IncentiveKind? kind = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Xodimga biriktirilgan fayllar uchun saqlash porti.</summary>
public interface IEmployeeDocumentRepository : IRepository<EmployeeDocument>
{
    /// <summary>
    /// Xodimning hujjatlari — yuklangan sana bo'yicha kamayish tartibida.
    /// </summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="documentTypeId">Faqat shu turdagilar. Bo'sh — hammasi («tur bo'yicha guruh»).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<EmployeeDocument>> GetByEmployeeAsync(
        Guid employeeId,
        Guid? documentTypeId = null,
        CancellationToken cancellationToken = default);
}
