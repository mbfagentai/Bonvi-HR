using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Processes;
using static Bonvi.Application.Tests.Processes.Queries.ProcessReadRows;

namespace Bonvi.Application.Tests.Processes.Queries;

/// <summary>§9.3 as buttons: the predicates the write slice has to share with the screen, so a button never lies.</summary>
public sealed class ProcessStepAbilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProcessManual ManualFallback = new(Allowed: true, []);
    private static readonly ProcessManual NoManualFallback = new(Allowed: false, []);

    private static ProcessViewer Holder(bool canWork = true) => Viewer(canWork: canWork, officeRoles: ProcessRoles.Hr2);

    private static ProcessViewer Stranger() => Viewer(officeRoles: ProcessRoles.Lawyer);

    private static ProcessViewer Manager(bool canWork = true) => Viewer(canWork: canWork, canManage: true);

    // ─────────────────────────── «Bajarildi» ───────────────────────────

    [Fact]
    public void The_holder_completes_an_active_manual_step()
        => ProcessStepAbility.CanComplete(Definition(), Step(), Case(), Holder()).ShouldBeTrue();

    [Fact]
    public void A_manager_completes_a_step_they_do_not_hold()
        => ProcessStepAbility.CanComplete(Definition(), Step(), Case(), Manager()).ShouldBeTrue();

    [Fact]
    public void Someone_who_neither_holds_nor_manages_cannot_complete()
        => ProcessStepAbility.CanComplete(Definition(), Step(), Case(), Stranger()).ShouldBeFalse();

    [Fact]
    public void Without_processes_work_neither_holder_nor_manager_can_complete()
    {
        ProcessStepAbility.CanComplete(Definition(), Step(), Case(), Holder(canWork: false)).ShouldBeFalse();
        ProcessStepAbility.CanComplete(Definition(), Step(), Case(), Manager(canWork: false)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProcessStepState.Locked)]
    [InlineData(ProcessStepState.Waiting)]
    [InlineData(ProcessStepState.Blocked)]
    [InlineData(ProcessStepState.Done)]
    [InlineData(ProcessStepState.Skipped)]
    public void Only_an_active_step_can_be_completed_even_by_a_manager(ProcessStepState state)
        => ProcessStepAbility.CanComplete(Definition(), Step(state), Case(), Manager()).ShouldBeFalse();

    [Theory]
    [InlineData(ProcessCaseStatus.Completed)]
    [InlineData(ProcessCaseStatus.Cancelled)]
    public void Nothing_completes_in_a_case_that_has_ended(ProcessCaseStatus status)
        => ProcessStepAbility.CanComplete(Definition(), Step(), Case(status), Manager()).ShouldBeFalse();

    [Fact]
    public void A_step_with_no_definition_cannot_be_completed()
        => ProcessStepAbility.CanComplete(null, Step(), Case(), Manager()).ShouldBeFalse();

    [Fact]
    public void A_fact_step_is_completed_by_hand_only_through_its_declared_fallback()
    {
        ProcessStepAbility.CanComplete(Definition(ProcessStepKind.Fact), Step(), Case(), Manager()).ShouldBeFalse();
        ProcessStepAbility.CanComplete(Definition(ProcessStepKind.Fact, NoManualFallback), Step(), Case(), Manager()).ShouldBeFalse();
        ProcessStepAbility.CanComplete(Definition(ProcessStepKind.Fact, ManualFallback), Step(), Case(), Holder()).ShouldBeTrue();
    }

    [Fact]
    public void An_auto_step_is_never_completed_by_hand()
        => ProcessStepAbility.CanComplete(Definition(ProcessStepKind.Auto, ManualFallback), Step(), Case(), Manager()).ShouldBeFalse();

    [Fact]
    public void A_step_resolved_to_a_person_belongs_to_that_person_and_not_to_the_office_role()
    {
        var person = Guid.NewGuid();
        var step = Step(holderEmployeeId: person);

        ProcessStepAbility.CanComplete(Definition(), step, Case(), Viewer(employeeId: person)).ShouldBeTrue();
        ProcessStepAbility.CanComplete(Definition(), step, Case(), Viewer(employeeId: Guid.NewGuid(), officeRoles: ProcessRoles.Hr2)).ShouldBeFalse();
    }

    // ─────────────────────────── «Kerak emas» ───────────────────────────

    [Theory]
    [InlineData(ProcessStepState.Locked)]
    [InlineData(ProcessStepState.Active)]
    [InlineData(ProcessStepState.Waiting)]
    public void The_holder_skips_an_open_skippable_step(ProcessStepState state)
        => ProcessStepAbility.CanSkip(Definition(skippable: true), Step(state), Case(), Holder()).ShouldBeTrue();

    [Fact]
    public void A_blocked_step_cannot_be_skipped_even_by_a_manager()
    {
        var definition = Definition(skippable: true, legacySkippable: true);

        ProcessStepAbility.CanSkip(definition, Step(ProcessStepState.Blocked), Case(), Manager()).ShouldBeFalse();
        ProcessStepAbility.CanSkip(definition, Step(ProcessStepState.Blocked), Case(legacy: true), Manager()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProcessStepState.Done)]
    [InlineData(ProcessStepState.Skipped)]
    public void A_closed_step_cannot_be_skipped(ProcessStepState state)
    {
        var definition = Definition(skippable: true, legacySkippable: true);

        ProcessStepAbility.CanSkip(definition, Step(state), Case(legacy: true), Manager()).ShouldBeFalse();
    }

    [Fact]
    public void A_step_that_is_not_skippable_cannot_be_skipped_even_by_a_manager()
    {
        ProcessStepAbility.CanSkip(Definition(), Step(), Case(), Manager()).ShouldBeFalse();
        ProcessStepAbility.CanSkip(Definition(), Step(), Case(legacy: true), Manager()).ShouldBeFalse();
    }

    [Fact]
    public void A_legacy_case_lets_the_holder_skip_a_legacy_skippable_step()
    {
        var definition = Definition(legacySkippable: true);

        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: true), Holder()).ShouldBeTrue();
    }

    [Fact]
    public void A_legacy_case_lets_a_manager_skip_a_legacy_skippable_step_they_do_not_hold()
    {
        var definition = Definition(legacySkippable: true);

        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: true), Manager()).ShouldBeTrue();
    }

    [Fact]
    public void Legacy_skippable_means_nothing_in_a_case_that_is_not_legacy()
    {
        var definition = Definition(legacySkippable: true);

        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: false), Holder()).ShouldBeFalse();
        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: false), Manager()).ShouldBeFalse();
    }

    [Fact]
    public void A_legacy_skip_still_asks_who_is_acting()
    {
        var definition = Definition(legacySkippable: true);

        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: true), Stranger()).ShouldBeFalse();
        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: true), Holder(canWork: false)).ShouldBeFalse();
        ProcessStepAbility.CanSkip(definition, Step(), Case(legacy: true), Manager(canWork: false)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProcessCaseStatus.Completed)]
    [InlineData(ProcessCaseStatus.Cancelled)]
    public void Nothing_skips_in_a_case_that_has_ended(ProcessCaseStatus status)
    {
        ProcessStepAbility.CanSkip(Definition(skippable: true), Step(), Case(status), Manager()).ShouldBeFalse();
        ProcessStepAbility.CanSkip(Definition(legacySkippable: true), Step(), Case(status, legacy: true), Manager()).ShouldBeFalse();
    }

    [Fact]
    public void A_step_with_no_definition_cannot_be_skipped()
        => ProcessStepAbility.CanSkip(null, Step(), Case(legacy: true), Manager()).ShouldBeFalse();

    [Fact]
    public void Someone_who_neither_holds_nor_manages_cannot_skip_a_skippable_step()
        => ProcessStepAbility.CanSkip(Definition(skippable: true), Step(), Case(), Stranger()).ShouldBeFalse();

    // ─────────────────────────── Undo ───────────────────────────

    private static ProcessStepRow ClosedBy(Guid by, DateTimeOffset at, ProcessStepResolution resolution = ProcessStepResolution.Manual)
        => Step(ProcessStepState.Done, resolution: resolution, doneAt: at, doneBy: by);

    [Theory]
    [InlineData(ProcessStepResolution.Manual)]
    [InlineData(ProcessStepResolution.UserSkip)]
    [InlineData(ProcessStepResolution.Legacy)]
    public void Who_closed_a_step_undoes_it_within_the_window(ProcessStepResolution resolution)
    {
        var user = Guid.NewGuid();

        ProcessStepAbility.CanUndo(ClosedBy(user, Now.AddHours(-1), resolution), Case(), Viewer(user), Now).ShouldBeTrue();
    }

    [Fact]
    public void The_window_includes_its_last_moment_and_nothing_after()
    {
        var user = Guid.NewGuid();
        var atTheEdge = Now - ProcessCase.UndoWindow;

        ProcessStepAbility.CanUndo(ClosedBy(user, atTheEdge), Case(), Viewer(user), Now).ShouldBeTrue();
        ProcessStepAbility.CanUndo(ClosedBy(user, atTheEdge.AddTicks(-1)), Case(), Viewer(user), Now).ShouldBeFalse();
    }

    [Fact]
    public void Someone_else_cannot_undo_within_the_window_even_holding_the_role()
    {
        var step = ClosedBy(Guid.NewGuid(), Now.AddMinutes(-1));

        ProcessStepAbility.CanUndo(step, Case(), Holder(), Now).ShouldBeFalse();
    }

    /// <summary>
    /// Why <c>ProcessStepWriter</c> refuses a viewer with no id before it asks: on a step with no closer, this comparison
    /// alone says yes. <c>ProcessWriteApiTests.Undo_with_no_user_id...</c> pins the refusal.
    /// </summary>
    [Fact]
    public void The_undo_ability_alone_matches_a_viewer_with_no_id_on_a_step_with_no_closer()
    {
        var step = Step(ProcessStepState.Done, resolution: ProcessStepResolution.Manual, doneAt: Now.AddMinutes(-1));
        var noId = new ProcessViewer(userId: null, employeeId: null, new HashSet<string>(StringComparer.Ordinal), canWork: true, canManage: false);

        ProcessStepAbility.CanUndo(step, Case(), noId, Now).ShouldBeTrue();
    }

    [Fact]
    public void A_manager_undoes_anyone_s_step_at_any_time()
    {
        var step = ClosedBy(Guid.NewGuid(), Now.AddDays(-400));

        ProcessStepAbility.CanUndo(step, Case(), Manager(), Now).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ProcessStepResolution.Fact)]
    [InlineData(ProcessStepResolution.Auto)]
    [InlineData(ProcessStepResolution.Condition)]
    [InlineData(ProcessStepResolution.CaseEnded)]
    public void What_the_engine_closed_cannot_be_undone_even_by_a_manager(ProcessStepResolution resolution)
    {
        var user = Guid.NewGuid();
        var step = ClosedBy(user, Now.AddMinutes(-1), resolution);

        ProcessStepAbility.CanUndo(step, Case(), Viewer(user, canManage: true), Now).ShouldBeFalse();
    }

    [Fact]
    public void An_open_step_has_nothing_to_undo()
    {
        var user = Guid.NewGuid();

        ProcessStepAbility.CanUndo(Step(), Case(), Viewer(user, canManage: true), Now).ShouldBeFalse();
    }

    [Fact]
    public void Without_processes_work_nobody_undoes()
    {
        var user = Guid.NewGuid();
        var step = ClosedBy(user, Now.AddMinutes(-1));

        ProcessStepAbility.CanUndo(step, Case(), Viewer(user, canWork: false), Now).ShouldBeFalse();
        ProcessStepAbility.CanUndo(step, Case(), Viewer(user, canWork: false, canManage: true), Now).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProcessCaseStatus.Completed)]
    [InlineData(ProcessCaseStatus.Cancelled)]
    public void Nothing_undoes_in_a_case_that_has_ended(ProcessCaseStatus status)
    {
        var user = Guid.NewGuid();
        var step = ClosedBy(user, Now.AddMinutes(-1));

        ProcessStepAbility.CanUndo(step, Case(status), Viewer(user, canManage: true), Now).ShouldBeFalse();
    }

    [Fact]
    public void Own_step_with_no_recorded_time_cannot_be_undone_by_its_owner()
    {
        var user = Guid.NewGuid();
        var step = Step(ProcessStepState.Done, resolution: ProcessStepResolution.Manual, doneAt: null, doneBy: user);

        ProcessStepAbility.CanUndo(step, Case(), Viewer(user), Now).ShouldBeFalse();
    }
}
