using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IUserRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public Task<User?> GetByLoginAsync(PhoneNumber login, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        // Qiymat obyektining O'ZI solishtiriladi, uning `.Value` xossasi emas.
        // Sabab: `Login` konvertor orqali `string` ga map qilingan, shu sababli
        // EF uchun u skalyar. `.Value` ga murojaat SQL'ga tarjima bo'lmaydi,
        // butun obyekt esa `users.login = @p0` ga aylanadi.
        return Set.FirstOrDefaultAsync(u => u.Login == login, cancellationToken);
    }

    public Task<bool> LoginExistsAsync(PhoneNumber login, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        return Set.AnyAsync(u => u.Login == login, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRoleCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await GetRoleIdsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (roleIds.Count == 0)
        {
            return [];
        }

        return await Context.Set<Role>()
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .OrderBy(r => r.Code)
            .Select(r => r.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await GetRoleIdsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (roleIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var perRole = await Context.Set<Role>()
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Permissions)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bir nechta rol bir xil ruxsatga ega bo'lishi mumkin — takrorlar olib tashlanadi
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permissions in perRole)
        {
            foreach (var permission in permissions)
            {
                result.Add(permission);
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<Guid>> GetRoleIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        return user?.RoleIds ?? [];
    }
}
