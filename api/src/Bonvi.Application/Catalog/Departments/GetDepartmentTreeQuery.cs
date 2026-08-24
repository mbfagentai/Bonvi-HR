using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Departments;

/// <summary>Bo'limlar daraxtini so'rash.</summary>
/// <param name="OnlyActive">Faqat faol bo'limlar ko'rsatilsinmi.</param>
public sealed record GetDepartmentTreeQuery(bool OnlyActive = false);

/// <summary>
/// Butun bo'limlar daraxtini bitta javobda qaytaradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega xotirada quriladi.</b> Rekursiv SQL (<c>WITH RECURSIVE</c>) bu yerda
/// ortiqcha: bo'limlar soni yuzta atrofida (Excel'da 73 ta edi), ya'ni butun
/// jadval bitta oddiy so'rovda o'qiladi. Xotirada daraxt qurish esa
/// ma'lumotlar bazasiga bog'liq bo'lmagan, testlanadigan sof mantiq.
/// </para>
/// <para>
/// <b>Yetim tugunlar.</b> <c>onlyActive=true</c> bo'lganda faol bo'lim
/// faolsizlantirilgan bo'lim ichida qolib ketishi mumkin. Bunday tugun
/// YO'QOLMAYDI — u ildiz sifatida ko'rsatiladi. Aks holda foydalanuvchi
/// mavjud bo'limni umuman ko'ra olmay qolardi.
/// </para>
/// </remarks>
public sealed class GetDepartmentTreeQueryHandler
{
    private readonly IDepartmentRepository _departments;

    public GetDepartmentTreeQueryHandler(IDepartmentRepository departments) => _departments = departments;

    public async Task<Result<IReadOnlyList<DepartmentTreeNodeDto>>> HandleAsync(
        GetDepartmentTreeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await _departments.ListAsync(query.OnlyActive, cancellationToken).ConfigureAwait(false);

        var byParent = all
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name, StringComparer.CurrentCulture)
            .ToLookup(d => d.ParentId);

        var known = all.Select(d => d.Id).ToHashSet();

        var roots = all
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name, StringComparer.CurrentCulture)
            .Where(d => d.ParentId is not { } parentId || !known.Contains(parentId));

        // Ko'rilgan tugunlar ro'yxati — halqadan himoya. Ma'lumotlarda halqa
        // bo'lmasligi kerak (ko'chirish amali tekshiradi), lekin agar u
        // qaysidir yo'l bilan paydo bo'lsa, bu kod cheksiz rekursiyaga
        // tushib butun servisni yiqitib yubormasligi kerak.
        var visited = new HashSet<Guid>();

        IReadOnlyList<DepartmentTreeNodeDto> tree =
        [
            .. roots.Select(root => BuildNode(root, byParent, visited)),
        ];

        return Result.Success(tree);
    }

    private static DepartmentTreeNodeDto BuildNode(
        Department department,
        ILookup<Guid?, Department> byParent,
        HashSet<Guid> visited)
    {
        IReadOnlyList<DepartmentTreeNodeDto> children = visited.Add(department.Id)
            ? [.. byParent[department.Id].Select(child => BuildNode(child, byParent, visited))]
            : [];

        return new DepartmentTreeNodeDto(
            department.Id,
            department.Name,
            department.Code,
            department.SortOrder,
            department.IsActive,
            department.ParentId,
            department.Level,
            department.Path,
            children);
    }
}
