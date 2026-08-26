using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IEmployeeCompetencyRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class EmployeeCompetencyRepository : Repository<EmployeeCompetency>, IEmployeeCompetencyRepository
{
    public EmployeeCompetencyRepository(AppDbContext context) : base(context) { }

    public Task<EmployeeCompetency?> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(competency => competency.EmployeeId == employeeId, cancellationToken);
}
