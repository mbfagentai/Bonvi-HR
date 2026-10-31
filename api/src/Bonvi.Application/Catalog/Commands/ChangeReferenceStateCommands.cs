using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Usage;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Commands;

/// <summary>Ma'lumotnoma yozuvini faolsizlantirish.</summary>
/// <param name="Id">Yozuv identifikatori.</param>
/// <param name="Reason">
/// Nima uchun faolsizlantirildi. Izohga yoziladi — bir yildan keyin
/// «nega bu bo'lim yo'q?» degan savolga javob shu yerdan topiladi.
/// </param>
public sealed record DeactivateReferenceCommand(Guid Id, string? Reason = null);

/// <summary>
/// Yozuvni ishlatishdan chiqaradi (o'chirmaydi).
/// </summary>
/// <remarks>
/// <para>
/// Ma'lumotnoma yozuvi HECH QACHON o'chirilmaydi. Sabab: 2019-yilgi buyruqda
/// «Плазмада кесиш бўлими» ko'rsatilgan bo'lsa, o'sha bo'lim bugun
/// tugatilgan bo'lsa ham, tarixiy yozuv o'z nomini yo'qotmasligi kerak.
/// </para>
/// <para>
/// Faolsizlantirilgan yozuv yangi formalarda tanlanmaydi, lekin eski
/// yozuvlarda ko'rinib turadi.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
public sealed class DeactivateReferenceCommandHandler<TEntity>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IReferenceStateRule<TEntity>> _rules;

    public DeactivateReferenceCommandHandler(
        IReferenceRepository<TEntity> repository,
        IUnitOfWork unitOfWork,
        IEnumerable<IReferenceStateRule<TEntity>>? rules = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        // Qoidasi yo'q ma'lumotnoma — bo'sh ro'yxat (SPEC-malumotnoma-faolsizlantirish §B)
        _rules = rules is null ? [] : [.. rules];
    }

    public async Task<Result> HandleAsync(
        DeactivateReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return CatalogErrors.NotFound;
        }

        foreach (var rule in _rules)
        {
            var allowed = await rule.BeforeDeactivateAsync(entity, cancellationToken).ConfigureAwait(false);
            if (allowed.IsFailure)
            {
                return allowed.Error;
            }
        }

        entity.Deactivate(command.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Faolsizlantirilgan yozuvni qaytarish.</summary>
/// <param name="Id">Yozuv identifikatori.</param>
public sealed record ActivateReferenceCommand(Guid Id);

/// <summary>
/// Yozuvni qayta faol holatga qaytaradi.
/// </summary>
/// <remarks>
/// Xatolikni tuzatish yo'li: xodim noto'g'ri yozuvni faolsizlantirsa,
/// uni administratorsiz, o'sha sahifaning o'zida qaytarish mumkin.
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
public sealed class ActivateReferenceCommandHandler<TEntity>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IReferenceStateRule<TEntity>> _rules;

    public ActivateReferenceCommandHandler(
        IReferenceRepository<TEntity> repository,
        IUnitOfWork unitOfWork,
        IEnumerable<IReferenceStateRule<TEntity>>? rules = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _rules = rules is null ? [] : [.. rules];
    }

    public async Task<Result> HandleAsync(
        ActivateReferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return CatalogErrors.NotFound;
        }

        foreach (var rule in _rules)
        {
            var allowed = await rule.BeforeActivateAsync(entity, cancellationToken).ConfigureAwait(false);
            if (allowed.IsFailure)
            {
                return allowed.Error;
            }
        }

        entity.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
