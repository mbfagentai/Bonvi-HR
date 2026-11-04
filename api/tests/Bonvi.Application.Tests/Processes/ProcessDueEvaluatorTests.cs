using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Processes.Definitions;

namespace Bonvi.Application.Tests.Processes;

/// <summary>Due rules → dates (SPEC-mening-ishlarim §3.1): fallback chain, anchors, floor, probation cutoff.</summary>
public sealed class ProcessDueEvaluatorTests
{
    // 2026-11-02 Mon, 05 Thu, 06 Fri, 09 Mon, 10 Tue, 12 Thu, 13 Fri
    private static DateOnly Nov(int day) => new(2026, 11, day);

    private static readonly IWorkingDayCalendar Calendar = WorkingDayCalendar.MondayToFriday;

    private static ProcessDueContext Raw(params (string Anchor, DateOnly Date)[] anchors)
        => new(anchors.ToDictionary(pair => pair.Anchor, pair => pair.Date), Calendar, DayOfWeek.Thursday, Floor: null);

    private static ProcessStep Load(string code, string stepId)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Processes", "Templates", code + ".json"));
        return ProcessDefinitionValidator.Load(json).FindStep(stepId)!;
    }

    [Fact]
    public void Working_days_from_an_anchor()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.CaseStartedOn, ProcessDueKind.WorkingDays, 2),
                Raw((ProcessAnchors.CaseStartedOn, Nov(5))))
            .ShouldBe(Nov(9));

    [Fact]
    public void Calendar_days_ignore_weekends()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.ProbationStartDate, ProcessDueKind.CalendarDays, 7),
                Raw((ProcessAnchors.ProbationStartDate, Nov(1))))
            .ShouldBe(Nov(8));

    [Fact]
    public void Next_council_day_uses_the_context_weekday_and_calendar()
    {
        var context = new ProcessDueContext(
            new Dictionary<string, DateOnly> { [ProcessAnchors.StepDoneOn("r02-6")] = Nov(9) },
            new WorkingDayCalendar([new ProcessCalendarDay(Nov(12), ProcessCalendarDayKind.Holiday)]),
            DayOfWeek.Thursday,
            Floor: null);

        ProcessDueEvaluator.Evaluate(new ProcessDueRule(ProcessAnchors.StepDoneOn("r02-6"), ProcessDueKind.NextCouncilDay, 0), context)
            .ShouldBe(Nov(13));
    }

    [Fact]
    public void Null_rule_is_no_due()
        => ProcessDueEvaluator.Evaluate(null, Raw((ProcessAnchors.CaseStartedOn, Nov(5)))).ShouldBeNull();

    [Fact]
    public void Unknown_anchor_without_fallback_is_no_due()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.ProbationEndDate, ProcessDueKind.CalendarDays, -5),
                Raw((ProcessAnchors.ProbationStartDate, Nov(2))))
            .ShouldBeNull();

    [Fact]
    public void Step_anchor_of_a_step_not_yet_done_is_no_due()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.StepDoneOn("r02-3"), ProcessDueKind.WorkingDays, 1),
                Raw((ProcessAnchors.CaseStartedOn, Nov(2))))
            .ShouldBeNull();

    [Fact]
    public void First_known_anchor_in_the_chain_wins()
    {
        var rule = Load("hire-candidate", "r02-12").Due; // f05.startDate −1 day, else step:r02-11.doneOn +3 wd

        ProcessDueEvaluator.Evaluate(rule, Raw(
                (ProcessAnchors.OfferStartDate, Nov(20)),
                (ProcessAnchors.StepDoneOn("r02-11"), Nov(5))))
            .ShouldBe(Nov(19));
    }

    [Fact]
    public void Unknown_first_anchor_falls_back()
        => ProcessDueEvaluator.Evaluate(
                Load("hire-candidate", "r02-12").Due,
                Raw((ProcessAnchors.StepDoneOn("r02-11"), Nov(5))))
            .ShouldBe(Nov(10));

    [Fact]
    public void Two_level_fallback_chain()
    {
        var rule = new ProcessDueRule("probation.endDate", ProcessDueKind.CalendarDays, 0,
            new ProcessDueRule("f05.startDate", ProcessDueKind.CalendarDays, 0,
                new ProcessDueRule(ProcessAnchors.CaseStartedOn, ProcessDueKind.WorkingDays, 1)));

        ProcessDueEvaluator.Evaluate(rule, Raw((ProcessAnchors.CaseStartedOn, Nov(6)))).ShouldBe(Nov(9));
    }

    [Fact]
    public void Whole_chain_unknown_is_no_due()
        => ProcessDueEvaluator.Evaluate(Load("hire-candidate", "r02-12").Due, Raw((ProcessAnchors.CaseStartedOn, Nov(2))))
            .ShouldBeNull();

    [Fact]
    public void Hire_order_date_falls_back_to_hire_date()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.EmploymentHireOrderDate, ProcessDueKind.WorkingDays, 3),
                Raw((ProcessAnchors.EmploymentHireDate, Nov(2))))
            .ShouldBe(Nov(5));

    [Fact]
    public void Hire_order_date_wins_over_hire_date_when_known()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.EmploymentHireOrderDate, ProcessDueKind.WorkingDays, 3),
                Raw((ProcessAnchors.EmploymentHireDate, Nov(2)), (ProcessAnchors.EmploymentHireOrderDate, Nov(9))))
            .ShouldBe(Nov(12));

    [Fact]
    public void Hire_date_does_not_fall_back_to_hire_order_date()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.EmploymentHireDate, ProcessDueKind.CalendarDays, 0),
                Raw((ProcessAnchors.EmploymentHireOrderDate, Nov(9))))
            .ShouldBeNull();

    // ── floor ──────────────────────────────────────────────────

    private static readonly Dictionary<string, DateOnly> LateStart = new()
    {
        [ProcessAnchors.CaseStartedOn] = Nov(10),
        [ProcessAnchors.EmploymentHireDate] = Nov(2),
    };

    private static readonly ProcessDueRule FromHireDate = new(ProcessAnchors.EmploymentHireDate, ProcessDueKind.WorkingDays, 1);

    [Fact]
    public void Non_legacy_case_never_starts_late()
    {
        var context = ProcessDueContext.ForCase(LateStart, Calendar, DayOfWeek.Thursday, legacy: false);

        context.Floor.ShouldBe(Nov(10));
        ProcessDueEvaluator.Evaluate(FromHireDate, context).ShouldBe(Nov(10));
    }

    [Fact]
    public void Legacy_case_keeps_the_raw_date()
    {
        var context = ProcessDueContext.ForCase(LateStart, Calendar, DayOfWeek.Thursday, legacy: true);

        context.Floor.ShouldBeNull();
        ProcessDueEvaluator.Evaluate(FromHireDate, context).ShouldBe(Nov(3));
    }

    [Fact]
    public void Floor_does_not_move_a_later_due()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.EmploymentHireDate, ProcessDueKind.CalendarDays, 20),
                ProcessDueContext.ForCase(LateStart, Calendar, DayOfWeek.Thursday, legacy: false))
            .ShouldBe(Nov(22));

    [Fact]
    public void Floor_does_not_invent_a_due()
        => ProcessDueEvaluator.Evaluate(
                new ProcessDueRule(ProcessAnchors.ProbationEndDate, ProcessDueKind.CalendarDays, 0),
                ProcessDueContext.ForCase(LateStart, Calendar, DayOfWeek.Thursday, legacy: false))
            .ShouldBeNull();

    [Fact]
    public void No_started_on_means_no_floor()
        => ProcessDueContext.ForCase(
                new Dictionary<string, DateOnly> { [ProcessAnchors.EmploymentHireDate] = Nov(2) },
                Calendar, DayOfWeek.Thursday, legacy: false)
            .Floor.ShouldBeNull();

    // ── probation cutoff (skipIfDueOnOrAfter = endDate − 5) ────

    private static ProcessDueContext Probation(DateOnly start, DateOnly end, DateOnly? startedOn = null)
    {
        var anchors = new Dictionary<string, DateOnly>
        {
            [ProcessAnchors.ProbationStartDate] = start,
            [ProcessAnchors.ProbationEndDate] = end,
        };

        if (startedOn is { } started)
        {
            anchors[ProcessAnchors.CaseStartedOn] = started;
        }

        return ProcessDueContext.ForCase(anchors, Calendar, DayOfWeek.Thursday, legacy: false);
    }

    [Theory]
    [InlineData(8, false)]  // end 12-08 → cutoff 12-03, p03-30 due 12-02 → kept
    [InlineData(7, true)]   // end 12-07 → cutoff 12-02 = due → Skipped (on-or-after)
    [InlineData(1, true)]   // short probation: end 12-01 → cutoff 11-26 < due → Skipped
    public void Probation_step_due_within_the_last_five_days_is_cut_off(int endDecemberDay, bool cutOff)
    {
        var step = Load("probation", "p03-30");
        var context = Probation(Nov(2), new DateOnly(2026, 12, endDecemberDay));
        var due = ProcessDueEvaluator.Evaluate(step.Due, context);

        due.ShouldBe(new DateOnly(2026, 12, 2));
        ProcessDueEvaluator.IsCutOff(step, due, context).ShouldBe(cutOff);
    }

    [Fact]
    public void Final_probation_steps_are_never_cut_off()
    {
        var step = Load("probation", "p03-85");
        var context = Probation(Nov(2), new DateOnly(2026, 12, 7));
        var due = ProcessDueEvaluator.Evaluate(step.Due, context);

        due.ShouldBe(new DateOnly(2026, 12, 2));
        ProcessDueEvaluator.IsCutOff(step, due, context).ShouldBeFalse();
    }

    [Fact]
    public void Cutoff_is_not_floored()
    {
        // Floor (startedOn) 12-20 is far after the raw cutoff 12-05; a floored cutoff would keep 12-05 dues alive
        var step = Load("probation", "p03-30");
        var context = Probation(Nov(2), new DateOnly(2026, 12, 10), startedOn: new DateOnly(2026, 12, 20));

        ProcessDueEvaluator.IsCutOff(step, new DateOnly(2026, 12, 4), context).ShouldBeFalse();
        ProcessDueEvaluator.IsCutOff(step, new DateOnly(2026, 12, 5), context).ShouldBeTrue();
    }

    [Fact]
    public void No_due_or_unknown_end_date_is_not_a_cutoff()
    {
        var step = Load("probation", "p03-30");

        ProcessDueEvaluator.IsCutOff(step, null, Probation(Nov(2), Nov(30))).ShouldBeFalse();
        ProcessDueEvaluator.IsCutOff(step, Nov(30), Raw((ProcessAnchors.ProbationStartDate, Nov(2)))).ShouldBeFalse();
    }
}
