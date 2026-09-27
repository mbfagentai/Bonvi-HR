using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary>
/// <see cref="IEmployeeCheckFileRepository"/> — EF Core orqali.
/// </summary>
/// <remarks>
/// Yumshoq o'chirilganlar global filtr bilan o'zi chiqib ketadi
/// (<c>AppDbContext</c> dagi <c>ISoftDeletable</c> qoidasi).
/// </remarks>
public sealed class EmployeeCheckFileRepository : Repository<EmployeeCheckFile>, IEmployeeCheckFileRepository
{
    public EmployeeCheckFileRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<EmployeeCheckFile>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(file => file.EmployeeId == employeeId)
            .OrderBy(file => file.CreatedAt)
            .ThenBy(file => file.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
