using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IRefreshTokenRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context) { }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return Set.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<int> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset at,
        string? byIp,
        CancellationToken cancellationToken = default)
    {
        var active = await Set
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in active)
        {
            token.Revoke(at, byIp);
        }

        return active.Count;
    }

    /// <remarks>
    /// <c>ExecuteDeleteAsync</c> — yozuvlarni xotiraga yuklamasdan o'chiradi.
    /// Bu fon vazifasi bo'lgani uchun minglab qator bo'lishi mumkin.
    /// </remarks>
    public Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        => Set
            .Where(t => t.ExpiresAt < before)
            .ExecuteDeleteAsync(cancellationToken);
}
