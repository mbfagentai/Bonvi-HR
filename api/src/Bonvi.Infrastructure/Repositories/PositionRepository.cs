using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IPositionRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class PositionRepository : ReferenceRepository<Position>, IPositionRepository
{
    public PositionRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// Vazifalar tartibi <c>Include</c> ichida ATAYLAB beriladi. Aks holda
    /// PostgreSQL qatorlarni ixtiyoriy tartibda qaytaradi va foydalanuvchi
    /// har ochganda vazifalar boshqacha joylashardi.
    /// So'rov <c>(position_id, sort_order)</c> indeksiga to'liq tushadi.
    /// </remarks>
    public Task<Position?> GetWithDutiesAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(position => position.Duties.OrderBy(duty => duty.SortOrder))
            .FirstOrDefaultAsync(position => position.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Position>> GetByDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(position => position.DepartmentId == departmentId)
            .OrderBy(position => position.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
