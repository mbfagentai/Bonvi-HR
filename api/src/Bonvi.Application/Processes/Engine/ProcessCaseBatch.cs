using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// §7.4 over a batch of cases: everything a <see cref="ProcessPass"/> reads, loaded once for the whole batch,
/// then the pass per case. Only changes the tracked cases; the caller saves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Candidate facts load first.</b> The <c>cand.hired</c> fact names the employment, and every <c>emp.*</c> /
/// <c>dis.*</c> fact and subject row of the case hangs on that link: loaded the other way round, a hire would show
/// on the next pass only and (b) of the same pass would start a second formalise case (§3.2).
/// </para>
/// </remarks>
public sealed class ProcessCaseBatch
{
    private readonly IProcessFactSource _facts;
    private readonly IProcessEngineReader _reader;
    private readonly IProcessCaseRepository _cases;
    private readonly IProcessEngineJournal _journal;

    public ProcessCaseBatch(
        IProcessFactSource facts,
        IProcessEngineReader reader,
        IProcessCaseRepository cases,
        IProcessEngineJournal journal)
    {
        _facts = facts;
        _reader = reader;
        _cases = cases;
        _journal = journal;
    }

    /// <summary>Passes every case of <paramref name="cases"/>.</summary>
    /// <returns>Those whose pass reported an accepted offer: the input of the hire-formalise (a) starter.</returns>
    public async Task<IReadOnlyList<ProcessCase>> ReconcileAsync(
        ProcessEnginePass pass,
        IReadOnlyList<ProcessCase> cases,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(cases);

        if (cases.Count == 0)
        {
            return [];
        }

        await pass.Definitions.EnsureVersionsAsync(cases.Select(processCase => processCase.TemplateVersionId), cancellationToken).ConfigureAwait(false);

        var candidateFacts = await _facts
            .LoadAsync(
                new ProcessFactQuery
                {
                    Candidates = [.. cases.Select(ProcessCaseFacts.CandidateSubject).OfType<CandidateFactSubject>().Distinct()],
                    StageBindings = pass.StageBindings,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await LinkHiresAsync(pass, cases, candidateFacts, cancellationToken).ConfigureAwait(false);

        var otherFacts = await _facts
            .LoadAsync(
                new ProcessFactQuery
                {
                    VacancyRequestIds = Ids(cases, processCase => processCase.VacancyRequestId),
                    EmploymentIds = Ids(cases, processCase => processCase.EmploymentId),
                    ProbationIds = Ids(cases, processCase => processCase.ProbationId),
                    EmployeeIds = Ids(cases, processCase => processCase.EmployeeId),
                    StageBindings = pass.StageBindings,
                },
                cancellationToken)
            .ConfigureAwait(false);

        var snapshot = new ProcessFactSnapshot(
            otherFacts.VacancyRequests,
            candidateFacts.Candidates,
            otherFacts.Employments,
            otherFacts.Probations,
            otherFacts.Employees);

        var subjects = await _reader
            .LoadSubjectsAsync(
                new ProcessSubjectQuery
                {
                    VacancyRequestIds = Ids(cases, processCase => processCase.VacancyRequestId),
                    CandidateIds = Ids(cases, processCase => processCase.CandidateId),
                    EmploymentIds = Ids(cases, processCase => processCase.EmploymentId),
                    ProbationIds = Ids(cases, processCase => processCase.ProbationId),
                    EmployeeIds = Ids(cases, processCase => processCase.EmployeeId),
                },
                cancellationToken)
            .ConfigureAwait(false);

        var candidateCaseIds = cases
            .Where(processCase => processCase.ProcessCode == ProcessCodes.HireCandidate)
            .Select(processCase => processCase.Id)
            .ToList();

        var formaliseChildren = (await _cases.ListByParentIdsAsync(candidateCaseIds, cancellationToken).ConfigureAwait(false))
            .Where(child => child.ProcessCode == ProcessCodes.HireFormalise)
            .ToLookup(child => child.ParentCaseId!.Value);

        var offerAccepted = new List<ProcessCase>();

        foreach (var processCase in cases)
        {
            if (pass.Definitions.ForVersion(processCase.TemplateVersionId) is not { } definition)
            {
                continue;
            }

            var safetyInstructionDone = formaliseChildren[processCase.Id]
                .Any(child => child.FindStep(ProcessStepIds.SafetyInstruction) is { State: ProcessStepState.Done });

            var result = ProcessPass.Apply(
                processCase,
                new ProcessPassInput(
                    definition,
                    snapshot,
                    SubjectOf(processCase, subjects, safetyInstructionDone),
                    pass.Calendar,
                    pass.Settings.CouncilDay,
                    pass.Settings.GatesEnforced,
                    pass.At));

            foreach (var problem in result.Problems)
            {
                _journal.CaseProblem(processCase.Id, processCase.ProcessCode, problem);
            }

            if (result.OfferAccepted)
            {
                offerAccepted.Add(processCase);
            }
        }

        return offerAccepted;
    }

    /// <summary>
    /// Fills only the empty ids. <c>process_cases</c> holds one case per process and employment, and one row that
    /// breaks it would fail the whole pass on every run, so a hire another case of the process already holds
    /// (one candidate, two applications) is reported instead of linked.
    /// </summary>
    private async Task LinkHiresAsync(
        ProcessEnginePass pass,
        IReadOnlyList<ProcessCase> cases,
        ProcessFactSnapshot candidateFacts,
        CancellationToken cancellationToken)
    {
        var hires = cases
            .Where(processCase => processCase.EmploymentId is null)
            .Select(processCase => (
                Case: processCase,
                Hire: ProcessCaseFacts.CandidateSubject(processCase) is { } subject
                    ? candidateFacts.ForCandidate(subject, ProcessFacts.CandHired)
                    : FactValue.NotHeld))
            .Where(row => row.Hire is { Holds: true, LinkedId: not null })
            .ToList();

        if (hires.Count == 0)
        {
            return;
        }

        var holders = await _cases
            .ListByEmploymentIdsAsync([.. hires.Select(row => row.Hire.LinkedId!.Value).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        var held = holders.Select(holder => (holder.ProcessCode, holder.EmploymentId!.Value)).ToHashSet();

        foreach (var (processCase, hire) in hires)
        {
            if (!held.Add((processCase.ProcessCode, hire.LinkedId!.Value)))
            {
                _journal.CaseProblem(processCase.Id, processCase.ProcessCode, $"hire {hire.LinkedId} is held by another case");
                continue;
            }

            var linked = processCase.LinkSubjects(
                new ProcessCaseLinks(
                    EmploymentId: hire.LinkedId,
                    EmployeeId: processCase.EmployeeId is null ? hire.LinkedEmployeeId : null),
                pass.At);

            if (linked.IsFailure)
            {
                _journal.CaseProblem(processCase.Id, processCase.ProcessCode, $"link: {linked.Error.Code}");
            }
        }
    }

    /// <summary>A case's rows. A row hidden by the soft-delete filters is an absent subject (§6.9).</summary>
    private static ProcessCaseSubject SubjectOf(ProcessCase processCase, ProcessSubjects subjects, bool safetyInstructionDone)
    {
        var request = Row(subjects.VacancyRequests, processCase.VacancyRequestId);
        var candidate = Row(subjects.Candidates, processCase.CandidateId);
        var employment = Row(subjects.Employments, processCase.EmploymentId);
        var probation = Row(subjects.Probations, processCase.ProbationId);

        var departmentId = employment?.DepartmentId ?? candidate?.DepartmentId ?? request?.DepartmentId;

        Guid? head = departmentId is { } id && subjects.DepartmentHeads.TryGetValue(id, out var headId) && subjects.EmployeesWithUser.Contains(headId)
            ? headId
            : null;

        Guid? subjectEmployee = processCase.EmployeeId is { } employeeId && subjects.EmployeesWithUser.Contains(employeeId)
            ? employeeId
            : null;

        return new ProcessCaseSubject
        {
            Exists = SubjectExists(processCase, request, candidate, employment, probation),
            DepartmentId = departmentId,
            DepartmentHeadWithUser = head,
            SubjectEmployeeWithUser = subjectEmployee,
            HireDate = employment?.HireDate,
            HireOrderDate = employment?.HireOrderDate,
            TerminationDate = employment?.TerminationDate,
            EmploymentStatus = employment?.Status,
            ProbationStart = probation?.StartDate,
            ProbationEnd = probation?.EndDate,
            EmployeeHasPinfl = employment?.EmployeeHasPinfl ?? false,
            SafetyInstructionDone = safetyInstructionDone,
        };
    }

    /// <summary>
    /// A formalise case started from a candidate outlives the candidate once it has the employment: the person
    /// was hired, and the paperwork is still owed. One started from the employment hangs on that alone.
    /// </summary>
    private static bool SubjectExists(
        ProcessCase processCase,
        VacancyRequestRow? request,
        CandidateRow? candidate,
        EmploymentRow? employment,
        ProbationRow? probation)
        => processCase.ProcessCode switch
        {
            ProcessCodes.HireRequest => request is not null,
            ProcessCodes.HireCandidate => candidate is not null,
            ProcessCodes.HireFormalise when ProcessSubjectRefs.IsDirectHire(processCase.SubjectRef) => employment is not null,
            ProcessCodes.HireFormalise => employment is not null || candidate is not null,
            ProcessCodes.Probation => probation is not null,
            ProcessCodes.Dismissal => employment is not null,
            _ => true,
        };

    private static TRow? Row<TRow>(IReadOnlyDictionary<Guid, TRow> rows, Guid? id)
        where TRow : class
        => id is { } key ? rows.GetValueOrDefault(key) : null;

    private static Guid[] Ids(IReadOnlyList<ProcessCase> cases, Func<ProcessCase, Guid?> id)
        => [.. cases.Select(id).OfType<Guid>().Distinct()];
}
