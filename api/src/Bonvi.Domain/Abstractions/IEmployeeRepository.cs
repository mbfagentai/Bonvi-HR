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
    Task<IReadOnlyList<EmployeeFlag>> GetByEmployeeAsync(
        Guid employeeId,
        bool onlyActive = false,
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

    Task<IReadOnlyList<EmployeeFlag>> ListByTypeAsync(
        EmployeeFlagType type,
        bool onlyActive,
        DateOnly on,
        CancellationToken cancellationToken = default);
}
