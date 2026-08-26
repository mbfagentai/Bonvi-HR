using Bonvi.Application.Employees.Common;
using Bonvi.Application.Feedback.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Feedback;

namespace Bonvi.Application.Feedback;

/// <summary>
/// <see cref="ExitInterview"/> ni DTO'ga o'giradi — ish joyi/bo'lim/lavozim
/// bog'langan <c>Employment</c> orqali qo'shiladi.
/// </summary>
/// <remarks>
/// Hozirgi hajm (Excel: 192 tarixiy yozuv) uchun har qatorda bitta qo'shimcha
/// so'rov muammo emas — <c>IEmployeeReadModel</c>dagi kabi bitta SQL
/// birlashmasi qurishga arzimaydi. Kelajakda hajm o'sib, bu sezilarli
/// bo'lib qolsa, aynan shu yerni <c>IEmployeeReadModel</c> uslubidagi
/// proyeksiyaga almashtirish kifoya — chaqiruvchi kod o'zgarmaydi.
/// </remarks>
public sealed class ExitInterviewMapper
{
    private readonly IEmploymentRepository _employments;
    private readonly IEmployeeRepository _employees;
    private readonly CatalogNameResolver _names;

    public ExitInterviewMapper(
        IEmploymentRepository employments,
        IEmployeeRepository employees,
        CatalogNameResolver names)
    {
        _employments = employments;
        _employees = employees;
        _names = names;
    }

    public async Task<ExitInterviewDto> ToDtoAsync(
        ExitInterview interview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interview);

        var employment = await _employments
            .GetByIdAsync(interview.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        var employee = employment is null
            ? null
            : await _employees.GetByIdAsync(employment.EmployeeId, cancellationToken).ConfigureAwait(false);

        var departmentName = employment is null
            ? null
            : await _names.DepartmentAsync(employment.DepartmentId, cancellationToken).ConfigureAwait(false);

        var positionName = employment is null
            ? null
            : await _names.PositionAsync(employment.PositionId, cancellationToken).ConfigureAwait(false);

        var legalEntityName = employment is null
            ? null
            : await _names.LegalEntityAsync(employment.LegalEntityId, cancellationToken).ConfigureAwait(false);

        var terminationReasonName = employment is null
            ? null
            : await _names
                .TerminationReasonAsync(employment.TerminationReasonId, cancellationToken)
                .ConfigureAwait(false);

        return new ExitInterviewDto(
            interview.Id,
            interview.EmploymentId,
            employee?.Id,
            employee?.FullName.Full,
            employee?.PhotoKey,
            employment?.LegalEntityId,
            legalEntityName,
            employment?.DepartmentId,
            departmentName,
            employment?.PositionId,
            positionName,
            employment?.TerminationDate,
            terminationReasonName,
            interview.InterviewDate,
            interview.Reason,
            interview.WouldRecommend,
            interview.Impressions,
            interview.ConditionsToReturn);
    }
}
