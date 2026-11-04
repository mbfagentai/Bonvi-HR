using Bonvi.Application.Processes.Calendar;

namespace Bonvi.Application.Processes.Definitions;

/// <summary>What a due rule is evaluated against, for one case in one pass.</summary>
/// <param name="Anchors">
/// Known anchor dates of the case (<see cref="ProcessAnchors"/>; <c>step:&lt;id&gt;.doneOn</c> only for Done
/// steps). A missing key = unknown.
/// </param>
/// <param name="Calendar">Working days for <c>workingDays</c> and <c>nextCouncilDay</c>.</param>
/// <param name="CouncilDay">Setting <c>processes.councilWeekday</c> (default Thursday).</param>
/// <param name="Floor">No due earlier than this; null for a legacy case.</param>
public sealed record ProcessDueContext(
    IReadOnlyDictionary<string, DateOnly> Anchors,
    IWorkingDayCalendar Calendar,
    DayOfWeek CouncilDay,
    DateOnly? Floor)
{
    /// <summary>
    /// Builds the context with the §3.1 due floor: a non-legacy case never starts already late, so its dues
    /// are at least <c>case.startedOn</c>; a legacy case keeps the raw dates.
    /// </summary>
    public static ProcessDueContext ForCase(
        IReadOnlyDictionary<string, DateOnly> anchors, IWorkingDayCalendar calendar, DayOfWeek councilDay, bool legacy)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        DateOnly? floor = !legacy && anchors.TryGetValue(ProcessAnchors.CaseStartedOn, out var startedOn) ? startedOn : null;
        return new ProcessDueContext(anchors, calendar, councilDay, floor);
    }
}

/// <summary>Turns a step's <see cref="ProcessDueRule"/> into a date (SPEC-mening-ishlarim §3.1).</summary>
/// <remarks>
/// Pure: everything it needs is in <see cref="ProcessDueContext"/>. An unknown anchor
/// moves to the rule's <c>fallback</c>; when the whole chain is unknown the due is null («muddatsiz»).
/// </remarks>
public static class ProcessDueEvaluator
{
    /// <summary>The step's due, floored by <see cref="ProcessDueContext.Floor"/>.</summary>
    public static DateOnly? Evaluate(ProcessDueRule? rule, ProcessDueContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Raw(rule, context) is not { } due)
        {
            return null;
        }

        return context.Floor is { } floor && floor > due ? floor : due;
    }

    /// <summary>
    /// True when the step must be Skipped because its due falls on or after its
    /// <see cref="ProcessStep.SkipIfDueOnOrAfter"/> date (probation: «within the last five days», §3.6). The
    /// cutoff is not floored — it is a fact about the probation, not a deadline.
    /// </summary>
    public static bool IsCutOff(ProcessStep step, DateOnly? due, ProcessDueContext context)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        return due is { } date && Raw(step.SkipIfDueOnOrAfter, context) is { } cutoff && date >= cutoff;
    }

    private static DateOnly? Raw(ProcessDueRule? rule, ProcessDueContext context)
    {
        foreach (var candidate in rule?.Chain() ?? [])
        {
            if (Anchor(candidate.From, context.Anchors) is not { } anchor)
            {
                continue;
            }

            return candidate.Kind switch
            {
                ProcessDueKind.WorkingDays => context.Calendar.AddWorkingDays(anchor, candidate.Offset),
                ProcessDueKind.CalendarDays => anchor.AddDays(candidate.Offset),
                ProcessDueKind.NextCouncilDay => context.Calendar.NextCouncilDay(anchor, context.CouncilDay),
                _ => throw new InvalidOperationException($"Unknown due kind {candidate.Kind}."),
            };
        }

        return null;
    }

    /// <summary>Anchor value; <c>employment.hireOrderDate</c> falls back to <c>employment.hireDate</c> (§3.1).</summary>
    private static DateOnly? Anchor(string name, IReadOnlyDictionary<string, DateOnly> anchors)
    {
        if (anchors.TryGetValue(name, out var date))
        {
            return date;
        }

        return name == ProcessAnchors.EmploymentHireOrderDate
            && anchors.TryGetValue(ProcessAnchors.EmploymentHireDate, out var hireDate)
                ? hireDate
                : null;
    }
}
