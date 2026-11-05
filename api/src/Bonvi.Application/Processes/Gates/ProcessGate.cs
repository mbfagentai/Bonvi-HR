using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;
using DomainGateCodes = Bonvi.Domain.Processes.ProcessGateCodes;

namespace Bonvi.Application.Processes.Gates;

/// <summary>
/// Q01 on Hire, Q12 and Q13 on Terminate (docs/SPEC-mening-ishlarim.md §8.1, §8.2, §8.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Order of the answers.</b> Dormant or mode <c>off</c> → Allow, before anything else is read: every flow that
/// predates the engine stays as it was. Mode <c>warn</c> or a legacy subject → a failure is a warning, and no
/// override is spent on it. Otherwise a failure blocks unless a usable override covers its gate.
/// </para>
/// <para>
/// <b>All or none.</b> Overrides are consumed only when nothing is left blocking, so a Terminate stopped by Q13
/// does not spend the override it already has for Q12.
/// </para>
/// <para>
/// <b>A gate that cannot answer.</b> A warning is not worth a refused Hire or Terminate: while the mode only warns, a
/// failure of the inspection is journalled and the operation goes on (Allow). Under <c>enforce</c> it reaches the
/// caller, because an unanswered gate must not read as a passed one. Reading the mode itself stays outside: without
/// the settings there is no telling which of the two applies, and the command needs that database anyway.
/// </para>
/// </remarks>
public sealed class ProcessGate : IProcessGate
{
    /// <summary>§8.1 Q01: the warning that never blocks.</summary>
    private static readonly HrWarning OrderNumberMissing =
        new(ProcessErrors.HireOrderNumberMissingWarning, "Ishga qabul buyrug'ining raqami kiritilmagan.");

    private readonly SystemSettingsReader _settings;
    private readonly ProcessEnginePassLoader _passLoader;
    private readonly IProcessFactSource _facts;
    private readonly IProcessCaseRepository _cases;
    private readonly IProcessGateOverrideRepository _overrides;
    private readonly ICandidateRepository _candidates;
    private readonly IEmploymentRepository _employments;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IProcessEngineJournal _journal;

    public ProcessGate(
        SystemSettingsReader settings,
        ProcessEnginePassLoader passLoader,
        IProcessFactSource facts,
        IProcessCaseRepository cases,
        IProcessGateOverrideRepository overrides,
        ICandidateRepository candidates,
        IEmploymentRepository employments,
        ICurrentUser currentUser,
        IClock clock,
        IProcessEngineJournal journal)
    {
        _settings = settings;
        _passLoader = passLoader;
        _facts = facts;
        _cases = cases;
        _overrides = overrides;
        _candidates = candidates;
        _employments = employments;
        _currentUser = currentUser;
        _clock = clock;
        _journal = journal;
    }

    /// <inheritdoc />
    public async Task<GateResult> CheckAsync(GateContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = ProcessEngineSettings.From(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false));
        if (settings.StartedOn is not { } startedOn || settings.GatesOff)
        {
            return GateResult.Allow;
        }

