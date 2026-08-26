using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IIncentiveRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class IncentiveRepository : Repository<Incentive>, IIncentiveRepository
{
    public IncentiveRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Incentive>> GetByEmployeeAsync(
        Guid employeeId,
        IncentiveKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()
            .Where(incentive => incentive.EmployeeId == employeeId);

        if (kind is { } value)
        {
            query = query.Where(incentive => incentive.Kind == value);
        }

        return await query
            .OrderByDescending(incentive => incentive.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
