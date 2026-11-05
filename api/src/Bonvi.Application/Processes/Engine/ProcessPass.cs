using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Engine;

/// <summary>What a pass knows about a case's subject besides its facts (anchors §3.1, holders §7.7, gates §8.3).</summary>
public sealed record ProcessCaseSubject
{
    /// <summary><c>false</c> → the subject was deleted, the case is Cancelled (§6.9).</summary>
    public bool Exists { get; init; } = true;

    public Guid? DepartmentId { get; init; }

    /// <summary>Department head who has a user; <c>null</c> → the step's <c>fallbackRole</c> holds it.</summary>
    public Guid? DepartmentHeadWithUser { get; init; }

    /// <summary>The case employee when they have a user; <c>null</c> → <c>fallbackRole</c>.</summary>
    public Guid? SubjectEmployeeWithUser { get; init; }

    public DateOnly? HireDate { get; init; }

    public DateOnly? HireOrderDate { get; init; }

    public DateOnly? TerminationDate { get; init; }

    public EmploymentStatus? EmploymentStatus { get; init; }

    public DateOnly? ProbationStart { get; init; }

    public DateOnly? ProbationEnd { get; init; }

    /// <summary>Q03.</summary>
    public bool EmployeeHasPinfl { get; init; }

    /// <summary>Q04: step <c>mm</c> of the child hire-formalise case is Done.</summary>
    public bool SafetyInstructionDone { get; init; }
}

/// <summary>Everything one pass needs for one case — no I/O, so the algorithm is unit-testable.</summary>
public sealed record ProcessPassInput(
    ProcessDefinition Definition,
    ProcessFactSnapshot Snapshot,
    ProcessCaseSubject Subject,
    IWorkingDayCalendar Calendar,
    DayOfWeek CouncilDay,
    bool GatesEnforced,
    DateTimeOffset At)
{
    /// <summary>The usable overrides of this case (§8.4). Whoever closes the step spends them, not the pass.</summary>
    public IReadOnlyCollection<ProcessGateOverride> GateOverrides { get; init; } = [];
}

/// <param name="Changed">Something was written on the case: the caller must save.</param>
/// <param name="OfferAccepted">r02-11 is Done on the accept branch: starter §3.2 hire-formalise (a).</param>
/// <param name="Problems">Transitions the aggregate refused — a programming error, logged and not fatal for the other cases.</param>
public sealed record ProcessPassResult(bool Changed, bool OfferAccepted, IReadOnlyList<string> Problems);

/// <summary>
/// One pass over one case (docs/SPEC-mening-ishlarim.md §7.4). The aggregate holds the state rules;
/// here is what the definition and the facts say should happen.
/// </summary>
/// <remarks>
/// Order matters and is deliberate:
/// <list type="number">
/// <item>Facts first (<c>when</c>, then completion) for ALL steps — a fact wins over order, so a later step can
/// be Done before an earlier one, and the prerequisites of step 3 must see every closure of this pass.</item>
/// <item>Case fields and the dues: <c>step:&lt;id&gt;.doneOn</c> anchors need the closures of step 1, and the probation
/// cut-off needs the due.</item>
/// <item>Open steps: Locked / Blocked / Active.</item>
/// <item>End rules (§7.5), which read the states above.</item>
/// </list>
/// A step closed by a person (manual Done, «Kerak emas», legacy skip) is never touched.
/// <para>Waiting (state 3) is not written in Phase 1: its only example, r02-13 while <c>mm</c> is open, is the Q04 block of §8.3.</para>
/// </remarks>
public static class ProcessPass
{
    public static ProcessPassResult Apply(ProcessCase processCase, ProcessPassInput input)
    {
        ArgumentNullException.ThrowIfNull(processCase);
        ArgumentNullException.ThrowIfNull(input);

        return new Run(processCase, input).Execute();
    }

    private sealed class Run
    {
        private readonly ProcessCase _case;
        private readonly ProcessPassInput _input;
        private readonly ProcessCaseFacts _facts;
        private readonly HashSet<string> _settled = new(StringComparer.Ordinal);
        private readonly HashSet<string> _whenUnknown = new(StringComparer.Ordinal);
        private readonly List<string> _problems = [];
        private bool _changed;

        public Run(ProcessCase processCase, ProcessPassInput input)
        {
            _case = processCase;
            _input = input;
            _facts = new ProcessCaseFacts(processCase, input.Definition, input.Snapshot);
        }

