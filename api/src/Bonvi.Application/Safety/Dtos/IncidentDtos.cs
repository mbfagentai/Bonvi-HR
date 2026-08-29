using Bonvi.Domain.Safety;

namespace Bonvi.Application.Safety.Dtos;

/// <summary>Baxtsiz hodisa yozuvi — jabrlangan va mas'ul xodim nomlari bilan.</summary>
public sealed record IncidentDto(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    string? PhotoKey,
    DateOnly IncidentDate,
    string? Shift,
    string Location,
    string Description,
    string? Cause,
    string? CorrectiveAction,
    IncidentSeverity Severity,
    bool MedicalAidProvided,
    string? MedicalAidNote,
    string? CommissionConclusion,
    DateOnly? ConcludedAt,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName);
