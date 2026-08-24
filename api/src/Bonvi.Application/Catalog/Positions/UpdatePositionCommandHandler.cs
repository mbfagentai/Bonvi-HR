using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;
using FluentValidation;

namespace Bonvi.Application.Catalog.Positions;

/// <summary>
/// Lavozim ma'lumotlarini tahrirlaydi.
/// </summary>
/// <remarks>
/// Vazifalar bu yerda O'ZGARMAYDI — ular uchun alohida amallar bor
/// (qo'shish, o'chirish, tartiblash). Sabab: lavozim kartochkasida vazifalar
/// alohida ro'yxat bo'lib ko'rinadi va ularni tahrirlash formani saqlashdan
/// mustaqil bo'lishi kerak.
/// </remarks>
public sealed class UpdatePositionCommandHandler
{
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly IReferenceDefinition<Position, PositionDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePositionCommandHandler(
        IPositionRepository positions,
        IDepartmentRepository departments,
        IReferenceDefinition<Position, PositionDto> definition,
        IValidator<SaveReferenceCommand> validator,
        IUnitOfWork unitOfWork)
    {
        _positions = positions;
        _departments = departments;
        _definition = definition;
        _validator = validator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PositionDetailsDto>> HandleAsync(
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

        // Vazifalari bilan yuklanadi: javobda ular qaytariladi, ya'ni
        // saqlagandan keyin ekran to'liq holatga ega bo'ladi.
        var position = await _positions.GetWithDutiesAsync(id, cancellationToken).ConfigureAwait(false);
        if (position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        if (command.DepartmentId is { } departmentId)
        {
            var departmentExists = await _departments
                .ExistsAsync(departmentId, cancellationToken)
                .ConfigureAwait(false);

            if (!departmentExists)
            {
                return CatalogErrors.DepartmentNotFound;
            }
        }

        var taken = await _positions
            .NameExistsAsync(command.SafeName, id, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return CatalogErrors.NameTaken;
        }

        var renamed = position.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var applied = _definition.Apply(position, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PositionDefinition.ToDetails(position));
    }
}
