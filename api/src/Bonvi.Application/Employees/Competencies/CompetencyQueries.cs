using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Competencies;

/// <summary>Xodimning qobiliyatlar profili — kartadagi «Qobiliyatlar» tabi uchun.</summary>
public sealed record GetEmployeeCompetencyQuery(Guid EmployeeId);

public sealed class GetEmployeeCompetencyQueryHandler
{
    private readonly IEmployeeCompetencyRepository _competencies;
    private readonly IEmployeeRepository _employees;

    public GetEmployeeCompetencyQueryHandler(
        IEmployeeCompetencyRepository competencies,
        IEmployeeRepository employees)
    {
        _competencies = competencies;
        _employees = employees;
    }

    /// <summary>
    /// Profil topilmasa <c>null</c> qiymat bilan MUVAFFAQIYAT qaytadi —
    /// bu xato emas, faqat HR hali to'ldirmagan degani.
    /// </summary>
    public async Task<Result<EmployeeCompetencyDto?>> HandleAsync(
        GetEmployeeCompetencyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var exists = await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var competency = await _competencies
            .GetByEmployeeAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(competency is null ? null : EmployeeMapper.ToDto(competency));
    }
}
