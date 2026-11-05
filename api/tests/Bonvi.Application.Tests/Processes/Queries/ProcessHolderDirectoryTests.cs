using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Queries;

namespace Bonvi.Application.Tests.Processes.Queries;

/// <summary>§7.7 and §9.1: who stands behind an office role, read at read time from «Rollar».</summary>
public sealed class ProcessHolderDirectoryTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private static readonly string[] Hr1AndItsStandIns = [ProcessRoles.Hr1, ProcessRoles.Hr5, ProcessRoles.Hr8];
    private static readonly string[] OnlyHr2 = [ProcessRoles.Hr2];
    private static readonly string[] Hr1AndHr8 = [ProcessRoles.Hr1, ProcessRoles.Hr8];
    private static readonly string[] OnlyHr5 = [ProcessRoles.Hr5];

    private static ProcessHolderDirectory Directory(params (string Role, Guid UserId)[] assignments) => new(assignments);

    [Fact]
    public void An_assigned_role_names_its_users_once_each()
    {
        var directory = Directory((ProcessRoles.Hr2, Alice), (ProcessRoles.Hr2, Bob), (ProcessRoles.Hr2, Alice));

        directory.UsersOf(ProcessRoles.Hr2).ShouldBe(new[] { Alice, Bob }, ignoreOrder: true);
    }

    [Fact]
    public void A_role_nobody_holds_has_no_users()
        => Directory((ProcessRoles.Hr2, Alice)).UsersOf(ProcessRoles.Lawyer).ShouldBeEmpty();

    [Theory]
    [InlineData(ProcessRoles.Hr5)]
    [InlineData(ProcessRoles.Hr8)]
    public void Hr5_and_hr8_fall_back_to_hr1_until_assigned(string role)
    {
        var directory = Directory((ProcessRoles.Hr1, Alice), (ProcessRoles.Hr1, Bob));

        directory.UsersOf(role).ShouldBe(new[] { Alice, Bob }, ignoreOrder: true);
    }

    [Theory]
    [InlineData(ProcessRoles.Hr5)]
    [InlineData(ProcessRoles.Hr8)]
    public void Hr5_and_hr8_stop_falling_back_once_assigned(string role)
    {
        var directory = Directory((ProcessRoles.Hr1, Alice), (role, Bob));

        directory.UsersOf(role).ShouldBe(new[] { Bob });
    }

    [Theory]
    [InlineData(ProcessRoles.Hr5)]
    [InlineData(ProcessRoles.Hr8)]
    public void Hr5_and_hr8_are_unassigned_when_hr1_is_too(string role)
        => Directory((ProcessRoles.Hr2, Alice)).UsersOf(role).ShouldBeEmpty();

    [Theory]
    [InlineData(ProcessRoles.Hr2)]
    [InlineData(ProcessRoles.Hr3)]
    [InlineData(ProcessRoles.Hr4)]
    [InlineData(ProcessRoles.Lawyer)]
    [InlineData(ProcessRoles.SafetyEngineer)]
    public void Only_hr5_and_hr8_fall_back_to_hr1(string role)
        => Directory((ProcessRoles.Hr1, Alice)).UsersOf(role).ShouldBeEmpty();

    [Fact]
    public void Role_codes_are_compared_exactly()
        => Directory(("HR2", Alice)).UsersOf(ProcessRoles.Hr2).ShouldBeEmpty();

    [Fact]
    public void An_assigned_user_sees_exactly_their_roles()
    {
        var directory = Directory((ProcessRoles.Hr2, Alice), (ProcessRoles.Lawyer, Bob));

        directory.OfficeRolesOf(Alice, canManage: false).ShouldBe(OnlyHr2, ignoreOrder: true);
    }

    [Fact]
    public void Hr1_sees_hr5_and_hr8_while_they_are_unassigned()
    {
        var directory = Directory((ProcessRoles.Hr1, Alice));

        directory.OfficeRolesOf(Alice, canManage: false).ShouldBe(Hr1AndItsStandIns, ignoreOrder: true);
    }

    [Fact]
    public void Once_hr5_is_assigned_hr1_no_longer_sees_it()
    {
        var directory = Directory((ProcessRoles.Hr1, Alice), (ProcessRoles.Hr5, Bob));

        directory.OfficeRolesOf(Alice, canManage: false).ShouldBe(Hr1AndHr8, ignoreOrder: true);
        directory.OfficeRolesOf(Bob, canManage: false).ShouldBe(OnlyHr5, ignoreOrder: true);
    }

    [Fact]
    public void With_nobody_assigned_a_manager_sees_every_office_role()
        => Directory().OfficeRolesOf(Alice, canManage: true).ShouldBe(ProcessRoles.Office, ignoreOrder: true);

    [Fact]
    public void With_nobody_assigned_someone_who_cannot_manage_sees_nothing()
        => Directory().OfficeRolesOf(Alice, canManage: false).ShouldBeEmpty();

    [Fact]
    public void A_manager_sees_the_unassigned_roles_and_not_the_ones_someone_else_holds()
    {
        var roles = Directory((ProcessRoles.Hr2, Bob)).OfficeRolesOf(Alice, canManage: true);

        roles.ShouldNotContain(ProcessRoles.Hr2);
        roles.ShouldBe(ProcessRoles.Office.Except(OnlyHr2), ignoreOrder: true);
    }

    [Fact]
    public void A_manager_does_not_inherit_hr5_from_an_hr1_held_by_someone_else()
    {
        var roles = Directory((ProcessRoles.Hr1, Bob)).OfficeRolesOf(Alice, canManage: true);

        roles.ShouldBe(ProcessRoles.Office.Except(Hr1AndItsStandIns), ignoreOrder: true);
    }

    [Fact]
    public void A_manager_who_is_assigned_a_role_sees_it_with_the_unassigned_ones()
    {
        var roles = Directory((ProcessRoles.Hr2, Alice), (ProcessRoles.Lawyer, Bob)).OfficeRolesOf(Alice, canManage: true);

        roles.ShouldContain(ProcessRoles.Hr2);
        roles.ShouldNotContain(ProcessRoles.Lawyer);
        roles.ShouldContain(ProcessRoles.Director);
    }

    [Fact]
    public void Without_a_user_only_the_manager_rule_applies()
    {
        var directory = Directory((ProcessRoles.Hr2, Alice));

        directory.OfficeRolesOf(userId: null, canManage: false).ShouldBeEmpty();
        directory.OfficeRolesOf(userId: null, canManage: true).ShouldBe(ProcessRoles.Office.Except(OnlyHr2), ignoreOrder: true);
    }

    [Fact]
    public void A_role_outside_the_office_list_never_becomes_an_office_role()
    {
        var directory = Directory((ProcessRoles.DepartmentHead, Alice));

        directory.OfficeRolesOf(Alice, canManage: false).ShouldBeEmpty();
    }

    [Fact]
    public void No_assignments_at_all_is_refused()
        => Should.Throw<ArgumentNullException>(() => new ProcessHolderDirectory(null!));
}
