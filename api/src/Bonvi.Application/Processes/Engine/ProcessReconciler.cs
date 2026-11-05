using System.Globalization;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Processes.Engine;

/// <summary>How a run ended.</summary>
public enum ProcessReconcileStatus
{
    /// <summary>The engine's start date is not set: nothing was read or written (§7.6).</summary>
    Dormant,

    /// <summary>Another pass holds the lock. Not an error: whoever asked serves the current state (§7.4).</summary>
    Busy,

    Finished,
}

/// <param name="Status">How the run ended.</param>
/// <param name="Started">Cases started or reopened.</param>
/// <param name="ChangedRows">Rows written; 0 = no signal fired.</param>
public sealed record ProcessReconcileResult(ProcessReconcileStatus Status, int Started, int ChangedRows);

/// <summary>
/// The engine's pass (docs/SPEC-mening-ishlarim.md §7.3–§7.4): one transaction under the global lock.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything is read inside the transaction.</b> The retrying execution strategy may run the work again from
/// the start with the change tracker cleared, so nothing tracked may come in from outside it.
/// </para>
/// <para>
/// <b>The saves between the stages are needed, not tidiness.</b> The scans and batch lookups read the database:
/// (b) has to see the employments linked in the stage before it, or one hire gets two formalise cases (§3.2). All
/// of them commit together. A failure rolls the whole pass back and the next pass starts over; the journal gets
/// it, and the exception goes on to the caller, who decides whether it may break anything (a read must not, a job
/// wants the failure recorded).
/// </para>
/// <para>
/// <b>A pass that changed nothing writes nothing</b>, the watermark included: <c>IDataChangeSignal</c> fires on any
/// saved row, and a watermark per pass would wake every open screen every five minutes (§7.4 p.6).
/// </para>
/// </remarks>
public sealed class ProcessReconciler
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessEngineLock _lock;
    private readonly IProcessEngineJournal _journal;
    private readonly SystemSettingsReader _settingsReader;
    private readonly ISystemSettingRepository _settings;
    private readonly ProcessEnginePassLoader _passLoader;
    private readonly IProcessCaseRepository _cases;
    private readonly ProcessStarters _starters;
    private readonly ProcessCaseBatch _batch;

    public ProcessReconciler(
        IUnitOfWork unitOfWork,
        IProcessEngineLock engineLock,
        IProcessEngineJournal journal,
        SystemSettingsReader settingsReader,
        ISystemSettingRepository settings,
        ProcessEnginePassLoader passLoader,
        IProcessCaseRepository cases,
        ProcessStarters starters,
        ProcessCaseBatch batch)
    {
        _unitOfWork = unitOfWork;
        _lock = engineLock;
        _journal = journal;
        _settingsReader = settingsReader;
        _settings = settings;
        _passLoader = passLoader;
        _cases = cases;
        _starters = starters;
        _batch = batch;
    }

    /// <summary>A full pass: every starter, every Active case.</summary>
    public Task<ProcessReconcileResult> RunAsync(CancellationToken cancellationToken = default)
        => InPassAsync(FullPassAsync, cancellationToken);

    /// <summary>
    /// One case under the same lock (§7.4, opening a case). A case that is not Active, or does not exist, is left
    /// alone. An offer accepted here starts its formalise case here: if this pass also completes the parent, no
    /// later pass would look at it again.
    /// </summary>
    public Task<ProcessReconcileResult> RunCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
        => InPassAsync((pass, token) => CasePassAsync(caseId, pass, token), cancellationToken);

    private async Task<ProcessReconcileResult> InPassAsync(
        Func<ProcessEnginePass, CancellationToken, Task<ProcessReconcileResult>> work,
        CancellationToken cancellationToken)
    {
        var settings = ProcessEngineSettings.From(await _settingsReader.ReadAsync(cancellationToken).ConfigureAwait(false));
        if (settings.StartedOn is not { } startedOn)
        {
            return new ProcessReconcileResult(ProcessReconcileStatus.Dormant, 0, 0);
        }

        try
        {
            var result = await _unitOfWork
                .ExecuteInTransactionAsync(
                    async token =>
                    {
                        if (!await _lock.TryAcquireAsync(token).ConfigureAwait(false))
                        {
                            return new ProcessReconcileResult(ProcessReconcileStatus.Busy, 0, 0);
                        }

                        var pass = await _passLoader.LoadAsync(startedOn, settings, token).ConfigureAwait(false);
                        var done = await work(pass, token).ConfigureAwait(false);

                        return done.ChangedRows > 0
                            ? done with { ChangedRows = done.ChangedRows + await WriteWatermarkAsync(pass, token).ConfigureAwait(false) }
                            : done;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.Status == ProcessReconcileStatus.Finished)
            {
                _journal.PassFinished(result.Started, result.ChangedRows);
            }

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _journal.PassFailed(exception);
            throw;
        }
    }

    /// <summary>The order of §7.3: (a) links its employments before (b) scans.</summary>
    private async Task<ProcessReconcileResult> FullPassAsync(ProcessEnginePass pass, CancellationToken cancellationToken)
    {
        var active = (await _cases.ListActiveWithStepsAsync(cancellationToken).ConfigureAwait(false)).ToList();

        var requests = await _starters.StartRequestsAsync(pass, cancellationToken).ConfigureAwait(false);
        active.AddRange(requests);
        var candidates = await _starters.StartCandidatesAsync(pass, active, cancellationToken).ConfigureAwait(false);
        active.AddRange(candidates);

        var rows = await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var offerAccepted = await _batch
            .ReconcileAsync(pass, [.. active.Where(processCase => processCase.Status == ProcessCaseStatus.Active)], cancellationToken)
            .ConfigureAwait(false);
        rows += await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var formalise = await _starters.StartFormaliseFromOffersAsync(pass, offerAccepted, cancellationToken).ConfigureAwait(false);
        rows += await SaveAndReconcileAsync(pass, formalise, cancellationToken).ConfigureAwait(false);
        active.AddRange(formalise);

        var direct = await _starters.StartFormaliseFromHiresAsync(pass, active, cancellationToken).ConfigureAwait(false);
        var probations = await _starters.StartProbationsAsync(pass, cancellationToken).ConfigureAwait(false);
        var dismissals = await _starters.StartDismissalsAsync(pass, cancellationToken).ConfigureAwait(false);
        rows += await SaveAndReconcileAsync(pass, [.. direct, .. probations, .. dismissals], cancellationToken).ConfigureAwait(false);

        var started = requests.Count + candidates.Count + formalise.Count + direct.Count + probations.Count + dismissals.Count;

        return new ProcessReconcileResult(ProcessReconcileStatus.Finished, started, rows);
    }

    private async Task<ProcessReconcileResult> CasePassAsync(Guid caseId, ProcessEnginePass pass, CancellationToken cancellationToken)
    {
        var processCase = await _cases.GetWithStepsAsync(caseId, cancellationToken).ConfigureAwait(false);
        if (processCase is not { Status: ProcessCaseStatus.Active })
        {
            return new ProcessReconcileResult(ProcessReconcileStatus.Finished, 0, 0);
        }

        var offerAccepted = await _batch.ReconcileAsync(pass, [processCase], cancellationToken).ConfigureAwait(false);
        var rows = await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var formalise = await _starters.StartFormaliseFromOffersAsync(pass, offerAccepted, cancellationToken).ConfigureAwait(false);
        rows += await SaveAndReconcileAsync(pass, formalise, cancellationToken).ConfigureAwait(false);

        return new ProcessReconcileResult(ProcessReconcileStatus.Finished, formalise.Count, rows);
    }

    private async Task<int> SaveAndReconcileAsync(ProcessEnginePass pass, IReadOnlyList<ProcessCase> cases, CancellationToken cancellationToken)
    {
        var rows = await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Its accepted offers are ignored: hire-formalise, probation and dismissal cases have no offer step.
        await _batch.ReconcileAsync(pass, cases, cancellationToken).ConfigureAwait(false);

        return rows + await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>§6.6: diagnostics only, the time of the last pass that changed something.</summary>
    private async Task<int> WriteWatermarkAsync(ProcessEnginePass pass, CancellationToken cancellationToken)
    {
        var value = pass.At.ToString("O", CultureInfo.InvariantCulture);
        var row = await _settings.FindAsync(SystemSettingKeys.ProcessesReconcileWatermark, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            var created = SystemSetting.CreateServerValue(SystemSettingKeys.ProcessesReconcileWatermark, value);
            if (created.IsFailure)
            {
                throw new InvalidOperationException($"The engine watermark was refused: {created.Error.Code}");
            }

            _settings.Add(created.Value);
        }
        else
        {
            var updated = row.SetServerValue(value);
            if (updated.IsFailure)
            {
                throw new InvalidOperationException($"The engine watermark was refused: {updated.Error.Code}");
            }
        }

        return await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
