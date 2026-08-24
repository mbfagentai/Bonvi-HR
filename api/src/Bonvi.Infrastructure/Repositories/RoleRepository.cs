using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IRoleRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class RoleRepository : Repository<Role>, IRoleRepository
{
    public RoleRepository(AppDbContext context) : base(context) { }

    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalized = code.Trim().ToLowerInvariant();
        return Set.FirstOrDefaultAsync(r => r.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
}
