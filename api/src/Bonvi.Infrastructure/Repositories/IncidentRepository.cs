using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Safety;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IIncidentRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class IncidentRepository : Repository<Incident>, IIncidentRepository
{
    public IncidentRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Incident>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        IncidentSeverity? severity = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (from is { } start)
        {
            query = query.Where(incident => incident.IncidentDate >= start);
        }

        if (to is { } end)
        {
            query = query.Where(incident => incident.IncidentDate <= end);
        }

        if (severity is { } value)
        {
            query = query.Where(incident => incident.Severity == value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Hodisa to'g'ridan-to'g'ri XODIMGA bog'langan (`EmployeeId`),
            // ya'ni «qayta aloqa» dagi kabi munosabat orqali o'tish kerak
            // emas. Qidiruv qoidasi esa aynan o'sha — xodimlar
            // ro'yxatidagi bilan bir xil (`EmployeeSearchQuery`).
            //
            // Mas'ul shaxs (`ResponsibleEmployeeId`) ATAYLAB qidiruvga
            // kirmaydi: jurnalning savoli — «kim jabrlangan», «kim
            // tekshirgan» emas. Ikkalasiga qo'llansa, komissiya raisi
            // sifatida o'nlab hodisada turgan odam har bir so'rovda
            // chiqib, ro'yxatni ma'nosiz qilardi.
            var employeeIds = EmployeeSearchQuery.MatchingIds(Context, search);

            query = query.Where(incident => employeeIds.Contains(incident.EmployeeId));
        }

        return await query
            .OrderByDescending(incident => incident.IncidentDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Incident>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(incident => incident.EmployeeId == employeeId)
            .OrderByDescending(incident => incident.IncidentDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
