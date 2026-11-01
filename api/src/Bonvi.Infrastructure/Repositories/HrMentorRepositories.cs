using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Recruiting;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <inheritdoc cref="IHrBlankRepository"/>
public sealed class HrBlankRepository : Repository<HrBlank>, IHrBlankRepository
{
    public HrBlankRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<HrBlank>> ListTrackedAsync(CancellationToken cancellationToken = default)
        => await Set
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<HrBlank>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .OrderBy(blank => blank.Code == string.Empty ? 1 : 0)
            .ThenBy(blank => blank.Code)
            .ThenBy(blank => blank.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <inheritdoc cref="IFormTextSnapshotRepository"/>
public sealed class FormTextSnapshotRepository : Repository<FormTextSnapshot>, IFormTextSnapshotRepository
{
    public FormTextSnapshotRepository(AppDbContext context) : base(context) { }

    public Task<FormTextSnapshot?> LatestAsync(string formCode, CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .Where(snapshot => snapshot.FormCode == formCode)
            .OrderByDescending(snapshot => snapshot.Revision)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<FormTextSnapshot?> FindAsync(string formCode, int revision, CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .FirstOrDefaultAsync(
                snapshot => snapshot.FormCode == formCode && snapshot.Revision == revision,
                cancellationToken);
}

/// <inheritdoc cref="IFormDefinitionRepository"/>
public sealed class FormDefinitionRepository : Repository<FormDefinitionSnapshot>, IFormDefinitionRepository
{
    public FormDefinitionRepository(AppDbContext context) : base(context) { }

    public Task<FormDefinitionSnapshot?> LatestAsync(string formCode, CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .Where(snapshot => snapshot.FormCode == formCode)
            .OrderByDescending(snapshot => snapshot.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<FormDefinitionSnapshot?> FindAsync(string formCode, int version, CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .FirstOrDefaultAsync(
                snapshot => snapshot.FormCode == formCode && snapshot.Version == version,
                cancellationToken);
}
