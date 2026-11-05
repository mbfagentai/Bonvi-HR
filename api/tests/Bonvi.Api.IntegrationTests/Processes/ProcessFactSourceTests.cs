using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes.Facts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The process fact catalogue (<see cref="EfProcessFactSource"/>, docs/SPEC-mening-ishlarim.md §7.2)
/// against real PostgreSQL: one positive and one negative case per fact family.
/// </summary>
/// <remarks>
/// <para>
/// Data is seeded through the domain aggregates (as <c>ProbationAutoPassJobTests</c> does). Blanks use
/// the built-in wave-1 templates (HR-F06 … F09) seeded at start-up; HR-F03/F04/F05/F11 do not exist
/// yet (they come from HRMentor), so this class registers minimal stand-ins with the §5.2 step ids
/// in its own database — one database per test class.
/// </para>
/// <para>
/// Each test asserts only on its own subjects: the database is shared by the tests of the class.
/// </para>
/// </remarks>
public sealed class ProcessFactSourceTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ProcessFactSourceTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => _factory.Services.GetRequiredService<IClock>().Today;

    // ────────────────────────────────────────────────────────────
    //  Vacancy requests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Vacancy_request_created_and_decided()
    {
        var pending = await VacancyRequestAsync(openings: 1, decide: null);
        var rejected = await VacancyRequestAsync(openings: 1, decide: VacancyRequestStatus.Rejected);
        var approved = await VacancyRequestAsync(openings: 1, decide: VacancyRequestStatus.Approved);

        var facts = await LoadAsync(new ProcessFactQuery { VacancyRequestIds = [pending.Id, rejected.Id, approved.Id, Guid.NewGuid()] });

        facts.ForVacancyRequest(pending.Id, ProcessFacts.VrCreated).Holds.ShouldBeTrue();
        facts.ForVacancyRequest(pending.Id, ProcessFacts.VrDecided).Holds.ShouldBeFalse();

        var decision = facts.ForVacancyRequest(rejected.Id, ProcessFacts.VrDecided);
        decision.Holds.ShouldBeTrue();
        decision.Value.ShouldBe("rejected");
        decision.OccurredAt.ShouldNotBeNull();

        facts.ForVacancyRequest(approved.Id, ProcessFacts.VrDecided).Value.ShouldBe("approved");

        facts.VacancyRequests.Count.ShouldBe(3, "an unknown request id has no facts at all");
    }

    [Fact]
    public async Task Vacancy_request_first_candidate_and_filled()
    {
        var one = await VacancyRequestAsync(openings: 1, decide: VacancyRequestStatus.Approved);
        var two = await VacancyRequestAsync(openings: 2, decide: VacancyRequestStatus.Approved);

        var before = await LoadAsync(new ProcessFactQuery { VacancyRequestIds = [one.Id, two.Id] });
        before.ForVacancyRequest(one.Id, ProcessFacts.VrFirstCandidate).Holds.ShouldBeFalse();
        before.ForVacancyRequest(one.Id, ProcessFacts.VrFilled).Holds.ShouldBeFalse();

        var hiredOnOne = await CandidateAsync(one.VacancyId!.Value);
        await HireAsync(candidateId: hiredOnOne);
        var hiredOnTwo = await CandidateAsync(two.VacancyId!.Value);
        await HireAsync(candidateId: hiredOnTwo);

        var after = await LoadAsync(new ProcessFactQuery { VacancyRequestIds = [one.Id, two.Id] });

        after.ForVacancyRequest(one.Id, ProcessFacts.VrFirstCandidate).Holds.ShouldBeTrue();
        after.ForVacancyRequest(one.Id, ProcessFacts.VrFilled).Holds.ShouldBeTrue("1 hire for 1 opening");
        after.ForVacancyRequest(two.Id, ProcessFacts.VrFilled).Holds.ShouldBeFalse("1 hire for 2 openings");

        await DbAsync(async db =>
        {
            var vacancy = await db.Vacancies.SingleAsync(row => row.Id == two.VacancyId, Ct);
            vacancy.Close(Today).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });

        (await LoadAsync(new ProcessFactQuery { VacancyRequestIds = [two.Id] }))
            .ForVacancyRequest(two.Id, ProcessFacts.VrFilled).Holds.ShouldBeTrue("a closed vacancy fills the request");
    }

    // ────────────────────────────────────────────────────────────
    //  Candidates
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Candidate_stage_fact_follows_the_binding()
    {
        var contactedStage = await StageAsync();
        var councilStage = await StageAsync();
        var vacancy = await VacancyAsync();
        var candidate = await CandidateAsync(vacancy);

        await DbAsync(async db =>
        {
            var row = await db.Candidates.SingleAsync(item => item.Id == candidate, Ct);
            row.RecordStage(contactedStage, Today).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });

        var bindings = new Dictionary<string, Guid?>
        {
            [StageBindings.Contacted] = contactedStage,
            [StageBindings.Council] = councilStage,
            [StageBindings.Invited] = null,
        };

        var current = new CandidateFactSubject(candidate);
        var future = new CandidateFactSubject(candidate, DateTimeOffset.UtcNow.AddHours(1));
        var facts = await LoadAsync(new ProcessFactQuery { Candidates = [current, future], StageBindings = bindings });

        facts.ForCandidate(current, ProcessFacts.CandContacted).Holds.ShouldBeTrue();
        facts.ForCandidate(current, ProcessFacts.CandStage(StageBindings.Contacted)).Holds.ShouldBeTrue();
        facts.ForCandidate(current, ProcessFacts.CandStage(StageBindings.Council)).Holds.ShouldBeFalse("no entry on the bound stage");
        facts.ForCandidate(current, ProcessFacts.CandStage(StageBindings.Invited)).Holds.ShouldBeFalse("unbound");

        facts.ForCandidate(future, ProcessFacts.CandContacted).Holds.ShouldBeFalse("the entry precedes that application");

        var status = facts.ForCandidate(current, ProcessFacts.CandStatus);
        status.Holds.ShouldBeTrue();
        status.Value.ShouldBe("1");
    }

    [Fact]
    public async Task Candidate_form()
    {
        var vacancy = await VacancyAsync();
        var withForm = await CandidateAsync(vacancy);
        var withoutForm = await CandidateAsync(vacancy);

        await DbAsync(async db =>
        {
            db.CandidateForms.Add(CandidateForm.Submit(
                withForm,
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

        var facts = await LoadAsync(new ProcessFactQuery { Candidates = [new(withForm), new(withoutForm)] });

        var form = facts.ForCandidate(new(withForm), ProcessFacts.CandF02);
        form.Holds.ShouldBeTrue();
        form.LinkedId.ShouldNotBeNull();
        facts.ForCandidate(new(withoutForm), ProcessFacts.CandF02).Holds.ShouldBeFalse();
    }

    [Fact]
    public async Task Candidate_hired_returns_the_employment_and_employee()
    {
        var vacancy = await VacancyAsync();
        var hired = await CandidateAsync(vacancy);
        var waiting = await CandidateAsync(vacancy);
        var hire = await HireAsync(candidateId: hired);

        var facts = await LoadAsync(new ProcessFactQuery { Candidates = [new(hired), new(waiting)] });

        var fact = facts.ForCandidate(new(hired), ProcessFacts.CandHired);
        fact.Holds.ShouldBeTrue();
        fact.LinkedId.ShouldBe(hire.EmploymentId);
        fact.LinkedEmployeeId.ShouldBe(hire.EmployeeId);
        facts.ForCandidate(new(hired), ProcessFacts.CandStatus).Value.ShouldBe("3");

        facts.ForCandidate(new(waiting), ProcessFacts.CandHired).Holds.ShouldBeFalse();
    }

    /// <summary>HR-F04 (HRMentor stand-in): the candidate is read from the answers' <c>candidates</c> table.</summary>
    [Fact]
    public async Task Council_list_and_decision()
    {
        await EnsureStandInTemplatesAsync();
        var vacancy = await VacancyAsync();
        var decided = await CandidateAsync(vacancy);
        var listedOnly = await CandidateAsync(vacancy);
        var absent = await CandidateAsync(vacancy);

        await BlankAsync(
            ProcessBlanks.F04,
            BlankSubject.None,
            CouncilAnswers((decided, "conditional")),
            sign: ["secretary", "chair"]);

        await BlankAsync(ProcessBlanks.F04, BlankSubject.None, CouncilAnswers((listedOnly, "accept")), submit: false);

        var facts = await LoadAsync(new ProcessFactQuery { Candidates = [new(decided), new(listedOnly), new(absent)] });

        facts.ForCandidate(new(decided), ProcessFacts.CandInF04).Holds.ShouldBeTrue();
        var decision = facts.ForCandidate(new(decided), ProcessFacts.CandF04Decision);
        decision.Holds.ShouldBeTrue();
        decision.Value.ShouldBe("conditional");

        facts.ForCandidate(new(listedOnly), ProcessFacts.CandInF04).Holds.ShouldBeTrue("a draft council still lists the candidate");
        facts.ForCandidate(new(listedOnly), ProcessFacts.CandF04Decision).Holds.ShouldBeFalse("the council is not approved");

        facts.ForCandidate(new(absent), ProcessFacts.CandInF04).Holds.ShouldBeFalse();
    }

    /// <summary>HR-F03 / HR-F05 (HRMentor stand-ins), candidate subject.</summary>
    [Fact]
    public async Task Candidate_check_and_offer_blanks()
    {
        await EnsureStandInTemplatesAsync();
        var vacancy = await VacancyAsync();
        var candidate = await CandidateAsync(vacancy);
        var subject = new BlankSubject(CandidateId: candidate);

        await BlankAsync(
            ProcessBlanks.F03,
            subject,
            "{}",
            sign: ["consent", "hr4Check", "hr1Approve"],
            decisions: new Dictionary<string, string> { ["hr4Check"] = "watch" });

        await BlankAsync(
            ProcessBlanks.F05,
            subject,
            """{"startDate":"2026-12-01"}""",
            sign: ["offerHr4", "offerCandidate"]);

        var current = new CandidateFactSubject(candidate);
        var later = new CandidateFactSubject(candidate, DateTimeOffset.UtcNow.AddHours(1));
        var facts = await LoadAsync(new ProcessFactQuery { Candidates = [current, later] });

        var check = facts.ForCandidate(current, ProcessFacts.CandF03);
        check.Holds.ShouldBeTrue();
        check.Value.ShouldBe("watch");

        facts.ForCandidate(current, ProcessFacts.CandF05Step("offerCandidate")).Holds.ShouldBeTrue();
        facts.ForCandidate(current, ProcessFacts.CandF05Step("docsHandover")).Holds.ShouldBeFalse("not signed yet");
        facts.ForCandidate(current, ProcessFacts.CandF05StartDate).Value.ShouldBe("2026-12-01");

        facts.ForCandidate(later, ProcessFacts.CandF03).Holds.ShouldBeFalse("a blank of an earlier application");
        facts.ForCandidate(later, ProcessFacts.CandF05Step("offerCandidate")).Holds.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Employments
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hire_order()
    {
        var withOrder = await HireAsync(orderNo: "12-K");
        var withoutOrder = await HireAsync();

        var facts = await LoadAsync(new ProcessFactQuery { EmploymentIds = [withOrder.EmploymentId, withoutOrder.EmploymentId] });

        facts.ForEmployment(withOrder.EmploymentId, ProcessFacts.EmpHired).Holds.ShouldBeTrue();
        var order = facts.ForEmployment(withOrder.EmploymentId, ProcessFacts.EmpHireOrder);
        order.Holds.ShouldBeTrue();
        order.Value.ShouldBe("12-K");

        facts.ForEmployment(withoutOrder.EmploymentId, ProcessFacts.EmpHired).Holds.ShouldBeTrue();
        facts.ForEmployment(withoutOrder.EmploymentId, ProcessFacts.EmpHireOrder).Holds.ShouldBeFalse();
    }

    [Fact]
    public async Task Attendance_identity_or_synced_enrolment()
    {
        var byIdentity = await HireAsync();
        var byEnrolment = await HireAsync();
        var notEnrolled = await HireAsync();

        await DbAsync(async db =>
        {
            db.AttendanceIdentities.Add(AttendanceIdentity.Link(
                AttendanceIdentityProvider.Verifix, "P" + Suffix(), byIdentity.EmployeeId).Value);

            // A removed identity and a failed enrolment do not count.
            var removed = AttendanceIdentity.Link(AttendanceIdentityProvider.Verifix, "P" + Suffix(), notEnrolled.EmployeeId).Value;
            removed.DeletedAt = DateTimeOffset.UtcNow;
            db.AttendanceIdentities.Add(removed);

            var device = AttendanceDevice.Register("Facts " + Suffix(), RandomNumberGenerator.GetBytes(32), "tst00000", DateTimeOffset.UtcNow).Value;
            db.AttendanceDevices.Add(device);

            var synced = DeviceEnrollment.Start(device.Id, byEnrolment.EmployeeId);
            synced.MarkSynced("v1", DateTimeOffset.UtcNow);
            db.DeviceEnrollments.Add(synced);

            var failed = DeviceEnrollment.Start(device.Id, notEnrolled.EmployeeId);
            failed.MarkFailed("no photo", DateTimeOffset.UtcNow);
            db.DeviceEnrollments.Add(failed);

            return await db.SaveChangesAsync(Ct);
        });

        var facts = await LoadAsync(new ProcessFactQuery
        {
            EmploymentIds = [byIdentity.EmploymentId, byEnrolment.EmploymentId, notEnrolled.EmploymentId],
            EmployeeIds = [byIdentity.EmployeeId, notEnrolled.EmployeeId],
        });

        facts.ForEmployee(byIdentity.EmployeeId, ProcessFacts.EmpAttendance).Holds.ShouldBeTrue();
        facts.ForEmployment(byIdentity.EmploymentId, ProcessFacts.EmpAttendance).Holds.ShouldBeTrue();
        facts.ForEmployment(byEnrolment.EmploymentId, ProcessFacts.EmpAttendance).Holds.ShouldBeTrue();

        facts.ForEmployee(notEnrolled.EmployeeId, ProcessFacts.EmpAttendance).Holds.ShouldBeFalse();
        facts.ForEmployment(notEnrolled.EmploymentId, ProcessFacts.EmpAttendance).Holds.ShouldBeFalse();
    }

    /// <summary>HR-F11 (HRMentor stand-in): both steps signed.</summary>
    [Fact]
    public async Task Introduction_sheet_needs_both_signatures()
    {
        await EnsureStandInTemplatesAsync();
        var both = await HireAsync();
        var one = await HireAsync();

        await BlankAsync(ProcessBlanks.F11, Subject(both), "{}", sign: ["signEmployee", "signIntroducer"]);
        await BlankAsync(ProcessBlanks.F11, Subject(one), "{}", sign: ["signEmployee"]);

        var facts = await LoadAsync(new ProcessFactQuery { EmploymentIds = [both.EmploymentId, one.EmploymentId] });

        facts.ForEmployment(both.EmploymentId, ProcessFacts.EmpF11).Holds.ShouldBeTrue();
        facts.ForEmployment(one.EmploymentId, ProcessFacts.EmpF11).Holds.ShouldBeFalse();
    }

    [Fact]
    public async Task Termination()
    {
        var terminated = await HireAsync();
        var active = await HireAsync();
        // Hired today (HireAsync); a termination date before the hire date is rejected by the domain.
        var lastDay = Today;

        await DbAsync(async db =>
        {
            var employment = await db.Employments.SingleAsync(row => row.Id == terminated.EmploymentId, Ct);
            employment.Terminate(lastDay, reasonId: null).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });

        var facts = await LoadAsync(new ProcessFactQuery { EmploymentIds = [terminated.EmploymentId, active.EmploymentId] });

        var fact = facts.ForEmployment(terminated.EmploymentId, ProcessFacts.DisTerminated);
        fact.Holds.ShouldBeTrue();
        fact.Value.ShouldBe(lastDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        facts.ForEmployment(active.EmploymentId, ProcessFacts.DisTerminated).Holds.ShouldBeFalse();
    }

    [Fact]
    public async Task Handover_act_of_kind_dismissal()
    {
        var dismissal = await HireAsync();
        var transfer = await HireAsync();
        var cancelled = await HireAsync();

        var act = await BlankAsync(ProcessBlanks.F09, Subject(dismissal), F09Answers("dismissal"), sign: ["handedOver", "received"]);
        await BlankAsync(ProcessBlanks.F09, Subject(transfer), F09Answers("transfer"), sign: ["handedOver"]);
        await BlankAsync(ProcessBlanks.F09, Subject(cancelled), F09Answers("dismissal"), cancel: true);

        var facts = await LoadAsync(new ProcessFactQuery { EmploymentIds = [dismissal.EmploymentId, transfer.EmploymentId, cancelled.EmploymentId] });

        var opened = facts.ForEmployment(dismissal.EmploymentId, ProcessFacts.DisF09Opened);
        opened.Holds.ShouldBeTrue();
        opened.LinkedId.ShouldBe(act);
        facts.ForEmployment(dismissal.EmploymentId, ProcessFacts.DisF09Step("handedOver")).Holds.ShouldBeTrue();
        facts.ForEmployment(dismissal.EmploymentId, ProcessFacts.DisF09Step("received")).Holds.ShouldBeTrue();
        facts.ForEmployment(dismissal.EmploymentId, ProcessFacts.DisF09Step("signHead")).Holds.ShouldBeFalse();
        facts.ForEmployment(dismissal.EmploymentId, ProcessFacts.DisF09Approved).Holds.ShouldBeFalse();

        facts.ForEmployment(transfer.EmploymentId, ProcessFacts.DisF09Opened).Holds.ShouldBeFalse("kind = transfer");
        facts.ForEmployment(cancelled.EmploymentId, ProcessFacts.DisF09Opened).Holds.ShouldBeFalse("cancelled");

        await SignAsync(act, "signHead", "hr3Control", "signHr1", "hr3Clearance");

        (await LoadAsync(new ProcessFactQuery { EmploymentIds = [dismissal.EmploymentId] }))
            .ForEmployment(dismissal.EmploymentId, ProcessFacts.DisF09Approved).Holds.ShouldBeTrue();
    }

    [Fact]
    public async Task Exit_interview_needs_approval_or_the_K_code()
    {
        var approved = await HireAsync();
        var coded = await HireAsync();
        var uncoded = await HireAsync();

        await BlankAsync(ProcessBlanks.F08, Subject(approved), F08Answers("k03"), sign: ["hr3Status", "signEmployee", "signHr4"]);
        await BlankAsync(ProcessBlanks.F08, Subject(coded), F08Answers("k05"));
        await BlankAsync(ProcessBlanks.F08, Subject(uncoded), F08Answers(null));

        var facts = await LoadAsync(new ProcessFactQuery { EmploymentIds = [approved.EmploymentId, coded.EmploymentId, uncoded.EmploymentId] });

        var fact = facts.ForEmployment(approved.EmploymentId, ProcessFacts.DisF08Approved);
        fact.Holds.ShouldBeTrue();
        fact.Value.ShouldBe("k03");
        facts.ForEmployment(approved.EmploymentId, ProcessFacts.DisF08LeaveDate).Value.ShouldBe("2026-12-31");

        facts.ForEmployment(coded.EmploymentId, ProcessFacts.DisF08Approved).Holds.ShouldBeTrue("in approval with the main K-code (Q13)");
        facts.ForEmployment(uncoded.EmploymentId, ProcessFacts.DisF08Approved).Holds.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Probations
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Probation_resolved()
    {
        var resolved = await ProbationAsync(Today.AddDays(-30));
        var open = await ProbationAsync(Today.AddDays(-30));

        await DbAsync(async db =>
        {
            var probation = await db.Probations.SingleAsync(row => row.Id == resolved.ProbationId, Ct);
            probation.Resolve(ProbationStatus.Passed, Today).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });

        var facts = await LoadAsync(new ProcessFactQuery { ProbationIds = [resolved.ProbationId, open.ProbationId] });

        var fact = facts.ForProbation(resolved.ProbationId, ProcessFacts.ProbResolved);
        fact.Holds.ShouldBeTrue();
        fact.Value.ShouldBe("2");
        facts.ForProbation(open.ProbationId, ProcessFacts.ProbResolved).Holds.ShouldBeFalse();
    }

    [Fact]
    public async Task Adaptation_plan_mentor_group_and_day_steps()
    {
        var signed = await ProbationAsync(Today);
        var draft = await ProbationAsync(Today);
        var later = await ProbationAsync(Today.AddDays(30));

        var mentor = Guid.NewGuid();
        await BlankAsync(
            ProcessBlanks.F06,
            Subject(signed),
            $$$"""{"mentor":{"id":"{{{mentor}}}"}}""",
            computed: """{"day7Pass":false}""",
            sign: ["signEmployee", "signMentor", "signHead", "signHr4", "day1", "day7"]);

        await BlankAsync(ProcessBlanks.F06, Subject(draft), "{}", submit: false);
        await BlankAsync(ProcessBlanks.F06, Subject(later), $$$"""{"mentor":{"id":"{{{mentor}}}"}}""", submit: false);

        var facts = await LoadAsync(new ProcessFactQuery { ProbationIds = [signed.ProbationId, draft.ProbationId, later.ProbationId] });

        facts.ForProbation(signed.ProbationId, ProcessFacts.ProbF06).Holds.ShouldBeTrue();
        facts.ForProbation(signed.ProbationId, ProcessFacts.ProbF06Group1).Holds.ShouldBeTrue();
        var day7 = facts.ForProbation(signed.ProbationId, ProcessFacts.ProbF06Step("day7"));
        day7.Holds.ShouldBeTrue();
        day7.Value.ShouldBe("false", "the computed day7Pass");
        facts.ForProbation(signed.ProbationId, ProcessFacts.ProbF06Step("day30")).Holds.ShouldBeFalse();

        facts.ForProbation(draft.ProbationId, ProcessFacts.ProbF06).Holds.ShouldBeFalse("no mentor");
        facts.ForProbation(draft.ProbationId, ProcessFacts.ProbF06Group1).Holds.ShouldBeFalse();

        facts.ForProbation(later.ProbationId, ProcessFacts.ProbF06).Holds.ShouldBeFalse("created more than 7 days before the start");
    }

    [Fact]
    public async Task Final_review_submitted_and_hr1_decision()
    {
        var decided = await ProbationAsync(Today.AddDays(-80));
        var draft = await ProbationAsync(Today.AddDays(-80));

        await BlankAsync(
            ProcessBlanks.F07,
            Subject(decided),
            "{}",
            sign: ["signHead", "signMentor", "hr4Tests", "signEmployee", "hr1Decision"],
            decisions: new Dictionary<string, string> { ["signEmployee"] = "agree", ["hr1Decision"] = "approve" });

        await BlankAsync(ProcessBlanks.F07, Subject(draft), "{}", submit: false);

        var facts = await LoadAsync(new ProcessFactQuery { ProbationIds = [decided.ProbationId, draft.ProbationId] });

        facts.ForProbation(decided.ProbationId, ProcessFacts.ProbF07Submitted).Holds.ShouldBeTrue();
        var decision = facts.ForProbation(decided.ProbationId, ProcessFacts.ProbF07Step("hr1Decision"));
        decision.Holds.ShouldBeTrue();
        decision.Value.ShouldBe("approve");

        facts.ForProbation(draft.ProbationId, ProcessFacts.ProbF07Submitted).Holds.ShouldBeFalse("still a draft");
        facts.ForProbation(draft.ProbationId, ProcessFacts.ProbF07Step("hr1Decision")).Holds.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Wiring
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Registration_resolves_the_source_and_an_empty_query_has_no_facts()
    {
        var services = new ServiceCollection();
        services.AddProcessFacts();
        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IProcessFactSource)
            && descriptor.ImplementationType == typeof(EfProcessFactSource)
            && descriptor.Lifetime == ServiceLifetime.Scoped);

        var facts = await LoadAsync(new ProcessFactQuery());
        facts.VacancyRequests.ShouldBeEmpty();
        facts.Candidates.ShouldBeEmpty();
        facts.Employments.ShouldBeEmpty();
        facts.Probations.ShouldBeEmpty();
        facts.Employees.ShouldBeEmpty();
        facts.ForEmployment(Guid.NewGuid(), ProcessFacts.EmpHired).ShouldBe(FactValue.NotHeld);
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    private sealed record Hire(Guid EmployeeId, Guid EmploymentId);

    private sealed record ProbationSubject(Guid EmployeeId, Guid EmploymentId, Guid ProbationId);

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    private static BlankSubject Subject(Hire hire) => new(hire.EmployeeId, hire.EmploymentId);

    private static BlankSubject Subject(ProbationSubject probation) => new(probation.EmployeeId, probation.EmploymentId);

    private static string F09Answers(string kind) => $$$"""{"kind":{"value":"{{{kind}}}"}}""";

    private static string F08Answers(string? kCode) => kCode is null
        ? """{"leaveDate":"2026-12-31"}"""
        : $$$"""{"leaveDate":"2026-12-31","mainReason":{"value":"{{{kCode}}}"}}""";

    private static string CouncilAnswers(params (Guid Candidate, string Decision)[] rows) => ProcessTestBlanks.CouncilAnswers(rows);

    private async Task<ProcessFactSnapshot> LoadAsync(ProcessFactQuery query)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var source = new EfProcessFactSource(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        return await source.LoadAsync(query, Ct);
    }

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<Guid> VacancyAsync() => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Facts vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(Ct);
        return vacancy.Id;
    });

    private Task<Guid> StageAsync() => DbAsync(async db =>
    {
        var stage = PipelineStage.Create("Facts stage " + Suffix(), sortOrder: 900).Value;
        db.PipelineStages.Add(stage);
        await db.SaveChangesAsync(Ct);
        return stage.Id;
    });

    private Task<Guid> CandidateAsync(Guid vacancyId) => DbAsync(async db =>
    {
        var candidate = Candidate.Register(FullName.Create("Faktov" + Suffix(), "Test", null).Value, vacancyId).Value;
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(Ct);
        return candidate.Id;
    });

    /// <summary>A vacancy request; approved ones get their own fresh vacancy, decided an hour ago.</summary>
    private Task<VacancyRequest> VacancyRequestAsync(int openings, VacancyRequestStatus? decide) => DbAsync(async db =>
    {
        var department = Department.Create("Facts department " + Suffix()).Value;
        db.Departments.Add(department);
        var requester = Employee.Create(FullName.Create("Sorovchi" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(requester);

        var request = VacancyRequest.Create(department.Id, requester.Id, "Facts position", openings, neededBy: null, note: null).Value;
        db.VacancyRequests.Add(request);

        var decidedAt = DateTimeOffset.UtcNow.AddHours(-1);
        if (decide == VacancyRequestStatus.Approved)
        {
            var vacancy = Vacancy.Open("Facts vacancy " + Suffix(), Today, openings).Value;
            db.Vacancies.Add(vacancy);
            request.Approve(vacancy.Id, Guid.NewGuid(), decidedAt).IsSuccess.ShouldBeTrue();
        }
        else if (decide == VacancyRequestStatus.Rejected)
        {
            request.Reject("Not needed", Guid.NewGuid(), decidedAt).IsSuccess.ShouldBeTrue();
        }

        await db.SaveChangesAsync(Ct);
        return request;
    });

    /// <summary>A hire (optionally of a candidate, who is then marked Hired).</summary>
    private Task<Hire> HireAsync(string? orderNo = null, Guid? candidateId = null) => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Facts MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var employee = Employee.Create(FullName.Create("Xodimov" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(employee);

        var employment = Employment.Hire(employee.Id, legalEntity.Id, Today, orderNo: orderNo, candidateId: candidateId).Value;
        db.Employments.Add(employment);

        if (candidateId is { } id)
        {
            var candidate = await db.Candidates.SingleAsync(row => row.Id == id, Ct);
            candidate.MarkHired(Today).IsSuccess.ShouldBeTrue();
        }

        await db.SaveChangesAsync(Ct);
        return new Hire(employee.Id, employment.Id);
    });

    private async Task<ProbationSubject> ProbationAsync(DateOnly start)
    {
        var hire = await HireAsync();
        return await DbAsync(async db =>
        {
            var probation = Probation.Import(hire.EmploymentId, start, start.AddDays(90)).Value;
            db.Probations.Add(probation);
            await db.SaveChangesAsync(Ct);
            return new ProbationSubject(hire.EmployeeId, hire.EmploymentId, probation.Id);
        });
    }

    /// <summary>
    /// Creates a blank of <paramref name="code"/> through the aggregate: draft → (submit with the
    /// version's own step plan) → sign <paramref name="sign"/> in order → (cancel).
    /// </summary>
    private Task<Guid> BlankAsync(
        string code,
        BlankSubject subject,
        string answers,
        string computed = "{}",
        bool submit = true,
        string[]? sign = null,
        IReadOnlyDictionary<string, string>? decisions = null,
        bool cancel = false)
        => DbAsync(db => ProcessTestBlanks.CreateAsync(db, code, subject, answers, computed, submit, sign, decisions, cancel, Ct));

    private Task<int> SignAsync(Guid blankId, params string[] stepCodes)
        => DbAsync(db => ProcessTestBlanks.SignAsync(db, blankId, stepCodes, Ct));

    private Task<int> EnsureStandInTemplatesAsync() => DbAsync(db => ProcessTestBlanks.EnsureStandInsAsync(db, Ct));
}
