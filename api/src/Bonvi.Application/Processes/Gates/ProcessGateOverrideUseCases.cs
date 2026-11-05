using Bonvi.Application.Abstractions;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Gates;

/// <summary>A gate override as the API shows it (docs/SPEC-mening-ishlarim.md §6.4).</summary>
public sealed record ProcessGateOverrideDto(
    Guid Id,
    string GateCode,
    Guid? CaseId,
    string? StepCode,
    Guid? EmploymentId,
    Guid? CandidateId,
    Guid? ForUserId,
    string Reason,
    Guid GrantedBy,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt,
    string? ConsumedOperation)
{
    public static ProcessGateOverrideDto From(ProcessGateOverride row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new ProcessGateOverrideDto(
            row.Id,
            row.GateCode,
            row.CaseId,
            row.StepCode,
            row.EmploymentId,
            row.CandidateId,
            row.ForUserId,
            row.Reason,
            row.GrantedBy,
            row.GrantedAt,
            row.ExpiresAt,
            row.ConsumedAt,
            row.ConsumedOperation);
    }
}

/// <summary>
/// §8.4, who may grant: <c>processes.override</c> AND the hr1 assignment; while nobody holds hr1,
/// <c>processes.manage</c> stands in. The endpoint's policy covers only the first half.
/// </summary>
internal static class GateOverrideGrantRule
{
    public static async Task<Result<Guid>> GrantorAsync(
        ICurrentUser currentUser,
        IBlankRoleAssignmentRepository assignments,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || !currentUser.HasPermission(Permissions.ProcessesOverride))
        {
            return ProcessErrors.Forbidden;
        }

        var hr1 = await assignments.ListUserIdsAsync(BlankActorRoles.Hr1, cancellationToken).ConfigureAwait(false);

        var allowed = hr1.Count == 0 ? currentUser.HasPermission(Permissions.ProcessesManage) : hr1.Contains(userId);

        return allowed ? Result.Success(userId) : ProcessErrors.Forbidden;
    }
}

/// <summary>POST <c>/api/processes/cases/{id}/gates/{code}/override</c>.</summary>
public sealed record GrantCaseGateOverrideCommand(Guid CaseId, string GateCode, string? Reason);

/// <summary>
/// Grants a 24-hour, single-use exception to a blocking gate of a case. The route names only the gate, and Q12 sits
/// on two steps: the grant goes to the step the gate is holding now, otherwise to the first open step that carries it.
/// </summary>
public sealed class GrantCaseGateOverrideCommandHandler
{
    private readonly IProcessCaseRepository _cases;
    private readonly IProcessTemplateRepository _templates;
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly IProcessEngineJournal _journal;
    private readonly IProcessGateOverrideRepository _overrides;
    private readonly IBlankRoleAssignmentRepository _assignments;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GrantCaseGateOverrideCommandHandler(
        IProcessCaseRepository cases,
        IProcessTemplateRepository templates,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        IProcessGateOverrideRepository overrides,
        IBlankRoleAssignmentRepository assignments,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _cases = cases;
        _templates = templates;
        _versions = versions;
        _journal = journal;
        _overrides = overrides;
        _assignments = assignments;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProcessGateOverrideDto>> HandleAsync(GrantCaseGateOverrideCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var grantor = await GateOverrideGrantRule.GrantorAsync(_currentUser, _assignments, cancellationToken).ConfigureAwait(false);
        if (grantor.IsFailure)
        {
            return grantor.Error;
        }

        var processCase = await _cases.GetWithStepsAsync(command.CaseId, cancellationToken).ConfigureAwait(false);
        if (processCase is null)
        {
            return ProcessErrors.NotFound;
        }

        if (processCase.Status != ProcessCaseStatus.Active)
        {
            return ProcessErrors.InvalidStatus;
        }

        var catalog = await ProcessDefinitionCatalog.LoadAsync(_templates, _versions, _journal, cancellationToken).ConfigureAwait(false);
        await catalog.EnsureVersionsAsync([processCase.TemplateVersionId], cancellationToken).ConfigureAwait(false);

        var carriers = catalog.ForVersion(processCase.TemplateVersionId)?.Steps
            .Where(step => step.Gates.Any(gate => gate.Code == command.GateCode && gate.Mode == ProcessGateMode.Block))
            .ToList() ?? [];

        if (carriers.Count == 0)
        {
            return ProcessErrors.Invalid;
        }

        var open = carriers
            .Select(step => processCase.FindStep(step.Id))
            .OfType<ProcessCaseStep>()
            .Where(step => !step.IsClosed)
            .ToList();

        var target = open.Find(step => step.BlockedGateCode == command.GateCode) ?? open.FirstOrDefault();
        if (target is null)
        {
            return ProcessErrors.InvalidStepState;
        }

        var granted = ProcessGateOverride.GrantForCase(
            command.GateCode,
            processCase.Id,
            target.StepCode,
            processCase.EmploymentId,
            processCase.CandidateId,
            command.Reason,
            grantor.Value,
            _clock.UtcNow);

        if (granted.IsFailure)
        {
            return granted.Error;
        }

        _overrides.Add(granted.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ProcessGateOverrideDto.From(granted.Value));
    }
}

/// <summary>POST <c>/api/processes/gate-overrides/direct-hire</c>.</summary>
public sealed record GrantDirectHireOverrideCommand(Guid ForUserId, string? Reason);

/// <summary>Allows one operator one hire without a candidate within 24 hours (gate Q01, no case).</summary>
public sealed class GrantDirectHireOverrideCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IProcessGateOverrideRepository _overrides;
    private readonly IBlankRoleAssignmentRepository _assignments;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GrantDirectHireOverrideCommandHandler(
        IUserRepository users,
        IProcessGateOverrideRepository overrides,
        IBlankRoleAssignmentRepository assignments,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _overrides = overrides;
        _assignments = assignments;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProcessGateOverrideDto>> HandleAsync(GrantDirectHireOverrideCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var grantor = await GateOverrideGrantRule.GrantorAsync(_currentUser, _assignments, cancellationToken).ConfigureAwait(false);
        if (grantor.IsFailure)
        {
            return grantor.Error;
        }

        if (await _users.GetByIdAsync(command.ForUserId, cancellationToken).ConfigureAwait(false) is null)
        {
            return ProcessErrors.Invalid;
        }

        var granted = ProcessGateOverride.GrantForDirectHire(command.ForUserId, command.Reason, grantor.Value, _clock.UtcNow);
        if (granted.IsFailure)
        {
            return granted.Error;
        }

        _overrides.Add(granted.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ProcessGateOverrideDto.From(granted.Value));
    }
}

/// <summary>GET <c>/api/processes/gate-overrides</c>: the overrides log of «Kim kechikdi» (§11.4).</summary>
/// <param name="From">Tashkent date of the oldest grant; empty → the last <see cref="ListGateOverridesQueryHandler.DefaultDays"/> days.</param>
public sealed record ListGateOverridesQuery(DateOnly? From);

public sealed class ListGateOverridesQueryHandler
{
    public const int DefaultDays = 30;

    private readonly IProcessGateOverrideRepository _overrides;
    private readonly IClock _clock;

    public ListGateOverridesQueryHandler(IProcessGateOverrideRepository overrides, IClock clock)
    {
        _overrides = overrides;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProcessGateOverrideDto>> HandleAsync(ListGateOverridesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var since = query.From is { } from ? ProcessSubjectRefs.StartOfDay(from) : _clock.UtcNow.AddDays(-DefaultDays);
        var rows = await _overrides.ListGrantedSinceAsync(since, cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(ProcessGateOverrideDto.From)];
    }
}
