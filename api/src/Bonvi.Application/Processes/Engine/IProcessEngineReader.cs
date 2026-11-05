using Bonvi.Domain.Employees;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// What the engine reads from existing tables besides the facts (§7.3 starter scans, §7.4 p.5 anchors
/// and holders, §7.7). Read-only: each method is one batch, never one query per subject.
/// </summary>
public interface IProcessEngineReader
{
    /// <summary>The subjects of a batch of cases. A soft-deleted subject is absent — that is how the engine sees §6.9.</summary>
    Task<ProcessSubjects> LoadSubjectsAsync(ProcessSubjectQuery query, CancellationToken cancellationToken = default);

    /// <summary>Requests created on or after <paramref name="startedOn"/> with no hire-request case yet.</summary>
    Task<IReadOnlyList<VacancyRequestStart>> ListVacancyRequestStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default);

    /// <summary>Candidates in progress whose current application is on or after <paramref name="startedOn"/>.</summary>
    Task<IReadOnlyList<CandidateStart>> ListCandidateStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default);

    /// <summary>Hire / Rehire events recorded on or after <paramref name="startedOn"/> whose employment has no hire-formalise case.</summary>
    Task<IReadOnlyList<HireEventStart>> ListHireEventStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default);

    /// <summary>Probations in progress starting on or after <paramref name="startedOn"/> whose employment has no probation case.</summary>
    Task<IReadOnlyList<ProbationStart>> ListProbationStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// §3.2 dismissal (b) and (c): employments with a live F08 / dismissal F09 created on or after
    /// <paramref name="startedOn"/>, and terminations dated on or after it. May return an employment
    /// whose only case ended <c>withdrawn</c> — the starter decides (it reopens on a newer start fact).
    /// </summary>
    Task<IReadOnlyList<DismissalStart>> ListDismissalStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default);
}

/// <summary>The subject ids of a batch.</summary>
public sealed record ProcessSubjectQuery
{
    public IReadOnlyCollection<Guid> VacancyRequestIds { get; init; } = [];

    public IReadOnlyCollection<Guid> CandidateIds { get; init; } = [];

    public IReadOnlyCollection<Guid> EmploymentIds { get; init; } = [];

    public IReadOnlyCollection<Guid> ProbationIds { get; init; } = [];

    /// <summary>Employees whose user link matters (§7.7 <c>subjectEmployee</c>).</summary>
    public IReadOnlyCollection<Guid> EmployeeIds { get; init; } = [];
}

public sealed record VacancyRequestRow(Guid Id, Guid DepartmentId, Guid? VacancyId);

/// <param name="Id">Candidate.</param>
/// <param name="DepartmentId">Department of the latest request behind the candidate's vacancy; vacancies carry none themselves.</param>
public sealed record CandidateRow(Guid Id, Guid? DepartmentId);

public sealed record EmploymentRow(
    Guid Id,
    Guid EmployeeId,
    Guid? DepartmentId,
    DateOnly HireDate,
    DateOnly? HireOrderDate,
    DateOnly? TerminationDate,
    EmploymentStatus Status,
    bool EmployeeHasPinfl);

public sealed record ProbationRow(Guid Id, Guid EmploymentId, DateOnly StartDate, DateOnly EndDate);

/// <summary>The rows of a batch, plus who can hold person-role steps (§7.7).</summary>
public sealed class ProcessSubjects
{
    public required IReadOnlyDictionary<Guid, VacancyRequestRow> VacancyRequests { get; init; }

    public required IReadOnlyDictionary<Guid, CandidateRow> Candidates { get; init; }

    public required IReadOnlyDictionary<Guid, EmploymentRow> Employments { get; init; }

    public required IReadOnlyDictionary<Guid, ProbationRow> Probations { get; init; }

    /// <summary>Department → head employee, for every department of the batch's rows.</summary>
    public required IReadOnlyDictionary<Guid, Guid> DepartmentHeads { get; init; }

    /// <summary>Employees (heads and subject employees of the batch) that have an active user.</summary>
    public required IReadOnlySet<Guid> EmployeesWithUser { get; init; }
}

public sealed record VacancyRequestStart(Guid Id, DateTimeOffset CreatedAt, Guid DepartmentId);

/// <param name="Id">Candidate.</param>
/// <param name="AppliedAt"><c>coalesce(reapplied_at, created_at)</c>.</param>
/// <param name="VacancyId">The vacancy; finds the parent hire-request case and the department.</param>
public sealed record CandidateStart(Guid Id, DateTimeOffset AppliedAt, Guid VacancyId);

/// <param name="EmploymentId">The hire's employment.</param>
/// <param name="EmployeeId">The employee.</param>
/// <param name="DepartmentId">Employment department.</param>
/// <param name="HireDate">Becomes the case anchor.</param>
/// <param name="RecordedAt">The event time — the start fact.</param>
/// <param name="CandidateId">The hired candidate, when there was one.</param>
/// <param name="CandidateAppliedAt">Their application; older than the engine's start → a legacy case.</param>
public sealed record HireEventStart(
    Guid EmploymentId,
    Guid EmployeeId,
    Guid? DepartmentId,
    DateOnly HireDate,
    DateTimeOffset RecordedAt,
    Guid? CandidateId,
    DateTimeOffset? CandidateAppliedAt);

public sealed record ProbationStart(Guid Id, Guid EmploymentId, Guid EmployeeId, Guid? DepartmentId, DateOnly StartDate);

/// <param name="EmploymentId">The employment leaving.</param>
/// <param name="EmployeeId">The employee.</param>
/// <param name="DepartmentId">Employment department.</param>
/// <param name="StartedAt">The earliest start fact on or after the engine's start.</param>
/// <param name="StartedOn">Tashkent date of the start fact (for a termination: its date).</param>
/// <param name="InFlight">A live F08 / dismissal F09 older than the engine's start exists: the dismissal was already under way (§7.3).</param>
/// <param name="LatestFactAt">The newest start fact; later than a <c>withdrawn</c> case's end → that case reopens.</param>
public sealed record DismissalStart(
    Guid EmploymentId,
    Guid EmployeeId,
    Guid? DepartmentId,
    DateTimeOffset StartedAt,
    DateOnly StartedOn,
    bool InFlight,
    DateTimeOffset LatestFactAt);

/// <summary>
/// The one global lock of a pass (§7.4): <c>pg_try_advisory_xact_lock</c>, released with the
/// transaction it is taken in. A busy lock is an answer, not an error.
/// </summary>
public interface IProcessEngineLock
{
    Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>Journal of the engine (structured log; nothing is stored).</summary>
public interface IProcessEngineJournal
{
    void PassFinished(int started, int changedRows);

    void PassFailed(Exception exception);

    void CaseProblem(Guid caseId, string processCode, string problem);

    void TemplateMissing(string processCode);

    void TemplateUnreadable(Guid versionId, Exception exception);

    /// <summary>A hook gate could not answer in <c>warn</c> mode and let the operation through (§8.2).</summary>
    void GateFailedOpen(string operation, Guid employeeId, Exception exception);
}
