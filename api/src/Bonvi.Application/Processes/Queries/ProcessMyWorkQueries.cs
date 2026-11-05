using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Identities;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Completeness;
using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

/// <summary>§10 #1: the sections of tab «Mening ishlarim». Pure, so #1 and #2 cannot disagree.</summary>
public static class ProcessWorkSections
{
    /// <summary>How far ahead «Kutilmoqda» looks (§10 #1).</summary>
    public const int WaitingHorizonDays = 7;

    public static (List<ProcessStepRow> Urgent, List<ProcessStepRow> Later, List<ProcessStepRow> Held) Split(
        IEnumerable<ProcessStepRow> steps,
        ProcessViewer viewer,
        DateOnly today,
        Func<ProcessStepRow, ProcessHolderRef?> resolverOf)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(resolverOf);

        var urgent = new List<ProcessStepRow>();
        var later = new List<ProcessStepRow>();
        var held = new List<ProcessStepRow>();

        foreach (var step in steps)
        {
            if (step.State == ProcessStepState.Blocked)
            {
                var resolver = resolverOf(step);
                (resolver is null || viewer.Holds(resolver.Role, resolver.EmployeeId) ? urgent : held).Add(step);
            }
            else if (step.State == ProcessStepState.Active)
            {
                (step.DueDate is { } due && due <= today ? urgent : later).Add(step);
            }
        }

        return (Ordered(urgent), Ordered(later), Ordered(held));
    }

    /// <summary>The soonest first; a step with no due date last.</summary>
    public static List<ProcessStepRow> Ordered(IEnumerable<ProcessStepRow> steps)
        => [.. steps.OrderBy(step => step.DueDate ?? DateOnly.MaxValue).ThenBy(step => step.OrderNo).ThenBy(step => step.CaseId)];
}

/// <summary>§10 #2. No pass in front of it: the badge is asked for on every page, and #1 runs one when opened.</summary>
public sealed class GetMyProcessCountQueryHandler
{
    private static readonly ProcessStepState[] CountedStates = [ProcessStepState.Active, ProcessStepState.Blocked];

    private readonly IProcessReadModel _readModel;
    private readonly ProcessViewerLoader _viewers;
    private readonly ProcessReadSetLoader _sets;
    private readonly IClock _clock;

    public GetMyProcessCountQueryHandler(
        IProcessReadModel readModel,
        ProcessViewerLoader viewers,
        ProcessReadSetLoader sets,
        IClock clock)
    {
        _readModel = readModel;
        _viewers = viewers;
        _sets = sets;
        _clock = clock;
    }

    public async Task<Result<ProcessMyCountDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var (viewer, _) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);

        var steps = await _readModel
            .ListHeldStepsAsync(viewer.OfficeRoles, viewer.EmployeeId, CountedStates, cancellationToken)
            .ConfigureAwait(false);

        // Only a Blocked step needs its case and definition: most badges never reach this.
        var blocked = steps.Where(step => step.State == ProcessStepState.Blocked).ToList();
        var resolvers = new Dictionary<(Guid, string), ProcessHolderRef>();
        if (blocked.Count > 0)
        {
            var cases = await _readModel
                .ListCasesAsync([.. blocked.Select(step => step.CaseId).Distinct()], cancellationToken)
                .ConfigureAwait(false);
            var definitions = await _sets
                .DefinitionsAsync(cases.Select(row => row.TemplateVersionId), cancellationToken)
                .ConfigureAwait(false);
            var found = await ProcessGateReading
                .ResolversAsync(blocked, cases.ToDictionary(row => row.Id), definitions, _readModel, cancellationToken)
                .ConfigureAwait(false);

            foreach (var (key, holder) in found)
            {
                resolvers[key] = holder;
            }
        }

        var (urgent, later, _) = ProcessWorkSections.Split(
            steps,
            viewer,
            _clock.Today,
            step => resolvers.GetValueOrDefault((step.CaseId, step.StepCode)));

        return new ProcessMyCountDto(urgent.Count + later.Count);
    }
}

/// <summary>§10 #1: the viewer's tasks and the «Tartib» cards.</summary>
public sealed class GetMyWorkQueryHandler
{
    private static readonly ProcessStepState[] OpenStates =
    [
        ProcessStepState.Active,
        ProcessStepState.Blocked,
        ProcessStepState.Locked,
        ProcessStepState.Waiting,
    ];

    private readonly ProcessInlineReconcile _inline;
    private readonly IProcessReadModel _readModel;
    private readonly ProcessViewerLoader _viewers;
    private readonly ProcessReadSetLoader _sets;
    private readonly ProcessTartibReader _tartib;
    private readonly IClock _clock;

    public GetMyWorkQueryHandler(
        ProcessInlineReconcile inline,
        IProcessReadModel readModel,
        ProcessViewerLoader viewers,
        ProcessReadSetLoader sets,
        ProcessTartibReader tartib,
        IClock clock)
    {
        _inline = inline;
        _readModel = readModel;
        _viewers = viewers;
        _sets = sets;
        _tartib = tartib;
        _clock = clock;
    }

    public async Task<Result<ProcessMyWorkDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        await _inline.BeforeReadAsync(cancellationToken).ConfigureAwait(false);

