using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Positions;

/// <summary>Lavozimga yangi vazifa qo'shish.</summary>
/// <param name="PositionId">Lavozim.</param>
/// <param name="Text">Vazifa matni.</param>
public sealed record AddPositionDutyCommand(Guid PositionId, string? Text);

/// <summary>Lavozimdan vazifani olib tashlash.</summary>
/// <param name="PositionId">Lavozim.</param>
/// <param name="DutyId">Vazifa.</param>
public sealed record RemovePositionDutyCommand(Guid PositionId, Guid DutyId);

/// <summary>Vazifalarni qayta tartiblash.</summary>
/// <param name="PositionId">Lavozim.</param>
/// <param name="OrderedIds">Yangi ketma-ketlikdagi vazifa identifikatorlari.</param>
public sealed record ReorderPositionDutiesCommand(Guid PositionId, IReadOnlyList<Guid> OrderedIds);

/// <summary>
/// Lavozimga vazifa qo'shadi.
/// </summary>
/// <remarks>
/// Matnni tozalash va takrorni tekshirish domenda (<c>Position.AddDuty</c>)
/// bajariladi: «Ускунани созлаш» va «Ускунани  созлаш» bitta vazifa,
/// ularni ikki marta qo'shib bo'lmaydi.
/// Javobda YANGILANGAN to'liq ro'yxat qaytariladi — interfeys uni
/// qayta so'ramasdan ko'rsatadi.
/// </remarks>
public sealed class AddPositionDutyCommandHandler
{
    private readonly IPositionRepository _positions;
    private readonly IUnitOfWork _unitOfWork;

    public AddPositionDutyCommandHandler(IPositionRepository positions, IUnitOfWork unitOfWork)
    {
        _positions = positions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<PositionDutyDto>>> HandleAsync(
        AddPositionDutyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var position = await _positions
            .GetWithDutiesAsync(command.PositionId, cancellationToken)
            .ConfigureAwait(false);

        if (position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        var added = position.AddDuty(command.Text);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PositionDefinition.ToDuties(position));
    }
}

/// <summary>
/// Lavozimdan vazifani olib tashlaydi.
/// </summary>
/// <remarks>
/// Mavjud bo'lmagan vazifa uchun xato QAYTARILMAYDI: amal natijasi
/// baribir kerakli holat — «bunday vazifa yo'q». Ikki marta bosilgan
/// «o'chirish» tugmasi xato oynasi ochmasligi kerak.
/// </remarks>
public sealed class RemovePositionDutyCommandHandler
{
    private readonly IPositionRepository _positions;
    private readonly IUnitOfWork _unitOfWork;

    public RemovePositionDutyCommandHandler(IPositionRepository positions, IUnitOfWork unitOfWork)
    {
        _positions = positions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<PositionDutyDto>>> HandleAsync(
        RemovePositionDutyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var position = await _positions
            .GetWithDutiesAsync(command.PositionId, cancellationToken)
            .ConfigureAwait(false);

        if (position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        position.RemoveDuty(command.DutyId);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PositionDefinition.ToDuties(position));
    }
}

/// <summary>
/// Vazifalarni foydalanuvchi bergan ketma-ketlikka keltiradi.
/// </summary>
/// <remarks>
/// Vazifalar tartibi mazmunli: lavozim yo'riqnomasida ular muhimlik
/// bo'yicha yoziladi va shu tartibda chop etiladi.
/// </remarks>
public sealed class ReorderPositionDutiesCommandHandler
{
    private readonly IPositionRepository _positions;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderPositionDutiesCommandHandler(IPositionRepository positions, IUnitOfWork unitOfWork)
    {
        _positions = positions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<PositionDutyDto>>> HandleAsync(
        ReorderPositionDutiesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var position = await _positions
            .GetWithDutiesAsync(command.PositionId, cancellationToken)
            .ConfigureAwait(false);

        if (position is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        position.ReorderDuties(command.OrderedIds);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PositionDefinition.ToDuties(position));
    }
}
