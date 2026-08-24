using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Abstractions;

/// <summary>Rollar uchun saqlash porti.</summary>
public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
}
