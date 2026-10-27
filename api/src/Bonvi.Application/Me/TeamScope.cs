using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Me;

/// <summary>
/// Bo'lim boshlig'ining qamrovi — u boshqaradigan bo'limlar daraxtlarining birlashmasi (SPEC-jamoam §B).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qamrov SERVERDA, bazadan va HAR SO'ROVDA hisoblanadi.</b> Mijoz «qaysi bo'limlarni
/// ko'rishim mumkin» degan narsani hech qachon yubormaydi; boshliq almashtirilsa yoki olib
/// tashlansa, keyingi so'rovning o'zida kuchga kiradi — tokenda hech narsa saqlanmaydi.
/// </para>
/// <para>
/// <b>Daraxt — <c>BranchIdsAsync</c> ma'nosida:</b> bo'limning o'zi va uning o'chirilmagan
/// barcha avlodlari, faolsizlari bilan birga. O'chirilgan oraliq bo'lim zanjirni uzadi —
/// rekursiv CTE ham aynan shunday qiladi (u faqat o'chirilmagan bolalarga tushadi). Shu
/// sababli bu yerdagi to'plam va panelga beriladigan ildizlardan o'qish modeli yig'adigan
/// to'plam bir xil bo'ladi.
/// </para>
/// <para>
/// Hisob XOTIRADA, bitta ro'yxatdan: bo'limlar yuztacha (prod'da 105), ya'ni har so'rovda
/// butun jadvalni o'qish rekursiv so'rovlardan arzon va mantiq bazasiz testlanadi.
/// </para>
/// </remarks>
public sealed class TeamScope
{
    private readonly HashSet<Guid> _departmentIds;

    private TeamScope(IReadOnlyList<Guid> roots, IReadOnlyList<Department> departments)
    {
        Roots = roots;
        Departments = departments;
        _departmentIds = [.. departments.Select(department => department.Id)];
    }

    /// <summary>Hech narsa boshqarmaydigan odamning qamrovi.</summary>
    public static TeamScope Empty { get; } = new([], []);

    /// <summary>
    /// Daraxtlarning ildizlari — boshliq boshqaradigan va boshqa boshqaradigan bo'limi ICHIDA
    /// bo'lmagan bo'limlar. Panelga aynan shular beriladi (<c>DashboardFilter.DepartmentRoots</c>).
    /// </summary>
    public IReadOnlyList<Guid> Roots { get; }

    /// <summary>Qamrovdagi barcha bo'limlar — daraxt tartibida, tekislangan.</summary>
    /// <remarks>Ildizdan keyin uning bolalari (chuqurlik bo'yicha), aka-ukalar ro'yxatdagi tartibda.</remarks>
    public IReadOnlyList<Department> Departments { get; }

    /// <summary>Kamida bitta bo'limning boshlig'imi.</summary>
    public bool IsHead => Roots.Count > 0;

    /// <summary>Bo'lim qamrovdami.</summary>
    /// <param name="departmentId">Bo'lim.</param>
    public bool Contains(Guid departmentId) => _departmentIds.Contains(departmentId);

    /// <summary>
    /// Xodim qamrovdami: bo'limi qamrovda bo'lgan va davrga TEGADIGAN munosabati bormi.
    /// </summary>
    /// <remarks>
    /// Panelning bo'lim filtri bilan bir xil qoida (§B): munosabat asosiy bo'lishi shart emas,
    /// ishdan ketish kuni ham qamraladi (<c>PrimaryEmployment.Covers</c>).
    /// </remarks>
    /// <param name="employments">Bitta xodimning munosabatlari.</param>
    /// <param name="from">Davr boshi (kiradi).</param>
    /// <param name="to">Davr oxiri (kiradi).</param>
    public bool Covers(IEnumerable<Employment> employments, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(employments);

        return employments.Any(employment =>
            employment.DeletedAt is null &&
            employment.DepartmentId is { } departmentId &&
            Contains(departmentId) &&
            employment.HireDate <= to &&
            (employment.TerminationDate is not { } terminated || terminated >= from));
    }

    /// <summary>Xodimning qamrovini bo'limlar ro'yxatidan yig'adi.</summary>
    /// <param name="employeeId">Boshliq bo'lishi mumkin bo'lgan xodim.</param>
    /// <param name="departments">
    /// O'chirilmagan BARCHA bo'limlar (faolsizlari bilan) — ro'yxatdagi tartib aka-ukalar tartibi bo'ladi.
    /// </param>
    public static TeamScope Of(Guid employeeId, IReadOnlyList<Department> departments)
    {
        ArgumentNullException.ThrowIfNull(departments);

        var headed = departments.Where(department => department.HeadEmployeeId == employeeId).ToList();
        if (headed.Count == 0)
        {
            return Empty;
        }

        var byId = departments.ToDictionary(department => department.Id);
        var children = departments.ToLookup(department => department.ParentId);
        var headedIds = headed.Select(department => department.Id).ToHashSet();

        // Ildiz — ajdodlari orasida boshqa boshqaradigan bo'limi YO'Q bo'lim: aks holda u
        // allaqachon o'sha daraxtning ichida va ikkinchi marta sanalmasligi kerak.
        var roots = headed.Where(department => !HasHeadedAncestor(department, byId, headedIds)).ToList();

        // Faqat buzilgan daraxtda (ikki boshqaradigan bo'lim bir-birining otasi — halqa) hech
        // biri ildiz bo'lmay qoladi. Bunday holatda boshliq huquqini yo'qotmasin.
        if (roots.Count == 0)
        {
            roots = headed;
        }

        var ordered = new List<Department>();
        var visited = new HashSet<Guid>();

        foreach (var root in roots)
        {
            Collect(root, children, visited, ordered);
        }

        return new TeamScope([.. roots.Select(root => root.Id)], ordered);
    }

    /// <summary>Bo'lim va uning avlodlari — chuqurlik bo'yicha, halqadan himoyalangan.</summary>
    /// <remarks>
    /// <c>visited</c> — halqa (A → B → A) cheksiz aylanishga aylanmasin; ma'lumotda halqa
    /// bo'lmasligi kerak (ko'chirish amali tekshiradi), lekin <c>GetDepartmentTreeQuery</c>
    /// dagi kabi himoya baribir turadi.
    /// </remarks>
    private static void Collect(
        Department department,
        ILookup<Guid?, Department> children,
        HashSet<Guid> visited,
        List<Department> ordered)
    {
        if (!visited.Add(department.Id))
        {
            return;
        }

        ordered.Add(department);

        foreach (var child in children[department.Id])
        {
            Collect(child, children, visited, ordered);
        }
    }

    private static bool HasHeadedAncestor(
        Department department,
        Dictionary<Guid, Department> byId,
        HashSet<Guid> headedIds)
    {
        var seen = new HashSet<Guid> { department.Id };
        var parentId = department.ParentId;

        // Otasi o'chirilgan (ro'yxatda yo'q) bo'lsa zanjir shu yerda uziladi — CTE bilan bir xil.
        while (parentId is { } id && byId.TryGetValue(id, out var parent) && seen.Add(id))
        {
            if (headedIds.Contains(id))
            {
                return true;
            }

            parentId = parent.ParentId;
        }

        return false;
    }
}
