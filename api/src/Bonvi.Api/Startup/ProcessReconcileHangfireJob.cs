using Bonvi.Application.Processes.Engine;
using Hangfire;

namespace Bonvi.Api.Startup;

/// <summary>
/// What Hangfire runs for <c>processes:reconcile</c>. It exists for the attribute alone: the job itself lives in the
/// Application layer, which does not know Hangfire.
/// </summary>
/// <remarks>
/// No retries: the next run comes five minutes later and does the same work over fresh data, so Hangfire's default
/// retries would only journal the same failure again beside it. A failed run still stays recorded as Failed.
/// </remarks>
[AutomaticRetry(Attempts = 0)]
public sealed class ProcessReconcileHangfireJob
{
    private readonly ProcessReconcileJob _job;

    public ProcessReconcileHangfireJob(ProcessReconcileJob job) => _job = job;

    /// <summary>One pass.</summary>
    /// <param name="cancellationToken">Cancellation token, supplied by Hangfire.</param>
    public Task RunAsync(CancellationToken cancellationToken) => _job.RunAsync(cancellationToken);
}
