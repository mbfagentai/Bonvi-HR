using System.ComponentModel;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Processes;
using Bonvi.Infrastructure.Persistence.ReadModels;
using Bonvi.Infrastructure.Processes.Engine;
using Bonvi.Infrastructure.Repositories;
using Bonvi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>«Mening ishlarim» processes, read-only (docs/SPEC-mening-ishlarim.md §12).</summary>
/// <remarks>
/// <para>
/// The rows and cards come from the same <see cref="ProcessReadModel"/> and <see cref="ProcessReadSet"/> as the
/// screens, opened over the read role's context: a step reads the same here and in «Kim kechikdi».
/// </para>
/// <para>
/// <b>No reconcile before a read</b>, unlike the API: a pass writes, and this context cannot. The answer is as old as
/// the last pass of <c>processes:reconcile</c>, five minutes at most.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class ProcessTools(McpToolContext t)
{
    private const string Area = "Jarayonlar";

    /// <summary>§12: how much of a free text may leave, marker included.</summary>
    private const int MaxTextLength = 500;

    private const string Cut = "…";

    /// <summary>How many rows each list of <c>process_stuck</c> gives; its total is still the full count.</summary>
    private const int StuckLimit = McpToolContext.MaxPageSize;

    // ─────────────────────────────────────────────────────────────────
    //  process_cases
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "process_cases", Title = "Jarayonlar ro'yxati",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("HR jarayonlari (ishga olish, rasmiylashtirish, sinov muddati, ishdan bo'shatish) yangilari birinchi: jarayon, kim haqida, "
        + "joriy qadam(lar), kimda turibdi (rol yoki xodim), muddat, necha kun kechikkan. Filtrlar: jarayon, holat, bo'lim (ichki bo'limlari "
        + "bilan), xodim, faqat kechikkanlar, boshlangan sana. Qadamlar to'liq: process_case; kim kechikdi: process_stuck. Ma'lumot jarayon "
        + "dvigatelining oxirgi o'tishi bo'yicha (5 daqiqagacha eski). Lists HR process cases with their current steps; paged.")]
    public async Task<string> ProcessCases(
        [Description("Jarayon kodi: hire-request, hire-candidate, hire-formalise, probation yoki dismissal. Bo'sh — hammasi.")] string? process = null,
        [Description("Holat: Active, Completed yoki Cancelled. Bo'sh — hammasi.")] string? status = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan birga.")] Guid? department = null,
        [Description("Xodim identifikatori (search_employees natijasidagi id).")] Guid? employee = null,
        [Description("true — faqat muddati o'tgan qadami borlar; false — qolganlari.")] bool? late = null,
        [Description("Boshlangan sana (dan), YYYY-MM-DD. Ikkalasi bo'sh — sana filtri yo'q.")] string? from = null,
        [Description("Boshlangan sana (gacha), YYYY-MM-DD. Ko'pi bilan 366 kunlik oraliq.")] string? to = null,
        [Description("Sahifa raqami (1 dan).")] int? page = null,
        [Description("Sahifa hajmi (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.ProcessesView, Area);

        ProcessCaseStatus? wanted = string.IsNullOrWhiteSpace(status) ? null : EmploymentTools.ParseEnum<ProcessCaseStatus>(status, "status");

        DateOnly? start = null;
        DateOnly? end = null;
        if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
        {
            (start, end) = McpToolContext.Range(from, to);
        }

        var branch = await EmploymentTools.DepartmentBranchAsync(t, department, cancellationToken).ConfigureAwait(false);
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var readModel = new ProcessReadModel(t.Db);

        var found = await readModel
            .SearchCasesAsync(
                new ProcessCaseFilter(
                    string.IsNullOrWhiteSpace(process) ? null : process.Trim(),
                    wanted,
                    DepartmentId: null,
                    employee,
                    CandidateId: null,
                    HolderRole: null,
                    late,
                    start,
                    end,
                    Search: null,
                    McpToolContext.Today,
                    pageNo,
                    size)
                {
                    DepartmentIds = branch,
                },
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ProcessStepRow> steps = found.Items.Count == 0
            ? []
            : await readModel.ListStepsOfCasesAsync([.. found.Items.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);
        var current = steps.Where(step => step.IsCurrent).ToLookup(step => step.CaseId);

        var set = await LoadSetAsync(SetLoader(readModel), found.Items, [.. current.SelectMany(group => group)], cancellationToken).ConfigureAwait(false);

        var items = found.Items.Select(row =>
        {
            var currentSteps = current[row.Id].Select(set.Current).ToList();

            return new
            {
                id = row.Id,
                process = row.ProcessCode,
                processTitle = set.DefinitionOf(row)?.Title ?? row.ProcessCode,
                status = row.Status,
                outcome = row.Outcome,
                legacy = row.Legacy,
                subject = SubjectOf(set.Subject(row)),
                department = set.DepartmentName(row),
                startedOn = McpToolContext.Iso(row.StartedOn),
                completedAt = McpToolContext.Local(row.CompletedAt),
                cancelledAt = McpToolContext.Local(row.CancelledAt),
                daysLate = currentSteps.Count == 0 ? 0 : currentSteps.Max(step => step.DaysLate),
                currentSteps = currentSteps.Select(step => new
                {
                    code = step.StepCode,
                    title = step.Title,
                    state = step.State,
                    holder = HolderOf(step.Holder),
                    due = McpToolContext.Iso(step.DueDate),
                    daysLate = step.DaysLate,
                    gate = step.GateCode,
                    warnings = step.Warnings,
                }),
            };
        }).ToList();

        return t.Json(new { total = found.Total, page = pageNo, pageSize = size, items }, items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  process_case
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "process_case", Title = "Jarayon",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Bitta jarayon qadamlari bilan: har qadam holati, kimda turibdi, muddat, qachon bajarilgan, qanday yopilgan, to'siq (gate) va "
        + "kiritilgan qiymatlar (yorliqlari bilan, uzun matn 500 belgigacha qisqartirilgan). Identifikator: process_cases yoki process_stuck dan. "
        + "One process case with all its steps.")]
    public async Task<string> ProcessCase(
        [Description("Jarayon identifikatori (process_cases natijasidagi id).")] string id,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.ProcessesView, Area);

        if (!Guid.TryParse(id?.Trim(), out var caseId))
        {
            throw new McpException("«id» — jarayon identifikatori (GUID) bo'lishi kerak.");
        }

        var readModel = new ProcessReadModel(t.Db);

        var cases = await readModel.ListCasesAsync([caseId], cancellationToken).ConfigureAwait(false);
        if (cases.Count == 0)
        {
            throw new McpException("Jarayon topilmadi.");
        }

        var row = cases[0];
        var steps = await readModel.ListStepsOfCasesAsync([caseId], cancellationToken).ConfigureAwait(false);
        var set = await LoadSetAsync(SetLoader(readModel), cases, steps, cancellationToken).ConfigureAwait(false);

        var payload = new
        {
            id = row.Id,
            process = row.ProcessCode,
            processTitle = set.DefinitionOf(row)?.Title ?? row.ProcessCode,
            status = row.Status,
            outcome = row.Outcome,
            legacy = row.Legacy,
            subject = SubjectOf(set.Subject(row)),
            department = set.DepartmentName(row),
            parentCaseId = row.ParentCaseId,
            anchorDate = McpToolContext.Iso(row.AnchorDate),
            startedOn = McpToolContext.Iso(row.StartedOn),
            completedAt = McpToolContext.Local(row.CompletedAt),
            cancelledAt = McpToolContext.Local(row.CancelledAt),
            cancelReason = Bounded(row.CancelReason),
            steps = steps.Select(step =>
            {
                var definition = set.DefinitionOf(step);

                return new
                {
                    code = step.StepCode,
                    title = definition?.Title ?? step.StepCode,
                    column = step.BoardColumn,
                    state = step.State,
                    resolution = step.Resolution,
                    holder = HolderOf(set.Holder(step.HolderRole, step.HolderEmployeeId)),
                    due = McpToolContext.Iso(step.DueDate),
                    daysLate = step.IsOpen ? set.DaysLate(step.DueDate) : 0,
                    doneAt = McpToolContext.Local(step.DoneAt),
                    gate = step.BlockedGateCode,
                    resolver = set.ResolverDto(step) is { } resolver ? HolderOf(resolver) : null,
                    warnings = set.WarningsOf(step),
                    blank = definition?.Blank,
                    linkedBlankId = step.LinkedBlankSubmissionId,
                    inputs = InputsOf(step.Payload, definition),
                };
            }),
        };

        return t.Json(payload, steps.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  process_stuck
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "process_stuck", Title = "Kim kechikdi",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Hozir tiqilib qolgan qadamlar, eng ko'p kechikkani birinchi: blocked — to'siq turgan (kim hal qilishi kerakligi bilan), "
        + "late — muddati o'tgan, warn — ogohlantirish chiqqan (masalan qonuniy muddat o'tgan). Har biri: jarayon, kim haqida, qadam, kimda "
        + "turibdi, muddat, necha kun kechikkan. Bir qadam bir nechta ro'yxatda bo'lishi mumkin. Bo'lim va eng kam kechikish bo'yicha filtrlash "
        + "mumkin. What is stuck right now: blocked, late and warned steps with their holders.")]
    public async Task<string> ProcessStuck(
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan birga.")] Guid? department = null,
        [Description("Kamida shuncha kun kechikkanlar; berilsa, muddati o'tmagan to'siqlar ham chiqmaydi. Bo'sh — hammasi.")] int? minDaysLate = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.ProcessesView, Area);

        var readModel = new ProcessReadModel(t.Db);

        var sets = SetLoader(readModel);

        var versions = await readModel.ListActiveVersionIdsAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await sets.DefinitionsAsync(versions, cancellationToken).ConfigureAwait(false);

        // AKT fires before the step is late, so those steps have to be read even when on time (as #6).
        var actSteps = definitions.Values
            .SelectMany(definition => definition.Steps)
            .Where(step => step.Gates.Any(gate => gate is { Code: ProcessGateCodes.Akt, Mode: ProcessGateMode.Warn }))
            .Select(step => step.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var steps = await readModel.ListProblemStepsAsync(McpToolContext.Today, actSteps, cancellationToken).ConfigureAwait(false);
        var cases = await readModel
            .ListCasesAsync([.. steps.Select(step => step.CaseId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        var branch = await EmploymentTools.DepartmentBranchAsync(t, department, cancellationToken).ConfigureAwait(false);
        if (branch is not null)
        {
            cases = [.. cases.Where(row => row.DepartmentId is { } id && branch.Contains(id))];

            var kept = cases.Select(row => row.Id).ToHashSet();
            steps = [.. steps.Where(step => kept.Contains(step.CaseId))];
        }

        var set = await LoadSetAsync(sets, cases, steps, cancellationToken).ConfigureAwait(false);

        var worstFirst = steps
            .Where(step => minDaysLate is not { } floor || set.DaysLate(step.DueDate) >= floor)
            .OrderBy(step => step.DueDate ?? DateOnly.MaxValue)
            .ThenBy(step => step.CaseId)
            .ThenBy(step => step.OrderNo)
            .ToList();

        var blocked = worstFirst.Where(step => step.State == ProcessStepState.Blocked).ToList();
        var lateSteps = worstFirst.Where(step => step.State == ProcessStepState.Active && set.DaysLate(step.DueDate) > 0).ToList();
        var warned = worstFirst.Where(step => set.WarningsOf(step).Count > 0).ToList();

        var payload = new
        {
            blocked = new { total = blocked.Count, items = blocked.Take(StuckLimit).Select(step => StuckRow(set.Task(step))) },
            late = new { total = lateSteps.Count, items = lateSteps.Take(StuckLimit).Select(step => StuckRow(set.Task(step))) },
            warn = new { total = warned.Count, items = warned.Take(StuckLimit).Select(step => StuckRow(set.Task(step))) },
        };

        return t.Json(payload, blocked.Count + lateSteps.Count + warned.Count);
    }

    private static object StuckRow(ProcessTaskDto task) => new
    {
        caseId = task.CaseId,
        process = task.ProcessCode,
        processTitle = task.ProcessTitle,
        subject = SubjectOf(task.Subject),
        department = task.DepartmentName,
        legacy = task.Legacy,
        step = task.StepCode,
        stepTitle = task.StepTitle,
        state = task.State,
        holder = HolderOf(task.Holder),
        due = McpToolContext.Iso(task.DueDate),
        daysLate = task.DaysLate,
        gate = task.GateCode,
        resolver = task.Resolver is { } resolver ? HolderOf(resolver) : null,
        warnings = task.Warnings,
        blank = task.BlankCode,
    };

    // ─────────────────────────────────────────────────────────────────
    //  Shared
    // ─────────────────────────────────────────────────────────────────

    /// <remarks>
    /// The loader is built per call over the read role's context. Its unreadable-template line stays with the API's
    /// reads and the pass: here such a step just shows its code.
    /// </remarks>
    private ProcessReadSetLoader SetLoader(ProcessReadModel readModel) => new(
        readModel,
        new ProcessTemplateVersionRepository(t.Db),
        new ProcessEngineJournal(NullLogger<ProcessEngineJournal>.Instance),
        new ProcessDefinitionCache(),
        new SystemClock());

    private async Task<ProcessReadSet> LoadSetAsync(
        ProcessReadSetLoader sets,
        IReadOnlyCollection<ProcessCaseRow> cases,
        IReadOnlyCollection<ProcessStepRow> steps,
        CancellationToken cancellationToken)
    {
        var assignments = await t.Db.BlankRoleAssignments
            .Select(row => new { row.RoleCode, row.UserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await sets
            .LoadAsync(
                cases,
                steps,
                new ProcessHolderDirectory(assignments.Select(row => (row.RoleCode, row.UserId))),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static object SubjectOf(ProcessSubjectDto subject) => new
    {
        kind = subject.Kind,
        name = subject.Name,
        position = subject.Position,
        employeeId = subject.EmployeeId,
        candidateId = subject.CandidateId,
    };

    /// <summary>A role, and either the person it resolved to or who stands behind the role; by name only.</summary>
    private static object HolderOf(ProcessHolderDto holder) => new
    {
        role = holder.Role,
        employeeId = holder.EmployeeId,
        name = holder.EmployeeName,
        users = holder.Users.Select(user => user.Name),
        unassigned = holder.Unassigned,
    };

    /// <summary>
    /// §12: the step's payload, labelled from the definition as <c>blank</c> labels its answers, so the reader does not
    /// have to guess at keys. Keyed by input id: <see cref="McpToolContext.Json"/> scrubs a forbidden name only as a key.
    /// </summary>
    private static JsonObject? InputsOf(string? payload, ProcessStep? definition)
    {
        if (string.IsNullOrEmpty(payload) || JsonNode.Parse(payload) is not JsonObject values)
        {
            return null;
        }

        var inputs = definition?.Manual?.Inputs ?? [];
        var labelled = new JsonObject();

        foreach (var (id, value) in values)
        {
            labelled[id] = new JsonObject
            {
                ["label"] = inputs.FirstOrDefault(input => input.Id == id)?.Label,
                ["value"] = BoundedNode(value),
            };
        }

        return labelled;
    }

    private static JsonNode? BoundedNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject map:
                var copy = new JsonObject();
                foreach (var (name, value) in map)
                {
                    copy[name] = BoundedNode(value);
                }

                return copy;

            case JsonArray list:
                return new JsonArray([.. list.Select(BoundedNode)]);

            case JsonValue value when value.TryGetValue(out string? text):
                return JsonValue.Create(Bounded(text));

            default:
                return node?.DeepClone();
        }
    }

    /// <summary>At most <see cref="MaxTextLength"/> chars; a cut never leaves half a surrogate pair, which cannot be written as JSON.</summary>
    private static string? Bounded(string? text)
    {
        if (text is null || text.Length <= MaxTextLength)
        {
            return text;
        }

        var end = MaxTextLength - Cut.Length;
        if (char.IsHighSurrogate(text[end - 1]))
        {
            end--;
        }

        return string.Concat(text.AsSpan(0, end), Cut);
    }
}
