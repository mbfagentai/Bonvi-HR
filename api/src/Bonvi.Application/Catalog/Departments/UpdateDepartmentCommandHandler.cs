using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
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
    private readonly IReferenceDefinition<Department, DepartmentDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDepartmentCommandHandler(
        IDepartmentRepository departments,
        IReferenceDefinition<Department, DepartmentDto> definition,
        IValidator<SaveReferenceCommand> validator,
        IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _definition = definition;
        _validator = validator;
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
