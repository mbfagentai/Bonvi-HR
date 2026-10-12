using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Queries;

/// <summary>
/// Xodimning butun mehnat yo'li — bitta tartiblangan lentada.
/// </summary>
/// <param name="Id">Xodim identifikatori.</param>
/// <param name="Newest">
/// <c>true</c> — eng yangi hodisa tepada (interfeysdagi vertikal lenta shunday).
/// <c>false</c> — xronologik tartib.
/// </param>
public sealed record GetEmployeeTimelineQuery(Guid Id, bool Newest = true);

public sealed class GetEmployeeTimelineQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly EmployeeMapper _mapper;
    private readonly CatalogNameResolver _names;

    public GetEmployeeTimelineQueryHandler(
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        EmployeeMapper mapper,
        CatalogNameResolver names)
    {
        _employees = employees;
        _employments = employments;
        _mapper = mapper;
        _names = names;
    }

    public async Task<Result<EmployeeTimelineDto>> HandleAsync(
        GetEmployeeTimelineQuery query,
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

        var loaded = new List<Employment>(employments.Count);

        foreach (var summary in employments)
        {
            // Hodisalar bilan qayta yuklanadi: `GetByEmployeeAsync` munosabatning
            // o'zini beradi, hodisalar esa alohida agregat to'plami.
            loaded.Add(await _employments
                .GetWithEventsAsync(summary.Id, cancellationToken)
                .ConfigureAwait(false) ?? summary);
        }

        // Buyruq mualliflari (§7.9) — barcha munosabatlar uchun bitta so'rov.
        await _mapper
            .PreloadAuthorsAsync(loaded.SelectMany(employment => employment.Events), cancellationToken)
            .ConfigureAwait(false);

        var items = new List<(DateOnly Effective, DateTimeOffset Recorded, TimelineItemDto Item)>();

        foreach (var employment in loaded)
        {
            var legalEntity = await _names
                .LegalEntityAsync(employment.LegalEntityId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var employmentEvent in employment.Events)
            {
                var dto = await _mapper.ToDtoAsync(employmentEvent, cancellationToken).ConfigureAwait(false);
                items.Add((
                    employmentEvent.EffectiveDate,
                    employmentEvent.RecordedAt,
                    new TimelineItemDto(employment.Id, legalEntity, dto)));
            }
        }

        // Tartib SANA bo'yicha, munosabat bo'yicha EMAS: bu lentaning butun
        // ma'nosi — «2019 VIKOR → 2021 Вело унион → 2023 qaytdi» ketma-ketligini
        // bitta ustunda ko'rsatish. Bir kunda ikki hodisa bo'lsa (masalan
        // bo'shatish va yangi firmaga qabul), yozilgan vaqt hal qiladi.
        var ordered = query.Newest
            ? items.OrderByDescending(x => x.Effective).ThenByDescending(x => x.Recorded)
            : items.OrderBy(x => x.Effective).ThenBy(x => x.Recorded);

        IReadOnlyList<TimelineItemDto> result = [.. ordered.Select(x => x.Item)];

        return Result.Success(new EmployeeTimelineDto(
            employee.Id,
            employee.FullName.Full,
            result));
    }
}
