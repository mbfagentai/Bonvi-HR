namespace Bonvi.Application.Processes.Definitions;

/// <summary>
/// Names a template may use in <c>completion.fact</c>, <c>when.fact</c> and the fact lists of
/// <c>completion.args</c> (SPEC-mening-ishlarim §7.2). Anything else is rejected by
/// <see cref="ProcessDefinitionValidator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes: a plain name (<c>vr.decided</c>) and a family with one parameter after a colon
/// (<c>cand.f05Step:docsHr2</c>). A family accepts only the parameters listed in
/// <see cref="FamilyParameters"/> — a typo in a blank step id is a startup error, not a step that never closes.
/// </para>
/// <para>
/// <b>Derived facts</b> (<see cref="CaseStarted"/>, <see cref="OfferBranch"/>) are not read from a table by
/// <c>IProcessFactSource</c>; the pass computes them from the case and the facts above. They exist because the
/// §3 tables need them («case started», «branch accept») and §7.2 has no row for them.
/// </para>
/// </remarks>
public static class ProcessFactNames
{
    /// <summary>Derived: always true once the case exists; occurred at = case creation (ex-1).</summary>
    public const string CaseStarted = "case.started";

    public const string VacancyRequestCreated = "vr.created";

    /// <summary>Value <c>approved</c> / <c>rejected</c> / <c>cancelled</c>.</summary>
    public const string VacancyRequestDecided = "vr.decided";

    public const string VacancyRequestFirstCandidate = "vr.firstCandidate";

    public const string VacancyRequestFilled = "vr.filled";

    /// <summary>The candidate entered the stage bound to <c>contacted</c> (§6.5).</summary>
    public const string CandidateContacted = "cand.contacted";

    public const string CandidateForm = "cand.f02";

    public const string CandidateInCouncilList = "cand.inF04";

    /// <summary>Value <c>accept</c> / <c>conditional</c> / <c>reserve</c> / <c>reject</c> (F04 row or manual r02-10).</summary>
    public const string CandidateCouncilDecision = "cand.f04Decision";

    /// <summary>Value <c>ok</c> / <c>watch</c> / <c>notRecommended</c> (F03 <c>hr4Check</c> or manual r02-8).</summary>
    public const string CandidateBackgroundCheck = "cand.f03";

    /// <summary>Value = candidate status number.</summary>
    public const string CandidateStatus = "cand.status";

    public const string CandidateHired = "cand.hired";

    /// <summary>
    /// Derived: value <c>accept</c> when the council decision is <c>accept</c> (legacy stage value <c>unknown</c>
    /// counts as accept), or <c>conditional</c> with the background check <c>ok</c>/<c>watch</c>; value
    /// <c>decline</c> for <c>reserve</c>, <c>reject</c> or check <c>notRecommended</c>. Unknown while either input
    /// is still missing (r02-11 … r02-13, §3.4).
    /// </summary>
    public const string OfferBranch = "cand.offerBranch";

    public const string EmploymentHired = "emp.hired";

    public const string EmploymentHireOrder = "emp.hireOrder";

    public const string EmploymentAttendance = "emp.attendance";

    public const string EmploymentF11 = "emp.f11";

    public const string ProbationF06 = "prob.f06";

    public const string ProbationF06Group1 = "prob.f06Group1";

    public const string ProbationF07Submitted = "prob.f07Submitted";

    /// <summary>Value = probation status number.</summary>
    public const string ProbationResolved = "prob.resolved";

    public const string DismissalF09Opened = "dis.f09Opened";

    public const string DismissalF09Approved = "dis.f09Approved";

    public const string DismissalF08Approved = "dis.f08Approved";

    public const string DismissalTerminated = "dis.terminated";

    /// <summary>Family: <c>cand.stage:&lt;binding&gt;</c> — a stage entry for the bound pipeline stage.</summary>
    public const string CandidateStageFamily = "cand.stage";

    /// <summary>Family: <c>cand.f05Step:&lt;stepId&gt;</c> — that HR-F05 step Signed/Approved.</summary>
    public const string CandidateF05StepFamily = "cand.f05Step";

    /// <summary>Family: <c>prob.f06Step:&lt;stepId&gt;</c> — that HR-F06 step Signed.</summary>
    public const string ProbationF06StepFamily = "prob.f06Step";

    /// <summary>Family: <c>prob.f07Step:&lt;stepId&gt;</c> — that HR-F07 step Signed; value = decision.</summary>
    public const string ProbationF07StepFamily = "prob.f07Step";

    /// <summary>Family: <c>dis.f09Step:&lt;stepId&gt;</c> — that HR-F09 step Signed.</summary>
    public const string DismissalF09StepFamily = "dis.f09Step";

    /// <summary>Stage binding codes of <c>process_stage_bindings</c> (§6.5).</summary>
    public static IReadOnlyList<string> StageBindings { get; } = ["contacted", "invited", "council", "practicalTest"];

    /// <summary>Every plain (parameterless) name.</summary>
    public static IReadOnlySet<string> Plain { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        CaseStarted,
        VacancyRequestCreated, VacancyRequestDecided, VacancyRequestFirstCandidate, VacancyRequestFilled,
        CandidateContacted, CandidateForm, CandidateInCouncilList, CandidateCouncilDecision, CandidateBackgroundCheck,
        CandidateStatus, CandidateHired, OfferBranch,
        EmploymentHired, EmploymentHireOrder, EmploymentAttendance, EmploymentF11,
        ProbationF06, ProbationF06Group1, ProbationF07Submitted, ProbationResolved,
        DismissalF09Opened, DismissalF09Approved, DismissalF08Approved, DismissalTerminated,
    };

    /// <summary>
    /// Family → the parameters it accepts. Blank step ids: HR-F05 from the HRMentor contract (§5.2), HR-F06 /
    /// HR-F07 / HR-F09 from the wave-1 definitions (<c>Blanks/Templates</c>) — HRMentor must keep them.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> FamilyParameters { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [CandidateStageFamily] = StageBindings,
            [CandidateF05StepFamily] = ["offerHr4", "offerCandidate", "docsHandover", "docsHr2", "docsCandidate"],
            [ProbationF06StepFamily] =
                ["signEmployee", "signMentor", "signHead", "signHr4", "day1", "day7", "day30", "day60", "day85", "day90"],
            [ProbationF07StepFamily] = ["signHead", "signMentor", "hr4Tests", "signEmployee", "hr1Decision"],
            [DismissalF09StepFamily] = ["handedOver", "received", "signHead", "hr3Control", "signHr1", "hr3Clearance"],
        };

    /// <summary>Builds a family name: <c>Family("cand.stage", "council")</c> → <c>cand.stage:council</c>.</summary>
    public static string Family(string family, string parameter) => $"{family}:{parameter}";

    /// <summary>True for a plain name or a family name with an accepted parameter.</summary>
    public static bool IsKnown(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Plain.Contains(name))
        {
            return true;
        }

        var colon = name.IndexOf(':', StringComparison.Ordinal);

        return colon > 0
            && FamilyParameters.TryGetValue(name[..colon], out var parameters)
            && parameters.Contains(name[(colon + 1)..], StringComparer.Ordinal);
    }
}
