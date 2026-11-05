using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Employees.Probations;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Application.Processes.Gates;
using Bonvi.Application.Recruiting.Candidates;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The hook gates and their overrides over HTTP against real PostgreSQL (docs/SPEC-mening-ishlarim.md §8, §10 rows 14–15, §14).
/// </summary>
/// <remarks>
/// The gate settings live in the database the class's tests share, so every test states its own mode first, and
/// asserts only on its own subjects.
/// </remarks>
public sealed class ProcessGateTests : IClassFixture<BonviApiFactory>
{
    private const string AnketaYoq = "jarayon.anketa_yoq";
    private const string KengashYoq = "jarayon.kengash_qarori_yoq";
    private const string SurishtiruvYoq = "jarayon.surishtiruv_yoq";
    private const string Nomzodsiz = "jarayon.nomzodsiz_qabul";
    private const string BuyruqYoq = "jarayon.buyruq_raqami_yoq";
    private const string AktYoq = "jarayon.akt_yopilmagan";
    private const string SuhbatYoq = "jarayon.chiqish_suhbati_yoq";
    private const string OxirgiKunda = "jarayon.istisno_oxirgi_kunda";
    private const string Yaroqsiz = "jarayon.istisno_yaroqsiz";
    private const string RuxsatYoq = "jarayon.ruxsat_yoq";
    private const string KiritishNotogri = "jarayon.kiritish_notogri";

    private const string Reason = "Rahbar yozma ruxsat berdi";
    private const string F09Dismissal = """{"kind":{"value":"dismissal"}}""";
    private const string F09Transfer = """{"kind":{"value":"transfer"}}""";
    private const string F08Coded = """{"leaveDate":"2026-12-31","mainReason":{"value":"k03"}}""";
    private const string F08Uncoded = """{"leaveDate":"2026-12-31"}""";

    private static readonly string[] F08Signers = ["hr3Status", "signEmployee", "signHr4"];
    private static readonly string[] F09Signers = ["handedOver", "received", "signHead", "hr3Control", "signHr1", "hr3Clearance"];

    private readonly BonviApiFactory _factory;

    public ProcessGateTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => _factory.Services.GetRequiredService<IClock>().Today;

