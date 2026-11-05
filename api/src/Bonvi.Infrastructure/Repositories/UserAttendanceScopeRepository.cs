using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <inheritdoc />
public sealed class UserAttendanceScopeRepository : IUserAttendanceScopeRepository
{
    private readonly AppDbContext _context;

    public UserAttendanceScopeRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<UserAttendanceScope>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _context.UserAttendanceScopes
            .Where(scope => scope.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountExistingDepartmentsAsync(
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken = default)
        => _context.Set<Department>()
            .AsNoTracking()
            .CountAsync(department => departmentIds.Contains(department.Id), cancellationToken);

    public void Add(UserAttendanceScope scope) => _context.UserAttendanceScopes.Add(scope);

    public void Remove(UserAttendanceScope scope) => _context.UserAttendanceScopes.Remove(scope);
}
