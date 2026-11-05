namespace Bonvi.Application.Processes.Facts;

/// <summary>
/// Read-only fact catalogue of the process engine (docs/SPEC-mening-ishlarim.md §7.2).
/// </summary>
/// <remarks>
/// <para>
/// One call answers every fact of every subject in the query, with one database query per fact
/// family — the reconciler passes all open cases of a pass at once, a gate passes one subject.
/// Nothing is cached: the answer is the database state at call time (gates evaluate facts
/// synchronously, §8.2).
/// </para>
/// <para>
/// Blank facts read only <c>blank_submissions</c> / <c>blank_submission_steps</c> by template code and
/// subject; they never call blank handlers. A «live» blank is Draft, InApproval or Approved —
/// Cancelled and Rejected ones are ignored; of several live ones the latest created wins.
/// </para>
/// </remarks>
public interface IProcessFactSource
{
    Task<ProcessFactSnapshot> LoadAsync(ProcessFactQuery query, CancellationToken cancellationToken = default);
}

/// <summary>A candidate subject: one application of a candidate (§3.2 <c>c:{id}:{appliedOn}</c>).</summary>
/// <param name="CandidateId">The candidate.</param>
/// <param name="AppliedAt">
/// <c>coalesce(reapplied_at, created_at)</c> of the application the case belongs to. <c>null</c> → the
/// candidate's current application (what a gate wants).
/// </param>
public sealed record CandidateFactSubject(Guid CandidateId, DateTimeOffset? AppliedAt = null);

/// <summary>The subjects to answer facts for, plus the stage bindings (§6.5).</summary>
public sealed record ProcessFactQuery
{
    public IReadOnlyCollection<Guid> VacancyRequestIds { get; init; } = [];

    public IReadOnlyCollection<CandidateFactSubject> Candidates { get; init; } = [];

    public IReadOnlyCollection<Guid> EmploymentIds { get; init; } = [];

    public IReadOnlyCollection<Guid> ProbationIds { get; init; } = [];

    public IReadOnlyCollection<Guid> EmployeeIds { get; init; } = [];

    /// <summary>
    /// <c>binding_code → pipeline_stage_id</c>. An unbound code (missing or <c>null</c>) makes its
    /// <c>cand.stage:&lt;code&gt;</c> fact false.
    /// </summary>
    public IReadOnlyDictionary<string, Guid?> StageBindings { get; init; } = new Dictionary<string, Guid?>();
}

/// <summary>One fact of one subject.</summary>
/// <param name="Holds">Whether the fact is true now.</param>
/// <param name="OccurredAt">When the fact itself happened (becomes the step's <c>done_at</c>).</param>
/// <param name="Value">Fact value where §7.2 defines one (decision, status number, date).</param>
/// <param name="LinkedId">The record that proves the fact (blank submission, form, employment).</param>
/// <param name="LinkedEmployeeId">Only <c>cand.hired</c>: the hired employee.</param>
/// <remarks>
/// <c>LinkedId</c> is what the step closed by this fact links. On a step with a blank it lands in
/// <c>linked_blank_submission_id</c>, a foreign key that fails the whole pass on anything else, so a fact that can
/// close a blank step names a submission or nothing.
/// </remarks>
public sealed record FactValue(
    bool Holds,
    DateTimeOffset? OccurredAt = null,
    string? Value = null,
    Guid? LinkedId = null,
    Guid? LinkedEmployeeId = null)
{
    public static FactValue NotHeld { get; } = new(false);
}

/// <summary>Facts per subject. A fact absent from a subject's map does not hold.</summary>
public sealed class ProcessFactSnapshot
{
    private static readonly IReadOnlyDictionary<string, FactValue> NoFacts = new Dictionary<string, FactValue>();

    public ProcessFactSnapshot(
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> vacancyRequests,
        IReadOnlyDictionary<CandidateFactSubject, IReadOnlyDictionary<string, FactValue>> candidates,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> employments,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> probations,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> employees)
    {
        VacancyRequests = vacancyRequests;
        Candidates = candidates;
        Employments = employments;
        Probations = probations;
        Employees = employees;
    }

    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> VacancyRequests { get; }

    public IReadOnlyDictionary<CandidateFactSubject, IReadOnlyDictionary<string, FactValue>> Candidates { get; }

    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> Employments { get; }

    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> Probations { get; }

    public IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> Employees { get; }

    public FactValue ForVacancyRequest(Guid id, string fact) => Find(VacancyRequests, id, fact);

    public FactValue ForCandidate(CandidateFactSubject subject, string fact) => Find(Candidates, subject, fact);

    public FactValue ForEmployment(Guid id, string fact) => Find(Employments, id, fact);

    public FactValue ForProbation(Guid id, string fact) => Find(Probations, id, fact);

    public FactValue ForEmployee(Guid id, string fact) => Find(Employees, id, fact);

    private static FactValue Find<TKey>(IReadOnlyDictionary<TKey, IReadOnlyDictionary<string, FactValue>> map, TKey key, string fact)
        where TKey : notnull
        => (map.TryGetValue(key, out var facts) ? facts : NoFacts).GetValueOrDefault(fact) ?? FactValue.NotHeld;
}
