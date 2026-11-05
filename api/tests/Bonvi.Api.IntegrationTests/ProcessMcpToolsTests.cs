using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// The MCP tools of «Mening ishlarim» (docs/SPEC-mening-ishlarim.md §12), through the read role: anything they touch
/// that the role has no grant on fails here with 42501 (H19). Cases are seeded directly and each test filters to its
/// own departments, so the rows of other tests stay out.
/// </summary>
public sealed class ProcessMcpToolsTests : IClassFixture<McpHost>
{
    private const string Hidden = "[yashirilgan]";
    private const string Pinfl = "12345678901234";
    private const string RequestNote = "maxfiy-sorov-izohi";

    private readonly McpHost _host;

    public ProcessMcpToolsTests(McpHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => Bonvi.Api.Mcp.McpToolContext.Today;

    private sealed record World(
        Guid Parent,
        Guid Child,
        Guid Sibling,
        Guid EmployeeId,
        string EmployeeName,
        string PositionName,
        Guid Dismissal,
        string LateStep,
        string LateInputLabel,
        Guid Request,
        string RequestTitle,
        Guid Candidate,
        string CandidateName,
        string VacancyTitle,
        string HolderUserName);

    // ─────────────────────────── process_cases ───────────────────────────

    [Fact]
    public async Task Cases_name_the_subject_of_every_kind_with_its_current_step_and_holder()
    {
        var world = await SeedAsync();
        var token = await TokenAsync();

        var items = await CasesAsync(token, new { department = world.Parent.ToString() });

        items.Select(IdOf).ShouldBe([world.Dismissal, world.Request, world.Candidate], ignoreOrder: true);

        var dismissal = Item(items, world.Dismissal);
        dismissal["process"]!.GetValue<string>().ShouldBe(ProcessCodes.Dismissal);
        dismissal["subject"]!["name"]!.GetValue<string>().ShouldBe(world.EmployeeName);
        dismissal["subject"]!["position"]!.GetValue<string>().ShouldBe(world.PositionName);
        dismissal["daysLate"]!.GetValue<int>().ShouldBe(3);

        var late = dismissal["currentSteps"]!.AsArray().Single(step => step!["code"]!.GetValue<string>() == world.LateStep)!;
        late["state"]!.GetValue<string>().ShouldBe(nameof(ProcessStepState.Active));
        late["due"]!.GetValue<string>().ShouldBe(Iso(Today.AddDays(-3)));
        late["daysLate"]!.GetValue<int>().ShouldBe(3);
        late["holder"]!["role"]!.GetValue<string>().ShouldBe("hr2");
        late["holder"]!["users"]!.AsArray().Select(user => user!.GetValue<string>()).ShouldContain(world.HolderUserName);

        // A blocked step counts as current too.
        dismissal["currentSteps"]!.AsArray().Select(step => step!["gate"]?.GetValue<string>()).ShouldContain(ProcessGateCodes.Q12);

        Item(items, world.Request)["subject"]!["name"]!.GetValue<string>().ShouldBe(world.RequestTitle);

        var candidate = Item(items, world.Candidate)["subject"]!;
        candidate["name"]!.GetValue<string>().ShouldBe(world.CandidateName);
        candidate["position"]!.GetValue<string>().ShouldBe(world.VacancyTitle);
    }

    [Fact]
    public async Task Cases_filter_by_each_argument_and_page()
    {
        var world = await SeedAsync();
        var token = await TokenAsync();
        var department = world.Parent.ToString();

        // A department takes its branch with it, as in every MCP tool.
        (await CasesAsync(token, new { department = world.Child.ToString() })).Select(IdOf).ShouldBe([world.Dismissal]);
        (await CasesAsync(token, new { department = world.Sibling.ToString() })).Select(IdOf)
            .ShouldBe([world.Request, world.Candidate], ignoreOrder: true);

        (await CasesAsync(token, new { department, process = ProcessCodes.HireRequest })).Select(IdOf).ShouldBe([world.Request]);
        (await CasesAsync(token, new { department, status = "cancelled" })).Select(IdOf).ShouldBe([world.Candidate]);
        (await CasesAsync(token, new { department, employee = world.EmployeeId.ToString() })).Select(IdOf).ShouldBe([world.Dismissal]);
        (await CasesAsync(token, new { department, late = true })).Select(IdOf).ShouldBe([world.Dismissal], "a cancelled case is nobody's lateness");
        (await CasesAsync(token, new { department, from = Iso(Today.AddDays(1)), to = Iso(Today.AddDays(2)) })).ShouldBeEmpty();

        var flow = new McpFlow(_host);
        var paged = await flow.CallAsync(token, "process_cases", new { department, page = 2, pageSize = 2 });
        paged.IsError.ShouldBeFalse(paged.Text);
        var page = Parse(paged.Text);
        page["total"]!.GetValue<int>().ShouldBe(3);
        page["items"]!.AsArray().Count.ShouldBe(1);

        var capped = await flow.CallAsync(token, "process_cases", new { department, pageSize = 100_000 });
        Parse(capped.Text)["pageSize"]!.GetValue<int>().ShouldBe(Bonvi.Api.Mcp.McpToolContext.MaxPageSize);

        var wrong = await flow.CallAsync(token, "process_cases", new { status = "1" });
        wrong.IsError.ShouldBeTrue(wrong.Text);
    }

    // ─────────────────────────── process_case ───────────────────────────

    [Fact]
    public async Task Case_shows_each_step_with_its_labelled_inputs_and_cuts_free_text()
    {
        var world = await SeedAsync();
        var token = await TokenAsync();

        var result = await new McpFlow(_host).CallAsync(token, "process_case", new { id = world.Dismissal.ToString() });
        result.IsError.ShouldBeFalse(result.Text);
        var detail = Parse(result.Text);

        detail["subject"]!["name"]!.GetValue<string>().ShouldBe(world.EmployeeName);
        var steps = detail["steps"]!.AsArray();
        steps.Count.ShouldBeGreaterThan(5);

        var done = steps.Single(step => step!["code"]!.GetValue<string>() == "ex-1")!;
        done["state"]!.GetValue<string>().ShouldBe(nameof(ProcessStepState.Done));
        done["resolution"]!.GetValue<string>().ShouldBe(nameof(ProcessStepResolution.Manual));
        done["doneAt"].ShouldNotBeNull();

        var blocked = steps.Single(step => step!["gate"]?.GetValue<string>() == ProcessGateCodes.Q12)!;
        blocked["state"]!.GetValue<string>().ShouldBe(nameof(ProcessStepState.Blocked));
        blocked["resolver"].ShouldNotBeNull();

        var late = steps.Single(step => step!["code"]!.GetValue<string>() == world.LateStep)!;
        late["warnings"]!.AsArray().Select(code => code!.GetValue<string>()).ShouldContain(ProcessGateCodes.Q05);

        var inputs = late["inputs"]!.AsObject();
        inputs["removedOn"]!["label"]!.GetValue<string>().ShouldBe(world.LateInputLabel);
        inputs["removedOn"]!["value"]!.GetValue<string>().ShouldBe("2026-11-01");

        // A forbidden name goes whole; a look-alike value is masked wherever it stands.
        inputs.ContainsKey("jshshir").ShouldBeFalse();
        result.Text.ShouldNotContain(Pinfl);

        var basis = inputs["basis"]!["value"]!.GetValue<string>();
        basis.ShouldContain(Hidden);
        basis.Length.ShouldBeLessThanOrEqualTo(500);
        basis.ShouldEndWith("…");
        basis.Any(char.IsSurrogate).ShouldBeFalse("a cut must not leave half a pair");

        inputs["kept"]!["value"]!["nested"]!.GetValue<string>().Length.ShouldBeLessThanOrEqualTo(500);
        inputs["count"]!["value"]!.GetValue<int>().ShouldBe(7);

        detail["cancelReason"].ShouldBeNull();

        var cancelled = await new McpFlow(_host).CallAsync(token, "process_case", new { id = world.Candidate.ToString() });
        cancelled.IsError.ShouldBeFalse(cancelled.Text);
        Parse(cancelled.Text)["cancelReason"]!.GetValue<string>().Length.ShouldBeLessThanOrEqualTo(500);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("019a0000-0000-7000-8000-00000000abcd")]
    public async Task Case_refuses_what_it_cannot_find(string id)
    {
        var token = await TokenAsync();

        var result = await new McpFlow(_host).CallAsync(token, "process_case", new { id });

        result.IsError.ShouldBeTrue(result.Text);
    }

    // ─────────────────────────── process_stuck ───────────────────────────

    [Fact]
    public async Task Stuck_lists_blocked_late_and_warned_steps_of_running_cases_only()
    {
        var world = await SeedAsync();
        var token = await TokenAsync();

        var result = await new McpFlow(_host).CallAsync(token, "process_stuck", new { department = world.Parent.ToString() });
        result.IsError.ShouldBeFalse(result.Text);
        var stuck = Parse(result.Text);

        var blocked = stuck["blocked"]!["items"]!.AsArray().Single()!;
        blocked["caseId"]!.GetValue<string>().ShouldBe(world.Dismissal.ToString());
        blocked["gate"]!.GetValue<string>().ShouldBe(ProcessGateCodes.Q12);
        blocked["resolver"].ShouldNotBeNull();
        blocked["subject"]!["name"]!.GetValue<string>().ShouldBe(world.EmployeeName);

        var late = stuck["late"]!["items"]!.AsArray();
        late.Select(row => row!["step"]!.GetValue<string>()).ShouldBe([world.LateStep], "the cancelled case's late step is nobody's work");
        late[0]!["daysLate"]!.GetValue<int>().ShouldBe(3);
        stuck["late"]!["total"]!.GetValue<int>().ShouldBe(1);

        stuck["warn"]!["items"]!.AsArray().Select(row => row!["step"]!.GetValue<string>()).ShouldContain(world.LateStep);

        result.Text.ShouldNotContain(RequestNote);
    }

    [Fact]
    public async Task Stuck_filters_by_department_branch_and_least_days_late()
    {
        var world = await SeedAsync();
        var token = await TokenAsync();
        var flow = new McpFlow(_host);

        var other = await flow.CallAsync(token, "process_stuck", new { department = world.Sibling.ToString() });
        other.IsError.ShouldBeFalse(other.Text);
        Parse(other.Text)["late"]!["total"]!.GetValue<int>().ShouldBe(0);
        Parse(other.Text)["blocked"]!["total"]!.GetValue<int>().ShouldBe(0);

        var child = Parse((await flow.CallAsync(token, "process_stuck", new { department = world.Child.ToString() })).Text);
        child["late"]!["total"]!.GetValue<int>().ShouldBe(1);

        var floor = Parse((await flow.CallAsync(token, "process_stuck", new { department = world.Child.ToString(), minDaysLate = 2 })).Text);
        floor["late"]!["total"]!.GetValue<int>().ShouldBe(1);
        floor["blocked"]!["total"]!.GetValue<int>().ShouldBe(0, "blocked a day ago is under the floor");

        var high = Parse((await flow.CallAsync(token, "process_stuck", new { department = world.Child.ToString(), minDaysLate = 4 })).Text);
        high["late"]!["total"]!.GetValue<int>().ShouldBe(0);
    }

    // ─────────────────────────── Access ───────────────────────────

    [Fact]
    public async Task Without_processes_view_every_tool_refuses()
    {
        var world = await SeedAsync();
        var flow = new McpFlow(_host);

        // Sees employees, but not the «Jarayonlar» section.
        var (_, tokens) = await flow.ConnectNewAsync(Permissions.McpConnect, Permissions.EmployeesView);

        var calls = new (string Tool, object Args)[]
        {
            ("process_cases", new { }),
            ("process_case", new { id = world.Dismissal.ToString() }),
            ("process_stuck", new { }),
        };

        foreach (var (tool, args) in calls)
        {
            var result = await flow.CallAsync(tokens.Access, tool, args);

            result.IsError.ShouldBeTrue($"{tool}: {result.Text}");
            result.Text.ShouldContain("Ruxsat yo'q", customMessage: tool);
            result.Text.ShouldNotContain(world.EmployeeName, customMessage: tool);
        }
    }

    [Fact]
    public async Task A_full_read_key_reads_the_tools()
    {
        var world = await SeedAsync();

        using var admin = await _host.AdminClientAsync();
        using var created = await admin.PostAsJsonAsync(
            new Uri("/api/api-keys", UriKind.Relative),
            new { name = PublicApiHarness.NoyobNom("Jarayon"), scope = "FullRead" },
            Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync(Ct));
        var key = await created.Content.ReadFromJsonAsync<ScopedKeyResponse>(Ct);

        using var client = _host.Client(McpFlow.RandomIp());
        client.DefaultRequestHeaders.Add("X-API-Key", key!.Key);

        foreach (var path in new[]
                 {
                     $"/api/public/v1/full/process_cases?department={world.Parent}",
                     $"/api/public/v1/full/process_case?id={world.Dismissal}",
                     $"/api/public/v1/full/process_stuck?department={world.Parent}&minDaysLate=1",
                 })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
            var text = await response.Content.ReadAsStringAsync(Ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{path}: {text}");
            text.ShouldContain(world.EmployeeName);
            text.ShouldNotContain(Pinfl);
        }
    }

    // ─────────────────────────── Seed ───────────────────────────

    /// <summary>
    /// A parent department with two children: a dismissal in one (a step 3 days late, one blocked a day ago, one done
    /// with a payload), in the other a running hire-request and a cancelled hire-candidate with a late step.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        var mark = McpHost.Belgi();

        await using var scope = _host.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parent = Department.Create("Jarayon " + mark).Value;
        db.Departments.Add(parent);
        var child = Department.Create("Jarayon ichki " + mark, parent.Id).Value;
        db.Departments.Add(child);
        var sibling = Department.Create("Jarayon qo'shni " + mark, parent.Id).Value;
        db.Departments.Add(sibling);

        var position = Position.Create("Jarayon lavozimi " + mark).Value;
        db.Positions.Add(position);
        var legalEntity = LegalEntity.Create("Jarayon MChJ " + mark).Value;
        db.LegalEntities.Add(legalEntity);

        var name = FullName.Create("Ketuvchi" + mark, "Test", null).Value;
        var employee = Employee.Create(name).Value;
        db.Employees.Add(employee);
        var employment = Employment.Hire(employee.Id, legalEntity.Id, Today.AddDays(-400), departmentId: child.Id, positionId: position.Id).Value;
        db.Employments.Add(employment);

        var requestTitle = "So'ralgan o'rin " + mark;
        var request = VacancyRequest.Create(sibling.Id, employee.Id, requestTitle, 1, neededBy: null, note: RequestNote).Value;
        db.VacancyRequests.Add(request);

        var vacancyTitle = "Jarayon vakansiyasi " + mark;
        var vacancy = Vacancy.Open(vacancyTitle, Today).Value;
        db.Vacancies.Add(vacancy);
        var candidateName = FullName.Create("Nomzodov" + mark, "Test", null).Value;
        var candidate = Candidate.Register(candidateName, vacancy.Id).Value;
        db.Candidates.Add(candidate);

        await db.SaveChangesAsync(Ct);

        var dismissal = await StartAsync(
            db,
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: employment.Id, EmployeeId: employee.Id),
            child.Id);
        var requestCase = await StartAsync(
            db,
            ProcessCodes.HireRequest,
            ProcessSubjectKind.VacancyRequest,
            new ProcessCaseLinks(VacancyRequestId: request.Id),
            sibling.Id);
        var candidateCase = await StartAsync(
            db,
            ProcessCodes.HireCandidate,
            ProcessSubjectKind.Candidate,
            new ProcessCaseLinks(CandidateId: candidate.Id),
            sibling.Id);

