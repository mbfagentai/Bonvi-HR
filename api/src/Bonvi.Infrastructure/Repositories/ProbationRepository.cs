using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IProbationRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class ProbationRepository : Repository<Probation>, IProbationRepository
{
    public ProbationRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<Probation?> GetByEmploymentAsync(
        Guid employmentId,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            probation => probation.EmploymentId == employmentId,
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Chegara sanasi so'rovdan TASHQARIDA hisoblanadi. Sabab:
    /// <c>today.AddDays(n)</c> ifodasi so'rov ichida qolsa, PostgreSQL uni har
    /// qatorda qayta hisoblardi va indeks o'rniga to'liq skanerlash bo'lardi.
    /// Tayyor qiymat bilan esa shart <c>(status, end_date)</c> indeksiga
    /// aynan tushadi.
    /// </para>
    /// <para>
    /// Muddati ALLAQACHON o'tib ketganlar ham qaytariladi (pastki chegara
    /// yo'q). Ular — HR o'tkazib yuborgan holatlar va aynan shular birinchi
    /// navbatda ko'rinishi kerak; shu sababli saralash tugash sanasi bo'yicha
    /// o'sish tartibida.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<Probation>> GetEndingSoonAsync(
        DateOnly today,
        int withinDays,
        CancellationToken cancellationToken = default)
    {
        var chegara = today.AddDays(withinDays);

        return await Set
            .AsNoTracking()
            .Where(probation =>
                probation.Status == ProbationStatus.InProgress &&
                probation.EndDate <= chegara)
            .OrderBy(probation => probation.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
