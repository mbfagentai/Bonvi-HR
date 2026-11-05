using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

public sealed class ProcessTemplateRepository : Repository<ProcessTemplate>, IProcessTemplateRepository
{
    public ProcessTemplateRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<ProcessTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(template => template.Code == code, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessTemplate>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .OrderBy(template => template.Code)
            .ToListAsync(cancellationToken);
}

public sealed class ProcessTemplateVersionRepository : Repository<ProcessTemplateVersion>, IProcessTemplateVersionRepository
{
    public ProcessTemplateVersionRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<ProcessTemplateVersion?> GetPublishedAsync(Guid templateId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            version => version.TemplateId == templateId && version.Status == ProcessTemplateVersionStatus.Published,
            cancellationToken);

    /// <inheritdoc />
    public Task<ProcessTemplateVersion?> GetAsync(Guid templateId, string version, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            row => row.TemplateId == templateId && row.Version == version,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessTemplateVersion>> ListPublishedAsync(CancellationToken cancellationToken = default)
        => await Set
            .Where(version => version.Status == ProcessTemplateVersionStatus.Published)
            .OrderBy(version => version.TemplateId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessTemplateVersion>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return ids.Count == 0
            ? []
            : await Set
                .Where(version => ids.Contains(version.Id))
                .ToListAsync(cancellationToken);
    }
}

/// <remarks>
/// Steps are always loaded with their case: every transition is an invariant of the aggregate.
/// The order lives inside <c>Include</c> (definition order, then the step code for a stable tie-break).
/// </remarks>
public sealed class ProcessCaseRepository : Repository<ProcessCase>, IProcessCaseRepository
{
    public ProcessCaseRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<ProcessCase?> GetWithStepsAsync(Guid id, CancellationToken cancellationToken = default)
        => WithSteps().FirstOrDefaultAsync(processCase => processCase.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessCase?> GetBySubjectRefAsync(string processCode, string subjectRef, CancellationToken cancellationToken = default)
        => WithSteps().FirstOrDefaultAsync(
            processCase => processCase.ProcessCode == processCode && processCase.SubjectRef == subjectRef,
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <c>AsSplitQuery()</c> — one query for the cases and one for all their steps: a JOIN would
    /// repeat every case row once per step, and a per-case load would be N+1.
    /// </remarks>
    public async Task<IReadOnlyList<ProcessCase>> ListActiveWithStepsAsync(CancellationToken cancellationToken = default)
        => await WithSteps()
            .Where(processCase => processCase.Status == ProcessCaseStatus.Active)
            .OrderBy(processCase => processCase.CreatedAt)
            .ThenBy(processCase => processCase.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessCase>> ListBySubjectRefsAsync(
        string processCode,
        IReadOnlyCollection<string> subjectRefs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectRefs);

        return subjectRefs.Count == 0
            ? []
            : await WithSteps()
                .Where(processCase => processCase.ProcessCode == processCode && subjectRefs.Contains(processCase.SubjectRef))
                .OrderBy(processCase => processCase.CreatedAt)
                .ThenBy(processCase => processCase.Id)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessCase>> ListByParentIdsAsync(
        IReadOnlyCollection<Guid> parentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parentIds);

        return parentIds.Count == 0
            ? []
            : await WithSteps()
                .Where(processCase => processCase.ParentCaseId != null && parentIds.Contains(processCase.ParentCaseId.Value))
                .OrderBy(processCase => processCase.CreatedAt)
                .ThenBy(processCase => processCase.Id)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessCase>> ListByEmploymentIdsAsync(
        IReadOnlyCollection<Guid> employmentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employmentIds);

        return employmentIds.Count == 0
            ? []
            : await WithSteps()
                .Where(processCase => processCase.EmploymentId != null && employmentIds.Contains(processCase.EmploymentId.Value))
                .OrderBy(processCase => processCase.CreatedAt)
                .ThenBy(processCase => processCase.Id)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
    }

    private IQueryable<ProcessCase> WithSteps()
        => Set.Include(processCase => processCase.Steps.OrderBy(step => step.OrderNo).ThenBy(step => step.StepCode));
}

public sealed class ProcessGateOverrideRepository : Repository<ProcessGateOverride>, IProcessGateOverrideRepository
{
    public ProcessGateOverrideRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessGateOverride>> ListUsableAsync(
        string gateCode,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(row => row.GateCode == gateCode && row.ConsumedAt == null && row.ExpiresAt > at)
            .OrderByDescending(row => row.GrantedAt)
            .ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessGateOverride>> ListUsableByCaseIdsAsync(
        IReadOnlyCollection<Guid> caseIds,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseIds);

        return caseIds.Count == 0
            ? []
            : await Set
                .Where(row => row.CaseId != null && caseIds.Contains(row.CaseId.Value) && row.ConsumedAt == null && row.ExpiresAt > at)
                .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessGateOverride>> ListGrantedSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(row => row.GrantedAt >= since)
            .OrderByDescending(row => row.GrantedAt)
            .ThenByDescending(row => row.Id)
            .ToListAsync(cancellationToken);
}

public sealed class ProcessStageBindingRepository : Repository<ProcessStageBinding>, IProcessStageBindingRepository
{
    public ProcessStageBindingRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessStageBinding>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .OrderBy(binding => binding.BindingCode)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ProcessStageBinding?> GetByCodeAsync(string bindingCode, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(binding => binding.BindingCode == bindingCode, cancellationToken);
}

public sealed class ProcessHolidayRepository : Repository<ProcessHoliday>, IProcessHolidayRepository
{
    public ProcessHolidayRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<ProcessHoliday?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(holiday => holiday.Date == date, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessHoliday>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => await Set
            .Where(holiday => holiday.Date >= from && holiday.Date <= to)
            .OrderBy(holiday => holiday.Date)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessHoliday>> ListAllAsync(CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .OrderBy(holiday => holiday.Date)
            .ToListAsync(cancellationToken);
}
