using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Processes;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Processes.Facts;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>Reads of «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §10); one query per method (see the port).</summary>
public sealed class ProcessReadModel : IProcessReadModel
{
    private const string HandoverActTemplate = "HR-F09";

    /// <summary>HR-F09 sections 1–4: tables of what is handed over (gate AKT).</summary>
    private static readonly string[] HandoverActSections = ["tasks", "documents", "access", "contacts"];

    private static readonly Expression<Func<ProcessCase, ProcessCaseRow>> CaseRow = row => new ProcessCaseRow(
        row.Id,
        row.ProcessCode,
        row.TemplateVersionId,
        row.Status,
        row.Outcome,
        row.Legacy,
        row.SubjectKind,
        row.VacancyRequestId,
        row.CandidateId,
        row.EmploymentId,
        row.ProbationId,
        row.EmployeeId,
        row.DepartmentId,
        row.ParentCaseId,
        row.AnchorDate,
        row.StartedOn,
        row.CompletedAt,
        row.CancelledAt,
        row.CancelReason,
        row.Version);

    private static readonly Expression<Func<ProcessCaseStep, ProcessStepRow>> StepRow = step => new ProcessStepRow(
        step.CaseId,
        step.StepCode,
        step.OrderNo,
        step.BoardColumn,
        step.State,
        step.Resolution,
        step.HolderRole,
        step.HolderEmployeeId,
        step.DueDate,
        step.DoneAt,
        step.DoneBy,
        step.Payload,
        step.BlockedGateCode,
        step.LinkedBlankSubmissionId,
        step.LinkedRecordId);

    private readonly AppDbContext _context;

    public ProcessReadModel(AppDbContext context) => _context = context;

    /// <summary>Steps of running cases: a finished case has nothing left to do.</summary>
    private IQueryable<ProcessCaseStep> RunningSteps
        => _context.ProcessCaseSteps
            .AsNoTracking()
            .Where(step => _context.ProcessCases.Any(row => row.Id == step.CaseId && row.Status == ProcessCaseStatus.Active));

