using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Processes.Queries;

/// <summary>
/// §7.7 and §9.1: which users stand behind an office role. A step stores the role; who of that role sees the task
/// is a read-time question, so an assignment changed in «Rollar» shows at once.
/// </summary>
public sealed class ProcessHolderDirectory
{
    private readonly Dictionary<string, IReadOnlyList<Guid>> _usersByRole;

    public ProcessHolderDirectory(IEnumerable<(string Role, Guid UserId)> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        _usersByRole = assignments
            .GroupBy(assignment => assignment.Role, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<Guid> (group) => [.. group.Select(row => row.UserId).Distinct()],
                StringComparer.Ordinal);
    }

    /// <summary>Users holding an office role now. Empty = unassigned: every <c>processes.manage</c> holder sees it.</summary>
    public IReadOnlyList<Guid> UsersOf(string role)
    {
        if (_usersByRole.TryGetValue(role, out var users))
        {
            return users;
        }

        // §9.1: HR-5/HR-8 work is done by HR-1 until someone is assigned.
        return role is ProcessRoles.Hr5 or ProcessRoles.Hr8 && _usersByRole.TryGetValue(ProcessRoles.Hr1, out var hr1)
            ? hr1
            : [];
    }

    /// <summary>The office roles whose tasks this user sees.</summary>
    public IReadOnlySet<string> OfficeRolesOf(Guid? userId, bool canManage)
        => ProcessRoles.Office
            .Where(role => UsersOf(role) is { Count: > 0 } users
                ? userId is { } id && users.Contains(id)
                : canManage)
            .ToHashSet(StringComparer.Ordinal);
}

/// <summary>Who is reading: the §7.7 answer for one user, plus what §9.3 lets them do.</summary>
public sealed class ProcessViewer
{
    public ProcessViewer(Guid? userId, Guid? employeeId, IReadOnlySet<string> officeRoles, bool canWork, bool canManage)
    {
        UserId = userId;
        EmployeeId = employeeId;
        OfficeRoles = officeRoles;
        CanWork = canWork;
        CanManage = canManage;
    }

    public Guid? UserId { get; }

    public Guid? EmployeeId { get; }

    public IReadOnlySet<string> OfficeRoles { get; }

    public bool CanWork { get; }

    public bool CanManage { get; }

    /// <summary>A resolved person holds the step alone; otherwise the office role does (§7.7).</summary>
    public bool Holds(string? role, Guid? holderEmployeeId)
        => holderEmployeeId is { } person
            ? person == EmployeeId
            : role is not null && OfficeRoles.Contains(role);
}

/// <summary>Builds the viewer and the directory once per request.</summary>
public sealed class ProcessViewerLoader
{
    private readonly ICurrentUser _user;
    private readonly IBlankReadModel _blanks;
    private readonly IBlankRoleAssignmentRepository _assignments;

    public ProcessViewerLoader(ICurrentUser user, IBlankReadModel blanks, IBlankRoleAssignmentRepository assignments)
    {
        _user = user;
        _blanks = blanks;
        _assignments = assignments;
    }

    public async Task<(ProcessViewer Viewer, ProcessHolderDirectory Directory)> LoadAsync(CancellationToken cancellationToken = default)
    {
        var assignments = await _assignments.ListAsync(cancellationToken).ConfigureAwait(false);
        var directory = new ProcessHolderDirectory(assignments.Select(row => (row.RoleCode, row.UserId)));

        var userId = _user.UserId;
        var employeeId = userId is { } id
            ? await _blanks.GetUserEmployeeIdAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        var canManage = _user.HasPermission(Permissions.ProcessesManage);

        return (
            new ProcessViewer(
                userId,
                employeeId,
                directory.OfficeRolesOf(userId, canManage),
                _user.HasPermission(Permissions.ProcessesWork),
                canManage),
            directory);
    }
}
