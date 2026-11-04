namespace Bonvi.Domain.Processes;

/*
  Process engine enumerations (docs/SPEC-mening-ishlarim.md §6). All are `short` based —
  the columns are `smallint`. The numbers live in the database and in the template JSON:
  the number of an existing member is NEVER changed.
*/

/// <summary>Status of a process template version (same numbering as blank template versions).</summary>
public enum ProcessTemplateVersionStatus : short
{
    /// <summary>Draft — not used for new cases.</summary>
    Draft = 1,

    /// <summary>Published — new cases start with this version. At most one per template.</summary>
    Published = 2,

    /// <summary>Retired — a newer version was published; running cases keep reading it.</summary>
    Retired = 3,
}

/// <summary>Who or what a process case is about (§6.2 <c>subject_kind</c>).</summary>
public enum ProcessSubjectKind : short
{
    /// <summary>A vacancy request (<c>vacancy_request_id</c>).</summary>
    VacancyRequest = 1,

    /// <summary>A candidate (<c>candidate_id</c>).</summary>
    Candidate = 2,

    /// <summary>An employment (<c>employment_id</c>, plus <c>employee_id</c>).</summary>
    Employment = 3,

    /// <summary>Not bound to a single subject.</summary>
    None = 4,
}

/// <summary>Lifecycle of a process case (§6.2 <c>status</c>).</summary>
public enum ProcessCaseStatus : short
{
    /// <summary>Running — the reconciler keeps its steps up to date.</summary>
    Active = 1,

    /// <summary>Every non-optional step is Done/Skipped, or an end fact holds (§7.5).</summary>
    Completed = 2,

    /// <summary>Cancelled by a manager (reason) or because the subject was deleted.</summary>
    Cancelled = 3,
}

/// <summary>State of one step of a case (§6.3 <c>state</c>).</summary>
public enum ProcessStepState : short
{
    /// <summary>A non-optional prerequisite is not Done/Skipped yet, or the <c>when</c> fact is unknown.</summary>
    Locked = 1,

    /// <summary>Prerequisites met — the holder's task.</summary>
    Active = 2,

    /// <summary>Waiting for another case (e.g. r02-13 while the formalise case's step is open).</summary>
    /// <remarks>«Waiting for another person» is computed at read time and never stored (§7.4 p.4).</remarks>
    Waiting = 3,

    /// <summary>Prerequisites met but a blocking gate fails (<c>blocked_gate_code</c>).</summary>
    Blocked = 4,

    /// <summary>Closed — by a fact, by hand or by the system (<see cref="ProcessStepResolution"/>).</summary>
    Done = 5,

    /// <summary>Not needed — condition, legacy, user decision or case end.</summary>
    Skipped = 6,
}

/// <summary>How a Done or Skipped step was closed (§6.3 <c>resolution</c>).</summary>
public enum ProcessStepResolution : short
{
    /// <summary>Done: the completion fact holds. Reverts to open when the fact disappears.</summary>
    Fact = 1,

    /// <summary>Done: a user pressed «Bajarildi». Never reverted by the reconciler.</summary>
    Manual = 2,

    /// <summary>Done: an <c>auto</c> step closed by the system from a fact.</summary>
    Auto = 3,

    /// <summary>Skipped: the step's <c>when</c> condition is false.</summary>
    Condition = 4,

    /// <summary>Skipped: a legacy case step skipped by its holder (<c>legacySkippable</c>).</summary>
    Legacy = 5,

    /// <summary>Skipped: a user pressed «Kerak emas» (<c>skippable</c>).</summary>
    UserSkip = 6,

    /// <summary>Skipped: the case ended while the step was still open.</summary>
    CaseEnded = 7,
}

/// <summary>Kind of a working-day calendar row (§6.10).</summary>
public enum ProcessHolidayKind : short
{
    /// <summary>A non-working day (public holiday or a transferred day off).</summary>
    Holiday = 1,

    /// <summary>A working day that would otherwise be a weekend (a working Saturday).</summary>
    WorkingDay = 2,
}
