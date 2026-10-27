using Bonvi.Domain.Organization;

namespace Bonvi.Domain.Abstractions;

/// <summary>Bo'limlar uchun saqlash porti — daraxt bilan ishlash qo'shimchalari.</summary>
public interface IDepartmentRepository : IReferenceRepository<Department>
{
    /// <summary>Bevosita ostidagi bo'limlar.</summary>
    Task<IReadOnlyList<Department>> GetChildrenAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Barcha avlodlar (rekursiv).
    /// Bo'limni ko'chirishda halqa hosil bo'lmasligini tekshirish uchun.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Butun daraxtning <c>Level</c> va <c>Path</c> maydonlarini qayta hisoblaydi.
    /// Bo'lim ko'chirilgandan yoki nomi o'zgargandan keyin chaqiriladi.
    /// </summary>
    Task RebuildHierarchyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Xodim boshlig'i bo'lgan bo'limlar — KUZATUVDA: o'zgarishi chaqiruvchining ish birligida saqlanadi.
    /// </summary>
    /// <remarks>
    /// Butunlay ishdan ketgan xodim boshliqlikdan bo'shatilishi uchun
    /// (<c>TerminateEmploymentCommandHandler</c>). O'chirilgan bo'limlar kirmaydi.
    /// </remarks>
    Task<IReadOnlyList<Department>> GetHeadedByAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);
}
