using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// One scenario per flow (§16 P4): each process from its start to its end through the pass the job runs. Facts come
/// from real rows and signed blanks wherever one exists; a step closes by hand only where the template says manual,
/// or where its source is out of the test's reach (the candidate's anketa, HR-F05 on a direct hire).
/// </summary>
public sealed partial class ProcessReconcilerTests
{
    private const ProcessStepState Locked = ProcessStepState.Locked;
    private const ProcessStepState Active = ProcessStepState.Active;
    private const ProcessStepState Blocked = ProcessStepState.Blocked;
    private const ProcessStepState Done = ProcessStepState.Done;
    private const ProcessStepState Skipped = ProcessStepState.Skipped;

    [Fact]
    public async Task Flow_hire_request_runs_from_the_request_to_filled()
    {
        await StartEngineAsync();
        var request = await VacancyRequestAsync();
        var subject = ProcessSubjectRefs.VacancyRequest(request.Id);

        await RunAsync();
        StepsShouldBe(await CaseAsync(ProcessCodes.HireRequest, subject), ("r02-1", Done), ("r02-2", Done), ("r02-3", Active), ("r02-4", Locked), ("r02-14", Locked));

        var vacancy = await ApproveAsync(request.Id);
        await RunAsync();
        StepsShouldBe(await CaseAsync(ProcessCodes.HireRequest, subject), ("r02-3", Done), ("r02-4", Active), ("r02-14", Active));

        var candidate = await CandidateAsync(vacancy);
        await RunAsync();
        var running = await CaseAsync(ProcessCodes.HireRequest, subject);
        StepsShouldBe(running, ("r02-4", Done), ("r02-14", Active));
        running.Status.ShouldBe(ProcessCaseStatus.Active);

        await HireAsync(candidateId: candidate);
        await RunAsync();

        var ended = await CaseAsync(ProcessCodes.HireRequest, subject);
        StepsShouldBe(ended, ("r02-14", Done));
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBe(ProcessOutcomes.Filled);
    }

    [Fact]
    public async Task Flow_hire_candidate_runs_from_the_application_through_the_council_and_offer_to_hired()
    {
        await StartEngineAsync();
        await StandInsAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        var startDate = Today.AddDays(10);

        await RunAsync();
        var parent = await CandidateCaseAsync(candidate);
        StepsShouldBe(parent, ("r02-5", Active), ("r02-6", Locked), ("r02-10", Locked));

        await CloseManuallyAsync(parent.Id, "r02-5", """{"result":"invited"}""");
        await CloseManuallyAsync(parent.Id, "r02-6", payload: null);

        await SignedBlankAsync(
            ProcessBlanks.F04,
            BlankSubject.None,
            ProcessTestBlanks.CouncilAnswers((candidate, "accept")),
            ["secretary", "chair"]);
        await RunAsync();
        StepsShouldBe(await CaseByIdAsync(parent.Id), ("r02-9", Done), ("r02-10", Done), ("r02-8", Skipped), ("r02-11", Active), ("r02-12", Locked));

        var offer = await SignedBlankAsync(
            ProcessBlanks.F05,
            new BlankSubject(CandidateId: candidate),
            $$"""{"startDate":"{{startDate:yyyy-MM-dd}}"}""",
            ["offerHr4", "offerCandidate"]);
        await RunAsync();
        StepsShouldBe(await CaseByIdAsync(parent.Id), ("r02-11", Done), ("r02-12", Active), ("r02-13", Locked));
        (await FormaliseOfAsync(parent)).AnchorDate.ShouldBe(startDate);

        await DbAsync(db => ProcessTestBlanks.SignAsync(db, offer, ["docsHandover"], Ct));
        await RunAsync();
        var firstDay = (await CaseByIdAsync(parent.Id)).FindStep(ProcessStepIds.FirstDay)!;
        firstDay.State.ShouldBe(Blocked, "Q04: the formalise case's safety instruction is open");

        var hire = await HireAsync(candidateId: candidate);
        await RunAsync();
        var child = await FormaliseOfAsync(parent);
        child.EmploymentId.ShouldBe(hire.EmploymentId);
        (await CaseByIdAsync(parent.Id)).Status.ShouldBe(ProcessCaseStatus.Active, "the hire does not end the case before the first day");

        await CloseManuallyAsync(child.Id, ProcessStepIds.SafetyInstruction, """{"topics":["fire"]}""");
        await RunAsync();
        StepsShouldBe(await CaseByIdAsync(parent.Id), (ProcessStepIds.FirstDay, Active));

        await SignedBlankAsync(ProcessBlanks.F11, new BlankSubject(hire.EmployeeId, hire.EmploymentId), "{}", ["signEmployee", "signIntroducer"]);
        await RunAsync();

        var ended = await CaseByIdAsync(parent.Id);
        StepsShouldBe(ended, (ProcessStepIds.FirstDay, Done), ("r02-7", Skipped));
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBe(ProcessOutcomes.Hired);
        (await CaseByIdAsync(child.Id)).Status.ShouldBe(ProcessCaseStatus.Active, "the formalise case has its own steps left");
    }

