using System.Security.Claims;
using Bonvi.Api.Startup;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Hangfire;
using Hangfire.Common;
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

    [Fact]
    public async Task Slow_pass_does_not_hold_the_reader_and_does_not_end_with_its_request()
    {
        await StartEngineAsync();
        var slow = new HeldLock();
        await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services
            => services.AddScoped<IProcessEngineLock>(_ => slow)));
        var inline = new ProcessInlineReconcile(host.Services.GetRequiredService<IServiceScopeFactory>(), new SteppingClock(DateTimeOffset.UtcNow));
        using var request = new CancellationTokenSource();

        try
        {
            var waited = System.Diagnostics.Stopwatch.StartNew();
            await inline.BeforeReadAsync(request.Token);
            waited.Stop();

            (await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct)).ShouldBeTrue();
            waited.Elapsed.ShouldBeLessThan(ProcessInlineReconcile.ReaderWait + TimeSpan.FromSeconds(5));
            slow.Running.ShouldBeTrue("the pass is still inside the lock");

            await request.CancelAsync();
            slow.PassToken.IsCancellationRequested.ShouldBeFalse("the pass works for everyone, not for this request");
        }
        finally
        {
            slow.Release();
        }
    }

    [Fact]
    public async Task Reader_leaves_a_slow_pass_on_its_own_cancellation()
    {
        await StartEngineAsync();
        var slow = new HeldLock();
        await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services
            => services.AddScoped<IProcessEngineLock>(_ => slow)));
        var inline = new ProcessInlineReconcile(host.Services.GetRequiredService<IServiceScopeFactory>(), new SteppingClock(DateTimeOffset.UtcNow));
        using var request = new CancellationTokenSource();

        try
        {
            var reading = inline.BeforeCaseReadAsync(Guid.NewGuid(), request.Token);
            await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            await request.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => reading);
            slow.Running.ShouldBeTrue();
        }
        finally
        {
            slow.Release();
        }
    }

    [Fact]
    public async Task Pass_that_runs_out_of_its_timeout_is_journalled_and_the_read_goes_on()
    {
        await StartEngineAsync();
        var slow = new HeldLock();
        var journal = new RecordingJournal();
        await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IProcessEngineLock>(_ => slow);
            services.AddSingleton<IProcessEngineJournal>(journal);
        }));
        var inline = new ProcessInlineReconcile(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            new SteppingClock(DateTimeOffset.UtcNow),
            passTimeout: TimeSpan.FromMilliseconds(200));

        await inline.BeforeReadAsync(Ct);

        journal.Failures.ShouldHaveSingleItem().ShouldBeOfType<TimeoutException>();
        slow.Running.ShouldBeFalse("the pass ended with its own timeout");
    }

    [Fact]
    public void Job_is_not_retried_by_Hangfire()
    {
        var job = Job.FromExpression<ProcessReconcileHangfireJob>(hangfire => hangfire.RunAsync(CancellationToken.None));

        var retries = JobFilterProviders.Providers.GetFilters(job).Select(filter => filter.Instance).OfType<AutomaticRetryAttribute>();

        retries.ShouldHaveSingleItem().Attempts.ShouldBe(0, "the next run comes five minutes later");
    }

    [Fact]
    public async Task Hangfire_wrapper_runs_a_full_pass_as_Hangfire_activates_it()
    {
        await StartEngineAsync();
        var request = await VacancyRequestAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        await ActivatorUtilities.GetServiceOrCreateInstance<ProcessReconcileHangfireJob>(scope.ServiceProvider).RunAsync(Ct);

        (await FindCaseAsync(ProcessCodes.HireRequest, ProcessSubjectRefs.VacancyRequest(request.Id))).ShouldNotBeNull();
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

    /// <summary>A pass stays inside it until released or until its token ends; then it finds the lock taken.</summary>
    private sealed class HeldLock : IProcessEngineLock
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken PassToken { get; private set; }

        public bool Running { get; private set; }

        public void Release() => _release.TrySetResult();

        public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
        {
            PassToken = cancellationToken;
            Running = true;
            Entered.TrySetResult(true);

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return false;
            }
            finally
            {
                Running = false;
            }
        }
    }

    /// <summary>A pass cannot get past it: the same failure wherever the pass is entered from.</summary>
    private sealed class BrokenLock : IProcessEngineLock
    {
        public Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("The engine lock is broken on purpose.");
    }
}
