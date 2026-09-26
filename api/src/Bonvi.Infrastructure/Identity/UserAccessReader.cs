using Bonvi.Application.Identity.Abstractions;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Bonvi.Infrastructure.Identity;

/// <summary>
/// <see cref="IUserAccessReader"/> — ikki kichik so'rov va qisqa muddatli xotira keshi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kesh muddati 30 soniya va u faqat zaxira.</b> Asosiy eskirish yo'li —
/// <see cref="AccessVersion"/>: rol yoki hisob saqlangan zahoti versiya
/// oshadi (<c>AccessChangeInterceptor</c>) va keyingi so'rov bazadan
/// yangi ro'yxatni oladi. Muddat faqat bazani chetlab o'tgan o'zgarish
/// (qo'lda SQL) uchun: u ham ko'pi bilan 30 soniyada yetib boradi.
/// </para>
/// <para>
/// <b>Versiya so'rovdan OLDIN o'qiladi.</b> Aksincha bo'lsa, so'rov eski
/// qatorni o'qib, uni YANGI versiya kaliti ostiga yozib qo'yishi mumkin
/// edi — va o'sha eski natija 30 soniya «yangi» bo'lib yurardi.
/// </para>
/// <para>
/// <b>Faolsiz hisob ham keshlanadi</b> (<c>null</c>): faollashtirish
/// versiyani baribir oshiradi, bloklangan tokenni esa har so'rovda bazaga
/// olib borishning ma'nosi yo'q.
/// </para>
/// </remarks>
public sealed class UserAccessReader : IUserAccessReader
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly AccessVersion _version;

    public UserAccessReader(AppDbContext context, IMemoryCache cache, AccessVersion version)
    {
        _context = context;
        _cache = cache;
        _version = version;
    }

    public async Task<UserAccess?> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = new CacheKey(_version.Current, userId);

        if (_cache.TryGetValue(key, out Cached? cached) && cached is not null)
        {
            return cached.Access;
        }

        var access = await LoadAsync(userId, cancellationToken).ConfigureAwait(false);

        _cache.Set(key, new Cached(access), Lifetime);

        return access;
    }

    private async Task<UserAccess?> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        // O'chirilgan hisob yumshoq o'chirish filtri bilan o'zi tushib qoladi.
        var roleIds = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.RoleIds)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (roleIds is null)
        {
            return null;
        }

        var permissions = new HashSet<string>(StringComparer.Ordinal);

        if (roleIds.Count == 0)
        {
            return new UserAccess([], permissions);
        }

        var roles = await _context.Roles
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .OrderBy(role => role.Code)
            .Select(role => new { role.Code, role.Permissions })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var role in roles)
        {
            permissions.UnionWith(role.Permissions);
        }

        return new UserAccess([.. roles.Select(role => role.Code)], permissions);
    }

    /// <summary>Kesh kaliti: versiya + hisob. Versiya oshsa — boshqa kalit.</summary>
    private sealed record CacheKey(long Version, Guid UserId);

    /// <summary>
    /// Kesh qiymati. <c>null</c> natija ham shu qobiqda saqlanadi — aks holda
    /// «keshda yo'q» va «hisob faolsiz» bir-biridan ajralmasdi.
    /// </summary>
    private sealed record Cached(UserAccess? Access);
}
