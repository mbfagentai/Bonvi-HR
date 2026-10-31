using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Usage;

/// <summary>
/// Ma'lumotnoma yozuvini faol emas / faol qilishdan OLDINGI qoida (docs/SPEC-malumotnoma-faolsizlantirish.md §B).
/// </summary>
/// <remarks>
/// <see cref="IReferenceSavedHook{TEntity}"/> bilan bir xil yo'l: generik handler boshqa modullarni
/// bilmaydi, qoidasi yo'q ma'lumotnoma avvalgidek ishlaydi. Qoida xato qaytarsa, holat o'zgarmaydi.
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
public interface IReferenceStateRule<in TEntity>
    where TEntity : ReferenceEntity
{
    /// <summary>Faol emas qilishdan oldin; kerak bo'lsa bog'liq yozuvlarni ham o'zgartiradi (bitta saqlashda).</summary>
    Task<Result> BeforeDeactivateAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>Qayta faol qilishdan oldin.</summary>
    Task<Result> BeforeActivateAsync(TEntity entity, CancellationToken cancellationToken);
}

/// <summary>
/// Lavozim, yuridik shaxs, yo'nalish: hozir ishlayotgan xodim biriktirilgan bo'lsa — faol emas qilinmaydi.
/// </summary>
/// <typeparam name="TEntity">Xodim biriktiriladigan ma'lumotnoma (<see cref="ReferenceUsageKinds.Of{TEntity}"/>).</typeparam>
public sealed class AssignedEmployeesRule<TEntity> : IReferenceStateRule<TEntity>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceUsageReadModel _usage;

    public AssignedEmployeesRule(IReferenceUsageReadModel usage) => _usage = usage;

    public async Task<Result> BeforeDeactivateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (ReferenceUsageKinds.Of<TEntity>() is not { } kind)
        {
            return Result.Success();
        }

        var count = await _usage.CountAsync(kind, [entity.Id], cancellationToken).ConfigureAwait(false);

        return count > 0 ? CatalogErrors.HasActiveEmployees : Result.Success();
    }

    public Task<Result> BeforeActivateAsync(TEntity entity, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success());
}

/// <summary>
/// Bo'lim: quyi bo'limlari bilan BIRGA faol emas bo'ladi (egasi, 2026-10-31) — butun tarmoqda
/// ishlayotgan xodim qolmagan bo'lsa.
/// </summary>
/// <remarks>
/// Qayta yoqishda faqat o'zi yoqiladi, quyi bo'limlar emas: ularning ba'zisi oldinroq alohida yopilgan
/// bo'lishi mumkin. Ota bo'limi faol emas bo'lim yoqilmaydi — daraxtda «yopiq bo'lim ichida ochiq bo'lim»
/// bo'lmasin.
/// </remarks>
public sealed class DepartmentStateRule : IReferenceStateRule<Department>
{
    private readonly IDepartmentRepository _departments;
    private readonly IReferenceUsageReadModel _usage;

    public DepartmentStateRule(IDepartmentRepository departments, IReferenceUsageReadModel usage)
    {
        _departments = departments;
        _usage = usage;
    }

    public async Task<Result> BeforeDeactivateAsync(Department entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var descendants = await _departments.GetDescendantIdsAsync(entity.Id, cancellationToken).ConfigureAwait(false);

        var count = await _usage
            .CountAsync(ReferenceUsageKind.Department, [entity.Id, .. descendants], cancellationToken)
            .ConfigureAwait(false);

        if (count > 0)
        {
            return CatalogErrors.HasActiveEmployees;
        }

        await DeactivateDescendantsAsync(descendants, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> BeforeActivateAsync(Department entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.ParentId is not { } parentId)
        {
            return Result.Success();
        }

        var parent = await _departments.GetByIdAsync(parentId, cancellationToken).ConfigureAwait(false);

        return parent is { IsActive: false } ? CatalogErrors.ParentDepartmentInactive : Result.Success();
    }

    /// <summary>Avlodlarning faollarini faol emas qiladi — chaqiruvchining saqlashida yoziladi.</summary>
    internal async Task DeactivateDescendantsAsync(
        IReadOnlyList<Guid> descendantIds,
        CancellationToken cancellationToken)
    {
        foreach (var id in descendantIds)
        {
            var child = await _departments.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (child is { IsActive: true })
            {
                child.Deactivate();
            }
        }
    }
}