        await db.SaveChangesAsync(Ct);

        var lateStep = dismissal.Definition.Steps.Single(step => step.Gates.Any(gate => gate.Code == ProcessGateCodes.Q05));
        var blockedStep = dismissal.Definition.Steps.First(step => step.Gates.Any(gate => gate.Code == ProcessGateCodes.Q12));
        var requestStep = requestCase.Definition.Steps[0].Id;
        var candidateStep = candidateCase.Definition.Steps[0].Id;

        var active = (short)ProcessStepState.Active;
        var blocked = (short)ProcessStepState.Blocked;
        var done = (short)ProcessStepState.Done;
        var manual = (short)ProcessStepResolution.Manual;
        var longText = new string('x', 600);

        // Starts over with half a pair at the cut: 498 chars, then a pair across 499.
        var basis = ("JSHSHIR " + Pinfl + " ").PadRight(498, 'a') + "😀" + longText;
        var payload = new JsonObject
        {
            ["removedOn"] = "2026-11-01",
            ["jshshir"] = Pinfl,
            ["basis"] = basis,
            ["kept"] = new JsonObject { ["nested"] = longText },
            ["count"] = 7,
        }.ToJsonString();

        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE process_case_steps
                SET state = {active}, due_date = {Today.AddDays(-3)}, holder_role = 'hr2', payload = {payload}::jsonb
              WHERE case_id = {dismissal.Id} AND step_code = {lateStep.Id}
             """,
            Ct);
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE process_case_steps
                SET state = {blocked}, due_date = {Today.AddDays(-1)}, blocked_gate_code = {ProcessGateCodes.Q12}
              WHERE case_id = {dismissal.Id} AND step_code = {blockedStep.Id}
             """,
            Ct);
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE process_case_steps
                SET state = {done}, resolution = {manual}, done_at = now()
              WHERE case_id = {dismissal.Id} AND step_code = 'ex-1'
             """,
            Ct);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE process_case_steps SET state = {active} WHERE case_id = {requestCase.Id} AND step_code = {requestStep}",
            Ct);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE process_case_steps SET state = {active}, due_date = {Today.AddDays(-9)} WHERE case_id = {candidateCase.Id} AND step_code = {candidateStep}",
            Ct);
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE process_cases
                SET status = {(short)ProcessCaseStatus.Cancelled}, cancelled_at = now(), cancel_reason = {longText}
              WHERE id = {candidateCase.Id}
             """,
            Ct);

        var holder = await TestAccounts.CreateAsync(_host.Factory, Permissions.ProcessesView);
        db.BlankRoleAssignments.Add(BlankRoleAssignment.Create("hr2", holder.UserId).Value);
        await db.SaveChangesAsync(Ct);

        var holderName = await db.Users.IgnoreQueryFilters()
            .Where(user => user.Id == holder.UserId)
            .Select(user => user.FullName.Full)
            .SingleAsync(Ct);

        return new World(
            parent.Id,
            child.Id,
            sibling.Id,
            employee.Id,
            name.Full,
            position.Name,
            dismissal.Id,
            lateStep.Id,
            lateStep.Manual!.Inputs[0].Label,
            requestCase.Id,
            requestTitle,
            candidateCase.Id,
            candidateName.Full,
            vacancyTitle,
            holderName);
    }

    private static async Task<(Guid Id, ProcessDefinition Definition)> StartAsync(
        AppDbContext db,
        string code,
        ProcessSubjectKind kind,
        ProcessCaseLinks links,
        Guid department)
    {
        var version = await db.ProcessTemplateVersions
            .Where(row => row.Status == ProcessTemplateVersionStatus.Published
                && db.ProcessTemplates.Any(template => template.Id == row.TemplateId && template.Code == code))
            .SingleAsync(Ct);

        var definition = ProcessDefinitionValidator.Load(version.Definition);

        var processCase = ProcessCase.Start(
            new ProcessCaseStart(code, version.Id, kind, "t:" + Guid.NewGuid().ToString("N"), Today, Legacy: false, links, DepartmentId: department),
            ProcessStarters.PlanOf(definition),
            DateTimeOffset.UtcNow).Value;

        db.ProcessCases.Add(processCase);

        return (processCase.Id, definition);
    }

    // ─────────────────────────── Calls ───────────────────────────

    private async Task<string> TokenAsync()
    {
        var (_, tokens) = await new McpFlow(_host).ConnectNewAsync(Permissions.McpConnect, Permissions.ProcessesView);
        return tokens.Access;
    }

    private async Task<List<JsonNode>> CasesAsync(string token, object args)
    {
        var result = await new McpFlow(_host).CallAsync(token, "process_cases", args);
        result.IsError.ShouldBeFalse(result.Text);

        return [.. Parse(result.Text)["items"]!.AsArray().Select(item => item!)];
    }

    private static Guid IdOf(JsonNode item) => Guid.Parse(item["id"]!.GetValue<string>());

    private static JsonNode Item(List<JsonNode> items, Guid id) => items.Single(item => IdOf(item) == id);

    private static JsonNode Parse(string text) => JsonNode.Parse(text)!;

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
