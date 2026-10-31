using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Usage;

/// <summary>
/// Ma'lumotnoma turlari ichida xodim BIRIKTIRILADIGANLARI (docs/SPEC-malumotnoma-faolsizlantirish.md §A).
/// </summary>
/// <remarks>
/// Qolgan ma'lumotnomalar (ishdan ketish sababi, rag'bat/jarima, hujjat, yo'qlik turi, qayerdan bilgan)
/// faqat o'tmish yozuvlarida turadi — ularni faol emas qilish hech kimning ishiga tegmaydi.
/// </remarks>
public enum ReferenceUsageKind
{
    /// <summary>Mehnat munosabatining bo'limi (quyi bo'limlari bilan).</summary>
    Department = 1,

    /// <summary>Mehnat munosabatining lavozimi.</summary>
    Position = 2,

    /// <summary>Mehnat munosabatining yuridik shaxsi.</summary>
    LegalEntity = 3,

    /// <summary>Xodimga biriktirilgan yo'nalish.</summary>
    Direction = 4,
}

/// <summary>Ma'lumotnoma entity'si → u bog'lanadigan xodimlar turi.</summary>
public static class ReferenceUsageKinds
{
    /// <summary><c>null</c> — bu ma'lumotnomaga xodim biriktirilmaydi.</summary>
    public static ReferenceUsageKind? Of<TEntity>()
        where TEntity : ReferenceEntity
        => typeof(TEntity) switch
        {
            var type when type == typeof(Department) => ReferenceUsageKind.Department,
            var type when type == typeof(Position) => ReferenceUsageKind.Position,
            var type when type == typeof(LegalEntity) => ReferenceUsageKind.LegalEntity,
            var type when type == typeof(Direction) => ReferenceUsageKind.Direction,
            _ => null,
        };

    /// <summary>
    /// Xodimlarni oynaning o'zida boshqasiga o'tkazish mumkinmi (egasi, 2026-10-31).
    /// </summary>
    /// <remarks>
    /// Yuridik shaxs — yo'q: uni almashtirish yangi mehnat shartnomasi, oddiy o'tkazish emas.
    /// </remarks>
    public static bool CanReassign(ReferenceUsageKind kind) => kind != ReferenceUsageKind.LegalEntity;
}

/// <summary>Ma'lumotnoma yozuviga biriktirilgan, hozir ishlayotgan xodim.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="EmploymentId">Mehnat munosabati; yo'nalishda — <c>null</c> (yo'nalish xodimga biriktiriladi).</param>
/// <param name="FullName">F.I.Sh.</param>
/// <param name="DepartmentName">Hozirgi bo'limi.</param>
/// <param name="PositionName">Hozirgi lavozimi.</param>
public sealed record ReferenceUserDto(
    Guid EmployeeId,
    Guid? EmploymentId,
    string FullName,
    string? DepartmentName,
    string? PositionName);

/// <summary>Yozuvni faol emas qilishdan oldin — unda kim qolgani.</summary>
/// <param name="ActiveEmployees">Hozir ishlayotgan biriktirilgan xodimlar soni.</param>
/// <param name="Employees">Ular (ko'pi bilan <see cref="GetReferenceUsageQueryHandler{TEntity}.ListLimit"/>); ruxsat bo'lmasa bo'sh.</param>
/// <param name="EmployeesHidden"><c>true</c> — ismlar <c>employees.view</c> ruxsatisiz ko'rsatilmadi.</param>
/// <param name="ActiveSubdepartments">Bo'lim uchun: birga faol emas bo'ladigan faol quyi bo'limlar soni.</param>
/// <param name="CanReassign">Xodimlarni shu oynada boshqasiga o'tkazish mumkinmi.</param>
public sealed record ReferenceUsageDto(
    int ActiveEmployees,
    IReadOnlyList<ReferenceUserDto> Employees,
    bool EmployeesHidden,
    int ActiveSubdepartments,
    bool CanReassign);

/// <summary>Biriktirilgan xodimlarni o'qish porti (Infrastructure'da).</summary>
public interface IReferenceUsageReadModel
{
    /// <summary>Hozir ishlayotgan biriktirilgan xodimlar soni.</summary>
    /// <param name="kind">Ma'lumotnoma turi.</param>
    /// <param name="ids">Yozuvlar (bo'limda — o'zi va avlodlari).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<int> CountAsync(
        ReferenceUsageKind kind,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Ular — familiya bo'yicha, ko'pi bilan <paramref name="limit"/> ta.</summary>
    Task<IReadOnlyList<ReferenceUserDto>> ListAsync(
        ReferenceUsageKind kind,
        IReadOnlyCollection<Guid> ids,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Faol emas qilishdan oldingi tekshiruv.</summary>
/// <param name="Id">Yozuv.</param>
public sealed record GetReferenceUsageQuery(Guid Id);

/// <summary>
/// Yozuvga kim biriktirilgan — faol emas qilish oynasi uchun.
/// </summary>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
public sealed class GetReferenceUsageQueryHandler<TEntity>
    where TEntity : ReferenceEntity
{
    /// <summary>Oynada ko'rsatiladigan eng ko'p ism — qolgani son bilan aytiladi.</summary>
    public const int ListLimit = 500;

    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IDepartmentRepository _departments;
    private readonly IReferenceUsageReadModel _usage;
    private readonly ICurrentUser _currentUser;

    public GetReferenceUsageQueryHandler(
        IReferenceRepository<TEntity> repository,
        IDepartmentRepository departments,
        IReferenceUsageReadModel usage,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _departments = departments;
        _usage = usage;
        _currentUser = currentUser;
    }

    public async Task<Result<ReferenceUsageDto>> HandleAsync(
        GetReferenceUsageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _repository.ExistsAsync(query.Id, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.NotFound;
        }

        if (ReferenceUsageKinds.Of<TEntity>() is not { } kind)
        {
            return new ReferenceUsageDto(0, [], false, 0, false);
        }

        IReadOnlyCollection<Guid> ids = [query.Id];
        var activeSubdepartments = 0;

        if (kind == ReferenceUsageKind.Department)
        {
            var descendants = await _departments.GetDescendantIdsAsync(query.Id, cancellationToken).ConfigureAwait(false);
            ids = [query.Id, .. descendants];

            if (descendants.Count > 0)
            {
                var active = await _departments.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);
                var set = descendants.ToHashSet();
                activeSubdepartments = active.Count(department => set.Contains(department.Id));
            }
        }

        var count = await _usage.CountAsync(kind, ids, cancellationToken).ConfigureAwait(false);

        // Ism — xodim ma'lumoti: ma'lumotnoma yurituvchida xodimlarni ko'rish huquqi bo'lmasa, faqat son
        var canSeeNames = _currentUser.HasPermission(Permissions.EmployeesView);
        var employees = count > 0 && canSeeNames
            ? await _usage.ListAsync(kind, ids, ListLimit, cancellationToken).ConfigureAwait(false)
            : [];

        return new ReferenceUsageDto(
            count,
            employees,
            count > 0 && !canSeeNames,
            activeSubdepartments,
            ReferenceUsageKinds.CanReassign(kind));
    }
}
