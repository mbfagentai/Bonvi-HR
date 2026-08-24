using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Positions;

/// <summary>Lavozim vazifalarini so'rash.</summary>
/// <param name="PositionId">Lavozim identifikatori.</param>
public sealed record GetPositionDutiesQuery(Guid PositionId);

/// <summary>
/// Lavozimning vazifalarini qaytaradi. ⭐
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu endpoint HR'ning aniq talabidan kelib chiqqan:</b> «lavozimni
/// tanlaganda avtomatik vazifasi ham yuklanadi». Ishga qabul formasida
/// foydalanuvchi lavozimni tanlaydi va vazifalar ro'yxati o'zi to'ladi —
/// ularni qo'lda ko'chirib yozish (Excel'dagi odat) tugaydi.
/// </para>
/// <para>
/// Nega to'liq lavozim emas, faqat vazifalar: forma lavozimning boshqa
/// maydonlarini allaqachon tanlash ro'yxatidan biladi, shu sababli javob
/// iloji boricha kichik bo'lishi kerak — u har bir tanlashda so'raladi.
/// </para>
/// </remarks>
public sealed class GetPositionDutiesQueryHandler
{
    private readonly IPositionRepository _positions;

    public GetPositionDutiesQueryHandler(IPositionRepository positions) => _positions = positions;

    public async Task<Result<IReadOnlyList<PositionDutyDto>>> HandleAsync(
        GetPositionDutiesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var position = await _positions
            .GetWithDutiesAsync(query.PositionId, cancellationToken)
            .ConfigureAwait(false);

        if (position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        return Result.Success(PositionDefinition.ToDuties(position));
    }
}
