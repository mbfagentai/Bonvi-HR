namespace Bonvi.Application.Processes.Facts;

/// <summary>
/// Fact names of the process engine (docs/SPEC-mening-ishlarim.md §7.2). Template JSON
/// (`completion.fact`, `when.fact`) and the gates refer to facts by these exact strings.
/// </summary>
/// <remarks>
/// Parameterised facts (<c>cand.stage:&lt;binding&gt;</c>, <c>…Step:&lt;stepId&gt;</c>) are built with the
/// helper methods; the parameter is a stage binding code (§6.5) or a blank step id (§5.2 / the
/// built-in template JSON).
/// </remarks>
public static class ProcessFacts
{
    // ── Vacancy request (subject: vacancy request id) ──

    /// <summary>The request row exists. At: <c>created_at</c>.</summary>
    public const string VrCreated = "vr.created";

    /// <summary>Status 2/3/4. At: <c>decided_at</c>; value <c>approved</c>/<c>rejected</c>/<c>cancelled</c>.</summary>
    public const string VrDecided = "vr.decided";

    /// <summary>A candidate for the request's vacancy created after the decision. At: that <c>created_at</c>.</summary>
    public const string VrFirstCandidate = "vr.firstCandidate";

    /// <summary>Hires since the request ≥ <c>openings</c>, or the vacancy closed. At: last hire time.</summary>
    public const string VrFilled = "vr.filled";

    // ── Candidate (subject: candidate id + appliedAt) ──

    /// <summary>The stage bound to <see cref="StageBindings.Contacted"/> was recorded after appliedAt.</summary>
    public const string CandContacted = "cand.contacted";

    /// <summary>Prefix of <see cref="CandStage"/>.</summary>
    public const string CandStagePrefix = "cand.stage:";

    /// <summary>A candidate form submitted ≥ appliedAt − 1 day. At: <c>submitted_at</c>; linked: form id.</summary>
    public const string CandF02 = "cand.f02";

    /// <summary>A live HR-F04 lists the candidate in its <c>candidates</c> table. At: submission <c>created_at</c>.</summary>
    public const string CandInF04 = "cand.inF04";

    /// <summary>That HR-F04 is Approved. At: approval; value = the row <c>decision</c>.</summary>
    public const string CandF04Decision = "cand.f04Decision";

    /// <summary>HR-F03 for the candidate Approved. At: approval; value = the <c>hr4Check</c> decision.</summary>
    public const string CandF03 = "cand.f03";

    /// <summary>Prefix of <see cref="CandF05Step"/>.</summary>
    public const string CandF05StepPrefix = "cand.f05Step:";

    /// <summary>Always holds when the candidate exists. At: <c>updated_at</c>; value = status number (1…6).</summary>
    public const string CandStatus = "cand.status";

    /// <summary>
    /// A Hire/Rehire event with this candidate, recorded ≥ appliedAt. At: <c>recorded_at</c>;
    /// linked: employment id, <see cref="FactValue.LinkedEmployeeId"/>: employee id.
    /// </summary>
    public const string CandHired = "cand.hired";

    /// <summary>
    /// Auxiliary (not a step trigger): the live HR-F05 has <c>startDate</c>. Value = <c>yyyy-MM-dd</c>;
    /// linked: the F05. Feeds the r02-12 due rule and the formalise anchor (§3.4, §3.5).
    /// </summary>
    public const string CandF05StartDate = "cand.f05StartDate";

    // ── Employment (subject: employment id) ──

    /// <summary>The employment exists. At: <c>created_at</c>.</summary>
    public const string EmpHired = "emp.hired";

    /// <summary><c>hire_order_no</c> not empty. At: <c>updated_at</c> (fallback <c>created_at</c>); value = the number.</summary>
    public const string EmpHireOrder = "emp.hireOrder";

    /// <summary>A live attendance identity or a Synced device enrolment. At: the earliest such row.</summary>
    /// <remarks>Answered for employee subjects and for the employment's employee.</remarks>
    public const string EmpAttendance = "emp.attendance";

    /// <summary>HR-F11 of the employment with both steps Signed. At: last <c>acted_at</c>.</summary>
    public const string EmpF11 = "emp.f11";

    /// <summary>Employment status 3. At: <c>updated_at</c>; value = <c>termination_date</c> (<c>yyyy-MM-dd</c>).</summary>
    public const string DisTerminated = "dis.terminated";

    /// <summary>A live HR-F09 with <c>answers.kind = dismissal</c>. At: <c>created_at</c>; linked: the F09.</summary>
    public const string DisF09Opened = "dis.f09Opened";

