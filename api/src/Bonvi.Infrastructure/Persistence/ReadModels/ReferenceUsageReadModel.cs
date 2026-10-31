using Bonvi.Application.Catalog.Usage;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Ma'lumotnoma yozuviga biriktirilgan, hozir ishlayotgan xodimlar (docs/SPEC-malumotnoma-faolsizlantirish.md §A).
/// </summary>
/// <remarks>
/// «Ishlayotgan» — ishdan ketmagan munosabat (sinovdagi yoki doimiy); o'chirilganlar global filtr bilan tushib qoladi.
/// Son — xodimlar bo'yicha (bir odamning ikkita munosabati bitta sanaladi), ro'yxat — munosabatlar bo'yicha.
/// </remarks>
public sealed class ReferenceUsageReadModel : IReferenceUsageReadModel
{
    private readonly AppDbContext _context;

    public ReferenceUsageReadModel(AppDbContext context) => _context = context;

    public async Task<int> CountAsync(
        ReferenceUsageKind kind,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (kind == ReferenceUsageKind.Direction)
        {
            return await DirectionEmployees(ids).CountAsync(cancellationToken).ConfigureAwait(false);
        }

        return await Placements(kind, ids)
            .Select(employment => employment.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReferenceUserDto>> ListAsync(
        ReferenceUsageKind kind,
        IReadOnlyCollection<Guid> ids,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        List<(Guid EmployeeId, Guid? EmploymentId, Guid? DepartmentId, Guid? PositionId)> rows;

        if (kind == ReferenceUsageKind.Direction)
        {
            // Yo'nalish xodimga qo'yiladi — bo'lim va lavozim uning asosiy (bo'lmasa birinchi) ish joyidan
            var people = await DirectionEmployees(ids)
                .Select(employee => employee.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var placements = await Working()
                .Where(employment => people.Contains(employment.EmployeeId))
                .OrderByDescending(employment => employment.IsPrimary)
                .Select(employment => new { employment.EmployeeId, employment.DepartmentId, employment.PositionId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            rows = [.. placements
                .GroupBy(placement => placement.EmployeeId)
                .Select(group => group.First())
                .Select(placement => (placement.EmployeeId, (Guid?)null, placement.DepartmentId, placement.PositionId))];
        }
        else
        {
            var placements = await Placements(kind, ids)
                .Select(employment => new { employment.EmployeeId, employment.Id, employment.DepartmentId, employment.PositionId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            rows = [.. placements.Select(placement =>
                (placement.EmployeeId, (Guid?)placement.Id, placement.DepartmentId, placement.PositionId))];
        }

        if (rows.Count == 0)
        {
            return [];
        }

        var employeeIds = rows.Select(row => row.EmployeeId).Distinct().ToList();
        var names = await _context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
            })
            .ToDictionaryAsync(employee => employee.Id, cancellationToken)
            .ConfigureAwait(false);

        var departmentIds = rows.Where(row => row.DepartmentId is not null).Select(row => row.DepartmentId!.Value).Distinct().ToList();
        var departments = await _context.Departments
            .AsNoTracking()
            .Where(department => departmentIds.Contains(department.Id))
            .Select(department => new { department.Id, Name = department.Path ?? department.Name })
            .ToDictionaryAsync(department => department.Id, department => department.Name, cancellationToken)
            .ConfigureAwait(false);

        var positionIds = rows.Where(row => row.PositionId is not null).Select(row => row.PositionId!.Value).Distinct().ToList();
        var positions = await _context.Positions
            .AsNoTracking()
            .Where(position => positionIds.Contains(position.Id))
            .ToDictionaryAsync(position => position.Id, position => position.Name, cancellationToken)
            .ConfigureAwait(false);

        return [.. rows
            .Where(row => names.ContainsKey(row.EmployeeId))
            .Select(row =>
            {
                var name = names[row.EmployeeId];
                return new ReferenceUserDto(
                    row.EmployeeId,
                    row.EmploymentId,
                    FullName.FromTrusted(name.LastName, name.FirstName, name.MiddleName).Full,
                    row.DepartmentId is { } departmentId ? departments.GetValueOrDefault(departmentId) : null,
                    row.PositionId is { } positionId ? positions.GetValueOrDefault(positionId) : null);
            })
            .OrderBy(row => row.FullName, StringComparer.CurrentCulture)
            .Take(limit)];
    }

    private IQueryable<Employment> Working()
        => _context.Employments
            .AsNoTracking()
            .Where(employment => employment.Status != EmploymentStatus.Terminated);

    private IQueryable<Employment> Placements(ReferenceUsageKind kind, IReadOnlyCollection<Guid> ids)
    {
        var list = ids.ToList();

        return kind switch
        {
            ReferenceUsageKind.Department => Working()
                .Where(employment => employment.DepartmentId != null && list.Contains(employment.DepartmentId.Value)),
            ReferenceUsageKind.Position => Working()
                .Where(employment => employment.PositionId != null && list.Contains(employment.PositionId.Value)),
            ReferenceUsageKind.LegalEntity => Working()
                .Where(employment => list.Contains(employment.LegalEntityId)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private IQueryable<Employee> DirectionEmployees(IReadOnlyCollection<Guid> ids)
    {
        var directionId = ids.First();
        var working = Working().Select(employment => employment.EmployeeId);

        return _context.Employees
            .AsNoTracking()
            .Where(employee => employee.DirectionIds.Contains(directionId) && working.Contains(employee.Id));
    }
}
