using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
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

namespace Bonvi.Api.IntegrationTests.Processes;

public sealed partial class ProcessWriteApiTests
{
    // ────────────────────────────────────────────────────────────
    //  POST /cases
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_ends_ex1_as_done_by_the_person_with_the_inputs_as_payload()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        var starter = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        using var client = ClientWith(starter.AccessToken);
        var lastWorkDay = Today.AddDays(14);

        var reply = await PostAsync(client, $"{Root}/cases", StartBody(hire, lastWorkDay, "  Ariza 12-son  "));

        reply.Status.ShouldBe(HttpStatusCode.Created, reply.Raw);
        var id = reply.Json.GetProperty("id").GetGuid();
        reply.Location.ShouldNotBeNull().ToString().ShouldEndWith($"/api/processes/cases/{id}");
        reply.Json.GetProperty("processCode").GetString().ShouldBe(ProcessCodes.Dismissal);
        reply.Json.GetProperty("status").GetString().ShouldBe("Active");
        reply.Json.GetProperty("legacy").GetBoolean().ShouldBeFalse();
        reply.Json.GetProperty("anchorDate").GetString().ShouldBe(Iso(lastWorkDay));

        var ex1 = StepOf(reply.Json, ProcessStepIds.DismissalApplication);
        ex1.GetProperty("state").GetString().ShouldBe("Done");
        ex1.GetProperty("resolution").GetString().ShouldBe("Manual");
        ex1.GetProperty("doneBy").GetGuid().ShouldBe(starter.UserId);
        ex1.GetProperty("payload").GetProperty("lastWorkDay").GetString().ShouldBe(Iso(lastWorkDay));
        ex1.GetProperty("payload").GetProperty("basis").GetString().ShouldBe("Ariza 12-son");
        StepOf(reply.Json, "ex-2").GetProperty("state").GetString().ShouldBe("Active", "the first pass ran before the answer");