    /// <summary>Prefix of <see cref="DisF09Step"/>.</summary>
    public const string DisF09StepPrefix = "dis.f09Step:";

    /// <summary>That HR-F09 is Approved. At: approval.</summary>
    public const string DisF09Approved = "dis.f09Approved";

    /// <summary>
    /// HR-F08 Approved, or InApproval with the main K-code filled (Q13). At: approval or the last step
    /// act; value = the K-code.
    /// </summary>
    public const string DisF08Approved = "dis.f08Approved";

    /// <summary>Auxiliary: the live HR-F08 has <c>leaveDate</c>. Value = <c>yyyy-MM-dd</c> (dismissal anchor, §3.7).</summary>
    public const string DisF08LeaveDate = "dis.f08LeaveDate";

    // ── Probation (subject: probation id) ──

    /// <summary>HR-F06 created ≥ probation start − 7 days with <c>answers.mentor</c> set.</summary>
    public const string ProbF06 = "prob.f06";

    /// <summary>That HR-F06 has signEmployee, signMentor, signHead, signHr4 signed. At: last <c>acted_at</c>.</summary>
    public const string ProbF06Group1 = "prob.f06Group1";

    /// <summary>Prefix of <see cref="ProbF06Step"/>.</summary>
    public const string ProbF06StepPrefix = "prob.f06Step:";

    /// <summary>HR-F07 (same window as F06) InApproval or Approved. At: <c>submitted_at</c>.</summary>
    public const string ProbF07Submitted = "prob.f07Submitted";

    /// <summary>Prefix of <see cref="ProbF07Step"/>.</summary>
    public const string ProbF07StepPrefix = "prob.f07Step:";

    /// <summary>Probation status 2–5. At: <c>resolved_at</c> (Tashkent midnight); value = status number.</summary>
    public const string ProbResolved = "prob.resolved";

    /// <summary><c>cand.stage:&lt;binding&gt;</c> — the bound pipeline stage was recorded after appliedAt.</summary>
    public static string CandStage(string bindingCode) => CandStagePrefix + bindingCode;

    /// <summary><c>cand.f05Step:&lt;stepId&gt;</c> — that HR-F05 step Signed. At: <c>acted_at</c>.</summary>
    public static string CandF05Step(string stepId) => CandF05StepPrefix + stepId;

    /// <summary><c>dis.f09Step:&lt;stepId&gt;</c> — that HR-F09 step Signed. At: <c>acted_at</c>.</summary>
    public static string DisF09Step(string stepId) => DisF09StepPrefix + stepId;

    /// <summary>
    /// <c>prob.f06Step:&lt;stepId&gt;</c> — that HR-F06 step Signed. At: <c>acted_at</c>; value = the computed
    /// pass flag for <c>day7</c>/<c>day85</c> (<c>true</c>/<c>false</c>), else the step decision.
    /// </summary>
    public static string ProbF06Step(string stepId) => ProbF06StepPrefix + stepId;

    /// <summary><c>prob.f07Step:&lt;stepId&gt;</c> — that HR-F07 step Signed. At: <c>acted_at</c>; value = decision.</summary>
    public static string ProbF07Step(string stepId) => ProbF07StepPrefix + stepId;
}

/// <summary>Stage binding codes (<c>process_stage_bindings.binding_code</c>, §6.5).</summary>
public static class StageBindings
{
    public const string Contacted = "contacted";

    public const string Invited = "invited";

    public const string Council = "council";

    public const string PracticalTest = "practicalTest";
}

/// <summary>Blank template codes and step ids the facts read (§5.2 and the built-in wave-1 templates).</summary>
public static class ProcessBlanks
{
    public const string F03 = "HR-F03";

    public const string F04 = "HR-F04";

    public const string F05 = "HR-F05";

    public const string F06 = "HR-F06";

    public const string F07 = "HR-F07";

    public const string F08 = "HR-F08";

    public const string F09 = "HR-F09";

    public const string F11 = "HR-F11";

    /// <summary>HR-F03 step whose decision is the check outcome (<c>ok</c>/<c>watch</c>/<c>notRecommended</c>).</summary>
    public const string F03DecisionStep = "hr4Check";

    /// <summary>HR-F06 group-1 signatures (§3.6 p03-1).</summary>
    public static IReadOnlyList<string> F06Group1Steps { get; } = ["signEmployee", "signMentor", "signHead", "signHr4"];

    /// <summary>HR-F11 steps that must both be signed (§5.2).</summary>
    public static IReadOnlyList<string> F11Steps { get; } = ["signEmployee", "signIntroducer"];

    /// <summary>HR-F09 <c>kind</c> option that makes the act a dismissal act.</summary>
    public const string F09DismissalKind = "dismissal";
}
