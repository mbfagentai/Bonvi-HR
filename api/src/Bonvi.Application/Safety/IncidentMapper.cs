using Bonvi.Application.Employees.Common;
using Bonvi.Application.Safety.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Safety;

namespace Bonvi.Application.Safety;

/// <summary><see cref="Incident"/> ni DTO'ga o'giradi — xodim va lavozim nomlari bilan.</summary>
public sealed class IncidentMapper
{
    private readonly IEmployeeRepository _employees;
    private readonly CatalogNameResolver _names;

    public IncidentMapper(IEmployeeRepository employees, CatalogNameResolver names)
    {
        _employees = employees;
        _names = names;
    }

    public async Task<IncidentDto> ToDtoAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var employee = await _employees.GetByIdAsync(incident.EmployeeId, cancellationToken).ConfigureAwait(false);

        var responsible = incident.ResponsibleEmployeeId is { } responsibleId
            ? await _employees.GetByIdAsync(responsibleId, cancellationToken).ConfigureAwait(false)
            : null;

        var positionName = await _names.PositionAsync(incident.PositionId, cancellationToken).ConfigureAwait(false);

        return new IncidentDto(
            incident.Id,
            incident.EmployeeId,
            employee?.FullName.Full,
            employee?.PhotoKey,
            incident.IncidentDate,
            incident.Shift,
            incident.PositionId,
            positionName,
            incident.Location,
            incident.Description,
            incident.Cause,
            incident.CorrectiveAction,
            incident.Severity,
            incident.MedicalAidProvided,
            incident.MedicalAidNote,
            incident.CommissionConclusion,
            incident.ConcludedAt,
            incident.ResponsibleEmployeeId,
            responsible?.FullName.Full);
    }
}
