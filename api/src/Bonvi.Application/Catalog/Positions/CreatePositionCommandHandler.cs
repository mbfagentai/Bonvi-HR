using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;
using FluentValidation;

namespace Bonvi.Application.Catalog.Positions;

/// <summary>
/// Yangi lavozim qo'shadi.
/// </summary>
/// <remarks>
/// Bo'lim mavjudligi ATAYLAB oldindan tekshiriladi. Aks holda mavjud
/// bo'lmagan bo'lim ko'rsatilsa, xato tashqi kalit cheklovidan kelib
/// chiqadi va foydalanuvchi «500 Internal Server Error» ni ko'radi.
/// </remarks>
public sealed class CreatePositionCommandHandler
{
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly IReferenceDefinition<Position, PositionDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePositionCommandHandler(
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
        SaveReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return validation.ToError();
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

        var position = created.Value;

        var applied = _definition.Apply(position, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        _positions.Add(position);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PositionDefinition.ToDetails(position));
    }
}
