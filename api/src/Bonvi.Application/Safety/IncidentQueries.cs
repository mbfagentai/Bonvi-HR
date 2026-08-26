using Bonvi.Application.Safety.Common;
using Bonvi.Application.Safety.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Safety;

namespace Bonvi.Application.Safety;

/// <summary>Baxtsiz hodisalar jurnali — hodisa sanasi bo'yicha kamayish tartibida.</summary>
/// <param name="From">Quyi chegara (o'zi ham kiradi). Bo'sh — chegarasiz.</param>
/// <param name="To">Yuqori chegara (o'zi ham kiradi). Bo'sh — chegarasiz.</param>
/// <param name="Severity">Faqat shu darajadagilar. Bo'sh — hammasi.</param>
public sealed record ListIncidentsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    IncidentSeverity? Severity = null);

public sealed class ListIncidentsQueryHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IncidentMapper _mapper;

    public ListIncidentsQueryHandler(IIncidentRepository incidents, IncidentMapper mapper)
    {
        _incidents = incidents;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<IncidentDto>>> HandleAsync(
        ListIncidentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _incidents
            .ListAsync(query.From, query.To, query.Severity, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<IncidentDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await _mapper.ToDtoAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<IncidentDto>>(items);
    }
}

public sealed record GetIncidentQuery(Guid IncidentId);

public sealed class GetIncidentQueryHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IncidentMapper _mapper;

    public GetIncidentQueryHandler(IIncidentRepository incidents, IncidentMapper mapper)
    {
        _incidents = incidents;
        _mapper = mapper;
    }

    public async Task<Result<IncidentDto>> HandleAsync(
        GetIncidentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var incident = await _incidents.GetByIdAsync(query.IncidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            return SafetyErrors.NotFound;
        }

        return await _mapper.ToDtoAsync(incident, cancellationToken).ConfigureAwait(false);
    }
}
