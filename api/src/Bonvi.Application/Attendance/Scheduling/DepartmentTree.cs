using Bonvi.Domain.Organization;

namespace Bonvi.Application.Attendance.Scheduling;

/// <summary>
/// Bo'limlar daraxti — jadval hal qilishda «eng yaqin ota-bo'lim» yurishi uchun.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Xotirada, bitta yuklashda.</b> Daraxt ~73 tugun; qayta hisoblash
/// uni to'plam boshida bir marta o'qiydi va har bir (xodim, sana) uchun
/// shu lug'at bo'ylab yuradi (SPEC §5.2). Har bir kun uchun bazaga
/// rekursiv so'rov yuborish 540 × 31 marta aynan bir xil javobni
/// qaytarardi.
/// </para>
/// <para>
/// ⚠️ <b><see cref="Department.Path"/> ishlatilmaydi.</b> U NOMLAR
/// yo'li («7-Департамент / Метан завод»), identifikatorlar emas: ikki
/// joyda bir xil nomli bo'lim bo'lsa yoki nom o'zgarsa, nom bo'yicha
/// moslash noto'g'ri bo'limning jadvalini berardi.
/// </para>
/// </remarks>
public sealed class DepartmentTree
{
    private readonly Dictionary<Guid, Guid?> _parents;

    /// <summary>Bo'limlar ro'yxatidan daraxt quradi.</summary>
    /// <param name="departments">Barcha bo'limlar (ota-bo'limlari bilan).</param>
    public DepartmentTree(IEnumerable<Department> departments)
    {
        ArgumentNullException.ThrowIfNull(departments);

        _parents = departments.ToDictionary(department => department.Id, department => department.ParentId);
    }

    /// <summary>Bo'limning o'zi va uning ota-bo'limlari — ENG YAQINI birinchi.</summary>
    /// <remarks>
    /// <para>
    /// Halqaga chidamli: <see cref="Department.MoveTo"/> halqani faqat
    /// use-case qatlamida tekshiradi, Excel'dan ko'chirilgan ma'lumot esa
    /// uni chetlab o'tishi mumkin. Himoyasiz yurish bunday daraxtda
    /// cheksiz aylanib, tungi qayta hisoblashni jimgina osib qo'yardi.
    /// </para>
    /// <para>
    /// Daraxtda yo'q bo'lim (masalan o'chirilgan) uchun faqat o'zi
    /// qaytadi: uning o'z biriktirmasi hali ham mos kelishi mumkin, lekin
    /// yuqoriga chiqishning yo'li yo'q.
    /// </para>
    /// </remarks>
    /// <param name="departmentId">Boshlang'ich bo'lim.</param>
    public IReadOnlyList<Guid> SelfAndAncestors(Guid departmentId)
    {
        var chain = new List<Guid>();
        var visited = new HashSet<Guid>();
        Guid? current = departmentId;

        while (current is { } id && visited.Add(id))
        {
            chain.Add(id);
            current = _parents.GetValueOrDefault(id);
        }

        return chain;
    }
}
