using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IEmployeeDocumentRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class EmployeeDocumentRepository : Repository<EmployeeDocument>, IEmployeeDocumentRepository
{
    public EmployeeDocumentRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<EmployeeDocument>> GetByEmployeeAsync(
        Guid employeeId,
        Guid? documentTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()
            .Where(document => document.EmployeeId == employeeId);

        if (documentTypeId is { } typeId)
        {
            query = query.Where(document => document.DocumentTypeId == typeId);
        }

        return await query
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