    [Fact]
    public async Task Flow_hire_formalise_runs_from_a_direct_hire_to_completed()
    {
        await StartEngineAsync();
        var hire = await HireAsync(orderNo: "ENG-" + Suffix());
        var subject = ProcessSubjectRefs.FormaliseFromEmployment(hire.EmploymentId);

        await RunAsync();
        var started = await CaseAsync(ProcessCodes.HireFormalise, subject);
        StepsShouldBe(
            started,
            ("r01-5", Done),
            ("r01-4", Done),
            ("r01-2", Active),
            ("r01-3", Locked),
            ("mm", Active),
            ("r01-8a", Active),
            (ProcessStepIds.PayrollHandover, Blocked));

        await DbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(row => row.Id == hire.EmployeeId, Ct);
            employee.SetPinfl(Pinfl.Create("31501914320045").Value).IsSuccess.ShouldBeTrue();

            db.AttendanceIdentities.Add(AttendanceIdentity.Link(AttendanceIdentityProvider.Verifix, "P" + Suffix(), hire.EmployeeId).Value);

            return await db.SaveChangesAsync(Ct);
        });
        await RunAsync();
        StepsShouldBe(await CaseByIdAsync(started.Id), ("r01-8a", Done), (ProcessStepIds.PayrollHandover, Active));

        foreach (var (step, payload) in new (string, string?)[]
        {
            ("r01-2", null),
            ("r01-3", """{"term":"permanent"}"""),
            ("mm", """{"topics":["fire"]}"""),
            ("r01-6", """{"enteredOn":"2026-11-05"}"""),
            ("r01-7", """{"items":["contract"]}"""),
            (ProcessStepIds.PayrollHandover, """{"payment":"card"}"""),
        })
        {
            await CloseManuallyAsync(started.Id, step, payload);
        }

        await RunAsync();

        var ended = await CaseByIdAsync(started.Id);
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBeNull();
    }

    [Fact]
    public async Task Flow_probation_runs_from_the_plan_through_the_reviews_to_passed()
    {
        await StartEngineAsync();
        var subject = await ProbationAsync(Today);
        var blankSubject = new BlankSubject(subject.EmployeeId, subject.EmploymentId);
        var refOf = ProcessSubjectRefs.Probation(subject.ProbationId);

        await RunAsync();
        StepsShouldBe(await CaseAsync(ProcessCodes.Probation, refOf), ("p03-0", Active), ("p03-1", Locked), ("p03-90", Locked));

        var plan = await SignedBlankAsync(
            ProcessBlanks.F06,
            blankSubject,
            $$$"""{"mentor":{"id":"{{{Guid.NewGuid()}}}"}}""",
            ["signEmployee", "signMentor", "signHead", "signHr4", "day1", "day7"],
            computed: """{"day7Pass":false}""");
        await RunAsync();
        var afterDay7 = await CaseAsync(ProcessCodes.Probation, refOf);
        StepsShouldBe(afterDay7, ("p03-0", Done), ("p03-1", Done), ("p03-7", Done), ("p03-14", Active), ("p03-30", Active));

        await CloseManuallyAsync(afterDay7.Id, "p03-14", """{"percent":80}""");
        await DbAsync(db => ProcessTestBlanks.SignAsync(db, plan, ["day30", "day60", "day85"], Ct));
        await SignedBlankAsync(
            ProcessBlanks.F07,
            blankSubject,
            "{}",
            ["signHead", "signMentor", "hr4Tests", "signEmployee", "hr1Decision"],
            decisions: new Dictionary<string, string> { ["signEmployee"] = "agree", ["hr1Decision"] = "approve" });
        await RunAsync();
        var reviewed = await CaseByIdAsync(afterDay7.Id);
        StepsShouldBe(reviewed, ("p03-14", Done), ("p03-30", Done), ("p03-60", Done), ("p03-85", Done), ("p03-88", Done), ("p03-90", Active));
        reviewed.Status.ShouldBe(ProcessCaseStatus.Active);

        await DbAsync(async db =>
        {
            var probation = await db.Probations.SingleAsync(row => row.Id == subject.ProbationId, Ct);
            probation.Resolve(ProbationStatus.Passed, Today).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });
        await RunAsync();

        var ended = await CaseByIdAsync(afterDay7.Id);
        StepsShouldBe(ended, ("p03-90", Done));
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBe("passed");
    }

    [Fact]
    public async Task Flow_dismissal_runs_from_the_act_through_the_termination_to_terminated()
    {
        await StartEngineAsync();
        var hire = await HireAsync();
        var blankSubject = new BlankSubject(hire.EmployeeId, hire.EmploymentId);
        var subject = ProcessSubjectRefs.Dismissal(hire.EmploymentId);

        var act = await SignedBlankAsync(ProcessBlanks.F09, blankSubject, F09Dismissal, sign: []);
        await RunAsync();
        StepsShouldBe(await CaseAsync(ProcessCodes.Dismissal, subject), ("ex-1", Done), (ProcessStepIds.ActOpened, Done), ("ex-3", Active), ("ex-4", Active), ("ex-8", Locked));

        await SignedBlankAsync(
            ProcessBlanks.F08,
            blankSubject,
            """{"leaveDate":"2026-12-31","mainReason":{"value":"k03"}}""",
            ["hr3Status", "signEmployee", "signHr4"]);
        await DbAsync(db => ProcessTestBlanks.SignAsync(db, act, ["handedOver", "received", "signHead", "hr3Control", "signHr1", "hr3Clearance"], Ct));
        await RunAsync();
        var acted = await CaseAsync(ProcessCodes.Dismissal, subject);
        StepsShouldBe(acted, ("ex-3", Done), ("ex-4", Done), ("ex-5", Done), ("ex-6", Done), ("ex-7", Done), ("ex-8", Active), ("ex-10", Locked));

        await TerminateAsync(hire);
        await RunAsync();
        var terminated = await CaseByIdAsync(acted.Id);
        StepsShouldBe(terminated, ("ex-8", Done), ("ex-11", Done), ("ex-8b", Active), ("ex-9", Active), ("ex-10", Active));
        terminated.Status.ShouldBe(ProcessCaseStatus.Active, "the termination does not skip the settlement");

        await CloseManuallyAsync(acted.Id, "ex-8b", """{"removedOn":"2026-11-05"}""");
        await CloseManuallyAsync(acted.Id, "ex-9", payload: null);
        await CloseManuallyAsync(acted.Id, "ex-10", """{"paidOn":"2026-11-05"}""");
        await RunAsync();

        var ended = await CaseByIdAsync(acted.Id);
        ended.Status.ShouldBe(ProcessCaseStatus.Completed);
        ended.Outcome.ShouldBe(ProcessOutcomes.Terminated);
    }

    [Fact]
    public async Task Dormant_engine_touches_no_subject_of_any_flow()
    {
        await StandInsAsync();
        await StartEngineAsync();
        await RunAsync();
        await SqlAsync($"UPDATE system_settings SET value = 'kept' WHERE key = {Bonvi.Domain.Settings.SystemSettingKeys.ProcessesReconcileWatermark}");
        await SetStartedOnAsync(null);

        var request = await VacancyRequestAsync();
        var candidate = await CandidateAsync(await VacancyAsync());
        var direct = await HireAsync();
        var probation = await ProbationAsync(Today);
        var dismissed = await HireAsync();
        await BlankAsync(ProcessBlanks.F09, dismissed, F09Dismissal);

        (await RunAsync()).Status.ShouldBe(ProcessReconcileStatus.Dormant);

        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).ShouldBeNull();
        (await DbAsync(db => db.ProcessCases.AnyAsync(row => row.CandidateId == candidate, Ct))).ShouldBeFalse();
        (await FindCaseAsync(ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(direct.EmploymentId))).ShouldBeNull();
        (await FindCaseAsync(ProcessCodes.Probation, ProcessSubjectRefs.Probation(probation.ProbationId))).ShouldBeNull();
        (await FindCaseAsync(ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(dismissed.EmploymentId))).ShouldBeNull();
        (await WatermarkAsync()).ShouldBe("kept");
    }

    private static void StepsShouldBe(ProcessCase processCase, params (string Step, ProcessStepState State)[] expected)
    {
        foreach (var (step, state) in expected)
        {
            processCase.FindStep(step)!.State.ShouldBe(state, step);
        }
    }

    private Task<int> StandInsAsync() => DbAsync(db => ProcessTestBlanks.EnsureStandInsAsync(db, Ct));

    private Task<Guid> SignedBlankAsync(
        string code,
        BlankSubject subject,
        string answers,
        string[] sign,
        string computed = "{}",
        IReadOnlyDictionary<string, string>? decisions = null)
        => DbAsync(db => ProcessTestBlanks.CreateAsync(db, code, subject, answers, computed, submit: true, sign, decisions, cancel: false, Ct));

    /// <summary>HR-1's approval, with the vacancy it opens.</summary>
    private Task<Guid> ApproveAsync(Guid requestId) => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Engine vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);

        var request = await db.VacancyRequests.SingleAsync(row => row.Id == requestId, Ct);
        request.Approve(vacancy.Id, Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();

        await db.SaveChangesAsync(Ct);
        return vacancy.Id;
    });
}
