using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// §7.3: turns the start facts of §3.2 into cases. Each method only adds to the unit of work; the caller saves,
/// and reconciles what comes back.
/// </summary>
/// <remarks>
/// <para>
/// <b>§7.3 says <c>INSERT … ON CONFLICT DO NOTHING</c>; here it is a check before the insert.</b> A case is an
/// aggregate with its steps, and only the pass holding the engine's lock inserts, so the check cannot lose a
/// race against another pass.
/// </para>
/// <para>
/// Only a start fact on or after the engine's start reaches this class (the scans filter it), so the one way to
/// a <c>legacy</c> case is a subject already under way when the engine started: a candidate who applied earlier,
/// or an older live dismissal blank.
/// </para>
/// </remarks>
public sealed class ProcessStarters
{
    private readonly IProcessEngineReader _reader;
    private readonly IProcessCaseRepository _cases;
    private readonly IProcessEngineJournal _journal;

    public ProcessStarters(IProcessEngineReader reader, IProcessCaseRepository cases, IProcessEngineJournal journal)
    {
        _reader = reader;
        _cases = cases;
        _journal = journal;
    }

    /// <summary>The step plan of a new case: definition order; a <c>system</c> step has no holder (§7.7).</summary>
    public static IReadOnlyList<ProcessStepPlan> PlanOf(ProcessDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return
        [
            .. definition.Steps.Select((step, index) => new ProcessStepPlan(
                step.Id,
                index + 1,
                step.Column,
                step.Role == ProcessRoles.System ? null : step.Role)),
        ];
    }

    // ───────────────────────── hire-request ─────────────────────────

    public async Task<IReadOnlyList<ProcessCase>> StartRequestsAsync(ProcessEnginePass pass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);

        if (pass.Definitions.Published(ProcessCodes.HireRequest) is not { } template)
        {
            return [];
        }

        var starts = await _reader.ListVacancyRequestStartsAsync(pass.StartedOn, cancellationToken).ConfigureAwait(false);

