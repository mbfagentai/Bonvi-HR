using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Incentives;

/// <summary>
/// Rag'bat/jarima turini tekshiradi: mavjudmi va summasi bormi.
/// </summary>
/// <remarks>
/// <see cref="Incentive.TypeId"/> uchun tashqi kalit ATAYLAB yo'q (sababi
/// <c>IncentiveConfiguration</c> izohida) — mavjudlik shu yerda, buyruq
/// ishlovchisida tekshiriladi. Yaratish va tahrirlash BIR XIL tekshiruvga
/// muhtoj, shu sababli mantiq ikkalasi uchun ham shu yerda, bitta joyda.
/// </remarks>
public sealed class IncentiveTypeResolver
{
    private readonly IReferenceRepository<AwardType> _awardTypes;
    private readonly IReferenceRepository<PenaltyType> _penaltyTypes;

    public IncentiveTypeResolver(
        IReferenceRepository<AwardType> awardTypes,
        IReferenceRepository<PenaltyType> penaltyTypes)
    {
        _awardTypes = awardTypes;
        _penaltyTypes = penaltyTypes;
    }

    /// <summary>Tur topilmasa mos xato, topilsa uning <c>HasAmount</c> belgisi.</summary>
    public async Task<Result<bool>> HasAmountAsync(
        IncentiveKind kind,
        Guid typeId,
        CancellationToken cancellationToken)
    {
        if (kind == IncentiveKind.Award)
        {
            var type = await _awardTypes.GetByIdAsync(typeId, cancellationToken).ConfigureAwait(false);
            return type is null ? EmployeeErrors.AwardTypeNotFound : Result.Success(type.HasAmount);
        }

        var penaltyType = await _penaltyTypes.GetByIdAsync(typeId, cancellationToken).ConfigureAwait(false);
        return penaltyType is null ? EmployeeErrors.PenaltyTypeNotFound : Result.Success(penaltyType.HasAmount);
    }

    /// <summary>Xom summani <see cref="Money"/>ga o'giradi. <c>null</c> — summasiz.</summary>
    public static Result<Money?> ParseAmount(decimal? amount)
    {
        if (amount is not { } value)
        {
            return Result.Success<Money?>(null);
        }

        var money = Money.Create(value);
        return money.IsFailure ? money.Error : Result.Success<Money?>(money.Value);
    }
}
