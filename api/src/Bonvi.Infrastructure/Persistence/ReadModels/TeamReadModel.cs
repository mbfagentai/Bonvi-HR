using Bonvi.Application.Me;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary><see cref="ITeamReadModel"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// Bitta so'rov, <c>WHERE id = ANY(…)</c> — sahifa hajmidan qat'i nazar (ko'pi bilan 200 ta
/// identifikator). Yumshoq o'chirilgan xodim filtr bilan tushib qoladi — panel ham uni ko'rsatmaydi.
/// </remarks>
internal sealed class TeamReadModel : ITeamReadModel
{
    private readonly AppDbContext _context;

    public TeamReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PhoneNumber>> PhonesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, PhoneNumber>();
        }

        var rows = await _context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id) && employee.Phone != null)
            .Select(employee => new { employee.Id, employee.Phone })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.Id, row => row.Phone!);
    }
}