        private ProcessStep? DefinitionOf(string stepId) => _input.Definition.FindStep(stepId);

        public ProcessPassResult Execute()
        {
            if (_case.Status != ProcessCaseStatus.Active)
            {
                return new ProcessPassResult(false, false, []);
            }

            if (!_input.Subject.Exists)
            {
                Apply("cancel", _case.CancelBecauseSubjectDeleted(_input.At));
                return Result(offerAccepted: false);
            }

            var dismissalWasUnderWay = IsFactDone(ProcessStepIds.ActOpened) || IsFactDone(ProcessStepIds.Terminated);

            foreach (var step in _input.Definition.Steps)
            {
                ApplyFacts(step);
            }

            RefreshCaseFields();

            var dueContext = ProcessDueContext.ForCase(Anchors(), _input.Calendar, _input.CouncilDay, _case.Legacy);
            foreach (var step in _input.Definition.Steps)
            {
                var due = ProcessDueEvaluator.Evaluate(step.Due, dueContext);
                OpenState(step, due, dueContext);
                Refresh(step, due);
                LinkRecord(step);
            }

            var offerAccepted = _case.FindStep(ProcessStepIds.Offer) is { State: ProcessStepState.Done }
                && _facts.Get(ProcessFactNames.OfferBranch).Value == ProcessCaseFacts.BranchAccept;

            if (ProcessCaseEndRules.Evaluate(_case, _input.Definition, _facts, _input.Subject, dismissalWasUnderWay) is { } end)
            {
                Apply("complete", _case.Complete(end.Outcome, _input.At));
            }

            return Result(offerAccepted);
        }

        private ProcessPassResult Result(bool offerAccepted) => new(_changed, offerAccepted, _problems);

        private bool IsFactDone(string stepId) => _case.FindStep(stepId) is { IsFactDone: true };

        // ───────────────────────── Step 1: facts ─────────────────────────

        private void ApplyFacts(ProcessStep definition)
        {
            if (_case.FindStep(definition.Id) is not { } step)
            {
                return;
            }

            if (step.IsUserClosed)
            {
                _settled.Add(definition.Id);
                return;
            }

            var condition = WhenState(definition);
            if (condition == false)
            {
                Revert(step);
                Apply(definition.Id, _case.SkipStepByCondition(definition.Id, _input.At));
                _settled.Add(definition.Id);
                return;
            }

            if (condition is null)
            {
                Revert(step);
                _whenUnknown.Add(definition.Id);
                return;
            }

            var completion = definition.Completion is null ? FactValue.NotHeld : _facts.Evaluate(definition.Completion);
            if (completion.Holds)
            {
                MarkDone(definition, step, completion);
                _settled.Add(definition.Id);
                return;
            }

            Revert(step);
        }

        /// <summary><c>true</c> — applies; <c>false</c> — value differs; <c>null</c> — not known yet.</summary>
        private bool? WhenState(ProcessStep definition)
        {
            if (definition.When is not { } when)
            {
                return true;
            }

            var fact = _facts.Get(when.Fact);
            if (!fact.Holds || fact.Value is null)
            {
                return null;
            }

            return when.EqualsValue.ToString() == fact.Value;
        }

        private void MarkDone(ProcessStep definition, ProcessCaseStep step, FactValue fact)
        {
            var resolution = definition.Kind == ProcessStepKind.Auto ? ProcessStepResolution.Auto : ProcessStepResolution.Fact;

            // A fact without its own time keeps the first time it was seen: the pass time would differ every run.
            var doneAt = fact.OccurredAt ?? (step.IsFactDone ? step.DoneAt : null) ?? _input.At;
            var payload = PayloadOf(definition, fact, doneAt);

            if (step.State == ProcessStepState.Done
                && step.Resolution == resolution
                && step.DoneAt == doneAt
                && ProcessStepPayload.AreEqual(step.Payload, payload))
            {
                return;
            }

            Apply(definition.Id, _case.MarkStepDoneByFact(definition.Id, resolution, doneAt, payload, _input.At));
        }

        private string? PayloadOf(ProcessStep definition, FactValue fact, DateTimeOffset doneAt)
            => definition.Id == ProcessStepIds.RequestClosed
                ? ProcessStepPayload.ForDaysToClose(doneAt.TashkentDate().DayNumber - _case.StartedOn.DayNumber)
                : ProcessStepPayload.ForFactValue(fact.Value);

