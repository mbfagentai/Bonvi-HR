using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Dtos;

/// <summary>Profildagi bitta til: kod + daraja (JSON'da ikkalasi NOM bilan yuradi).</summary>
public sealed record EmployeeCompetencyLanguageDto(
    LanguageCode Language,
    LanguageLevel Level);

/// <summary>Xodim qobiliyatlar profili (91.15): til, kompyuter, ta'lim, mutaxassislik, qiziqish.</summary>
/// <remarks>
/// <c>Languages</c> — ESKI erkin matn, faqat Excel'dan ko'chgan yozuvlar
/// uchun ko'rsatiladi; yangi kiritishlar <c>LanguageSkills</c> ro'yxatida.
/// </remarks>
public sealed record EmployeeCompetencyDto(
    Guid Id,
    Guid EmployeeId,
    string? Languages,
    IReadOnlyList<EmployeeCompetencyLanguageDto> LanguageSkills,
    int? ComputerLiteracyPercent,
    string? Specialization,
    string? OtherAbilities,
    string? Interest,
    string? EducationPlace,

    // Nomzodlik anketasidan ko'chgan qo'shimcha ma'lumot — tayyor matn
    // (sababi `EmployeeCompetency.CandidateNote` izohida).
    string? CandidateNote);
