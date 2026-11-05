using Bonvi.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Application.Processes.Engine;

/// <summary>
/// The pass in front of a process read (docs/SPEC-mening-ishlarim.md §7.4). Whatever happens to the pass, the read
/// goes on and serves the state the last pass left: a slow pass costs a reader <see cref="ReaderWait"/> at most.
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
/// <b>Its own clock.</b> A pass works for everyone, so it does not end with the request that happened to start it: it
/// runs under <see cref="PassTimeout"/> and no other token. A reader only stops waiting.
/// </para>
/// <para>
/// A failure is caught here and not logged: <see cref="ProcessReconciler"/> has journalled it already. The reconciler
/// takes the token it was given for the caller's, so running out of <see cref="PassTimeout"/> is journalled here. A
/// failure to resolve the reconciler never reaches that journal, but the job resolves the same graph and Hangfire
/// records it.
/// </para>
/// </remarks>
public sealed class ProcessInlineReconcile
{
    /// <summary>§7.4: at most one full pass in this window, however many reads arrive.</summary>
    public static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(60);

    /// <summary>How long a pass in front of a read may run; the five-minute job has no such limit.</summary>
    public static readonly TimeSpan PassTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a reader waits for that pass before serving the current state.</summary>
    public static readonly TimeSpan ReaderWait = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly TimeSpan _passTimeout;
    private long _nextPassAtTicks;

    /// <param name="scopes">Each pass gets its own.</param>
    /// <param name="clock">The window.</param>
    /// <param name="passTimeout"><see cref="PassTimeout"/> unless a test needs to see it run out.</param>
    public ProcessInlineReconcile(IServiceScopeFactory scopes, IClock clock, TimeSpan? passTimeout = null)
    {
        _scopes = scopes;
        _clock = clock;
        _passTimeout = passTimeout ?? PassTimeout;
    }

    /// <summary>In front of a list or a dashboard read: a full pass, unless one ran within the window.</summary>
    public Task BeforeReadAsync(CancellationToken cancellationToken = default)
    {
        // A read already abandoned does not spend the window.
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return TryTakeSlot()
            ? RunShelteredAsync((reconciler, passToken) => reconciler.RunAsync(passToken), cancellationToken)
            : Task.CompletedTask;
    }

    /// <summary>In front of a case detail: that one case, every time. It is cheap and has no window (§7.4).</summary>
    public Task BeforeCaseReadAsync(Guid caseId, CancellationToken cancellationToken = default)
        => RunShelteredAsync((reconciler, passToken) => reconciler.RunCaseAsync(caseId, passToken), cancellationToken);

    /// <summary>
    /// §10 #18: a full pass now, past the throttle, and its answer. Not sheltered: this is a diagnostic, and a failure
    /// must reach whoever asked. Still «the system» to the audit, whoever clicked.
    /// </summary>
    public async Task<ProcessReconcileResult> RunNowAsync(CancellationToken cancellationToken = default)
    {
        Task<ProcessReconcileResult> running;
        using (ExecutionContext.SuppressFlow())
        {
            running = Task.Run(
                async () =>
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    return await scope.ServiceProvider.GetRequiredService<ProcessReconciler>().RunAsync(cancellationToken).ConfigureAwait(false);
                },
                cancellationToken);
        }

        return await running.ConfigureAwait(false);
    }

    private bool TryTakeSlot()
    {
        var now = _clock.UtcNow.UtcTicks;
        var next = Interlocked.Read(ref _nextPassAtTicks);

        return now >= next
            && Interlocked.CompareExchange(ref _nextPassAtTicks, now + ThrottleWindow.Ticks, next) == next;
    }

    private async Task RunShelteredAsync(Func<ProcessReconciler, CancellationToken, Task> pass, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task running;
        using (ExecutionContext.SuppressFlow())
        {
            running = Task.Run(() => RunWithinTimeoutAsync(pass), CancellationToken.None);
        }

        try
        {
            await running.WaitAsync(ReaderWait, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The pass goes on without this reader, which serves the current state.
        }
    }

    /// <summary>Never faults: the reader that could observe it may be long gone.</summary>
    private async Task RunWithinTimeoutAsync(Func<ProcessReconciler, CancellationToken, Task> pass)
    {
        using var timeout = new CancellationTokenSource(_passTimeout);

        try
        {
            await using var scope = _scopes.CreateAsyncScope();

            try
            {
                await pass(scope.ServiceProvider.GetRequiredService<ProcessReconciler>(), timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
            {
                scope.ServiceProvider.GetRequiredService<IProcessEngineJournal>().PassFailed(
                    new TimeoutException($"The pass in front of a read ran out of its {_passTimeout.TotalSeconds:0.#} s.", exception));
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Journalled by the reconciler; the read goes on (see the remarks).
        }
    }
}
