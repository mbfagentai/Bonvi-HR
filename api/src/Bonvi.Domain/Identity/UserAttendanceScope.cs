namespace Bonvi.Domain.Identity;

/// <summary>
/// One department assigned to a scoped attendance user (<see cref="Permissions.AttendanceScoped"/>).
/// </summary>
/// <remarks>
/// The assignment covers the department and all its descendants. A scoped user with no row
/// here sees nothing: the check fails closed. The row carries only its two keys, so changing
/// a list means adding and removing rows, and it is audited as one change on the
/// <see cref="User"/> (see <c>SetUserAttendanceScopeCommandHandler</c>).
/// </remarks>
public sealed class UserAttendanceScope
{
    public UserAttendanceScope(Guid userId, Guid departmentId)
    {
        UserId = userId;
        DepartmentId = departmentId;
    }

    public Guid UserId { get; private set; }

    public Guid DepartmentId { get; private set; }
}