        if (settings.GatesEnforced)
        {
            var findings = await InspectAsync(context, startedOn, settings, cancellationToken).ConfigureAwait(false);

            return findings.Legacy
                ? WarnOf(findings)
                : await ApplyOverridesAsync(context, findings, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return WarnOf(await InspectAsync(context, startedOn, settings, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _journal.GateFailedOpen(context.Operation.ToString(), context.EmployeeId, exception);
            return GateResult.Allow;
        }
    }

    private Task<GateFindings> InspectAsync(GateContext context, DateOnly startedOn, ProcessEngineSettings settings, CancellationToken cancellationToken)
        => context.Operation == GateOperation.Hire
            ? InspectHireAsync(context, startedOn, settings, cancellationToken)
            : InspectTerminateAsync(context, cancellationToken);

    private static GateResult WarnOf(GateFindings findings)
        => GateResult.Warn([.. findings.Warnings, .. findings.Failures.Select(failure => HrWarning.From(failure.Error))]);

    private async Task<GateResult> ApplyOverridesAsync(GateContext context, GateFindings findings, CancellationToken cancellationToken)
    {
        var at = _clock.UtcNow;
        var blocking = new List<Error>();
        var covering = new List<ProcessGateOverride>();

        foreach (var gate in findings.Failures.GroupBy(failure => failure.GateCode, StringComparer.Ordinal))
        {
            var granted = await FindOverrideAsync(gate.Key, context, at, cancellationToken).ConfigureAwait(false);

            if (granted is null)
            {
                blocking.AddRange(gate.Select(failure => failure.Error));
            }
            else if (gate.Key == DomainGateCodes.Q12 && context.TerminationDate > _clock.Today)
            {
                // §8.1: the act may still close before the last day, so until then the exception is not honoured.
                blocking.Add(ProcessErrors.OverrideOnlyOnLastDay);
            }
            else
            {
                covering.Add(granted);
            }
        }

        if (blocking.Count > 0)
        {
            return GateResult.Block(blocking);
        }

        var operation = context.Operation == GateOperation.Hire ? ProcessGateOperations.Hire : ProcessGateOperations.Terminate;

        foreach (var granted in covering)
        {
            var consumed = granted.Consume(operation, at);
            if (consumed.IsFailure)
            {
                return GateResult.Block([consumed.Error]);
            }
        }

        return GateResult.Warn(findings.Warnings);
    }

    /// <summary>
    /// A hire is covered by the override of its candidate's case, or, with no candidate, by a direct-hire override
    /// for this operator. A termination is covered by its employment's, except ex-10: that one is the engine's.
    /// </summary>
    private async Task<ProcessGateOverride?> FindOverrideAsync(string gateCode, GateContext context, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var usable = await _overrides.ListUsableAsync(gateCode, at, cancellationToken).ConfigureAwait(false);

        return context switch
        {
            { Operation: GateOperation.Terminate } => usable.FirstOrDefault(row
                => row.EmploymentId == context.EmploymentId && row.StepCode != ProcessStepIds.FinalSettlement),
            { CandidateId: { } candidateId } => usable.FirstOrDefault(row => row.CaseId != null && row.CandidateId == candidateId),
            _ => _currentUser.UserId is { } userId
                ? usable.FirstOrDefault(row => row.CaseId == null && row.ForUserId == userId)
                : null,
        };
    }

    // ───────────────────────── Q01 ─────────────────────────

    private async Task<GateFindings> InspectHireAsync(GateContext context, DateOnly startedOn, ProcessEngineSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<HrWarning> warnings = string.IsNullOrWhiteSpace(context.OrderNo) ? [OrderNumberMissing] : [];

        if (context.CandidateId is not { } candidateId)
        {
            var active = await _employments.GetActiveByEmployeeAsync(context.EmployeeId, cancellationToken).ConfigureAwait(false);
            var worksElsewhere = active.Any(employment => employment.LegalEntityId != context.LegalEntityId);

            return new GateFindings(worksElsewhere ? [] : [new GateFailure(DomainGateCodes.Q01, ProcessErrors.HireWithoutCandidate)], false, warnings);
        }

        var candidate = await _candidates.GetByIdAsync(candidateId, cancellationToken).ConfigureAwait(false);

        var processCase = candidate is null
            ? null
            : await _cases
                .GetBySubjectRefAsync(
                    ProcessCodes.HireCandidate,
                    ProcessSubjectRefs.Candidate(candidateId, candidate.LastAppliedAt.TashkentDate()),
                    cancellationToken)
                .ConfigureAwait(false);

        var legacy = processCase is { Legacy: true } || candidate?.LastAppliedAt < ProcessSubjectRefs.StartOfDay(startedOn);

        var factOf = await CandidateFactsAsync(candidateId, processCase, startedOn, settings, cancellationToken).ConfigureAwait(false);

        return new GateFindings(Q01Failures(factOf), legacy, warnings);
    }

    private static IReadOnlyList<GateFailure> Q01Failures(Func<string, FactValue> factOf)
    {
        var missing = new List<Error>();

        if (!factOf(ProcessFactNames.CandidateForm).Holds)
        {
            missing.Add(ProcessErrors.FormMissing);
        }

        var decision = factOf(ProcessFactNames.CandidateCouncilDecision);

        // `unknown`: a legacy case proved only by the «Кенгаш» stage branches as accept (§3.4).
        if (decision is not { Holds: true, Value: "accept" or "conditional" or ProcessCaseFacts.UnknownDecision })
        {
            missing.Add(ProcessErrors.CouncilDecisionMissing);
        }
        else if (decision.Value == "conditional"
            && factOf(ProcessFactNames.CandidateBackgroundCheck) is not { Holds: true, Value: "ok" or "watch" })
        {
            missing.Add(ProcessErrors.BackgroundCheckMissing);
        }

        return [.. missing.Select(error => new GateFailure(DomainGateCodes.Q01, error))];
    }

    /// <summary>
    /// The facts as the candidate's case sees them when there is one: §1.5, Q01 reads the manual r02-10 decision the
    /// same as an F04 decision, and only the case knows that input.
    /// </summary>
    private async Task<Func<string, FactValue>> CandidateFactsAsync(
        Guid candidateId,
        ProcessCase? processCase,
        DateOnly startedOn,
        ProcessEngineSettings settings,
        CancellationToken cancellationToken)
    {
        if (processCase is not null && ProcessCaseFacts.CandidateSubject(processCase) is { } caseSubject)
        {
            var pass = await _passLoader.LoadAsync(startedOn, settings, cancellationToken).ConfigureAwait(false);
            await pass.Definitions.EnsureVersionsAsync([processCase.TemplateVersionId], cancellationToken).ConfigureAwait(false);

            if (pass.Definitions.ForVersion(processCase.TemplateVersionId) is { } definition)
            {
                var snapshot = await _facts
                    .LoadAsync(new ProcessFactQuery { Candidates = [caseSubject], StageBindings = pass.StageBindings }, cancellationToken)
                    .ConfigureAwait(false);

                return new ProcessCaseFacts(processCase, definition, snapshot).Get;
            }
        }

        var subject = new CandidateFactSubject(candidateId);
        var facts = await _facts.LoadAsync(new ProcessFactQuery { Candidates = [subject] }, cancellationToken).ConfigureAwait(false);

        return fact => facts.ForCandidate(subject, fact);
    }

    // ───────────────────────── Q12, Q13 ─────────────────────────

    private async Task<GateFindings> InspectTerminateAsync(GateContext context, CancellationToken cancellationToken)
    {
        var employmentId = context.EmploymentId
            ?? throw new ArgumentException("A termination gate needs the employment.", nameof(context));

        var facts = await _facts
            .LoadAsync(new ProcessFactQuery { EmploymentIds = [employmentId] }, cancellationToken)
            .ConfigureAwait(false);

        var failures = new List<GateFailure>();

        if (!facts.ForEmployment(employmentId, ProcessFactNames.DismissalF09Approved).Holds)
        {
            failures.Add(new GateFailure(DomainGateCodes.Q12, ProcessErrors.HandoverActOpen));
        }

        if (!facts.ForEmployment(employmentId, ProcessFactNames.DismissalF08Approved).Holds)
        {
            failures.Add(new GateFailure(DomainGateCodes.Q13, ProcessErrors.ExitInterviewMissing));
        }

        if (failures.Count == 0)
        {
            return new GateFindings([], false, []);
        }

        // §8.2: no dismissal case → enforced. A legacy one exists only for a blank older than the engine.
        var dismissal = await _cases
            .GetBySubjectRefAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(employmentId), cancellationToken)
            .ConfigureAwait(false);

        return new GateFindings(failures, dismissal is { Legacy: true }, []);
    }

    private sealed record GateFailure(string GateCode, Error Error);

    private sealed record GateFindings(IReadOnlyList<GateFailure> Failures, bool Legacy, IReadOnlyList<HrWarning> Warnings);
}
