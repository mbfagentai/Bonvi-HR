using Bonvi.Application.Abstractions;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>
/// What the current request may see of attendance: everything, or the staff of the
/// departments assigned to a scoped user.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>null</c> means unrestricted, an empty set means nothing.</b> Callers must keep the
/// two apart: treating <c>null</c> as «no one» breaks every existing role, treating an empty
/// set as «no filter» opens all staff to a user with no assignment.
/// </para>
/// <para>
/// Background jobs and employee tokens carry no marker, so they resolve to unrestricted.
/// Results are cached for the lifetime of the scope, i.e. one request.
/// </para>
/// </remarks>
public interface IAttendanceScope
{
    /// <summary>Whether the current principal is restricted (see <see cref="Permissions.AttendanceScoped"/>).</summary>
    bool IsScoped { get; }

    /// <summary>Assigned departments and all their descendants; <c>null</c> when unrestricted.</summary>
    Task<IReadOnlySet<Guid>?> AllowedDepartmentIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Employees whose department today is in <see cref="AllowedDepartmentIdsAsync"/>;
    /// <c>null</c> when unrestricted.
    /// </summary>
    /// <remarks>
    /// An employee's department is that of their primary employment today. With none, it is
    /// that of the latest ended employment, so former employees stay on past timesheets and
    /// alerts. Someone transferred away leaves the scope, past days included.
    /// </remarks>
    Task<IReadOnlySet<Guid>?> EmployeeIdsInScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether this employee may be shown; always <c>true</c> when unrestricted.</summary>
    Task<bool> CanAccessEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this employee is the restricted user themselves (<see cref="User.EmployeeId"/>).
    /// </summary>
    /// <remarks>
    /// Always <c>false</c> when unrestricted, and for an account linked to no employee: with no
    /// link there is nothing to compare, so this rule cannot apply to that account.
    /// </remarks>
    Task<bool> IsOwnRecordAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

/// <summary>The one definition of «scoped», shared by the guard and <see cref="IAttendanceScope"/>.</summary>
public static class AttendanceScopeRule
{
    /// <summary>Holds the marker and does not manage attendance.</summary>
    /// <param name="user">The current principal.</param>
    public static bool IsAttendanceScoped(this ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.HasPermission(Permissions.AttendanceScoped)
            && !user.HasPermission(Permissions.AttendanceManage);
    }

    /// <summary>The same rule for a role's permissions, for what a holder would become.</summary>
    /// <param name="permissions">The role's permission codes.</param>
    public static bool IsAttendanceScopedRole(IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return permissions.Contains(Permissions.AttendanceScoped)
            && !permissions.Contains(Permissions.AttendanceManage);
    }
}
