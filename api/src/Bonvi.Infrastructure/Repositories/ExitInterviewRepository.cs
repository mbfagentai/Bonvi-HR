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

    public async Task<IReadOnlyList<ExitInterview>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
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

        return await query
            .OrderByDescending(interview => interview.InterviewDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
