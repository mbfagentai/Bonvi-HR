using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using FluentValidation;

namespace Bonvi.Application.Catalog.Departments;

/// <summary>
/// Bo'lim ma'lumotlarini tahrirlaydi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ota bo'lim bu yerda O'ZGARMAYDI.</b> Ko'chirish alohida amal
/// (<see cref="MoveDepartmentCommandHandler"/>), chunki u halqa tekshiruvini
/// talab qiladi. Agar tahrirlash formasi ham ota bo'limni o'zgartira olsa,
/// foydalanuvchi butun bo'limni bexosdan boshqa joyga ko'chirib yuborishi
/// va buni sezmasligi mumkin.
/// </para>
/// <para>
/// Nom o'zgarsa <c>Path</c> ham o'zgaradi — u shu bo'lim nomini o'z ichiga
/// oladi va avlodlarida ham takrorlanadi. Shu sababli tahrirlashdan keyin
/// ierarxiya qayta hisoblanadi.
/// </para>
/// </remarks>
public sealed class UpdateDepartmentCommandHandler
{
    private readonly IDepartmentRepository _departments;
    private readonly IEmployeeRepository _employees;
    private readonly IReferenceDefinition<Department, DepartmentDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDepartmentCommandHandler(
        IDepartmentRepository departments,
        IEmployeeRepository employees,
        IReferenceDefinition<Department, DepartmentDto> definition,
        IValidator<SaveReferenceCommand> validator,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _employees = employees;
        _definition = definition;
        _validator = validator;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(
        Guid id,
        SaveReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var department = await _departments.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return CatalogErrors.DepartmentNotFound;
        }

        // Boshliqni o'zgartirish (olib tashlash ham) — davomatni ko'rish huquqi bilan
        // (CatalogErrors.HeadPermissionRequired). O'zgarmagan boshliq — oddiy qayta nomlash, ruxsat kerak emas.
        if (command.HeadEmployeeId != department.HeadEmployeeId
            && !_currentUser.HasPermission(Permissions.AttendanceView))
        {
            return CatalogErrors.HeadPermissionRequired;
        }

        // Boshliq — mavjud, o'chirilmagan xodim (SPEC-jamoam §E). Faqat O'ZGARGANDA tekshiriladi:
        // forma joriy qiymatni har saqlashda yuboradi, ya'ni keyinroq o'chirilgan boshliq bo'limni
        // qayta nomlashni to'sib qo'ymasligi kerak.
        if (command.HeadEmployeeId is { } headId
            && headId != department.HeadEmployeeId
            && !await _employees.ExistsAsync(headId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.HeadEmployeeNotFound;
        }

        var taken = await _departments
            .NameExistsAsync(command.SafeName, id, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return CatalogErrors.NameTaken;
        }

        var renamed = department.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var applied = _definition.Apply(department, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _departments.RebuildHierarchyAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_definition.ToDto(department));
    }
}
