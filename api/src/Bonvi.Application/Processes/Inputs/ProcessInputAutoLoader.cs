using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Inputs;

/// <summary>Reads what the subject's records hold, as yes/no. A fixed number of queries whatever the case holds.</summary>
public interface IProcessSubjectDataReader
{
    Task<ProcessSubjectData> LoadAsync(Guid? employeeId, Guid? employmentId, Guid? candidateId, CancellationToken cancellationToken = default);
}

/// <summary>The data-backed items of a case's open steps. The read and the write take them from here, so they can't disagree.</summary>
public sealed class ProcessInputAutoLoader
{
    private readonly IProcessSubjectDataReader _reader;

    public ProcessInputAutoLoader(IProcessSubjectDataReader reader) => _reader = reader;

    /// <summary>By step code; only open steps the map covers. A case with none of them spends no query.</summary>
    public async Task<IReadOnlyDictionary<string, ProcessStepAutoItems>> LoadAsync(
        string processCode,
        ProcessDefinition definition,
        IReadOnlyDictionary<string, ProcessStepState> stepStates,
        Guid? employeeId,
        Guid? employmentId,
        Guid? candidateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stepStates);

        var covered = definition.Steps
            .Where(step => ProcessInputSources.Covers(processCode, step)
                && stepStates.TryGetValue(step.Id, out var state)
                && state is not (ProcessStepState.Done or ProcessStepState.Skipped))
            .ToList();

        if (covered.Count == 0)
        {
            return new Dictionary<string, ProcessStepAutoItems>(StringComparer.Ordinal);
        }

        var data = await _reader.LoadAsync(employeeId, employmentId, candidateId, cancellationToken).ConfigureAwait(false);

        return covered.ToDictionary(
            step => step.Id,
            step => ProcessInputSources.Resolve(processCode, step, definition, data, stepStates),
            StringComparer.Ordinal);
    }
}
