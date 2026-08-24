using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Employees.Employments;

/// <summary>Bitta mehnat munosabati — hodisalari va sinov muddati bilan.</summary>
public sealed record GetEmploymentQuery(Guid Id);

/// <summary>Mehnat munosabati va uning hodisalari.</summary>
public sealed record EmploymentDetailsDto(
    EmploymentDto Employment,
    IReadOnlyList<EmploymentEventDto> Events);

public sealed class GetEmploymentQueryHandler
{
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;

    public GetEmploymentQueryHandler(
        IEmploymentRepository employments,
        IProbationRepository probations,
        EmployeeMapper mapper)
    {
        _employments = employments;
        _probations = probations;
        _mapper = mapper;
    }

    public async Task<Result<EmploymentDetailsDto>> HandleAsync(
        GetEmploymentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employment = await _employments
            .GetWithEventsAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return EmployeeErrors.EmploymentNotFound;
        }

        var probation = await _probations
            .GetByEmploymentAsync(employment.Id, cancellationToken)
            .ConfigureAwait(false);

        var dto = await _mapper.ToDtoAsync(employment, probation, cancellationToken).ConfigureAwait(false);

        var events = new List<EmploymentEventDto>(employment.Events.Count);

        // Xronologik tartib: bitta munosabat kartasida buyruqlar «boshidan
        // oxirigacha» o'qiladi. Umumiy lenta (timeline) esa teskari tartibda.
        foreach (var employmentEvent in employment.Events
            .OrderBy(e => e.EffectiveDate)
            .ThenBy(e => e.RecordedAt))
        {
            events.Add(await _mapper.ToDtoAsync(employmentEvent, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success(new EmploymentDetailsDto(dto, events));
    }
}
