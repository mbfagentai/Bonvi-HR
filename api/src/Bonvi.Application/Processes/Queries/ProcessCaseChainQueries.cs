using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;

namespace Bonvi.Application.Processes.Queries;

/// <summary>The cases of one person's chain, in reading order; the asked case is always among them.</summary>
/// <param name="CaseIds">Oldest stage first: hire-request, hire-candidate, hire-formalise, as far as they exist.</param>
public sealed record ProcessCaseChainDto(IReadOnlyList<Guid> CaseIds);

/// <summary>
/// Hiring is three cases linked by <c>parent_case_id</c>, and one page to the person reading it. A hire-request can have
/// many candidates, so it belongs to a person's chain only when asked from below: asked for itself, it is just itself.
/// </summary>
/// <remarks>At most three queries, whichever case is asked: the case, then at most two links.</remarks>
public sealed class GetProcessCaseChainQueryHandler
{
    private readonly IProcessReadModel _readModel;

    public GetProcessCaseChainQueryHandler(IProcessReadModel readModel) => _readModel = readModel;

    public async Task<Result<ProcessCaseChainDto>> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asked = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (asked is null)
        {
            return ProcessErrors.NotFound;
        }

        return asked.ProcessCode switch
        {
            ProcessCodes.HireCandidate => new ProcessCaseChainDto(await OfCandidateAsync(asked, cancellationToken).ConfigureAwait(false)),
            ProcessCodes.HireFormalise => new ProcessCaseChainDto(await OfFormaliseAsync(asked, cancellationToken).ConfigureAwait(false)),
            _ => new ProcessCaseChainDto([asked.Id]),
        };
    }

    private async Task<IReadOnlyList<Guid>> OfCandidateAsync(ProcessCaseRow candidate, CancellationToken cancellationToken)
    {
        var request = await ParentAsync(candidate, ProcessCodes.HireRequest, cancellationToken).ConfigureAwait(false);
        var children = await _readModel
            .ListChildCasesAsync(candidate.Id, ProcessCodes.HireFormalise, cancellationToken)
            .ConfigureAwait(false);

        // The starters give a candidate one formalise case; were there ever more, the living and newest is the person's.
        var formalise = children
            .OrderBy(row => row.Status == ProcessCaseStatus.Cancelled)
            .ThenByDescending(row => row.StartedOn)
            .ThenByDescending(row => row.Id)
            .FirstOrDefault();

        return Ids(request, candidate, formalise);
    }

    private async Task<IReadOnlyList<Guid>> OfFormaliseAsync(ProcessCaseRow formalise, CancellationToken cancellationToken)
    {
        var candidate = await ParentAsync(formalise, ProcessCodes.HireCandidate, cancellationToken).ConfigureAwait(false);
        var request = candidate is null
            ? null
            : await ParentAsync(candidate, ProcessCodes.HireRequest, cancellationToken).ConfigureAwait(false);

        return Ids(request, candidate, formalise);
    }

    private async Task<ProcessCaseRow?> ParentAsync(ProcessCaseRow child, string processCode, CancellationToken cancellationToken)
    {
        if (child.ParentCaseId is not { } parentId)
        {
            return null;
        }

        var parent = await FindAsync(parentId, cancellationToken).ConfigureAwait(false);
        return parent?.ProcessCode == processCode ? parent : null;
    }

    private async Task<ProcessCaseRow?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await _readModel.ListCasesAsync([id], cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    private static List<Guid> Ids(params ProcessCaseRow?[] rows)
        => [.. rows.OfType<ProcessCaseRow>().Select(row => row.Id)];
}
