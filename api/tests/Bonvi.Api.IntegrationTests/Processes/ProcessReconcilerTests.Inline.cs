using System.Security.Claims;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>The two ways a pass is run (§7.4): the Hangfire job and the wrapper in front of reads.</summary>
public sealed partial class ProcessReconcilerTests
{
    [Fact]
    public async Task Job_runs_a_full_pass_from_DI()
    {
        await StartEngineAsync();
        var request = await VacancyRequestAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessReconcileJob>().RunAsync(Ct);

        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Failed_pass_reaches_the_job_and_stays_outside_the_read_and_is_journalled_once_each()
    {
        await StartEngineAsync();
        var journal = new RecordingJournal();
        await using var failing = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IProcessEngineLock, BrokenLock>();
            services.AddSingleton<IProcessEngineJournal>(journal);
        }));

        await failing.Services.GetRequiredService<ProcessInlineReconcile>().BeforeReadAsync(Ct);
        journal.Failures.Count.ShouldBe(1);

        await failing.Services.GetRequiredService<ProcessInlineReconcile>().BeforeCaseReadAsync(Guid.NewGuid(), Ct);
        journal.Failures.Count.ShouldBe(2);

        await using var scope = failing.Services.CreateAsyncScope();
        await Should.ThrowAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ProcessReconcileJob>().RunAsync(Ct));
        journal.Failures.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Inline_pass_runs_once_per_window()
    {
        await StartEngineAsync();
        var clock = new SteppingClock(DateTimeOffset.UtcNow);
        var inline = new ProcessInlineReconcile(_factory.Services.GetRequiredService<IServiceScopeFactory>(), clock);

        var first = await VacancyRequestAsync();
        await inline.BeforeReadAsync(Ct);
        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(first.Id))).ShouldNotBeNull();

        var second = await VacancyRequestAsync();
        clock.Now += ProcessInlineReconcile.ThrottleWindow - TimeSpan.FromSeconds(1);
        await inline.BeforeReadAsync(Ct);
        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(second.Id))).ShouldBeNull("still inside the window");

        clock.Now += TimeSpan.FromSeconds(1);
        await inline.BeforeReadAsync(Ct);
        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(second.Id))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Inline_pass_is_not_recorded_as_the_user_who_opened_the_page()
    {
        await StartEngineAsync();
        var inline = new ProcessInlineReconcile(_factory.Services.GetRequiredService<IServiceScopeFactory>(), new SteppingClock(DateTimeOffset.UtcNow));
        var accessor = _factory.Services.GetRequiredService<IHttpContextAccessor>();
        var viewer = await UserIdAsync();

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, viewer.ToString())], "test")),
        };

        try
        {
            var byInline = await VacancyRequestAsync();
            await inline.BeforeReadAsync(Ct);
            (await CaseAuditUserAsync(ProcessSubjectRefs.VacancyRequest(byInline.Id))).ShouldBeNull();

            // The control: without the wrapper the same pass does take the viewer's name, so the test can fail.
            var direct = await VacancyRequestAsync();
            await RunAsync();
            (await CaseAuditUserAsync(ProcessSubjectRefs.VacancyRequest(direct.Id))).ShouldBe(viewer);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    [Fact]
    public async Task Cancelled_read_stays_cancelled()
    {
        await StartEngineAsync();
        var inline = new ProcessInlineReconcile(_factory.Services.GetRequiredService<IServiceScopeFactory>(), new SteppingClock(DateTimeOffset.UtcNow));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => inline.BeforeReadAsync(cancelled.Token));
    }

    private async Task<Guid?> CaseAuditUserAsync(string subjectRef)
    {
        var processCase = await CaseAsync(ProcessCodes.HireRequest, subjectRef);
        var id = processCase.Id.ToString();

        return await DbAsync(db => db.AuditLogs.AsNoTracking()
            .Where(row => row.EntityId == id)
            .Select(row => row.UserId)
            .FirstAsync(Ct));
    }

    private sealed class SteppingClock(DateTimeOffset start) : IClock
    {
        public DateTimeOffset Now { get; set; } = start;

        public DateTimeOffset UtcNow => Now;

        public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);
    }

    /// <summary>A pass cannot get past it: the same failure wherever the pass is entered from.</summary>
    private sealed class BrokenLock : IProcessEngineLock
    {
        public Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("The engine lock is broken on purpose.");
    }
}
