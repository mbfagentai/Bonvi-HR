using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Text.Json;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The list reads of «Mening ishlarim» send as many SQL commands for many cases as for a few: an N+1 on a screen that
/// HR opens all day shows here as a number, long before it shows as slowness.
/// </summary>
/// <remarks>
/// The few-cases set already holds one of each kind (Q03, Q04, Q12, a vacancy request), so each conditional query runs
/// at both sizes and the two counts differ only if something loops. The inline pass would run its own commands inside
/// a read, so its clock is frozen: it takes its one slot on the warm-up read and never runs again. Every measurement
/// follows a warm-up of its route, so a cache filling up is not counted as a query.
/// </remarks>
public sealed class ProcessReadQueryCountTests : IClassFixture<BonviApiFactory>
{
    private const string Root = "/api/processes";
    private const string StepsTable = "process_case_steps";
    private const string F09Dismissal = """{"kind":{"value":"dismissal"}}""";

    /// <summary>What «many» adds of each kind on top of the one each of «few».</summary>
    private const int AddedOfEachKind = 5;

    private static readonly string[] GatesOfTheSet = [ProcessGateCodes.Q03, ProcessGateCodes.Q04, ProcessGateCodes.Q12];
    private static readonly string[] SectionsOfBlockedTasks = ["urgent", "held"];

    private static readonly Read[] Reads =
    [
        new(Root + "/my", BlockedTasks),
        new(Root + "/cases?pageSize=1000", page => page.GetProperty("items").GetArrayLength()),
        new(Root + "/board?process=" + ProcessCodes.HireFormalise, Cards),
        new(Root + "/board?process=" + ProcessCodes.HireCandidate, Cards),
        new(Root + "/board?process=" + ProcessCodes.HireRequest, Cards),
        new(Root + "/board?process=" + ProcessCodes.Dismissal, Cards),
    ];

    private readonly BonviApiFactory _factory;
    private readonly CommandLog _log = new();

    public ProcessReadQueryCountTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_list_reads_send_the_same_commands_for_a_few_cases_and_for_many()
    {
        await using var host = CountingHost();
        await PrepareAsync(host);

        var viewer = await TestAccounts.CreateAsync(
            host,
            Permissions.ProcessesView,
            Permissions.ProcessesWork,
            Permissions.ProcessesManage,
            Permissions.EmployeesView,
            Permissions.AttendanceManage);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewer.AccessToken);

        await SeedAsync(host, 1);
        var few = await MeasureAsync(client);

        await SeedAsync(host, AddedOfEachKind);
        var many = await MeasureAsync(client);

        // The guards: a read that returned nothing, or sent nothing to the steps table, would agree with itself.
        foreach (var read in Reads)
        {
            var small = few[read.Route];
            var large = many[read.Route];

            small.Commands.ShouldContain(command => command.Contains(StepsTable, StringComparison.Ordinal), read.Route);
            large.Commands.ShouldContain(command => command.Contains(StepsTable, StringComparison.Ordinal), read.Route);
            small.Size.ShouldBeGreaterThan(0, read.Route);
            large.Size.ShouldBeGreaterThanOrEqualTo(small.Size + AddedOfEachKind, read.Route);
        }

        // One blocked task per gate per seeded case, whichever viewer's section it lands in.
        few[Reads[0].Route].Size.ShouldBe(GatesOfTheSet.Length);
        many[Reads[0].Route].Size.ShouldBe(GatesOfTheSet.Length * (1 + AddedOfEachKind));

        var grown = Reads
            .Where(read => few[read.Route].Commands.Count != many[read.Route].Commands.Count)
            .Select(read => Difference(read.Route, few[read.Route], many[read.Route]))
            .ToList();

        grown.ShouldBeEmpty(string.Join(Environment.NewLine, grown));

