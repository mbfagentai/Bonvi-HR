using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IEmploymentRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class EmploymentRepository : Repository<Employment>, IEmploymentRepository
{
    public EmploymentRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// Hodisalar tartibi <c>Include</c> ichida ATAYLAB beriladi: aks holda
    /// PostgreSQL qatorlarni ixtiyoriy tartibda qaytaradi va «mehnat yo'li»
    /// har ochilganda boshqacha ko'rinardi. So'rov
    /// <c>(employment_id, effective_date)</c> indeksiga to'liq tushadi.
    /// </remarks>
    public Task<Employment?> GetWithEventsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(employment => employment.Events.OrderBy(item => item.EffectiveDate))
            .FirstOrDefaultAsync(employment => employment.Id == id, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Eng yangisi birinchi: xodim kartasida joriy ish joyi tepada turishi
    /// kerak, tarix esa pastda.
    /// </remarks>
    public async Task<IReadOnlyList<Employment>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(employment => employment.EmployeeId == employeeId)
            .OrderByDescending(employment => employment.HireDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Shart <see cref="EmploymentEvent.IsActiveDuty"/> bilan AYNAN bir xil —
    /// SQL'ga tarjima qilinishi uchun bu yerda ochib yozilgan. So'rov
    /// <c>(employment_id, effective_date)</c> indeksiga tushadi.
    /// </remarks>
    public async Task<IReadOnlyList<EmploymentEvent>> GetActiveDutiesAsync(
        IReadOnlyCollection<Guid> employmentIds,
        DateOnly day,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employmentIds);

        if (employmentIds.Count == 0)
        {
            return [];
        }

        return await Context.Set<EmploymentEvent>()
            .AsNoTracking()
            .Where(item =>
                employmentIds.Contains(item.EmploymentId) &&
                item.Type == EmploymentEventType.AdditionalDuty &&
                item.EffectiveDate <= day &&
                (item.DutyEndDate == null || item.DutyEndDate > day))
            .OrderBy(item => item.EffectiveDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// «Faol» — hali yakunlanmagan, ya'ni sinovda yoki doimiy. Holat bo'yicha
    /// tekshiriladi, sana bo'yicha emas: ishdan bo'shatish sanasi kelajakka
    /// qo'yilgan bo'lishi mumkin (buyruq oldindan chiqadi), lekin xodim o'sha
    /// kungacha hali ishlab turadi.
    /// </remarks>
    public async Task<IReadOnlyList<Employment>> GetActiveByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(employment =>
                employment.EmployeeId == employeeId &&
                employment.Status != EmploymentStatus.Terminated)
            .OrderByDescending(employment => employment.IsPrimary)
            .ThenByDescending(employment => employment.HireDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// So'rov ishga qabul HODISASI orqali ketadi: bog'lanish aynan o'sha
    /// yerda saqlanadi (sababi <see cref="EmploymentEvent.CandidateId"/>
    /// izohida). <c>candidate_id</c> ustidagi qisman indeks tufayli bu
    /// arzon qidiruv.
    /// </para>
    /// <para>
    /// Hodisa turi ham tekshiriladi: kelajakda boshqa turdagi hodisaga
    /// nomzod yozilib qolsa (masalan qayta qabul), «bu nomzod ishga
    /// olinganmi?» degan savolga faqat QABUL hodisasi javob berishi kerak.
    /// </para>
    /// </remarks>
    public Task<Employment?> FindByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .Where(employment => Context.EmploymentEvents.Any(item =>
                item.EmploymentId == employment.Id &&
                item.CandidateId == candidateId &&
                item.Type == EmploymentEventType.Hire))
            .OrderByDescending(employment => employment.HireDate)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Yig'indi BAZADA hisoblanadi (<c>SUM(rate)</c>), C# da emas. Sabab:
    /// bu tekshiruv har ishga qabul qilishda bajariladi va xodimning butun
    /// munosabatlar ro'yxatini tarmoq orqali tortib kelish ortiqcha —
    /// kerak bo'lgani bitta son. Yozuv topilmasa PostgreSQL <c>NULL</c>
    /// qaytaradi, EF esa uni 0 ga aylantiradi.
    /// </remarks>
    public async Task<decimal> GetTotalActiveRateAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(employment =>
                employment.EmployeeId == employeeId &&
                employment.Status != EmploymentStatus.Terminated)
            .SumAsync(employment => employment.Rate, cancellationToken)
            .ConfigureAwait(false);
}