        var today = _clock.Today;
        var (viewer, directory) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);

        var steps = await _readModel
            .ListHeldStepsAsync(viewer.OfficeRoles, viewer.EmployeeId, OpenStates, cancellationToken)
            .ConfigureAwait(false);

        // «Kutilmoqda» can only come from steps that are not open to act on yet and fall within the horizon.
        var horizon = today.AddDays(ProcessWorkSections.WaitingHorizonDays);
        var waitingCandidates = steps
            .Where(step => step.State is ProcessStepState.Locked or ProcessStepState.Waiting
                && step.DueDate is { } due && due <= horizon)
            .ToList();
        var shown = steps.Where(step => step.State is ProcessStepState.Active or ProcessStepState.Blocked).ToList();

        var cases = await _readModel
            .ListCasesAsync([.. shown.Concat(waitingCandidates).Select(step => step.CaseId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        var set = await _sets
            .LoadAsync(cases, [.. shown, .. waitingCandidates], directory, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var (urgent, later, held) = ProcessWorkSections.Split(shown, viewer, today, set.ResolverOf);
        var waiting = await WaitingOnOthersAsync(waitingCandidates, viewer, set, cancellationToken).ConfigureAwait(false);

        var tartib = await _tartib.ReadAsync(today, cancellationToken).ConfigureAwait(false);

        return new ProcessMyWorkDto(
            today,
            urgent.Count + later.Count,
            urgent.Count(step => step.State == ProcessStepState.Active && set.DaysLate(step.DueDate) > 0),
            [.. urgent.Select(set.Task)],
            [.. later.Select(set.Task)],
            [.. held.Select(set.Task)],
            [.. ProcessWorkSections.Ordered(waiting).Select(set.Task)],
            tartib);
    }

    /// <summary>
    /// «Waiting» is per viewer and never stored (§7.4 p.4): a blocker the viewer holds is their own next task, so
    /// it does not make the step «waiting»; one on someone else's list does. A prerequisite not open yet is no
    /// blocker here: nobody can act on it, so the step is simply further down the road.
    /// </summary>
    private async Task<List<ProcessStepRow>> WaitingOnOthersAsync(
        List<ProcessStepRow> candidates,
        ProcessViewer viewer,
        ProcessReadSet set,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var siblings = (await _readModel
                .ListStepsOfCasesAsync([.. candidates.Select(step => step.CaseId).Distinct()], cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(step => (step.CaseId, step.StepCode));

        return
        [
            .. candidates.Where(step =>
                step.State == ProcessStepState.Waiting
                || (set.DefinitionOf(step)?.Prereqs ?? []).Any(prerequisite =>
                    siblings.TryGetValue((step.CaseId, prerequisite), out var blocker)
                    && blocker.State is ProcessStepState.Active or ProcessStepState.Blocked
                    && blocker.HolderRole is not null
                    && set.Cases.TryGetValue(step.CaseId, out var processCase)
                    && set.DefinitionOf(processCase)?.FindStep(prerequisite) is { Optional: false }
                    && !viewer.Holds(blocker.HolderRole, blocker.HolderEmployeeId))),
        ];
    }
}

/// <summary>
/// §11.8: what still needs tidying outside any process. Read on request, not stored. Each part runs only under the
/// permission of the screen it leads to, the same rule as the notification collector's.
/// </summary>
public sealed class ProcessTartibReader
{
    public const string UnmatchedTerminalPersons = "unmatchedTerminalPersons";
    public const string EmployeesWithoutPhoto = "employeesWithoutPhoto";
    public const string EmployeesWithoutSchedule = "employeesWithoutSchedule";
    public const string EmployeesWithIncompleteFiles = "employeesWithIncompleteFiles";

    private static readonly HashSet<NotificationType> Shown =
    [
        NotificationType.CandidateContactDue,
        NotificationType.CandidateContactOverdue,
        NotificationType.CandidateInterviewSoon,
        NotificationType.CandidateReapplied,
        NotificationType.ProbationMissing,
    ];

    private readonly NotificationCollector _notifications;
    private readonly ListUnmatchedQueryHandler _unmatched;
    private readonly IProcessReadModel _readModel;
    private readonly IEmployeeCompletenessReadModel _completeness;
    private readonly ICurrentUser _user;

    public ProcessTartibReader(
        NotificationCollector notifications,
        ListUnmatchedQueryHandler unmatched,
        IProcessReadModel readModel,
        IEmployeeCompletenessReadModel completeness,
        ICurrentUser user)
    {
        _notifications = notifications;
        _unmatched = unmatched;
        _readModel = readModel;
        _completeness = completeness;
        _user = user;
    }

    public async Task<ProcessTartibDto> ReadAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var notifications = await _notifications.CollectAsync(cancellationToken).ConfigureAwait(false);

        var counts = new List<ProcessTartibCountDto>();

        if (_user.HasPermission(Permissions.AttendanceManage))
        {
            var unmatched = await _unmatched
                .HandleAsync(new ListUnmatchedQuery(Page: 1, PageSize: 1), cancellationToken)
                .ConfigureAwait(false);

            if (unmatched.IsSuccess)
            {
                Add(counts, UnmatchedTerminalPersons, unmatched.Value.Total);
            }
        }

        if (_user.HasPermission(Permissions.EmployeesView))
        {
            Add(counts, EmployeesWithoutPhoto, await _readModel.CountActiveEmployeesWithoutPhotoAsync(cancellationToken).ConfigureAwait(false));
            Add(counts, EmployeesWithIncompleteFiles, await _completeness.CountWithGapsAsync(_user.CanSeeSalary(), cancellationToken).ConfigureAwait(false));
        }

        if (_user.HasPermission(Permissions.AttendanceManage))
        {
            Add(counts, EmployeesWithoutSchedule, await _readModel.CountActiveEmployeesWithoutScheduleAsync(today, cancellationToken).ConfigureAwait(false));
        }

        return new ProcessTartibDto([.. notifications.Where(item => Shown.Contains(item.Type))], counts);
    }

    private static void Add(List<ProcessTartibCountDto> counts, string key, int count)
    {
        if (count > 0)
        {
            counts.Add(new ProcessTartibCountDto(key, count));
        }
    }
}
