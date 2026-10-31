using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Departments;

/// <summary>Bo'lim boshlig'ini tayinlash yoki olib tashlash — xodim kartasidan (egasi, 2026-10-31).</summary>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="EmployeeId">Yangi boshliq; <c>null</c> — boshliqni olib tashlash.</param>
public sealed record SetDepartmentHeadCommand(Guid DepartmentId, Guid? EmployeeId);

/// <summary>
/// Faqat boshliqni o'zgartiradi — bo'lim formasidagi (<see cref="UpdateDepartmentCommandHandler"/>)
/// qoidalarning o'zi.
/// </summary>
/// <remarks>
/// Butun bo'lim formasini qayta yubormaslik uchun alohida amal: xodim kartasi bo'lim nomi va yuridik
/// shaxsini bilmaydi, eski nusxani yuborsa esa o'rtada boshqa HR qilgan o'zgarishni ustidan yozardi.
/// </remarks>
public sealed class SetDepartmentHeadCommandHandler
{
    private readonly IDepartmentRepository _departments;
    private readonly IEmployeeRepository _employees;
    private readonly IReferenceDefinition<Department, DepartmentDto> _definition;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SetDepartmentHeadCommandHandler(
        IDepartmentRepository departments,
        IEmployeeRepository employees,
        IReferenceDefinition<Department, DepartmentDto> definition,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _employees = employees;
        _definition = definition;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(
        SetDepartmentHeadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Boshliq davomatni ko'radi — beruvchida ham shu huquq bo'lishi shart (CatalogErrors.HeadPermissionRequired)
        if (!_currentUser.HasPermission(Permissions.AttendanceView))
        {
            return CatalogErrors.HeadPermissionRequired;
        }

        var department = await _departments.GetByIdAsync(command.DepartmentId, cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return CatalogErrors.DepartmentNotFound;
        }

        if (command.EmployeeId is { } headId
            && headId != department.HeadEmployeeId
            && !await _employees.ExistsAsync(headId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.HeadEmployeeNotFound;
        }

        if (command.EmployeeId != department.HeadEmployeeId)
        {
            department.AssignHead(command.EmployeeId);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(_definition.ToDto(department));
    }
}