    // ────────────────────────────────────────────────────────────
    //  Q01 — hire of a candidate
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Q01_candidate_with_nothing_stops_on_the_form_and_lists_the_council_decision()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeBlocked(AnketaYoq, KengashYoq);
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Q01_form_without_a_council_decision_answers_the_council_code_alone()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeBlocked(KengashYoq);
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Q01_council_still_in_draft_is_not_a_decision()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await CouncilAsync(candidate, "accept", approved: false);
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(KengashYoq);
    }

    [Theory]
    [InlineData("reject")]
    [InlineData("reserve")]
    public async Task Q01_decision_other_than_accept_or_conditional_answers_the_council_code(string decision)
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await CouncilAsync(candidate, decision);
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(KengashYoq);
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Q01_conditional_decision_without_a_background_check_answers_the_check_code()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await CouncilAsync(candidate, "conditional");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(SurishtiruvYoq);
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Q01_conditional_decision_with_a_not_recommended_check_answers_the_check_code()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await CouncilAsync(candidate, "conditional");
        await BackgroundCheckAsync(candidate, "notRecommended");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(SurishtiruvYoq);
    }

    [Fact]
    public async Task Q01_form_and_accept_lets_the_hire_through_with_no_warnings()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await CouncilAsync(candidate, "accept");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        reply.Json.GetProperty("employment").GetProperty("employeeId").GetGuid().ShouldBe(target.EmployeeId);
        (await EmploymentCountAsync(target)).ShouldBe(1);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("watch")]
    public async Task Q01_conditional_decision_with_a_passed_check_lets_the_hire_through(string outcome)
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await CouncilAsync(candidate, "conditional");
        await BackgroundCheckAsync(candidate, outcome);
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        (await EmploymentCountAsync(target)).ShouldBe(1);
    }

    [Fact]
    public async Task Q01_manual_council_decision_on_the_case_counts_as_an_accept()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await RunAsync();
        var processCase = await CandidateCaseAsync(candidate);
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(KengashYoq);

        await CloseManuallyAsync(processCase.Id, "r02-10", """{"decision":"accept"}""");

        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        (await EmploymentCountAsync(target)).ShouldBe(1);
    }

    [Fact]
    public async Task Q01_manual_council_reject_on_the_case_still_blocks()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await RunAsync();
        await CloseManuallyAsync((await CandidateCaseAsync(candidate)).Id, "r02-10", """{"decision":"reject"}""");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(KengashYoq);
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Q01_manual_conditional_needs_the_manual_check_as_well()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await FormAsync(candidate);
        await RunAsync();
        var processCase = await CandidateCaseAsync(candidate);
        await CloseManuallyAsync(processCase.Id, "r02-10", """{"decision":"conditional"}""");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(SurishtiruvYoq);

        await CloseManuallyAsync(processCase.Id, "r02-8", """{"outcome":"watch"}""");

        (await HireAsync(hr, target, candidate)).ShouldBeOk();
    }

    /// <summary>§8.2: the gate reads the facts at call time; the case's steps have not been reconciled since the council.</summary>
    [Fact]
    public async Task Q01_reads_the_facts_at_call_time_not_the_reconciled_steps()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await RunAsync();
        var before = await CandidateCaseAsync(candidate);
        before.FindStep("r02-10")!.IsClosed.ShouldBeFalse();

        await FormAsync(candidate);
        await CouncilAsync(candidate, "accept");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeOk();
    }

    [Fact]
    public async Task Q01_applies_on_the_candidate_route_and_leaves_the_candidate_unhired()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        var target = await HireTargetAsync();
        var position = await PositionAsync();
        using var hr = await ClientAsync(Permissions.RecruitingView, Permissions.RecruitingManage, Permissions.EmploymentHire, Permissions.EmployeesCreate);

        var reply = await PostAsync(
            hr,
            $"/api/recruiting/candidates/{candidate}/hire",
            new { legalEntityId = target.LegalEntityId, positionId = position, employeeId = target.EmployeeId, orderNo = "T-1" });

        reply.ShouldBeBlocked(AnketaYoq, KengashYoq);
        (await EmploymentCountAsync(target)).ShouldBe(0);
        var row = await DbAsync(db => db.Candidates.AsNoTracking().SingleAsync(item => item.Id == candidate, Ct));
        row.Status.ShouldNotBe(CandidateStatus.Hired);
    }

    // ────────────────────────────────────────────────────────────
    //  Q01 — direct hire
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Direct_hire_without_a_candidate_is_blocked()
    {
        await EnforceAsync();
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target)).ShouldBeBlocked(Nomzodsiz);
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Direct_hire_of_a_new_person_is_blocked_and_no_card_is_left_behind()
    {
        await EnforceAsync();
        var legalEntity = await LegalEntityAsync();
        var lastName = "Yangiyev" + Suffix();
        using var hr = await ClientAsync(Permissions.EmploymentHire, Permissions.EmployeesCreate);

        var reply = await PostAsync(hr, "/api/employments/hire", NewPersonBody(legalEntity, lastName));

        reply.ShouldBeBlocked(Nomzodsiz);
        (await DbAsync(db => db.Employments.AsNoTracking().CountAsync(row => row.LegalEntityId == legalEntity, Ct))).ShouldBe(0);
        (await DbAsync(db => db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM employees WHERE last_name = {lastName}")
            .SingleAsync(Ct))).ShouldBe(0);
    }

    [Fact]
    public async Task Direct_hire_of_someone_active_in_another_legal_entity_is_exempt_and_spends_no_override()
    {
        await EnforceAsync();
        var existing = await EmployedAsync();
        var second = new HireTarget(existing.EmployeeId, await LegalEntityAsync());
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var hr = Client(account);
        using var grantor = await GrantorAsync();
        var granted = await GrantDirectHireAsync(grantor, account.UserId);

        var reply = await HireAsync(hr, second);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        (await EmploymentCountAsync(second)).ShouldBe(1);
        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Direct_hire_exemption_needs_an_active_employment_not_a_closed_one()
    {
        await EnforceAsync();
        var left = await EmployedAsync();
        await TerminateInDbAsync(left.EmploymentId, Today);
        var second = new HireTarget(left.EmployeeId, await LegalEntityAsync());
        using var hr = await HrAsync();

        (await HireAsync(hr, second)).ShouldBeBlocked(Nomzodsiz);
        (await EmploymentCountAsync(second)).ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Missing_order_number_warns_and_the_hire_goes_through(string? orderNo)
    {
        await EnforceAsync();
        var existing = await EmployedAsync();
        var second = new HireTarget(existing.EmployeeId, await LegalEntityAsync());
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, second, orderNo: orderNo);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBe([BuyruqYoq]);
        (await EmploymentCountAsync(second)).ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Q12, Q13 — terminate
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Terminate_with_neither_blank_stops_on_the_act_and_lists_the_exit_interview()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        using var hr = await TerminatorAsync();

        var reply = await TerminateAsync(hr, employed);

        reply.ShouldBeBlocked(AktYoq, SuhbatYoq);
        (await EmploymentAsync(employed)).Status.ShouldNotBe(EmploymentStatus.Terminated);
    }

    [Fact]
    public async Task Q12_blocks_while_the_act_is_not_approved_even_with_confirm()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        await ExitInterviewAsync(employed);
        await ActAsync(employed, approved: false);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(AktYoq);
        (await TerminateAsync(hr, employed, confirm: true)).ShouldBeBlocked(AktYoq);
        (await PostAsync(hr, $"/api/employments/{employed.EmploymentId}/terminate", new { confirm = true, terminationDate = Today }))
            .ShouldBeBlocked(AktYoq);

        (await EmploymentAsync(employed)).Status.ShouldNotBe(EmploymentStatus.Terminated);
    }

    [Fact]
    public async Task Q13_blocks_without_the_exit_interview_even_with_confirm()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        await ActAsync(employed, approved: true);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(SuhbatYoq);
        (await TerminateAsync(hr, employed, confirm: true)).ShouldBeBlocked(SuhbatYoq);

        (await EmploymentAsync(employed)).Status.ShouldNotBe(EmploymentStatus.Terminated);
    }

    /// <summary>The one place <c>confirm</c> does something: a flagged employee. It answers the flag, not the gates.</summary>
    [Fact]
    public async Task Confirming_a_flag_does_not_get_past_the_gates()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        await DbAsync(async db =>
        {
            db.EmployeeFlags.Add(EmployeeFlag.Raise(employed.EmployeeId, EmployeeFlagType.Monitoring, Today, "kuzatuvda").Value);
            return await db.SaveChangesAsync(Ct);
        });
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).Title.ShouldBe("xodim.nazoratda");
        (await TerminateAsync(hr, employed, confirm: true)).ShouldBeBlocked(AktYoq, SuhbatYoq);

        (await EmploymentAsync(employed)).Status.ShouldNotBe(EmploymentStatus.Terminated);
    }

    [Fact]
    public async Task Q12_a_transfer_act_or_a_cancelled_act_does_not_count()
    {
        await EnforceAsync();
        var transfer = await EmployedAsync();
        await ExitInterviewAsync(transfer);
        await BlankAsync(ProcessBlanks.F09, transfer, F09Transfer, F09Signers);
        var cancelled = await EmployedAsync();
        await ExitInterviewAsync(cancelled);
        await BlankAsync(ProcessBlanks.F09, cancelled, F09Dismissal, sign: [], cancel: true);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, transfer)).ShouldBeBlocked(AktYoq);
        (await TerminateAsync(hr, cancelled)).ShouldBeBlocked(AktYoq);
    }

    [Fact]
    public async Task Q13_an_interview_without_the_main_k_code_does_not_count_and_one_with_it_does()
    {
        await EnforceAsync();
        var uncoded = await EmployedAsync();
        await ActAsync(uncoded, approved: true);
        await BlankAsync(ProcessBlanks.F08, uncoded, F08Uncoded, sign: []);
        var coded = await EmployedAsync();
        await ActAsync(coded, approved: true);
        await BlankAsync(ProcessBlanks.F08, coded, F08Coded, sign: []);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, uncoded)).ShouldBeBlocked(SuhbatYoq);
        (await TerminateAsync(hr, coded)).ShouldBeOk();
    }

    [Fact]
    public async Task Terminate_with_both_blanks_goes_through_with_no_gate_warnings()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        await ExitInterviewAsync(employed);
        await ActAsync(employed, approved: true);
        using var hr = await TerminatorAsync();

        var reply = await TerminateAsync(hr, employed);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        var row = await EmploymentAsync(employed);
        row.Status.ShouldBe(EmploymentStatus.Terminated);
        row.TerminationDate.ShouldBe(Today);
    }

    [Fact]
    public async Task Gates_of_one_employment_read_only_its_own_blanks()
    {
        await EnforceAsync();
        var neighbour = await EmployedAsync();
        await ExitInterviewAsync(neighbour);
        await ActAsync(neighbour, approved: true);
        var employed = await EmployedAsync();
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(AktYoq, SuhbatYoq);
    }

    // ────────────────────────────────────────────────────────────
    //  Modes: warn, off, dormant, legacy
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Warn_mode_turns_the_hire_blocks_into_warnings_and_spends_no_override()
    {
        await EnforceAsync();
        await ModeAsync(SystemSettingKeys.ProcessesGatesEnforce);
        var candidate = await CandidateAsync();
        await RunAsync();
        using var grantor = await GrantorAsync();
        var granted = await GrantCaseAsync(grantor, (await CandidateCaseAsync(candidate)).Id, ProcessGateCodes.Q01);
        await ModeAsync("warn");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target, candidate, orderNo: null);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBe([BuyruqYoq, AnketaYoq, KengashYoq], ignoreOrder: true);
        (await EmploymentCountAsync(target)).ShouldBe(1);
        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Warn_mode_turns_the_direct_hire_block_into_a_warning()
    {
        await EnforceAsync();
        await ModeAsync("warn");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBe([Nomzodsiz]);
    }

    [Fact]
    public async Task Warn_mode_turns_the_terminate_blocks_into_warnings()
    {
        await EnforceAsync();
        await ModeAsync("warn");
        var employed = await EmployedAsync();
        using var hr = await TerminatorAsync();

        var reply = await TerminateAsync(hr, employed);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBe([AktYoq, SuhbatYoq], ignoreOrder: true);
        (await EmploymentAsync(employed)).Status.ShouldBe(EmploymentStatus.Terminated);
    }

    [Fact]
    public async Task Off_mode_allows_everything_without_even_a_warning()
    {
        await EnforceAsync();
        await ModeAsync(SystemSettingKeys.ProcessesGatesOff);
        var target = await HireTargetAsync();
        var employed = await EmployedAsync();
        using var hr = await ClientAsync(Permissions.EmploymentHire, Permissions.EmploymentTerminate);

        var hire = await HireAsync(hr, target, orderNo: null);
        var terminate = await TerminateAsync(hr, employed);

        hire.ShouldBeOk();
        hire.ResultWarnings.ShouldBeEmpty();
        terminate.ShouldBeOk();
        terminate.ResultWarnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-date")]
    public async Task Dormant_engine_allows_everything_without_even_a_warning(string startedOn)
    {
        await EnforceAsync();
        await SqlAsync($"UPDATE system_settings SET value = {startedOn} WHERE key = {SystemSettingKeys.ProcessesStartedOn}");
        var target = await HireTargetAsync();
        var candidateTarget = await HireTargetAsync();
        var candidate = await CandidateAsync();
        var employed = await EmployedAsync();
        using var hr = await ClientAsync(Permissions.EmploymentHire, Permissions.EmploymentTerminate);

        var direct = await HireAsync(hr, target, orderNo: null);
        var ofCandidate = await HireAsync(hr, candidateTarget, candidate, orderNo: null);
        var terminate = await TerminateAsync(hr, employed);

        direct.ShouldBeOk();
        direct.ResultWarnings.ShouldBeEmpty();
        ofCandidate.ShouldBeOk();
        ofCandidate.ResultWarnings.ShouldBeEmpty();
        terminate.ShouldBeOk();
        terminate.ResultWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dormant_with_no_setting_row_at_all_allows_a_direct_hire()
    {
        await EnforceAsync();
        await SqlAsync($"DELETE FROM system_settings WHERE key = {SystemSettingKeys.ProcessesStartedOn}");
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Candidate_who_applied_before_the_engine_started_only_warns()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        await SetStartedOnAsync(Today.AddDays(1));
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBe([AnketaYoq, KengashYoq], ignoreOrder: true);
        (await EmploymentCountAsync(target)).ShouldBe(1);
    }

    [Fact]
    public async Task Direct_hire_is_never_legacy()
    {
        await EnforceAsync();
        await SetStartedOnAsync(Today.AddDays(1));
        var target = await HireTargetAsync();
        using var hr = await HrAsync();

        (await HireAsync(hr, target)).ShouldBeBlocked(Nomzodsiz);
    }

    [Fact]
    public async Task Legacy_dismissal_case_only_warns_and_spends_no_override()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        var old = await BlankAsync(ProcessBlanks.F09, employed, F09Dismissal, sign: []);
        await SqlAsync($"UPDATE blank_submissions SET created_at = {DateTimeOffset.UtcNow.AddDays(-30)} WHERE id = {old}");
        await BlankAsync(ProcessBlanks.F08, employed, F08Uncoded, sign: []);
        await RunAsync();
        var processCase = await DismissalCaseAsync(employed);
        processCase.Legacy.ShouldBeTrue();
        using var grantor = await GrantorAsync();
        var granted = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var hr = await TerminatorAsync();

        var reply = await TerminateAsync(hr, employed);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBe([AktYoq, SuhbatYoq], ignoreOrder: true);
        (await EmploymentAsync(employed)).Status.ShouldBe(EmploymentStatus.Terminated);
        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Dismissal_case_started_after_the_engine_is_not_legacy_and_blocks()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        await ActAsync(employed, approved: false);
        await RunAsync();
        (await DismissalCaseAsync(employed)).Legacy.ShouldBeFalse();
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(AktYoq, SuhbatYoq);
    }

    /// <summary>
    /// §8.2 says no case → enforced; an old blank alone starts no case (§3.2), so it does not make the dismissal
    /// legacy until a newer start fact arrives. Pinned, see the report to the owner.
    /// </summary>
    [Fact]
    public async Task An_old_blank_with_no_case_yet_is_still_enforced()
    {
        await EnforceAsync();
        var employed = await EmployedAsync();
        var old = await BlankAsync(ProcessBlanks.F09, employed, F09Dismissal, sign: []);
        await SqlAsync($"UPDATE blank_submissions SET created_at = {DateTimeOffset.UtcNow.AddDays(-30)} WHERE id = {old}");
        await RunAsync();
        (await FindDismissalCaseAsync(employed)).ShouldBeNull();
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(AktYoq, SuhbatYoq);
    }

    // ────────────────────────────────────────────────────────────
    //  Overrides — direct hire
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Direct_hire_override_answers_the_row_and_works_exactly_once()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        var grantorAccount = await AccountAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage);
        using var grantor = Client(grantorAccount);
        using var hr = Client(account);

        var grant = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = account.UserId, reason = Reason });

        grant.ShouldBeOk();
        grant.Json.GetProperty("gateCode").GetString().ShouldBe(ProcessGateCodes.Q01);
        grant.Json.GetProperty("forUserId").GetGuid().ShouldBe(account.UserId);
        grant.Json.GetProperty("grantedBy").GetGuid().ShouldBe(grantorAccount.UserId);
        grant.Json.GetProperty("reason").GetString().ShouldBe(Reason);
        grant.Json.GetProperty("caseId").ValueKind.ShouldBe(JsonValueKind.Null);
        grant.Json.GetProperty("consumedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        (grant.Json.GetProperty("expiresAt").GetDateTimeOffset() - grant.Json.GetProperty("grantedAt").GetDateTimeOffset())
            .ShouldBe(TimeSpan.FromHours(24));

        var first = await HireTargetAsync();
        var second = await HireTargetAsync();

        (await HireAsync(hr, first)).ShouldBeOk();
        var spent = await OverrideAsync(grant.Json.GetProperty("id").GetGuid());
        spent.ConsumedAt.ShouldNotBeNull();
        spent.ConsumedOperation.ShouldBe(ProcessGateOperations.Hire);

        (await HireAsync(hr, second)).ShouldBeBlocked(Nomzodsiz);
        (await EmploymentCountAsync(first)).ShouldBe(1);
        (await EmploymentCountAsync(second)).ShouldBe(0);
    }

    [Fact]
    public async Task Direct_hire_override_covers_only_the_operator_it_names()
    {
        await EnforceAsync();
        var named = await AccountAsync(Permissions.EmploymentHire);
        using var other = await HrAsync();
        using var grantor = await GrantorAsync();
        var granted = await GrantDirectHireAsync(grantor, named.UserId);
        var target = await HireTargetAsync();

        (await HireAsync(other, target)).ShouldBeBlocked(Nomzodsiz);

        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Direct_hire_override_does_not_cover_a_candidate_hire()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var hr = Client(account);
        using var grantor = await GrantorAsync();
        var granted = await GrantDirectHireAsync(grantor, account.UserId);
        var candidate = await CandidateAsync();
        var target = await HireTargetAsync();

        (await HireAsync(hr, target, candidate)).ShouldBeBlocked(AnketaYoq, KengashYoq);

        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Override_past_24_hours_no_longer_works_and_stays_unconsumed()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var hr = Client(account);
        using var grantor = await GrantorAsync();
        var granted = await GrantDirectHireAsync(grantor, account.UserId);
        await SqlAsync($"UPDATE process_gate_overrides SET expires_at = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE id = {granted}");
        var target = await HireTargetAsync();

        (await HireAsync(hr, target)).ShouldBeBlocked(Nomzodsiz);

        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();
        (await EmploymentCountAsync(target)).ShouldBe(0);
    }

    [Fact]
    public async Task Override_still_inside_its_24_hours_works()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var hr = Client(account);
        using var grantor = await GrantorAsync();
        var granted = await GrantDirectHireAsync(grantor, account.UserId);
        await SqlAsync($"UPDATE process_gate_overrides SET granted_at = {DateTimeOffset.UtcNow.AddHours(-23)}, expires_at = {DateTimeOffset.UtcNow.AddHours(1)} WHERE id = {granted}");
        var target = await HireTargetAsync();

        (await HireAsync(hr, target)).ShouldBeOk();

        (await OverrideAsync(granted)).ConsumedOperation.ShouldBe(ProcessGateOperations.Hire);
    }

    // ────────────────────────────────────────────────────────────
    //  Overrides — a candidate's case
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Case_override_lifts_q01_for_its_candidate_only_and_is_spent_on_the_hire()
    {
        await EnforceAsync();
        var candidate = await CandidateAsync();
        var neighbour = await CandidateAsync();
        await RunAsync();
        var processCase = await CandidateCaseAsync(candidate);
        using var grantor = await GrantorAsync();
        var grant = await PostAsync(grantor, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q01}/override", new { reason = Reason });

        grant.ShouldBeOk();
        grant.Json.GetProperty("caseId").GetGuid().ShouldBe(processCase.Id);
        grant.Json.GetProperty("candidateId").GetGuid().ShouldBe(candidate);
        grant.Json.GetProperty("stepCode").GetString().ShouldBe(ProcessStepIds.FirstDay);
        var granted = grant.Json.GetProperty("id").GetGuid();

        using var hr = await HrAsync();

        (await HireAsync(hr, await HireTargetAsync(), neighbour)).ShouldBeBlocked(AnketaYoq, KengashYoq);
        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull();

        var target = await HireTargetAsync();
        var reply = await HireAsync(hr, target, candidate);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        (await EmploymentCountAsync(target)).ShouldBe(1);
        (await OverrideAsync(granted)).ConsumedOperation.ShouldBe(ProcessGateOperations.Hire);

        // Spent: the same candidate's next hire meets the gate again.
        (await HireAsync(hr, await HireTargetAsync(), candidate)).ShouldBeBlocked(AnketaYoq, KengashYoq);
    }

    // ────────────────────────────────────────────────────────────
    //  Overrides — a dismissal's case
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Q12_and_q13_overrides_together_let_a_termination_through_and_both_are_spent()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        var q13 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q13);
        using var hr = await TerminatorAsync();

        var reply = await TerminateAsync(hr, employed);

        reply.ShouldBeOk();
        reply.ResultWarnings.ShouldBeEmpty();
        (await EmploymentAsync(employed)).Status.ShouldBe(EmploymentStatus.Terminated);

        var spentQ12 = await OverrideAsync(q12);
        spentQ12.ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
        spentQ12.ConsumedAt.ShouldNotBeNull();
        spentQ12.StepCode.ShouldBe("ex-8");
        spentQ12.EmploymentId.ShouldBe(employed.EmploymentId);
        (await OverrideAsync(q13)).ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
    }

    [Fact]
    public async Task Q12_override_before_the_last_day_answers_its_own_code_and_works_on_the_day()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await ExitInterviewAsync(employed);
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed, date: Today.AddDays(3))).ShouldBeBlocked(OxirgiKunda);

        (await OverrideAsync(q12)).ConsumedAt.ShouldBeNull();
        (await EmploymentAsync(employed)).Status.ShouldNotBe(EmploymentStatus.Terminated);

        (await TerminateAsync(hr, employed, date: Today)).ShouldBeOk();
        (await OverrideAsync(q12)).ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
    }

    [Fact]
    public async Task Q12_override_with_a_termination_date_in_the_past_works()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await ExitInterviewAsync(employed);
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed, date: Today.AddDays(-1))).ShouldBeOk();

        (await OverrideAsync(q12)).ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
    }

    [Fact]
    public async Task Q12_override_early_with_no_q13_cover_lists_both_and_spends_neither()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed, date: Today.AddDays(3))).ShouldBeBlocked(OxirgiKunda, SuhbatYoq);

        (await OverrideAsync(q12)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Q13_override_is_not_tied_to_the_last_day()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await SignF09Async(employed);
        using var grantor = await GrantorAsync();
        var q13 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q13);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed, date: Today.AddDays(3))).ShouldBeOk();

        (await OverrideAsync(q13)).ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
    }

    [Fact]
    public async Task Terminate_stopped_by_q13_does_not_spend_the_q12_override()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(SuhbatYoq);

        (await OverrideAsync(q12)).ConsumedAt.ShouldBeNull();
        (await EmploymentAsync(employed)).Status.ShouldNotBe(EmploymentStatus.Terminated);

        // The saved override still does its job once the interview exists.
        await ExitInterviewAsync(employed);
        (await TerminateAsync(hr, employed)).ShouldBeOk();
        (await OverrideAsync(q12)).ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
    }

    [Fact]
    public async Task Terminate_that_fails_after_the_gate_leaves_the_override_unspent()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await ExitInterviewAsync(employed);
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var hr = await TerminatorAsync();

        var reply = await TerminateAsync(hr, employed, date: Today.AddYears(-5));

        reply.Status.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        reply.Title.ShouldBe("mehnat.ketish_sanasi");
        (await OverrideAsync(q12)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Override_of_one_employment_does_not_cover_another()
    {
        await EnforceAsync();
        var (_, processCase) = await DismissalAsync();
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        var q13 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q13);
        var other = await EmployedAsync();
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, other)).ShouldBeBlocked(AktYoq, SuhbatYoq);

        (await OverrideAsync(q12)).ConsumedAt.ShouldBeNull();
        (await OverrideAsync(q13)).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Q13_override_does_not_cover_q12()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        using var grantor = await GrantorAsync();
        var q13 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q13);
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(AktYoq);

        (await OverrideAsync(q13)).ConsumedAt.ShouldBeNull();
    }

    /// <summary>§8.1: the engine part of Q12 is ex-10's; its override is no exception for the Terminate hook.</summary>
    [Fact]
    public async Task Override_on_the_final_settlement_step_is_ignored_by_the_terminate_hook()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await ExitInterviewAsync(employed);
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        await SqlAsync($"UPDATE process_gate_overrides SET step_code = {ProcessStepIds.FinalSettlement} WHERE id = {q12}");
        using var hr = await TerminatorAsync();

        (await TerminateAsync(hr, employed)).ShouldBeBlocked(AktYoq);

        (await OverrideAsync(q12)).ConsumedAt.ShouldBeNull();
    }

    // ────────────────────────────────────────────────────────────
    //  Overrides — the engine's ex-10
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Override_on_the_blocked_settlement_unblocks_it_and_the_pass_spends_it_when_the_step_closes()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await TerminateInDbAsync(employed.EmploymentId, Today);
        await RunAsync();
        (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.FinalSettlement)!.BlockedGateCode.ShouldBe(ProcessGateCodes.Q12);

        using var grantor = await GrantorAsync();
        var grant = await PostAsync(grantor, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });
        grant.ShouldBeOk();
        grant.Json.GetProperty("stepCode").GetString().ShouldBe(ProcessStepIds.FinalSettlement);
        var granted = grant.Json.GetProperty("id").GetGuid();

        await RunAsync();

        var lifted = (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.FinalSettlement)!;
        lifted.State.ShouldBe(ProcessStepState.Active);
        lifted.BlockedGateCode.ShouldBeNull();
        (await OverrideAsync(granted)).ConsumedAt.ShouldBeNull("the step is still open");

        await CloseOpenStepAsync(processCase.Id, ProcessStepIds.FinalSettlement, """{"paidOn":"2026-11-05"}""");
        await RunAsync();

        var spent = await OverrideAsync(granted);
        spent.ConsumedOperation.ShouldBe(ProcessGateOperations.Step);
        spent.ConsumedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Settlement_goes_back_to_blocked_when_its_override_expires_unused()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await TerminateInDbAsync(employed.EmploymentId, Today);
        await RunAsync();
        using var grantor = await GrantorAsync();
        var granted = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        await RunAsync();
        (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.FinalSettlement)!.State.ShouldBe(ProcessStepState.Active);

        await SqlAsync($"UPDATE process_gate_overrides SET expires_at = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE id = {granted}");
        await RunAsync();

        var step = (await CaseByIdAsync(processCase.Id)).FindStep(ProcessStepIds.FinalSettlement)!;
        step.State.ShouldBe(ProcessStepState.Blocked);
        step.BlockedGateCode.ShouldBe(ProcessGateCodes.Q12);
    }

    // ────────────────────────────────────────────────────────────
    //  Races: 409, never 500
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_direct_hires_racing_on_one_override_get_one_success_and_one_409()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var grantor = await GrantorAsync();
        var granted = await GrantDirectHireAsync(grantor, account.UserId);
        var first = await HireTargetAsync();
        var second = await HireTargetAsync();
        using var one = Client(account);
        using var two = Client(account);

        var replies = await RaceOnOverrideAsync(granted, () => HireAsync(one, first), () => HireAsync(two, second));

        ShouldBeOneWinner(replies);
        ((await EmploymentCountAsync(first)) + (await EmploymentCountAsync(second))).ShouldBe(1, "the loser wrote nothing");
        (await OverrideAsync(granted)).ConsumedOperation.ShouldBe(ProcessGateOperations.Hire);
    }

    [Fact]
    public async Task Two_terminations_racing_on_one_override_get_one_success_and_one_409()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await ExitInterviewAsync(employed);
        using var grantor = await GrantorAsync();
        var q12 = await GrantCaseAsync(grantor, processCase.Id, ProcessGateCodes.Q12);
        using var one = await TerminatorAsync();
        using var two = await TerminatorAsync();

        var replies = await RaceOnOverrideAsync(q12, () => TerminateAsync(one, employed), () => TerminateAsync(two, employed));

        ShouldBeOneWinner(replies);
        (await EmploymentAsync(employed)).Status.ShouldBe(EmploymentStatus.Terminated);
        (await OverrideAsync(q12)).ConsumedOperation.ShouldBe(ProcessGateOperations.Terminate);
    }

    // ────────────────────────────────────────────────────────────
    //  Audit (§14)
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Granting_and_consuming_an_override_are_both_audited_with_who_and_why()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        var grantorAccount = await AccountAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage);
        using var grantor = Client(grantorAccount);
        using var hr = Client(account);

        var granted = await GrantDirectHireAsync(grantor, account.UserId);
        (await HireAsync(hr, await HireTargetAsync())).ShouldBeOk();

        var rows = await DbAsync(db => db.AuditLogs.AsNoTracking()
            .Where(row => row.EntityName == nameof(ProcessGateOverride) && row.EntityId == granted.ToString())
            .OrderBy(row => row.OccurredAt)
            .ToListAsync(Ct));

        rows.Count.ShouldBe(2);
        rows[0].Action.ShouldBe(AuditAction.Create);
        rows[0].UserId.ShouldBe(grantorAccount.UserId);
        rows[0].NewValues.ShouldNotBeNull().ShouldContain(Reason);
        rows[1].UserId.ShouldBe(account.UserId);
        rows[1].NewValues.ShouldNotBeNull().ShouldContain(ProcessGateOperations.Hire);
    }

    // ────────────────────────────────────────────────────────────
    //  Grant validation
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("qisqa")]
    [InlineData("123456789")]
    [InlineData("  abc                ")]
    public async Task Reason_shorter_than_ten_characters_is_refused_on_both_routes(string reason)
    {
        await EnforceAsync();
        var (_, processCase) = await DismissalAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var grantor = await GrantorAsync();
        var before = await OverrideCountAsync();

        var onCase = await PostAsync(grantor, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason });
        var direct = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = account.UserId, reason });

        onCase.Status.ShouldBe(HttpStatusCode.BadRequest, onCase.Raw);
        onCase.Title.ShouldBe(KiritishNotogri);
        direct.Status.ShouldBe(HttpStatusCode.BadRequest, direct.Raw);
        direct.Title.ShouldBe(KiritishNotogri);
        (await OverrideCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Reason_of_exactly_ten_characters_is_accepted_and_longer_than_the_limit_is_not()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var grantor = await GrantorAsync();

        var shortest = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = account.UserId, reason = "1234567890" });
        var tooLong = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = account.UserId, reason = new string('x', ProcessGateOverride.MaxReasonLength + 1) });

        shortest.ShouldBeOk();
        shortest.Json.GetProperty("reason").GetString().ShouldBe("1234567890");
        tooLong.Status.ShouldBe(HttpStatusCode.BadRequest, tooLong.Raw);
        tooLong.Title.ShouldBe(KiritishNotogri);
    }

    [Theory]
    [InlineData("Q99")]
    [InlineData("q12")]
    [InlineData("Q02")]
    [InlineData("AKT")]
    public async Task Gate_the_case_does_not_block_on_cannot_be_overridden(string code)
    {
        await EnforceAsync();
        var (_, processCase) = await DismissalAsync();
        using var grantor = await GrantorAsync();
        var before = await OverrideCountAsync();

        var reply = await PostAsync(grantor, $"/api/processes/cases/{processCase.Id}/gates/{code}/override", new { reason = Reason });

        reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
        reply.Title.ShouldBe(KiritishNotogri);
        (await OverrideCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Override_on_a_case_that_does_not_exist_is_404()
    {
        await EnforceAsync();
        using var grantor = await GrantorAsync();

        var reply = await PostAsync(grantor, $"/api/processes/cases/{Guid.NewGuid()}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });

        reply.Status.ShouldBe(HttpStatusCode.NotFound, reply.Raw);
        reply.Title.ShouldBe("jarayon.topilmadi");
    }

    [Fact]
    public async Task Override_on_a_closed_case_or_a_closed_step_is_409()
    {
        await EnforceAsync();
        var (employed, processCase) = await DismissalAsync();
        await ExitInterviewAsync(employed);
        await RunAsync();
        using var grantor = await GrantorAsync();
        var before = await OverrideCountAsync();

        var closedStep = await PostAsync(grantor, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q13}/override", new { reason = Reason });

        closedStep.Status.ShouldBe(HttpStatusCode.Conflict, closedStep.Raw);
        closedStep.Title.ShouldBe("jarayon.qadam_holati_notogri");

        await SqlAsync($"UPDATE process_cases SET status = {(int)ProcessCaseStatus.Completed} WHERE id = {processCase.Id}");

        var closedCase = await PostAsync(grantor, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });

        closedCase.Status.ShouldBe(HttpStatusCode.Conflict, closedCase.Raw);
        closedCase.Title.ShouldBe("jarayon.holat_notogri");
        (await OverrideCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Direct_hire_override_for_an_unknown_user_is_refused()
    {
        await EnforceAsync();
        using var grantor = await GrantorAsync();
        var before = await OverrideCountAsync();

        var unknown = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = Guid.NewGuid(), reason = Reason });
        var empty = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = Guid.Empty, reason = Reason });

        unknown.Status.ShouldBe(HttpStatusCode.BadRequest, unknown.Raw);
        unknown.Title.ShouldBe(KiritishNotogri);
        empty.Status.ShouldBe(HttpStatusCode.BadRequest, empty.Raw);
        empty.Title.ShouldBe(KiritishNotogri);
        (await OverrideCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Direct_hire_body_of_the_wrong_shape_is_a_400_not_a_500()
    {
        await EnforceAsync();
        using var grantor = await GrantorAsync();

        var wrongType = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId = "not-a-guid", reason = Reason });

        wrongType.Status.ShouldBe(HttpStatusCode.BadRequest, wrongType.Raw);
    }

    // ────────────────────────────────────────────────────────────
    //  RBAC — anonymous, expired token
    // ────────────────────────────────────────────────────────────

    public static TheoryData<string, string> Routes => new()
    {
        { "POST", $"/api/processes/cases/{Guid.Empty}/gates/Q12/override" },
        { "POST", "/api/processes/gate-overrides/direct-hire" },
        { "GET", "/api/processes/gate-overrides" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Anonymous_gets_401_on_every_route(string verb, string path)
    {
        using var anonymous = _factory.CreateClient();
        var before = await OverrideCountAsync();

        var reply = await SendAsync(anonymous, verb, path, new { forUserId = Guid.NewGuid(), reason = Reason });

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized, reply.Raw);
        reply.Raw.ShouldNotContain(Reason);
        (await OverrideCountAsync()).ShouldBe(before);
    }

    /// <summary>The same token re-signed with a future expiry is let in: it is the expiry, not the forgery, that gets the 401.</summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Expired_token_gets_401_on_every_route(string verb, string path)
    {
        var account = await AccountAsync(Permissions.ProcessesView, Permissions.ProcessesOverride, Permissions.ProcessesManage);
        using var expired = _factory.CreateClient();
        expired.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Resign(account.AccessToken, DateTimeOffset.UtcNow.AddMinutes(-5)));
        using var fresh = _factory.CreateClient();
        fresh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Resign(account.AccessToken, DateTimeOffset.UtcNow.AddMinutes(5)));
        var body = new { forUserId = Guid.Empty, reason = Reason };

        var refused = await SendAsync(expired, verb, path, body);
        var control = await SendAsync(fresh, verb, path, body);

        refused.Status.ShouldBe(HttpStatusCode.Unauthorized, refused.Raw);
        control.Status.ShouldNotBe(HttpStatusCode.Unauthorized, control.Raw);
    }

    // ────────────────────────────────────────────────────────────
    //  RBAC — permissions and the hr1 rule
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Case_override_without_the_permission_is_403_even_for_the_hr1_holder()
    {
        await EnforceAsync();
        var (_, processCase) = await DismissalAsync();
        var account = await AccountAsync(Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        await AssignHr1Async(account.UserId);
        using var client = Client(account);
        var before = await OverrideCountAsync();

        var reply = await PostAsync(client, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });

        reply.ShouldBeForbidden();
        (await OverrideCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Direct_hire_override_without_the_permission_is_403_even_for_the_hr1_holder()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        await AssignHr1Async(account.UserId);
        using var client = Client(account);
        var before = await OverrideCountAsync();

        var reply = await PostAsync(client, "/api/processes/gate-overrides/direct-hire", new { forUserId = account.UserId, reason = Reason });

        reply.ShouldBeForbidden();
        (await OverrideCountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task Overrides_log_without_the_view_permission_is_403()
    {
        await EnforceAsync();
        using var client = await ClientAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage, Permissions.EmploymentHire);

        var reply = await SendAsync(client, "GET", "/api/processes/gate-overrides", null);

        reply.ShouldBeForbidden();
        reply.Raw.ShouldNotContain("reason", Case.Insensitive, "a refused caller reads none of the log");
    }

    [Fact]
    public async Task Nobody_holds_hr1_the_permission_alone_is_refused_and_manage_stands_in()
    {
        await EnforceAsync();
        var (_, processCase) = await DismissalAsync();
        var target = await AccountAsync(Permissions.EmploymentHire);
        using var overrideOnly = await ClientAsync(Permissions.ProcessesOverride);
        using var withManage = await ClientAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage);
        var before = await OverrideCountAsync();

        var refusedCase = await PostAsync(overrideOnly, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });
        var refusedDirect = await PostAsync(overrideOnly, "/api/processes/gate-overrides/direct-hire", new { forUserId = target.UserId, reason = Reason });

        refusedCase.Status.ShouldBe(HttpStatusCode.Forbidden, refusedCase.Raw);
        refusedCase.Title.ShouldBe(RuxsatYoq);
        refusedDirect.Status.ShouldBe(HttpStatusCode.Forbidden, refusedDirect.Raw);
        refusedDirect.Title.ShouldBe(RuxsatYoq);
        (await OverrideCountAsync()).ShouldBe(before);

        (await PostAsync(withManage, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason })).ShouldBeOk();
        (await PostAsync(withManage, "/api/processes/gate-overrides/direct-hire", new { forUserId = target.UserId, reason = Reason })).ShouldBeOk();
    }

    [Fact]
    public async Task Someone_holds_hr1_manage_no_longer_stands_in_and_only_the_holder_may_grant()
    {
        await EnforceAsync();
        var (_, processCase) = await DismissalAsync();
        var target = await AccountAsync(Permissions.EmploymentHire);
        var holder = await AccountAsync(Permissions.ProcessesOverride);
        await AssignHr1Async(holder.UserId);
        using var manager = await ClientAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage);
        using var holding = Client(holder);
        var before = await OverrideCountAsync();

        var refusedCase = await PostAsync(manager, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });
        var refusedDirect = await PostAsync(manager, "/api/processes/gate-overrides/direct-hire", new { forUserId = target.UserId, reason = Reason });

        refusedCase.Status.ShouldBe(HttpStatusCode.Forbidden, refusedCase.Raw);
        refusedCase.Title.ShouldBe(RuxsatYoq);
        refusedDirect.Status.ShouldBe(HttpStatusCode.Forbidden, refusedDirect.Raw);
        refusedDirect.Title.ShouldBe(RuxsatYoq);
        (await OverrideCountAsync()).ShouldBe(before);

        var granted = await PostAsync(holding, $"/api/processes/cases/{processCase.Id}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });
        granted.ShouldBeOk();
        granted.Json.GetProperty("grantedBy").GetGuid().ShouldBe(holder.UserId);
        (await PostAsync(holding, "/api/processes/gate-overrides/direct-hire", new { forUserId = target.UserId, reason = Reason })).ShouldBeOk();
    }

    [Fact]
    public async Task Refused_case_override_answers_before_it_says_whether_the_case_exists()
    {
        await EnforceAsync();
        using var overrideOnly = await ClientAsync(Permissions.ProcessesOverride);

        var reply = await PostAsync(overrideOnly, $"/api/processes/cases/{Guid.NewGuid()}/gates/{ProcessGateCodes.Q12}/override", new { reason = Reason });

        reply.Status.ShouldBe(HttpStatusCode.Forbidden, reply.Raw);
        reply.Title.ShouldBe(RuxsatYoq);
    }

    // ────────────────────────────────────────────────────────────
    //  The overrides log
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Log_lists_grants_newest_first_with_their_use_and_filters_by_date()
    {
        await EnforceAsync();
        var account = await AccountAsync(Permissions.EmploymentHire);
        using var grantor = await GrantorAsync();
        using var hr = Client(account);
        using var viewer = await ClientAsync(Permissions.ProcessesView);
        var older = await GrantDirectHireAsync(grantor, account.UserId);
        (await HireAsync(hr, await HireTargetAsync())).ShouldBeOk();
        var newer = await GrantDirectHireAsync(grantor, account.UserId);
        var ancient = await GrantDirectHireAsync(grantor, account.UserId);
        await SqlAsync($"UPDATE process_gate_overrides SET granted_at = {DateTimeOffset.UtcNow.AddDays(-40)}, expires_at = {DateTimeOffset.UtcNow.AddDays(-39)} WHERE id = {ancient}");

        var list = await SendAsync(viewer, "GET", "/api/processes/gate-overrides", null);

        list.ShouldBeOk();
        var rows = list.Json.EnumerateArray().ToList();
        var ids = rows.Select(row => row.GetProperty("id").GetGuid()).ToList();
        ids.ShouldContain(older);
        ids.ShouldContain(newer);
        ids.ShouldNotContain(ancient, "older than the default 30 days");
        ids.IndexOf(newer).ShouldBeLessThan(ids.IndexOf(older));
        var used = rows.Single(row => row.GetProperty("id").GetGuid() == older);
        used.GetProperty("consumedOperation").GetString().ShouldBe(ProcessGateOperations.Hire);
        used.GetProperty("reason").GetString().ShouldBe(Reason);

        var wide = await SendAsync(viewer, "GET", $"/api/processes/gate-overrides?from={Today.AddDays(-60):yyyy-MM-dd}", null);
        wide.Json.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).ShouldContain(ancient);

        var future = await SendAsync(viewer, "GET", $"/api/processes/gate-overrides?from={Today.AddDays(2):yyyy-MM-dd}", null);
        future.ShouldBeOk();
        future.Json.EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task Log_with_a_date_that_is_not_a_date_is_a_400()
    {
        await EnforceAsync();
        using var viewer = await ClientAsync(Permissions.ProcessesView);

        var reply = await SendAsync(viewer, "GET", "/api/processes/gate-overrides?from=kecha", null);

        reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
    }

    // ────────────────────────────────────────────────────────────
    //  Harness
    // ────────────────────────────────────────────────────────────

    private sealed record HireTarget(Guid EmployeeId, Guid LegalEntityId);

    private sealed record Employed(Guid EmployeeId, Guid EmploymentId, Guid LegalEntityId);

    /// <summary>What a test asserts on: status, body, and the challenge of a 401.</summary>
    private sealed record Reply(HttpStatusCode Status, string Raw, string Challenge)
    {
        public JsonElement Json => JsonDocument.Parse(Raw).RootElement;

        public bool Bearer => Challenge.StartsWith("Bearer", StringComparison.Ordinal);

        public string? Title => Raw.Length == 0 ? null : Json.TryGetProperty("title", out var title) ? title.GetString() : null;

        /// <summary>The <c>warnings</c> of a success: <see cref="Bonvi.Application.Employees.Common.HrWarning"/> objects.</summary>
        public IReadOnlyList<string> ResultWarnings
            => [.. Json.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetProperty("code").GetString()!)];

        public void ShouldBeOk() => Status.ShouldBe(HttpStatusCode.OK, Raw);

        public void ShouldBeBlocked(string code, params string[] related)
        {
            Status.ShouldBe(HttpStatusCode.Conflict, Raw);
            Title.ShouldBe(code, Raw);

            if (related.Length == 0)
            {
                Json.TryGetProperty("warnings", out _).ShouldBeFalse(Raw);
            }
            else
            {
                Json.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()).ShouldBe(related, ignoreOrder: true, Raw);
            }
        }

        public void ShouldBeForbidden()
        {
            Status.ShouldBe(HttpStatusCode.Forbidden, Raw);
            Raw.ShouldNotContain(Reason);
        }
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    // ── Settings ──

    /// <summary>Every test's start: the engine started today, gates enforced, hr1 unassigned.</summary>
    private async Task EnforceAsync()
    {
        await SetStartedOnAsync(Today);
        await ModeAsync(SystemSettingKeys.ProcessesGatesEnforce);
        await DbAsync(db => db.BlankRoleAssignments.ExecuteDeleteAsync(Ct));
    }

    private Task<int> SetStartedOnAsync(DateOnly startedOn) => DbAsync(async db =>
    {
        var value = startedOn.ToString(SystemSettingKeys.ProcessesStartedOnFormat, System.Globalization.CultureInfo.InvariantCulture);
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

    private async Task ModeAsync(string mode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var row = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == SystemSettingKeys.ProcessesGatesMode, Ct);

        if (row is null)
        {
            db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.ProcessesGatesMode, mode, protector).Value);
        }
        else
        {
            row.SetValue(mode, protector).IsSuccess.ShouldBeTrue();
        }

        await db.SaveChangesAsync(Ct);
    }

    private Task<int> AssignHr1Async(Guid userId) => DbAsync(async db =>
    {
        db.BlankRoleAssignments.Add(BlankRoleAssignment.Create(BlankActorRoles.Hr1, userId).Value);
        return await db.SaveChangesAsync(Ct);
    });

    // ── Database ──

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> SqlAsync(FormattableString sql)
        => DbAsync(db => db.Database.ExecuteSqlAsync(sql, Ct));

    private async Task RunAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ProcessReconciler>().RunAsync(Ct);
        result.Status.ShouldBe(ProcessReconcileStatus.Finished);
    }

    private Task<int> OverrideCountAsync() => DbAsync(db => db.ProcessGateOverrides.CountAsync(Ct));

    private Task<ProcessGateOverride> OverrideAsync(Guid id)
        => DbAsync(db => db.ProcessGateOverrides.AsNoTracking().SingleAsync(row => row.Id == id, Ct));

    private Task<int> EmploymentCountAsync(HireTarget target)
        => DbAsync(db => db.Employments.AsNoTracking()
            .CountAsync(row => row.EmployeeId == target.EmployeeId && row.LegalEntityId == target.LegalEntityId, Ct));

    private Task<Employment> EmploymentAsync(Employed employed)
        => DbAsync(db => db.Employments.AsNoTracking().SingleAsync(row => row.Id == employed.EmploymentId, Ct));

    private Task<ProcessCase> CaseByIdAsync(Guid id)
        => DbAsync(db => db.ProcessCases.AsNoTracking().Include(row => row.Steps).SingleAsync(row => row.Id == id, Ct));

    private Task<ProcessCase> CandidateCaseAsync(Guid candidateId)
    {
        var subjectRef = ProcessSubjectRefs.Candidate(candidateId, Today);
        return DbAsync(db => db.ProcessCases.AsNoTracking().Include(row => row.Steps)
            .SingleAsync(row => row.ProcessCode == ProcessCodes.HireCandidate && row.SubjectRef == subjectRef, Ct));
    }

    private Task<ProcessCase?> FindDismissalCaseAsync(Employed employed)
    {
        var subjectRef = ProcessSubjectRefs.Dismissal(employed.EmploymentId);
        return DbAsync(db => db.ProcessCases.AsNoTracking().Include(row => row.Steps)
            .SingleOrDefaultAsync(row => row.ProcessCode == ProcessCodes.Dismissal && row.SubjectRef == subjectRef, Ct));
    }

    private async Task<ProcessCase> DismissalCaseAsync(Employed employed)
        => (await FindDismissalCaseAsync(employed)).ShouldNotBeNull();

    /// <summary>An employment with a draft act, whose dismissal case the pass has started: neither blank is approved.</summary>
    private async Task<(Employed Employed, ProcessCase Case)> DismissalAsync()
    {
        var employed = await EmployedAsync();
        await ActAsync(employed, approved: false);
        await RunAsync();
        return (employed, await DismissalCaseAsync(employed));
    }

    private Task<Guid> PositionAsync() => DbAsync(async db =>
    {
        var position = Position.Create("Gate position " + Suffix()).Value;
        db.Add(position);
        await db.SaveChangesAsync(Ct);
        return position.Id;
    });

    private Task<Guid> LegalEntityAsync() => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Gate MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        await db.SaveChangesAsync(Ct);
        return legalEntity.Id;
    });

    /// <summary>An existing person with no employment, and a legal entity to hire them into.</summary>
    private Task<HireTarget> HireTargetAsync() => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Gate MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var employee = Employee.Create(FullName.Create("Xodimov" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(employee);
        await db.SaveChangesAsync(Ct);
        return new HireTarget(employee.Id, legalEntity.Id);
    });

    private Task<Employed> EmployedAsync() => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Gate MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var employee = Employee.Create(FullName.Create("Xodimov" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(employee);
        var employment = Employment.Hire(employee.Id, legalEntity.Id, Today.AddDays(-30), orderNo: "T-1").Value;
        db.Employments.Add(employment);
        await db.SaveChangesAsync(Ct);
        return new Employed(employee.Id, employment.Id, legalEntity.Id);
    });

    private Task<int> TerminateInDbAsync(Guid employmentId, DateOnly date) => DbAsync(async db =>
    {
        var employment = await db.Employments.SingleAsync(row => row.Id == employmentId, Ct);
        employment.Terminate(date, reasonId: null).IsSuccess.ShouldBeTrue();
        return await db.SaveChangesAsync(Ct);
    });

    private Task<Guid> CandidateAsync() => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Gate vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);
        var candidate = Candidate.Register(FullName.Create("Motorov" + Suffix(), "Test", null).Value, vacancy.Id).Value;
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(Ct);
        return candidate.Id;
    });

    private Task<int> FormAsync(Guid candidateId) => DbAsync(async db =>
    {
        db.CandidateForms.Add(CandidateForm.Submit(
            candidateId,
            RandomNumberGenerator.GetInt32(100_000, int.MaxValue),
            "{}",
            "uz",
            "test/photo.jpg",
            "test/signature.png",
            Guid.NewGuid(),
            fileId: null,
            DateTimeOffset.UtcNow).Value);
        return await db.SaveChangesAsync(Ct);
    });

    private async Task CouncilAsync(Guid candidateId, string decision, bool approved = true)
    {
        await DbAsync(db => ProcessTestBlanks.EnsureStandInsAsync(db, Ct));
        await DbAsync(db => ProcessTestBlanks.CreateAsync(
            db,
            ProcessBlanks.F04,
            BlankSubject.None,
            ProcessTestBlanks.CouncilAnswers((candidateId, decision)),
            "{}",
            submit: approved,
            sign: approved ? ["secretary", "chair"] : null,
            decisions: null,
            cancel: false,
            Ct));
    }

    private async Task BackgroundCheckAsync(Guid candidateId, string outcome)
    {
        await DbAsync(db => ProcessTestBlanks.EnsureStandInsAsync(db, Ct));
        await DbAsync(db => ProcessTestBlanks.CreateAsync(
            db,
            ProcessBlanks.F03,
            new BlankSubject(CandidateId: candidateId),
            "{}",
            "{}",
            submit: true,
            sign: ["consent", "hr4Check", "hr1Approve"],
            decisions: new Dictionary<string, string> { ["hr4Check"] = outcome },
            cancel: false,
            Ct));
    }

    private Task<Guid> BlankAsync(string code, Employed employed, string answers, string[] sign, bool cancel = false)
        => DbAsync(db => ProcessTestBlanks.CreateAsync(
            db,
            code,
            new BlankSubject(employed.EmployeeId, employed.EmploymentId),
            answers,
            "{}",
            submit: true,
            sign,
            decisions: null,
            cancel,
            Ct));

    private Task<Guid> ExitInterviewAsync(Employed employed) => BlankAsync(ProcessBlanks.F08, employed, F08Coded, F08Signers);

    private Task<Guid> ActAsync(Employed employed, bool approved)
        => BlankAsync(ProcessBlanks.F09, employed, F09Dismissal, approved ? F09Signers : []);

    /// <summary>Signs the employment's open act through to Approved.</summary>
    private async Task SignF09Async(Employed employed)
    {
        var act = await DbAsync(db => db.BlankSubmissions.AsNoTracking()
            .Where(row => row.TemplateCode == ProcessBlanks.F09 && row.EmploymentId == employed.EmploymentId)
            .Select(row => row.Id)
            .SingleAsync(Ct));

        await DbAsync(db => ProcessTestBlanks.SignAsync(db, act, F09Signers, Ct));
    }

    /// <summary>Opens the step past its order and closes it: the state rules are the aggregate's, who may act is P6's.</summary>
    private Task<int> CloseManuallyAsync(Guid caseId, string stepCode, string? payload) => DbAsync(async db =>
    {
        var row = await db.ProcessCases.Include(processCase => processCase.Steps).SingleAsync(processCase => processCase.Id == caseId, Ct);
        var now = DateTimeOffset.UtcNow;

        row.ActivateStep(stepCode, now).IsSuccess.ShouldBeTrue();
        row.CompleteStepManually(stepCode, await AnyUserIdAsync(db), payload, evidenceSha256: null, now).IsSuccess.ShouldBeTrue();

        return await db.SaveChangesAsync(Ct);
    });

    /// <summary>Closes a step the pass has already opened.</summary>
    private Task<int> CloseOpenStepAsync(Guid caseId, string stepCode, string? payload) => DbAsync(async db =>
    {
        var row = await db.ProcessCases.Include(processCase => processCase.Steps).SingleAsync(processCase => processCase.Id == caseId, Ct);

        row.CompleteStepManually(stepCode, await AnyUserIdAsync(db), payload, evidenceSha256: null, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();

        return await db.SaveChangesAsync(Ct);
    });

    private static Task<Guid> AnyUserIdAsync(AppDbContext db)
        => db.Users.AsNoTracking().OrderBy(user => user.Id).Select(user => user.Id).FirstAsync(Ct);

    // ── HTTP ──

    private Task<TestAccount> AccountAsync(params string[] permissions) => TestAccounts.CreateAsync(_factory, permissions);

    private HttpClient Client(TestAccount account)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        return client;
    }

    private async Task<HttpClient> ClientAsync(params string[] permissions) => Client(await AccountAsync(permissions));

    private Task<HttpClient> HrAsync() => ClientAsync(Permissions.EmploymentHire);

    private Task<HttpClient> TerminatorAsync() => ClientAsync(Permissions.EmploymentTerminate);

    /// <summary>Allowed to grant only while nobody holds hr1, which <see cref="EnforceAsync"/> sees to.</summary>
    private Task<HttpClient> GrantorAsync() => ClientAsync(Permissions.ProcessesOverride, Permissions.ProcessesManage);

    private static async Task<Reply> ReplyOfAsync(HttpResponseMessage response)
    {
        using (response)
        {
            return new Reply(
                response.StatusCode,
                await response.Content.ReadAsStringAsync(Ct),
                string.Join(' ', response.Headers.WwwAuthenticate.Select(header => header.ToString())));
        }
    }

    private static async Task<Reply> PostAsync(HttpClient client, string path, object body)
        => await ReplyOfAsync(await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct));

    private static async Task<Reply> SendAsync(HttpClient client, string verb, string path, object? body)
        => verb == "GET"
            ? await ReplyOfAsync(await client.GetAsync(new Uri(path, UriKind.Relative), Ct))
            : await PostAsync(client, path, body!);

    private Task<Reply> HireAsync(HttpClient client, HireTarget target, Guid? candidateId = null, string? orderNo = "T-1")
        => PostAsync(client, "/api/employments/hire", new
        {
            legalEntityId = target.LegalEntityId,
            employeeId = target.EmployeeId,
            candidateId,
            hireDate = Today.AddDays(-5),
            orderNo,
            rate = 0.5m,
            isPrimary = true,
            salaryCurrency = "UZS",
        });

    private object NewPersonBody(Guid legalEntityId, string lastName)
        => new
        {
            legalEntityId,
            hireDate = Today.AddDays(-5),
            orderNo = "T-1",
            rate = 0.5m,
            isPrimary = true,
            salaryCurrency = "UZS",
            person = new { lastName, firstName = "Test" },
        };

    private Task<Reply> TerminateAsync(HttpClient client, Employed employed, bool confirm = false, DateOnly? date = null)
        => PostAsync(
            client,
            $"/api/employments/{employed.EmploymentId}/terminate{(confirm ? "?confirm=true" : string.Empty)}",
            new { terminationDate = date ?? Today });

    private static async Task<Guid> GrantDirectHireAsync(HttpClient grantor, Guid forUserId)
    {
        var reply = await PostAsync(grantor, "/api/processes/gate-overrides/direct-hire", new { forUserId, reason = Reason });
        reply.ShouldBeOk();
        return reply.Json.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> GrantCaseAsync(HttpClient grantor, Guid caseId, string gateCode)
    {
        var reply = await PostAsync(grantor, $"/api/processes/cases/{caseId}/gates/{gateCode}/override", new { reason = Reason });
        reply.ShouldBeOk();
        return reply.Json.GetProperty("id").GetGuid();
    }

    /// <summary>The same HR token with another expiry, signed with the host's own key.</summary>
    private string Resign(string token, DateTimeOffset expires)
    {
        var parts = token.Split('.');
        var claims = JsonNode.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(parts[1]))!.AsObject();
        var issued = expires.AddHours(-2).ToUnixTimeSeconds();
        claims["exp"] = expires.ToUnixTimeSeconds();
        claims["nbf"] = issued;
        claims["iat"] = issued;

        var signed = parts[0] + "." + System.Buffers.Text.Base64Url.EncodeToString(Encoding.UTF8.GetBytes(claims.ToJsonString()));
        var key = Encoding.UTF8.GetBytes(_factory.Services.GetRequiredService<IConfiguration>()["Jwt:SigningKey"]!);

        return signed + "." + System.Buffers.Text.Base64Url.EncodeToString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(signed)));
    }

    // ── Races ──

    /// <summary>
    /// A real race, not a hopeful one: a row lock holds the override while both commands read it as usable and queue
    /// up to write it; only when both are waiting is it released.
    /// </summary>
    private async Task<Reply[]> RaceOnOverrideAsync(Guid overrideId, Func<Task<Reply>> first, Func<Task<Reply>> second)
    {
        await using var holder = new NpgsqlConnection(_factory.ConnectionString);
        await holder.OpenAsync(Ct);
        await using var hold = await holder.BeginTransactionAsync(Ct);

        await using (var lockRow = new NpgsqlCommand("SELECT id FROM process_gate_overrides WHERE id = @id FOR UPDATE", holder, hold))
        {
            lockRow.Parameters.AddWithValue("id", overrideId);
            await lockRow.ExecuteScalarAsync(Ct);
        }

        var one = first();
        var two = second();

        await WaitForTwoBlockedWritersAsync(one, two);
        await hold.RollbackAsync(Ct);

        return await Task.WhenAll(one, two);
    }

    private async Task WaitForTwoBlockedWritersAsync(Task<Reply> one, Task<Reply> two)
    {
        await using var watcher = new NpgsqlConnection(_factory.ConnectionString);
        await watcher.OpenAsync(Ct);
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND pid <> pg_backend_pid()",
            watcher);

        for (var attempt = 0; attempt < 600; attempt++)
        {
            if ((long)(await count.ExecuteScalarAsync(Ct))! == 2)
            {
                return;
            }

            if (one.IsCompleted || two.IsCompleted)
            {
                var done = one.IsCompleted ? await one : await two;
                throw new Xunit.Sdk.XunitException($"A racer finished before both reached the write: {done.Status} {done.Raw}");
            }

            await Task.Delay(50, Ct);
        }

        throw new Xunit.Sdk.XunitException("The two commands never queued on the lock.");
    }

    private static void ShouldBeOneWinner(Reply[] replies)
    {
        replies.Select(reply => reply.Status).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true, string.Join(" | ", replies.Select(reply => reply.Raw)));

        var loser = replies.Single(reply => reply.Status == HttpStatusCode.Conflict);
        loser.Title.ShouldBe(Yaroqsiz, loser.Raw);
    }

    // ── Wiring ──

    private static TField FieldOf<TField>(object owner)
        where TField : class
    {
        var field = owner.GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(item => item.FieldType == typeof(TField));

        return (TField)field.GetValue(owner)!;
    }

    // ────────────────────────────────────────────────────────────
    //  Wiring: an unregistered gate would silently allow everything
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// The handlers take an optional <see cref="IProcessGate"/> that falls back to allow-all, for the unit tests that
    /// build them by hand. So DI failing to supply it would not fail anywhere; this is the only protection.
    /// </summary>
    [Fact]
    public async Task Handlers_resolved_from_di_hold_the_real_gate_on_every_road_in()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var hire = services.GetRequiredService<HireEmployeeCommandHandler>();
        var terminate = services.GetRequiredService<TerminateEmploymentCommandHandler>();
        var candidateHire = services.GetRequiredService<HireCandidateCommandHandler>();
        var probationResolve = services.GetRequiredService<ResolveProbationCommandHandler>();

        services.GetRequiredService<IProcessGate>().ShouldBeOfType<ProcessGate>();
        FieldOf<IProcessGate>(hire).ShouldBeOfType<ProcessGate>();
        FieldOf<IProcessGate>(terminate).ShouldBeOfType<ProcessGate>();
        FieldOf<IProcessGate>(FieldOf<HireEmployeeCommandHandler>(candidateHire)).ShouldBeOfType<ProcessGate>();
        FieldOf<IProcessGate>(FieldOf<TerminateEmploymentCommandHandler>(probationResolve)).ShouldBeOfType<ProcessGate>();
    }

    /// <summary>The same thing from outside: a handler straight from DI, and it refuses.</summary>
    [Fact]
    public async Task Handlers_resolved_from_di_really_refuse()
    {
        await EnforceAsync();
        var target = await HireTargetAsync();
        var employed = await EmployedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();

        var hired = await scope.ServiceProvider.GetRequiredService<HireEmployeeCommandHandler>()
            .HandleAsync(new HireEmployeeCommand(target.LegalEntityId, EmployeeId: target.EmployeeId, HireDate: Today, OrderNo: "T-1"), Ct);
        var terminated = await scope.ServiceProvider.GetRequiredService<TerminateEmploymentCommandHandler>()
            .HandleAsync(new TerminateEmploymentCommand(employed.EmploymentId, Today, null, null, null, null, false), Ct);

        hired.IsFailure.ShouldBeTrue();
        hired.Error.Code.ShouldBe(Nomzodsiz);
        terminated.IsFailure.ShouldBeTrue();
        terminated.Error.Code.ShouldBe(AktYoq);
    }
}
