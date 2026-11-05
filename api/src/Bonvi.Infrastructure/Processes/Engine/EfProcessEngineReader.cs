using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes.Facts;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Engine;

/// <summary>
/// EF implementation of <see cref="IProcessEngineReader"/> (docs/SPEC-mening-ishlarim.md §3.2, §7.3, §7.7).
/// </summary>
/// <remarks>
/// <para>
/// Read-only and <c>AsNoTracking</c>; each method is one batch, never one query per subject. Soft-deleted
/// rows stay hidden by the global filters, and every employment query joins its employee, so a deleted
/// employee is an absent subject too.
/// </para>
/// <para>
/// A scan leaves out subjects that already have a case with <c>NOT EXISTS</c>, so its result shrinks to
/// the new starts instead of growing with history. The exceptions are spelled out on the scans.
/// </para>
/// </remarks>
public sealed class EfProcessEngineReader : IProcessEngineReader
{
    private static readonly string[] DismissalBlankCodes = [ProcessBlanks.F08, ProcessBlanks.F09];

    private readonly AppDbContext _context;

    public EfProcessEngineReader(AppDbContext context) => _context = context;

    // ────────────────────────────────────────────────────────────
    //  Subjects of a batch
    // ────────────────────────────────────────────────────────────

    public async Task<ProcessSubjects> LoadSubjectsAsync(ProcessSubjectQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vacancyRequests = await VacancyRequestRowsAsync(Distinct(query.VacancyRequestIds), cancellationToken).ConfigureAwait(false);
        var candidates = await CandidateRowsAsync(Distinct(query.CandidateIds), cancellationToken).ConfigureAwait(false);
        var employments = await EmploymentRowsAsync(Distinct(query.EmploymentIds), cancellationToken).ConfigureAwait(false);
        var probations = await ProbationRowsAsync(Distinct(query.ProbationIds), cancellationToken).ConfigureAwait(false);

        var departmentIds = vacancyRequests.Values.Select(row => (Guid?)row.DepartmentId)
            .Concat(candidates.Values.Select(row => row.DepartmentId))
            .Concat(employments.Values.Select(row => row.DepartmentId))
            .OfType<Guid>();

        var heads = await DepartmentHeadsAsync(Distinct(departmentIds), cancellationToken).ConfigureAwait(false);

        var employeeIds = heads.Values
            .Concat(query.EmployeeIds)
            .Concat(employments.Values.Select(row => row.EmployeeId));

        var withUser = await EmployeesWithUserAsync(Distinct(employeeIds), cancellationToken).ConfigureAwait(false);

        return new ProcessSubjects
        {
            VacancyRequests = vacancyRequests,
            Candidates = candidates,
            Employments = employments,
            Probations = probations,
            DepartmentHeads = heads,
            EmployeesWithUser = withUser,
        };
    }

