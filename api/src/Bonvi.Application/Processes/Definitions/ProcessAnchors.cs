namespace Bonvi.Application.Processes.Definitions;

/// <summary>
/// Dates a due rule may count from (<c>due.from</c>, SPEC-mening-ishlarim §3.1). The pass supplies the known
/// ones per case; an anchor it cannot supply makes the due null («muddatsiz»).
/// </summary>
public static class ProcessAnchors
{
    public const string CaseStartedOn = "case.startedOn";

    /// <summary>Per process: planned start (hire-formalise) or last working day (dismissal).</summary>
    public const string CaseAnchorDate = "case.anchorDate";

    public const string CandidateAppliedOn = "candidate.appliedOn";

    public const string EmploymentHireDate = "employment.hireDate";

    /// <summary>Falls back to <see cref="EmploymentHireDate"/> when the order date is unknown.</summary>
    public const string EmploymentHireOrderDate = "employment.hireOrderDate";

    public const string EmploymentTerminationDate = "employment.terminationDate";

    public const string ProbationStartDate = "probation.startDate";

    public const string ProbationEndDate = "probation.endDate";

    /// <summary>
    /// HR-F05 answer <c>startDate</c> (or the manual r02-11 input <c>startDate</c>). Used by the §3.4 table
    /// (r02-12, r02-13) although the §3.1 anchor list omits it.
    /// </summary>
    public const string OfferStartDate = "f05.startDate";

    private const string StepPrefix = "step:";

    private const string StepSuffix = ".doneOn";

    public static IReadOnlySet<string> Fixed { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CaseStartedOn, CaseAnchorDate, CandidateAppliedOn, EmploymentHireDate, EmploymentHireOrderDate,
        EmploymentTerminationDate, ProbationStartDate, ProbationEndDate, OfferStartDate,
    };

    /// <summary><c>step:r02-3.doneOn</c> — the Tashkent date the step became Done.</summary>
    public static string StepDoneOn(string stepId) => $"{StepPrefix}{stepId}{StepSuffix}";

    /// <summary>Reads the step id out of a <c>step:&lt;id&gt;.doneOn</c> anchor.</summary>
    public static bool TryGetStepId(string anchor, out string stepId)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (anchor.StartsWith(StepPrefix, StringComparison.Ordinal)
            && anchor.EndsWith(StepSuffix, StringComparison.Ordinal)
            && anchor.Length > StepPrefix.Length + StepSuffix.Length)
        {
            stepId = anchor[StepPrefix.Length..^StepSuffix.Length];
            return true;
        }

        stepId = string.Empty;
        return false;
    }

    /// <summary>A fixed anchor or a well-formed step anchor (whether the step exists is the validator's job).</summary>
    public static bool IsKnown(string anchor) => Fixed.Contains(anchor) || TryGetStepId(anchor, out _);
}
