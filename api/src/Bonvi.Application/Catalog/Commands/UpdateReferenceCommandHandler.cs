using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using FluentValidation;

namespace Bonvi.Application.Catalog.Commands;

/// <summary>
/// Har qanday ma'lumotnoma yozuvini tahrirlaydi.
/// </summary>
/// <remarks>
/// Yozuv O'CHIRILMAYDI va tahrirlash uni o'chirmaydi: eski buyruq va
/// hodisalarda unga havola qolgan bo'lishi mumkin. Ishlatishdan chiqarish
/// uchun «faolsizlantirish» amali bor.
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
/// <typeparam name="TDto">Tashqariga chiqadigan ko'rinish.</typeparam>
public sealed class UpdateReferenceCommandHandler<TEntity, TDto>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IReferenceDefinition<TEntity, TDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateReferenceCommandHandler(
        IReferenceRepository<TEntity> repository,
        IReferenceDefinition<TEntity, TDto> definition,
        IValidator<SaveReferenceCommand> validator,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _definition = definition;
        _validator = validator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TDto>> HandleAsync(
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

        var entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return CatalogErrors.NotFound;
        }

        // `exceptId` — yozuvning o'z nomini o'ziga qarshi ishlatmaslik uchun:
        // faqat kodni o'zgartirib saqlash ham «nom band» xatosini bermasligi kerak.
        var taken = await _repository
            .NameExistsAsync(command.SafeName, id, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return CatalogErrors.NameTaken;
        }

        var renamed = entity.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var applied = _definition.Apply(entity, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_definition.ToDto(entity));
    }
}
