using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Processes;
using static Bonvi.Application.Tests.Processes.Queries.ProcessReadRows;

namespace Bonvi.Application.Tests.Processes.Queries;

/// <summary>§10 #1: urgent = today, late and Blocked held by me; held = Blocked on someone else; the rest is later.</summary>
public sealed class ProcessWorkSectionsTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);

    private static (List<ProcessStepRow> Urgent, List<ProcessStepRow> Later, List<ProcessStepRow> Held) Split(
        ProcessViewer viewer,
        Func<ProcessStepRow, ProcessHolderRef?> resolverOf,
        params ProcessStepRow[] steps)
        => ProcessWorkSections.Split(steps, viewer, Today, resolverOf);

    private static (List<ProcessStepRow> Urgent, List<ProcessStepRow> Later, List<ProcessStepRow> Held) Split(params ProcessStepRow[] steps)
        => Split(Viewer(officeRoles: ProcessRoles.Hr2), _ => null, steps);

    [Theory]
    [InlineData(-30)]
    [InlineData(-1)]
    [InlineData(0)]
    public void An_active_step_due_today_or_earlier_is_urgent(int days)
    {
        var step = Step(due: Today.AddDays(days));

        var (urgent, later, held) = Split(step);

        urgent.ShouldHaveSingleItem().ShouldBeSameAs(step);
        later.ShouldBeEmpty();
        held.ShouldBeEmpty();
    }

    [Fact]
    public void An_active_step_due_tomorrow_is_later()
    {
        var step = Step(due: Today.AddDays(1));

        var (urgent, later, held) = Split(step);

        later.ShouldHaveSingleItem().ShouldBeSameAs(step);
        urgent.ShouldBeEmpty();
        held.ShouldBeEmpty();
    }

    [Fact]
    public void An_active_step_with_no_due_is_later()
    {
        var step = Step(due: null);

        var (urgent, later, _) = Split(step);

        later.ShouldHaveSingleItem().ShouldBeSameAs(step);
        urgent.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ProcessStepState.Locked)]
    [InlineData(ProcessStepState.Waiting)]
    [InlineData(ProcessStepState.Done)]
    [InlineData(ProcessStepState.Skipped)]
    public void Only_active_and_blocked_steps_are_placed(ProcessStepState state)
    {
        var (urgent, later, held) = Split(Step(state, due: Today));

        urgent.ShouldBeEmpty();
        later.ShouldBeEmpty();
        held.ShouldBeEmpty();
    }

    [Fact]
    public void A_blocked_step_whose_missing_thing_the_viewer_holds_is_urgent_whatever_its_due()
    {
        var step = Step(ProcessStepState.Blocked, due: Today.AddDays(30));
        var viewer = Viewer(officeRoles: ProcessRoles.SafetyEngineer);

        var (urgent, later, held) = Split(viewer, _ => new ProcessHolderRef(ProcessRoles.SafetyEngineer, null), step);

        urgent.ShouldHaveSingleItem().ShouldBeSameAs(step);
        later.ShouldBeEmpty();
        held.ShouldBeEmpty();
    }

    [Fact]
    public void A_blocked_step_whose_missing_thing_someone_else_holds_is_held_even_when_late()
    {
        var step = Step(ProcessStepState.Blocked, due: Today.AddDays(-30));
        var viewer = Viewer(officeRoles: ProcessRoles.Hr2);

        var (urgent, later, held) = Split(viewer, _ => new ProcessHolderRef(ProcessRoles.SafetyEngineer, null), step);

        held.ShouldHaveSingleItem().ShouldBeSameAs(step);
        urgent.ShouldBeEmpty();
        later.ShouldBeEmpty();
    }

    [Fact]
    public void A_blocked_step_with_no_known_resolver_stays_urgent()
    {
        var step = Step(ProcessStepState.Blocked);

        var (urgent, _, held) = Split(step);

        urgent.ShouldHaveSingleItem().ShouldBeSameAs(step);
        held.ShouldBeEmpty();
    }

    [Fact]
    public void A_person_resolver_is_held_by_that_person_and_no_office_role_stands_in()
    {
        var person = Guid.NewGuid();
        var step = Step(ProcessStepState.Blocked);
        ProcessHolderRef Resolver(ProcessStepRow _) => new(ProcessRoles.Hr2, person);

        var (mineUrgent, _, mineHeld) = Split(Viewer(employeeId: person), Resolver, step);
        mineUrgent.ShouldHaveSingleItem().ShouldBeSameAs(step);
        mineHeld.ShouldBeEmpty();

        var (otherUrgent, _, otherHeld) = Split(Viewer(employeeId: Guid.NewGuid(), officeRoles: ProcessRoles.Hr2), Resolver, step);
        otherHeld.ShouldHaveSingleItem().ShouldBeSameAs(step);
        otherUrgent.ShouldBeEmpty();
    }

    [Fact]
    public void The_resolver_is_asked_for_blocked_steps_only()
    {
        var blocked = Step(ProcessStepState.Blocked, orderNo: 2);
        var asked = new List<ProcessStepRow>();

        Split(
            Viewer(),
            step =>
            {
                asked.Add(step);
                return null;
            },
            Step(orderNo: 1),
            blocked,
            Step(ProcessStepState.Locked, orderNo: 3));

        asked.ShouldHaveSingleItem().ShouldBeSameAs(blocked);
    }

    [Fact]
    public void Each_section_runs_soonest_first_with_a_step_with_no_due_last()
    {
        var noDueFirstOrder = Step(due: null, orderNo: 1);
        var tomorrow = Step(due: Today.AddDays(1), orderNo: 9);
        var nextWeek = Step(due: Today.AddDays(7), orderNo: 2);
        var late = Step(due: Today.AddDays(-3), orderNo: 8);
        var today = Step(due: Today, orderNo: 1);

        var (urgent, later, _) = Split(noDueFirstOrder, nextWeek, today, tomorrow, late);

        urgent.ShouldBe(new[] { late, today });
        later.ShouldBe(new[] { tomorrow, nextWeek, noDueFirstOrder });
    }

    [Fact]
    public void Blocked_and_active_mix_in_urgent_by_due()
    {
        var blocked = Step(ProcessStepState.Blocked, due: Today.AddDays(-2));
        var active = Step(due: Today.AddDays(-1));
        var noDue = Step(ProcessStepState.Blocked, due: null);

        Split(noDue, active, blocked).Urgent.ShouldBe(new[] { blocked, active, noDue });
    }

    [Fact]
    public void Ties_on_due_go_by_step_order_then_case()
    {
        var firstCase = new Guid("00000000-0000-0000-0000-000000000001");
        var secondCase = new Guid("00000000-0000-0000-0000-000000000002");
        var secondOrder = Step(due: Today, orderNo: 2, caseId: firstCase);
        var firstOrderSecondCase = Step(due: Today, orderNo: 1, caseId: secondCase);
        var firstOrderFirstCase = Step(due: Today, orderNo: 1, caseId: firstCase);

        ProcessWorkSections.Ordered([secondOrder, firstOrderSecondCase, firstOrderFirstCase])
            .ShouldBe(new[] { firstOrderFirstCase, firstOrderSecondCase, secondOrder });
    }

    [Fact]
    public void No_steps_gives_three_empty_sections()
    {
        var (urgent, later, held) = Split();

        urgent.ShouldBeEmpty();
        later.ShouldBeEmpty();
        held.ShouldBeEmpty();
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        var viewer = Viewer();

        Should.Throw<ArgumentNullException>(() => ProcessWorkSections.Split(null!, viewer, Today, _ => null));
        Should.Throw<ArgumentNullException>(() => ProcessWorkSections.Split([], null!, Today, _ => null));
        Should.Throw<ArgumentNullException>(() => ProcessWorkSections.Split([], viewer, Today, null!));
    }
}