        var stored = await CaseByIdAsync(id);
        stored.StartedBy.ShouldBe(starter.UserId);
        stored.SubjectRef.ShouldBe(ProcessSubjectRefs.Dismissal(hire.EmploymentId));
        stored.EmploymentId.ShouldBe(hire.EmploymentId);
        stored.EmployeeId.ShouldBe(hire.EmployeeId);
        stored.StartedOn.ShouldBe(Today);
    }

    [Fact]
    public async Task Start_with_no_basis_keeps_only_the_date_in_the_payload()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        using var client = await ManagerAsync();

        var reply = await PostAsync(client, $"{Root}/cases", new { processCode = ProcessCodes.Dismissal, employmentId = hire.EmploymentId, lastWorkDay = Iso(Today) });

        reply.Status.ShouldBe(HttpStatusCode.Created, reply.Raw);
        var payload = StepOf(reply.Json, ProcessStepIds.DismissalApplication).GetProperty("payload");
        payload.GetProperty("lastWorkDay").GetString().ShouldBe(Iso(Today));
        payload.TryGetProperty("basis", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Started_case_keeps_its_anchor_and_ex1_through_passes_an_F08_and_the_termination()
    {
        await StandInsAsync();
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        var starter = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        using var client = ClientWith(starter.AccessToken);
        var lastWorkDay = Today.AddDays(14);
        var reply = await PostAsync(client, $"{Root}/cases", StartBody(hire, lastWorkDay, "Ariza"));
        reply.Status.ShouldBe(HttpStatusCode.Created, reply.Raw);
        var id = reply.Json.GetProperty("id").GetGuid();

        await RunAsync();
        (await CaseByIdAsync(id)).AnchorDate.ShouldBe(lastWorkDay);

        await BlankAsync(ProcessBlanks.F08, hire, $$"""{"leaveDate":"{{Iso(Today.AddDays(40))}}"}""");
        await RunAsync();
        var afterForm = await CaseByIdAsync(id);
        afterForm.AnchorDate.ShouldBe(lastWorkDay, "§3.7: the manual date wins over the F08 leave date");

        await TerminateAsync(hire);
        await RunAsync();
        var afterTermination = await CaseByIdAsync(id);
        afterTermination.AnchorDate.ShouldBe(lastWorkDay, "§3.7: the manual date wins over the termination date");

        var ex1 = afterTermination.FindStep(ProcessStepIds.DismissalApplication)!;
        ex1.State.ShouldBe(ProcessStepState.Done);
        ex1.Resolution.ShouldBe(ProcessStepResolution.Manual, "the pass does not overwrite a person's word");
        ex1.DoneBy.ShouldBe(starter.UserId);
        ex1.Payload.ShouldNotBeNull().ShouldContain("Ariza");
    }

    [Fact]
    public async Task Start_while_dormant_is_409_and_creates_nothing()
    {
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        using var client = await ManagerAsync();
        await SetStartedOnAsync(null);

        var reply = await PostAsync(client, $"{Root}/cases", StartBody(hire));

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(Holat);
        (await FindCaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).ShouldBeNull();
        await StartEngineAsync();
    }

    [Fact]
    public async Task Second_start_is_409_and_leaves_the_first_alone()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        using var client = await ManagerAsync();
        var first = await PostAsync(client, $"{Root}/cases", StartBody(hire, Today.AddDays(5), "Birinchi"));
        first.Status.ShouldBe(HttpStatusCode.Created, first.Raw);
        var id = first.Json.GetProperty("id").GetGuid();

        var second = await PostAsync(client, $"{Root}/cases", StartBody(hire, Today.AddDays(9), "Ikkinchi"));

        second.Status.ShouldBe(HttpStatusCode.Conflict, second.Raw);
        second.Title.ShouldBe(Ochilgan);
        var stored = await CaseByIdAsync(id);
        stored.AnchorDate.ShouldBe(Today.AddDays(5));
        stored.FindStep(ProcessStepIds.DismissalApplication)!.Payload.ShouldNotBeNull().ShouldContain("Birinchi");
    }

    [Fact]
    public async Task Start_is_refused_when_the_starter_has_opened_a_case_or_a_withdrawn_one_stands()
    {
        await StandInsAsync();
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        await BlankAsync(ProcessBlanks.F09, hire, F09WithARow);
        await RunAsync();
        var existing = await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId));
        using var client = await ManagerAsync();

        var running = await PostAsync(client, $"{Root}/cases", StartBody(hire));
        running.Status.ShouldBe(HttpStatusCode.Conflict, running.Raw);
        running.Title.ShouldBe(Ochilgan);

        await SqlAsync($"UPDATE process_cases SET status = {(short)ProcessCaseStatus.Completed}, outcome = 'withdrawn', completed_at = now() WHERE id = {existing.Id}");

        var withdrawn = await PostAsync(client, $"{Root}/cases", StartBody(hire));
        withdrawn.Status.ShouldBe(HttpStatusCode.Conflict, withdrawn.Raw);
        withdrawn.Title.ShouldBe(Ochilgan);
    }

    [Fact]
    public async Task Start_on_a_terminated_employment_is_409_and_creates_nothing()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        await TerminateAsync(hire);
        using var client = await ManagerAsync();

        var reply = await PostAsync(client, $"{Root}/cases", StartBody(hire));

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(Holat);
        (await FindCaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).ShouldBeNull();
    }

    [Fact]
    public async Task Start_validates_each_field()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        using var client = await ManagerAsync();
        var url = $"{Root}/cases";
        var day = Iso(Today);

        object[] bodies =
        [
            new { processCode = "hire-request", employmentId = hire.EmploymentId, lastWorkDay = day },
            new { processCode = "Dismissal", employmentId = hire.EmploymentId, lastWorkDay = day },
            new { employmentId = hire.EmploymentId, lastWorkDay = day },
            new { processCode = ProcessCodes.Dismissal, lastWorkDay = day },
            new { processCode = ProcessCodes.Dismissal, employmentId = hire.EmploymentId },
            new { processCode = ProcessCodes.Dismissal, employmentId = hire.EmploymentId, lastWorkDay = Iso(Today.AddDays(-31)) },
            new { processCode = ProcessCodes.Dismissal, employmentId = hire.EmploymentId, lastWorkDay = day, basis = new string('b', ProcessCase.MaxCancelReasonLength + 1) },
        ];

        foreach (var body in bodies)
        {
            var reply = await PostAsync(client, url, body);
            reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
            reply.Title.ShouldBe(Kiritish, JsonSerializer.Serialize(body));
        }

        (await PostRawAsync(client, url, $$"""{"processCode":"dismissal","employmentId":"{{hire.EmploymentId}}","lastWorkDay":"2026-13-45"}""")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await PostRawAsync(client, url, """{"processCode":"dismissal","employmentId":"not-a-guid","lastWorkDay":"2026-12-01"}""")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await FindCaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).ShouldBeNull();
    }

    [Fact]
    public async Task Start_on_the_hire_date_itself_is_allowed()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-3));
        using var client = await ManagerAsync();

        var reply = await PostAsync(client, $"{Root}/cases", StartBody(hire, Today.AddDays(-3)));

        reply.Status.ShouldBe(HttpStatusCode.Created, reply.Raw);
    }

    [Fact]
    public async Task Two_parallel_starts_make_one_case_and_the_other_is_a_409()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        using var first = await ManagerAsync();
        using var second = await ManagerAsync();

        var replies = await Task.WhenAll(PostAsync(first, $"{Root}/cases", StartBody(hire)), PostAsync(second, $"{Root}/cases", StartBody(hire)));

        replies.Count(reply => reply.Status == HttpStatusCode.Created).ShouldBe(1, string.Join(" | ", replies.Select(reply => reply.Raw)));
        var loser = replies.Single(reply => reply.Status != HttpStatusCode.Created);
        loser.Status.ShouldBe(HttpStatusCode.Conflict, loser.Raw);
        loser.Title.ShouldBeOneOf(Ochilgan, Band);
        (await CaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Start_while_a_pass_holds_the_lock_is_409_band_and_the_same_click_works_after()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        using var client = await ManagerAsync();

        var busy = await WhileEngineLockedAsync(() => PostAsync(client, $"{Root}/cases", StartBody(hire)));

        busy.Status.ShouldBe(HttpStatusCode.Conflict, busy.Raw);
        busy.Title.ShouldBe(Band);
        (await FindCaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId))).ShouldBeNull();

        (await PostAsync(client, $"{Root}/cases", StartBody(hire))).Status.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Undo_of_ex1_lets_the_pass_close_it_as_automatic_and_the_audit_keeps_the_basis()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        var starter = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        using var client = ClientWith(starter.AccessToken);
        var started = await PostAsync(client, $"{Root}/cases", StartBody(hire, Today.AddDays(3), "Ariza asosi"));
        started.Status.ShouldBe(HttpStatusCode.Created, started.Raw);
        var id = started.Json.GetProperty("id").GetGuid();
        StepOf(started.Json, ProcessStepIds.DismissalApplication).GetProperty("canUndo").GetBoolean().ShouldBeTrue();

        var undone = await UndoAsync(client, id, ProcessStepIds.DismissalApplication, Version(started.Json));

        undone.Status.ShouldBe(HttpStatusCode.OK, undone.Raw);
        var ex1 = StepOf(undone.Json, ProcessStepIds.DismissalApplication);
        ex1.GetProperty("state").GetString().ShouldBe("Done");
        ex1.GetProperty("resolution").GetString().ShouldBe("Auto");
        ex1.GetProperty("doneBy").ValueKind.ShouldBe(JsonValueKind.Null);

        var stepId = (await CaseByIdAsync(id)).FindStep(ProcessStepIds.DismissalApplication)!.Id;
        (await AuditRowsAsync(nameof(ProcessCaseStep), stepId)).ShouldContain(row => row.NewValues != null && row.NewValues.Contains("Ariza asosi"));
    }

    // ────────────────────────────────────────────────────────────
    //  Cancel and reopen
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_stops_the_case_with_its_reason_and_a_second_cancel_is_409()
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();

        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "  Nomzod boshqa ishga kirdi  " });

        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Raw);
        cancelled.Json.GetProperty("status").GetString().ShouldBe("Cancelled");
        cancelled.Json.GetProperty("cancelReason").GetString().ShouldBe("Nomzod boshqa ishga kirdi");
        cancelled.Json.GetProperty("cancelledAt").ValueKind.ShouldBe(JsonValueKind.String);
        (await CaseByIdAsync(processCase.Id)).Status.ShouldBe(ProcessCaseStatus.Cancelled);

        var again = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(cancelled.Json), reason = "Yana" });
        again.Status.ShouldBe(HttpStatusCode.Conflict, again.Raw);
        again.Title.ShouldBe(Holat);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("reopen")]
    public async Task Cancel_and_reopen_validate_version_and_reason_and_409_on_stale(string verb)
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));
        if (verb == "reopen")
        {
            var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version, reason = "Tayyorlash" });
            version = Version(cancelled.Json);
        }

        var url = $"{Root}/cases/{processCase.Id}/{verb}";
        var status = (await CaseByIdAsync(processCase.Id)).Status;

        object[] invalid =
        [
            new { reason = "Sabab" },
            new { version },
            new { version, reason = "   " },
            new { version, reason = new string('s', ProcessCase.MaxCancelReasonLength + 1) },
        ];

        foreach (var body in invalid)
        {
            var reply = await PostAsync(client, url, body);
            reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
            reply.Title.ShouldBe(Kiritish);
        }

        var stale = await PostAsync(client, url, new { version = version - 1, reason = "Sabab" });
        stale.Status.ShouldBe(HttpStatusCode.Conflict, stale.Raw);
        stale.Title.ShouldBe(Versiya);

        var after = await CaseByIdAsync(processCase.Id);
        after.Status.ShouldBe(status);
        after.Version.ShouldBe(version);
    }

    [Fact]
    public async Task Reopen_of_a_rejected_candidate_is_409_and_the_row_stays_completed()
    {
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.Rejected} WHERE id = {candidate}");
        await RunAsync();
        var before = await CaseByIdAsync(processCase.Id);
        before.Status.ShouldBe(ProcessCaseStatus.Completed);
        before.Outcome.ShouldBe("rejected");
        var auditBefore = (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).Count;
        using var client = await ManagerAsync();

        var reply = await PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = before.Version, reason = "Xato rad etilgan" });

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(YakunKuchda);

        var after = await CaseByIdAsync(processCase.Id);
        after.Status.ShouldBe(ProcessCaseStatus.Completed);
        after.Outcome.ShouldBe("rejected");
        after.CompletedAt.ShouldBe(before.CompletedAt);
        after.Version.ShouldBe(before.Version);
        after.Steps.Select(step => (step.StepCode, step.State, step.Resolution)).OrderBy(row => row.StepCode, StringComparer.Ordinal)
            .ShouldBe(before.Steps.Select(step => (step.StepCode, step.State, step.Resolution)).OrderBy(row => row.StepCode, StringComparer.Ordinal));
        (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).Count.ShouldBe(auditBefore, "a refused reopen leaves no trace");
    }

    [Fact]
    public async Task Reopen_with_no_end_fact_goes_active_recomputes_and_audits_the_reason()
    {
        var processCase = await CandidateCaseAsync();
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        using var client = ClientWith(manager.AccessToken);
        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Xato bekor qilindi" });
        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Raw);

        var reply = await PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = Version(cancelled.Json), reason = "  Nomzod qaytdi  " });

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        reply.Json.GetProperty("status").GetString().ShouldBe("Active");
        reply.Json.GetProperty("cancelReason").ValueKind.ShouldBe(JsonValueKind.Null);
        reply.Json.GetProperty("cancelledAt").ValueKind.ShouldBe(JsonValueKind.Null);
        StepOf(reply.Json, "r02-5").GetProperty("state").GetString().ShouldBe("Active");

        var stored = await CaseByIdAsync(processCase.Id);
        stored.Status.ShouldBe(ProcessCaseStatus.Active);
        stored.CancelReason.ShouldBeNull();

        var rows = await AuditRowsAsync(nameof(ProcessCase), processCase.Id);
        var withReason = rows.Single(row => row.NewValues != null && row.NewValues.Contains("Nomzod qaytdi"));
        withReason.Action.ShouldBe(AuditAction.Update);
        withReason.UserId.ShouldBe(manager.UserId);
        withReason.NewValues.ShouldNotBeNull().ShouldNotContain("  Nomzod qaytdi");
    }

    [Fact]
    public async Task Reopen_after_the_end_fact_went_away_goes_active()
    {
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.Rejected} WHERE id = {candidate}");
        await RunAsync();
        var ended = await CaseByIdAsync(processCase.Id);
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.InProgress} WHERE id = {candidate}");
        using var client = await ManagerAsync();

        var reply = await PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = ended.Version, reason = "Rad etish bekor qilindi" });

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        reply.Json.GetProperty("status").GetString().ShouldBe("Active");
        reply.Json.GetProperty("outcome").ValueKind.ShouldBe(JsonValueKind.Null);
        StepOf(reply.Json, "r02-5").GetProperty("state").GetString().ShouldBe("Active");
    }

    [Fact]
    public async Task Reopen_of_a_running_case_is_409_and_unknown_is_404()
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();

        var running = await PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Sabab" });
        running.Status.ShouldBe(HttpStatusCode.Conflict, running.Raw);
        running.Title.ShouldBe(Holat);

        var unknown = await PostAsync(client, $"{Root}/cases/{Guid.NewGuid()}/reopen", new { version = 1, reason = "Sabab" });
        unknown.Status.ShouldBe(HttpStatusCode.NotFound, unknown.Raw);
        unknown.Title.ShouldBe(Topilmadi);
    }

    [Fact]
    public async Task Reopen_while_a_pass_holds_the_lock_is_409_band_and_changes_nothing()
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();
        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Tayyorlash" });

        var busy = await WhileEngineLockedAsync(() => PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = Version(cancelled.Json), reason = "Sabab" }));

        busy.Status.ShouldBe(HttpStatusCode.Conflict, busy.Raw);
        busy.Title.ShouldBe(Band);
        (await CaseByIdAsync(processCase.Id)).Status.ShouldBe(ProcessCaseStatus.Cancelled);
    }

    [Fact]
    public async Task Reopen_while_dormant_runs_no_pass_and_stands()
    {
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        var processCase = await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
        await SqlAsync($"UPDATE candidates SET status = {(short)CandidateStatus.Rejected} WHERE id = {candidate}");
        await RunAsync();
        var ended = await CaseByIdAsync(processCase.Id);
        using var client = await ManagerAsync();
        await SetStartedOnAsync(null);

        var reply = await PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = ended.Version, reason = "Tizim uxlayapti" });

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        (await CaseByIdAsync(processCase.Id)).Status.ShouldBe(ProcessCaseStatus.Active);
        await StartEngineAsync();
    }

    [Fact]
    public async Task Text_with_a_nul_character_is_400_and_not_a_database_error()
    {
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        var (_, processCase) = await FormaliseAsync();
        var candidate = await CandidateCaseAsync();
        using var client = await ManagerAsync();
        var version = Version(await DetailAsync(client, processCase.Id));
        var candidateVersion = Version(await DetailAsync(client, candidate.Id));
        const string Nul = "a\\u0000b";

        var replies = new[]
        {
            await PostRawAsync(client, $"{Root}/cases", $$"""{"processCode":"dismissal","employmentId":"{{hire.EmploymentId}}","lastWorkDay":"{{Iso(Today)}}","basis":"{{Nul}}"}"""),
            await PostRawAsync(client, $"{Root}/cases/{candidate.Id}/steps/r02-7/skip", $$"""{"version":{{candidateVersion}},"reason":"{{Nul}}"}"""),
            await PostRawAsync(client, $"{Root}/cases/{candidate.Id}/cancel", $$"""{"version":{{candidateVersion}},"reason":"{{Nul}}"}"""),
            await PostRawAsync(client, $"{Root}/holidays", $$"""{"date":"2033-05-06","kind":"Holiday","title":"{{Nul}}"}"""),
        };

        foreach (var reply in replies)
        {
            reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
            reply.Title.ShouldBe(Kiritish);
        }

        await CloseManuallyAsync(processCase.Id, "r01-2", await UserIdAsync(), payload: null);
        await RunAsync();
        version = Version(await DetailAsync(client, processCase.Id));
        var input = await PostRawAsync(client, $"{Root}/cases/{processCase.Id}/steps/r01-3/done", $$$"""{"version":{{{version}}},"inputs":{"term":"permanent","contractNo":"{{{Nul}}}"}}""");
        input.Status.ShouldBe(HttpStatusCode.BadRequest, input.Raw);
        input.Title.ShouldBe(Kiritish);
    }

    // ────────────────────────────────────────────────────────────
    //  §14 audit
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_is_audited_as_the_person_on_the_case_and_on_ex1()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today.AddDays(-30));
        var starter = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        using var client = ClientWith(starter.AccessToken);

        var reply = await PostAsync(client, $"{Root}/cases", StartBody(hire, Today.AddDays(2), "Ariza 7"));
        reply.Status.ShouldBe(HttpStatusCode.Created, reply.Raw);
        var id = reply.Json.GetProperty("id").GetGuid();

        var caseRow = (await AuditRowsAsync(nameof(ProcessCase), id)).ShouldHaveSingleItem();
        caseRow.Action.ShouldBe(AuditAction.Create);
        caseRow.UserId.ShouldBe(starter.UserId);
        caseRow.SubjectEmployeeId.ShouldBe(hire.EmployeeId);

        var stored = await CaseByIdAsync(id);
        var stepRow = (await AuditRowsAsync(nameof(ProcessCaseStep), stored.FindStep(ProcessStepIds.DismissalApplication)!.Id)).ShouldHaveSingleItem();
        stepRow.UserId.ShouldBe(starter.UserId);
        stepRow.NewValues.ShouldNotBeNull().ShouldContain("Ariza 7");

        (await AuditRowsAsync(nameof(ProcessCaseStep), stored.FindStep("ex-2")!.Id)).ShouldBeEmpty("a step nobody closed is the engine's");
    }

    [Fact]
    public async Task Done_is_audited_with_the_inputs_and_the_hash_and_the_pass_after_it_is_not()
    {
        var (_, processCase) = await FormaliseAsync();
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        using var client = ClientWith(manager.AccessToken);

        var done = await DoneAsync(client, processCase.Id, ProcessStepIds.PayrollHandover, new { payment = "card" }, Version(await DetailAsync(client, processCase.Id)));
        done.Status.ShouldBe(HttpStatusCode.OK, done.Raw);

        var stored = await CaseByIdAsync(processCase.Id);
        var step = stored.FindStep(ProcessStepIds.PayrollHandover)!;
        var row = (await AuditRowsAsync(nameof(ProcessCaseStep), step.Id)).ShouldHaveSingleItem();
        row.Action.ShouldBe(AuditAction.Update);
        row.UserId.ShouldBe(manager.UserId);
        row.ActorKind.ShouldBe(AuditActorKind.User);
        row.NewValues.ShouldNotBeNull().ShouldContain("card");
        row.NewValues!.ShouldContain(step.EvidenceSha256!);

        (await AuditRowsAsync(nameof(ProcessCaseStep), stored.FindStep("r01-5")!.Id)).ShouldBeEmpty("a fact-driven step is not audited");
        (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).ShouldBeEmpty("a step write is the step's entry, not the case's");
    }

    [Fact]
    public async Task Skip_and_undo_are_audited_as_the_person()
    {
        var processCase = await CandidateCaseAsync();
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        using var client = ClientWith(manager.AccessToken);

        var skipped = await PostAsync(client, $"{Root}/cases/{processCase.Id}/steps/r02-7/skip", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Sinov shart emas" });
        skipped.Status.ShouldBe(HttpStatusCode.OK, skipped.Raw);
        var stepId = (await CaseByIdAsync(processCase.Id)).FindStep("r02-7")!.Id;

        var afterSkip = (await AuditRowsAsync(nameof(ProcessCaseStep), stepId)).ShouldHaveSingleItem();
        afterSkip.UserId.ShouldBe(manager.UserId);
        afterSkip.NewValues.ShouldNotBeNull().ShouldContain("Sinov shart emas");

        var undone = await UndoAsync(client, processCase.Id, "r02-7", Version(skipped.Json));
        undone.Status.ShouldBe(HttpStatusCode.OK, undone.Raw);

        var rows = await AuditRowsAsync(nameof(ProcessCaseStep), stepId);
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(row => row.UserId == manager.UserId);
        rows.OrderBy(row => row.OccurredAt).Last().OldValues.ShouldNotBeNull().ShouldContain("Sinov shart emas");
    }

    [Fact]
    public async Task Cancel_is_audited_with_its_reason()
    {
        var processCase = await CandidateCaseAsync();
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        using var client = ClientWith(manager.AccessToken);

        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Bekor sababi" });
        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Raw);

        var row = (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).ShouldHaveSingleItem();
        row.Action.ShouldBe(AuditAction.Update);
        row.UserId.ShouldBe(manager.UserId);
        row.NewValues.ShouldNotBeNull().ShouldContain("Bekor sababi");
    }

    [Fact]
    public async Task Audit_screen_lists_process_entries_in_their_module_with_the_reopen_reason()
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();
        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Xato" });
        var reopened = await PostAsync(client, $"{Root}/cases/{processCase.Id}/reopen", new { version = Version(cancelled.Json), reason = "Audit ko'rinishi" });
        reopened.Status.ShouldBe(HttpStatusCode.OK, reopened.Raw);
        using var auditor = await ClientAsync(Permissions.AuditView, Permissions.ProcessesView);

        var page = await GetAsync(auditor, $"/api/audit?module={AccessModuleKeys.Processes}&entity={nameof(ProcessCase)}&entityId={processCase.Id}");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.ShouldAllBe(item => item.GetProperty("module").GetString() == AccessModuleKeys.Processes);
        items.ShouldAllBe(item => item.GetProperty("entityName").GetString() == nameof(ProcessCase));
        items.ShouldContain(item => item.GetRawText().Contains("Audit ko'rinishi", StringComparison.Ordinal) || item.GetRawText().Contains("Audit ko\\u0027rinishi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Audit_screen_without_the_module_permission_hides_process_entries()
    {
        var processCase = await CandidateCaseAsync();
        using var client = await ManagerAsync();
        var cancelled = await PostAsync(client, $"{Root}/cases/{processCase.Id}/cancel", new { version = Version(await DetailAsync(client, processCase.Id)), reason = "Xato" });
        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Raw);
        using var auditor = await ClientAsync(Permissions.AuditView, Permissions.EmployeesView);

        var page = await GetAsync(auditor, $"/api/audit?entity={nameof(ProcessCase)}&entityId={processCase.Id}");

        page.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    // ────────────────────────────────────────────────────────────
    //  Stage bindings
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bindings_list_always_has_the_four_codes_and_put_binds_and_unbinds_only_what_it_names()
    {
        await ClearBindingsAsync();
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesManage);
        using var client = ClientWith(manager.AccessToken);
        var url = $"{Root}/settings/stage-bindings";

        var empty = await GetAsync(client, url);
        BindingCodes(empty).ShouldBe(ProcessStageBinding.Codes, ignoreOrder: true);
        empty.EnumerateArray().ShouldAllBe(row => row.GetProperty("pipelineStageId").ValueKind == JsonValueKind.Null);

        var (stage, stageName) = await StageAsync();
        var (other, _) = await StageAsync();
        var codes = ProcessStageBinding.Codes.ToArray();

        var bound = await PutAsync(client, url, new { bindings = new object[] { new { bindingCode = codes[0], pipelineStageId = stage }, new { bindingCode = codes[1], pipelineStageId = other } } });
        bound.Status.ShouldBe(HttpStatusCode.OK, bound.Raw);
        BindingCodes(bound.Json).ShouldBe(ProcessStageBinding.Codes, ignoreOrder: true);
        var row0 = BindingOf(bound.Json, codes[0]);
        row0.GetProperty("pipelineStageId").GetGuid().ShouldBe(stage);
        row0.GetProperty("pipelineStageName").GetString().ShouldBe(stageName);

        // codes[1] is left out: it keeps its stage. codes[0] goes back to none.
        var unbound = await PutAsync(client, url, new { bindings = new object[] { new { bindingCode = codes[0], pipelineStageId = (Guid?)null } } });
        unbound.Status.ShouldBe(HttpStatusCode.OK, unbound.Raw);
        BindingOf(unbound.Json, codes[0]).GetProperty("pipelineStageId").ValueKind.ShouldBe(JsonValueKind.Null);
        BindingOf(unbound.Json, codes[1]).GetProperty("pipelineStageId").GetGuid().ShouldBe(other);

        var rows = await AuditRowsAsync(nameof(ProcessStageBinding), null);
        rows.ShouldNotBeEmpty();
        rows.ShouldContain(row => row.UserId == manager.UserId && row.NewValues != null && row.NewValues.Contains(stage.ToString()));

        await ClearBindingsAsync();
    }

    [Fact]
    public async Task Bindings_refuse_the_whole_request_on_one_bad_item()
    {
        await ClearBindingsAsync();
        using var client = await ClientAsync(Permissions.ProcessesManage);
        var url = $"{Root}/settings/stage-bindings";
        var (stage, _) = await StageAsync();
        var codes = ProcessStageBinding.Codes.ToArray();

        var bodies = new[]
        {
            $$"""{"bindings":[{"bindingCode":"{{codes[0]}}","pipelineStageId":"{{stage}}"},{"bindingCode":"nomalum","pipelineStageId":"{{stage}}"}]}""",
            $$"""{"bindings":[{"bindingCode":"{{codes[0]}}","pipelineStageId":"{{stage}}"},{"bindingCode":"{{codes[1]}}","pipelineStageId":"{{Missing}}"}]}""",
            $$"""{"bindings":[{"bindingCode":"{{codes[0]}}","pipelineStageId":"{{stage}}"},{"bindingCode":"{{codes[0]}}","pipelineStageId":null}]}""",
            $$"""{"bindings":[{"bindingCode":"{{codes[0]}}","pipelineStageId":"{{stage}}"},{"pipelineStageId":"{{stage}}"}]}""",
            $$"""{"bindings":[{"bindingCode":"{{codes[0]}}","pipelineStageId":"{{stage}}"},null]}""",
            "{}",
            """{"bindings":null}""",
        };

        foreach (var body in bodies)
        {
            var reply = await SendAsync(client, "PUT", url, body);
            reply.Status.ShouldBe(HttpStatusCode.BadRequest, $"{body}: {reply.Raw}");
            reply.Title.ShouldBe(Kiritish, body);
        }

        (await GetAsync(client, url)).EnumerateArray().ShouldAllBe(row => row.GetProperty("pipelineStageId").ValueKind == JsonValueKind.Null);
    }

    // ────────────────────────────────────────────────────────────
    //  Holidays
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Holiday_is_added_changed_by_its_date_listed_by_year_and_deleted_with_audit()
    {
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesManage, Permissions.ProcessesView);
        using var client = ClientWith(manager.AccessToken);
        var later = new DateOnly(2040, 9, 1);
        var earlier = new DateOnly(2040, 3, 21);
        var otherYear = new DateOnly(2041, 1, 1);
        var url = $"{Root}/holidays";

        var added = await PostAsync(client, url, new { date = Iso(later), kind = "Holiday", title = "  Mustaqillik kuni  " });
        added.Status.ShouldBe(HttpStatusCode.OK, added.Raw);
        added.Json.GetProperty("date").GetString().ShouldBe(Iso(later));
        added.Json.GetProperty("kind").GetString().ShouldBe("Holiday");
        added.Json.GetProperty("title").GetString().ShouldBe("Mustaqillik kuni");

        (await PostAsync(client, url, new { date = Iso(earlier), kind = "WorkingDay", title = "Ishchi shanba" })).Status.ShouldBe(HttpStatusCode.OK);
        (await PostAsync(client, url, new { date = Iso(otherYear), kind = "Holiday", title = "Yangi yil" })).Status.ShouldBe(HttpStatusCode.OK);

        var changed = await PostAsync(client, url, new { date = Iso(later), kind = "WorkingDay", title = "Oʻzgardi" });
        changed.Status.ShouldBe(HttpStatusCode.OK, changed.Raw);
        changed.Json.GetProperty("kind").GetString().ShouldBe("WorkingDay");

        var year = await GetAsync(client, $"{url}?year=2040");
        year.EnumerateArray().Select(row => row.GetProperty("date").GetString()).ShouldBe([Iso(earlier), Iso(later)]);
        year[1].GetProperty("title").GetString().ShouldBe("Oʻzgardi");
        year[0].GetProperty("kind").GetString().ShouldBe("WorkingDay");

        (await DbAsync(db => db.ProcessHolidays.CountAsync(row => row.Date == later, Ct))).ShouldBe(1);
        var id = await DbAsync(db => db.ProcessHolidays.Where(row => row.Date == later).Select(row => row.Id).SingleAsync(Ct));

        var deleted = await SendAsync(client, "DELETE", $"{url}/{Iso(later)}", string.Empty);
        deleted.Status.ShouldBe(HttpStatusCode.NoContent, deleted.Raw);
        (await GetAsync(client, $"{url}?year=2040")).EnumerateArray().Select(row => row.GetProperty("date").GetString()).ShouldBe([Iso(earlier)]);

        var again = await SendAsync(client, "DELETE", $"{url}/{Iso(later)}", string.Empty);
        again.Status.ShouldBe(HttpStatusCode.NotFound, again.Raw);
        again.Title.ShouldBe(Topilmadi);

        var rows = await AuditRowsAsync(nameof(ProcessHoliday), id);
        rows.Select(row => row.Action).ShouldBe([AuditAction.Create, AuditAction.Update, AuditAction.Delete], ignoreOrder: true);
        rows.ShouldAllBe(row => row.UserId == manager.UserId);
    }

    [Fact]
    public async Task Holiday_validates_its_fields_and_writes_nothing()
    {
        using var client = await ClientAsync(Permissions.ProcessesManage, Permissions.ProcessesView);
        var url = $"{Root}/holidays";
        var date = new DateOnly(2042, 5, 5);
        var day = Iso(date);

        var bodies = new[]
        {
            $$"""{"kind":"Holiday","title":"Sana yo'q"}""",
            $$"""{"date":"{{day}}","title":"Tur yo'q"}""",
            $$"""{"date":"{{day}}","kind":"Holiday"}""",
            $$"""{"date":"{{day}}","kind":"Holiday","title":"   "}""",
            $$"""{"date":"{{day}}","kind":"Holiday","title":"{{new string('t', 201)}}"}""",
            $$"""{"date":"{{day}}","kind":99,"title":"Noma'lum tur"}""",
        };

        foreach (var body in bodies)
        {
            var reply = await PostRawAsync(client, url, body);
            reply.Status.ShouldBe(HttpStatusCode.BadRequest, $"{body}: {reply.Raw}");
            reply.Title.ShouldBe(Kiritish, body);
        }

        (await PostRawAsync(client, url, $$"""{"date":"{{day}}","kind":"Dam","title":"Noma'lum tur"}""")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await PostRawAsync(client, url, """{"date":"2042-02-31","kind":"Holiday","title":"Yo'q kun"}""")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(client, "DELETE", $"{url}/kecha", string.Empty)).Status.ShouldBe(HttpStatusCode.BadRequest);

        (await GetAsync(client, $"{url}?year=2042")).GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData("year=0", 400)]
    [InlineData("year=-1", 400)]
    [InlineData("year=10000", 400)]
    [InlineData("year=abc", 400)]
    [InlineData("year=1", 200)]
    [InlineData("year=9999", 200)]
    public async Task Holiday_year_boundaries(string query, int status)
    {
        using var client = await ClientAsync(Permissions.ProcessesView);

        var reply = await SendAsync(client, "GET", $"{Root}/holidays?{query}", string.Empty);

        ((int)reply.Status).ShouldBe(status, reply.Raw);
        if (status == 200)
        {
            reply.Json.ValueKind.ShouldBe(JsonValueKind.Array);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Reconcile
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reconcile_reports_finished_and_opens_the_new_case()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        using var client = await ClientAsync(Permissions.ProcessesManage);

        var reply = await PostRawAsync(client, $"{Root}/reconcile", string.Empty);

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        reply.Json.GetProperty("status").GetString().ShouldBe("Finished");
        reply.Json.GetProperty("started").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        reply.Json.GetProperty("changedRows").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        (await FindCaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Reconcile_reports_busy_and_dormant_and_does_nothing()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        using var client = await ClientAsync(Permissions.ProcessesManage);

        var busy = await WhileEngineLockedAsync(() => PostRawAsync(client, $"{Root}/reconcile", string.Empty));
        busy.Status.ShouldBe(HttpStatusCode.OK, busy.Raw);
        busy.Json.GetProperty("status").GetString().ShouldBe("Busy");
        (await FindCaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today))).ShouldBeNull();

        await SetStartedOnAsync(null);
        var dormant = await PostRawAsync(client, $"{Root}/reconcile", string.Empty);
        dormant.Status.ShouldBe(HttpStatusCode.OK, dormant.Raw);
        dormant.Json.GetProperty("status").GetString().ShouldBe("Dormant");
        (await FindCaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today))).ShouldBeNull();
        await StartEngineAsync();
    }

    // ────────────────────────────────────────────────────────────
    //  Data
    // ────────────────────────────────────────────────────────────

    /// <summary>A hire-formalise case with its order number, so r01-4 and r01-5 are closed by fact.</summary>
    private async Task<(Hire Hire, ProcessCase Case)> FormaliseAsync(bool withPinfl = true)
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today);

        if (withPinfl)
        {
            await DbAsync(async db =>
            {
                var employee = await db.Employees.SingleAsync(row => row.Id == hire.EmployeeId, Ct);
                employee.SetPinfl(RandomPinfl()).IsSuccess.ShouldBeTrue();
                return await db.SaveChangesAsync(Ct);
            });
        }

        await RunAsync();
        return (hire, await CaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId)));
    }

    /// <summary>A valid JSHSHIR: 13 digits and the one check digit that fits them, so it is never left to chance.</summary>
    private static Pinfl RandomPinfl()
    {
        var stem = "3150191" + Random.Shared.Next(100_000, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var check = Enumerable.Range(0, 10).First(digit => Pinfl.IsChecksumValid(stem + digit));
        return Pinfl.Create(stem + check).Value;
    }

    private async Task<ProcessCase> CandidateCaseAsync()
    {
        await StartEngineAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        await RunAsync();
        return await CaseAsync(ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, Today));
    }

    /// <summary>The council has decided: the offer step is Active on its own, by the same road the flow test takes.</summary>
    private async Task<ProcessCase> CandidateCaseAtOfferAsync(string decision)
    {
        var processCase = await CandidateCaseAsync();
        var by = await UserIdAsync();

        await CloseManuallyAsync(processCase.Id, "r02-5", by, payload: null);
        await CloseManuallyAsync(processCase.Id, "r02-6", by, payload: null);
        await CloseManuallyAsync(processCase.Id, "r02-9", by, payload: null);
        await CloseManuallyAsync(processCase.Id, "r02-10", by, $$"""{"decision":"{{decision}}"}""");
        await RunAsync();

        var after = await CaseByIdAsync(processCase.Id);
        after.FindStep(ProcessStepIds.Offer)!.State.ShouldBe(ProcessStepState.Active);
        return after;
    }

    private async Task<ProcessCase> ProbationCaseAsync()
    {
        await StartEngineAsync();
        var hire = await HireAsync("WR-" + Suffix(), Today);
        var probationId = await DbAsync(async db =>
        {
            var probation = Probation.Import(hire.EmploymentId, Today, Today.AddDays(90)).Value;
            db.Probations.Add(probation);
            await db.SaveChangesAsync(Ct);
            return probation.Id;
        });

        await RunAsync();
        return await CaseAsync(ProcessCodes.Probation, ProcessSubjectRefs.Probation(probationId));
    }

    private async Task<ProcessCase> LegacyProbationCaseAsync()
    {
        var processCase = await ProbationCaseAsync();
        await SqlAsync($"UPDATE process_cases SET legacy = TRUE WHERE id = {processCase.Id}");
        return await CaseByIdAsync(processCase.Id);
    }

    /// <summary>The closer is under test, not the close: r01-6 is closed directly, as that user.</summary>
    private Task<int> CloseStepAsAsync(Guid caseId, string stepCode, Guid by)
        => CloseManuallyAsync(caseId, stepCode, by, $$"""{"enteredOn":"{{Iso(Today)}}"}""");

    private object StartBody(Hire hire, DateOnly? lastWorkDay = null, string? basis = null) => new
    {
        processCode = ProcessCodes.Dismissal,
        employmentId = hire.EmploymentId,
        lastWorkDay = Iso(lastWorkDay ?? Today.AddDays(14)),
        basis,
    };

    private async Task<(Guid Id, string Name)> StageAsync()
    {
        var name = "Bosqich " + Suffix();
        var id = await DbAsync(async db =>
        {
            var stage = PipelineStage.Create(name).Value;
            db.PipelineStages.Add(stage);
            await db.SaveChangesAsync(Ct);
            return stage.Id;
        });

        return (id, name);
    }

    private Task<int> ClearBindingsAsync() => SqlAsync($"DELETE FROM process_stage_bindings");

    private async Task<string[]> InputOptionsAsync(Guid templateVersionId, string stepCode, string inputId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var definition = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProcessTemplateVersions
            .AsNoTracking()
            .Where(row => row.Id == templateVersionId)
            .Select(row => row.Definition)
            .SingleAsync(Ct);

        using var document = JsonDocument.Parse(definition);
        var step = document.RootElement.GetProperty("steps").EnumerateArray().Single(row => row.GetProperty("id").GetString() == stepCode);
        var input = step.GetProperty("manual").GetProperty("inputs").EnumerateArray().Single(row => row.GetProperty("id").GetString() == inputId);
        return [.. input.GetProperty("options").EnumerateArray().Select(row => row.GetProperty("value").GetString()!)];
    }

    private async Task<List<AuditLog>> AuditRowsAsync(string entityName, Guid? entityId)
    {
        var id = entityId?.ToString();
        return await DbAsync(db => db.AuditLogs.AsNoTracking()
            .Where(row => row.EntityName == entityName && (id == null || row.EntityId == id))
            .ToListAsync(Ct));
    }

    /// <summary>Runs <paramref name="work"/> while this scope holds the engine lock, as a pass would.</summary>
    private async Task<T> WhileEngineLockedAsync<T>(Func<Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var engineLock = scope.ServiceProvider.GetRequiredService<IProcessEngineLock>();

        return await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                (await engineLock.TryAcquireAsync(token)).ShouldBeTrue();
                return await work();
            },
            Ct);
    }

    // ────────────────────────────────────────────────────────────
    //  HTTP and JSON
    // ────────────────────────────────────────────────────────────

    private async Task<HttpClient> ClientAsync(params string[] permissions)
        => ClientWith(await TestAccounts.TokenAsync(_factory, permissions));

    private Task<HttpClient> ManagerAsync()
        => ClientAsync(Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Reply> SendAsync(HttpClient client, string method, string route, string body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(route, UriKind.Relative));
        if (body.Length > 0)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, Ct);
        return new Reply(response.StatusCode, await response.Content.ReadAsStringAsync(Ct), response.Headers.Location);
    }

    private static Task<Reply> PostRawAsync(HttpClient client, string url, string body) => SendAsync(client, "POST", url, body);

    private static Task<Reply> PostAsync(HttpClient client, string url, object body) => SendAsync(client, "POST", url, JsonSerializer.Serialize(body));

    private static Task<Reply> PutAsync(HttpClient client, string url, object body) => SendAsync(client, "PUT", url, JsonSerializer.Serialize(body));

    private static Task<Reply> DoneAsync(HttpClient client, Guid caseId, string step, object? inputs, int version)
        => PostAsync(client, $"{Root}/cases/{caseId}/steps/{step}/done", new { version, inputs });

    private static Task<Reply> UndoAsync(HttpClient client, Guid caseId, string step, int version)
        => PostAsync(client, $"{Root}/cases/{caseId}/steps/{step}/undo", new { version });

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        var reply = await SendAsync(client, "GET", url, string.Empty);
        reply.Status.ShouldBe(HttpStatusCode.OK, $"{url}: {reply.Raw}");
        return reply.Json;
    }

    private static Task<JsonElement> DetailAsync(HttpClient client, Guid caseId) => GetAsync(client, $"{Root}/cases/{caseId}");

    private static int Version(JsonElement detail) => detail.GetProperty("version").GetInt32();

    private static JsonElement StepOf(JsonElement detail, string stepCode)
        => detail.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("stepCode").GetString() == stepCode);

    private static string[] BindingCodes(JsonElement list)
        => [.. list.EnumerateArray().Select(row => row.GetProperty("bindingCode").GetString()!)];

    private static JsonElement BindingOf(JsonElement list, string code)
        => list.EnumerateArray().Single(row => row.GetProperty("bindingCode").GetString() == code);

    /// <summary>Adds the version to a raw body that has none.</summary>
    private static string WithVersion(string body, int version)
        => body == "{}" ? $$"""{"version":{{version}}}""" : $$"""{"version":{{version}},{{body[1..]}}""";

    private static string Sha256(string canonical) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The same claims under another expiry, signed with the key of the test host.</summary>
    private static string Resigned(string token, DateTime expires) => Sign(new JsonWebToken(token), token, expires, _ => true);

    /// <summary>A valid token with its permissions and no user id: what <c>ICurrentUser.UserId == null</c> looks like.</summary>
    private static string WithoutUserId(string token)
        => Sign(new JsonWebToken(token), token, DateTime.UtcNow.AddHours(1), claim => claim.Type is not (JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier or "nameid"));

    private static string Sign(JsonWebToken read, string token, DateTime expires, Func<Claim, bool> keep)
    {
        _ = token;
        var claims = read.Claims.Where(claim => claim.Type is not ("exp" or "iat" or "nbf" or "iss" or "aud")).Where(keep);

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

    private Task<Guid> VacancyAsync() => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Engine vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(Ct);
        return vacancy.Id;
    });

    private async Task<Guid> CandidateAsync(Guid vacancyId)
    {
        await StartEngineAsync();

        return await DbAsync(async db =>
        {
            var candidate = Candidate.Register(FullName.Create("Motorov" + Suffix(), "Test", null).Value, vacancyId).Value;
            db.Candidates.Add(candidate);
            await db.SaveChangesAsync(Ct);
            return candidate.Id;
        });
    }

    private Task<Hire> HireAsync(string? orderNo, DateOnly hireDate) => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Engine MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var name = FullName.Create("Xodimov" + Suffix(), "Test", null).Value;
        var employee = Employee.Create(name).Value;
        db.Employees.Add(employee);

        var employment = Employment.Hire(employee.Id, legalEntity.Id, hireDate, departmentId: null, positionId: null, orderNo: orderNo, orderDate: null).Value;
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

    private sealed record Hire(Guid EmployeeId, Guid EmploymentId, string FullName);

    private sealed record Reply(HttpStatusCode Status, string Raw, Uri? Location)
    {
        public JsonElement Json
        {
            get
            {
                using var document = JsonDocument.Parse(Raw);
                return document.RootElement.Clone();
            }
        }

        public string? Title => Raw.Length > 0 && Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty("title", out var title) ? title.GetString() : null;
    }
}
