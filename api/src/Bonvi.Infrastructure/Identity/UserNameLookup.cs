using Bonvi.Application.Abstractions;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Identity;

/// <summary>
/// <see cref="IUserNameLookup"/> — <c>users</c> jadvalidan, yumshoq o'chirish filtrisiz.
/// </summary>
public sealed class UserNameLookup : IUserNameLookup
{
    private readonly AppDbContext _context;

    public UserNameLookup(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        List<Guid> ids = [.. userIds];

        var rows = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName.LastName, user.FullName.FirstName, user.FullName.MiddleName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.Id,
            row => FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full);
    }
}
