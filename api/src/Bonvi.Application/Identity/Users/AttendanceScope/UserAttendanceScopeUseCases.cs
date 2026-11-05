using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.AttendanceScope;

/// <summary>The departments assigned to a scoped attendance user.</summary>
/// <param name="DepartmentIds">Assigned ones only, without the descendants they cover.</param>
public sealed record UserAttendanceScopeDto(IReadOnlyList<Guid> DepartmentIds);

/// <summary>Reads a user's assignment.</summary>
/// <param name="UserId">The account.</param>
public sealed record GetUserAttendanceScopeQuery(Guid UserId);

/// <summary>Handles <see cref="GetUserAttendanceScopeQuery"/>.</summary>
public sealed class GetUserAttendanceScopeQueryHandler
{
    private readonly IUserRepository _users;
    private readonly IUserAttendanceScopeRepository _scopes;

    public GetUserAttendanceScopeQueryHandler(IUserRepository users, IUserAttendanceScopeRepository scopes)
    {
        _users = users;
        _scopes = scopes;
    }

    public async Task<Result<UserAttendanceScopeDto>> HandleAsync(
        GetUserAttendanceScopeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await _users.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return UserErrors.NotFound;
        }

        var rows = await _scopes.ListForUserAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return new UserAttendanceScopeDto([.. rows.Select(row => row.DepartmentId).Order()]);
    }
}

/// <summary>Replaces a user's whole assignment.</summary>
/// <param name="UserId">The account.</param>
/// <param name="DepartmentIds">The complete new list; missing or empty means no department, so nothing visible.</param>
public sealed record SetUserAttendanceScopeCommand(Guid UserId, IReadOnlyList<Guid>? DepartmentIds);

/// <summary>Handles <see cref="SetUserAttendanceScopeCommand"/>.</summary>
/// <remarks>
/// The rows hold nothing but their keys, so <c>AuditTrailInterceptor</c> would record one
/// empty line per department. This writes one Update on the User with both lists instead,
/// and nothing at all when the list did not change.
/// </remarks>
public sealed class SetUserAttendanceScopeCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUserAttendanceScopeRepository _scopes;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _unitOfWork;

    public SetUserAttendanceScopeCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IUserAttendanceScopeRepository scopes,
        IAuditLogger audit,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _scopes = scopes;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserAttendanceScopeDto>> HandleAsync(
        SetUserAttendanceScopeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var target = await _users.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UserErrors.NotFound;
        }

        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var allowed = UserRoleRules.EnsureCanActOn(acting.Value, target, roles);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        List<Guid> wanted = [.. (command.DepartmentIds ?? []).Distinct().Order()];

        var existing = await _scopes.CountExistingDepartmentsAsync(wanted, cancellationToken).ConfigureAwait(false);
        if (existing != wanted.Count)
        {
            return CatalogErrors.DepartmentNotFound;
        }

        var current = await _scopes.ListForUserAsync(target.Id, cancellationToken).ConfigureAwait(false);
        List<Guid> before = [.. current.Select(row => row.DepartmentId).Order()];

        if (before.SequenceEqual(wanted))
        {
            return new UserAttendanceScopeDto(wanted);
        }

        foreach (var stale in current.Where(row => !wanted.Contains(row.DepartmentId)))
        {
            _scopes.Remove(stale);
        }

        foreach (var added in wanted.Except(before))
        {
            _scopes.Add(new UserAttendanceScope(target.Id, added));
        }

        _audit.Record(
            AuditAction.Update,
            nameof(User),
            target.Id.ToString(),
            oldValues: Snapshot(before),
            newValues: Snapshot(wanted));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserAttendanceScopeDto(wanted);
    }

    private static string Snapshot(IReadOnlyList<Guid> departmentIds)
        => JsonSerializer.Serialize(new Dictionary<string, IReadOnlyList<Guid>>
        {
            ["AttendanceScopeDepartmentIds"] = departmentIds,
        });
}
