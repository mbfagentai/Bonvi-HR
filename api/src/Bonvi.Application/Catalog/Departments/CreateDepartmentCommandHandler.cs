using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;
using FluentValidation;

namespace Bonvi.Application.Catalog.Departments;

/// <summary>
/// Daraxtga yangi bo'lim qo'shadi.
/// </summary>
/// <remarks>
/// <para>
/// Umumiy <c>CreateReferenceCommandHandler</c> dan farqi ikkita:
/// ota bo'lim mavjudligi tekshiriladi va yozilgandan keyin butun
/// daraxtning <c>Level</c>/<c>Path</c> maydonlari qayta hisoblanadi.
/// </para>
/// <para>
/// Ierarxiyani qayta hisoblash SAQLAGANDAN KEYIN chaqiriladi: hisob
/// ma'lumotlar bazasida bajariladi va u yangi qatorni ko'rishi kerak.
/// </para>
/// </remarks>
public sealed class CreateDepartmentCommandHandler
{
    private readonly IDepartmentRepository _departments;
    private readonly IEmployeeRepository _employees;
    private readonly IReferenceDefinition<Department, DepartmentDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDepartmentCommandHandler(
        IDepartmentRepository departments,
        IEmployeeRepository employees,
        IReferenceDefinition<Department, DepartmentDto> definition,
        IValidator<SaveReferenceCommand> validator,
        IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _employees = employees;
        _definition = definition;
        _validator = validator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DepartmentDto>> HandleAsync(
        SaveReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        if (command.ParentId is { } parentId)
        {
            var parentExists = await _departments.ExistsAsync(parentId, cancellationToken).ConfigureAwait(false);
            if (!parentExists)
            {
                return CatalogErrors.ParentDepartmentNotFound;
            }
        }

        // Boshliq — mavjud, o'chirilmagan xodim (SPEC-jamoam §E).
        if (command.HeadEmployeeId is { } headId
            && !await _employees.ExistsAsync(headId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.HeadEmployeeNotFound;
        }

        var taken = await _departments
            .NameExistsAsync(command.SafeName, null, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return CatalogErrors.NameTaken;
        }

        var created = _definition.Create(command);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var department = created.Value;

        var applied = _definition.Apply(department, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        _departments.Add(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _departments.RebuildHierarchyAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_definition.ToDto(department));
    }
}
