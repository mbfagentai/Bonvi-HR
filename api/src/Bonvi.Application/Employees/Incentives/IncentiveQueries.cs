using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Incentives;

/// <summary>Xodimning barcha rag'bat/jarima yozuvlari — kartadagi «Rag'bat/Jarima» tabi uchun.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Kind">Faqat shu turdagilar. Bo'sh — hammasi.</param>
public sealed record GetEmployeeIncentivesQuery(Guid EmployeeId, IncentiveKind? Kind = null);

public sealed class GetEmployeeIncentivesQueryHandler
{
    private readonly IIncentiveRepository _incentives;
    private readonly IEmployeeRepository _employees;
    private readonly EmployeeMapper _mapper;

    public GetEmployeeIncentivesQueryHandler(
        IIncentiveRepository incentives,
        IEmployeeRepository employees,
        EmployeeMapper mapper)
    {
        _incentives = incentives;
        _employees = employees;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<IncentiveDto>>> HandleAsync(
        GetEmployeeIncentivesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var exists = await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var rows = await _incentives
            .GetByEmployeeAsync(query.EmployeeId, query.Kind, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<IncentiveDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await _mapper.ToDtoAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<IncentiveDto>>(items);
    }
}
