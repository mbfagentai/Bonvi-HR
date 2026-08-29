using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IEmployeeFlagRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// Faollik sharti hamma joyda BIR XIL yozilgan:
/// <c>start_date &lt;= kun AND (end_date IS NULL OR end_date &gt;= kun)</c>.
/// Uni har joyda boshqacha yozish — belgining bir ekranda ko'rinib,
/// boshqasida ko'rinmasligining eng keng tarqalgan sababi.
/// </remarks>
public sealed class EmployeeFlagRepository : Repository<EmployeeFlag>, IEmployeeFlagRepository
{
    public EmployeeFlagRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeFlag>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Where(flag => flag.EmployeeId == employeeId)
            .OrderByDescending(flag => flag.StartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ishdan bo'shatish va qayta ishga olishdagi ogohlantirish shu metodga
    /// tayanadi. Qator o'qilmaydi — faqat mavjudligi tekshiriladi, so'rov
    /// <c>(employee_id, type)</c> indeksidan chiqmaydi.
    /// </remarks>
    public Task<bool> HasActiveFlagAsync(
        Guid employeeId,
        EmployeeFlagType type,
        DateOnly on,
        CancellationToken cancellationToken = default)
        => Set.AnyAsync(
            flag =>
                flag.EmployeeId == employeeId &&
                flag.Type == type &&
                flag.StartDate <= on &&
                (flag.EndDate == null || flag.EndDate >= on),
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <c>AsNoTracking</c> ATAYLAB YO'Q — bu yagona joyda belgilar
    /// o'zgartirish uchun olinadi (yangisi bilan almashtirilganda).
    /// Shart yuqoridagilardan bir belgiga farq qiladi:
    /// <c>end_date &gt; kun</c>, <c>&gt;=</c> emas — o'sha kuni tugaydigan
    /// belgi yangisiga xalaqit bermaydi.
    /// </remarks>
    public async Task<IReadOnlyList<EmployeeFlag>> GetInForceAsync(
        Guid employeeId,
        DateOnly on,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(flag =>
                flag.EmployeeId == employeeId &&
                flag.StartDate <= on &&
                (flag.EndDate == null || flag.EndDate > on))
            .OrderByDescending(flag => flag.StartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

}
