using System.Collections.Concurrent;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Processes.Queries;

/// <summary>
/// Parsed template versions for the reads: a version's JSON never changes once written (a changed file needs a new
/// version, §3.1), and the validator runs over all of it, which is too much to do for every menu badge.
/// </summary>
/// <remarks>
/// Only a definition that loaded is kept. One that does not is journalled on each read and answers «absent»: the
/// screen shows the step codes instead of failing, as the pass goes on without that case.
/// </remarks>
public sealed class ProcessDefinitionCache
{
    private readonly ConcurrentDictionary<Guid, ProcessDefinition> _byVersion = new();

    public async Task<IReadOnlyDictionary<Guid, ProcessDefinition>> GetAsync(
        IEnumerable<Guid> versionIds,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versionIds);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(journal);

        var wanted = versionIds.Distinct().ToList();
        var missing = wanted.Where(id => !_byVersion.ContainsKey(id)).ToList();

        if (missing.Count > 0)
        {
            foreach (var row in await versions.ListByIdsAsync(missing, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    _byVersion[row.Id] = ProcessDefinitionValidator.Load(row.Definition);
                }
                catch (InvalidOperationException exception)
                {
                    journal.TemplateUnreadable(row.Id, exception);
                }
            }
        }

        return wanted
            .Where(_byVersion.ContainsKey)
            .ToDictionary(id => id, id => _byVersion[id]);
    }
}
