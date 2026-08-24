using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Positions;

/// <summary>Bitta lavozimni vazifalari bilan so'rash.</summary>
/// <param name="Id">Lavozim identifikatori.</param>
public sealed record GetPositionQuery(Guid Id);

/// <summary>
/// Lavozimni VAZIFALARI BILAN qaytaradi.
/// </summary>
/// <remarks>
/// Vazifalar alohida so'rovda emas, shu yerda beriladi: lavozim kartochkasi
/// ochilganda ular baribir darhol kerak bo'ladi va ikkinchi so'rov
/// ekranning yarim yuklangan holatini hosil qilardi.
/// </remarks>
public sealed class GetPositionQueryHandler
{
    private readonly IPositionRepository _positions;

    public GetPositionQueryHandler(IPositionRepository positions) => _positions = positions;

    public async Task<Result<PositionDetailsDto>> HandleAsync(
        GetPositionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var position = await _positions.GetWithDutiesAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        return Result.Success(PositionDefinition.ToDetails(position));
    }
}
