namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// Hangfire <c>processes:reconcile</c> (docs/SPEC-mening-ishlarim.md §7.4, hook H15): one full pass every five minutes.
/// </summary>
/// <remarks>
/// A pass that finds the lock taken or the engine dormant simply ends; the next run comes. A failed pass is journalled
/// by the reconciler and reaches Hangfire on purpose, so the failure stays recorded against the job.
/// </remarks>
public sealed class ProcessReconcileJob
{
    private readonly ProcessReconciler _reconciler;

    public ProcessReconcileJob(ProcessReconciler reconciler) => _reconciler = reconciler;

    /// <summary>One pass.</summary>
    /// <param name="cancellationToken">Cancellation token, supplied by Hangfire.</param>
    public Task RunAsync(CancellationToken cancellationToken)
        => _reconciler.RunAsync(cancellationToken);
}
