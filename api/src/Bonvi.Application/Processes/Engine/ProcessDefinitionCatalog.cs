using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Processes.Engine;

/// <summary>A process's Published version: what new cases start on (§6.9).</summary>
public sealed record ProcessPublishedTemplate(string Code, Guid VersionId, ProcessDefinition Definition);

/// <summary>
/// The template definitions of one pass: new cases take the Published version, a running case keeps its own
/// even after it is retired (§6.9).
/// </summary>
/// <remarks>
/// A definition that does not load is journalled and answers <c>null</c>: its cases wait while every other
/// case of the pass goes on. The seeder validates what it writes, so this means someone edited the row.
/// </remarks>
public sealed class ProcessDefinitionCatalog
{
    private readonly IProcessTemplateVersionRepository _versions;
    private readonly IProcessEngineJournal _journal;
    private readonly Dictionary<string, Guid> _publishedByCode;
    private readonly Dictionary<Guid, ProcessDefinition?> _byVersion = [];

    private ProcessDefinitionCatalog(
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        Dictionary<string, Guid> publishedByCode)
    {
        _versions = versions;
        _journal = journal;
        _publishedByCode = publishedByCode;
    }

    public static async Task<ProcessDefinitionCatalog> LoadAsync(
        IProcessTemplateRepository templates,
        IProcessTemplateVersionRepository versions,
        IProcessEngineJournal journal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(journal);

        var templateRows = await templates.ListAsync(cancellationToken).ConfigureAwait(false);
        var published = await versions.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var codeByTemplate = templateRows.ToDictionary(row => row.Id, row => row.Code);

        var catalog = new ProcessDefinitionCatalog(
            versions,
            journal,
            published
                .Where(row => codeByTemplate.ContainsKey(row.TemplateId))
                .ToDictionary(row => codeByTemplate[row.TemplateId], row => row.Id, StringComparer.Ordinal));

        foreach (var row in published)
        {
            catalog.Parse(row.Id, row.Definition);
        }

        return catalog;
    }

    /// <summary>The version new cases of this process start on; absent or unreadable → journalled, <c>null</c>.</summary>
    public ProcessPublishedTemplate? Published(string processCode)
    {
        if (!_publishedByCode.TryGetValue(processCode, out var versionId) || _byVersion[versionId] is not { } definition)
        {
            _journal.TemplateMissing(processCode);
            return null;
        }

        return new ProcessPublishedTemplate(processCode, versionId, definition);
    }

    /// <summary>Makes <see cref="ForVersion"/> answer for these versions; one query for those not seen yet.</summary>
    public async Task EnsureVersionsAsync(IEnumerable<Guid> versionIds, CancellationToken cancellationToken = default)
    {
        var missing = versionIds.Distinct().Where(id => !_byVersion.ContainsKey(id)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var rows = await _versions.ListByIdsAsync(missing, cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            Parse(row.Id, row.Definition);
        }
    }

    /// <summary>The definition of a version that <see cref="EnsureVersionsAsync"/> covered; <c>null</c> if it does not load.</summary>
    public ProcessDefinition? ForVersion(Guid versionId) => _byVersion.GetValueOrDefault(versionId);

    private void Parse(Guid versionId, string json)
    {
        try
        {
            _byVersion[versionId] = ProcessDefinitionValidator.Load(json);
        }
        catch (InvalidOperationException exception)
        {
            _journal.TemplateUnreadable(versionId, exception);
            _byVersion[versionId] = null;
        }
    }
}
