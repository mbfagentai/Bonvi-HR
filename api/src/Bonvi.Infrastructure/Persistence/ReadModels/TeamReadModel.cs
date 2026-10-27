using Bonvi.Application.Me;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary><see cref="ITeamReadModel"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// Telefonlar — bitta so'rov, <c>WHERE id = ANY(…)</c> — sahifa hajmidan qat'i nazar (ko'pi bilan
/// 200 ta identifikator). Yumshoq o'chirilgan xodim filtr bilan tushib qoladi — panel ham uni
/// ko'rsatmaydi.
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

    /// <inheritdoc />
    /// <remarks>
    /// <c>IgnoreQueryFilters</c> — panel qatoridagi nomlar (<c>AttendanceReadModel.CatalogNamesAsync</c>)
    /// bilan bir xil: o'chirilgan lavozimda ishlagan oy nomsiz qolmasin.
    /// </remarks>
    public async Task<TeamPlacementNames> PlacementNamesAsync(
        Guid? departmentId,
        Guid? positionId,
        CancellationToken cancellationToken = default)
    {
        var department = departmentId is { } departmentKey
            ? await _context.Departments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(row => row.Id == departmentKey)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        var position = positionId is { } positionKey
            ? await _context.Positions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(row => row.Id == positionKey)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        return new TeamPlacementNames(department, position);
    }
}
