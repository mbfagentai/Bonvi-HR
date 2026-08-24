using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Positions;

/// <summary>Lavozimlar ro'yxatini so'rash.</summary>
/// <param name="OnlyActive">Faqat faol lavozimlar.</param>
/// <param name="DepartmentId">
/// Bo'lim bo'yicha filtr. Ishga qabul formasida bo'lim tanlangach,
/// lavozimlar ro'yxati shu bo'limnikiga qisqartiriladi — 150 ta
/// lavozimdan kerakligini topish qiyin.
/// </param>
public sealed record ListPositionsQuery(bool OnlyActive = false, Guid? DepartmentId = null);

/// <summary>
/// Lavozimlar ro'yxatini qaytaradi (vazifalarsiz).
/// </summary>
/// <remarks>
/// Umumiy ro'yxat handleridan farqi — bo'lim bo'yicha filtr. Filtr
/// repozitoriyda bajariladi, xotirada emas: bo'limda 5 ta lavozim bo'lsa,
/// 150 tasini o'qib keyin tashlab yuborish ma'nosiz.
/// </remarks>
public sealed class ListPositionsQueryHandler
{
    private readonly IPositionRepository _positions;
    private readonly IReferenceDefinition<Position, PositionDto> _definition;

    public ListPositionsQueryHandler(
        IPositionRepository positions,
        IReferenceDefinition<Position, PositionDto> definition)
    {
        _positions = positions;
        _definition = definition;
    }

    public async Task<Result<IReadOnlyList<PositionDto>>> HandleAsync(
        ListPositionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = query.DepartmentId is { } departmentId
            ? await _positions.GetByDepartmentAsync(departmentId, cancellationToken).ConfigureAwait(false)
            : await _positions.ListAsync(query.OnlyActive, cancellationToken).ConfigureAwait(false);

        // Bo'lim bo'yicha so'rovda faollik filtri xotirada qo'llanadi:
        // repozitoriy interfeysida bu ikki shartni birlashtirgan metod yo'q,
        // bitta bo'limdagi lavozimlar soni esa o'nlab — filtr arzon.
        var items = query.DepartmentId is not null && query.OnlyActive
            ? found.Where(position => position.IsActive)
            : found;

        IReadOnlyList<PositionDto> result =
        [
            .. items
                .OrderBy(position => position.Name, StringComparer.CurrentCulture)
                .Select(_definition.ToDto),
        ];

        return Result.Success(result);
    }
}
