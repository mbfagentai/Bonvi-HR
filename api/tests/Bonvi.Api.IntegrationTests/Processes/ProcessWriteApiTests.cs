using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Processes.Commands;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The write API of «Mening ishlarim» end to end (docs/SPEC-mening-ishlarim.md §10 #9–#13, #16–#18, §14): real HTTP,
/// real JSON, real PostgreSQL, so optimistic locking, the engine lock and the audit interceptor are the real ones.
/// </summary>
/// <remarks>
/// The database is shared by the tests of the class and a pass sweeps all of it, so each test asserts on its own case
/// ids. Role assignments are global: a test that depends on them starts from <see cref="ClearRolesAsync"/>. A test that
/// makes the engine dormant is followed by one that calls <see cref="StartEngineAsync"/>: every data helper does.
/// </remarks>
public sealed partial class ProcessWriteApiTests : IClassFixture<BonviApiFactory>
{
    private const string Root = "/api/processes";
    private const string F09WithARow = """{"kind":{"value":"dismissal"},"tasks":[{"name":"Hisobot"}]}""";
    private const string Missing = "019a0000-0000-7000-8000-000000000001";

    private const string Kiritish = "jarayon.kiritish_notogri";
    private const string Ruxsat = "jarayon.ruxsat_yoq";
    private const string Topilmadi = "jarayon.topilmadi";
    private const string Versiya = "jarayon.versiya";
    private const string QadamHolati = "jarayon.qadam_holati_notogri";
    private const string Holat = "jarayon.holat_notogri";
    private const string Ochilgan = "jarayon.allaqachon_ochilgan";
    private const string Band = "jarayon.band";
    private const string YakunKuchda = "jarayon.yakun_kuchda";
    private const string JshshirYoq = "jarayon.jshshir_yoq";

    private static readonly string[] SafetyTopics = ["machine", "gas", "electricity", "fire"];

    private readonly BonviApiFactory _factory;

    public ProcessWriteApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => _factory.Services.GetRequiredService<IClock>().Today;

    /// <summary>Method, route, the one permission it needs, a body that gets past binding, the answer that permission alone gets.</summary>
    public static TheoryData<string, string, string, string, int, string?> Endpoints => new()
    {
        { "POST", $"{Root}/cases", Permissions.ProcessesWork, $$"""{"processCode":"dismissal","employmentId":"{{Missing}}","lastWorkDay":"2026-12-01"}""", 404, Topilmadi },
        { "POST", $"{Root}/cases/{Missing}/steps/r01-9/done", Permissions.ProcessesWork, """{"version":1}""", 404, Topilmadi },
        { "POST", $"{Root}/cases/{Missing}/steps/r01-9/skip", Permissions.ProcessesWork, """{"version":1}""", 404, Topilmadi },
        { "POST", $"{Root}/cases/{Missing}/steps/r01-9/undo", Permissions.ProcessesWork, """{"version":1}""", 404, Topilmadi },
        { "POST", $"{Root}/cases/{Missing}/cancel", Permissions.ProcessesManage, """{"version":1,"reason":"Sabab"}""", 404, Topilmadi },
        { "POST", $"{Root}/cases/{Missing}/reopen", Permissions.ProcessesManage, """{"version":1,"reason":"Sabab"}""", 404, Topilmadi },
        { "POST", $"{Root}/reconcile", Permissions.ProcessesManage, string.Empty, 200, null },
        { "GET", $"{Root}/settings/stage-bindings", Permissions.ProcessesManage, string.Empty, 200, null },
        { "PUT", $"{Root}/settings/stage-bindings", Permissions.ProcessesManage, """{"bindings":[]}""", 200, null },
        { "GET", $"{Root}/holidays?year=2031", Permissions.ProcessesView, string.Empty, 200, null },
        { "POST", $"{Root}/holidays", Permissions.ProcessesManage, """{"date":"2031-02-03","kind":"Holiday","title":"RBAC"}""", 200, null },
        { "DELETE", $"{Root}/holidays/2031-02-04", Permissions.ProcessesManage, string.Empty, 404, Topilmadi },
    };

    // ────────────────────────────────────────────────────────────
    //  Auth and RBAC, per endpoint
    // ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task No_token_is_401_and_shows_nothing(string method, string route, string permission, string body, int status, string? code)
    {
        _ = (permission, status, code);
        using var client = _factory.CreateClient();

        var reply = await SendAsync(client, method, route, body);

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized, reply.Raw);
        reply.Raw.ShouldNotContain("processCode");
        reply.Raw.ShouldNotContain("bindingCode");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Expired_token_is_401_though_the_same_token_unexpired_gets_past_auth(string method, string route, string permission, string body, int status, string? code)
    {
        _ = code;
        var account = await TestAccounts.CreateAsync(_factory, permission);

        using var expired = ClientWith(Resigned(account.AccessToken, DateTime.UtcNow.AddDays(-1)));
        (await SendAsync(expired, method, route, body)).Status.ShouldBe(HttpStatusCode.Unauthorized);

        // The control: only the expiry differs, so the 401 above can be nothing else.
        using var valid = ClientWith(Resigned(account.AccessToken, DateTime.UtcNow.AddHours(1)));
        var reply = await SendAsync(valid, method, route, body);
        ((int)reply.Status).ShouldBe(status, reply.Raw);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Everything_but_the_needed_permission_is_403(string method, string route, string permission, string body, int status, string? code)
    {
        _ = (status, code);
        string[] others = [.. new[] { Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage, Permissions.ProcessesOverride, Permissions.EmployeesView }.Where(row => row != permission)];
        using var client = await ClientAsync(others);

        var reply = await SendAsync(client, method, route, body);

        reply.Status.ShouldBe(HttpStatusCode.Forbidden, reply.Raw);
        reply.Raw.ShouldNotContain("bindingCode");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task No_permission_at_all_is_403(string method, string route, string permission, string body, int status, string? code)
    {
        _ = (permission, status, code);
        using var client = await ClientAsync();

        (await SendAsync(client, method, route, body)).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task The_needed_permission_alone_gets_through_to_the_handler(string method, string route, string permission, string body, int status, string? code)
    {
        await StartEngineAsync();
        using var client = await ClientAsync(permission);

        var reply = await SendAsync(client, method, route, body);

        ((int)reply.Status).ShouldBe(status, reply.Raw);
        if (code is not null)
        {
            reply.Title.ShouldBe(code);
        }
    }

    [Fact]
    public async Task A_refused_403_writes_nothing()
    {
        var hire = await HireAsync("WR-" + Suffix(), Today);
        var date = new DateOnly(2032, 3, 4);
        using var noWork = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesManage);
        using var noManage = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork);
        await StartEngineAsync();

        (await PostAsync(noWork, $"{Root}/cases", StartBody(hire))).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await PostAsync(noManage, $"{Root}/holidays", new { date = Iso(date), kind = "Holiday", title = "Yo'q" })).Status.ShouldBe(HttpStatusCode.Forbidden);

        (await FindCaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).ShouldBeNull();
        (await DbAsync(db => db.ProcessHolidays.AnyAsync(row => row.Date == date, Ct))).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  «Bajarildi»
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Done_closes_the_step_as_the_person_and_the_answer_shows_what_the_pass_opened()
    {
        await ClearRolesAsync();
        var (hire, processCase) = await FormaliseAsync();
        await FillCardAsync(hire.EmployeeId, RandomPassport());
        await AttachDocumentsAsync(hire.EmployeeId, DocsStepTypes);
        var holder = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(holder.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(holder.AccessToken);
        var before = await DetailAsync(client, processCase.Id);
        StepOf(before, "r01-3").GetProperty("state").GetString().ShouldBe("Locked");
        var docs = await InputOptionsAsync(processCase.TemplateVersionId, "r01-2", "docs");

        var reply = await DoneAsync(client, processCase.Id, "r01-2", new { docs = docs.ToDictionary(item => item, _ => "done") }, Version(before));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        reply.Json.GetProperty("id").GetGuid().ShouldBe(processCase.Id);
        var closed = StepOf(reply.Json, "r01-2");
        closed.GetProperty("state").GetString().ShouldBe("Done");
        closed.GetProperty("resolution").GetString().ShouldBe("Manual");
        closed.GetProperty("doneBy").GetGuid().ShouldBe(holder.UserId);
        closed.GetProperty("doneAt").ValueKind.ShouldBe(JsonValueKind.String);
        closed.GetProperty("canUndo").GetBoolean().ShouldBeTrue();
        closed.GetProperty("payload").GetProperty("docs").GetProperty(docs[0]).GetString().ShouldBe("done");
        StepOf(reply.Json, "r01-3").GetProperty("state").GetString().ShouldBe("Active", "the pass of the answer's read");

        var stored = (await CaseByIdAsync(processCase.Id)).FindStep("r01-2")!;
        stored.State.ShouldBe(ProcessStepState.Done);
        stored.DoneBy.ShouldBe(holder.UserId);
    }

    [Fact]
    public async Task The_answers_version_is_the_one_the_next_write_needs()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var before = await DetailAsync(client, processCase.Id);

        var first = await DoneAsync(client, processCase.Id, "r01-6", new { enteredOn = Iso(Today) }, Version(before));
        first.Status.ShouldBe(HttpStatusCode.OK, first.Raw);
        Version(first.Json).ShouldBeGreaterThan(Version(before));
        Version(first.Json).ShouldBe((await CaseByIdAsync(processCase.Id)).Version);

        var second = await DoneAsync(client, processCase.Id, "r01-9", new { payment = "cash" }, Version(first.Json));
        second.Status.ShouldBe(HttpStatusCode.OK, second.Raw);
        StepOf(second.Json, "r01-9").GetProperty("state").GetString().ShouldBe("Done");
    }

    [Fact]
    public async Task Two_reads_and_no_change_keep_the_version_the_screen_was_given()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();

        var first = await DetailAsync(client, processCase.Id);
        var second = await DetailAsync(client, processCase.Id);

        Version(second).ShouldBe(Version(first), "a pass that changes nothing must not make every screen stale");
    }

    [Theory]
    [InlineData("done")]
    [InlineData("skip")]
    [InlineData("undo")]
    public async Task Step_write_without_a_version_is_400_with_the_code(string verb)
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();

        var reply = await PostRawAsync(client, $"{Root}/cases/{processCase.Id}/steps/r01-6/{verb}", "{}");

        reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
        reply.Title.ShouldBe(Kiritish);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("skip")]
    [InlineData("undo")]
    public async Task Step_write_with_a_stale_version_is_409_and_changes_nothing(string verb)
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var reply = await PostAsync(client, $"{Root}/cases/{processCase.Id}/steps/r01-6/{verb}", new { version = version - 1, inputs = new { enteredOn = Iso(Today) }, reason = "Sabab" });

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(Versiya);

        var after = await CaseByIdAsync(processCase.Id);
        after.Version.ShouldBe(version);
        after.FindStep("r01-6")!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task Version_of_the_wrong_type_and_a_missing_body_are_400()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var url = $"{Root}/cases/{processCase.Id}/steps/r01-6/done";

        (await PostRawAsync(client, url, """{"version":"yangi"}""")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await PostRawAsync(client, url, string.Empty)).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await PostRawAsync(client, url, "[1,2]")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task Unknown_case_and_unknown_step_are_404_and_a_finished_case_409()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var noStep = await DoneAsync(client, processCase.Id, "r99-9", null, version);
        noStep.Status.ShouldBe(HttpStatusCode.NotFound, noStep.Raw);
        noStep.Title.ShouldBe(Topilmadi);

        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version, reason = "Sabab" });
        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Raw);

        var finished = await DoneAsync(client, processCase.Id, "r01-6", new { enteredOn = Iso(Today) }, Version(cancelled.Json));
        finished.Status.ShouldBe(HttpStatusCode.Conflict, finished.Raw);
        finished.Title.ShouldBe(Holat);
    }

    [Fact]
    public async Task Done_on_a_locked_or_closed_step_is_409_even_for_a_manager()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var locked = await DoneAsync(client, processCase.Id, "r01-3", new { term = "permanent" }, version);
        locked.Status.ShouldBe(HttpStatusCode.Conflict, locked.Raw);
        locked.Title.ShouldBe(QadamHolati);

        var byFact = await DoneAsync(client, processCase.Id, "r01-5", null, version);
        byFact.Status.ShouldBe(HttpStatusCode.Conflict, byFact.Raw);
        byFact.Title.ShouldBe(QadamHolati);
    }

    [Fact]
    public async Task Fact_step_with_no_fallback_is_409_for_its_holder_and_a_manager_and_403_for_anyone_else()
    {
        await ClearRolesAsync();
        var (_, processCase) = await FormaliseAsync();
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-8a")!.State.ShouldBe(ProcessStepState.Active);
        var holder = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(holder.UserId, ProcessRoles.Hr3);
        using var holderClient = ClientWith(holder.AccessToken);
        using var manager = await ManagerAsync();
        using var stranger = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork);
        var version = Version(await DetailAsync(manager, processCase.Id));

        var asHolder = await DoneAsync(holderClient, processCase.Id, "r01-8a", null, version);
        asHolder.Status.ShouldBe(HttpStatusCode.Conflict, asHolder.Raw);
        asHolder.Title.ShouldBe(QadamHolati);

        var asManager = await DoneAsync(manager, processCase.Id, "r01-8a", null, version);
        asManager.Status.ShouldBe(HttpStatusCode.Conflict, asManager.Raw);
        asManager.Title.ShouldBe(QadamHolati);

        var asStranger = await DoneAsync(stranger, processCase.Id, "r01-8a", null, version);
        asStranger.Status.ShouldBe(HttpStatusCode.Forbidden, asStranger.Raw);
        asStranger.Title.ShouldBe(Ruxsat);

        (await CaseByIdAsync(processCase.Id)).FindStep("r01-8a")!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task Manual_step_refuses_a_non_holder_with_work_and_keeps_it_open()
    {
        await ClearRolesAsync();
        var (_, processCase) = await FormaliseAsync();
        var holder = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(holder.UserId, ProcessRoles.Hr2);
        var hr3 = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(hr3.UserId, ProcessRoles.Hr3);
        using var hr3Client = ClientWith(hr3.AccessToken);
        var version = Version(await DetailAsync(hr3Client, processCase.Id));

        var reply = await DoneAsync(hr3Client, processCase.Id, "r01-6", new { enteredOn = Iso(Today) }, version);

        reply.Status.ShouldBe(HttpStatusCode.Forbidden, reply.Raw);
        reply.Title.ShouldBe(Ruxsat);
        var after = (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!;
        after.State.ShouldBe(ProcessStepState.Active);
        after.DoneBy.ShouldBeNull();
    }

    [Fact]
    public async Task A_step_of_one_person_is_refused_to_another_employee_with_work()
    {
        await ClearRolesAsync();
        await StandInsAsync();
        var hire = await HireAsync(orderNo: null, Today);
        await StartEngineAsync();
        await BlankAsync(ProcessBlanks.F09, hire, F09WithARow);

        // subjectEmployee holds a step only when that employee has a user; without one the pass gives it to the fallback role.
        var subject = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await LinkUserAsync(subject.UserId, hire.EmployeeId);

        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));
        processCase.FindStep("ex-4")!.State.ShouldBe(ProcessStepState.Active);
        processCase.FindStep("ex-4")!.HolderEmployeeId.ShouldBe(hire.EmployeeId);

        var other = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await LinkUserAsync(other.UserId, (await HireAsync(orderNo: null, Today)).EmployeeId);
        using var subjectClient = ClientWith(subject.AccessToken);
        using var otherClient = ClientWith(other.AccessToken);
        var version = Version(await DetailAsync(subjectClient, processCase.Id));

        var asOther = await DoneAsync(otherClient, processCase.Id, "ex-4", null, version);
        asOther.Status.ShouldBe(HttpStatusCode.Forbidden, asOther.Raw);
        asOther.Title.ShouldBe(Ruxsat);

        // The subject holds it; it is a fact step with no fallback, so holding is not enough.
        var asSubject = await DoneAsync(subjectClient, processCase.Id, "ex-4", null, version);
        asSubject.Status.ShouldBe(HttpStatusCode.Conflict, asSubject.Raw);
        asSubject.Title.ShouldBe(QadamHolati);
    }

    // ────────────────────────────────────────────────────────────
    //  r01-9 and the inputs
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"inputs":null}""")]
    [InlineData("""{"inputs":{}}""")]
    [InlineData("""{"inputs":{"payment":null}}""")]
    [InlineData("""{"inputs":{"payment":"  "}}""")]
    [InlineData("""{"inputs":{"payment":"bitcoin"}}""")]
    [InlineData("""{"inputs":{"payment":1}}""")]
    [InlineData("""{"inputs":{"payment":"card","other":"x"}}""")]
    [InlineData("""{"inputs":{"payment":"card","other":null}}""")]
    [InlineData("""{"inputs":[]}""")]
    [InlineData("""{"inputs":"card"}""")]
    public async Task Payroll_handover_without_a_valid_payment_is_400_and_stays_open(string body)
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var reply = await PostRawAsync(client, $"{Root}/cases/{processCase.Id}/steps/{ProcessStepIds.PayrollHandover}/done", WithVersion(body, version));

        reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
        reply.Title.ShouldBe(Kiritish);
        var after = await CaseByIdAsync(processCase.Id);
        after.Version.ShouldBe(version);
        after.FindStep(ProcessStepIds.PayrollHandover)!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task Payroll_handover_with_no_jshshir_is_409_with_the_gates_code_even_with_the_payment()
    {
        var (_, processCase) = await FormaliseAsync(withPinfl: false);
        using var client = await ManagerAsync();
        var detail = await DetailAsync(client, processCase.Id);
        StepOf(detail, ProcessStepIds.PayrollHandover).GetProperty("state").GetString().ShouldBe("Blocked");

        var reply = await DoneAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, new { payment = "card" }, Version(detail));

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(JshshirYoq);
        (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.PayrollHandover)!.State.ShouldBe(ProcessStepState.Blocked);
    }

    [Fact]
    public async Task Skipping_the_payroll_handover_blocked_on_the_jshshir_is_409_with_the_gates_code()
    {
        var (_, processCase) = await FormaliseAsync(withPinfl: false);
        using var client = await ManagerAsync();
        var detail = await DetailAsync(client, processCase.Id);
        StepOf(detail, ProcessStepIds.PayrollHandover).GetProperty("state").GetString().ShouldBe("Blocked");

        var reply = await PostAsync(
            client,
            $"{Root}/cases/{processCase.Id}/steps/{ProcessStepIds.PayrollHandover}/skip",
            new { version = Version(detail), reason = "Kerak emas" });

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(JshshirYoq);
        (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.PayrollHandover)!.State.ShouldBe(ProcessStepState.Blocked);
    }

    [Fact]
    public async Task An_override_on_Q03_lifts_the_jshshir_block_but_not_the_payment()
    {
        var (_, processCase) = await FormaliseAsync(withPinfl: false);
        await ClearRolesAsync();
        using var grantor = await ClientAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage); // §8.4: nobody holds hr1, so manage decides.
        var grant = await PostAsync(grantor, $"{Root}/cases/{processCase.Id}/gates/{ProcessGateCodes.Q03}/override", new { reason = "Rahbar yozma ruxsat berdi" });
        grant.Status.ShouldNotBe(HttpStatusCode.BadRequest, grant.Raw);
        ((int)grant.Status).ShouldBeLessThan(300, grant.Raw);

        using var client = await ManagerAsync();
        var detail = await DetailAsync(client, processCase.Id);
        StepOf(detail, ProcessStepIds.PayrollHandover).GetProperty("state").GetString().ShouldBe("Active", "the override lifted the gate");

        var without = await DoneAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, null, Version(detail));
        without.Status.ShouldBe(HttpStatusCode.BadRequest, without.Raw);
        without.Title.ShouldBe(Kiritish);
        (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.PayrollHandover)!.State.ShouldBe(ProcessStepState.Active);

        var with = await DoneAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, new { payment = "cash" }, Version(detail));
        with.Status.ShouldBe(HttpStatusCode.OK, with.Raw);
        var done = StepOf(with.Json, ProcessStepIds.PayrollHandover);
        done.GetProperty("state").GetString().ShouldBe("Done");
        done.GetProperty("payload").GetProperty("payment").GetString().ShouldBe("cash");
    }

    [Fact]
    public async Task D1_step_writes_the_hash_of_the_canonical_payload()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var reply = await DoneAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, new { payment = "card" }, version);
        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);

        var stored = (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.PayrollHandover)!;
        stored.EvidenceSha256.ShouldBe(Sha256("""{"payment":"card"}"""));
    }

    [Fact]
    public async Task D1_hash_is_of_sorted_keys_and_can_be_recomputed_from_what_is_stored()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        // Sent in reverse, and jsonb stores keys shortest first: only the canonical form is stable.
        var reply = await DoneAsync(client, processCase.Id, ProcessStepIds.SafetyInstruction, new { topics = new { machine = "done", gas = "na", fire = "done", electricity = "done" } }, version);
        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);

        var stored = (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.SafetyInstruction)!;
        stored.EvidenceSha256.ShouldBe(Sha256("""{"topics":{"electricity":"done","fire":"done","gas":"na","machine":"done"}}"""));
    }

    [Fact]
    public async Task Step_without_a_signature_level_keeps_no_hash()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var reply = await DoneAsync(client, processCase.Id, "r01-6", new { enteredOn = Iso(Today) }, version);
        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);

        var stored = (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!;
        stored.EvidenceSha256.ShouldBeNull();
        stored.Payload.ShouldNotBeNull().ShouldContain(Iso(Today));
    }

    [Fact]
    public async Task Checklist_required_means_every_item_and_a_full_one_passes()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var partial = await DoneAsync(client, processCase.Id, ProcessStepIds.SafetyInstruction, new { topics = new { machine = "done" } }, version);
        partial.Status.ShouldBe(HttpStatusCode.BadRequest, partial.Raw);
        partial.Title.ShouldBe(Kiritish);

        var wrongMark = await DoneAsync(client, processCase.Id, ProcessStepIds.SafetyInstruction, new { topics = SafetyTopics.ToDictionary(item => item, _ => "yes") }, version);
        wrongMark.Status.ShouldBe(HttpStatusCode.BadRequest, wrongMark.Raw);

        var full = await DoneAsync(client, processCase.Id, ProcessStepIds.SafetyInstruction, new { topics = SafetyTopics.ToDictionary(item => item, _ => "done") }, version);
        full.Status.ShouldBe(HttpStatusCode.OK, full.Raw);
    }

    [Fact]
    public async Task Required_when_input_follows_the_other_inputs_value()
    {
        var (_, processCase) = await FormaliseAsync();
        var by = await UserIdAsync();
        await CloseManuallyAsync(processCase.Id, "r01-2", by, payload: null);
        await RunAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var fixedNoBasis = await DoneAsync(client, processCase.Id, "r01-3", new { term = "fixed" }, version);
        fixedNoBasis.Status.ShouldBe(HttpStatusCode.BadRequest, fixedNoBasis.Raw);
        fixedNoBasis.Title.ShouldBe(Kiritish);

        var fixedWithBasis = await DoneAsync(client, processCase.Id, "r01-3", new { term = "fixed", basis = "Mavsumiy ish" }, version);
        fixedWithBasis.Status.ShouldBe(HttpStatusCode.OK, fixedWithBasis.Raw);
        StepOf(fixedWithBasis.Json, "r01-3").GetProperty("payload").GetProperty("basis").GetString().ShouldBe("Mavsumiy ish");
    }

    [Fact]
    public async Task Required_when_input_does_not_ask_on_the_other_value()
    {
        var (_, processCase) = await FormaliseAsync();
        await CloseManuallyAsync(processCase.Id, "r01-2", await UserIdAsync(), payload: null);
        await RunAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var reply = await DoneAsync(client, processCase.Id, "r01-3", new { term = "permanent" }, version);

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
    }

    [Fact]
    public async Task Required_when_fact_asks_for_the_start_date_only_on_an_accepted_offer()
    {
        var accepted = await CandidateCaseAtOfferAsync("accept");
        var declined = await CandidateCaseAtOfferAsync("reject");
        using var client = await ManagerAsync();

        var acceptedVersion = Version(await DetailAsync(client, accepted.Id));
        var noDate = await DoneAsync(client, accepted.Id, ProcessStepIds.Offer, null, acceptedVersion);
        noDate.Status.ShouldBe(HttpStatusCode.BadRequest, noDate.Raw);
        noDate.Title.ShouldBe(Kiritish);

        var startDate = Iso(Today.AddDays(10));
        var withDate = await DoneAsync(client, accepted.Id, ProcessStepIds.Offer, new Dictionary<string, string> { [ProcessStepPayload.StartDateInput] = startDate }, acceptedVersion);
        withDate.Status.ShouldBe(HttpStatusCode.OK, withDate.Raw);
        StepOf(withDate.Json, ProcessStepIds.Offer).GetProperty("payload").GetProperty(ProcessStepPayload.StartDateInput).GetString().ShouldBe(startDate);

        var noNeed = await DoneAsync(client, declined.Id, ProcessStepIds.Offer, null, Version(await DetailAsync(client, declined.Id)));
        noNeed.Status.ShouldBe(HttpStatusCode.OK, noNeed.Raw);
    }

    // ────────────────────────────────────────────────────────────
    //  «Kerak emas»
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Legacy_skip_needs_a_reason_and_keeps_it()
    {
        var processCase = await LegacyProbationCaseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));
        var url = $"{Root}/cases/{processCase.Id}/steps/p03-0/skip";

        foreach (var body in new object[] { new { version }, new { version, reason = "   " } })
        {
            var refused = await PostAsync(client, url, body);
            refused.Status.ShouldBe(HttpStatusCode.BadRequest, refused.Raw);
            refused.Title.ShouldBe(Kiritish);
        }

        (await CaseByIdAsync(processCase.Id)).FindStep("p03-0")!.IsClosed.ShouldBeFalse();

        var skipped = await PostAsync(client, url, new { version, reason = "  Eski xodim, reja yo'q  " });
        skipped.Status.ShouldBe(HttpStatusCode.OK, skipped.Raw);

        var step = StepOf(skipped.Json, "p03-0");
        step.GetProperty("state").GetString().ShouldBe("Skipped");
        step.GetProperty("resolution").GetString().ShouldBe("Legacy");
        step.GetProperty("payload").GetProperty("reason").GetString().ShouldBe("Eski xodim, reja yo'q");
    }

    [Fact]
    public async Task Legacy_skip_is_refused_on_a_case_that_is_not_legacy()
    {
        await ClearRolesAsync();
        var processCase = await ProbationCaseAsync();
        using var manager = await ManagerAsync();
        using var stranger = await ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork);
        var version = Version(await DetailAsync(manager, processCase.Id));
        var url = $"{Root}/cases/{processCase.Id}/steps/p03-0/skip";

        var asManager = await PostAsync(manager, url, new { version, reason = "Sabab" });
        asManager.Status.ShouldBe(HttpStatusCode.Conflict, asManager.Raw);
        asManager.Title.ShouldBe(QadamHolati);

        var asStranger = await PostAsync(stranger, url, new { version, reason = "Sabab" });
        asStranger.Status.ShouldBe(HttpStatusCode.Forbidden, asStranger.Raw);
        asStranger.Title.ShouldBe(Ruxsat);

        (await CaseByIdAsync(processCase.Id)).FindStep("p03-0")!.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public async Task Skippable_step_needs_no_reason_and_keeps_one_given()
    {
        var quiet = await CandidateCaseAsync();
        var told = await CandidateCaseAsync();
        using var client = await ManagerAsync();

        var skipped = await PostAsync(client, $"{Root}/cases/{quiet.Id}/steps/r02-7/skip", new { version = Version(await DetailAsync(client, quiet.Id)) });
        skipped.Status.ShouldBe(HttpStatusCode.OK, skipped.Raw);
        var step = StepOf(skipped.Json, "r02-7");
        step.GetProperty("state").GetString().ShouldBe("Skipped");
        step.GetProperty("resolution").GetString().ShouldBe("UserSkip");
        step.GetProperty("payload").ValueKind.ShouldBe(JsonValueKind.Null);

        var withReason = await PostAsync(client, $"{Root}/cases/{told.Id}/steps/r02-7/skip", new { version = Version(await DetailAsync(client, told.Id)), reason = "Amaliy sinov shart emas" });
        withReason.Status.ShouldBe(HttpStatusCode.OK, withReason.Raw);
        StepOf(withReason.Json, "r02-7").GetProperty("payload").GetProperty("reason").GetString().ShouldBe("Amaliy sinov shart emas");
    }

    [Fact]
    public async Task Skip_of_a_closed_step_and_of_too_long_a_reason_are_refused()
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));
        var url = $"{Root}/cases/{processCase.Id}/steps/r02-7/skip";

        var tooLong = await PostAsync(client, url, new { version, reason = new string('s', ProcessCase.MaxCancelReasonLength + 1) });
        tooLong.Status.ShouldBe(HttpStatusCode.BadRequest, tooLong.Raw);
        tooLong.Title.ShouldBe(Kiritish);

        var first = await PostAsync(client, url, new { version });
        first.Status.ShouldBe(HttpStatusCode.OK, first.Raw);

        var again = await PostAsync(client, url, new { version = Version(first.Json) });
        again.Status.ShouldBe(HttpStatusCode.Conflict, again.Raw);
        again.Title.ShouldBe(QadamHolati);
    }

    // ────────────────────────────────────────────────────────────
    //  Undo
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Undo_of_ones_own_reopens_the_step_and_forgets_what_was_entered()
    {
        await ClearRolesAsync();
        var (_, processCase) = await FormaliseAsync();
        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(closer.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(closer.AccessToken);

        var done = await DoneAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, new { payment = "card" }, Version(await DetailAsync(client, processCase.Id)));
        done.Status.ShouldBe(HttpStatusCode.OK, done.Raw);

        var undone = await UndoAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, Version(done.Json));

        undone.Status.ShouldBe(HttpStatusCode.OK, undone.Raw);
        var step = StepOf(undone.Json, ProcessStepIds.PayrollHandover);
        step.GetProperty("state").GetString().ShouldBe("Active", "the pass of the answer's read recomputed it");
        step.GetProperty("resolution").ValueKind.ShouldBe(JsonValueKind.Null);
        step.GetProperty("doneBy").ValueKind.ShouldBe(JsonValueKind.Null);
        step.GetProperty("payload").ValueKind.ShouldBe(JsonValueKind.Null);

        var stored = (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.PayrollHandover)!;
        stored.EvidenceSha256.ShouldBeNull();
        stored.DoneAt.ShouldBeNull();
    }

    [Fact]
    public async Task Undo_of_someone_elses_step_is_403_and_it_stays_closed()
    {
        await ClearRolesAsync();
        var (_, processCase) = await FormaliseAsync();
        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        var colleague = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(closer.UserId, ProcessRoles.Hr2);
        await AssignRoleAsync(colleague.UserId, ProcessRoles.Hr2);
        await CloseStepAsAsync(processCase.Id, "r01-6", closer.UserId);
        using var client = ClientWith(colleague.AccessToken);

        var reply = await UndoAsync(client, processCase.Id, "r01-6", Version(await DetailAsync(client, processCase.Id)));

        reply.Status.ShouldBe(HttpStatusCode.Forbidden, reply.Raw);
        reply.Title.ShouldBe(Ruxsat);
        var stored = (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!;
        stored.State.ShouldBe(ProcessStepState.Done);
        stored.DoneBy.ShouldBe(closer.UserId);
    }

    [Fact]
    public async Task Undo_past_the_window_is_refused_to_the_closer_and_open_to_a_manager()
    {
        var (_, processCase) = await FormaliseAsync();
        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await CloseStepAsAsync(processCase.Id, "r01-6", closer.UserId);
        var old = DateTimeOffset.UtcNow - ProcessCase.UndoWindow - TimeSpan.FromMinutes(1);
        await SqlAsync($"UPDATE process_case_steps SET done_at = {old} WHERE case_id = {processCase.Id} AND step_code = 'r01-6'");
        using var client = ClientWith(closer.AccessToken);
        var version = Version(await DetailAsync(client, processCase.Id));

        var late = await UndoAsync(client, processCase.Id, "r01-6", version);
        late.Status.ShouldBe(HttpStatusCode.Forbidden, late.Raw);
        late.Title.ShouldBe(Ruxsat);
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!.State.ShouldBe(ProcessStepState.Done);

        using var manager = await ManagerAsync();
        var asManager = await UndoAsync(manager, processCase.Id, "r01-6", version);
        asManager.Status.ShouldBe(HttpStatusCode.OK, asManager.Raw);
        StepOf(asManager.Json, "r01-6").GetProperty("state").GetString().ShouldBe("Active");
    }

    [Fact]
    public async Task Undo_within_the_window_still_works_for_the_closer_just_before_it_ends()
    {
        var (_, processCase) = await FormaliseAsync();
        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await CloseStepAsAsync(processCase.Id, "r01-6", closer.UserId);
        var nearlyOver = DateTimeOffset.UtcNow - ProcessCase.UndoWindow + TimeSpan.FromMinutes(5);
        await SqlAsync($"UPDATE process_case_steps SET done_at = {nearlyOver} WHERE case_id = {processCase.Id} AND step_code = 'r01-6'");
        using var client = ClientWith(closer.AccessToken);

        var reply = await UndoAsync(client, processCase.Id, "r01-6", Version(await DetailAsync(client, processCase.Id)));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
    }

    [Fact]
    public async Task Undo_of_a_step_a_fact_closed_is_409_even_for_a_manager()
    {
        var (_, processCase) = await FormaliseAsync();
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-5")!.State.ShouldBe(ProcessStepState.Done);
        using var client = await ManagerAsync();

        var reply = await UndoAsync(client, processCase.Id, "r01-5", Version(await DetailAsync(client, processCase.Id)));

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(QadamHolati);
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-5")!.State.ShouldBe(ProcessStepState.Done);
    }

    [Fact]
    public async Task A_token_with_no_user_id_is_turned_away_by_authentication_before_any_handler()
    {
        var (_, processCase) = await FormaliseAsync();
        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await CloseStepAsAsync(processCase.Id, "r01-6", closer.UserId);
        await SqlAsync($"UPDATE process_case_steps SET done_by = NULL WHERE case_id = {processCase.Id} AND step_code = 'r01-6'");

        var account = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        using var client = ClientWith(WithoutUserId(account.AccessToken));
        using var manager = await ManagerAsync();
        var version = Version(await DetailAsync(manager, processCase.Id));

        var reply = await UndoAsync(client, processCase.Id, "r01-6", version);

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized, reply.Raw);
        reply.Title.ShouldBe("auth.token_yaroqsiz");
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!.State.ShouldBe(ProcessStepState.Done);
    }

    /// <summary>
    /// HTTP cannot get here (the test above), so the handler is called directly, as someone with the permissions and no
    /// id. <c>ProcessStepAbilityTests</c> pins that the ability alone says yes to this viewer on this step.
    /// </summary>
    [Fact]
    public async Task Undo_with_no_user_id_is_refused_though_the_ability_alone_would_match_a_null_closer()
    {
        var (_, processCase) = await FormaliseAsync();
        var closer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await CloseStepAsAsync(processCase.Id, "r01-6", closer.UserId);
        await SqlAsync($"UPDATE process_case_steps SET done_by = NULL WHERE case_id = {processCase.Id} AND step_code = 'r01-6'");
        using var manager = await ManagerAsync();
        var version = Version(await DetailAsync(manager, processCase.Id));

        await using var scope = _factory.Services.CreateAsyncScope();
        var viewers = ActivatorUtilities.CreateInstance<ProcessViewerLoader>(scope.ServiceProvider, new WorkerWithNoId());
        var writer = ActivatorUtilities.CreateInstance<ProcessStepWriter>(scope.ServiceProvider, viewers);

        var result = await new UndoProcessStepCommandHandler(writer).HandleAsync(new UndoProcessStepCommand(processCase.Id, "r01-6", version), Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(Ruxsat);
        var stored = (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!;
        stored.State.ShouldBe(ProcessStepState.Done);
        stored.DoneBy.ShouldBeNull();
    }

    private sealed class WorkerWithNoId : ICurrentUser
    {
        public Guid? UserId => null;

        public string? UserName => null;

        public bool IsAuthenticated => true;

        public bool HasPermission(string permission) => permission == Permissions.ProcessesView || permission == Permissions.ProcessesWork;
    }

    // ────────────────────────────────────────────────────────────
    //  Races
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_race_between_two_contexts_is_a_stale_process_write()
    {
        var (_, processCase) = await FormaliseAsync();
        var by = await UserIdAsync();
        await using var scopeA = _factory.Services.CreateAsyncScope();
        await using var scopeB = _factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await dbA.ProcessCases.Include(row => row.Steps).SingleAsync(row => row.Id == processCase.Id, Ct);
        var b = await dbB.ProcessCases.Include(row => row.Steps).SingleAsync(row => row.Id == processCase.Id, Ct);
        var now = DateTimeOffset.UtcNow;

        // Different steps of one case: only the case's version can see this one.
        a.CompleteStepManually("r01-6", by, null, null, now).IsSuccess.ShouldBeTrue();
        b.CompleteStepManually(ProcessStepIds.PayrollHandover, by, null, null, now).IsSuccess.ShouldBeTrue();

        await dbA.SaveChangesAsync(Ct);
        var exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));

        scopeB.ServiceProvider.GetRequiredService<IDatabaseFault>().IsStaleProcessWrite(exception).ShouldBeTrue();

        var after = await CaseByIdAsync(processCase.Id);
        after.FindStep("r01-6")!.State.ShouldBe(ProcessStepState.Done);
        after.FindStep(ProcessStepIds.PayrollHandover)!.State.ShouldBe(ProcessStepState.Active, "the loser wrote nothing");
    }

    [Fact]
    public async Task Two_parallel_clicks_on_one_step_give_one_200_and_one_409_never_500()
    {
        var (_, processCase) = await FormaliseAsync();
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();
        var version = Version(await DetailAsync(first, processCase.Id));
        var url = $"{Root}/cases/{processCase.Id}/steps/r01-6/done";
        var body = new { version, inputs = new { enteredOn = Iso(Today) } };

        var replies = await Task.WhenAll(PostAsync(first, url, body), PostAsync(second, url, body));

        replies.Count(reply => reply.Status == HttpStatusCode.OK).ShouldBe(1, string.Join(" | ", replies.Select(reply => reply.Raw)));
        var loser = replies.Single(reply => reply.Status != HttpStatusCode.OK);
        loser.Status.ShouldBe(HttpStatusCode.Conflict, loser.Raw);
        loser.Title.ShouldBe(Versiya);

        var stepId = (await CaseByIdAsync(processCase.Id)).FindStep("r01-6")!.Id;
        (await AuditRowsAsync(nameof(ProcessCaseStep), stepId)).Count.ShouldBe(1, "the loser left no audit row");
    }

    [Fact]
    public async Task Two_parallel_writes_on_different_steps_give_one_200_and_one_409_never_500()
    {
        var (_, processCase) = await FormaliseAsync();
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();
        var version = Version(await DetailAsync(first, processCase.Id));

        var replies = await Task.WhenAll(
            PostAsync(first, $"{Root}/cases/{processCase.Id}/steps/r01-6/done", new { version, inputs = new { enteredOn = Iso(Today) } }),
            PostAsync(second, $"{Root}/cases/{processCase.Id}/steps/{ProcessStepIds.PayrollHandover}/done", new { version, inputs = new { payment = "card" } }));

        replies.Count(reply => reply.Status == HttpStatusCode.OK).ShouldBe(1, string.Join(" | ", replies.Select(reply => reply.Raw)));
        var loser = replies.Single(reply => reply.Status != HttpStatusCode.OK);
        loser.Status.ShouldBe(HttpStatusCode.Conflict, loser.Raw);
        loser.Title.ShouldBe(Versiya);

        var after = await CaseByIdAsync(processCase.Id);
        new[] { after.FindStep("r01-6")!.State, after.FindStep(ProcessStepIds.PayrollHandover)!.State }
            .Count(state => state == ProcessStepState.Done).ShouldBe(1);
    }

    [Fact]
    public async Task Step_write_takes_no_engine_lock_and_goes_through_while_a_pass_holds_it()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));

        var reply = await WhileEngineLockedAsync(() => DoneAsync(client, processCase.Id, "r01-6", new { enteredOn = Iso(Today) }, version));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        StepOf(reply.Json, "r01-6").GetProperty("state").GetString().ShouldBe("Done");
    }
}
