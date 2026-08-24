using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using FluentValidation;

namespace Bonvi.Application.Catalog.Commands;

/// <summary>
/// Ma'lumotnoma yozuvlarini foydalanuvchi bergan ketma-ketlikka keltiradi.
/// </summary>
/// <remarks>
/// <para>
/// HR uchun tartib muhim: «Ўз хохиши билан» ishdan ketish sabablarining
/// eng ko'p ishlatiladigani, u ro'yxatning boshida turishi kerak.
/// Alifbo tartibi bu yerda foyda bermaydi.
/// </para>
/// <para>
/// <b>Nega ikki bosqichli o'qish.</b> <c>ListAsync</c> yozuvlarni KUZATUVSIZ
/// qaytaradi (u ko'rsatish uchun mo'ljallangan), ya'ni undan olingan obyektni
/// o'zgartirish bazaga yetib bormaydi. Shu sababli ro'yxat faqat mavjudlikni
/// va joriy tartibni bilish uchun o'qiladi, keyin esa <b>faqat o'rni haqiqatan
/// o'zgargan</b> yozuvlar kuzatiladigan holda yuklanadi. Sudrab tashlashdan
/// keyin odatda ikki-uch yozuv siljiydi, ya'ni so'rovlar soni ham shuncha.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
public sealed class ReorderReferenceCommandHandler<TEntity>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IValidator<ReorderReferenceCommand> _validator;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderReferenceCommandHandler(
        IReferenceRepository<TEntity> repository,
        IValidator<ReorderReferenceCommand> validator,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _validator = validator;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ReorderReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var all = await _repository.ListAsync(false, cancellationToken).ConfigureAwait(false);
        var currentOrder = all.ToDictionary(entity => entity.Id, entity => entity.SortOrder);

        for (var i = 0; i < command.OrderedIds.Count; i++)
        {
            var id = command.OrderedIds[i];

            if (!currentOrder.TryGetValue(id, out var existingOrder))
            {
                return CatalogErrors.NotFound;
            }

            if (existingOrder == i)
            {
                continue;
            }

            var entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (entity is null)
            {
                return CatalogErrors.NotFound;
            }

            entity.SetSortOrder(i);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
