using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The read API of «Mening ishlarim» end to end (docs/SPEC-mening-ishlarim.md §10 #1–#8): real HTTP, real JSON, real
/// PostgreSQL, so every EF query of the slice is translated here at least once.
/// </summary>
/// <remarks>
/// The database is shared by the tests of the class and a pass sweeps all of it, so each test asserts on its own case
/// ids. Role assignments are global: a test that depends on them starts from <see cref="ClearRolesAsync"/>.
/// </remarks>
public sealed class ProcessReadApiTests : IClassFixture<BonviApiFactory>
{
    private const string Root = "/api/processes";
    private const string F09Dismissal = """{"kind":{"value":"dismissal"}}""";
    private const string F09WithARow = """{"kind":{"value":"dismissal"},"tasks":[{"name":"Hisobot"}]}""";

    private static readonly string[] Sections = ["urgent", "later", "held", "waiting"];
    private static readonly string[] ActAndApplicationSteps = ["ex-3", "ex-4", "ex-5", "ex-6"];

    private readonly BonviApiFactory _factory;

    public ProcessReadApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => _factory.Services.GetRequiredService<IClock>().Today;

    public static TheoryData<string> Routes =>
    [
        Root + "/my",
        Root + "/my/count",
        Root + "/cases",
        Root + "/board?process=hire-request",
        Root + "/cases/019a0000-0000-7000-8000-000000000001",
        Root + "/cases/019a0000-0000-7000-8000-000000000001/chain",
        Root + "/late",
        Root + "/templates",
        Root + "/status",
    ];

    // ────────────────────────────────────────────────────────────
    //  Auth and RBAC, per endpoint
    // ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task No_token_is_401_and_shows_nothing(string route)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("processCode");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Expired_token_is_401_though_the_same_token_unexpired_gets_past_auth(string route)
    {
        var account = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);

        using var expired = ClientWith(Resigned(account.AccessToken, DateTime.UtcNow.AddDays(-1)));
        using (var response = await expired.GetAsync(new Uri(route, UriKind.Relative), Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // The control: only the expiry differs, so the 401 above can be nothing else.
        using var valid = ClientWith(Resigned(account.AccessToken, DateTime.UtcNow.AddHours(1)));
        using (var response = await valid.GetAsync(new Uri(route, UriKind.Relative), Ct))
        {
            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
            response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Without_processes_view_is_403_whatever_else_the_account_holds(string route)
    {
        using var client = await ClientAsync(Permissions.EmployeesView, Permissions.ProcessesWork, Permissions.ProcessesManage, Permissions.ProcessesOverride);

        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("processCode");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task No_permission_at_all_is_403(string route)
    {
        using var client = await ClientAsync();

        using var response = await client.GetAsync(new Uri(route, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Processes_view_alone_reads_every_endpoint_and_the_body_has_its_shape()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        var my = await GetAsync(client, Root + "/my");
        foreach (var section in Sections)
        {
            my.GetProperty(section).ValueKind.ShouldBe(JsonValueKind.Array);
        }

        my.GetProperty("today").GetString().ShouldBe(Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        my.GetProperty("tartib").GetProperty("counts").GetArrayLength().ShouldBe(0, "no destination permission, no count card");
        my.GetProperty("tartib").GetProperty("notifications").GetArrayLength().ShouldBe(0);

        (await GetAsync(client, Root + "/my/count")).GetProperty("count").GetInt32().ShouldBe(0);

        var cases = await GetAsync(client, Root + "/cases");
        cases.GetProperty("page").GetInt32().ShouldBe(1);
        cases.GetProperty("pageSize").GetInt32().ShouldBe(50);
        cases.GetProperty("items").ValueKind.ShouldBe(JsonValueKind.Array);

        (await GetAsync(client, Root + "/board?process=hire-request")).GetProperty("processCode").GetString().ShouldBe("hire-request");

        (await GetAsync(client, Root + "/late")).GetProperty("blocked").ValueKind.ShouldBe(JsonValueKind.Array);
        (await GetAsync(client, Root + "/templates")).GetArrayLength().ShouldBe(5);
        (await GetAsync(client, Root + "/status")).GetProperty("engine").GetString().ShouldBeOneOf("dormant", "active");
    }

    // ────────────────────────────────────────────────────────────
    //  #1, #2 — /my and /my/count
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task My_work_splits_the_holders_steps_and_each_task_has_its_full_card()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await RunningSinceAsync(await FormaliseAsync(hire), Today.AddDays(-7));

        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(viewer.AccessToken);

        var my = await GetAsync(client, Root + "/my");

        var urgent = StepsOf(my, "urgent", processCase.Id);
        urgent.ShouldContain("r01-2", "due the working day before the hire");
        urgent.ShouldContain(ProcessStepIds.PayrollHandover, "Blocked on Q03: the holder clears it, so it is urgent");

        StepsOf(my, "later", processCase.Id).ShouldBeSubsetOf(["r01-6", "r01-7"]);
        StepsOf(my, "later", processCase.Id).ShouldNotBeEmpty();
        StepsOf(my, "held", processCase.Id).ShouldBeEmpty();

        // Not this viewer's: hr3's and the safety engineer's steps of the same case.
        var everySection = Sections.SelectMany(section => StepsOf(my, section, processCase.Id)).ToList();
        everySection.ShouldNotContain("r01-8a");
        everySection.ShouldNotContain(ProcessStepIds.SafetyInstruction);
        everySection.ShouldNotContain("r01-3", "Locked behind the viewer's own r01-2: not «waiting on someone else»");

        var card = Task(my, "urgent", processCase.Id, "r01-2");
        card.GetProperty("processCode").GetString().ShouldBe(ProcessCodes.HireFormalise);
        card.GetProperty("processTitle").GetString().ShouldBe("Ishga qabulni rasmiylashtirish");
        card.GetProperty("stepTitle").GetString().ShouldNotBe("r01-2", "the title comes from the definition");
        card.GetProperty("state").GetString().ShouldBe("Active");
        card.GetProperty("daysLate").GetInt32().ShouldBeGreaterThan(0);
        card.GetProperty("legacy").GetBoolean().ShouldBeFalse();
        card.GetProperty("blankCode").GetString().ShouldBe("HR-F05");
        card.GetProperty("gateCode").ValueKind.ShouldBe(JsonValueKind.Null);
        card.GetProperty("link").GetProperty("screen").GetString().ShouldNotBeNullOrEmpty();

        var subject = card.GetProperty("subject");
        subject.GetProperty("kind").GetString().ShouldBe("Employment");
        subject.GetProperty("name").GetString().ShouldBe(hire.FullName);
        subject.GetProperty("employmentId").GetGuid().ShouldBe(hire.EmploymentId);
        subject.GetProperty("employeeId").GetGuid().ShouldBe(hire.EmployeeId);

        var holder = card.GetProperty("holder");
        holder.GetProperty("role").GetString().ShouldBe(ProcessRoles.Hr2);
        holder.GetProperty("unassigned").GetBoolean().ShouldBeFalse();
        holder.GetProperty("users").EnumerateArray().Single().GetProperty("userId").GetGuid().ShouldBe(viewer.UserId);
        holder.GetProperty("users").EnumerateArray().Single().GetProperty("name").GetString().ShouldNotBeNullOrEmpty();

        var blocked = Task(my, "urgent", processCase.Id, ProcessStepIds.PayrollHandover);
        blocked.GetProperty("state").GetString().ShouldBe("Blocked");
        blocked.GetProperty("gateCode").GetString().ShouldBe(ProcessGateCodes.Q03);
        blocked.GetProperty("gateErrorCode").GetString().ShouldBe("jarayon.jshshir_yoq");
        blocked.GetProperty("resolver").GetProperty("role").GetString().ShouldBe(ProcessRoles.Hr2);
    }

    [Fact]
    public async Task My_count_equals_urgent_plus_later_and_leaves_out_held()
    {
        await ClearRolesAsync();
        var parent = await BlockedFirstDayAsync();
        var role = (await CaseByIdAsync(parent.Id)).FindStep(ProcessStepIds.FirstDay)!.HolderRole.ShouldNotBeNull();
        await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();

        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(viewer.UserId, role);
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(viewer.AccessToken);

        var my = await GetAsync(client, Root + "/my");
        var count = (await GetAsync(client, Root + "/my/count")).GetProperty("count").GetInt32();

        var urgent = my.GetProperty("urgent").GetArrayLength();
        var later = my.GetProperty("later").GetArrayLength();
        var held = my.GetProperty("held").GetArrayLength();

        held.ShouldBeGreaterThan(0, "the Q04 step is the point of this test");
        (urgent + later).ShouldBeGreaterThan(0);
        count.ShouldBe(urgent + later);
        my.GetProperty("count").GetInt32().ShouldBe(count);

        var lateCount = my.GetProperty("urgent").EnumerateArray()
            .Count(task => task.GetProperty("state").GetString() == "Active" && task.GetProperty("daysLate").GetInt32() > 0);
        my.GetProperty("lateCount").GetInt32().ShouldBe(lateCount);
    }

    [Fact]
    public async Task Blocked_step_is_held_while_someone_else_holds_the_cure_and_urgent_once_the_viewer_does()
    {
        await ClearRolesAsync();
        var parent = await BlockedFirstDayAsync();
        var role = (await CaseByIdAsync(parent.Id)).FindStep(ProcessStepIds.FirstDay)!.HolderRole.ShouldNotBeNull();

        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(viewer.UserId, role);
        using var client = ClientWith(viewer.AccessToken);

        var before = await GetAsync(client, Root + "/my");

        StepsOf(before, "urgent", parent.Id).ShouldNotContain(ProcessStepIds.FirstDay);
        StepsOf(before, "held", parent.Id).ShouldContain(ProcessStepIds.FirstDay);

        var held = Task(before, "held", parent.Id, ProcessStepIds.FirstDay);
        held.GetProperty("gateCode").GetString().ShouldBe(ProcessGateCodes.Q04);
        held.GetProperty("gateErrorCode").GetString().ShouldBe("jarayon.yoriqnoma_yoq");
        held.GetProperty("resolver").GetProperty("role").GetString().ShouldBe(ProcessRoles.SafetyEngineer);

        await AssignRoleAsync(viewer.UserId, ProcessRoles.SafetyEngineer);

        var after = await GetAsync(client, Root + "/my");

        StepsOf(after, "held", parent.Id).ShouldNotContain(ProcessStepIds.FirstDay);
        StepsOf(after, "urgent", parent.Id).ShouldContain(ProcessStepIds.FirstDay);
    }

    [Fact]
    public async Task Waiting_is_a_locked_step_within_seven_days_behind_someone_elses_open_step()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        await StandInsAsync();

        var soon = await DismissalAsync(leaveDate: Today.AddDays(3), act: F09Dismissal);
        var far = await DismissalAsync(leaveDate: Today.AddDays(30), act: F09Dismissal);

        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(viewer.AccessToken);

        var my = await GetAsync(client, Root + "/my");

        (await CaseByIdAsync(soon.Id)).FindStep("ex-8")!.State.ShouldBe(ProcessStepState.Locked);
        StepsOf(my, "waiting", soon.Id).ShouldContain("ex-8", "ex-3 is hr4's open step");
        StepsOf(my, "waiting", far.Id).ShouldNotContain("ex-8", "due beyond the horizon");

        var card = Task(my, "waiting", soon.Id, "ex-8");
        card.GetProperty("state").GetString().ShouldBe("Locked");

        // The blocker is now the viewer's own next task, so the step is no longer «waiting on someone else».
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr4);
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr3);
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr1);

        var own = await GetAsync(client, Root + "/my");
        var soonCase = await CaseByIdAsync(soon.Id);
        var openBlockers = ActAndApplicationSteps
            .Where(code => soonCase.FindStep(code)!.State is ProcessStepState.Active or ProcessStepState.Blocked)
            .ToList();
        openBlockers.ShouldContain("ex-3");
        StepsOf(own, "waiting", soon.Id).ShouldNotContain("ex-8");
    }

    [Fact]
    public async Task Viewer_with_no_role_no_person_and_no_manage_sees_no_task_at_all()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();

        using var client = await ClientAsync(Permissions.ProcessesView);

        var my = await GetAsync(client, Root + "/my");

        foreach (var section in Sections)
        {
            my.GetProperty(section).GetArrayLength().ShouldBe(0, section);
        }

        my.GetProperty("count").GetInt32().ShouldBe(0);
        (await GetAsync(client, Root + "/my/count")).GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Unassigned_office_role_goes_to_managers_and_stops_when_someone_is_assigned()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await FormaliseAsync(hire);

        using var manager = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesManage);

        var unassigned = await GetAsync(manager, Root + "/my");
        var card = Task(unassigned, "urgent", processCase.Id, "r01-2");
        card.GetProperty("holder").GetProperty("unassigned").GetBoolean().ShouldBeTrue();
        card.GetProperty("holder").GetProperty("users").GetArrayLength().ShouldBe(0);

        var someoneElse = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(someoneElse.UserId, ProcessRoles.Hr2);

        var assigned = await GetAsync(manager, Root + "/my");
        StepsOf(assigned, "urgent", processCase.Id).ShouldNotContain("r01-2", "the role has a holder now, the manager is not one");
        StepsOf(assigned, "urgent", processCase.Id).ShouldContain(ProcessStepIds.SafetyInstruction, "still unassigned");
    }

    [Fact]
    public async Task Step_resolved_to_a_person_is_theirs_alone_not_another_employees_nor_a_managers()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        await StandInsAsync();
        var hire = await HireAsync(orderNo: null, hireDate: Today);
        var owner = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await LinkUserAsync(owner.UserId, hire.EmployeeId);
        await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));

        var step = processCase.FindStep("ex-4")!;
        step.State.ShouldBe(ProcessStepState.Active);
        step.HolderEmployeeId.ShouldBe(hire.EmployeeId);

        var other = await HireAsync(orderNo: null, hireDate: Today);
        var stranger = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await LinkUserAsync(stranger.UserId, other.EmployeeId);

        using var ownerClient = ClientWith(owner.AccessToken);
        using var strangerClient = ClientWith(stranger.AccessToken);
        using var manager = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);

        var mine = await GetAsync(ownerClient, Root + "/my");
        var card = Task(mine, "urgent", processCase.Id, "ex-4", orSection: "later");
        card.GetProperty("holder").GetProperty("employeeId").GetGuid().ShouldBe(hire.EmployeeId);
        card.GetProperty("holder").GetProperty("employeeName").GetString().ShouldBe(hire.FullName);

        foreach (var client in new[] { strangerClient, manager })
        {
            var theirs = await GetAsync(client, Root + "/my");
            foreach (var section in Sections)
            {
                StepsOf(theirs, section, processCase.Id).ShouldNotContain("ex-4", section);
            }
        }

        var seen = StepOfDetail(await GetAsync(strangerClient, $"{Root}/cases/{processCase.Id}"), "ex-4");
        seen.GetProperty("viewerHolds").GetBoolean().ShouldBeFalse();
        seen.GetProperty("canComplete").GetBoolean().ShouldBeFalse();
        StepOfDetail(await GetAsync(ownerClient, $"{Root}/cases/{processCase.Id}"), "ex-4").GetProperty("viewerHolds").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Finished_case_has_left_the_task_list()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await FormaliseAsync(hire);

        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(viewer.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(viewer.AccessToken);

        StepsOf(await GetAsync(client, Root + "/my"), "urgent", processCase.Id).ShouldNotBeEmpty();
        var before = (await GetAsync(client, Root + "/my/count")).GetProperty("count").GetInt32();

        await SqlAsync($"UPDATE process_cases SET status = {(short)ProcessCaseStatus.Cancelled}, cancelled_at = now(), cancel_reason = 'test' WHERE id = {processCase.Id}");

        var my = await GetAsync(client, Root + "/my");
        StepsOf(my, "urgent", processCase.Id).ShouldBeEmpty();
        StepsOf(my, "later", processCase.Id).ShouldBeEmpty();
        (await GetAsync(client, Root + "/my/count")).GetProperty("count").GetInt32().ShouldBeLessThan(before);
    }

    // ────────────────────────────────────────────────────────────
    //  Tartib cards
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tartib_counts_follow_the_destination_permission_and_are_exact()
    {
        using var attendance = await ClientAsync(Permissions.ProcessesView, Permissions.AttendanceManage);
        using var employees = await ClientAsync(Permissions.ProcessesView, Permissions.EmployeesView);

        var photoBefore = TartibCount(await GetAsync(employees, Root + "/my"), "employeesWithoutPhoto");
        var scheduleBefore = TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule");

        var hire = await HireAsync(orderNo: null, hireDate: Today);

        var employeesView = await GetAsync(employees, Root + "/my");
        TartibCount(employeesView, "employeesWithoutPhoto").ShouldBe(photoBefore + 1);
        TartibKeys(employeesView).ShouldNotContain("employeesWithoutSchedule", "that card leads to a screen this viewer cannot open");
        TartibKeys(employeesView).ShouldNotContain("unmatchedTerminalPersons");

        var attendanceView = await GetAsync(attendance, Root + "/my");
        TartibCount(attendanceView, "employeesWithoutSchedule").ShouldBe(scheduleBefore + 1);
        TartibKeys(attendanceView).ShouldNotContain("employeesWithoutPhoto");
        TartibKeys(attendanceView).ShouldNotContain("unmatchedTerminalPersons", "zero is omitted, and no terminal has reported anyone");

        // An assignment that covers the employee takes them out of the count; an expired one does not.
        var schedule = await ScheduleAsync();
        var expired = await AssignScheduleAsync(schedule, ScheduleScope.Employee, hire.EmployeeId, Today.AddDays(-30), Today.AddDays(-1));
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(scheduleBefore + 1, "the assignment has ended");

        var current = await AssignScheduleAsync(schedule, ScheduleScope.Employee, hire.EmployeeId, Today.AddDays(1), null);
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(scheduleBefore + 1, "not in force yet");

        await DeleteAssignmentAsync(expired);
        await DeleteAssignmentAsync(current);
        var effective = await AssignScheduleAsync(schedule, ScheduleScope.Employee, hire.EmployeeId, Today.AddDays(-1), null);
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(scheduleBefore);

        await DeleteAssignmentAsync(effective);
    }

    [Fact]
    public async Task Tartib_schedule_count_honours_department_position_and_default_assignments()
    {
        using var attendance = await ClientAsync(Permissions.ProcessesView, Permissions.AttendanceManage);
        var schedule = await ScheduleAsync();

        var department = await DepartmentAsync();
        var position = await PositionAsync();
        var byDepartment = await HireAsync(orderNo: null, hireDate: Today, departmentId: department);
        var byPosition = await HireAsync(orderNo: null, hireDate: Today, positionId: position);

        var before = TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule");
        before.ShouldBeGreaterThanOrEqualTo(2);

        var viaDepartment = await AssignScheduleAsync(schedule, ScheduleScope.Department, department, Today.AddDays(-1), null);
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(before - 1);

        var viaPosition = await AssignScheduleAsync(schedule, ScheduleScope.Position, position, Today.AddDays(-1), null);
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(before - 2);

        var viaDefault = await AssignScheduleAsync(schedule, ScheduleScope.Default, null, Today.AddDays(-1), null);
        TartibKeys(await GetAsync(attendance, Root + "/my")).ShouldNotContain("employeesWithoutSchedule", "a default assignment reaches everyone, and zero is omitted");

        await DeleteAssignmentAsync(viaDefault);
        await DeleteAssignmentAsync(viaPosition);
        await DeleteAssignmentAsync(viaDepartment);
        byDepartment.EmploymentId.ShouldNotBe(byPosition.EmploymentId);
    }

    [Fact]
    public async Task Tartib_schedule_count_reaches_a_sub_department_through_its_parent()
    {
        using var attendance = await ClientAsync(Permissions.ProcessesView, Permissions.AttendanceManage);
        var schedule = await ScheduleAsync();
        var parent = await DepartmentAsync();
        var child = await DepartmentAsync(parentId: parent);
        var grandchild = await DepartmentAsync(parentId: child);
        await HireAsync(orderNo: null, hireDate: Today, departmentId: child);
        await HireAsync(orderNo: null, hireDate: Today, departmentId: grandchild);

        var before = TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule");

        var viaChild = await AssignScheduleAsync(schedule, ScheduleScope.Department, grandchild, Today.AddDays(-1), null);
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(before - 1, "a child's assignment does not reach its parent");
        await DeleteAssignmentAsync(viaChild);

        var viaParent = await AssignScheduleAsync(schedule, ScheduleScope.Department, parent, Today.AddDays(-1), null);
        TartibCount(await GetAsync(attendance, Root + "/my"), "employeesWithoutSchedule").ShouldBe(before - 2, "ScheduleResolver walks up the department tree");
        await DeleteAssignmentAsync(viaParent);
    }

    [Fact]
    public async Task Tartib_shows_only_the_listed_notification_types_under_their_own_permission()
    {
        var vacancy = await VacancyAsync();
        var candidate = await CandidateAsync(vacancy);
        var name = await DbAsync(db => db.Candidates.AsNoTracking()
            .Where(row => row.Id == candidate)
            .Select(row => row.FullName.LastName)
            .SingleAsync(Ct));
        await SqlAsync($"UPDATE candidates SET reapplied_at = {DateTimeOffset.UtcNow} WHERE id = {candidate}");

        using var recruiter = await ClientAsync(Permissions.ProcessesView, Permissions.RecruitingView);

        var notifications = (await GetAsync(recruiter, Root + "/my")).GetProperty("tartib").GetProperty("notifications").EnumerateArray().ToList();

        var types = notifications.Select(item => item.GetProperty("type").GetString()).ToList();
        types.ShouldContain("CandidateReapplied");
        types.ShouldNotContain("CandidateNew", "§11.8: the processes cover it");
        types.ShouldNotContain("CandidateFromBot");
        types.ShouldNotContain("VacancyRequested");
        notifications.ShouldContain(item => item.GetRawText().Contains(name, StringComparison.Ordinal));

        using var outsider = await ClientAsync(Permissions.ProcessesView);
        (await GetAsync(outsider, Root + "/my")).GetProperty("tartib").GetProperty("notifications").GetArrayLength().ShouldBe(0);
    }

    // ────────────────────────────────────────────────────────────
    //  #3 — /cases
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cases_each_filter_keeps_its_case_and_drops_the_other()
    {
        await StartEngineAsync();
        var departmentA = await DepartmentAsync();
        var departmentB = await DepartmentAsync();
        var hireA = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today, departmentId: departmentA);
        var hireB = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today, departmentId: departmentB);
        var request = await VacancyRequestAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();

        var caseA = await RunningSinceAsync(await FormaliseAsync(hireA), Today.AddDays(-7));
        var caseB = await FormaliseAsync(hireB);
        var requestCase = await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id));
        var candidateCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));

        using var client = await ClientAsync(Permissions.ProcessesView);

        async Task<List<Guid>> IdsAsync(string query)
            => [.. (await GetAsync(client, $"{Root}/cases?pageSize=10000&{query}")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

        var all = await IdsAsync(string.Empty);
        new[] { caseA.Id, caseB.Id, requestCase.Id, candidateCase.Id }.ShouldBeSubsetOf(all);

        var byProcess = await IdsAsync("process=hire-formalise");
        byProcess.ShouldContain(caseA.Id);
        byProcess.ShouldNotContain(requestCase.Id);
        byProcess.ShouldNotContain(candidateCase.Id);

        var byDepartment = await IdsAsync($"department={departmentA}");
        byDepartment.ShouldBe([caseA.Id]);

        (await IdsAsync($"employee={hireB.EmployeeId}")).ShouldBe([caseB.Id]);
        (await IdsAsync($"candidate={candidate}")).ShouldBe([candidateCase.Id]);

        var byHolder = await IdsAsync("holderRole=hr3");
        byHolder.ShouldContain(caseA.Id, "r01-8a is hr3's Active step");
        byHolder.ShouldNotContain(requestCase.Id);

        var late = await IdsAsync("late=true");
        late.ShouldContain(caseA.Id, "r01-2 was due before the hire");
        late.ShouldNotContain(requestCase.Id);

        var notLate = await IdsAsync("late=false");
        notLate.ShouldContain(requestCase.Id);
        notLate.ShouldNotContain(caseA.Id);

        // Same filters combine, not widen.
        (await IdsAsync($"department={departmentA}&employee={hireB.EmployeeId}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Cases_list_item_has_its_full_card()
    {
        await StartEngineAsync();
        var department = await DepartmentAsync("Oʻqish " + Suffix());
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today, departmentId: department);
        await RunAsync();
        var started = await FormaliseAsync(hire);
        started.StartedOn.ShouldBe(Today, "the day the hire was recorded");
        var since = Today.AddDays(-7);
        var processCase = await RunningSinceAsync(started, since);

        using var client = await ClientAsync(Permissions.ProcessesView);

        var page = await GetAsync(client, $"{Root}/cases?employee={hire.EmployeeId}");
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("pageCount").GetInt32().ShouldBe(1);

        var item = page.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(processCase.Id);
        item.GetProperty("processTitle").GetString().ShouldBe("Ishga qabulni rasmiylashtirish");
        item.GetProperty("status").GetString().ShouldBe("Active");
        item.GetProperty("outcome").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("departmentId").GetGuid().ShouldBe(department);
        item.GetProperty("departmentName").GetString().ShouldStartWith("Oʻqish");
        item.GetProperty("subject").GetProperty("name").GetString().ShouldBe(hire.FullName);
        item.GetProperty("startedOn").GetString().ShouldBe(Iso(since));
        item.GetProperty("completedAt").ValueKind.ShouldBe(JsonValueKind.Null);

        var current = item.GetProperty("currentSteps").EnumerateArray().ToList();
        current.Select(step => step.GetProperty("stepCode").GetString()).ShouldContain(ProcessStepIds.PayrollHandover);
        current.Select(step => step.GetProperty("stepCode").GetString()).ShouldNotContain("r01-3", "Locked is not current");
        current.Select(step => step.GetProperty("stepCode").GetString()).ShouldNotContain("r01-4", "Done is not current");

        var blocked = current.Single(step => step.GetProperty("stepCode").GetString() == ProcessStepIds.PayrollHandover);
        blocked.GetProperty("state").GetString().ShouldBe("Blocked");
        blocked.GetProperty("gateCode").GetString().ShouldBe(ProcessGateCodes.Q03);
        blocked.GetProperty("column").GetString().ShouldBe("firstDay");
        blocked.GetProperty("holder").GetProperty("role").GetString().ShouldBe(ProcessRoles.Hr2);

        item.GetProperty("daysLate").GetInt32().ShouldBe(current.Max(step => step.GetProperty("daysLate").GetInt32()));
        item.GetProperty("daysLate").GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Cases_status_filter_reads_names_and_a_finished_case_shows_how_it_ended()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));

        using var client = await ClientAsync(Permissions.ProcessesView);

        (await TotalAsync(client, $"candidate={candidate}&status=Active")).ShouldBe(1);
        (await TotalAsync(client, $"candidate={candidate}&status=active")).ShouldBe(1, "any case, the same parser as the rest");
        (await TotalAsync(client, $"candidate={candidate}&status=Cancelled")).ShouldBe(0);

        await SqlAsync($"UPDATE candidates SET deleted_at = now() WHERE id = {candidate}");
        await RunAsync();

        (await TotalAsync(client, $"candidate={candidate}&status=Active")).ShouldBe(0);
        var item = (await GetAsync(client, $"{Root}/cases?candidate={candidate}&status=Cancelled")).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(processCase.Id);
        item.GetProperty("status").GetString().ShouldBe("Cancelled");
        item.GetProperty("cancelledAt").ValueKind.ShouldBe(JsonValueKind.String);
        item.GetProperty("daysLate").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData("status=Nope")]
    [InlineData("status=1")]
    public async Task Cases_unknown_status_is_400_with_the_code(string query)
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        await ProblemAsync(client, $"{Root}/cases?{query}", HttpStatusCode.BadRequest, "jarayon.kiritish_notogri");
    }

    [Fact]
    public async Task Cases_reversed_dates_are_400_with_the_list_code()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        await ProblemAsync(client, $"{Root}/cases?from=2026-02-01&to=2026-01-01", HttpStatusCode.BadRequest, "royxat.sana_oraligi_notogri");
    }

    [Theory]
    [InlineData("department=not-a-guid")]
    [InlineData("employee=1")]
    [InlineData("from=yesterday")]
    [InlineData("late=maybe")]
    [InlineData("page=abc")]
    [InlineData("pageSize=1.5")]
    public async Task Cases_parameter_of_the_wrong_type_is_400(string query)
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        using var response = await client.GetAsync(new Uri($"{Root}/cases?{query}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("\"items\"");
    }

    [Fact]
    public async Task Cases_dates_are_both_included()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
        var day = new DateOnly(2021, 3, 15);
        await SqlAsync($"UPDATE process_cases SET started_on = {day} WHERE id = {processCase.Id}");

        using var client = await ClientAsync(Permissions.ProcessesView);

        (await TotalAsync(client, $"candidate={candidate}&from=2021-03-15&to=2021-03-15")).ShouldBe(1);
        (await TotalAsync(client, $"candidate={candidate}&from=2021-03-16")).ShouldBe(0);
        (await TotalAsync(client, $"candidate={candidate}&to=2021-03-14")).ShouldBe(0);
    }

    [Fact]
    public async Task Cases_search_finds_employee_candidate_and_request_title_blind_to_the_alphabet()
    {
        await StartEngineAsync();
        var token = "Qdr" + Suffix();
        var cyrillic = await HireNamedAsync("Шукуруллаев", "Жасур");
        var named = await HireNamedAsync(token + "ov", "Test");
        var candidate = await CandidateNamedAsync(token + "qizi");
        var request = await VacancyRequestAsync(title: "Lavozim " + token);
        var outsider = await HireAsync(orderNo: null, hireDate: Today);
        await RunAsync();

        using var client = await ClientAsync(Permissions.ProcessesView);

        var found = await IdsOfAsync(client, $"q={token}");
        found.ShouldContain((await FormaliseAsync(named)).Id);
        found.ShouldContain((await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today))).Id);
        found.ShouldContain((await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).Id);
        found.ShouldNotContain((await FormaliseAsync(outsider)).Id);

        (await IdsOfAsync(client, "q=Shukurullaev")).ShouldContain((await FormaliseAsync(cyrillic)).Id, "the employee search rule: a Latin query finds a Cyrillic name");

        // A pattern character is a character here, not a wildcard.
        (await IdsOfAsync(client, "q=%25")).ShouldNotContain((await FormaliseAsync(named)).Id);
    }

    [Fact]
    public async Task Cases_paging_boundaries_are_clamped_and_order_is_newest_first()
    {
        await StartEngineAsync();
        var department = await DepartmentAsync();
        var hires = new List<Hire>();
        for (var index = 0; index < 3; index++)
        {
            hires.Add(await HireAsync(orderNo: null, hireDate: Today, departmentId: department));
        }

        await RunAsync();
        var ids = new List<Guid>();
        foreach (var hire in hires)
        {
            ids.Add((await FormaliseAsync(hire)).Id);
        }

        var oldest = ids[0];
        await SqlAsync($"UPDATE process_cases SET started_on = {new DateOnly(2021, 1, 1)} WHERE id = {oldest}");

        using var client = await ClientAsync(Permissions.ProcessesView);

        var filter = $"department={department}";

        var first = await GetAsync(client, $"{Root}/cases?{filter}&pageSize=2&page=1");
        first.GetProperty("total").GetInt32().ShouldBe(3);
        first.GetProperty("pageCount").GetInt32().ShouldBe(2);
        first.GetProperty("pageSize").GetInt32().ShouldBe(2);
        first.GetProperty("items").GetArrayLength().ShouldBe(2);
        var firstIds = first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        firstIds.ShouldNotContain(oldest, "the oldest start comes last");

        var second = await GetAsync(client, $"{Root}/cases?{filter}&pageSize=2&page=2");
        second.GetProperty("page").GetInt32().ShouldBe(2);
        second.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(oldest);

        var beyond = await GetAsync(client, $"{Root}/cases?{filter}&pageSize=2&page=99");
        beyond.GetProperty("items").GetArrayLength().ShouldBe(0);
        beyond.GetProperty("total").GetInt32().ShouldBe(3, "the total does not depend on the page");

        var zero = await GetAsync(client, $"{Root}/cases?{filter}&page=0&pageSize=0");
        zero.GetProperty("page").GetInt32().ShouldBe(1);
        zero.GetProperty("pageSize").GetInt32().ShouldBe(1);

        var negative = await GetAsync(client, $"{Root}/cases?{filter}&page=-3&pageSize=-1");
        negative.GetProperty("page").GetInt32().ShouldBe(1);
        negative.GetProperty("pageSize").GetInt32().ShouldBe(1);

        var huge = await GetAsync(client, $"{Root}/cases?{filter}&pageSize=999999");
        huge.GetProperty("pageSize").GetInt32().ShouldBe(10_000);
        huge.GetProperty("items").GetArrayLength().ShouldBe(3);

        (await GetAsync(client, $"{Root}/cases?{filter}")).GetProperty("pageSize").GetInt32().ShouldBe(50);

        // No case at all is a page, not an error.
        var empty = await GetAsync(client, $"{Root}/cases?department={Guid.NewGuid()}");
        empty.GetProperty("total").GetInt32().ShouldBe(0);
        empty.GetProperty("pageCount").GetInt32().ShouldBe(0);
        empty.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    // ────────────────────────────────────────────────────────────
    //  #4 — /board
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Board_without_process_is_400_and_an_unknown_one_404()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        await ProblemAsync(client, Root + "/board", HttpStatusCode.BadRequest, "jarayon.kiritish_notogri");
        await ProblemAsync(client, Root + "/board?process=%20", HttpStatusCode.BadRequest, "jarayon.kiritish_notogri");
        await ProblemAsync(client, Root + "/board?process=no-such-process", HttpStatusCode.NotFound, "jarayon.topilmadi");
    }

    [Fact]
    public async Task Board_puts_each_running_case_in_its_column_and_lets_finished_ones_go()
    {
        await StartEngineAsync();
        var department = await DepartmentAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today, departmentId: department);
        var cancelled = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await FormaliseAsync(hire);
        var gone = await FormaliseAsync(cancelled);
        await SqlAsync($"UPDATE process_cases SET status = {(short)ProcessCaseStatus.Cancelled}, cancelled_at = now(), cancel_reason = 'test' WHERE id = {gone.Id}");

        using var client = await ClientAsync(Permissions.ProcessesView);

        var board = await GetAsync(client, Root + "/board?process=hire-formalise");

        board.GetProperty("processCode").GetString().ShouldBe(ProcessCodes.HireFormalise);
        board.GetProperty("processTitle").GetString().ShouldBe("Ishga qabulni rasmiylashtirish");

        var columns = board.GetProperty("columns").EnumerateArray().ToList();
        columns.Select(column => column.GetProperty("key").GetString()).ShouldBe(["docs", "order", "after", "firstDay"], "the template's order");
        columns[0].GetProperty("title").GetString().ShouldBe("Hujjatlar");

        var inDocs = columns[0].GetProperty("cards").EnumerateArray().ToList();
        var card = inDocs.Single(item => item.GetProperty("caseId").GetGuid() == processCase.Id);
        card.GetProperty("departmentId").GetGuid().ShouldBe(department);
        card.GetProperty("departmentName").GetString().ShouldNotBeNullOrEmpty();
        card.GetProperty("subject").GetProperty("name").GetString().ShouldBe(hire.FullName);
        card.GetProperty("legacy").GetBoolean().ShouldBeFalse();

        var current = card.GetProperty("currentSteps").EnumerateArray().Select(step => step.GetProperty("stepCode").GetString()).ToList();
        current.ShouldContain("r01-2");
        current.ShouldNotContain("r01-3");

        var row = await CaseByIdAsync(processCase.Id);
        var earliest = row.Steps
            .Where(step => step.State is ProcessStepState.Active or ProcessStepState.Blocked or ProcessStepState.Waiting && step.DueDate is not null)
            .Min(step => step.DueDate);
        card.GetProperty("dueDate").GetString().ShouldBe(Iso(earliest!.Value));
        card.GetProperty("daysLate").GetInt32().ShouldBe(Today.DayNumber - earliest.Value.DayNumber);

        var everyCard = columns.SelectMany(column => column.GetProperty("cards").EnumerateArray()).Select(item => item.GetProperty("caseId").GetGuid()).ToList();
        everyCard.ShouldNotContain(gone.Id);
        everyCard.Count(id => id == processCase.Id).ShouldBe(1, "a case stands in one column only");
    }

    [Fact]
    public async Task Board_with_no_case_still_has_every_column()
    {
        // Probation has its columns; nothing here starts a probation case in this test's process list.
        using var client = await ClientAsync(Permissions.ProcessesView);

        var board = await GetAsync(client, Root + "/board?process=probation");

        board.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("key").GetString())
            .ShouldBe(["start", "d7", "d30", "final", "decision"]);
    }

    // ────────────────────────────────────────────────────────────
    //  #5 — /cases/{id}
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Case_unknown_id_is_404_with_the_code_and_a_malformed_id_does_not_match_the_route()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        await ProblemAsync(client, $"{Root}/cases/{Guid.NewGuid()}", HttpStatusCode.NotFound, "jarayon.topilmadi");

        using var malformed = await client.GetAsync(new Uri($"{Root}/cases/not-a-guid", UriKind.Relative), Ct);
        malformed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Case_detail_has_every_step_in_order_with_definition_and_columns()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await FormaliseAsync(hire);
        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        using var client = ClientWith(viewer.AccessToken);

        var detail = await GetAsync(client, $"{Root}/cases/{processCase.Id}");

        detail.GetProperty("id").GetGuid().ShouldBe(processCase.Id);
        detail.GetProperty("processCode").GetString().ShouldBe(ProcessCodes.HireFormalise);
        detail.GetProperty("source").GetString().ShouldNotBeNullOrEmpty();
        detail.GetProperty("status").GetString().ShouldBe("Active");
        detail.GetProperty("anchorDate").GetString().ShouldBe(Iso(Today));
        detail.GetProperty("version").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
        detail.GetProperty("subject").GetProperty("name").GetString().ShouldBe(hire.FullName);
        detail.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("key").GetString()).ShouldBe(["docs", "order", "after", "firstDay"]);
        detail.GetProperty("blanks").GetArrayLength().ShouldBe(0);

        var steps = detail.GetProperty("steps").EnumerateArray().ToList();
        steps.Count.ShouldBe(processCase.Steps.Count);
        var orders = steps.Select(step => step.GetProperty("orderNo").GetInt32()).ToList();
        orders.ShouldBe(orders.Order().ToList());

        var manual = StepOfDetail(detail, "r01-3");
        manual.GetProperty("kind").GetString().ShouldBe("Manual");
        manual.GetProperty("state").GetString().ShouldBe("Locked");
        manual.GetProperty("prereqs").EnumerateArray().Select(item => item.GetString()).ShouldBe(["r01-2"]);
        manual.GetProperty("guide").ValueKind.ShouldBe(JsonValueKind.Array);
        manual.GetProperty("sourceRef").ValueKind.ShouldBe(JsonValueKind.String);
        manual.GetProperty("manual").GetProperty("allowed").GetBoolean().ShouldBeTrue();

        var blocked = StepOfDetail(detail, ProcessStepIds.PayrollHandover);
        blocked.GetProperty("gateCode").GetString().ShouldBe(ProcessGateCodes.Q03);
        blocked.GetProperty("gateErrorCode").GetString().ShouldBe("jarayon.jshshir_yoq");
        blocked.GetProperty("resolver").GetProperty("role").GetString().ShouldBe(ProcessRoles.Hr2);
        blocked.GetProperty("blankCode").GetString().ShouldBe("HR-F18");
        blocked.GetProperty("signature").GetString().ShouldBe("D1");
        blocked.GetProperty("manual").GetProperty("inputs").EnumerateArray().First().GetProperty("options").GetArrayLength().ShouldBe(2);

        var auto = StepOfDetail(detail, "r01-5");
        auto.GetProperty("state").GetString().ShouldBe("Done");
        auto.GetProperty("holder").GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Case_flags_say_what_this_viewer_may_do_on_each_step()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await FormaliseAsync(hire);
        var url = $"{Root}/cases/{processCase.Id}";

        var holder = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(holder.UserId, ProcessRoles.Hr2);
        using var holderClient = ClientWith(holder.AccessToken);

        var holderView = await GetAsync(holderClient, url);

        var ownFallback = StepOfDetail(holderView, "r01-2");
        ownFallback.GetProperty("viewerHolds").GetBoolean().ShouldBeTrue();
        ownFallback.GetProperty("canComplete").GetBoolean().ShouldBeTrue("a fact step with its declared manual fallback");

        var locked = StepOfDetail(holderView, "r01-3");
        locked.GetProperty("viewerHolds").GetBoolean().ShouldBeTrue();
        locked.GetProperty("canComplete").GetBoolean().ShouldBeFalse("Locked: only an Active step can be completed");

        var blocked = StepOfDetail(holderView, ProcessStepIds.PayrollHandover);
        blocked.GetProperty("canComplete").GetBoolean().ShouldBeFalse("Blocked: only an Active step can be completed");

        var someoneElses = StepOfDetail(holderView, ProcessStepIds.SafetyInstruction);
        someoneElses.GetProperty("viewerHolds").GetBoolean().ShouldBeFalse();
        someoneElses.GetProperty("canComplete").GetBoolean().ShouldBeFalse();

        var factWithNoFallback = StepOfDetail(holderView, "r01-8a");
        factWithNoFallback.GetProperty("canComplete").GetBoolean().ShouldBeFalse();

        // The holder without processes.work: the task shows, the button does not.
        var readOnly = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await AssignRoleAsync(readOnly.UserId, ProcessRoles.Hr2);
        using var readOnlyClient = ClientWith(readOnly.AccessToken);
        var readOnlyStep = StepOfDetail(await GetAsync(readOnlyClient, url), "r01-2");
        readOnlyStep.GetProperty("viewerHolds").GetBoolean().ShouldBeTrue();
        readOnlyStep.GetProperty("canComplete").GetBoolean().ShouldBeFalse();

        // A manager acts on any manual step, without holding it (§9.3).
        using var manager = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        var managerStep = StepOfDetail(await GetAsync(manager, url), "r01-2");
        managerStep.GetProperty("viewerHolds").GetBoolean().ShouldBeFalse("hr2 has a holder and it is not the manager");
        managerStep.GetProperty("canComplete").GetBoolean().ShouldBeTrue();

        // A non-holder with processes.work may do nothing here.
        using var stranger = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork);
        var strangerStep = StepOfDetail(await GetAsync(stranger, url), "r01-2");
        strangerStep.GetProperty("canComplete").GetBoolean().ShouldBeFalse();
        strangerStep.GetProperty("canSkip").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Case_undo_is_the_closers_for_the_window_and_the_managers_always_and_names_who_closed_it()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        await RunAsync();
        var processCase = await FormaliseAsync(hire);
        var url = $"{Root}/cases/{processCase.Id}";

        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await CloseManuallyAsync(processCase.Id, "r01-3", closer.UserId, """{"nothing":"needed","count":3}""");

        using var closerClient = ClientWith(closer.AccessToken);
        var closed = StepOfDetail(await GetAsync(closerClient, url), "r01-3");
        closed.GetProperty("state").GetString().ShouldBe("Done");
        closed.GetProperty("resolution").GetString().ShouldBe("Manual");
        closed.GetProperty("doneBy").GetGuid().ShouldBe(closer.UserId);
        closed.GetProperty("doneByName").GetString().ShouldNotBeNullOrEmpty();
        closed.GetProperty("doneAt").ValueKind.ShouldBe(JsonValueKind.String);
        closed.GetProperty("canUndo").GetBoolean().ShouldBeTrue();
        closed.GetProperty("canComplete").GetBoolean().ShouldBeFalse();

        var payload = closed.GetProperty("payload");
        payload.GetProperty("nothing").GetString().ShouldBe("needed");
        payload.GetProperty("count").GetInt32().ShouldBe(3);

        using var stranger = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork);
        StepOfDetail(await GetAsync(stranger, url), "r01-3").GetProperty("canUndo").GetBoolean().ShouldBeFalse("someone else's step");

        // Past the window: the closer may not, a manager still may.
        var old = DateTimeOffset.UtcNow - ProcessCase.UndoWindow - TimeSpan.FromMinutes(1);
        await SqlAsync($"UPDATE process_case_steps SET done_at = {old} WHERE case_id = {processCase.Id} AND step_code = 'r01-3'");

        StepOfDetail(await GetAsync(closerClient, url), "r01-3").GetProperty("canUndo").GetBoolean().ShouldBeFalse("past the window");

        using var manager = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        StepOfDetail(await GetAsync(manager, url), "r01-3").GetProperty("canUndo").GetBoolean().ShouldBeTrue();

        // The engine's own closure is never undone by hand.
        StepOfDetail(await GetAsync(manager, url), "r01-5").GetProperty("canUndo").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Case_is_reconciled_before_it_is_read()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));

        await SqlAsync($"UPDATE candidates SET deleted_at = now() WHERE id = {candidate}");

        using var client = await ClientAsync(Permissions.ProcessesView);
        var detail = await GetAsync(client, $"{Root}/cases/{processCase.Id}");

        detail.GetProperty("status").GetString().ShouldBe("Cancelled", "no pass ran but the read's own");
        detail.GetProperty("cancelReason").GetString().ShouldBe(ProcessCase.SubjectDeletedReason);
        detail.GetProperty("cancelledAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Case_lists_its_linked_blanks_and_names_the_template()
    {
        await StartEngineAsync();
        await StandInsAsync();
        var hire = await HireAsync(orderNo: null, hireDate: Today);
        var blank = await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));

        using var client = await ClientAsync(Permissions.ProcessesView);
        var detail = await GetAsync(client, $"{Root}/cases/{processCase.Id}");

        var row = detail.GetProperty("blanks").EnumerateArray().Single();
        row.GetProperty("id").GetGuid().ShouldBe(blank);
        row.GetProperty("templateCode").GetString().ShouldBe(ProcessBlanks.F09);
        row.GetProperty("templateTitle").GetString().ShouldNotBeNullOrEmpty();
        row.GetProperty("status").GetString().ShouldBe(nameof(BlankSubmissionStatus.Draft));

        StepOfDetail(detail, "ex-4").GetProperty("linkedBlankSubmissionId").GetGuid().ShouldBe(blank);
    }

    [Fact]
    public async Task Case_of_a_person_shows_the_position_and_a_candidate_case_the_vacancy()
    {
        await StartEngineAsync();
        var position = await PositionAsync();
        var hire = await HireAsync(orderNo: null, hireDate: Today, positionId: position);
        var vacancy = await VacancyAsync();
        var candidate = await CandidateAsync(vacancy);
        var request = await VacancyRequestAsync(title: "Lavozim " + Suffix());
        await RunAsync();

        var positionName = await DbAsync(db => db.Positions.AsNoTracking().Where(row => row.Id == position).Select(row => row.Name).SingleAsync(Ct));
        var vacancyTitle = await DbAsync(db => db.Vacancies.AsNoTracking().Where(row => row.Id == vacancy).Select(row => row.Title).SingleAsync(Ct));

        using var client = await ClientAsync(Permissions.ProcessesView);

        var employment = (await GetAsync(client, $"{Root}/cases/{(await FormaliseAsync(hire)).Id}")).GetProperty("subject");
        employment.GetProperty("position").GetString().ShouldBe(positionName);

        var candidateCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
        var candidateSubject = (await GetAsync(client, $"{Root}/cases/{candidateCase.Id}")).GetProperty("subject");
        candidateSubject.GetProperty("kind").GetString().ShouldBe("Candidate");
        candidateSubject.GetProperty("position").GetString().ShouldBe(vacancyTitle);
        candidateSubject.GetProperty("candidateId").GetGuid().ShouldBe(candidate);
        candidateSubject.GetProperty("name").GetString().ShouldStartWith("Motorov");

        var requestCase = await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id));
        var requestSubject = (await GetAsync(client, $"{Root}/cases/{requestCase.Id}")).GetProperty("subject");
        requestSubject.GetProperty("kind").GetString().ShouldBe("VacancyRequest");
        requestSubject.GetProperty("name").GetString().ShouldBe(request.Title);
        requestSubject.GetProperty("vacancyRequestId").GetGuid().ShouldBe(request.Id);
    }

    // ────────────────────────────────────────────────────────────
    //  #6 — /late
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Late_sorts_blocked_late_and_warnings_for_any_viewer_worst_first()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today.AddDays(-30), orderDate: Today.AddDays(-30));
        var worse = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today.AddDays(-60), orderDate: Today.AddDays(-60));
        var onTime = await VacancyRequestAsync();
        await RunAsync();
        var processCase = await RunningSinceAsync(await FormaliseAsync(hire), Today.AddDays(-40));
        var worseCase = await RunningSinceAsync(await FormaliseAsync(worse), Today.AddDays(-70));
        var requestCase = await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(onTime.Id));

        using var client = await ClientAsync(Permissions.ProcessesView);

        var late = await GetAsync(client, Root + "/late");

        StepsOf(late, "blocked", processCase.Id).ShouldBe([ProcessStepIds.PayrollHandover]);
        var blocked = Task(late, "blocked", processCase.Id, ProcessStepIds.PayrollHandover);
        blocked.GetProperty("gateErrorCode").GetString().ShouldBe("jarayon.jshshir_yoq");
        blocked.GetProperty("resolver").GetProperty("role").GetString().ShouldBe(ProcessRoles.Hr2);

        StepsOf(late, "late", processCase.Id).ShouldContain("r01-2");
        StepsOf(late, "late", processCase.Id).ShouldNotContain(ProcessStepIds.PayrollHandover, "Blocked has a list of its own");
        StepsOf(late, "late", requestCase.Id).ShouldBeEmpty("nothing of that request is due yet");
        var lateStep = Task(late, "late", processCase.Id, "r01-2");
        lateStep.GetProperty("daysLate").GetInt32().ShouldBeGreaterThanOrEqualTo(30);

        StepsOf(late, "warn", processCase.Id).ShouldContain("r01-6", "Q05: past its due");
        Task(late, "warn", processCase.Id, "r01-6").GetProperty("warnings").EnumerateArray().Select(item => item.GetString()).ShouldBe([ProcessGateCodes.Q05]);
        Task(late, "warn", processCase.Id, "r01-8a").GetProperty("warnings").EnumerateArray().Select(item => item.GetString()).ShouldBe([ProcessGateCodes.Q02], "not done by the hire date");
        StepsOf(late, "late", processCase.Id).ShouldContain("r01-6", "a step can stand in both lists");

        var lateDays = late.GetProperty("late").EnumerateArray().Select(task => task.GetProperty("daysLate").GetInt32()).ToList();
        lateDays.ShouldBe(lateDays.OrderDescending().ToList(), "most days late first");
        StepsOf(late, "late", worseCase.Id).ShouldContain("r01-2");
        Task(late, "late", worseCase.Id, "r01-2").GetProperty("daysLate").GetInt32()
            .ShouldBeGreaterThan(lateStep.GetProperty("daysLate").GetInt32());
    }

    /// <summary>
    /// §3.1 floor: a hire recorded 30 days after the fact is due the day it is recorded, not 31 days ago. The floor
    /// is the start date, so it stays put: the same step is one day late the next day, and the rule's own date wins
    /// once it is the later one.
    /// </summary>
    [Fact]
    public async Task Late_a_case_is_not_late_on_the_day_it_starts_and_is_late_from_the_next()
    {
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today.AddDays(-30), orderDate: Today.AddDays(-30));
        await RunAsync();
        var processCase = await FormaliseAsync(hire);

        using var client = await ClientAsync(Permissions.ProcessesView);

        processCase.StartedOn.ShouldBe(Today);
        processCase.FindStep("r01-2")!.DueDate.ShouldBe(Today);
        StepsOf(await GetAsync(client, Root + "/late"), "late", processCase.Id).ShouldNotContain("r01-2");
        (await IdsOfAsync(client, $"employee={hire.EmployeeId}&late=true")).ShouldBeEmpty();

        var nextDay = await RunningSinceAsync(processCase, Today.AddDays(-1));

        nextDay.FindStep("r01-2")!.DueDate.ShouldBe(Today.AddDays(-1));
        var late = await GetAsync(client, Root + "/late");
        Task(late, "late", processCase.Id, "r01-2").GetProperty("daysLate").GetInt32().ShouldBe(1);
        (await IdsOfAsync(client, $"employee={hire.EmployeeId}&late=true")).ShouldBe([processCase.Id]);

        var longAgo = await RunningSinceAsync(processCase, Today.AddDays(-60));

        var raw = longAgo.FindStep("r01-2")!.DueDate.ShouldNotBeNull();
        raw.ShouldBeLessThan(Today.AddDays(-30), "the working day before the hire");
        Task(await GetAsync(client, Root + "/late"), "late", processCase.Id, "r01-2").GetProperty("daysLate").GetInt32()
            .ShouldBe(Today.DayNumber - raw.DayNumber);
    }

    [Fact]
    public async Task Late_warns_about_an_empty_handover_act_before_the_step_is_late_and_not_about_a_filled_one()
    {
        await StartEngineAsync();
        await StandInsAsync();
        var empty = await DismissalAsync(leaveDate: Today.AddDays(30), act: F09Dismissal);
        var filled = await DismissalAsync(leaveDate: Today.AddDays(30), act: F09WithARow);

        using var client = await ClientAsync(Permissions.ProcessesView);

        var late = await GetAsync(client, Root + "/late");

        var warned = StepsOf(late, "warn", empty.Id);
        warned.ShouldContain("ex-4");
        Task(late, "warn", empty.Id, "ex-4").GetProperty("warnings").EnumerateArray().Select(item => item.GetString()).ShouldBe([ProcessGateCodes.Akt]);
        StepsOf(late, "late", empty.Id).ShouldNotContain("ex-4", "the warning fires on time");

        StepsOf(late, "warn", filled.Id).ShouldNotContain("ex-4", "the act has a row");
    }

    [Fact]
    public async Task Blocked_Q12_points_at_the_first_open_act_step()
    {
        await ClearRolesAsync();
        await StartEngineAsync();
        await StandInsAsync();
        var hire = await HireAsync(orderNo: null, hireDate: Today);
        await BlankAsync(ProcessBlanks.F09, hire, F09Dismissal);
        await RunAsync();
        await TerminateAsync(hire);
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));

        processCase.FindStep("ex-10")!.State.ShouldBe(ProcessStepState.Blocked);
        processCase.FindStep("ex-10")!.BlockedGateCode.ShouldBe(ProcessGateCodes.Q12);

        using var client = await ClientAsync(Permissions.ProcessesView);

        var blocked = Task(await GetAsync(client, Root + "/late"), "blocked", processCase.Id, "ex-10");
        blocked.GetProperty("gateErrorCode").GetString().ShouldBe("jarayon.akt_yopilmagan");

        var open = processCase.Steps
            .Where(step => step.StepCode is "ex-2" or "ex-4" or "ex-5" or "ex-6" && step.State is not (ProcessStepState.Done or ProcessStepState.Skipped))
            .OrderBy(step => step.OrderNo)
            .First();
        blocked.GetProperty("resolver").GetProperty("role").GetString().ShouldBe(open.HolderRole);
    }

    [Fact]
    public async Task Late_lists_overrides_of_the_last_thirty_days_with_names()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));

        var grantor = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        var at = DateTimeOffset.UtcNow;

        var recent = await GrantAsync(processCase.Id, candidate, grantor.UserId, "yaqinda " + Suffix(), at);
        var old = await GrantAsync(processCase.Id, candidate, grantor.UserId, "eski " + Suffix(), at.AddDays(-31));

        using var client = await ClientAsync(Permissions.ProcessesView);
        var overrides = (await GetAsync(client, Root + "/late")).GetProperty("overrides").EnumerateArray().ToList();

        overrides.ShouldNotContain(row => row.GetProperty("id").GetGuid() == old);

        var row = overrides.Single(item => item.GetProperty("id").GetGuid() == recent);
        row.GetProperty("gateCode").GetString().ShouldBe(ProcessGateCodes.Q03);
        row.GetProperty("caseId").GetGuid().ShouldBe(processCase.Id);
        row.GetProperty("candidateId").GetGuid().ShouldBe(candidate);
        row.GetProperty("grantedBy").GetGuid().ShouldBe(grantor.UserId);
        row.GetProperty("grantedByName").GetString().ShouldNotBeNullOrEmpty();
        row.GetProperty("reason").GetString().ShouldStartWith("yaqinda");
        row.GetProperty("consumedAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ────────────────────────────────────────────────────────────
    //  #7, #8 — /templates, /status
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Templates_list_the_published_definitions_without_their_rules()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        var templates = (await GetAsync(client, Root + "/templates")).EnumerateArray().ToList();

        templates.Select(template => template.GetProperty("code").GetString()).ShouldBe(
            [ProcessCodes.Dismissal, ProcessCodes.HireCandidate, ProcessCodes.HireFormalise, ProcessCodes.HireRequest, ProcessCodes.Probation]);

        var formalise = templates.Single(template => template.GetProperty("code").GetString() == ProcessCodes.HireFormalise);
        formalise.GetProperty("title").GetString().ShouldBe("Ishga qabulni rasmiylashtirish");
        formalise.GetProperty("version").GetString().ShouldBe("1.1");
        formalise.GetProperty("subjectKind").GetString().ShouldBe("Employment");
        formalise.GetProperty("board").EnumerateArray().Select(column => column.GetProperty("key").GetString()).ShouldBe(["docs", "order", "after", "firstDay"]);

        var payroll = formalise.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetString() == ProcessStepIds.PayrollHandover);
        payroll.GetProperty("role").GetString().ShouldBe(ProcessRoles.Hr2);
        payroll.GetProperty("blank").GetString().ShouldBe("HR-F18");
        payroll.GetProperty("kind").GetString().ShouldBe("Manual");
        var gate = payroll.GetProperty("gates").EnumerateArray().Single();
        gate.GetProperty("code").GetString().ShouldBe(ProcessGateCodes.Q03);
        gate.GetProperty("mode").GetString().ShouldBe("Block");
        var payment = payroll.GetProperty("manual").GetProperty("inputs").EnumerateArray().Single(input => input.GetProperty("id").GetString() == "payment");
        payment.GetProperty("required").GetBoolean().ShouldBeTrue();
        payment.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("value").GetString()).ShouldBe(["card", "cash"]);

        payroll.TryGetProperty("completion", out _).ShouldBeFalse("completion rules stay with the engine");
        payroll.TryGetProperty("when", out _).ShouldBeFalse();
        payroll.TryGetProperty("due", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Status_says_dormant_without_a_start_date_and_active_with_one_and_runs_no_pass()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        await SetStartedOnAsync(null);
        await SqlAsync($"DELETE FROM system_settings WHERE key = {SystemSettingKeys.ProcessesReconcileWatermark}");

        var dormant = await GetAsync(client, Root + "/status");
        dormant.GetProperty("engine").GetString().ShouldBe("dormant");
        dormant.GetProperty("startedOn").ValueKind.ShouldBe(JsonValueKind.Null);
        dormant.GetProperty("lastPassAt").ValueKind.ShouldBe(JsonValueKind.Null);
        dormant.GetProperty("gatesMode").ValueKind.ShouldBe(JsonValueKind.String);
        dormant.GetProperty("councilWeekday").GetInt32().ShouldBeInRange(1, 7);

        await SetStartedOnAsync(Today);
        var request = await VacancyRequestAsync();

        var active = await GetAsync(client, Root + "/status");
        active.GetProperty("engine").GetString().ShouldBe("active");
        active.GetProperty("startedOn").GetString().ShouldBe(Iso(Today));
        active.GetProperty("lastPassAt").ValueKind.ShouldBe(JsonValueKind.Null, "no pass ran yet");

        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).ShouldBeNull("asking about the engine must not run it");

        await RunAsync();

        var after = await GetAsync(client, Root + "/status");
        var lastPass = after.GetProperty("lastPassAt").GetDateTimeOffset();
        (DateTimeOffset.UtcNow - lastPass).ShouldBeLessThan(TimeSpan.FromMinutes(5));

        // A watermark nobody can read is «no pass known», not a 500.
        await SqlAsync($"UPDATE system_settings SET value = 'kept' WHERE key = {SystemSettingKeys.ProcessesReconcileWatermark}");
        (await GetAsync(client, Root + "/status")).GetProperty("lastPassAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Dormant_engine_leaves_every_read_answering_with_nothing_new()
    {
        await SetStartedOnAsync(null);
        var hire = await HireAsync(orderNo: "READ-" + Suffix(), hireDate: Today);
        using var client = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesManage);

        (await GetAsync(client, Root + "/my")).GetProperty("today").GetString().ShouldBe(Iso(Today));
        (await GetAsync(client, Root + "/late")).GetProperty("blocked").ValueKind.ShouldBe(JsonValueKind.Array);
        (await GetAsync(client, Root + "/board?process=hire-formalise")).GetProperty("columns").GetArrayLength().ShouldBe(4);
        (await GetAsync(client, $"{Root}/cases?employee={hire.EmployeeId}")).GetProperty("total").GetInt32().ShouldBe(0);

        (await FindCaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId))).ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  Scenarios
    // ────────────────────────────────────────────────────────────

    /// <summary>Candidate past the offer: the first day is Blocked on Q04 and the formalise child exists.</summary>
    // ────────────────────────────────────────────────────────────
    //  Chain, the three hiring cases as one person's page
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chain_is_request_candidate_formalise_from_the_candidate_and_from_the_formalise()
    {
        var (request, candidate, formalise) = await WholeChainAsync();
        using var client = await ClientAsync(Permissions.ProcessesView);

        (await ChainAsync(client, candidate)).ShouldBe([request, candidate, formalise]);
        (await ChainAsync(client, formalise)).ShouldBe([request, candidate, formalise]);
    }

    [Fact]
    public async Task Chain_of_a_request_with_several_candidates_is_the_request_and_a_candidate_stays_with_their_own()
    {
        var (request, candidate, formalise) = await WholeChainAsync();
        var other = await CandidateCaseAsync();
        await SqlAsync($"UPDATE process_cases SET parent_case_id = {request} WHERE id = {other}");
        using var client = await ClientAsync(Permissions.ProcessesView);

        (await ChainAsync(client, request)).ShouldBe([request]);
        (await ChainAsync(client, other)).ShouldBe([request, other]);
        (await ChainAsync(client, candidate)).ShouldBe([request, candidate, formalise]);
    }

    [Fact]
    public async Task Chain_of_a_request_with_one_candidate_is_still_the_request()
    {
        var (request, _, _) = await WholeChainAsync();
        using var client = await ClientAsync(Permissions.ProcessesView);

        (await ChainAsync(client, request)).ShouldBe([request]);
    }

    [Fact]
    public async Task Chain_holds_only_the_links_that_exist()
    {
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: null, hireDate: Today);
        var alone = await CandidateCaseAsync();
        await RunAsync();
        var directHire = await FormaliseAsync(hire);
        using var client = await ClientAsync(Permissions.ProcessesView);

        (await ChainAsync(client, alone)).ShouldBe([alone], "a candidate with no request and no hire yet");
        (await ChainAsync(client, directHire.Id)).ShouldBe([directHire.Id], "a direct hire has no candidate");
    }

    [Fact]
    public async Task Chain_of_another_process_is_just_itself_even_with_a_parent()
    {
        var (_, _, formalise) = await WholeChainAsync();
        var dismissal = await DismissalAsync(Today.AddDays(14), F09Dismissal);
        await SqlAsync($"UPDATE process_cases SET parent_case_id = {formalise} WHERE id = {dismissal.Id}");
        using var client = await ClientAsync(Permissions.ProcessesView);

        (await ChainAsync(client, dismissal.Id)).ShouldBe([dismissal.Id]);
        (await ChainAsync(client, formalise)).ShouldNotContain(dismissal.Id, "a child of another process is not this person's hiring");
    }

    [Fact]
    public async Task Chain_does_not_follow_a_parent_of_the_wrong_process()
    {
        var (_, candidate, _) = await WholeChainAsync();
        var dismissal = await DismissalAsync(Today.AddDays(14), F09Dismissal);
        await SqlAsync($"UPDATE process_cases SET parent_case_id = {dismissal.Id} WHERE id = {candidate}");
        using var client = await ClientAsync(Permissions.ProcessesView);

        (await ChainAsync(client, candidate)).ShouldNotContain(dismissal.Id);
    }

    [Fact]
    public async Task Chain_of_an_unknown_case_is_404_with_the_code()
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        await ProblemAsync(client, $"{Root}/cases/{Guid.CreateVersion7()}/chain", HttpStatusCode.NotFound, ProcessErrors.NotFound.Code);
    }

    [Fact]
    public async Task Case_on_the_earlier_formalise_keeps_its_order_while_a_new_case_takes_the_new_one()
    {
        string?[] earlierColumns = ["docs", "order", "firstDay", "after"];
        string[] earlierSteps = ["r01-2", "r01-3", "r01-4", "r01-5", "mm", "r01-8a", "r01-6", "r01-7", "r01-9"];

        await StartEngineAsync();
        var pinnedHire = await HireAsync(orderNo: null, hireDate: Today);
        await RunAsync();
        var pinned = await FormaliseAsync(pinnedHire);
        var earlier = await EarlierFormaliseVersionAsync(earlierColumns, earlierSteps);

        await SqlAsync($"UPDATE process_cases SET template_version_id = {earlier} WHERE id = {pinned.Id}");
        await SqlAsync($"UPDATE process_case_steps SET order_no = order_no + 100 WHERE case_id = {pinned.Id}");
        foreach (var (stepCode, index) in earlierSteps.Select((stepCode, index) => (stepCode, index)))
        {
            await SqlAsync($"UPDATE process_case_steps SET order_no = {index + 1} WHERE case_id = {pinned.Id} AND step_code = {stepCode}");
        }

        var newHire = await HireAsync(orderNo: null, hireDate: Today);
        await RunAsync();
        var fresh = await FormaliseAsync(newHire);
        using var client = await ClientAsync(Permissions.ProcessesView);

        // A pass ran over both: neither moved to the other's version.
        var pinnedAfter = await CaseByIdAsync(pinned.Id);
        pinnedAfter.TemplateVersionId.ShouldBe(earlier);
        var pinnedDetail = await GetAsync(client, $"{Root}/cases/{pinned.Id}");
        pinnedDetail.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("key").GetString()).ShouldBe(earlierColumns);
        pinnedDetail.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("stepCode").GetString()).ShouldBe(earlierSteps);

        fresh.TemplateVersionId.ShouldNotBe(earlier);
        var freshDetail = await GetAsync(client, $"{Root}/cases/{fresh.Id}");
        freshDetail.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("key").GetString()).ShouldBe(["docs", "order", "after", "firstDay"]);
        freshDetail.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("stepCode").GetString())
            .ShouldBe(["r01-2", "r01-3", "r01-4", "r01-5", "r01-6", "r01-7", "mm", "r01-8a", "r01-9"]);
    }

    /// <summary>
    /// What a database that ran the first release holds: the current definition in the earlier order, stored as a retired
    /// version. The published one stays, so the rest of the class is unaffected.
    /// </summary>
    private Task<Guid> EarlierFormaliseVersionAsync(string?[] columns, string[] steps) => DbAsync(async db =>
    {
        var template = await db.ProcessTemplates.AsNoTracking().SingleAsync(row => row.Code == ProcessCodes.HireFormalise, Ct);
        var published = await db.ProcessTemplateVersions.AsNoTracking()
            .SingleAsync(row => row.TemplateId == template.Id && row.Status == ProcessTemplateVersionStatus.Published, Ct);

        var definition = JsonNode.Parse(published.Definition)!.AsObject();
        definition["version"] = "1.0";
        var board = definition["board"]!.AsArray().Select(column => column!.DeepClone()).ToList();
        definition["board"] = new JsonArray([.. columns.Select(key => board.Single(column => column!["key"]!.GetValue<string>() == key))]);
        var ordered = definition["steps"]!.AsArray().Select(step => step!.DeepClone()).ToList();
        definition["steps"] = new JsonArray([.. steps.Select(id => ordered.Single(step => step!["id"]!.GetValue<string>() == id))]);

        var text = definition.ToJsonString();
        var version = ProcessTemplateVersion.Create(template.Id, "1.0", text, ProcessTemplateSeeder.Sha256Hex(text)).Value;
        version.Publish(DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        version.Retire().IsSuccess.ShouldBeTrue();
        db.ProcessTemplateVersions.Add(version);
        await db.SaveChangesAsync(Ct);
        return version.Id;
    });

    /// <summary>A request, its candidate past the accepted offer, and the formalise case that starts from that offer.</summary>
    private async Task<(Guid Request, Guid Candidate, Guid Formalise)> WholeChainAsync()
    {
        var candidate = await BlockedFirstDayAsync();
        var request = await VacancyRequestAsync(title: "Lavozim " + Suffix());
        await RunAsync();
        var requestCase = await CaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id));
        await SqlAsync($"UPDATE process_cases SET parent_case_id = {requestCase.Id} WHERE id = {candidate.Id}");

        var formalise = await DbAsync(db => db.ProcessCases.AsNoTracking()
            .Where(row => row.ParentCaseId == candidate.Id && row.ProcessCode == ProcessCodes.HireFormalise)
            .Select(row => row.Id)
            .SingleAsync(Ct));

        return (requestCase.Id, candidate.Id, formalise);
    }

    private async Task<Guid> CandidateCaseAsync()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        return (await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today))).Id;
    }

    private static async Task<List<Guid>> ChainAsync(HttpClient client, Guid caseId)
        => [.. (await GetAsync(client, $"{Root}/cases/{caseId}/chain")).GetProperty("caseIds").EnumerateArray().Select(id => id.GetGuid())];

    private async Task<ProcessCase> BlockedFirstDayAsync()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var parent = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
        var by = await UserIdAsync();

        await CloseManuallyAsync(parent.Id, "r02-10", by, """{"decision":"accept"}""");
        await CloseManuallyAsync(parent.Id, ProcessStepIds.Offer, by, $$"""{"{{ProcessStepPayload.StartDateInput}}":"{{Today.AddDays(10):yyyy-MM-dd}}"}""");
        await CloseManuallyAsync(parent.Id, "r02-12", by, payload: null);
        await RunAsync();

        var after = await CaseByIdAsync(parent.Id);
        after.FindStep(ProcessStepIds.FirstDay)!.State.ShouldBe(ProcessStepState.Blocked);
        return after;
    }

    /// <summary>A dismissal under way: the act and the leave application as drafts, the leave date is the anchor.</summary>
    private async Task<ProcessCase> DismissalAsync(DateOnly leaveDate, string act)
    {
        var hire = await HireAsync(orderNo: null, hireDate: Today);
        await BlankAsync(ProcessBlanks.F09, hire, act);
        await BlankAsync(ProcessBlanks.F08, hire, $$"""{"leaveDate":"{{leaveDate:yyyy-MM-dd}}"}""");
        await RunAsync();
        return await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));
    }

    private Task<ProcessCase> FormaliseAsync(Hire hire)
        => CaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId));

    /// <summary>
    /// The case has been open since <paramref name="since"/>, then a pass. A case never starts already late (§3.1
    /// floor), so a test of lateness needs one that started earlier: this class runs on the real clock.
    /// </summary>
    private async Task<ProcessCase> RunningSinceAsync(ProcessCase processCase, DateOnly since)
    {
        await SqlAsync($"UPDATE process_cases SET started_on = {since} WHERE id = {processCase.Id}");
        await RunAsync();
        return await CaseByIdAsync(processCase.Id);
    }

    // ────────────────────────────────────────────────────────────
    //  HTTP and JSON
    // ────────────────────────────────────────────────────────────

    private async Task<HttpClient> ClientAsync(params string[] permissions)
        => ClientWith(await TestAccounts.TokenAsync(_factory, permissions));

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{url}: {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task ProblemAsync(HttpClient client, string url, HttpStatusCode status, string code)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.ShouldBe(status, body);

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("title").GetString().ShouldBe(code);
    }

    private static async Task<int> TotalAsync(HttpClient client, string query)
        => (await GetAsync(client, $"{Root}/cases?{query}")).GetProperty("total").GetInt32();

    private static async Task<List<Guid>> IdsOfAsync(HttpClient client, string query)
        => [.. (await GetAsync(client, $"{Root}/cases?pageSize=10000&{query}")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static List<string> StepsOf(JsonElement dto, string section, Guid caseId)
        =>
        [
            .. dto.GetProperty(section).EnumerateArray()
                .Where(task => task.GetProperty("caseId").GetGuid() == caseId)
                .Select(task => task.GetProperty("stepCode").GetString()!),
        ];

    private static JsonElement Task(JsonElement dto, string section, Guid caseId, string stepCode, string? orSection = null)
    {
        var sections = orSection is null ? [section] : new[] { section, orSection };

        return sections
            .SelectMany(name => dto.GetProperty(name).EnumerateArray())
            .Single(task => task.GetProperty("caseId").GetGuid() == caseId && task.GetProperty("stepCode").GetString() == stepCode);
    }

    private static JsonElement StepOfDetail(JsonElement detail, string stepCode)
        => detail.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("stepCode").GetString() == stepCode);

    private static List<string?> TartibKeys(JsonElement my)
        => [.. my.GetProperty("tartib").GetProperty("counts").EnumerateArray().Select(row => row.GetProperty("key").GetString())];

    /// <summary>0 when the card is absent: zero is omitted (§11.8).</summary>
    private static int TartibCount(JsonElement my, string key)
        => my.GetProperty("tartib").GetProperty("counts").EnumerateArray()
            .Where(row => row.GetProperty("key").GetString() == key)
            .Select(row => row.GetProperty("count").GetInt32())
            .SingleOrDefault();

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The same claims under another expiry, signed with the key of the test host.</summary>
    private static string Resigned(string token, DateTime expires)
    {
        var read = new JsonWebToken(token);
        var claims = read.Claims.Where(claim => claim.Type is not ("exp" or "iat" or "nbf" or "iss" or "aud"));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = read.Issuer,
            Audience = read.Audiences.Single(),
            Subject = new ClaimsIdentity(claims),
            IssuedAt = expires.AddHours(-2),
            NotBefore = expires.AddHours(-2),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test_uchun_kamida_32_belgili_maxfiy_kalit_1234")),
                read.Alg),
        });
    }

    // ────────────────────────────────────────────────────────────
    //  Data (the shape of ProcessReconcilerTests' helpers)
    // ────────────────────────────────────────────────────────────

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    private async Task RunAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ProcessReconciler>().RunAsync(Ct);
        result.Status.ShouldNotBe(ProcessReconcileStatus.Dormant);
    }

    private Task<int> StartEngineAsync() => SetStartedOnAsync(Today);

    private Task<int> SetStartedOnAsync(DateOnly? startedOn) => DbAsync(async db =>
    {
        var value = startedOn?.ToString(SystemSettingKeys.ProcessesStartedOnFormat, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        var row = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == SystemSettingKeys.ProcessesStartedOn, Ct);

        if (row is null)
        {
            db.SystemSettings.Add(SystemSetting.CreateServerValue(SystemSettingKeys.ProcessesStartedOn, value).Value);
        }
        else
        {
            row.SetServerValue(value).IsSuccess.ShouldBeTrue();
        }

        return await db.SaveChangesAsync(Ct);
    });

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> SqlAsync(FormattableString sql) => DbAsync(db => db.Database.ExecuteSqlAsync(sql, Ct));

    private Task<int> ClearRolesAsync() => SqlAsync($"DELETE FROM blank_role_assignments");

    private Task<int> AssignRoleAsync(Guid userId, string role) => DbAsync(db =>
    {
        db.BlankRoleAssignments.Add(BlankRoleAssignment.Create(role, userId).Value);
        return db.SaveChangesAsync(Ct);
    });

    private Task<int> LinkUserAsync(Guid userId, Guid employeeId) => DbAsync(async db =>
    {
        var user = await db.Users.SingleAsync(row => row.Id == userId, Ct);
        user.LinkToEmployee(employeeId);
        return await db.SaveChangesAsync(Ct);
    });

    private Task<int> StandInsAsync() => DbAsync(db => ProcessTestBlanks.EnsureStandInsAsync(db, Ct));

    private Task<ProcessCase?> FindCaseAsync(string processCode, string subjectRef)
        => DbAsync(db => db.ProcessCases.AsNoTracking()
            .Include(processCase => processCase.Steps)
            .SingleOrDefaultAsync(processCase => processCase.ProcessCode == processCode && processCase.SubjectRef == subjectRef, Ct));

    private async Task<ProcessCase> CaseAsync(string processCode, string subjectRef)
        => (await FindCaseAsync(processCode, subjectRef)).ShouldNotBeNull();

    private Task<ProcessCase> CaseByIdAsync(Guid id)
        => DbAsync(db => db.ProcessCases.AsNoTracking()
            .Include(processCase => processCase.Steps)
            .SingleAsync(processCase => processCase.Id == id, Ct));

    /// <summary>Opens the step past its order and closes it: who may act is not the aggregate's business.</summary>
    private Task<int> CloseManuallyAsync(Guid caseId, string stepCode, Guid by, string? payload) => DbAsync(async db =>
    {
        var row = await db.ProcessCases.Include(processCase => processCase.Steps).SingleAsync(processCase => processCase.Id == caseId, Ct);
        var now = DateTimeOffset.UtcNow;

        row.ActivateStep(stepCode, now).IsSuccess.ShouldBeTrue();
        row.CompleteStepManually(stepCode, by, payload, evidenceSha256: null, now).IsSuccess.ShouldBeTrue();

        return await db.SaveChangesAsync(Ct);
    });

    private Task<Guid> UserIdAsync() => DbAsync(async db =>
    {
        var phone = PhoneNumber.Create("90" + Random.Shared.Next(1_000_000, 9_999_999)).Value;
        var user = User.Create(phone, "test-hash", FullName.Create("Foydalanuvchi" + Suffix(), "Test", null).Value).Value;
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    });

    private Task<Guid> DepartmentAsync(string? name = null, Guid? parentId = null) => DbAsync(async db =>
    {
        var department = Department.Create(name ?? "Oʻqish boʻlimi " + Suffix(), parentId).Value;
        db.Departments.Add(department);
        await db.SaveChangesAsync(Ct);
        return department.Id;
    });

    private Task<Guid> PositionAsync() => DbAsync(async db =>
    {
        var position = Position.Create("Oʻqish lavozimi " + Suffix()).Value;
        db.Positions.Add(position);
        await db.SaveChangesAsync(Ct);
        return position.Id;
    });

    private Task<Guid> VacancyAsync() => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Engine vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(Ct);
        return vacancy.Id;
    });

    private Task<Guid> CandidateAsync(Guid vacancyId) => CandidateNamedAsync("Motorov" + Suffix(), vacancyId);

    private async Task<Guid> CandidateNamedAsync(string lastName, Guid? vacancyId = null)
    {
        var vacancy = vacancyId ?? await VacancyAsync();

        return await DbAsync(async db =>
        {
            var candidate = Candidate.Register(FullName.Create(lastName, "Test", null).Value, vacancy).Value;
            db.Candidates.Add(candidate);
            await db.SaveChangesAsync(Ct);
            return candidate.Id;
        });
    }

    private Task<VacancyRequest> VacancyRequestAsync(string? title = null) => DbAsync(async db =>
    {
        var department = Department.Create("Engine department " + Suffix()).Value;
        db.Departments.Add(department);
        var requester = Employee.Create(FullName.Create("Sorovchi" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(requester);

        var row = VacancyRequest.Create(department.Id, requester.Id, title ?? "Engine position", 1, neededBy: null, note: null).Value;
        db.VacancyRequests.Add(row);

        await db.SaveChangesAsync(Ct);
        return row;
    });

    private Task<Hire> HireAsync(
        string? orderNo,
        DateOnly hireDate,
        Guid? departmentId = null,
        Guid? positionId = null,
        DateOnly? orderDate = null)
        => HireCoreAsync("Xodimov" + Suffix(), "Test", orderNo, hireDate, departmentId, positionId, orderDate);

    private Task<Hire> HireNamedAsync(string lastName, string firstName)
        => HireCoreAsync(lastName, firstName, orderNo: null, Today, departmentId: null, positionId: null, orderDate: null);

    private Task<Hire> HireCoreAsync(
        string lastName,
        string firstName,
        string? orderNo,
        DateOnly hireDate,
        Guid? departmentId,
        Guid? positionId,
        DateOnly? orderDate) => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Engine MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var name = FullName.Create(lastName, firstName, null).Value;
        var employee = Employee.Create(name).Value;
        db.Employees.Add(employee);

        var employment = Employment.Hire(
            employee.Id,
            legalEntity.Id,
            hireDate,
            departmentId: departmentId,
            positionId: positionId,
            orderNo: orderNo,
            orderDate: orderDate).Value;
        db.Employments.Add(employment);

        await db.SaveChangesAsync(Ct);
        return new Hire(employee.Id, employment.Id, name.Full);
    });

    private Task<int> TerminateAsync(Hire hire) => DbAsync(async db =>
    {
        var employment = await db.Employments.SingleAsync(row => row.Id == hire.EmploymentId, Ct);
        employment.Terminate(Today, reasonId: null).IsSuccess.ShouldBeTrue();
        return await db.SaveChangesAsync(Ct);
    });

    private Task<Guid> BlankAsync(string code, Hire hire, string answers) => DbAsync(async db =>
    {
        var template = await db.BlankTemplates.SingleAsync(row => row.Code == code, Ct);
        var version = await db.BlankTemplateVersions
            .SingleAsync(row => row.TemplateId == template.Id && row.Status == BlankTemplateVersionStatus.Published, Ct);

        var blank = BlankSubmission.Create(
            template,
            version,
            new BlankSubject(hire.EmployeeId, hire.EmploymentId),
            new BlankAnswersState(answers, "{}", "[]", 0, null)).Value;

        db.BlankSubmissions.Add(blank);
        await db.SaveChangesAsync(Ct);
        return blank.Id;
    });

    private Task<Guid> ScheduleAsync() => DbAsync(async db =>
    {
        var days = Enumerable.Range(1, 7)
            .Select(index => index <= 5
                ? new WorkScheduleDayInput(index, IsWorking: true, new TimeOnly(9, 0), new TimeOnly(18, 0), BreakMinutes: 60, new TimeOnly(13, 0), new TimeOnly(14, 0))
                : new WorkScheduleDayInput(index, IsWorking: false))
            .ToArray();

        var schedule = WorkSchedule.Create("Oʻqish jadvali " + Suffix(), days).Value;
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync(Ct);
        return schedule.Id;
    });

    private Task<Guid> AssignScheduleAsync(Guid schedule, ScheduleScope scope, Guid? target, DateOnly from, DateOnly? to) => DbAsync(async db =>
    {
        var assignment = ScheduleAssignment.Assign(schedule, scope, target, from, to).Value;
        db.ScheduleAssignments.Add(assignment);
        await db.SaveChangesAsync(Ct);
        return assignment.Id;
    });

    private Task<int> DeleteAssignmentAsync(Guid id) => SqlAsync($"DELETE FROM schedule_assignments WHERE id = {id}");

    /// <summary>Granted at <paramref name="at"/>: the log's window is what is under test, so the date goes in by hand.</summary>
    private Task<Guid> GrantAsync(Guid caseId, Guid candidateId, Guid by, string reason, DateTimeOffset at) => DbAsync(async db =>
    {
        var row = ProcessGateOverride.GrantForCase(ProcessGateCodes.Q03, caseId, "r02-13", employmentId: null, candidateId, reason, by, at).Value;
        db.ProcessGateOverrides.Add(row);
        await db.SaveChangesAsync(Ct);
        return row.Id;
    });

    private sealed record Hire(Guid EmployeeId, Guid EmploymentId, string FullName);
}