        private void Revert(ProcessCaseStep step)
        {
            if (step.IsFactDone)
            {
                Apply(step.StepCode, _case.RevertStepFact(step.StepCode, _input.At));
            }
        }

        // ───────────────────────── Step 2: case fields and dues ─────────────────────────

        private void RefreshCaseFields()
        {
            var subject = _input.Subject;
            _changed |= _case.RefreshDepartment(subject.DepartmentId, _input.At);

            var anchor = _case.ProcessCode switch
            {
                ProcessCodes.HireFormalise when ProcessSubjectRefs.IsDirectHire(_case.SubjectRef) => subject.HireDate,
                ProcessCodes.HireFormalise => DateOf(_facts.Get(ProcessFacts.CandF05StartDate)) ?? _case.AnchorDate ?? subject.HireDate,

                // A manual start's lastWorkDay outranks everything (§3.7).
                ProcessCodes.Dismissal when _case.StartedBy is not null => _case.AnchorDate,
                ProcessCodes.Dismissal => DateOf(_facts.Get(ProcessFacts.DisF08LeaveDate)) ?? subject.TerminationDate ?? _case.AnchorDate,
                _ => _case.AnchorDate,
            };

            _changed |= _case.RefreshAnchorDate(anchor, _input.At);
        }

        private Dictionary<string, DateOnly> Anchors()
        {
            var anchors = new Dictionary<string, DateOnly>(StringComparer.Ordinal)
            {
                [ProcessAnchors.CaseStartedOn] = _case.StartedOn,
            };

            var subject = _input.Subject;
            Put(anchors, ProcessAnchors.CaseAnchorDate, _case.AnchorDate);
            Put(anchors, ProcessAnchors.CandidateAppliedOn, ProcessSubjectRefs.AppliedOn(_case.SubjectRef));
            Put(anchors, ProcessAnchors.EmploymentHireDate, subject.HireDate);
            Put(anchors, ProcessAnchors.EmploymentHireOrderDate, subject.HireOrderDate);
            Put(anchors, ProcessAnchors.EmploymentTerminationDate, subject.TerminationDate);
            Put(anchors, ProcessAnchors.ProbationStartDate, subject.ProbationStart);
            Put(anchors, ProcessAnchors.ProbationEndDate, subject.ProbationEnd);
            Put(anchors, ProcessAnchors.OfferStartDate, OfferStartDate());

            foreach (var step in _case.Steps)
            {
                if (step is { State: ProcessStepState.Done, DoneAt: { } doneAt })
                {
                    anchors[ProcessAnchors.StepDoneOn(step.StepCode)] = doneAt.TashkentDate();
                }
            }

            return anchors;
        }

        /// <summary>HR-F05 <c>startDate</c>, or the manual r02-11 input while F05 is not served (§1.5).</summary>
        private DateOnly? OfferStartDate()
            => DateOf(_facts.Get(ProcessFacts.CandF05StartDate))
                ?? ProcessStepPayload.ReadDateInput(_case.FindStep(ProcessStepIds.Offer)?.Payload, ProcessStepPayload.StartDateInput);

        private static void Put(Dictionary<string, DateOnly> anchors, string name, DateOnly? date)
        {
            if (date is { } value)
            {
                anchors[name] = value;
            }
        }

        private static DateOnly? DateOf(FactValue fact) => fact.Holds ? ParseDate(fact.Value) : null;

