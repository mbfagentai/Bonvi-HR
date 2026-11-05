using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Identity;

/// <inheritdoc />
public sealed class AttendanceScope : IAttendanceScope
{
    private readonly ICurrentUser _user;
    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;
    private readonly IClock _clock;

    private HashSet<Guid>? _allowedDepartments;
    private HashSet<Guid>? _employees;

    public AttendanceScope(
        ICurrentUser user,
        AppDbContext context,
        IDepartmentRepository departments,
        IClock clock)
    {
        _user = user;
        _context = context;
        _departments = departments;
        _clock = clock;
    }

    public bool IsScoped => _user.IsAttendanceScoped();

    public async Task<IReadOnlySet<Guid>?> AllowedDepartmentIdsAsync(CancellationToken cancellationToken = default)
        => IsScoped ? await LoadAllowedDepartmentsAsync(cancellationToken).ConfigureAwait(false) : null;

    public async Task<IReadOnlySet<Guid>?> EmployeeIdsInScopeAsync(CancellationToken cancellationToken = default)
        => IsScoped ? await LoadEmployeesAsync(cancellationToken).ConfigureAwait(false) : null;

    public async Task<bool> CanAccessEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employees = await EmployeeIdsInScopeAsync(cancellationToken).ConfigureAwait(false);

        return employees is null || employees.Contains(employeeId);
    }

    public async Task<bool> IsOwnRecordAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (!IsScoped || _user.UserId is not { } userId)
        {
            return false;
        }

        var ownEmployeeId = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return ownEmployeeId == employeeId;
    }

    private async Task<HashSet<Guid>> LoadAllowedDepartmentsAsync(CancellationToken cancellationToken)
    {
        if (_allowedDepartments is not null)
        {
            return _allowedDepartments;
        }

        var allowed = new HashSet<Guid>();

        // A marker without a user id cannot happen on a real token; it stays empty rather than open.
        if (_user.UserId is { } userId)
        {
            var roots = await _context.UserAttendanceScopes
                .AsNoTracking()
                .Where(scope => scope.UserId == userId)
                .Select(scope => scope.DepartmentId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // One recursive query per root, the same walk as `AttendanceReadModel.BranchUnionAsync`.
            // A factory's responsible has one or two roots.
            foreach (var root in roots)
            {
                allowed.Add(root);
                allowed.UnionWith(await _departments
                    .GetDescendantIdsAsync(root, cancellationToken)
                    .ConfigureAwait(false));
            }
        }

        return _allowedDepartments = allowed;
    }

    private async Task<HashSet<Guid>> LoadEmployeesAsync(CancellationToken cancellationToken)
    {
        if (_employees is not null)
        {
            return _employees;
        }

        var allowed = await LoadAllowedDepartmentsAsync(cancellationToken).ConfigureAwait(false);

        if (allowed.Count == 0)
        {
            return _employees = [];
        }

        // Only employees who ever worked in an allowed department can qualify, but ALL of
        // their employments are needed: a later one elsewhere is what takes them out.
        var employments = await _context.Set<Employment>()
            .AsNoTracking()
            .Where(employment => _context.Set<Employment>().Any(other =>
                other.EmployeeId == employment.EmployeeId
                && other.DepartmentId != null
                && allowed.Contains(other.DepartmentId.Value)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var today = _clock.Today;

        return _employees =
        [
            .. employments
                .GroupBy(employment => employment.EmployeeId)
                .Where(group => DepartmentOf(group, today) is { } department && allowed.Contains(department))
                .Select(group => group.Key),
        ];
    }

    private static Guid? DepartmentOf(IEnumerable<Employment> employments, DateOnly today)
    {
        var current = PrimaryEmployment.Select(employments, today);

        if (current is not null)
        {
            return current.DepartmentId;
        }

        return employments
            .Where(employment => employment.TerminationDate < today)
            .OrderByDescending(employment => employment.TerminationDate)
            .ThenByDescending(employment => employment.HireDate)
            .ThenBy(employment => employment.Id)
            .FirstOrDefault()
            ?.DepartmentId;
    }
}
