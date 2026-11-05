using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Tests.Processes.Engine;

/// <summary>
/// One case on a real template with facts the test sets by hand. Every pass runs a minute after the last one,
/// so «nothing changed» means the pass really wrote nothing, not that the clock stood still, and it fails
/// on any transition the aggregate refused.
/// </summary>
internal sealed class PassHarness
{
    /// <summary>Monday 2026-11-02, 10:00 in Tashkent. Fact times are built from this: they come out of <c>timestamptz</c>.</summary>
    public static readonly DateTimeOffset T0 = new(2026, 11, 2, 5, 0, 0, TimeSpan.Zero);

    /// <summary>What the application clock says at T0: the odd ticks do not survive <c>timestamptz</c>.</summary>
    private static readonly DateTimeOffset Clock = T0.AddTicks(7);

    public static readonly DateOnly StartedOn = new(2026, 11, 2);

    public static readonly Guid User = Guid.CreateVersion7();

    private readonly Dictionary<string, FactValue> _facts = new(StringComparer.Ordinal);

    private PassHarness(ProcessDefinition definition, ProcessCase processCase)
    {
        Definition = definition;
        Case = processCase;
    }

    public ProcessDefinition Definition { get; set; }

    public ProcessCase Case { get; }

    public ProcessCaseSubject Subject { get; set; } = new();

    public bool GatesEnforced { get; set; } = true;

    /// <summary>What a pass gets as the case's usable overrides; the batch does the filtering in production.</summary>
    public IReadOnlyCollection<ProcessGateOverride> GateOverrides { get; set; } = [];

    public DateTimeOffset At { get; private set; } = Clock;

    public static ProcessDefinition Template(string code)
        => ProcessDefinitionValidator.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Processes", "Templates", code + ".json")));

    public static PassHarness HireRequest()
    {
        var id = Guid.CreateVersion7();
        return Start(ProcessCodes.HireRequest, ProcessSubjectKind.VacancyRequest, ProcessSubjectRefs.VacancyRequest(id), new ProcessCaseLinks(VacancyRequestId: id));
    }

    public static PassHarness HireCandidate(bool legacy = false, Guid? employmentId = null)
    {
        var id = Guid.CreateVersion7();
        return Start(
            ProcessCodes.HireCandidate,
            ProcessSubjectKind.Candidate,
            ProcessSubjectRefs.Candidate(id, StartedOn),
            new ProcessCaseLinks(CandidateId: id, EmploymentId: employmentId),
            legacy);
    }

    public static PassHarness FormaliseFromCandidate(Guid? employmentId = null, bool legacy = false, DateOnly? anchor = null)
    {
        var id = Guid.CreateVersion7();
        return Start(
            ProcessCodes.HireFormalise,
            ProcessSubjectKind.Employment,
            ProcessSubjectRefs.FormaliseFromCandidate(id, StartedOn),
            new ProcessCaseLinks(CandidateId: id, EmploymentId: employmentId),
            legacy,
            anchor);
    }

    public static PassHarness FormaliseDirect(bool legacy = false)
    {
        var id = Guid.CreateVersion7();
        return Start(
            ProcessCodes.HireFormalise,
            ProcessSubjectKind.Employment,
            ProcessSubjectRefs.FormaliseFromEmployment(id),
            new ProcessCaseLinks(EmploymentId: id, EmployeeId: Guid.CreateVersion7()),
            legacy);
    }

    public static PassHarness Probation()
    {
        var id = Guid.CreateVersion7();
        return Start(
            ProcessCodes.Probation,
            ProcessSubjectKind.Employment,
            ProcessSubjectRefs.Probation(id),
            new ProcessCaseLinks(EmploymentId: Guid.CreateVersion7(), ProbationId: id, EmployeeId: Guid.CreateVersion7()));
    }

    public static PassHarness Dismissal(DateOnly? manualLastDay = null)
    {
        var id = Guid.CreateVersion7();
        return Start(
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            ProcessSubjectRefs.Dismissal(id),
            new ProcessCaseLinks(EmploymentId: id, EmployeeId: Guid.CreateVersion7()),
            anchor: manualLastDay,
            startedBy: manualLastDay is null ? null : User);
    }

    public PassHarness Holds(string fact, string? value = null, DateTimeOffset? at = null, Guid? linked = null)
    {
        _facts[fact] = new FactValue(true, at ?? T0.AddHours(-1), value, linked);
        return this;
    }

