using Bonvi.Domain.Common;

namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Agregat ildizi uchun saqlash porti.
/// </summary>
/// <remarks>
/// Bu interfeys Domain qatlamida e'lon qilinadi, Infrastructure'da amalga oshiriladi.
/// Shu sababli domen PostgreSQL, EF Core yoki boshqa texnologiyani bilmaydi —
/// ularni almashtirish biznes mantiqqa tegmaydi.
/// </remarks>
public interface IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(TAggregate aggregate);

    void Remove(TAggregate aggregate);
}