        // Here, not in a test of its own: the database is the class's, and a second seed would break the exact counts above.
        await TheChainReadSendsAtMostThreeCaseCommandsAsync(host, client);
    }

    private async Task TheChainReadSendsAtMostThreeCaseCommandsAsync(WebApplicationFactory<Program> host, HttpClient client)
    {
        var candidate = await DbAsync(host, db => db.ProcessCases.AsNoTracking()
            .Where(row => row.ProcessCode == ProcessCodes.HireFormalise && row.ParentCaseId != null)
            .Select(row => row.ParentCaseId!.Value)
            .FirstAsync(Ct));
        var formalise = await DbAsync(host, db => db.ProcessCases.AsNoTracking()
            .Where(row => row.ParentCaseId == candidate)
            .Select(row => row.Id)
            .FirstAsync(Ct));
        var request = await DbAsync(host, db => db.ProcessCases.AsNoTracking()
            .Where(row => row.ProcessCode == ProcessCodes.HireRequest)
            .Select(row => row.Id)
            .FirstAsync(Ct));
        await DbAsync(host, db => db.Database.ExecuteSqlAsync($"UPDATE process_cases SET parent_case_id = {request} WHERE id = {candidate}", Ct));

        foreach (var (asked, length) in new[] { (request, 1), (candidate, 3), (formalise, 3) })
        {
            (await GetAsync(client, $"{Root}/cases/{asked}/chain")).ValueKind.ShouldBe(JsonValueKind.Object);

            _log.Start();
            var chain = await GetAsync(client, $"{Root}/cases/{asked}/chain");
            var commands = _log.Stop();

            chain.GetProperty("caseIds").GetArrayLength().ShouldBe(length);
            commands.Count(command => command.Contains("process_cases", StringComparison.Ordinal)).ShouldBeLessThanOrEqualTo(3);
            commands.ShouldNotContain(command => command.Contains(StepsTable, StringComparison.Ordinal));
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Measuring
    // ────────────────────────────────────────────────────────────

    private WebApplicationFactory<Program> CountingHost() => _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
        services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(_log));

        services.RemoveAll<ProcessInlineReconcile>();
        services.AddSingleton(provider => new ProcessInlineReconcile(provider.GetRequiredService<IServiceScopeFactory>(), new FrozenClock()));
    }));

    private async Task<Dictionary<string, Measured>> MeasureAsync(HttpClient client)
    {
        var measured = new Dictionary<string, Measured>();

        foreach (var read in Reads)
        {
            (await GetAsync(client, read.Route)).ValueKind.ShouldBe(JsonValueKind.Object, "the warm-up of " + read.Route);

            _log.Start();
            var body = await GetAsync(client, read.Route);
            measured[read.Route] = new Measured(_log.Stop(), read.Size(body));
        }

        return measured;
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        response.IsSuccessStatusCode.ShouldBeTrue(url + " → " + text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static int BlockedTasks(JsonElement my)
        => SectionsOfBlockedTasks
            .SelectMany(section => my.GetProperty(section).EnumerateArray())
            .Count(task => task.GetProperty("state").GetString() == "Blocked"
                && GatesOfTheSet.Contains(task.GetProperty("gateCode").GetString()));

    private static int Cards(JsonElement board)
        => board.GetProperty("columns").EnumerateArray().Sum(column => column.GetProperty("cards").GetArrayLength());

    /// <summary>Which statements ran how often at each size: the failure names the query that loops.</summary>
    private static string Difference(string route, Measured few, Measured many)
    {
        var fewByText = few.Commands.CountBy(Shortened).ToDictionary(pair => pair.Key, pair => pair.Value);
        var lines = many.Commands.CountBy(Shortened)
            .Where(pair => fewByText.GetValueOrDefault(pair.Key) != pair.Value)
            .Select(pair => $"  {fewByText.GetValueOrDefault(pair.Key)} → {pair.Value}: {pair.Key}");

        return $"{route}: {few.Commands.Count} → {many.Commands.Count} commands{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private static string Shortened(string command)
    {
        var line = command.ReplaceLineEndings(" ");
        return line.Length <= 160 ? line : line[..160];
    }

    // ────────────────────────────────────────────────────────────
    //  Data (the shape of ProcessReadApiTests' helpers), all through the counting host
    // ────────────────────────────────────────────────────────────

    private static async Task PrepareAsync(WebApplicationFactory<Program> host)
    {
        await DbAsync(host, db => db.Database.ExecuteSqlAsync($"DELETE FROM blank_role_assignments", Ct));
        await DbAsync(host, db => ProcessTestBlanks.EnsureStandInsAsync(db, Ct));
        await ProcessTestGates.EnforceAsync(host.Services, Ct);
        await DbAsync(host, async db =>
        {
            var value = Today(host).ToString(SystemSettingKeys.ProcessesStartedOnFormat, System.Globalization.CultureInfo.InvariantCulture);
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
    }

    /// <summary><paramref name="each"/> blocked on Q03, on Q04, on Q12, and that many vacancy requests.</summary>
    private static async Task SeedAsync(WebApplicationFactory<Program> host, int each)
    {
        var today = Today(host);
        var formalised = new List<Guid>();
        var candidates = new List<Guid>();
        var leaving = new List<Hire>();

        for (var index = 0; index < each; index++)
        {
            formalised.Add((await HireAsync(host, "READ-" + Suffix())).EmploymentId);
            candidates.Add(await CandidateAsync(host));
            leaving.Add(await LeavingAsync(host));
            await VacancyRequestAsync(host);
        }

        await RunAsync(host);

        var since = today.AddDays(-7);
        foreach (var employment in formalised)
        {
            var processCase = await CaseAsync(host, ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(employment));
            await DbAsync(host, db => db.Database.ExecuteSqlAsync($"UPDATE process_cases SET started_on = {since} WHERE id = {processCase.Id}", Ct));
        }

        var by = await UserIdAsync(host);
        foreach (var candidate in candidates)
        {
            var processCase = await CaseAsync(host, ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, today));
            await CloseManuallyAsync(host, processCase.Id, "r02-10", by, """{"decision":"accept"}""");
            await CloseManuallyAsync(host, processCase.Id, ProcessStepIds.Offer, by, $$"""{"{{ProcessStepPayload.StartDateInput}}":"{{today.AddDays(10):yyyy-MM-dd}}"}""");
            await CloseManuallyAsync(host, processCase.Id, "r02-12", by, payload: null);
        }

        foreach (var hire in leaving)
        {
            await TerminateAsync(host, hire, today);
        }

        await RunAsync(host);

        foreach (var employment in formalised)
        {
            await ShouldBeBlockedAsync(host, ProcessCodes.HireFormalise, ProcessSubjectRefs.FormaliseFromEmployment(employment), ProcessStepIds.PayrollHandover, ProcessGateCodes.Q03);
        }

        foreach (var candidate in candidates)
        {
            await ShouldBeBlockedAsync(host, ProcessCodes.HireCandidate, ProcessSubjectRefs.Candidate(candidate, today), ProcessStepIds.FirstDay, ProcessGateCodes.Q04);
        }

        foreach (var hire in leaving)
        {
            await ShouldBeBlockedAsync(host, ProcessCodes.Dismissal, ProcessSubjectRefs.Dismissal(hire.EmploymentId), "ex-10", ProcessGateCodes.Q12);
        }
    }

    private static async Task ShouldBeBlockedAsync(WebApplicationFactory<Program> host, string process, string subjectRef, string stepCode, string gate)
    {
        var step = (await CaseAsync(host, process, subjectRef)).FindStep(stepCode).ShouldNotBeNull();

        step.State.ShouldBe(ProcessStepState.Blocked, stepCode);
        step.BlockedGateCode.ShouldBe(gate, stepCode);
    }

    private static DateOnly Today(WebApplicationFactory<Program> host) => host.Services.GetRequiredService<IClock>().Today;

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    private static async Task RunAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ProcessReconciler>().RunAsync(Ct);
        result.Status.ShouldNotBe(ProcessReconcileStatus.Dormant);
    }

    private static async Task<T> DbAsync<T>(WebApplicationFactory<Program> host, Func<AppDbContext, Task<T>> work)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static Task<ProcessCase> CaseAsync(WebApplicationFactory<Program> host, string process, string subjectRef)
        => DbAsync(host, db => db.ProcessCases.AsNoTracking()
            .Include(row => row.Steps)
            .SingleAsync(row => row.ProcessCode == process && row.SubjectRef == subjectRef, Ct));

    private static Task<int> CloseManuallyAsync(WebApplicationFactory<Program> host, Guid caseId, string stepCode, Guid by, string? payload) => DbAsync(host, async db =>
    {
        var row = await db.ProcessCases.Include(processCase => processCase.Steps).SingleAsync(processCase => processCase.Id == caseId, Ct);
        var now = DateTimeOffset.UtcNow;

        row.ActivateStep(stepCode, now).IsSuccess.ShouldBeTrue();
        row.CompleteStepManually(stepCode, by, payload, evidenceSha256: null, now).IsSuccess.ShouldBeTrue();

        return await db.SaveChangesAsync(Ct);
    });

    private static Task<Guid> UserIdAsync(WebApplicationFactory<Program> host) => DbAsync(host, async db =>
    {
        var phone = PhoneNumber.Create("90" + Random.Shared.Next(1_000_000, 9_999_999)).Value;
        var user = User.Create(phone, "test-hash", FullName.Create("Foydalanuvchi" + Suffix(), "Test", null).Value).Value;
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    });

    private static Task<Guid> CandidateAsync(WebApplicationFactory<Program> host) => DbAsync(host, async db =>
    {
        var vacancy = Vacancy.Open("Engine vacancy " + Suffix(), Today(host)).Value;
        db.Vacancies.Add(vacancy);
        var candidate = Candidate.Register(FullName.Create("Motorov" + Suffix(), "Test", null).Value, vacancy.Id).Value;
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(Ct);
        return candidate.Id;
    });

    private static Task<int> VacancyRequestAsync(WebApplicationFactory<Program> host) => DbAsync(host, db =>
    {
        var department = Department.Create("Engine department " + Suffix()).Value;
        db.Departments.Add(department);
        var requester = Employee.Create(FullName.Create("Sorovchi" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(requester);
        db.VacancyRequests.Add(VacancyRequest.Create(department.Id, requester.Id, "Engine position", 1, neededBy: null, note: null).Value);
        return db.SaveChangesAsync(Ct);
    });

    private static Task<Hire> HireAsync(WebApplicationFactory<Program> host, string? orderNo) => DbAsync(host, async db =>
    {
        var legalEntity = LegalEntity.Create("Engine MChJ " + Suffix()).Value;
        db.LegalEntities.Add(legalEntity);
        var employee = Employee.Create(FullName.Create("Xodimov" + Suffix(), "Test", null).Value).Value;
        db.Employees.Add(employee);
        var employment = Employment.Hire(employee.Id, legalEntity.Id, Today(host), orderNo: orderNo).Value;
        db.Employments.Add(employment);
        await db.SaveChangesAsync(Ct);
        return new Hire(employee.Id, employment.Id);
    });

    /// <summary>A hire with a dismissal act as a draft: a pass opens the case, the termination then blocks ex-10.</summary>
    private static async Task<Hire> LeavingAsync(WebApplicationFactory<Program> host)
    {
        var hire = await HireAsync(host, orderNo: null);

        await DbAsync(host, async db =>
        {
            var template = await db.BlankTemplates.SingleAsync(row => row.Code == ProcessBlanks.F09, Ct);
            var version = await db.BlankTemplateVersions
                .SingleAsync(row => row.TemplateId == template.Id && row.Status == BlankTemplateVersionStatus.Published, Ct);

            db.BlankSubmissions.Add(BlankSubmission.Create(
                template,
                version,
                new BlankSubject(hire.EmployeeId, hire.EmploymentId),
                new BlankAnswersState(F09Dismissal, "{}", "[]", 0, null)).Value);

            return await db.SaveChangesAsync(Ct);
        });

        return hire;
    }

    private static Task<int> TerminateAsync(WebApplicationFactory<Program> host, Hire hire, DateOnly on) => DbAsync(host, async db =>
    {
        var employment = await db.Employments.SingleAsync(row => row.Id == hire.EmploymentId, Ct);
        employment.Terminate(on, reasonId: null).IsSuccess.ShouldBeTrue();
        return await db.SaveChangesAsync(Ct);
    });

    private sealed record Hire(Guid EmployeeId, Guid EmploymentId);

    private sealed record Read(string Route, Func<JsonElement, int> Size);

    private sealed record Measured(IReadOnlyList<string> Commands, int Size);

    /// <summary>Frozen, so <see cref="ProcessInlineReconcile"/> never sees its window end.</summary>
    private sealed class FrozenClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;

        public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    }

    /// <summary>Every command EF prepares while recording: readers, scalars and non-queries, sync or async.</summary>
    private sealed class CommandLog : DbCommandInterceptor
    {
        private ConcurrentQueue<string>? _recording;

        public void Start() => Volatile.Write(ref _recording, new ConcurrentQueue<string>());

        public IReadOnlyList<string> Stop() => [.. Interlocked.Exchange(ref _recording, null) ?? []];

        public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
        {
            Volatile.Read(ref _recording)?.Enqueue(result.CommandText);
            return result;
        }
    }
}
