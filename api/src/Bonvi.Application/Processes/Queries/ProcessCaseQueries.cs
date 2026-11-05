using System.Text.Json;
using Bonvi.Application.Common.Lists;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

/// <summary>#3 query: every filter optional. <paramref name="Status"/> is a <see cref="ProcessCaseStatus"/> name.</summary>
public sealed record ListProcessCasesQuery(
    string? Process,
    string? Status,
    Guid? Department,
    Guid? Employee,
    Guid? Candidate,
    string? HolderRole,
    bool? Late,
    DateOnly? From,
    DateOnly? To,
    string? Q,
    int? Page,
    int? PageSize);

/// <summary>§10 #3.</summary>
public sealed class ListProcessCasesQueryHandler
{
    public const int DefaultPageSize = 50;

    /// <summary>«Barchasi» — the same ceiling as every other list.</summary>
    public const int MaxPageSize = 10_000;

    private readonly ProcessInlineReconcile _inline;
    private readonly IProcessReadModel _readModel;
    private readonly ProcessViewerLoader _viewers;
    private readonly ProcessReadSetLoader _sets;
    private readonly IClock _clock;

    public ListProcessCasesQueryHandler(
        ProcessInlineReconcile inline,
        IProcessReadModel readModel,
        ProcessViewerLoader viewers,
        ProcessReadSetLoader sets,
        IClock clock)
    {
        _inline = inline;
        _readModel = readModel;
        _viewers = viewers;
        _sets = sets;
        _clock = clock;
    }

    public async Task<Result<PagedResult<ProcessCaseListItemDto>>> HandleAsync(
        ListProcessCasesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ProcessCaseStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ProcessCaseStatus>(query.Status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed)
                || char.IsDigit(query.Status.Trim()[0]))
            {
                return ProcessErrors.Invalid;
            }

