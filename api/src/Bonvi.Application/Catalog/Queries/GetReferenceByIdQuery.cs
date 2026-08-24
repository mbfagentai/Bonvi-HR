using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Queries;

/// <summary>Bitta ma'lumotnoma yozuvini so'rash.</summary>
/// <param name="Id">Yozuv identifikatori.</param>
public sealed record GetReferenceByIdQuery(Guid Id);

/// <summary>
/// Har qanday ma'lumotnomaning bitta yozuvini qaytaradi.
/// </summary>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
/// <typeparam name="TDto">Tashqariga chiqadigan ko'rinish.</typeparam>
public sealed class GetReferenceByIdQueryHandler<TEntity, TDto>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IReferenceDefinition<TEntity, TDto> _definition;

    public GetReferenceByIdQueryHandler(
        IReferenceRepository<TEntity> repository,
        IReferenceDefinition<TEntity, TDto> definition)
    {
        _repository = repository;
        _definition = definition;
    }

    public async Task<Result<TDto>> HandleAsync(
        GetReferenceByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entity = await _repository.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            return CatalogErrors.NotFound;
        }

        return Result.Success(_definition.ToDto(entity));
    }
}
