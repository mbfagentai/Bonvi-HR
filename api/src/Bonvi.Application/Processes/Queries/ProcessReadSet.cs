using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

/// <summary>
/// Everything one response knows besides its rows, loaded in one go: definitions, names, resolvers. The handlers
/// only choose rows; every card is shaped here, so one step reads the same in all three tabs.
/// </summary>
public sealed class ProcessReadSet
{
    private readonly IReadOnlyDictionary<(Guid CaseId, string StepCode), ProcessHolderRef> _resolvers;
    private readonly IReadOnlySet<Guid> _emptyHandoverActs;

    internal ProcessReadSet(
        DateOnly today,
        IReadOnlyDictionary<Guid, ProcessCaseRow> cases,
        IReadOnlyDictionary<Guid, ProcessDefinition> definitions,
        ProcessNames names,
        ProcessHolderDirectory directory,
        IReadOnlyDictionary<(Guid CaseId, string StepCode), ProcessHolderRef> resolvers,
        IReadOnlySet<Guid> emptyHandoverActs)
    {
        Today = today;
        Cases = cases;
        Definitions = definitions;
        Names = names;
        Directory = directory;
        _resolvers = resolvers;
        _emptyHandoverActs = emptyHandoverActs;
    }

    public DateOnly Today { get; }

    public IReadOnlyDictionary<Guid, ProcessCaseRow> Cases { get; }

    public IReadOnlyDictionary<Guid, ProcessDefinition> Definitions { get; }

    public ProcessNames Names { get; }

    public ProcessHolderDirectory Directory { get; }

    public ProcessDefinition? DefinitionOf(ProcessCaseRow processCase)
        => Definitions.GetValueOrDefault(processCase.TemplateVersionId);

    public ProcessStep? DefinitionOf(ProcessStepRow step)
        => Cases.TryGetValue(step.CaseId, out var processCase) ? DefinitionOf(processCase)?.FindStep(step.StepCode) : null;

    public int DaysLate(DateOnly? due) => due is { } date && date < Today ? Today.DayNumber - date.DayNumber : 0;

    public string? BlankTitle(string? code) => code is null ? null : Names.BlankTitles.GetValueOrDefault(code);

    public string? DepartmentName(ProcessCaseRow processCase)
        => processCase.DepartmentId is { } id ? Names.Departments.GetValueOrDefault(id) : null;

    public ProcessHolderRef? ResolverOf(ProcessStepRow step) => _resolvers.GetValueOrDefault((step.CaseId, step.StepCode));

    public IReadOnlyList<string> WarningsOf(ProcessStepRow step)
    {
        var employment = Cases.TryGetValue(step.CaseId, out var processCase) ? processCase.EmploymentId : null;

        return ProcessGateReading.Warnings(
            DefinitionOf(step),
            step,
            Today,
            employment is { } id && _emptyHandoverActs.Contains(id));
    }

    public ProcessHolderDto Holder(string? role, Guid? employeeId)
    {
        if (employeeId is { } person)
        {
            return new ProcessHolderDto(role, person, Names.Employees.GetValueOrDefault(person), [], Unassigned: false);
        }

        if (role is null)
        {
            return new ProcessHolderDto(null, null, null, [], Unassigned: false);
        }

        var users = Directory.UsersOf(role);

        return new ProcessHolderDto(
            role,
            null,
            null,
            [
                .. users
                    .Where(Names.Users.ContainsKey)
                    .Select(user => new ProcessUserNameDto(user, Names.Users[user]))
                    .OrderBy(user => user.Name, StringComparer.Ordinal),
            ],
            Unassigned: users.Count == 0 && ProcessRoles.Office.Contains(role));
    }

    public ProcessSubjectDto Subject(ProcessCaseRow processCase)
    {
        ArgumentNullException.ThrowIfNull(processCase);

        var (name, position) = processCase.SubjectKind switch
        {
            ProcessSubjectKind.VacancyRequest when processCase.VacancyRequestId is { } id
                => (Names.VacancyRequests.GetValueOrDefault(id), (string?)null),
            ProcessSubjectKind.Candidate when processCase.CandidateId is { } id
                => (Names.Candidates.GetValueOrDefault(id)?.Name, Names.Candidates.GetValueOrDefault(id)?.VacancyTitle),
            ProcessSubjectKind.Employment when processCase.EmployeeId is { } id
                => (
                    Names.Employees.GetValueOrDefault(id),
                    processCase.EmploymentId is { } employment ? Names.EmploymentPositions.GetValueOrDefault(employment) : null),
            _ => ((string?)null, (string?)null),
        };

        return new ProcessSubjectDto(
            processCase.SubjectKind,
            name,
            position,
            processCase.VacancyRequestId,
            processCase.CandidateId,
            processCase.EmploymentId,
            processCase.ProbationId,
            processCase.EmployeeId);
    }

    public ProcessHolderDto? ResolverDto(ProcessStepRow step)
        => ResolverOf(step) is { } resolver ? Holder(resolver.Role, resolver.EmployeeId) : null;

    public static ProcessLinkDto LinkOf(ProcessStep? definition)
        => definition is null
            ? new ProcessLinkDto(ProcessScreens.None, new Dictionary<string, string>())
            : new ProcessLinkDto(definition.Link.Screen, definition.Link.Args);

