using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Feedback;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IExitInterviewRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class ExitInterviewRepository : Repository<ExitInterview>, IExitInterviewRepository
{
    public ExitInterviewRepository(AppDbContext context) : base(context) { }

    public Task<ExitInterview?> GetByEmploymentAsync(Guid employmentId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(interview => interview.EmploymentId == employmentId, cancellationToken);

    public async Task<IReadOnlyList<ExitInterview>> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        // Ikki qadam, `ListAsync` dagi qidiruv bilan bir xil naqsh — faqat
        // bu yerda xodim MATN bo'yicha emas, `id` bo'yicha aniq olinadi.
        // Ichki so'rov ustkiga `employment_id IN (…)` bo'lib qo'shiladi:
        // tarmoqdan oraliq ro'yxat o'tmaydi.
        var employmentIds = Context.Employments
            .AsNoTracking()
            .Where(employment => employment.EmployeeId == employeeId)
            .Select(employment => employment.Id);

        return await Set
            .AsNoTracking()
            .Where(interview => employmentIds.Contains(interview.EmploymentId))
            .OrderByDescending(interview => interview.InterviewDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExitInterview>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (from is { } start)
        {
            query = query.Where(interview => interview.InterviewDate >= start);
        }

        if (to is { } end)
        {
            query = query.Where(interview => interview.InterviewDate <= end);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Suhbat MUNOSABATGA bog'langan, xodimga emas (sababi
            // `ExitInterview` izohida: bitta shaxsning bir nechta ketishi
            // bo'lishi mumkin). Shu sababli qidiruv ikki qadamda:
            // xodim → uning munosabatlari → shu munosabatlardagi suhbatlar.
            //
            // Ikkala ichki so'rov ham SERVERDA bajariladi va ustki so'rovga
            // `employment_id IN (…)` bo'lib qo'shiladi — tarmoqdan birorta
            // ham oraliq ro'yxat o'tmaydi. Qidiruv qoidasi xodimlar
            // ro'yxatidagi bilan AYNAN bir xil: sabab `EmployeeSearchQuery`
            // izohida.
            var employeeIds = EmployeeSearchQuery.MatchingIds(Context, search);

            var employmentIds = Context.Employments
                .AsNoTracking()
                .Where(employment => employeeIds.Contains(employment.EmployeeId))
                .Select(employment => employment.Id);

            query = query.Where(interview => employmentIds.Contains(interview.EmploymentId));
        }

        return await query
            .OrderByDescending(interview => interview.InterviewDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
