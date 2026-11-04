using Bonvi.Domain.Processes;

namespace Bonvi.Domain.Abstractions;

/// <summary>Storage port for process templates (docs/SPEC-mening-ishlarim.md §6.1).</summary>
public interface IProcessTemplateRepository : IRepository<ProcessTemplate>
{
    /// <summary>By process code (<c>hire-request</c>), exact match; <c>null</c> if absent.</summary>
    Task<ProcessTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>All templates, by code.</summary>
    Task<IReadOnlyList<ProcessTemplate>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Storage port for process template versions (§6.1).</summary>
public interface IProcessTemplateVersionRepository : IRepository<ProcessTemplateVersion>
{
    /// <summary>The template's Published version; <c>null</c> if none.</summary>
    Task<ProcessTemplateVersion?> GetPublishedAsync(Guid templateId, CancellationToken cancellationToken = default);

    /// <summary>A specific version (<c>1.0</c>); <c>null</c> if absent.</summary>
    Task<ProcessTemplateVersion?> GetAsync(Guid templateId, string version, CancellationToken cancellationToken = default);

    /// <summary>Every Published version (one per template).</summary>
    Task<IReadOnlyList<ProcessTemplateVersion>> ListPublishedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Storage port for process cases (§6.2–§6.3).</summary>
public interface IProcessCaseRepository : IRepository<ProcessCase>
{
    /// <summary>
    /// The case WITH its steps (ordered by <c>order_no</c>) — every mutating action starts here,
    /// step transitions are invariants of the aggregate.
    /// </summary>
    Task<ProcessCase?> GetWithStepsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The case of a subject (UNIQUE (process_code, subject_ref)), with steps; <c>null</c> if absent.</summary>
    Task<ProcessCase?> GetBySubjectRefAsync(string processCode, string subjectRef, CancellationToken cancellationToken = default);

    /// <summary>Every Active case with its steps — one reconciler pass (split query, no N+1).</summary>
    Task<IReadOnlyList<ProcessCase>> ListActiveWithStepsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Storage port for gate overrides (§6.4).</summary>
public interface IProcessGateOverrideRepository : IRepository<ProcessGateOverride>
{
    /// <summary>Unconsumed, unexpired overrides of a gate at <paramref name="at"/>, newest first.</summary>
    Task<IReadOnlyList<ProcessGateOverride>> ListUsableAsync(string gateCode, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Overrides granted on or after <paramref name="since"/>, newest first («Kim kechikdi»).</summary>
    Task<IReadOnlyList<ProcessGateOverride>> ListGrantedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
}

/// <summary>Storage port for pipeline stage bindings (§6.5).</summary>
public interface IProcessStageBindingRepository : IRepository<ProcessStageBinding>
{
    /// <summary>Every binding row.</summary>
    Task<IReadOnlyList<ProcessStageBinding>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>By binding code; <c>null</c> if the row does not exist yet.</summary>
    Task<ProcessStageBinding?> GetByCodeAsync(string bindingCode, CancellationToken cancellationToken = default);
}

/// <summary>Storage port for the working-day calendar (§6.10).</summary>
public interface IProcessHolidayRepository : IRepository<ProcessHoliday>
{
    /// <summary>The row of a date; <c>null</c> if the date is ordinary.</summary>
    Task<ProcessHoliday?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Rows in <c>[from, to]</c>, by date.</summary>
    Task<IReadOnlyList<ProcessHoliday>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
