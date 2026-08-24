using Bonvi.Domain.Organization;

namespace Bonvi.Domain.Abstractions;

/// <summary>Lavozimlar uchun saqlash porti.</summary>
public interface IPositionRepository : IReferenceRepository<Position>
{
    /// <summary>Vazifalari bilan birga yuklaydi.</summary>
    Task<Position?> GetWithDutiesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Bo'lim bo'yicha lavozimlar.</summary>
    Task<IReadOnlyList<Position>> GetByDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default);
}