    public async Task<IReadOnlyList<ProcessStepRow>> ListHeldStepsAsync(
        IReadOnlyCollection<string> officeRoles,
        Guid? employeeId,
        IReadOnlyCollection<ProcessStepState> states,
        CancellationToken cancellationToken = default)
    {
        var roles = officeRoles.ToList();
        var wanted = states.ToList();

        return await RunningSteps
            .Where(step => wanted.Contains(step.State))
            .Where(step => (step.HolderEmployeeId != null && step.HolderEmployeeId == employeeId)
                || (step.HolderEmployeeId == null && step.HolderRole != null && roles.Contains(step.HolderRole)))
            .Select(StepRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessStepRow>> ListProblemStepsAsync(
        DateOnly today,
        IReadOnlyCollection<string> alsoStepCodes,
        CancellationToken cancellationToken = default)
    {
        var codes = alsoStepCodes.ToList();

        return await RunningSteps
            .Where(step => step.State != ProcessStepState.Done && step.State != ProcessStepState.Skipped)
            .Where(step => step.State == ProcessStepState.Blocked
                || (step.DueDate != null && step.DueDate < today)
                || codes.Contains(step.StepCode))
            .Select(StepRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveVersionIdsAsync(CancellationToken cancellationToken = default)
        => await _context.ProcessCases
            .AsNoTracking()
            .Where(row => row.Status == ProcessCaseStatus.Active)
            .Select(row => row.TemplateVersionId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ProcessCaseRow>> ListCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default)
    {
        if (caseIds.Count == 0)
        {
            return [];
        }

        var ids = caseIds.ToList();

        return await _context.ProcessCases
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id))
            .Select(CaseRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessCaseRow>> ListActiveCasesAsync(string processCode, CancellationToken cancellationToken = default)
        => await _context.ProcessCases
            .AsNoTracking()
            .Where(row => row.ProcessCode == processCode && row.Status == ProcessCaseStatus.Active)
            .OrderBy(row => row.StartedOn)
            .ThenBy(row => row.Id)
            .Select(CaseRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ProcessCasePage> SearchCasesAsync(ProcessCaseFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.ProcessCases.AsNoTracking();

        if (filter.ProcessCode is { } process)
        {
            query = query.Where(row => row.ProcessCode == process);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(row => row.Status == status);
        }

        if (filter.DepartmentId is { } department)
        {
            query = query.Where(row => row.DepartmentId == department);
        }

        if (filter.EmployeeId is { } employee)
        {
            query = query.Where(row => row.EmployeeId == employee);
        }

        if (filter.CandidateId is { } candidate)
        {
            query = query.Where(row => row.CandidateId == candidate);
        }

        if (filter.From is { } from)
        {
            query = query.Where(row => row.StartedOn >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(row => row.StartedOn <= to);
        }

        if (filter.HolderRole is { } holderRole)
        {
            query = query.Where(row => _context.ProcessCaseSteps.Any(step =>
                step.CaseId == row.Id
                && step.HolderRole == holderRole
                && (step.State == ProcessStepState.Active || step.State == ProcessStepState.Blocked)));
        }

        if (filter.Late is { } late)
        {
            var today = filter.Today;
            var lateCases = _context.ProcessCaseSteps
                .Where(step => (step.State == ProcessStepState.Active || step.State == ProcessStepState.Blocked)
                    && step.DueDate != null
                    && step.DueDate < today)
                .Select(step => step.CaseId);

            query = late
                ? query.Where(row => row.Status == ProcessCaseStatus.Active && lateCases.Contains(row.Id))
                : query.Where(row => !lateCases.Contains(row.Id));
        }

        // Search: subject's name (latin/cyrillic-blind, the employee and candidate search rule) or the request's title.
        if (filter.Search is { } search)
        {
            var pattern = SearchPattern.For(search);
            var employees = EmployeeSearchQuery.MatchingIds(_context, search);
            var candidates = CandidateSearchQuery.MatchingIds(_context, search);
            var requests = _context.VacancyRequests
                .Where(request => EF.Functions.ILike(request.Title, pattern, SearchPattern.Escape))
                .Select(request => request.Id);

            query = query.Where(row =>
                (row.EmployeeId != null && employees.Contains(row.EmployeeId.Value))
                || (row.CandidateId != null && candidates.Contains(row.CandidateId.Value))
                || (row.VacancyRequestId != null && requests.Contains(row.VacancyRequestId.Value)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(row => row.StartedOn)
            .ThenByDescending(row => row.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(CaseRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ProcessCasePage(items, total);
    }

    public async Task<IReadOnlyList<ProcessStepRow>> ListStepsOfCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default)
    {
        if (caseIds.Count == 0)
        {
            return [];
        }

        var ids = caseIds.ToList();

        return await _context.ProcessCaseSteps
            .AsNoTracking()
            .Where(step => ids.Contains(step.CaseId))
            .OrderBy(step => step.CaseId)
            .ThenBy(step => step.OrderNo)
            .Select(StepRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessChildStepRow>> ListChildStepsAsync(
        IReadOnlyCollection<Guid> parentCaseIds,
        string stepCode,
        CancellationToken cancellationToken = default)
    {
        var parents = parentCaseIds.ToList();

        var rows = await (
                from child in _context.ProcessCases.AsNoTracking()
                where child.ParentCaseId != null && parents.Contains(child.ParentCaseId.Value)
                join step in _context.ProcessCaseSteps.AsNoTracking() on child.Id equals step.CaseId
                where step.StepCode == stepCode
                select new { ParentCaseId = child.ParentCaseId!.Value, step.CaseId, step.StepCode, step.OrderNo, step.BoardColumn, step.State, step.HolderRole, step.HolderEmployeeId, step.DueDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new ProcessChildStepRow(
                row.ParentCaseId,
                new ProcessStepRow(
                    row.CaseId,
                    row.StepCode,
                    row.OrderNo,
                    row.BoardColumn,
                    row.State,
                    Resolution: null,
                    row.HolderRole,
                    row.HolderEmployeeId,
                    row.DueDate,
                    DoneAt: null,
                    DoneBy: null,
                    Payload: null,
                    BlockedGateCode: null,
                    LinkedBlankSubmissionId: null,
                    LinkedRecordId: null))),
        ];
    }

    public async Task<ProcessNames> LoadNamesAsync(ProcessNameQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employeeIds = query.EmployeeIds.ToList();
        var employees = employeeIds.Count == 0
            ? []
            : await _context.Employees
                .AsNoTracking()
                .Where(row => employeeIds.Contains(row.Id))
                .Select(row => new { row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var candidateIds = query.CandidateIds.ToList();
        var candidates = candidateIds.Count == 0
            ? []
            : await _context.Candidates
                .AsNoTracking()
                .Where(row => candidateIds.Contains(row.Id))
                .Select(row => new
                {
                    row.Id,
                    row.FullName.LastName,
                    row.FullName.FirstName,
                    row.FullName.MiddleName,
                    VacancyTitle = _context.Vacancies.Where(vacancy => vacancy.Id == row.VacancyId).Select(vacancy => vacancy.Title).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var requestIds = query.VacancyRequestIds.ToList();
        var requests = requestIds.Count == 0
            ? []
            : await _context.VacancyRequests
                .AsNoTracking()
                .Where(row => requestIds.Contains(row.Id))
                .Select(row => new { row.Id, row.Title })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var employmentIds = query.EmploymentIds.ToList();
        var positions = employmentIds.Count == 0
            ? []
            : await _context.Employments
                .AsNoTracking()
                .Where(row => employmentIds.Contains(row.Id) && row.PositionId != null)
                .Select(row => new
                {
                    row.Id,
                    Name = _context.Positions.Where(position => position.Id == row.PositionId).Select(position => position.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var departmentIds = query.DepartmentIds.ToList();
        var departments = departmentIds.Count == 0
            ? []
            : await _context.Departments
                .AsNoTracking()
                .Where(row => departmentIds.Contains(row.Id))
                .Select(row => new { row.Id, row.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var userIds = query.UserIds.ToList();
        var users = userIds.Count == 0
            ? []
            : await _context.Users
                .AsNoTracking()
                .Where(row => userIds.Contains(row.Id))
                .Select(row => new { row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var blankCodes = query.BlankCodes.ToList();
        var blanks = blankCodes.Count == 0
            ? []
            : await _context.BlankTemplates
                .AsNoTracking()
                .Where(row => blankCodes.Contains(row.Code))
                .Select(row => new { row.Code, row.Title })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        return new ProcessNames(
            employees.ToDictionary(row => row.Id, row => FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full),
            candidates.ToDictionary(
                row => row.Id,
                row => new ProcessCandidateName(FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full, row.VacancyTitle)),
            requests.ToDictionary(row => row.Id, row => row.Title),
            positions.Where(row => row.Name is not null).ToDictionary(row => row.Id, row => row.Name!),
            departments.ToDictionary(row => row.Id, row => row.Name),
            users.ToDictionary(row => row.Id, row => FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full),
            blanks.ToDictionary(row => row.Code, row => row.Title, StringComparer.Ordinal));
    }

    public async Task<IReadOnlyList<ProcessLinkedBlankDto>> ListBlanksAsync(IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default)
    {
        var ids = submissionIds.ToList();

        var rows = await _context.BlankSubmissions
            .AsNoTracking()
            .Where(row => ids.Contains(row.Id))
            .OrderBy(row => row.CreatedAt)
            .Select(row => new
            {
                row.Id,
                row.TemplateCode,
                Title = _context.BlankTemplates.Where(template => template.Id == row.TemplateId).Select(template => template.Title).FirstOrDefault(),
                row.Number,
                row.Status,
                row.DueDate,
                row.CompletedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new ProcessLinkedBlankDto(row.Id, row.TemplateCode, row.Title, row.Number, row.Status.ToString(), row.DueDate, row.CompletedAt))];
    }

    public async Task<IReadOnlySet<Guid>> ListEmptyHandoverActsAsync(IReadOnlyCollection<Guid> employmentIds, CancellationToken cancellationToken = default)
    {
        var ids = employmentIds.ToList();

        var rows = await _context.BlankSubmissions
            .AsNoTracking()
            .Where(row => row.TemplateCode == HandoverActTemplate
                && row.EmploymentId != null
                && ids.Contains(row.EmploymentId.Value)
                && row.Status != BlankSubmissionStatus.Cancelled
                && row.Status != BlankSubmissionStatus.Rejected)
            .Select(row => new { EmploymentId = row.EmploymentId!.Value, row.Answers })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Where(row => IsEmptyHandoverAct(row.Answers))
            .Select(row => row.EmploymentId)
            .ToHashSet();
    }

    public Task<int> CountActiveEmployeesWithoutPhotoAsync(CancellationToken cancellationToken = default)
        => _context.Employees
            .AsNoTracking()
            .Where(employee => employee.PhotoKey == null
                && _context.Employments.Any(row => row.EmployeeId == employee.Id && row.Status == EmploymentStatus.Active))
            .CountAsync(cancellationToken);

    public async Task<int> CountActiveEmployeesWithoutScheduleAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var effective = _context.ScheduleAssignments
            .AsNoTracking()
            .Where(row => row.EffectiveFrom <= today && (row.EffectiveTo == null || row.EffectiveTo >= today));

        // A default assignment reaches everyone: nobody is without a schedule then (ScheduleResolver's last rung).
        if (await effective.AnyAsync(row => row.Scope == ScheduleScope.Default, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        return await _context.Employments
            .AsNoTracking()
            .Where(row => row.Status == EmploymentStatus.Active && row.IsPrimary)
            .Where(row => !effective.Any(assignment =>
                (assignment.Scope == ScheduleScope.Employee && assignment.EmployeeId == row.EmployeeId)
                || (assignment.Scope == ScheduleScope.Position && assignment.PositionId == row.PositionId)
                || (assignment.Scope == ScheduleScope.Department && assignment.DepartmentId == row.DepartmentId)))
            .Select(row => row.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>A dismissal act (kind = dismissal) with no row in any of sections 1–4.</summary>
    private static bool IsEmptyHandoverAct(string answers)
    {
        var json = BlankJson.Parse(answers);

        if (BlankJson.Choice(json["kind"]) != "dismissal")
        {
            return false;
        }

        return !HandoverActSections.Any(section => json[section] is JsonArray { Count: > 0 });
    }
}
