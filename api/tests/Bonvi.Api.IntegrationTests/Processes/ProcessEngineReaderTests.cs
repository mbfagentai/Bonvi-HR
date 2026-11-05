using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes.Engine;
using Bonvi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The engine's batch reads against real PostgreSQL: the starter scans and subjects of
/// <see cref="EfProcessEngineReader"/>, the case repository's batch lookups (docs/SPEC-mening-ishlarim.md §3.2, §7.3, §7.7).
/// </summary>
/// <remarks>
/// Seeded through the domain aggregates as in <c>ProcessFactSourceTests</c>; times the aggregates do not
/// accept (old blanks, reapplications) are backdated with SQL. The database is shared by the tests of the
/// class, so each test asserts only on its own subjects.
/// </remarks>
public sealed class ProcessEngineReaderTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ProcessEngineReaderTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => _factory.Services.GetRequiredService<IClock>().Today;

    private DateOnly Tomorrow => Today.AddDays(1);

    // ────────────────────────────────────────────────────────────
    //  Vacancy requests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Vacancy_request_scan_lists_a_new_request_without_a_case()
    {
        var request = await VacancyRequestAsync();

        var start = (await ReadAsync(reader => reader.ListVacancyRequestStartsAsync(Today, Ct))).Single(row => row.Id == request.Id);

        start.DepartmentId.ShouldBe(request.DepartmentId);
        start.CreatedAt.ShouldBe(request.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Vacancy_request_scan_skips_a_request_with_a_case_or_older_than_the_start()
    {
        var withCase = await VacancyRequestAsync();
        await CaseAsync(
            ProcessCodes.HireRequest,
            ProcessSubjectKind.VacancyRequest,
            new ProcessCaseLinks(VacancyRequestId: withCase.Id));
        var older = await VacancyRequestAsync();

        (await ReadAsync(reader => reader.ListVacancyRequestStartsAsync(Today, Ct)))
            .ShouldNotContain(row => row.Id == withCase.Id);
        (await ReadAsync(reader => reader.ListVacancyRequestStartsAsync(Tomorrow, Ct)))
            .ShouldNotContain(row => row.Id == older.Id);
    }

    // ────────────────────────────────────────────────────────────
    //  Candidates
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Candidate_scan_lists_only_candidates_in_progress_since_the_start()
    {
        var vacancyId = await VacancyAsync();
        var inProgress = await CandidateAsync(vacancyId);
        var hired = await CandidateAsync(vacancyId);
        await HireAsync(candidateId: hired);
        var deleted = await CandidateAsync(vacancyId);
        await SqlAsync($"UPDATE candidates SET deleted_at = now() WHERE id = {deleted}");

        var starts = await ReadAsync(reader => reader.ListCandidateStartsAsync(Today, Ct));

        starts.Single(row => row.Id == inProgress).VacancyId.ShouldBe(vacancyId);
        starts.ShouldNotContain(row => row.Id == hired);
        starts.ShouldNotContain(row => row.Id == deleted);

        (await ReadAsync(reader => reader.ListCandidateStartsAsync(Tomorrow, Ct)))
            .ShouldNotContain(row => row.Id == inProgress);
    }

    [Fact]
    public async Task Candidate_scan_sees_a_reapplication_past_the_old_case()
    {
        var candidate = await CandidateAsync(await VacancyAsync());
        await CaseAsync(
            ProcessCodes.HireCandidate,
            ProcessSubjectKind.Candidate,
            new ProcessCaseLinks(CandidateId: candidate));

        (await ReadAsync(reader => reader.ListCandidateStartsAsync(Today, Ct)))
            .ShouldNotContain(row => row.Id == candidate, "the case covers the current application");

        var reappliedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await SqlAsync($"UPDATE candidates SET reapplied_at = {reappliedAt} WHERE id = {candidate}");

        var start = (await ReadAsync(reader => reader.ListCandidateStartsAsync(Today, Ct))).Single(row => row.Id == candidate);

        start.AppliedAt.ShouldBe(reappliedAt, TimeSpan.FromMilliseconds(1));
    }

    // ────────────────────────────────────────────────────────────
    //  Hire events
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hire_event_scan_carries_the_employment_and_the_candidate()
    {
        var candidate = await CandidateAsync(await VacancyAsync());
        var hire = await HireAsync(candidateId: candidate);
        var direct = await HireAsync();

        var starts = await ReadAsync(reader => reader.ListHireEventStartsAsync(Today, Ct));

        var ofCandidate = starts.Single(row => row.EmploymentId == hire.EmploymentId);
        ofCandidate.EmployeeId.ShouldBe(hire.EmployeeId);
        ofCandidate.HireDate.ShouldBe(Today);
        ofCandidate.CandidateId.ShouldBe(candidate);
        ofCandidate.CandidateAppliedAt.ShouldNotBeNull();

        var ofDirect = starts.Single(row => row.EmploymentId == direct.EmploymentId);
        ofDirect.CandidateId.ShouldBeNull();
        ofDirect.CandidateAppliedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Hire_event_scan_skips_imported_events_old_events_and_employments_with_a_case()
    {
        var imported = await HireAsync();
        await SqlAsync($"UPDATE employment_events SET is_imported = true WHERE employment_id = {imported.EmploymentId}");
        var withCase = await HireAsync();
        await CaseAsync(
            ProcessCodes.HireFormalise,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: withCase.EmploymentId, EmployeeId: withCase.EmployeeId));
        var older = await HireAsync();

        var starts = await ReadAsync(reader => reader.ListHireEventStartsAsync(Today, Ct));

        starts.ShouldNotContain(row => row.EmploymentId == imported.EmploymentId);
        starts.ShouldNotContain(row => row.EmploymentId == withCase.EmploymentId);

        (await ReadAsync(reader => reader.ListHireEventStartsAsync(Tomorrow, Ct)))
            .ShouldNotContain(row => row.EmploymentId == older.EmploymentId);
    }

    [Fact]
    public async Task Hire_event_scan_gives_an_employment_one_row_from_its_earliest_event()
    {
        var hire = await HireAsync();
        var rehireAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await DbAsync(async db =>
        {
            db.EmploymentEvents.Add(EmploymentEvent.Import(hire.EmploymentId, EmploymentEventType.Rehire, Today));
            return await db.SaveChangesAsync(Ct);
        });
        await SqlAsync(
            $"UPDATE employment_events SET is_imported = false, recorded_at = {rehireAt} WHERE employment_id = {hire.EmploymentId} AND type = 8");

        var row = (await ReadAsync(reader => reader.ListHireEventStartsAsync(Today, Ct)))
            .Single(start => start.EmploymentId == hire.EmploymentId);

        row.RecordedAt.ShouldBeLessThan(rehireAt);
    }

    // ────────────────────────────────────────────────────────────
    //  Probations
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Probation_scan_lists_a_probation_in_progress_from_the_start_on()
    {
        var current = await ProbationAsync(Today);
        var older = await ProbationAsync(Today.AddDays(-1));
        var resolved = await ProbationAsync(Today);
        await SqlAsync($"UPDATE probations SET status = 2 WHERE id = {resolved.ProbationId}");

        var starts = await ReadAsync(reader => reader.ListProbationStartsAsync(Today, Ct));

        var start = starts.Single(row => row.Id == current.ProbationId);
        start.EmploymentId.ShouldBe(current.EmploymentId);
        start.EmployeeId.ShouldBe(current.EmployeeId);
        start.StartDate.ShouldBe(Today);

        starts.ShouldNotContain(row => row.Id == older.ProbationId);
        starts.ShouldNotContain(row => row.Id == resolved.ProbationId);
    }

    [Fact]
    public async Task Probation_scan_checks_the_employment_not_the_probation()
    {
        var probation = await ProbationAsync(Today);

        // The case of an earlier probation of this employment: its row is gone, the case stays.
        await CaseAsync(
            ProcessCodes.Probation,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: probation.EmploymentId, EmployeeId: probation.EmployeeId));

        (await ReadAsync(reader => reader.ListProbationStartsAsync(Today, Ct)))
            .ShouldNotContain(row => row.EmploymentId == probation.EmploymentId);
    }

    // ────────────────────────────────────────────────────────────
    //  Dismissals
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dismissal_scan_starts_from_a_recent_f08_or_dismissal_f09()
    {
        var byF08 = await HireAsync();
        var f08 = await BlankAsync(ProcessBlanks.F08, byF08, F08Answers);
        var byF09 = await HireAsync();
        await BlankAsync(ProcessBlanks.F09, byF09, F09Answers(ProcessBlanks.F09DismissalKind));

        var starts = await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct));

        var start = starts.Single(row => row.EmploymentId == byF08.EmploymentId);
        start.EmployeeId.ShouldBe(byF08.EmployeeId);
        start.InFlight.ShouldBeFalse();
        start.StartedOn.ShouldBe(start.StartedAt.TashkentDate());
        start.StartedAt.ShouldBe(await BlankCreatedAtAsync(f08), TimeSpan.FromMilliseconds(1));
        start.LatestFactAt.ShouldBe(start.StartedAt);

        starts.ShouldContain(row => row.EmploymentId == byF09.EmploymentId);
    }

    [Fact]
    public async Task Dismissal_scan_ignores_blanks_that_prove_nothing()
    {
        var otherKind = await HireAsync();
        await BlankAsync(ProcessBlanks.F09, otherKind, F09Answers("transfer"));
        var cancelled = await HireAsync();
        await BlankAsync(ProcessBlanks.F08, cancelled, F08Answers, cancel: true);
        var oldOnly = await HireAsync();
        await BackdateAsync(await BlankAsync(ProcessBlanks.F08, oldOnly, F08Answers));

        var starts = await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct));

        starts.ShouldNotContain(row => row.EmploymentId == otherKind.EmploymentId);
        starts.ShouldNotContain(row => row.EmploymentId == cancelled.EmploymentId);
        starts.ShouldNotContain(row => row.EmploymentId == oldOnly.EmploymentId, "an older dismissal waits for its next start fact");
    }

    [Fact]
    public async Task Dismissal_scan_marks_an_older_blank_as_in_flight()
    {
        var hire = await HireAsync();
        var old = await BlankAsync(ProcessBlanks.F09, hire, F09Answers(ProcessBlanks.F09DismissalKind));
        await BackdateAsync(old);
        var recent = await BlankAsync(ProcessBlanks.F08, hire, F08Answers);

        var start = (await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct)))
            .Single(row => row.EmploymentId == hire.EmploymentId);

        start.InFlight.ShouldBeTrue();
        start.StartedAt.ShouldBe(await BlankCreatedAtAsync(recent), TimeSpan.FromMilliseconds(1), "the older blank is not a start fact");
    }

    [Fact]
    public async Task Dismissal_scan_keeps_blanks_to_their_own_employment()
    {
        var hire = await HireAsync();
        var otherEmployment = await HireAsync();
        await BlankAsync(ProcessBlanks.F08, new BlankSubject(hire.EmployeeId, otherEmployment.EmploymentId), F08Answers);

        (await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct)))
            .ShouldNotContain(row => row.EmploymentId == hire.EmploymentId);
    }

    [Fact]
    public async Task Dismissal_scan_starts_a_late_case_from_a_termination()
    {
        var hire = await HireAsync();
        await TerminateAsync(hire);

        var start = (await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct)))
            .Single(row => row.EmploymentId == hire.EmploymentId);

        start.StartedAt.ShouldBe(ProcessSubjectRefs.StartOfDay(Today));
        start.StartedOn.ShouldBe(Today);
        start.InFlight.ShouldBeFalse();
        start.LatestFactAt.ShouldBeGreaterThan(start.StartedAt, "the recording time, not the date of the termination");

        (await ReadAsync(reader => reader.ListDismissalStartsAsync(Tomorrow, Ct)))
            .ShouldNotContain(row => row.EmploymentId == hire.EmploymentId);
    }

    [Fact]
    public async Task Dismissal_scan_merges_a_termination_with_its_blank()
    {
        var hire = await HireAsync();
        var blank = await BlankAsync(ProcessBlanks.F08, hire, F08Answers);
        await TerminateAsync(hire);

        var start = (await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct)))
            .Single(row => row.EmploymentId == hire.EmploymentId);

        start.StartedAt.ShouldBe(ProcessSubjectRefs.StartOfDay(Today), "the earliest fact wins");
        start.LatestFactAt.ShouldBeGreaterThanOrEqualTo(await BlankCreatedAtAsync(blank));
    }

    [Fact]
    public async Task Dismissal_scan_skips_a_live_case_and_returns_a_withdrawn_one()
    {
        var open = await HireAsync();
        await BlankAsync(ProcessBlanks.F08, open, F08Answers);
        await CaseAsync(
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: open.EmploymentId, EmployeeId: open.EmployeeId));

        var terminated = await HireAsync();
        await BlankAsync(ProcessBlanks.F08, terminated, F08Answers);
        await CaseAsync(
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: terminated.EmploymentId, EmployeeId: terminated.EmployeeId),
            outcome: ProcessOutcomes.Terminated);

        var withdrawn = await HireAsync();
        await BlankAsync(ProcessBlanks.F08, withdrawn, F08Answers);
        await CaseAsync(
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: withdrawn.EmploymentId, EmployeeId: withdrawn.EmployeeId),
            outcome: ProcessOutcomes.Withdrawn);

        var starts = await ReadAsync(reader => reader.ListDismissalStartsAsync(Today, Ct));

        starts.ShouldNotContain(row => row.EmploymentId == open.EmploymentId);
        starts.ShouldNotContain(row => row.EmploymentId == terminated.EmploymentId);
        starts.ShouldContain(row => row.EmploymentId == withdrawn.EmploymentId);
    }

    // ────────────────────────────────────────────────────────────
    //  Subjects
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subjects_of_a_batch_with_heads_and_users()
    {
        var request = await VacancyRequestAsync();
        var vacancyId = await VacancyAsync();
        await SqlAsync($"UPDATE vacancy_requests SET vacancy_id = {vacancyId} WHERE id = {request.Id}");
        var candidate = await CandidateAsync(vacancyId);

        var head = await HireAsync();
        await DbAsync(async db =>
        {
            var department = await db.Departments.SingleAsync(row => row.Id == request.DepartmentId, Ct);
            department.AssignHead(head.EmployeeId);
            return await db.SaveChangesAsync(Ct);
        });
        await UserAsync(head.EmployeeId);

        var probation = await ProbationAsync(Today);
        var noUser = Guid.NewGuid();

        var subjects = await ReadAsync(reader => reader.LoadSubjectsAsync(
            new ProcessSubjectQuery
            {
                VacancyRequestIds = [request.Id],
                CandidateIds = [candidate],
                EmploymentIds = [probation.EmploymentId],
                ProbationIds = [probation.ProbationId],
                EmployeeIds = [noUser],
            },
            Ct));

        subjects.VacancyRequests[request.Id].VacancyId.ShouldBe(vacancyId);
        subjects.Candidates[candidate].DepartmentId.ShouldBe(request.DepartmentId, "from the request behind the vacancy");
        subjects.Employments[probation.EmploymentId].EmployeeId.ShouldBe(probation.EmployeeId);
        subjects.Employments[probation.EmploymentId].EmployeeHasPinfl.ShouldBeFalse();
        subjects.Probations[probation.ProbationId].EmploymentId.ShouldBe(probation.EmploymentId);
        subjects.DepartmentHeads[request.DepartmentId].ShouldBe(head.EmployeeId);
        subjects.EmployeesWithUser.ShouldContain(head.EmployeeId);
        subjects.EmployeesWithUser.ShouldNotContain(noUser);
    }

    [Fact]
    public async Task Subjects_hide_what_was_deleted_and_an_inactive_user()
    {
        var deletedCandidate = await CandidateAsync(await VacancyAsync());
        await SqlAsync($"UPDATE candidates SET deleted_at = now() WHERE id = {deletedCandidate}");
        var probation = await ProbationAsync(Today);
        await SqlAsync($"UPDATE employees SET deleted_at = now() WHERE id = {probation.EmployeeId}");

        var inactive = await HireAsync();
        await UserAsync(inactive.EmployeeId, active: false);

        var subjects = await ReadAsync(reader => reader.LoadSubjectsAsync(
            new ProcessSubjectQuery
            {
                CandidateIds = [deletedCandidate],
                EmploymentIds = [probation.EmploymentId, inactive.EmploymentId],
                ProbationIds = [probation.ProbationId],
            },
            Ct));

        subjects.Candidates.ShouldNotContainKey(deletedCandidate);
        subjects.Employments.ShouldNotContainKey(probation.EmploymentId);
        subjects.Probations.ShouldNotContainKey(probation.ProbationId);
        subjects.EmployeesWithUser.ShouldNotContain(inactive.EmployeeId);
    }

    [Fact]
    public async Task An_empty_query_reads_nothing()
    {
        var subjects = await ReadAsync(reader => reader.LoadSubjectsAsync(new ProcessSubjectQuery(), Ct));

        subjects.Employments.ShouldBeEmpty();
        subjects.DepartmentHeads.ShouldBeEmpty();
        subjects.EmployeesWithUser.ShouldBeEmpty();
    }

    // ────────────────────────────────────────────────────────────
    //  Repositories
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Case_lookups_by_subject_and_parent_bring_their_steps()
    {
        var parentHire = await HireAsync();
        var parent = await CaseAsync(
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: parentHire.EmploymentId));
        var childHire = await HireAsync();
        var child = await CaseAsync(
            ProcessCodes.Dismissal,
            ProcessSubjectKind.Employment,
            new ProcessCaseLinks(EmploymentId: childHire.EmploymentId),
            parentCaseId: parent.Id);

        var (bySubject, byParent) = await DbAsync(async db =>
        {
            var repository = new ProcessCaseRepository(db);
            return (
                await repository.ListBySubjectRefsAsync(ProcessCodes.Dismissal, [parent.SubjectRef, child.SubjectRef, "absent"], Ct),
                await repository.ListByParentIdsAsync([parent.Id], Ct));
        });

        bySubject.Select(row => row.Id).ShouldBe([parent.Id, child.Id], ignoreOrder: true);
        bySubject.ShouldAllBe(row => row.Steps.Count == 1);
        byParent.Single().Id.ShouldBe(child.Id);
        byParent.Single().Steps.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Case_lookups_with_nothing_to_look_up_are_empty()
    {
        var (bySubject, byParent, versions) = await DbAsync(async db => (
            await new ProcessCaseRepository(db).ListBySubjectRefsAsync(ProcessCodes.Dismissal, [], Ct),
            await new ProcessCaseRepository(db).ListByParentIdsAsync([], Ct),
            await new ProcessTemplateVersionRepository(db).ListByIdsAsync([], Ct)));

        bySubject.ShouldBeEmpty();
        byParent.ShouldBeEmpty();
        versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Template_versions_by_id_include_a_retired_one()
    {
        var published = await DbAsync(db => db.ProcessTemplateVersions.AsNoTracking()
            .FirstAsync(row => row.Status == ProcessTemplateVersionStatus.Published, Ct));

        // A transaction that is never committed: other tests of the class keep the published version.
        await DbAsync(db => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            await db.Database.ExecuteSqlAsync(
                $"UPDATE process_template_versions SET status = 3 WHERE id = {published.Id}",
                Ct);

            var versions = await new ProcessTemplateVersionRepository(db).ListByIdsAsync([published.Id], Ct);

            versions.Single().Status.ShouldBe(ProcessTemplateVersionStatus.Retired);
            return 0;
        }));
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    private const string F08Answers = """{"leaveDate":"2026-12-31"}""";

    private sealed record Hire(Guid EmployeeId, Guid EmploymentId);

    private sealed record ProbationSubject(Guid EmployeeId, Guid EmploymentId, Guid ProbationId);

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    private static string F09Answers(string kind) => $$$"""{"kind":{"value":"{{{kind}}}"}}""";

    private async Task<T> ReadAsync<T>(Func<EfProcessEngineReader, Task<T>> read)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await read(new EfProcessEngineReader(scope.ServiceProvider.GetRequiredService<AppDbContext>()));
    }

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> SqlAsync(FormattableString sql)
        => DbAsync(db => db.Database.ExecuteSqlAsync(sql, Ct));

    private Task<Guid> VacancyAsync() => DbAsync(async db =>
    {
        var vacancy = Vacancy.Open("Engine vacancy " + Suffix(), Today).Value;
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(Ct);
        return vacancy.Id;
    });

    private Task<Guid> CandidateAsync(Guid vacancyId) => DbAsync(async db =>
    {
        var candidate = Candidate.Register(FullName.Create("Motorov" + Suffix(), "Test", null).Value, vacancyId).Value;
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(Ct);
        return candidate.Id;
    });

    private Task<VacancyRequest> VacancyRequestAsync() => DbAsync(async db =>
    {
        var department = Department.Create("Engine department " + Suffix()).Value;
        db.Departments.Add(department);
        var requester = Employee.Create(FullName.Create("Sorovchi" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(requester);

        var request = VacancyRequest.Create(department.Id, requester.Id, "Engine position", 1, neededBy: null, note: null).Value;
        db.VacancyRequests.Add(request);

        await db.SaveChangesAsync(Ct);
        return request;
    });

    /// <summary>A hire (optionally of a candidate, who is then marked Hired).</summary>
    private Task<Hire> HireAsync(Guid? candidateId = null) => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Engine MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var employee = Employee.Create(FullName.Create("Xodimov" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(employee);

        var employment = Employment.Hire(employee.Id, legalEntity.Id, Today, candidateId: candidateId).Value;
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

    private Task<int> TerminateAsync(Hire hire) => DbAsync(async db =>
    {
        var employment = await db.Employments.SingleAsync(row => row.Id == hire.EmploymentId, Ct);
        employment.Terminate(Today, reasonId: null).IsSuccess.ShouldBeTrue();
        return await db.SaveChangesAsync(Ct);
    });

    private Task<Guid> BlankAsync(string code, Hire hire, string answers, bool cancel = false)
        => BlankAsync(code, new BlankSubject(hire.EmployeeId, hire.EmploymentId), answers, cancel);

    /// <summary>A draft blank: a draft is live, which is all the scans ask of a blank.</summary>
    private Task<Guid> BlankAsync(string code, BlankSubject subject, string answers, bool cancel = false) => DbAsync(async db =>
    {
        var template = await db.BlankTemplates.SingleAsync(row => row.Code == code, Ct);
        var version = await db.BlankTemplateVersions
            .SingleAsync(row => row.TemplateId == template.Id && row.Status == BlankTemplateVersionStatus.Published, Ct);

        var blank = BlankSubmission.Create(template, version, subject, new BlankAnswersState(answers, "{}", "[]", 0, null)).Value;

        if (cancel)
        {
            blank.Cancel("test", DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        }

        db.BlankSubmissions.Add(blank);
        await db.SaveChangesAsync(Ct);
        return blank.Id;
    });

    private Task<int> BackdateAsync(Guid blankId)
    {
        var old = ProcessSubjectRefs.StartOfDay(Today.AddDays(-30));
        return SqlAsync($"UPDATE blank_submissions SET created_at = {old} WHERE id = {blankId}");
    }

    private Task<DateTimeOffset> BlankCreatedAtAsync(Guid blankId)
        => DbAsync(db => db.BlankSubmissions.AsNoTracking()
            .Where(row => row.Id == blankId)
            .Select(row => row.CreatedAt)
            .SingleAsync(Ct));

    /// <summary>A case on the seeded version of any template: the scans read only its code, subject and outcome.</summary>
    private Task<ProcessCase> CaseAsync(
        string processCode,
        ProcessSubjectKind kind,
        ProcessCaseLinks links,
        string? outcome = null,
        Guid? parentCaseId = null) => DbAsync(async db =>
    {
        var version = await db.ProcessTemplateVersions.AsNoTracking()
            .FirstAsync(row => row.Status == ProcessTemplateVersionStatus.Published, Ct);
        var now = DateTimeOffset.UtcNow;

        var processCase = ProcessCase.Start(
            new ProcessCaseStart(processCode, version.Id, kind, "t:" + Guid.NewGuid().ToString("N"), Today, Legacy: false, links, ParentCaseId: parentCaseId),
            [new ProcessStepPlan("t-1", 1, "Test", HolderRole: null)],
            now).Value;

        if (outcome is not null)
        {
            processCase.Complete(outcome, now).IsSuccess.ShouldBeTrue();
        }

        db.ProcessCases.Add(processCase);
        await db.SaveChangesAsync(Ct);
        return processCase;
    });

    private Task<int> UserAsync(Guid employeeId, bool active = true) => DbAsync(async db =>
    {
        var phone = PhoneNumber.Create("90" + Random.Shared.Next(1_000_000, 9_999_999)).Value;
        var user = User.Create(phone, "test-hash", FullName.Create("Foydalanuvchi" + Suffix(), "Test", null).Value).Value;
        user.LinkToEmployee(employeeId);

        if (!active)
        {
            user.Deactivate();
        }

        db.Users.Add(user);
        return await db.SaveChangesAsync(Ct);
    });
}
