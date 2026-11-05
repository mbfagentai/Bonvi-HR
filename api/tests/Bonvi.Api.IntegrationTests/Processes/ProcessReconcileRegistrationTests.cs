using Bonvi.Api.Startup;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// The schedule itself, not the job: with <c>ProcessReconcileJob</c> scheduled directly a pass would still run, but
/// with Hangfire's default retries, which only journal the same failure again beside the next run.
/// </summary>
public sealed class ProcessReconcileRegistrationTests
{
    [Fact]
    public async Task The_schedule_runs_the_wrapper_that_has_no_retries()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        var manager = new RecordingJobManager();
        builder.Services.AddSingleton<IRecurringJobManager>(manager);
        await using var app = builder.Build();

        app.UseBonviRecurringJobs();

        var (job, cron) = manager.Added["processes:reconcile"];
        job.Type.ShouldBe(typeof(ProcessReconcileHangfireJob));
        job.Method.Name.ShouldBe(nameof(ProcessReconcileHangfireJob.RunAsync));
        cron.ShouldBe("4-59/5 * * * *");
    }

    private sealed class RecordingJobManager : IRecurringJobManager
    {
        public Dictionary<string, (Job Job, string Cron)> Added { get; } = new(StringComparer.Ordinal);

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
            => Added[recurringJobId] = (job, cronExpression);

        public void Trigger(string recurringJobId)
        {
        }

        public void RemoveIfExists(string recurringJobId)
        {
        }
    }
}