        private static DateOnly? ParseDate(string? text)
            => DateOnly.TryParseExact(text, "yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var date)
                ? date
                : null;

        // ───────────────────────── Step 3: open steps ─────────────────────────

        private void OpenState(ProcessStep definition, DateOnly? due, ProcessDueContext dueContext)
        {
            if (_settled.Contains(definition.Id) || _case.FindStep(definition.Id) is null)
            {
                return;
            }

            // The probation cut-off comes before an unknown `when`: p03-14 must not wait for a test that is not held.
            if (ProcessDueEvaluator.IsCutOff(definition, due, dueContext))
            {
                Apply(definition.Id, _case.SkipStepByCondition(definition.Id, _input.At));
                return;
            }

            if (_whenUnknown.Contains(definition.Id) || !PrerequisitesMet(definition))
            {
                Apply(definition.Id, _case.LockStep(definition.Id, _input.At));
                return;
            }

            var gate = ProcessEngineGates.FirstFailing(
                definition,
                _case,
                _facts,
                _input.Subject,
                _input.GatesEnforced,
                _input.GateOverrides,
                _input.At.TashkentDate());
            Apply(definition.Id, gate is null
                ? _case.ActivateStep(definition.Id, _input.At)
                : _case.BlockStep(definition.Id, gate, _input.At));
        }

        private bool PrerequisitesMet(ProcessStep definition)
            => definition.Prereqs.All(id => DefinitionOf(id) is { Optional: true } || _case.FindStep(id) is null or { IsClosed: true });

        // ───────────────────────── Holder, due, record link ─────────────────────────

        /// <summary>A closed step keeps the holder it was closed under; the due follows its anchors.</summary>
        private void Refresh(ProcessStep definition, DateOnly? due)
        {
            if (_case.FindStep(definition.Id) is not { } step)
            {
                return;
            }

            var (role, employee) = step.IsClosed ? (step.HolderRole, step.HolderEmployeeId) : Holder(definition);
            Apply(definition.Id, _case.RefreshStep(definition.Id, role, employee, due, _input.At));
        }

        /// <summary>§7.7. Who of an office role sees the task is a read-time question; here — who holds it.</summary>
        private (string? Role, Guid? Employee) Holder(ProcessStep definition)
        {
            Guid? person = definition.Role switch
            {
                ProcessRoles.System => null,
                ProcessRoles.DepartmentHead => _input.Subject.DepartmentHeadWithUser,
                ProcessRoles.SubjectEmployee => _input.Subject.SubjectEmployeeWithUser,
                _ => null,
            };

            return definition.Role switch
            {
                ProcessRoles.System => (null, null),
                ProcessRoles.DepartmentHead or ProcessRoles.SubjectEmployee when person is null => (definition.FallbackRole, null),
                _ => (definition.Role, person),
            };
        }

        private void LinkRecord(ProcessStep definition)
        {
            if (_case.FindStep(definition.Id) is not { } step || RecordOf(definition) is not { } id)
            {
                return;
            }

            var isBlank = definition.Blank is not null && !RecordBlanks.Contains(definition.Blank);
            Apply(definition.Id, _case.LinkStep(
                definition.Id,
                isBlank ? id : step.LinkedBlankSubmissionId,
                isBlank ? step.LinkedRecordId : id,
                _input.At));
        }

        /// <summary>The proof of the completion; while open, the blank the step is working on.</summary>
        /// <remarks>
        /// A completion with <c>allOf</c> can span two blanks: p03-85 is the F06's day 85 AND the F07 it is named for.
        /// Its main fact names the F06, and the step's blank chip and «open» button would then show the wrong
        /// document, so the step's own blank wins there.
        /// </remarks>
        private Guid? RecordOf(ProcessStep definition)
        {
            var own = definition.Blank is { } blank && BlankOpenFacts.TryGetValue(blank, out var fact)
                ? _facts.Get(fact).LinkedId
                : null;

            if (definition.Completion is { } completion && _facts.Evaluate(completion) is { Holds: true, LinkedId: { } closing })
            {
                return completion.AllOf.Count > 0 ? own ?? closing : closing;
            }

            return own;
        }

        // ───────────────────────── Aggregate results ─────────────────────────

        private void Apply(string what, Result<bool> result)
        {
            if (result.IsFailure)
            {
                _problems.Add($"{what}: {result.Error.Code}");
                return;
            }

            _changed |= result.Value;
        }

        private void Apply(string what, Result result)
        {
            if (result.IsFailure)
            {
                _problems.Add($"{what}: {result.Error.Code}");
                return;
            }

            _changed = true;
        }
    }

    /// <summary>Blank codes that are really records of another module (§2): their id is a record, not a submission.</summary>
    private static readonly HashSet<string> RecordBlanks = new(StringComparer.Ordinal) { "HR-F01", "HR-F02" };

    /// <summary>Blank code → the fact that names its submission before the step's own completion holds.</summary>
    private static readonly Dictionary<string, string> BlankOpenFacts = new(StringComparer.Ordinal)
    {
        ["HR-F03"] = ProcessFacts.CandF03,
        ["HR-F04"] = ProcessFacts.CandInF04,
        ["HR-F05"] = ProcessFacts.CandF05StartDate,
        ["HR-F06"] = ProcessFacts.ProbF06,
        ["HR-F07"] = ProcessFacts.ProbF07Submitted,
        ["HR-F08"] = ProcessFacts.DisF08LeaveDate,
        ["HR-F09"] = ProcessFacts.DisF09Opened,
        ["HR-F11"] = ProcessFacts.EmpF11,
    };
}