    private async Task<Dictionary<Guid, VacancyRequestRow>> VacancyRequestRowsAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        return await _context.VacancyRequests.AsNoTracking()
            .Where(request => ids.Contains(request.Id))
            .Select(request => new VacancyRequestRow(request.Id, request.DepartmentId, request.VacancyId))
            .ToDictionaryAsync(row => row.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>A vacancy carries no department: it comes from the latest request behind the vacancy.</summary>
    private async Task<Dictionary<Guid, CandidateRow>> CandidateRowsAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        var candidates = await _context.Candidates.AsNoTracking()
            .Where(candidate => ids.Contains(candidate.Id))
            .Select(candidate => new { candidate.Id, candidate.VacancyId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vacancyIds = Distinct(candidates.Select(candidate => candidate.VacancyId));

        var requests = await _context.VacancyRequests.AsNoTracking()
            .Where(request => request.VacancyId != null && vacancyIds.Contains(request.VacancyId.Value))
            .Select(request => new { VacancyId = request.VacancyId!.Value, request.DepartmentId, request.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var departmentByVacancy = requests
            .GroupBy(request => request.VacancyId)
            .ToDictionary(group => group.Key, group => group.MaxBy(request => request.CreatedAt)!.DepartmentId);

        return candidates.ToDictionary(
            candidate => candidate.Id,
            candidate => new CandidateRow(
                candidate.Id,
                departmentByVacancy.TryGetValue(candidate.VacancyId, out var departmentId) ? departmentId : null));
    }

    private async Task<Dictionary<Guid, EmploymentRow>> EmploymentRowsAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        return await (
                from employment in _context.Employments.AsNoTracking()
                join employee in _context.Employees.AsNoTracking() on employment.EmployeeId equals employee.Id
                where ids.Contains(employment.Id)
                select new EmploymentRow(
                    employment.Id,
                    employment.EmployeeId,
                    employment.DepartmentId,
                    employment.HireDate,
                    employment.HireOrderDate,
                    employment.TerminationDate,
                    employment.Status,
                    employee.Pinfl != null))
            .ToDictionaryAsync(row => row.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, ProbationRow>> ProbationRowsAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        return await (
                from probation in _context.Probations.AsNoTracking()
                join employment in _context.Employments.AsNoTracking() on probation.EmploymentId equals employment.Id
                join employee in _context.Employees.AsNoTracking() on employment.EmployeeId equals employee.Id
                where ids.Contains(probation.Id)
                select new ProbationRow(probation.Id, probation.EmploymentId, probation.StartDate, probation.EndDate))
            .ToDictionaryAsync(row => row.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, Guid>> DepartmentHeadsAsync(Guid[] departmentIds, CancellationToken cancellationToken)
    {
        if (departmentIds.Length == 0)
        {
            return [];
        }

        var rows = await _context.Departments.AsNoTracking()
            .Where(department => departmentIds.Contains(department.Id) && department.HeadEmployeeId != null)
            .Select(department => new { department.Id, Head = department.HeadEmployeeId!.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.Id, row => row.Head);
    }

    private async Task<HashSet<Guid>> EmployeesWithUserAsync(Guid[] employeeIds, CancellationToken cancellationToken)
    {
        if (employeeIds.Length == 0)
        {
            return [];
        }

        var rows = await _context.Users.AsNoTracking()
            .Where(user => user.IsActive && user.EmployeeId != null && employeeIds.Contains(user.EmployeeId.Value))
            .Select(user => user.EmployeeId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows];
    }

    // ────────────────────────────────────────────────────────────
    //  Starter scans
    // ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<VacancyRequestStart>> ListVacancyRequestStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default)
    {
        var since = ProcessSubjectRefs.StartOfDay(startedOn);

        return await _context.VacancyRequests.AsNoTracking()
            .Where(request => request.CreatedAt >= since
                && !_context.ProcessCases.Any(processCase => processCase.ProcessCode == ProcessCodes.HireRequest
                    && processCase.VacancyRequestId == request.Id))
            .OrderBy(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .Select(request => new VacancyRequestStart(request.Id, request.CreatedAt, request.DepartmentId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <remarks>
    /// <c>subject_ref</c> holds the Tashkent date of the application, which SQL here cannot compute. So the
    /// filter asks for a case created since the current application. A same-day reapplication passes it and
    /// shares its <c>subject_ref</c> with the existing case: the starter's <c>ON CONFLICT</c> absorbs that.
    /// </remarks>
    public async Task<IReadOnlyList<CandidateStart>> ListCandidateStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default)
    {
        var since = ProcessSubjectRefs.StartOfDay(startedOn);

        return await _context.Candidates.AsNoTracking()
            .Where(candidate => candidate.Status == CandidateStatus.InProgress
                && (candidate.ReappliedAt ?? candidate.CreatedAt) >= since
                && !_context.ProcessCases.Any(processCase => processCase.ProcessCode == ProcessCodes.HireCandidate
                    && processCase.CandidateId == candidate.Id
                    && processCase.CreatedAt >= (candidate.ReappliedAt ?? candidate.CreatedAt)))
            .OrderBy(candidate => candidate.ReappliedAt ?? candidate.CreatedAt)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new CandidateStart(candidate.Id, candidate.ReappliedAt ?? candidate.CreatedAt, candidate.VacancyId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <remarks>
    /// Imported events are history from Excel, not hires made under the engine. An employment can have
    /// several qualifying events (hire, rehire) and takes one case, so the earliest one speaks for it.
    /// </remarks>
    public async Task<IReadOnlyList<HireEventStart>> ListHireEventStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default)
    {
        var since = ProcessSubjectRefs.StartOfDay(startedOn);

        var rows = await (
                from employmentEvent in _context.EmploymentEvents.AsNoTracking()
                join employment in _context.Employments.AsNoTracking() on employmentEvent.EmploymentId equals employment.Id
                join employee in _context.Employees.AsNoTracking() on employment.EmployeeId equals employee.Id
                where (employmentEvent.Type == EmploymentEventType.Hire || employmentEvent.Type == EmploymentEventType.Rehire)
                    && !employmentEvent.IsImported
                    && employmentEvent.RecordedAt >= since
                    && !_context.ProcessCases.Any(processCase => processCase.ProcessCode == ProcessCodes.HireFormalise
                        && processCase.EmploymentId == employment.Id)
                orderby employmentEvent.RecordedAt, employmentEvent.Id
                select new HireEventStart(
                    employment.Id,
                    employment.EmployeeId,
                    employment.DepartmentId,
                    employment.HireDate,
                    employmentEvent.RecordedAt,
                    employmentEvent.CandidateId,
                    _context.Candidates
                        .Where(candidate => candidate.Id == employmentEvent.CandidateId)
                        .Select(candidate => (DateTimeOffset?)(candidate.ReappliedAt ?? candidate.CreatedAt))
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.DistinctBy(row => row.EmploymentId)];
    }

    /// <remarks>
    /// Checked by <c>employment_id</c>, not by probation: <c>process_cases</c> holds one case per process
    /// and employment (partial UNIQUE), so a probation recreated after its row was deleted would otherwise
    /// fail the whole pass at insert. <c>probations.employment_id</c> is UNIQUE, so no employment repeats here.
    /// </remarks>
    public async Task<IReadOnlyList<ProbationStart>> ListProbationStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default)
    {
        return await (
                from probation in _context.Probations.AsNoTracking()
                join employment in _context.Employments.AsNoTracking() on probation.EmploymentId equals employment.Id
                join employee in _context.Employees.AsNoTracking() on employment.EmployeeId equals employee.Id
                where probation.Status == ProbationStatus.InProgress
                    && probation.StartDate >= startedOn
                    && !_context.ProcessCases.Any(processCase => processCase.ProcessCode == ProcessCodes.Probation
                        && processCase.EmploymentId == probation.EmploymentId)
                orderby probation.StartDate, probation.Id
                select new ProbationStart(
                    probation.Id,
                    probation.EmploymentId,
                    employment.EmployeeId,
                    employment.DepartmentId,
                    probation.StartDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <remarks>
    /// <para>
    /// The <c>NOT EXISTS</c> here keeps employments whose only case ended <c>withdrawn</c>: that case
    /// reopens on a newer start fact, and <see cref="DismissalStart.LatestFactAt"/> is what the starter
    /// compares. They come back every pass until then, which is a handful of rows.
    /// </para>
    /// <para>
    /// The blank kind lives in the answers JSON, so it is filtered in memory: an employee whose only recent
    /// F09 is not a dismissal is loaded and then dropped.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<DismissalStart>> ListDismissalStartsAsync(DateOnly startedOn, CancellationToken cancellationToken = default)
    {
        var since = ProcessSubjectRefs.StartOfDay(startedOn);

        var employeesWithRecentBlank = await _context.BlankSubmissions.AsNoTracking()
            .Where(blank => DismissalBlankCodes.Contains(blank.TemplateCode)
                && BlankFactRows.LiveStatuses.Contains(blank.Status)
                && blank.EmployeeId != null
                && blank.CreatedAt >= since)
            .Select(blank => blank.EmployeeId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var employments = await (
                from employment in _context.Employments.AsNoTracking()
                join employee in _context.Employees.AsNoTracking() on employment.EmployeeId equals employee.Id
                where ((employment.Status == EmploymentStatus.Probation || employment.Status == EmploymentStatus.Active)
                        && employeesWithRecentBlank.Contains(employment.EmployeeId))
                    || (employment.Status == EmploymentStatus.Terminated && employment.TerminationDate >= startedOn)
                where !_context.ProcessCases.Any(processCase => processCase.ProcessCode == ProcessCodes.Dismissal
                    && processCase.EmploymentId == employment.Id
                    && processCase.Outcome != ProcessOutcomes.Withdrawn)
                select new DismissalEmployment(
                    employment.Id,
                    employment.EmployeeId,
                    employment.DepartmentId,
                    employment.Status,
                    employment.TerminationDate,
                    employment.CreatedAt,
                    employment.UpdatedAt ?? employment.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var blanks = await BlankFactRows.ForEmployeesAsync(
                _context,
                DismissalBlankCodes,
                Distinct(employments.Select(employment => employment.EmployeeId)),
                cancellationToken)
            .ConfigureAwait(false);

        var dismissalBlanks = blanks.Where(IsDismissalBlank).ToLookup(blank => blank.EmployeeId!.Value);

        return
        [
            .. employments
                .Select(employment => ToStart(employment, dismissalBlanks[employment.EmployeeId], since))
                .OfType<DismissalStart>()
                .OrderBy(start => start.StartedAt)
                .ThenBy(start => start.EmploymentId),
        ];
    }

    /// <returns><c>null</c> when the employment turns out to have no start fact.</returns>
    private static DismissalStart? ToStart(DismissalEmployment employment, IEnumerable<BlankFactRow> blanksOfEmployee, DateTimeOffset since)
    {
        var blanks = blanksOfEmployee.Where(blank => BelongsTo(blank, employment)).ToList();

        var startFacts = blanks.Select(blank => blank.CreatedAt).Where(createdAt => createdAt >= since).ToList();
        var latestFact = startFacts.Count == 0 ? (DateTimeOffset?)null : startFacts.Max();

        if (employment is { Status: EmploymentStatus.Terminated, TerminationDate: { } terminationDate })
        {
            startFacts.Add(ProcessSubjectRefs.StartOfDay(terminationDate));

            // The termination date says nothing about when it was recorded: a dismissal withdrawn this
            // morning and terminated this afternoon has a date older than its withdrawal.
            latestFact = new[] { latestFact, startFacts[^1], employment.ChangedAt }.Max();
        }

        if (startFacts.Count == 0)
        {
            return null;
        }

        var startedAt = startFacts.Min();

        return new DismissalStart(
            employment.Id,
            employment.EmployeeId,
            employment.DepartmentId,
            startedAt,
            startedAt.TashkentDate(),
            InFlight: blanks.Exists(blank => blank.CreatedAt < since),
            LatestFactAt: latestFact!.Value);
    }

    private static bool IsDismissalBlank(BlankFactRow blank)
        => blank.TemplateCode == ProcessBlanks.F08
            || BlankJson.Choice(blank.AnswersJson["kind"]) == ProcessBlanks.F09DismissalKind;

    /// <summary>The ownership rule of the fact source (<c>EfProcessFactSource.Employments.cs</c>): the two must agree.</summary>
    private static bool BelongsTo(BlankFactRow blank, DismissalEmployment employment)
        => blank.EmploymentId == employment.Id
            || (blank.EmploymentId is null && blank.CreatedAt >= employment.CreatedAt);

    private static Guid[] Distinct(IEnumerable<Guid> ids) => [.. ids.Distinct()];

    private sealed record DismissalEmployment(
        Guid Id,
        Guid EmployeeId,
        Guid? DepartmentId,
        EmploymentStatus Status,
        DateOnly? TerminationDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset ChangedAt);
}
