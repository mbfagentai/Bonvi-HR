using System.Text.Json.Nodes;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Queries;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Commands;

/// <summary>POST <c>/api/processes/cases</c> (§10 #9).</summary>
/// <param name="ProcessCode">Only <c>dismissal</c> in Phase 1.</param>
/// <param name="EmploymentId">Whose dismissal.</param>
/// <param name="LastWorkDay">The case's anchor date; wins over the F08 leave date and the termination date (§3.7).</param>
/// <param name="Basis">The application or other basis, free text.</param>
public sealed record StartProcessCaseCommand(string? ProcessCode, Guid? EmploymentId, DateOnly? LastWorkDay, string? Basis);

/// <summary>
/// A manual start (§7.3). Takes the engine lock because a pass runs the same <c>subject_ref</c> scan: with the lock,
/// «is there a case?» and the insert are one step, and the two can never meet at the unique index.
/// </summary>
/// <remarks>
/// <para>
/// <b>ex-1 closes as Done(manual)</b> with the inputs as its payload: «manual start = the application record» (§3.7),
/// the step keeps who recorded it and the pass does not overwrite a person's word.
/// </para>
/// <para>
/// <b>Every existing case refuses</b>, a <c>withdrawn</c> one included: one employment has one <c>subject_ref</c>. A
/// withdrawn case comes back through its next F08 / F09 (the starter) or a manager's reopen.
/// </para>
/// </remarks>
public sealed class StartProcessCaseCommandHandler
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessEngineLock _lock;
    private readonly IProcessCaseRepository _cases;
    private readonly IProcessTemplateRepository _templates;
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly IProcessEngineJournal _journal;
    private readonly IEmploymentRepository _employments;
    private readonly SystemSettingsReader _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IDatabaseFault _fault;
    private readonly GetProcessCaseQueryHandler _details;

    public StartProcessCaseCommandHandler(
        IUnitOfWork unitOfWork,
        IProcessEngineLock engineLock,
        IProcessCaseRepository cases,
        IProcessTemplateRepository templates,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        IEmploymentRepository employments,
        SystemSettingsReader settings,
        ICurrentUser currentUser,
        IClock clock,
        IDatabaseFault fault,
        GetProcessCaseQueryHandler details)
    {
        _unitOfWork = unitOfWork;
        _lock = engineLock;
        _cases = cases;
        _templates = templates;
        _versions = versions;
        _journal = journal;
        _employments = employments;
        _settings = settings;
        _currentUser = currentUser;
        _clock = clock;
        _fault = fault;
        _details = details;
    }

    public async Task<Result<ProcessCaseDetailDto>> HandleAsync(StartProcessCaseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_currentUser.UserId is not { } userId)
        {
            return ProcessErrors.Forbidden;
        }

        var basis = command.Basis?.Trim();
        if (command.ProcessCode != ProcessCodes.Dismissal
            || command.EmploymentId is not { } employmentId
            || command.LastWorkDay is not { } lastWorkDay
            || basis is { Length: > ProcessCase.MaxCancelReasonLength })
        {
            return ProcessErrors.Invalid;
        }

        // §7.6: while dormant the engine creates nothing, and no pass would ever pick the case up.
        if (ProcessEngineSettings.From(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false)).IsDormant)
        {
            return ProcessErrors.InvalidStatus;
        }

        Result<Guid> started;
        try
        {
            started = await _unitOfWork
                .ExecuteInTransactionAsync(token => StartAsync(userId, employmentId, lastWorkDay, basis, token), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        if (started.IsFailure)
        {
            return started.Error;
        }

        return await _details.HandleAsync(started.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs in the transaction, and a retry runs it again: it reads everything itself.</summary>
    private async Task<Result<Guid>> StartAsync(Guid userId, Guid employmentId, DateOnly lastWorkDay, string? basis, CancellationToken cancellationToken)
    {
        if (!await _lock.TryAcquireAsync(cancellationToken).ConfigureAwait(false))
        {
            return ProcessErrors.EngineBusy;
        }

        var employment = await _employments.GetByIdAsync(employmentId, cancellationToken).ConfigureAwait(false);
        if (employment is null)
        {
            return ProcessErrors.NotFound;
        }

        if (employment.Status is not (EmploymentStatus.Active or EmploymentStatus.Probation))
        {
            return ProcessErrors.InvalidStatus;
        }

        if (lastWorkDay < employment.HireDate)
        {
            return ProcessErrors.Invalid;
        }

        var subjectRef = ProcessSubjectRefs.Dismissal(employmentId);
        if (await _cases.GetBySubjectRefAsync(ProcessCodes.Dismissal, subjectRef, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ProcessErrors.CaseAlreadyStarted;
        }

        var catalog = await ProcessDefinitionCatalog.LoadAsync(_templates, _versions, _journal, cancellationToken).ConfigureAwait(false);
        if (catalog.Published(ProcessCodes.Dismissal) is not { } template)
        {
            return ProcessErrors.InvalidStatus;
        }

        var at = _clock.UtcNow;

        var created = ProcessCase.Start(
            new ProcessCaseStart(
                template.Code,
                template.VersionId,
                ProcessSubjectKind.Employment,
                subjectRef,
                _clock.Today,
                Legacy: false,
                new ProcessCaseLinks(EmploymentId: employmentId, EmployeeId: employment.EmployeeId),
                DepartmentId: employment.DepartmentId,
                AnchorDate: lastWorkDay,
                StartedBy: userId),
            ProcessStarters.PlanOf(template.Definition),
            at);

        if (created.IsFailure)
        {
            return created.Error;
        }

        var processCase = created.Value;

        var payload = new JsonObject
        {
            ["lastWorkDay"] = lastWorkDay.ToString("yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrEmpty(basis))
        {
            payload["basis"] = basis;
        }

        // A new case's steps are all Locked until its first pass; this one is closed by the start itself.
        var activated = processCase.ActivateStep(ProcessStepIds.DismissalApplication, at);
        if (activated.IsFailure)
        {
            return activated.Error;
        }

        var recorded = processCase.CompleteStepManually(ProcessStepIds.DismissalApplication, userId, payload.ToJsonString(), evidenceSha256: null, at);
        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        _cases.Add(processCase);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return processCase.Id;
    }
}

/// <summary>POST <c>/api/processes/cases/{id}/cancel</c> (§10 #13).</summary>
public sealed record CancelProcessCaseCommand(Guid CaseId, int? Version, string? Reason);

/// <summary>A manager cancels a running case; the reason stays on the row and in the audit (§14).</summary>
public sealed class CancelProcessCaseCommandHandler
{
    private readonly IProcessCaseRepository _cases;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly GetProcessCaseQueryHandler _details;

    public CancelProcessCaseCommandHandler(
        IProcessCaseRepository cases,
        IUnitOfWork unitOfWork,
        IDatabaseFault fault,
        IClock clock,
        GetProcessCaseQueryHandler details)
    {
        _cases = cases;
        _unitOfWork = unitOfWork;
        _fault = fault;
        _clock = clock;
        _details = details;
    }

    public async Task<Result<ProcessCaseDetailDto>> HandleAsync(CancelProcessCaseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Version is not { } version)
        {
            return ProcessErrors.Invalid;
        }

        var processCase = await _cases.GetWithStepsAsync(command.CaseId, cancellationToken).ConfigureAwait(false);
        if (processCase is null)
        {
            return ProcessErrors.NotFound;
        }

        var checkedVersion = processCase.CheckVersion(version);
        if (checkedVersion.IsFailure)
        {
            return checkedVersion.Error;
        }

        var cancelled = processCase.Cancel(command.Reason, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        return await _details.HandleAsync(command.CaseId, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>POST <c>/api/processes/cases/{id}/reopen</c> (§10 #13).</summary>
public sealed record ReopenProcessCaseCommand(Guid CaseId, int? Version, string? Reason);

/// <summary>
/// A manager takes a finished case back to Active (§7.5) — unless what ended it still holds: the next pass would end it
/// again, and the manager would have seen «reopened» for at most five minutes, with the case silently gone after.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question is put to the pass itself, not to a copy of its rules.</b> Inside the transaction and under the
/// engine lock the case is reopened and passed on its tracked rows; if the pass ends it, nothing is saved and the
/// request's scope throws the changes away. Any reason the pass has counts: an end fact, every step closed, a deleted
/// subject. <c>ProcessReconcilerTests.Reopened_case_whose_end_fact_still_holds_completes_again</c> pins why this is
/// needed; the aggregate's <see cref="ProcessCase.Reopen"/> does not refuse, the starters use it too.
/// </para>
/// <para>
/// <b>The reason goes through the audit event</b>: the case has no column for it, and the interceptor's own entry
/// shows only that status and outcome changed.
/// </para>
/// </remarks>
public sealed class ReopenProcessCaseCommandHandler
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessEngineLock _lock;
    private readonly IProcessCaseRepository _cases;
    private readonly ProcessEnginePassLoader _passLoader;
    private readonly ProcessCaseBatch _batch;
    private readonly SystemSettingsReader _settings;
    private readonly IAuditLogger _audit;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly GetProcessCaseQueryHandler _details;

    public ReopenProcessCaseCommandHandler(
        IUnitOfWork unitOfWork,
        IProcessEngineLock engineLock,
        IProcessCaseRepository cases,
        ProcessEnginePassLoader passLoader,
        ProcessCaseBatch batch,
        SystemSettingsReader settings,
        IAuditLogger audit,
        IDatabaseFault fault,
        IClock clock,
        GetProcessCaseQueryHandler details)
    {
        _unitOfWork = unitOfWork;
        _lock = engineLock;
        _cases = cases;
        _passLoader = passLoader;
        _batch = batch;
        _settings = settings;
        _audit = audit;
        _fault = fault;
        _clock = clock;
        _details = details;
    }

    public async Task<Result<ProcessCaseDetailDto>> HandleAsync(ReopenProcessCaseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reason = command.Reason?.Trim();
        if (command.Version is not { } version || string.IsNullOrEmpty(reason) || reason.Length > ProcessCase.MaxCancelReasonLength)
        {
            return ProcessErrors.Invalid;
        }

        var settings = ProcessEngineSettings.From(await _settings.ReadAsync(cancellationToken).ConfigureAwait(false));

        Result reopened;
        try
        {
            reopened = await _unitOfWork
                .ExecuteInTransactionAsync(token => ReopenAsync(command.CaseId, version, reason, settings, token), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsStaleProcessWrite(exception))
        {
            return ProcessErrors.Stale;
        }

        if (reopened.IsFailure)
        {
            return reopened.Error;
        }

        return await _details.HandleAsync(command.CaseId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs in the transaction, and a retry runs it again: it reads everything itself.</summary>
    private async Task<Result> ReopenAsync(Guid caseId, int version, string reason, ProcessEngineSettings settings, CancellationToken cancellationToken)
    {
        if (!await _lock.TryAcquireAsync(cancellationToken).ConfigureAwait(false))
        {
            return ProcessErrors.EngineBusy;
        }

        var processCase = await _cases.GetWithStepsAsync(caseId, cancellationToken).ConfigureAwait(false);
        if (processCase is null)
        {
            return ProcessErrors.NotFound;
        }

        var checkedVersion = processCase.CheckVersion(version);
        if (checkedVersion.IsFailure)
        {
            return checkedVersion;
        }

        var result = processCase.Reopen(_clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        // Dormant, no pass would run to end it again: the reopen stands as it is.
        if (settings.StartedOn is { } startedOn)
        {
            var pass = await _passLoader.LoadAsync(startedOn, settings, cancellationToken).ConfigureAwait(false);
            await _batch.ReconcileAsync(pass, [processCase], cancellationToken).ConfigureAwait(false);

            if (processCase.Status != ProcessCaseStatus.Active)
            {
                return ProcessErrors.EndFactStillHolds;
            }
        }

        _audit.Record(
            AuditAction.Update,
            nameof(ProcessCase),
            processCase.Id.ToString(),
            newValues: new JsonObject { ["action"] = "reopened", ["reason"] = reason }.ToJsonString());

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>POST <c>/api/processes/reconcile</c> (§10 #18): a pass now, for diagnostics.</summary>
public sealed class RunProcessReconcileCommandHandler
{
    private readonly ProcessInlineReconcile _inline;

    public RunProcessReconcileCommandHandler(ProcessInlineReconcile inline) => _inline = inline;

    public Task<ProcessReconcileResult> HandleAsync(CancellationToken cancellationToken = default)
        => _inline.RunNowAsync(cancellationToken);
}
