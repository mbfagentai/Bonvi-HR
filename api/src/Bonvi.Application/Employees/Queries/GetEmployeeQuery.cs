using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Queries;

/// <summary>Xodim kartasi: shaxs + barcha mehnat munosabatlari + belgilar.</summary>
public sealed record GetEmployeeQuery(Guid Id);

public sealed class GetEmployeeQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly IEmployeeFlagRepository _flags;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;

    public GetEmployeeQueryHandler(
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IProbationRepository probations,
        IEmployeeFlagRepository flags,
        EmployeeMapper mapper,
        IClock clock)
    {
        _employees = employees;
        _employments = employments;
        _probations = probations;
        _flags = flags;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<Result<EmployeeCardDto>> HandleAsync(
        GetEmployeeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employee = await _employees.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var employments = await _employments
            .GetByEmployeeAsync(employee.Id, cancellationToken)
            .ConfigureAwait(false);

        var cards = new List<EmploymentDto>(employments.Count);

        // Munosabatlar tartibi: eng yangisi tepada — karta ochilganda HR
        // avvalo hozirgi ishni ko'rishi kerak, arxivni emas.
        foreach (var employment in employments.OrderByDescending(e => e.HireDate).ThenByDescending(e => e.Id))
        {
            // Har bir munosabat uchun alohida so'rov: bitta xodimda ular
            // odatda 1–3 ta. Ommaviy yuklash uchun portga metod qo'shish
            // shu narxga arzimaydi.
            var probation = await _probations
                .GetByEmploymentAsync(employment.Id, cancellationToken)
                .ConfigureAwait(false);

            cards.Add(await _mapper.ToDtoAsync(employment, probation, cancellationToken).ConfigureAwait(false));
        }

        var flags = await _flags
            .GetByEmployeeAsync(employee.Id, cancellationToken)
            .ConfigureAwait(false);

        var today = _clock.Today;

        IReadOnlyList<EmployeeFlagDto> flagDtos =
        [
            .. flags
                .OrderByDescending(flag => flag.StartDate)
                .Select(flag => EmployeeMapper.ToDto(flag, today)),
        ];

        var totalRate = await _employments
            .GetTotalActiveRateAsync(employee.Id, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new EmployeeCardDto(
            EmployeeMapper.ToDto(employee),
            cards,
            flagDtos,
            totalRate));
    }
}