    public PassHarness HoldsWithoutTime(string fact)
    {
        _facts[fact] = new FactValue(true);
        return this;
    }

    public PassHarness Drops(string fact)
    {
        _facts.Remove(fact);
        return this;
    }

    /// <summary>Moves the clock of the next pass on, for a test that crosses a date.</summary>
    public PassHarness Advance(TimeSpan by)
    {
        At += by;
        return this;
    }

    public ProcessCaseStep Step(string id) => Case.FindStep(id) ?? throw new InvalidOperationException($"No step {id}");

    public ProcessPassResult Pass()
    {
        At = At.AddMinutes(1);
        var result = ProcessPass.Apply(
            Case,
            new ProcessPassInput(Definition, Snapshot(), Subject, WorkingDayCalendar.MondayToFriday, DayOfWeek.Thursday, GatesEnforced, At)
            {
                GateOverrides = GateOverrides,
            });

        result.Problems.ShouldBeEmpty();
        return result;
    }

    /// <summary>«Bajarildi» by a user; the step must be Active.</summary>
    public void DoneByUser(string stepId, string? payload = null)
        => Case.CompleteStepManually(stepId, User, payload, evidenceSha256: null, At).IsSuccess.ShouldBeTrue();

    /// <summary>
    /// What saving and loading does to the case: <c>jsonb</c> re-spaces the text, <c>timestamptz</c> keeps microseconds.
    /// </summary>
    public void SaveAndReload()
    {
        foreach (var step in Case.Steps)
        {
            if (step.Payload is { } payload)
            {
                var respaced = payload.Replace("\":", "\": ", StringComparison.Ordinal);
                respaced.ShouldNotBe(payload);
                Write(step, nameof(ProcessCaseStep.Payload), respaced);
            }

            if (step.DoneAt is { } doneAt)
            {
                Write(step, nameof(ProcessCaseStep.DoneAt), (DateTimeOffset?)ToMicroseconds(doneAt));
            }
        }

        Write(Case, nameof(ProcessCase.CreatedAt), ToMicroseconds(Case.CreatedAt));
    }

    /// <summary>Asserts a further pass writes nothing, including after the round trip — otherwise SSE fires forever (§7.4 p.6).</summary>
    public void ShouldBeStable()
    {
        SaveAndReload();
        var version = Case.Version;
        var updatedAt = Case.UpdatedAt;

        Pass().Changed.ShouldBeFalse();

        Case.Version.ShouldBe(version);
        Case.UpdatedAt.ShouldBe(updatedAt);
    }

    private static PassHarness Start(
        string code,
        ProcessSubjectKind kind,
        string subjectRef,
        ProcessCaseLinks links,
        bool legacy = false,
        DateOnly? anchor = null,
        Guid? startedBy = null)
    {
        var definition = Template(code);
        var plan = definition.Steps
            .Select((step, index) => new ProcessStepPlan(step.Id, index + 1, step.Column, step.Role == ProcessRoles.System ? null : step.Role))
            .ToList();

        var start = new ProcessCaseStart(code, Guid.CreateVersion7(), kind, subjectRef, StartedOn, legacy, links, AnchorDate: anchor, StartedBy: startedBy);
        return new PassHarness(definition, ProcessCase.Start(start, plan, Clock).Value);
    }

    /// <summary>The one fact map stands for every subject the case has: which ones it can see is the case's routing.</summary>
    private ProcessFactSnapshot Snapshot()
    {
        var vacancyRequests = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();
        var candidates = new Dictionary<CandidateFactSubject, IReadOnlyDictionary<string, FactValue>>();
        var employments = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();
        var probations = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();

        if (Case.VacancyRequestId is { } request)
        {
            vacancyRequests[request] = _facts;
        }

        if (ProcessCaseFacts.CandidateSubject(Case) is { } candidate)
        {
            candidates[candidate] = _facts;
        }

        if (Case.EmploymentId is { } employment)
        {
            employments[employment] = _facts;
        }

        if (Case.ProbationId is { } probation)
        {
            probations[probation] = _facts;
        }

        return new ProcessFactSnapshot(vacancyRequests, candidates, employments, probations, new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>());
    }

    private static DateTimeOffset ToMicroseconds(DateTimeOffset value)
        => new(value.UtcTicks - (value.UtcTicks % 10), TimeSpan.Zero);

    private static void Write<T>(object target, string property, T value)
        => target.GetType().GetProperty(property)!.SetValue(target, value);
}
