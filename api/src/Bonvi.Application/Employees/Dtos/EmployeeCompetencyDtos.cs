namespace Bonvi.Application.Employees.Dtos;

/// <summary>Xodim qobiliyatlar profili (91.15): til, kompyuter, ta'lim, mutaxassislik, qiziqish.</summary>
public sealed record EmployeeCompetencyDto(
    Guid Id,
    Guid EmployeeId,
    string? Languages,
    int? ComputerLiteracyPercent,
    string? Specialization,
    string? OtherAbilities,
    string? Interest,
    string? EducationPlace);
