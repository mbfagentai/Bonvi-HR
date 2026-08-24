using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Organization;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary>
/// <see cref="IDepartmentRepository"/> ning EF Core orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para>
/// Daraxt bilan bog'liq ikkala amal ham <b>rekursiv CTE</b> bilan bajariladi,
/// C# da aylanib chiqish bilan emas. Sabab: 73 ta bo'limni bittalab yuklash
/// N+1 so'rovga aylanadi, butun jadvalni xotiraga olish esa jadval o'sgan
/// sari sekinlashadi. PostgreSQL daraxtni bitta so'rovda, indeks bo'yicha
/// aylanib chiqadi.
/// </para>
/// <para>
/// Ikkala so'rovda ham <c>CYCLE</c> bandi bor. U bo'lmasa, bazada halqa
/// paydo bo'lgan zahoti (A → B → A) so'rov cheksiz aylanib, ulanishni band
/// qilib qo'yardi. Halqa nazariy jihatdan mumkin: <c>Department.MoveTo</c>
/// uni tekshira olmaydi (buning uchun butun daraxt kerak).
/// </para>
/// </remarks>
public sealed class DepartmentRepository : ReferenceRepository<Department>, IDepartmentRepository
{
    /// <summary>
    /// Butun daraxtning <c>level</c> va <c>path</c> ustunlarini bitta
    /// so'rovda qayta hisoblaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ildiz deb faqat <c>parent_id IS NULL</c> emas, otasi yo'q bo'lib
    /// qolgan («yetim») bo'limlar ham olinadi. Aks holda otasi yumshoq
    /// o'chirilgan bo'lim daraxtdan tushib qolib, eski <c>path</c> bilan
    /// abadiy qolib ketardi.
    /// </para>
    /// <para>
    /// <c>IS DISTINCT FROM</c> sharti: qiymati o'zgarmagan qatorlar
    /// yangilanmaydi. Natijada <c>updated_at</c> bejiz o'zgarmaydi va
    /// jadval keraksiz shishmaydi.
    /// </para>
    /// <para>
    /// Yo'l ajratgichi — <c>« / »</c> (ikki tomonida bo'sh joy):
    /// «7-Департамент / Метан завод / Плазма бўлими».
    /// </para>
    /// </remarks>
    private const string RebuildHierarchySql = """
        WITH RECURSIVE daraxt AS (
            SELECT d.id, 0 AS level, d.name::text AS path
            FROM departments d
            WHERE d.deleted_at IS NULL
              AND (d.parent_id IS NULL
                   OR NOT EXISTS (
                       SELECT 1 FROM departments ota
                       WHERE ota.id = d.parent_id AND ota.deleted_at IS NULL))
            UNION ALL
            SELECT bola.id, t.level + 1, t.path || ' / ' || bola.name
            FROM departments bola
            JOIN daraxt t ON bola.parent_id = t.id
            WHERE bola.deleted_at IS NULL
        ) CYCLE id SET halqa USING yol
        UPDATE departments AS d
        SET level = t.level, path = t.path
        FROM daraxt t
        WHERE d.id = t.id
          AND NOT t.halqa
          AND (d.level IS DISTINCT FROM t.level OR d.path IS DISTINCT FROM t.path)
        """;

    public DepartmentRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Department>> GetChildrenAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        // Ikki shoxga ATAYLAB bo'lingan: `d.ParentId == parentId` ifodasida
        // parametr NULL bo'lsa, SQL'da `parent_id = NULL` hosil bo'lardi —
        // bu hech qachon rost emas va ildiz bo'limlar topilmasdi.
        var query = parentId is { } id
            ? Set.Where(department => department.ParentId == id)
            : Set.Where(department => department.ParentId == null);

        return await query
            .AsNoTracking()
            .OrderBy(department => department.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        // `SqlQuery<Guid>` — skalyar natija uchun `FromSql`ning ekvivalenti:
        // rekursiv CTE'ni EF modeliga soxta entity qo'shmasdan bajarishga imkon beradi.
        // Ustun nomi «Value» bo'lishi SHART — EF skalyar so'rovda aynan shuni izlaydi.
        return await Context.Database
            .SqlQuery<Guid>($"""
                WITH RECURSIVE avlodlar AS (
                    SELECT d.id
                    FROM departments d
                    WHERE d.parent_id = {departmentId} AND d.deleted_at IS NULL
                    UNION ALL
                    SELECT bola.id
                    FROM departments bola
                    JOIN avlodlar a ON bola.parent_id = a.id
                    WHERE bola.deleted_at IS NULL
                ) CYCLE id SET halqa USING yol
                SELECT id AS "Value" FROM avlodlar WHERE NOT halqa
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RebuildHierarchyAsync(CancellationToken cancellationToken = default)
    {
        await Context.Database
            .ExecuteSqlRawAsync(RebuildHierarchySql, cancellationToken)
            .ConfigureAwait(false);

        // Xom SQL o'zgarish kuzatuvchisidan chetlab o'tadi: xotiradagi obyektlar
        // eski `Level`/`Path` bilan qolgan bo'lardi va keyingi `SaveChanges`
        // ularni bazaga QAYTA yozib, hisobni bekor qilardi.
        // Shu sababli o'zgartirilmagan yozuvlar bazadan qayta o'qiladi.
        var tracked = Context.ChangeTracker
            .Entries<Department>()
            .Where(entry => entry.State == EntityState.Unchanged)
            .ToList();

        foreach (var entry in tracked)
        {
            await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