        return await InsertAsync(
                pass,
                template,
                [
                    .. starts.Select(start => new ProcessCaseStart(
                        template.Code,
                        template.VersionId,
                        ProcessSubjectKind.VacancyRequest,
                        ProcessSubjectRefs.VacancyRequest(start.Id),
                        start.CreatedAt.TashkentDate(),
                        Legacy: false,
                        new ProcessCaseLinks(VacancyRequestId: start.Id),
                        DepartmentId: start.DepartmentId)),
                ],
                cancellationToken)
            .ConfigureAwait(false);
    }

    // ───────────────────────── hire-candidate ─────────────────────────

    /// <summary>A new application starts a new case and ends the old Active one (§6.9).</summary>
    /// <param name="pass">The pass.</param>
    /// <param name="active">The Active cases, with the requests started in this pass: parents and superseded cases are found here.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<ProcessCase>> StartCandidatesAsync(
        ProcessEnginePass pass,
        IReadOnlyList<ProcessCase> active,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(active);

        if (pass.Definitions.Published(ProcessCodes.HireCandidate) is not { } template)
        {
            return [];
        }

        var starts = await _reader.ListCandidateStartsAsync(pass.StartedOn, cancellationToken).ConfigureAwait(false);
        if (starts.Count == 0)
        {
            return [];
        }

        var parents = await LatestRequestCaseByVacancyAsync(active, cancellationToken).ConfigureAwait(false);

        var started = await InsertAsync(
                pass,
                template,
                [
                    .. starts.Select(start =>
                    {
                        var appliedOn = start.AppliedAt.TashkentDate();
                        var parent = parents.GetValueOrDefault(start.VacancyId);

                        return new ProcessCaseStart(
                            template.Code,
                            template.VersionId,
                            ProcessSubjectKind.Candidate,
                            ProcessSubjectRefs.Candidate(start.Id, appliedOn),
                            appliedOn,
                            Legacy: false,
                            new ProcessCaseLinks(CandidateId: start.Id),
                            DepartmentId: parent?.DepartmentId,
                            ParentCaseId: parent?.Id);
                    }),
                ],
                cancellationToken)
            .ConfigureAwait(false);

        await CompleteSupersededAsync(pass, active, started, cancellationToken).ConfigureAwait(false);

        return started;
    }

    /// <summary>Parent of a candidate case: the latest Active hire-request whose request has the same vacancy (§3.2).</summary>
    private async Task<Dictionary<Guid, ProcessCase>> LatestRequestCaseByVacancyAsync(IReadOnlyList<ProcessCase> active, CancellationToken cancellationToken)
    {
        var requestCases = active
            .Where(processCase => processCase is { ProcessCode: ProcessCodes.HireRequest, Status: ProcessCaseStatus.Active, VacancyRequestId: not null })
            .ToList();

        if (requestCases.Count == 0)
        {
            return [];
        }

        var subjects = await _reader
            .LoadSubjectsAsync(new ProcessSubjectQuery { VacancyRequestIds = [.. requestCases.Select(processCase => processCase.VacancyRequestId!.Value)] }, cancellationToken)
            .ConfigureAwait(false);

        return requestCases
            .Select(processCase => (Case: processCase, Vacancy: subjects.VacancyRequests.GetValueOrDefault(processCase.VacancyRequestId!.Value)?.VacancyId))
            .Where(row => row.Vacancy is not null)
            .GroupBy(row => row.Vacancy!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(row => row.Case.CreatedAt).Case);
    }

    /// <summary>
    /// §6.9, plus the old application's formalise case while it has no employment: nothing could close it any
    /// more, and left open it would take the hire of the new application from the case that is really waiting for it.
    /// </summary>
    private async Task CompleteSupersededAsync(
        ProcessEnginePass pass,
        IReadOnlyList<ProcessCase> active,
        IReadOnlyList<ProcessCase> started,
        CancellationToken cancellationToken)
    {
        var superseded = new List<ProcessCase>();
        foreach (var newCase in started)
        {
            superseded.AddRange(active.Where(processCase => processCase.ProcessCode == ProcessCodes.HireCandidate
                && processCase.Status == ProcessCaseStatus.Active
                && processCase.CandidateId == newCase.CandidateId
                && processCase.SubjectRef != newCase.SubjectRef));
        }

        if (superseded.Count == 0)
        {
            return;
        }

        var children = await _cases.ListByParentIdsAsync([.. superseded.Select(processCase => processCase.Id)], cancellationToken).ConfigureAwait(false);
        var openFormalise = children.Where(child => child is { ProcessCode: ProcessCodes.HireFormalise, Status: ProcessCaseStatus.Active, EmploymentId: null });

        foreach (var processCase in superseded.Concat(openFormalise))
        {
            var completed = processCase.Complete(ProcessOutcomes.Reapplied, pass.At);
            if (completed.IsFailure)
            {
                _journal.CaseProblem(processCase.Id, processCase.ProcessCode, $"reapplied: {completed.Error.Code}");
            }
        }
    }

    // ───────────────────────── hire-formalise ─────────────────────────

    /// <summary>§3.2 (a): the offer was accepted.</summary>
    /// <param name="pass">The pass.</param>
    /// <param name="offerAccepted">Cases whose pass reported <see cref="ProcessPassResult.OfferAccepted"/>.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <remarks>
    /// The fact is reported on every pass, so a parent that already has a child is the normal case: that child
    /// may be a direct-hire one, whose hire outran the offer. A parent that ended any other way than <c>hired</c>
    /// has nobody left to formalise.
    /// </remarks>
    public async Task<IReadOnlyList<ProcessCase>> StartFormaliseFromOffersAsync(
        ProcessEnginePass pass,
        IReadOnlyList<ProcessCase> offerAccepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(offerAccepted);

        var parents = offerAccepted
            .Where(parent => parent.CandidateId is not null
                && ProcessSubjectRefs.AppliedOn(parent.SubjectRef) is not null
                && (parent.Status == ProcessCaseStatus.Active || parent.Outcome == ProcessOutcomes.Hired))
            .ToList();

        if (parents.Count == 0 || pass.Definitions.Published(ProcessCodes.HireFormalise) is not { } template)
        {
            return [];
        }

        var children = await _cases.ListByParentIdsAsync([.. parents.Select(parent => parent.Id)], cancellationToken).ConfigureAwait(false);
        var withChild = children
            .Where(child => child.ProcessCode == ProcessCodes.HireFormalise)
            .Select(child => child.ParentCaseId!.Value)
            .ToHashSet();

        return await InsertAsync(
                pass,
                template,
                [.. parents.Where(parent => !withChild.Contains(parent.Id)).Select(parent => FormaliseFromOffer(pass, template, parent))],
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessCaseStart FormaliseFromOffer(ProcessEnginePass pass, ProcessPublishedTemplate template, ProcessCase parent)
    {
        var offer = parent.FindStep(ProcessStepIds.Offer);

        return new ProcessCaseStart(
            template.Code,
            template.VersionId,
            ProcessSubjectKind.Employment,
            ProcessSubjectRefs.FormaliseFromCandidate(parent.CandidateId!.Value, ProcessSubjectRefs.AppliedOn(parent.SubjectRef)!.Value),
            (offer?.DoneAt ?? pass.At).TashkentDate(),
            parent.Legacy,
            new ProcessCaseLinks(CandidateId: parent.CandidateId),
            DepartmentId: parent.DepartmentId,
            ParentCaseId: parent.Id,
            AnchorDate: ProcessStepPayload.ReadDateInput(offer?.Payload, ProcessStepPayload.StartDateInput));
    }

    /// <summary>§3.2 (b): a hire event whose employment has no formalise case. Runs after (a) has linked its employments.</summary>
    /// <param name="pass">The pass.</param>
    /// <param name="active">The cases of the pass: the parent is the candidate's Active hire-candidate case.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<ProcessCase>> StartFormaliseFromHiresAsync(
        ProcessEnginePass pass,
        IReadOnlyList<ProcessCase> active,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(active);

        if (pass.Definitions.Published(ProcessCodes.HireFormalise) is not { } template)
        {
            return [];
        }

        var starts = await _reader.ListHireEventStartsAsync(pass.StartedOn, cancellationToken).ConfigureAwait(false);
        var engineStart = ProcessSubjectRefs.StartOfDay(pass.StartedOn);

        return await InsertAsync(
                pass,
                template,
                [
                    .. starts.Select(start => new ProcessCaseStart(
                        template.Code,
                        template.VersionId,
                        ProcessSubjectKind.Employment,
                        ProcessSubjectRefs.FormaliseFromEmployment(start.EmploymentId),
                        start.RecordedAt.TashkentDate(),
                        Legacy: start.CandidateAppliedAt < engineStart,
                        new ProcessCaseLinks(CandidateId: start.CandidateId, EmploymentId: start.EmploymentId, EmployeeId: start.EmployeeId),
                        DepartmentId: start.DepartmentId,
                        ParentCaseId: ActiveCandidateCase(active, start.CandidateId)?.Id,
                        AnchorDate: start.HireDate)),
                ],
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessCase? ActiveCandidateCase(IReadOnlyList<ProcessCase> active, Guid? candidateId)
        => candidateId is null
            ? null
            : active
                .Where(processCase => processCase.ProcessCode == ProcessCodes.HireCandidate
                    && processCase.Status == ProcessCaseStatus.Active
                    && processCase.CandidateId == candidateId)
                .MaxBy(processCase => processCase.CreatedAt);

    // ───────────────────────── probation ─────────────────────────

    public async Task<IReadOnlyList<ProcessCase>> StartProbationsAsync(ProcessEnginePass pass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);

        if (pass.Definitions.Published(ProcessCodes.Probation) is not { } template)
        {
            return [];
        }

        var starts = await _reader.ListProbationStartsAsync(pass.StartedOn, cancellationToken).ConfigureAwait(false);

        return await InsertAsync(
                pass,
                template,
                [
                    .. starts.Select(start => new ProcessCaseStart(
                        template.Code,
                        template.VersionId,
                        ProcessSubjectKind.Employment,
                        ProcessSubjectRefs.Probation(start.Id),
                        start.StartDate,
                        Legacy: false,
                        new ProcessCaseLinks(EmploymentId: start.EmploymentId, ProbationId: start.Id, EmployeeId: start.EmployeeId),
                        DepartmentId: start.DepartmentId)),
                ],
                cancellationToken)
            .ConfigureAwait(false);
    }

    // ───────────────────────── dismissal ─────────────────────────

    /// <summary>§3.2 (b), (c). A <c>withdrawn</c> case takes a newer start fact back instead of a new case: one employment, one <c>subject_ref</c>.</summary>
    /// <returns>The cases started and the cases reopened: both need a pass.</returns>
    public async Task<IReadOnlyList<ProcessCase>> StartDismissalsAsync(ProcessEnginePass pass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pass);

        if (pass.Definitions.Published(ProcessCodes.Dismissal) is not { } template)
        {
            return [];
        }

        var starts = await _reader.ListDismissalStartsAsync(pass.StartedOn, cancellationToken).ConfigureAwait(false);
        if (starts.Count == 0)
        {
            return [];
        }

        var existing = await _cases
            .ListBySubjectRefsAsync(template.Code, [.. starts.Select(start => ProcessSubjectRefs.Dismissal(start.EmploymentId))], cancellationToken)
            .ConfigureAwait(false);
        var existingByRef = existing.ToDictionary(processCase => processCase.SubjectRef, StringComparer.Ordinal);

        var reopened = new List<ProcessCase>();
        var fresh = new List<ProcessCaseStart>();

        foreach (var start in starts)
        {
            var subjectRef = ProcessSubjectRefs.Dismissal(start.EmploymentId);

            if (!existingByRef.TryGetValue(subjectRef, out var processCase))
            {
                fresh.Add(new ProcessCaseStart(
                    template.Code,
                    template.VersionId,
                    ProcessSubjectKind.Employment,
                    subjectRef,
                    start.StartedOn,
                    start.InFlight,
                    new ProcessCaseLinks(EmploymentId: start.EmploymentId, EmployeeId: start.EmployeeId),
                    DepartmentId: start.DepartmentId));
            }
            else if (processCase.Outcome == ProcessOutcomes.Withdrawn && start.LatestFactAt > processCase.CompletedAt)
            {
                var result = processCase.Reopen(pass.At);
                if (result.IsSuccess)
                {
                    reopened.Add(processCase);
                }
                else
                {
                    _journal.CaseProblem(processCase.Id, processCase.ProcessCode, $"reopen: {result.Error.Code}");
                }
            }
        }

        var inserted = await InsertAsync(pass, template, fresh, cancellationToken).ConfigureAwait(false);

        return [.. inserted, .. reopened];
    }

    // ───────────────────────── Insert ─────────────────────────

    /// <summary>Adds the starts whose <c>subject_ref</c> is free. A same-day reapplication, for one, arrives here with its case already there.</summary>
    private async Task<IReadOnlyList<ProcessCase>> InsertAsync(
        ProcessEnginePass pass,
        ProcessPublishedTemplate template,
        List<ProcessCaseStart> starts,
        CancellationToken cancellationToken)
    {
        if (starts.Count == 0)
        {
            return [];
        }

        var existing = await _cases
            .ListBySubjectRefsAsync(template.Code, [.. starts.Select(start => start.SubjectRef)], cancellationToken)
            .ConfigureAwait(false);

        var taken = existing.Select(processCase => processCase.SubjectRef).ToHashSet(StringComparer.Ordinal);
        var plan = PlanOf(template.Definition);
        var started = new List<ProcessCase>();

        foreach (var start in starts)
        {
            if (!taken.Add(start.SubjectRef))
            {
                continue;
            }

            var result = ProcessCase.Start(start, plan, pass.At);
            if (result.IsFailure)
            {
                _journal.CaseProblem(Guid.Empty, template.Code, $"start {start.SubjectRef}: {result.Error.Code}");
                continue;
            }

            _cases.Add(result.Value);
            started.Add(result.Value);
        }

        return started;
    }
}