            status = parsed;
        }

        var range = ListDateRange.Validate(query.From, query.To);
        if (range.IsFailure)
        {
            return range.Error;
        }

        await _inline.BeforeReadAsync(cancellationToken).ConfigureAwait(false);

        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var found = await _readModel
            .SearchCasesAsync(
                new ProcessCaseFilter(
                    Blank(query.Process),
                    status,
                    query.Department,
                    query.Employee,
                    query.Candidate,
                    Blank(query.HolderRole),
                    query.Late,
                    query.From,
                    query.To,
                    Blank(query.Q),
                    _clock.Today,
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        var (_, directory) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);
        var steps = await CurrentStepsAsync(_readModel, found.Items, cancellationToken).ConfigureAwait(false);
        var set = await _sets.LoadAsync(found.Items, [.. steps.SelectMany(group => group)], directory, cancellationToken: cancellationToken).ConfigureAwait(false);

        var items = found.Items
            .Select(row =>
            {
                var current = steps[row.Id].Select(set.Current).ToList();
                return new ProcessCaseListItemDto(
                    row.Id,
                    row.ProcessCode,
                    set.DefinitionOf(row)?.Title ?? row.ProcessCode,
                    row.Status,
                    row.Outcome,
                    row.Legacy,
                    set.Subject(row),
                    row.DepartmentId,
                    set.DepartmentName(row),
                    row.StartedOn,
                    row.CompletedAt,
                    row.CancelledAt,
                    current,
                    current.Count == 0 ? 0 : current.Max(step => step.DaysLate));
            })
            .ToList();

        var pageCount = (int)Math.Ceiling(found.Total / (double)pageSize);

        return new PagedResult<ProcessCaseListItemDto>(items, page, pageSize, found.Total, pageCount);
    }

    internal static async Task<ILookup<Guid, ProcessStepRow>> CurrentStepsAsync(
        IProcessReadModel readModel,
        IReadOnlyCollection<ProcessCaseRow> cases,
        CancellationToken cancellationToken)
    {
        if (cases.Count == 0)
        {
            return Array.Empty<ProcessStepRow>().ToLookup(step => step.CaseId);
        }

        var steps = await readModel.ListStepsOfCasesAsync([.. cases.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);

        return steps.Where(step => step.IsCurrent).ToLookup(step => step.CaseId);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>§10 #4: running cases only; a finished case has left the board (it is in #3).</summary>
public sealed class GetProcessBoardQueryHandler
{
    private readonly ProcessInlineReconcile _inline;
    private readonly IProcessReadModel _readModel;
    private readonly ProcessViewerLoader _viewers;
    private readonly ProcessReadSetLoader _sets;
    private readonly IProcessTemplateRepository _templates;
    private readonly IProcessTemplateVersionRepository _versions;

    public GetProcessBoardQueryHandler(
        ProcessInlineReconcile inline,
        IProcessReadModel readModel,
        ProcessViewerLoader viewers,
        ProcessReadSetLoader sets,
        IProcessTemplateRepository templates,
        IProcessTemplateVersionRepository versions)
    {
        _inline = inline;
        _readModel = readModel;
        _viewers = viewers;
        _sets = sets;
        _templates = templates;
        _versions = versions;
    }

    public async Task<Result<ProcessBoardDto>> HandleAsync(string? process, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(process))
        {
            return ProcessErrors.Invalid;
        }

        var template = await _templates.GetByCodeAsync(process.Trim(), cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return ProcessErrors.NotFound;
        }

        await _inline.BeforeReadAsync(cancellationToken).ConfigureAwait(false);

        var published = await _versions.GetPublishedAsync(template.Id, cancellationToken).ConfigureAwait(false);
        var publishedDefinition = published is null
            ? null
            : (await _sets.DefinitionsAsync([published.Id], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(published.Id);

        var cases = await _readModel.ListActiveCasesAsync(template.Code, cancellationToken).ConfigureAwait(false);
        var (_, directory) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);

        var allSteps = cases.Count == 0
            ? []
            : await _readModel.ListStepsOfCasesAsync([.. cases.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);
        var stepsByCase = allSteps.Where(step => step.IsOpen).ToLookup(step => step.CaseId);
        var set = await _sets
            .LoadAsync(cases, [.. allSteps.Where(step => step.IsCurrent)], directory, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var columns = (publishedDefinition?.Board ?? []).Select(column => new ProcessColumnDto(column.Key, column.Title)).ToList();
        var cards = new Dictionary<string, List<ProcessBoardCardDto>>(StringComparer.Ordinal);
        foreach (var row in cases)
        {
            var open = stepsByCase[row.Id].OrderBy(step => step.OrderNo).ToList();
            var current = open.Where(step => step.IsCurrent).ToList();

            // A case with nothing open is about to complete on the next pass; it stands in the last column until then.
            var column = (current.FirstOrDefault() ?? open.FirstOrDefault())?.BoardColumn ?? columns.LastOrDefault()?.Key;
            if (column is null)
            {
                continue;
            }

            if (columns.TrueForAll(known => known.Key != column))
            {
                // A case on an older version may name a column the published one dropped: it must still show.
                columns.Add(new ProcessColumnDto(column, column));
            }

            if (!cards.TryGetValue(column, out var list))
            {
                cards[column] = list = [];
            }

            var due = current.Where(step => step.DueDate is not null).Select(step => step.DueDate).Min();
            list.Add(new ProcessBoardCardDto(
                row.Id,
                set.Subject(row),
                row.DepartmentId,
                set.DepartmentName(row),
                row.Legacy,
                [.. current.Select(set.Current)],
                due,
                set.DaysLate(due)));
        }

        return new ProcessBoardDto(
            template.Code,
            publishedDefinition?.Title ?? template.Title,
            [
                .. columns.Select(column => new ProcessBoardColumnDto(
                    column.Key,
                    column.Title,
                    cards.TryGetValue(column.Key, out var list) ? list : [])),
            ]);
    }
}

/// <summary>§10 #5: the case, reconciled first (§7.4), with what this viewer may do on each step (§9.3).</summary>
public sealed class GetProcessCaseQueryHandler
{
    private readonly ProcessInlineReconcile _inline;
    private readonly IProcessReadModel _readModel;
    private readonly ProcessViewerLoader _viewers;
    private readonly ProcessReadSetLoader _sets;
    private readonly IClock _clock;

    public GetProcessCaseQueryHandler(
        ProcessInlineReconcile inline,
        IProcessReadModel readModel,
        ProcessViewerLoader viewers,
        ProcessReadSetLoader sets,
        IClock clock)
    {
        _inline = inline;
        _readModel = readModel;
        _viewers = viewers;
        _sets = sets;
        _clock = clock;
    }

    public async Task<Result<ProcessCaseDetailDto>> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _inline.BeforeCaseReadAsync(id, cancellationToken).ConfigureAwait(false);

        var cases = await _readModel.ListCasesAsync([id], cancellationToken).ConfigureAwait(false);
        if (cases.Count == 0)
        {
            return ProcessErrors.NotFound;
        }

        var row = cases[0];
        var (viewer, directory) = await _viewers.LoadAsync(cancellationToken).ConfigureAwait(false);
        var steps = await _readModel.ListStepsOfCasesAsync([id], cancellationToken).ConfigureAwait(false);

        var set = await _sets
            .LoadAsync(
                cases,
                steps,
                directory,
                [.. steps.Select(step => step.DoneBy).OfType<Guid>().Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        var blankIds = steps.Select(step => step.LinkedBlankSubmissionId).OfType<Guid>().Distinct().ToList();
        var blanks = blankIds.Count == 0
            ? []
            : await _readModel.ListBlanksAsync(blankIds, cancellationToken).ConfigureAwait(false);

        var definition = set.DefinitionOf(row);
        var at = _clock.UtcNow;

        return new ProcessCaseDetailDto(
            row.Id,
            row.ProcessCode,
            definition?.Title ?? row.ProcessCode,
            definition?.Source,
            row.Status,
            row.Outcome,
            row.Legacy,
            set.Subject(row),
            row.DepartmentId,
            set.DepartmentName(row),
            row.ParentCaseId,
            row.AnchorDate,
            row.StartedOn,
            row.CompletedAt,
            row.CancelledAt,
            row.CancelReason,
            row.Version,
            [.. (definition?.Board ?? []).Select(column => new ProcessColumnDto(column.Key, column.Title))],
            [.. steps.Select(step => StepOf(step, row, viewer, set, at))],
            blanks);
    }

    private static ProcessCaseStepDto StepOf(
        ProcessStepRow step,
        ProcessCaseRow row,
        ProcessViewer viewer,
        ProcessReadSet set,
        DateTimeOffset at)
    {
        var definition = set.DefinitionOf(step);
        var holds = viewer.Holds(step.HolderRole, step.HolderEmployeeId);

        return new ProcessCaseStepDto(
            step.StepCode,
            step.OrderNo,
            definition?.Title ?? step.StepCode,
            step.BoardColumn,
            definition?.Kind,
            step.State,
            step.Resolution,
            set.Holder(step.HolderRole, step.HolderEmployeeId),
            step.DueDate,
            set.DaysLate(step.DueDate),
            step.DoneAt,
            step.DoneBy,
            step.DoneBy is { } by ? set.Names.Users.GetValueOrDefault(by) : null,
            PayloadOf(step.Payload),
            definition?.Blank,
            set.BlankTitle(definition?.Blank),
            definition?.Signature,
            step.LinkedBlankSubmissionId,
            step.LinkedRecordId,
            step.BlockedGateCode,
            ProcessGateReading.ErrorCodeOf(step.BlockedGateCode),
            set.ResolverDto(step),
            set.WarningsOf(step),
            definition?.Prereqs ?? [],
            definition?.Optional ?? false,
            definition?.Guide ?? [],
            definition?.SourceRef,
            ProcessReadSet.LinkOf(definition),
            ProcessDefinitionDtos.Manual(definition?.Manual),
            holds,
            ProcessStepAbility.CanComplete(definition, step, row, viewer),
            ProcessStepAbility.CanSkip(definition, step, row, viewer),
            ProcessStepAbility.CanUndo(step, row, viewer, at));
    }

    private static JsonElement? PayloadOf(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }
}

/// <summary>
/// §9.3 as the screen sees it: the buttons the write endpoints would accept. The aggregate enforces only the state
/// rules, so the write slice must apply these same predicates rather than a second copy.
/// </summary>
public static class ProcessStepAbility
{
    /// <summary>§9.3: the resolved holder, or any <c>processes.manage</c> holder.</summary>
    public static bool MayAct(ProcessStepRow step, ProcessViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(viewer);

        return viewer.CanManage || viewer.Holds(step.HolderRole, step.HolderEmployeeId);
    }

    public static bool CanComplete(ProcessStep? definition, ProcessStepRow step, ProcessCaseRow processCase, ProcessViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(processCase);
        ArgumentNullException.ThrowIfNull(viewer);

        return viewer.CanWork
            && processCase.Status == ProcessCaseStatus.Active
            && step.State == ProcessStepState.Active
            && definition is not null
            && (definition.Kind == ProcessStepKind.Manual || definition.HasManualFallback)
            && MayAct(step, viewer);
    }

    /// <remarks>
    /// A Blocked step cannot be skipped by anyone: «Kerak emas» would close it without the gate's condition and open
    /// everything behind it. The way past a gate is the override (§8.4).
    /// </remarks>
    public static bool CanSkip(ProcessStep? definition, ProcessStepRow step, ProcessCaseRow processCase, ProcessViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(processCase);
        ArgumentNullException.ThrowIfNull(viewer);

        return viewer.CanWork
            && processCase.Status == ProcessCaseStatus.Active
            && step.IsOpen
            && step.State != ProcessStepState.Blocked
            && definition is not null
            && (definition.Skippable || (processCase.Legacy && definition.LegacySkippable))
            && MayAct(step, viewer);
    }

    /// <summary>The window and the manager's exemption are the aggregate's own (<see cref="ProcessCase.UndoStep"/>).</summary>
    public static bool CanUndo(ProcessStepRow step, ProcessCaseRow processCase, ProcessViewer viewer, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(processCase);
        ArgumentNullException.ThrowIfNull(viewer);

        var userClosed = step.Resolution is ProcessStepResolution.Manual or ProcessStepResolution.UserSkip or ProcessStepResolution.Legacy;

        return viewer.CanWork
            && processCase.Status == ProcessCaseStatus.Active
            && userClosed
            && (viewer.CanManage
                || (step.DoneBy == viewer.UserId && step.DoneAt is { } doneAt && at - doneAt <= ProcessCase.UndoWindow));
    }
}
