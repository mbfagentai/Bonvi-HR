using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// The facts of ONE case: routes a fact name to the subject it is about and adds what the fact catalogue
/// cannot know — the derived facts and the values people gave by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routing</b> is by name prefix: <c>vr.*</c> → the request, <c>cand.*</c> → the application,
/// <c>prob.*</c> → the probation, <c>emp.*</c> / <c>dis.*</c> → the employment. A case that has no such
/// subject yet simply does not have the fact.
/// </para>
/// <para>
/// <b>Stand-ins.</b> A step closed by «Bajarildi» whose completion names <c>manualValue</c> answers its
/// fact from the manual input (§1.5: F04 / F03 are served by HRMentor, the manual fallback must drive
/// the branches meanwhile). In a legacy case the «Кенгаш» stage answers the council decision with
/// <c>unknown</c>, which branches as accept (§3.4).
/// </para>
/// </remarks>
public sealed class ProcessCaseFacts
{
    /// <summary>Council decision of a legacy case proved only by its pipeline stage.</summary>
    public const string UnknownDecision = "unknown";

    public const string BranchAccept = "accept";

    public const string BranchDecline = "decline";

    private readonly ProcessCase _case;
    private readonly ProcessDefinition _definition;
    private readonly ProcessFactSnapshot _snapshot;
    private readonly CandidateFactSubject? _candidate;

    public ProcessCaseFacts(ProcessCase processCase, ProcessDefinition definition, ProcessFactSnapshot snapshot)
    {
        _case = processCase;
        _definition = definition;
        _snapshot = snapshot;
        _candidate = CandidateSubject(processCase);
    }

    /// <summary>The fact subject of the case's candidate — the same key is used to load and to read.</summary>
    public static CandidateFactSubject? CandidateSubject(ProcessCase processCase)
    {
        ArgumentNullException.ThrowIfNull(processCase);

        if (processCase.CandidateId is not { } candidateId)
        {
            return null;
        }

        return new CandidateFactSubject(
            candidateId,
            ProcessSubjectRefs.AppliedOn(processCase.SubjectRef) is { } appliedOn ? ProcessSubjectRefs.StartOfDay(appliedOn) : null);
    }

    /// <summary>The fact as this case sees it.</summary>
    public FactValue Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return name switch
        {
            ProcessFactNames.CaseStarted => new FactValue(true, _case.CreatedAt),
            ProcessFactNames.OfferBranch => OfferBranch(),
            _ => ManualStandIn(name) ?? LegacyDecision(name) ?? Stored(name),
        };
    }

    /// <summary>Whether <paramref name="completion"/> holds; the value and time are those of the fact that closed it.</summary>
    public FactValue Evaluate(ProcessCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        var main = Get(completion.Fact);

        if (completion.ByValue.Count > 0)
        {
            return main is { Holds: true, Value: { } value } && completion.ByValue.TryGetValue(value, out var branch)
                ? Evaluate(branch)
                : FactValue.NotHeld;
        }

        if (main.Holds && Passes(completion, main.Value))
        {
            var others = completion.AllOf.Select(Get).ToList();
            if (others.TrueForAll(other => other.Holds))
            {
                return main with { OccurredAt = others.Select(other => other.OccurredAt).Append(main.OccurredAt).Max() };
            }
        }

        return _case.Legacy
            ? completion.LegacyAnyOf.Select(Get).FirstOrDefault(fact => fact.Holds) ?? FactValue.NotHeld
            : FactValue.NotHeld;
    }

    private static bool Passes(ProcessCompletion completion, string? value)
    {
        if (completion.EqualsValue is { } expected && expected.ToString() != value)
        {
            return false;
        }

        return completion.InValues.Count == 0 || completion.InValues.Any(item => item.ToString() == value);
    }

    private FactValue Stored(string name)
    {
        var family = name[..name.IndexOf('.', StringComparison.Ordinal)];

        return family switch
        {
            "vr" when _case.VacancyRequestId is { } id => _snapshot.ForVacancyRequest(id, name),
            "cand" when _candidate is { } subject => _snapshot.ForCandidate(subject, name),
            "prob" when _case.ProbationId is { } id => _snapshot.ForProbation(id, name),
            "emp" or "dis" when _case.EmploymentId is { } id => _snapshot.ForEmployment(id, name),
            _ => FactValue.NotHeld,
        };
    }

    /// <summary>The input of a manually closed step whose completion names this fact and a <c>manualValue</c>.</summary>
    private FactValue? ManualStandIn(string name)
    {
        var definition = _definition.Steps.FirstOrDefault(step => step.Completion is { ManualValue: not null } completion && completion.Fact == name);
        if (definition is null
            || _case.FindStep(definition.Id) is not { State: ProcessStepState.Done, Resolution: ProcessStepResolution.Manual } step)
        {
            return null;
        }

        var value = ProcessStepPayload.ReadInput(step.Payload, definition.Completion!.ManualValue!);
        return value is null ? null : new FactValue(true, step.DoneAt, value, Stored(name).LinkedId);
    }

    private FactValue? LegacyDecision(string name)
    {
        if (!_case.Legacy || name != ProcessFactNames.CandidateCouncilDecision || Stored(name).Holds)
        {
            return null;
        }

        var stage = Stored(ProcessFacts.CandStage(StageBindings.Council));
        return stage.Holds ? stage with { Value = UnknownDecision } : null;
    }

    /// <summary>§3.4: accept / conditional + F03 ok or watch → accept; reserve, reject, F03 notRecommended → decline.</summary>
    private FactValue OfferBranch()
    {
        var decision = Get(ProcessFactNames.CandidateCouncilDecision);
        if (!decision.Holds)
        {
            return FactValue.NotHeld;
        }

        switch (decision.Value)
        {
            case "accept" or UnknownDecision:
                return new FactValue(true, decision.OccurredAt, BranchAccept);
            case "reserve" or "reject":
                return new FactValue(true, decision.OccurredAt, BranchDecline);
            case "conditional":
                var check = Get(ProcessFactNames.CandidateBackgroundCheck);
                return check switch
                {
                    { Holds: true, Value: "ok" or "watch" } => new FactValue(true, check.OccurredAt, BranchAccept),
                    { Holds: true, Value: "notRecommended" } => new FactValue(true, check.OccurredAt, BranchDecline),
                    _ => FactValue.NotHeld,
                };
            default:
                return FactValue.NotHeld;
        }
    }
}
