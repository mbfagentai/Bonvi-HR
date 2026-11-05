using Bonvi.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// The pass in front of a process read (docs/SPEC-mening-ishlarim.md §7.4). Whatever happens to the pass, the read
/// goes on and serves the state the last pass left.
/// </summary>
/// <remarks>
/// <para>
/// <b>The slot is taken before the pass, not after it.</b> Readers arriving during a pass do not wait for it or
/// queue behind it, and a pass that failed is not retried by every read: one attempt per
/// <see cref="ThrottleWindow"/>, with the five-minute job behind it. One API process runs, so memory is enough.
/// </para>
/// <para>
/// <b>Its own scope</b>, so a failed pass cannot leave anything behind in the read's change tracker or transaction,
/// and the read sees what the pass committed.
/// </para>
/// <para>
/// <b>Its own execution context.</b> <c>HttpContext</c> flows through an <c>AsyncLocal</c>: with it, whoever happened
/// to open the page would be recorded as the author of every row the engine writes. Suppressed, the pass is
/// «the system», exactly as in the job.
/// </para>
/// <para>
/// A failure is caught here and not logged: <see cref="ProcessReconciler"/> has journalled it already. A failure to
/// resolve the reconciler never reaches that journal, but the job resolves the same graph and Hangfire records it.
/// </para>
/// </remarks>
public sealed class ProcessInlineReconcile
{
    /// <summary>§7.4: at most one full pass in this window, however many reads arrive.</summary>
    public static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private long _nextPassAtTicks;

    public ProcessInlineReconcile(IServiceScopeFactory scopes, IClock clock)
    {
        _scopes = scopes;
        _clock = clock;
    }

    /// <summary>In front of a list or a dashboard read: a full pass, unless one ran within the window.</summary>
    public Task BeforeReadAsync(CancellationToken cancellationToken = default)
        => TryTakeSlot()
            ? RunSheltered(reconciler => reconciler.RunAsync(cancellationToken), cancellationToken)
            : Task.CompletedTask;

    /// <summary>In front of a case detail: that one case, every time. It is cheap and has no window (§7.4).</summary>
    public Task BeforeCaseReadAsync(Guid caseId, CancellationToken cancellationToken = default)
        => RunSheltered(reconciler => reconciler.RunCaseAsync(caseId, cancellationToken), cancellationToken);

    private bool TryTakeSlot()
    {
        var now = _clock.UtcNow.UtcTicks;
        var next = Interlocked.Read(ref _nextPassAtTicks);

        return now >= next
            && Interlocked.CompareExchange(ref _nextPassAtTicks, now + ThrottleWindow.Ticks, next) == next;
    }

    private async Task RunSheltered(Func<ProcessReconciler, Task> pass, CancellationToken cancellationToken)
    {
        Task running;
        using (ExecutionContext.SuppressFlow())
        {
            running = Task.Run(
                async () =>
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await pass(scope.ServiceProvider.GetRequiredService<ProcessReconciler>()).ConfigureAwait(false);
                },
                cancellationToken);
        }

        try
        {
            await running.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Journalled by the reconciler; the read goes on (see the remarks).
        }
    }
}
