using Bonvi.Domain.Feedback;

namespace Bonvi.Application.Feedback.Dtos;

/// <summary>
/// «Qayta aloqa» (exit interview) yozuvi.
/// </summary>
/// <remarks>
/// Ish joyi (<see cref="LegalEntityName"/>), bo'lim va lavozim bu yerda
/// bog'langan <c>Employment</c>dan olinadi — <c>ExitInterview</c> ularni
/// o'zida SAQLAMAYDI (sababi domen entity izohida). <see cref="TerminationReasonName"/>
/// — rasmiy buyruqdagi sabab, <see cref="ExitInterview.Reason"/> esa
/// xodimning o'z so'zi bilan aytgani: ikkalasi ataylab yonma-yon.
/// </remarks>
public sealed record ExitInterviewDto(
    Guid Id,
    Guid EmploymentId,
    Guid? EmployeeId,
    string? EmployeeName,
    string? PhotoKey,
    Guid? LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? PositionId,
    string? PositionName,
    DateOnly? TerminationDate,
    string? TerminationReasonName,
    DateOnly InterviewDate,
    string Reason,
    RecommendationAnswer? WouldRecommend,
    string? Impressions,
    string? ConditionsToReturn);