    public ProcessTaskDto Task(ProcessStepRow step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var processCase = Cases[step.CaseId];
        var definition = DefinitionOf(step);

        return new ProcessTaskDto(
            step.CaseId,
            processCase.ProcessCode,
            DefinitionOf(processCase)?.Title ?? processCase.ProcessCode,
            step.StepCode,
            definition?.Title ?? step.StepCode,
            step.State,
            step.DueDate,
            DaysLate(step.DueDate),
            Subject(processCase),
            processCase.DepartmentId,
            DepartmentName(processCase),
            definition?.Blank,
            BlankTitle(definition?.Blank),
            step.LinkedBlankSubmissionId,
            processCase.Legacy,
            step.BlockedGateCode,
            ProcessGateReading.ErrorCodeOf(step.BlockedGateCode),
            Holder(step.HolderRole, step.HolderEmployeeId),
            ResolverDto(step),
            WarningsOf(step),
            LinkOf(definition));
    }

    public ProcessCurrentStepDto Current(ProcessStepRow step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return new ProcessCurrentStepDto(
            step.StepCode,
            DefinitionOf(step)?.Title ?? step.StepCode,
            step.BoardColumn,
            step.State,
            Holder(step.HolderRole, step.HolderEmployeeId),
            step.DueDate,
            DaysLate(step.DueDate),
            step.BlockedGateCode,
            WarningsOf(step));
    }
}

/// <summary>Loads a <see cref="ProcessReadSet"/>; a fixed handful of queries, however many rows it is given.</summary>
public sealed class ProcessReadSetLoader
{
    private readonly IProcessReadModel _readModel;
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly IProcessEngineJournal _journal;
    private readonly ProcessDefinitionCache _definitions;
    private readonly IClock _clock;

    public ProcessReadSetLoader(
        IProcessReadModel readModel,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        ProcessDefinitionCache definitions,
        IClock clock)
    {
        _readModel = readModel;
        _versions = versions;
        _journal = journal;
        _definitions = definitions;
        _clock = clock;
    }

    public Task<IReadOnlyDictionary<Guid, ProcessDefinition>> DefinitionsAsync(
        IEnumerable<Guid> versionIds,
        CancellationToken cancellationToken = default)
        => _definitions.GetAsync(versionIds, _versions, _journal, cancellationToken);

    /// <remarks>
    /// <para><c>cases</c>: Cases of the response.</para>
    /// <para><c>steps</c>: Steps of the response; each must belong to one of <paramref name="cases"/>.</para>
    /// <para><c>directory</c>: Who stands behind each office role.</para>
    /// <para><c>alsoUserIds</c>: Users named outside the holders (who closed a step, who granted an override).</para>
    /// </remarks>
    public async Task<ProcessReadSet> LoadAsync(
        IReadOnlyCollection<ProcessCaseRow> cases,
        IReadOnlyCollection<ProcessStepRow> steps,
        ProcessHolderDirectory directory,
        IReadOnlyCollection<Guid>? alsoUserIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(directory);

        var caseById = cases.ToDictionary(row => row.Id);
        var definitions = await DefinitionsAsync(cases.Select(row => row.TemplateVersionId), cancellationToken).ConfigureAwait(false);
        var resolvers = await ProcessGateReading
            .ResolversAsync(steps, caseById, definitions, _readModel, cancellationToken)
            .ConfigureAwait(false);

        var stepDefinitions = steps
            .Select(step => (Step: step, Definition: definitions.GetValueOrDefault(caseById[step.CaseId].TemplateVersionId)?.FindStep(step.StepCode)))
            .ToList();

        var emptyActs = await EmptyHandoverActsAsync(stepDefinitions, caseById, cancellationToken).ConfigureAwait(false);

        var holders = steps
            .Select(step => new ProcessHolderRef(step.HolderRole, step.HolderEmployeeId))
            .Concat(resolvers.Values)
            .ToList();

        var names = await _readModel
            .LoadNamesAsync(
                new ProcessNameQuery
                {
                    EmployeeIds = Ids(holders.Select(holder => holder.EmployeeId).Concat(cases.Select(row => row.EmployeeId))),
                    CandidateIds = Ids(cases.Select(row => row.CandidateId)),
                    VacancyRequestIds = Ids(cases.Select(row => row.VacancyRequestId)),
                    EmploymentIds = Ids(cases.Select(row => row.EmploymentId)),
                    DepartmentIds = Ids(cases.Select(row => row.DepartmentId)),
                    UserIds =
                    [
                        .. holders
                            .Where(holder => holder is { EmployeeId: null, Role: not null })
                            .SelectMany(holder => directory.UsersOf(holder.Role!))
                            .Concat(alsoUserIds ?? [])
                            .Distinct(),
                    ],
                    BlankCodes =
                    [
                        .. stepDefinitions
                            .Select(row => row.Definition?.Blank)
                            .OfType<string>()
                            .Distinct(StringComparer.Ordinal),
                    ],
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessReadSet(_clock.Today, caseById, definitions, names, directory, resolvers, emptyActs);
    }

    private async Task<IReadOnlySet<Guid>> EmptyHandoverActsAsync(
        List<(ProcessStepRow Step, ProcessStep? Definition)> steps,
        Dictionary<Guid, ProcessCaseRow> cases,
        CancellationToken cancellationToken)
    {
        var employments = Ids(steps
            .Where(row => row.Step.IsOpen
                && row.Definition is { } definition
                && definition.Gates.Any(gate => gate is { Code: ProcessGateCodes.Akt, Mode: ProcessGateMode.Warn }))
            .Select(row => cases[row.Step.CaseId].EmploymentId));

        return employments.Count == 0
            ? new HashSet<Guid>()
            : await _readModel.ListEmptyHandoverActsAsync(employments, cancellationToken).ConfigureAwait(false);
    }

    private static List<Guid> Ids(IEnumerable<Guid?> ids) => [.. ids.OfType<Guid>().Distinct()];
}
