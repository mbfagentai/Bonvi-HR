using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary>
/// Barcha repozitoriylar uchun umumiy asos.
/// </summary>
/// <remarks>
/// <c>Set&lt;T&gt;()</c> orqali murojaat qilinadi, <c>DbSet</c> xossalari orqali emas —
/// shunda repozitoriy <c>AppDbContext</c> dagi nomlarga bog'lanmaydi.
/// </remarks>
public abstract class Repository<TAggregate> : IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    protected Repository(AppDbContext context) => Context = context;

    protected AppDbContext Context { get; }

    protected DbSet<TAggregate> Set => Context.Set<TAggregate>();

    public virtual Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public virtual Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.AnyAsync(e => e.Id == id, cancellationToken);

    public virtual void Add(TAggregate aggregate) => Set.Add(aggregate);

    public virtual void Remove(TAggregate aggregate) => Set.Remove(aggregate);
}
