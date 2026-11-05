using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

public sealed record ProcessCaseRow(
    Guid Id,
    string ProcessCode,
    Guid TemplateVersionId,
    ProcessCaseStatus Status,
    string? Outcome,
    bool Legacy,
    ProcessSubjectKind SubjectKind,
    Guid? VacancyRequestId,
    Guid? CandidateId,
    Guid? EmploymentId,
    Guid? ProbationId,
    Guid? EmployeeId,
    Guid? DepartmentId,
    Guid? ParentCaseId,
    DateOnly? AnchorDate,
    DateOnly StartedOn,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    int Version);

public sealed record ProcessStepRow(
    Guid CaseId,
    string StepCode,
    int OrderNo,
    string BoardColumn,
    ProcessStepState State,
    ProcessStepResolution? Resolution,
    string? HolderRole,
    Guid? HolderEmployeeId,
    DateOnly? DueDate,
    DateTimeOffset? DoneAt,
    Guid? DoneBy,
    string? Payload,
    string? BlockedGateCode,
    Guid? LinkedBlankSubmissionId,
    Guid? LinkedRecordId)
{
    public bool IsOpen => State is not (ProcessStepState.Done or ProcessStepState.Skipped);

    /// <summary>The step a case is on now; a Locked one is still ahead of it.</summary>
    public bool IsCurrent => State is ProcessStepState.Active or ProcessStepState.Blocked or ProcessStepState.Waiting;
}

/// <summary>A step of a case's child, with the parent it answers for.</summary>
public sealed record ProcessChildStepRow(Guid ParentCaseId, ProcessStepRow Step);

/// <summary>Filters of #3; all optional, null means no filter. Dates are <c>started_on</c>, both ends included.</summary>
public sealed record ProcessCaseFilter(
    string? ProcessCode,
    ProcessCaseStatus? Status,
    Guid? DepartmentId,
    Guid? EmployeeId,
    Guid? CandidateId,
    string? HolderRole,
    bool? Late,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    DateOnly Today,
    int Page,
    int PageSize)
{
    /// <summary>Any of these departments; the MCP tools filter a department with its branch (§12).</summary>
    public IReadOnlyCollection<Guid>? DepartmentIds { get; init; }
}

public sealed record ProcessCasePage(IReadOnlyList<ProcessCaseRow> Items, int Total);

/// <summary>The ids whose display names a response needs.</summary>
public sealed record ProcessNameQuery
{
    public IReadOnlyCollection<Guid> EmployeeIds { get; init; } = [];

    public IReadOnlyCollection<Guid> CandidateIds { get; init; } = [];

    public IReadOnlyCollection<Guid> VacancyRequestIds { get; init; } = [];

    public IReadOnlyCollection<Guid> EmploymentIds { get; init; } = [];

    public IReadOnlyCollection<Guid> DepartmentIds { get; init; } = [];

    public IReadOnlyCollection<Guid> UserIds { get; init; } = [];

    public IReadOnlyCollection<string> BlankCodes { get; init; } = [];
}

/// <remarks>
/// <para><c>Name</c>: Candidate's name.</para>
/// <para><c>VacancyTitle</c>: Title of the candidate's vacancy.</para>
/// </remarks>
public sealed record ProcessCandidateName(string Name, string? VacancyTitle);

/// <summary>Display names of a response; a deleted row is simply absent.</summary>
public sealed record ProcessNames(
    IReadOnlyDictionary<Guid, string> Employees,
    IReadOnlyDictionary<Guid, ProcessCandidateName> Candidates,
    IReadOnlyDictionary<Guid, string> VacancyRequests,
    IReadOnlyDictionary<Guid, string> EmploymentPositions,
    IReadOnlyDictionary<Guid, string> Departments,
    IReadOnlyDictionary<Guid, string> Users,
    IReadOnlyDictionary<string, string> BlankTitles);

/// <summary>
/// The reads of «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §10). Each method is ONE query over a whole batch:
/// a response costs the same number of queries for 1 case and for 50,000. <c>ProcessReadApiTests</c> pins it.
/// </summary>
public interface IProcessReadModel
{
    /// <summary>Open steps of running cases held by a person or by one of the office roles (§7.7).</summary>
    /// <remarks>A step with a resolved person belongs to that person only, whatever its role says.</remarks>
    Task<IReadOnlyList<ProcessStepRow>> ListHeldStepsAsync(
        IReadOnlyCollection<string> officeRoles,
        Guid? employeeId,
        IReadOnlyCollection<ProcessStepState> states,
        CancellationToken cancellationToken = default);

    /// <summary>How many rows <see cref="ListHeldStepsAsync"/> would return.</summary>
    Task<int> CountHeldStepsAsync(
        IReadOnlyCollection<string> officeRoles,
        Guid? employeeId,
        IReadOnlyCollection<ProcessStepState> states,
        CancellationToken cancellationToken = default);

    /// <summary>Open steps of running cases that are Blocked, past <paramref name="today"/>, or one of <paramref name="alsoStepCodes"/>.</summary>
    Task<IReadOnlyList<ProcessStepRow>> ListProblemStepsAsync(
        DateOnly today,
        IReadOnlyCollection<string> alsoStepCodes,
        CancellationToken cancellationToken = default);

    /// <summary>The template versions running cases are on.</summary>
    Task<IReadOnlyList<Guid>> ListActiveVersionIdsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessCaseRow>> ListCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default);

    /// <summary>Every running case of a process, oldest first.</summary>
    Task<IReadOnlyList<ProcessCaseRow>> ListActiveCasesAsync(string processCode, CancellationToken cancellationToken = default);

    /// <summary>A page of #3, newest first.</summary>
    Task<ProcessCasePage> SearchCasesAsync(ProcessCaseFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Every step of the given cases, by case and <c>order_no</c>.</summary>
    Task<IReadOnlyList<ProcessStepRow>> ListStepsOfCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default);

    /// <summary>The step <paramref name="stepCode"/> of the children of the given cases.</summary>
    Task<IReadOnlyList<ProcessChildStepRow>> ListChildStepsAsync(
        IReadOnlyCollection<Guid> parentCaseIds,
        string stepCode,
        CancellationToken cancellationToken = default);

    Task<ProcessNames> LoadNamesAsync(ProcessNameQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessLinkedBlankDto>> ListBlanksAsync(IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default);

    /// <summary>Of the given employments, those whose live dismissal HR-F09 has sections 1–4 all empty (gate AKT).</summary>
    Task<IReadOnlySet<Guid>> ListEmptyHandoverActsAsync(IReadOnlyCollection<Guid> employmentIds, CancellationToken cancellationToken = default);

    /// <summary>§11.8: employees still working (on probation or permanent, as every list means it) and no photo.</summary>
    Task<int> CountActiveEmployeesWithoutPhotoAsync(CancellationToken cancellationToken = default);

    /// <summary>§11.8: employees still working whose primary employment no schedule assignment reaches today.</summary>
    Task<int> CountActiveEmployeesWithoutScheduleAsync(DateOnly today, CancellationToken cancellationToken = default);
}
