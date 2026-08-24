using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using FluentValidation;

namespace Bonvi.Application.Catalog.Commands;

/// <summary>
/// Har qanday ma'lumotnomaga yangi yozuv qo'shadi.
/// </summary>
/// <remarks>
/// <para>
/// Qadamlar tartibi ataylab shunday:
/// </para>
/// <list type="number">
///   <item><b>Format tekshiruvi</b> — bazaga bormasdan (arzon).</item>
///   <item><b>Nom bandligi</b> — bitta so'rov (qimmatroq).</item>
///   <item><b>Entity yaratish</b> — domen qoidalari.</item>
///   <item><b>Saqlash.</b></item>
/// </list>
/// <para>
/// Nom bandligi tekshiruvi bazadagi unikal indeksni ALMASHTIRMAYDI: ikki
/// foydalanuvchi bir vaqtda bir xil nom kiritsa, tekshiruv ikkalasida ham
/// o'tadi va to'qnashuvni indeks tutadi. Tekshiruvning maqsadi — odatiy
/// holatda foydalanuvchiga tushunarli xato berish, 500 emas.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
/// <typeparam name="TDto">Tashqariga chiqadigan ko'rinish.</typeparam>
public sealed class CreateReferenceCommandHandler<TEntity, TDto>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IReferenceDefinition<TEntity, TDto> _definition;
    private readonly IValidator<SaveReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public CreateReferenceCommandHandler(
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
        SaveReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var taken = await _repository
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

        var entity = created.Value;

        var applied = _definition.Apply(entity, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        _repository.Add(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_definition.ToDto(entity));
    }
}
