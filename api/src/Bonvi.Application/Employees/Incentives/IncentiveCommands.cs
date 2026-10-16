using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Incentives;

/// <summary>Rag'bat yoki jarima beradi.</summary>
/// <remarks>
/// Summa kerak-kerakmasligi bu yerda TANLANMAYDI — u tanlangan
/// <c>Type</c>ning <c>HasAmount</c> belgisidan olinadi va domenga
/// o'tkaziladi (<see cref="Incentive.Grant"/> izohida sababi bor).
/// Jarimada summa o'rniga <see cref="Percent"/> berilishi mumkin; buyruq
/// raqami va sanasi faqat rag'batda.
/// </remarks>
public sealed record GrantIncentiveCommand(
    Guid EmployeeId,
    IncentiveKind Kind,
    Guid TypeId,
    DateOnly Date,
    decimal? Amount = null,
    string? GiftDescription = null,
    string? Reason = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    decimal? Percent = null);

/// <summary>Xatolik tuzatiladi: tur, sana, summa yoki foiz, sovg'a, sabab, buyruq.</summary>
public sealed record UpdateIncentiveCommand(
    Guid IncentiveId,
    Guid TypeId,
    DateOnly Date,
    decimal? Amount = null,
    string? GiftDescription = null,
    string? Reason = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    decimal? Percent = null);

public sealed record DeleteIncentiveCommand(Guid IncentiveId);

public sealed class GrantIncentiveCommandHandler
{
    private readonly IIncentiveRepository _incentives;
    private readonly IEmployeeRepository _employees;
    private readonly IncentiveTypeResolver _types;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public GrantIncentiveCommandHandler(
        IIncentiveRepository incentives,
        IEmployeeRepository employees,
        IncentiveTypeResolver types,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _incentives = incentives;
        _employees = employees;
        _types = types;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IncentiveDto>> HandleAsync(
        GrantIncentiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var exists = await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var hasAmount = await _types
            .HasAmountAsync(command.Kind, command.TypeId, cancellationToken)
            .ConfigureAwait(false);
        if (hasAmount.IsFailure)
        {
            return hasAmount.Error;
        }

        var amount = IncentiveTypeResolver.ParseAmount(command.Amount);
        if (amount.IsFailure)
        {
            return amount.Error;
        }

        var granted = Incentive.Grant(
            command.EmployeeId,
            command.Kind,
            command.TypeId,
            command.Date,
            hasAmount.Value,
            amount.Value,
            command.GiftDescription,
            command.Reason,
            command.OrderNo,
            command.OrderDate,
            command.Percent);

        if (granted.IsFailure)
        {
            return granted.Error;
        }

        _incentives.Add(granted.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(granted.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateIncentiveCommandHandler
{
    private readonly IIncentiveRepository _incentives;
    private readonly IncentiveTypeResolver _types;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIncentiveCommandHandler(
        IIncentiveRepository incentives,
        IncentiveTypeResolver types,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _incentives = incentives;
        _types = types;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IncentiveDto>> HandleAsync(
        UpdateIncentiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var incentive = await _incentives.GetByIdAsync(command.IncentiveId, cancellationToken).ConfigureAwait(false);
        if (incentive is null)
        {
            return EmployeeErrors.IncentiveNotFound;
        }

        // `Kind` Update orqali o'zgarmaydi — tur mavjud Kind asosida
        // tekshiriladi (sababi `Incentive.Update` izohida).
        var hasAmount = await _types
            .HasAmountAsync(incentive.Kind, command.TypeId, cancellationToken)
            .ConfigureAwait(false);
        if (hasAmount.IsFailure)
        {
            return hasAmount.Error;
        }

        var amount = IncentiveTypeResolver.ParseAmount(command.Amount);
        if (amount.IsFailure)
        {
            return amount.Error;
        }

        var updated = incentive.Update(
            command.TypeId,
            command.Date,
            hasAmount.Value,
            amount.Value,
            command.GiftDescription,
            command.Reason,
            command.OrderNo,
            command.OrderDate,
            command.Percent);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(incentive, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DeleteIncentiveCommandHandler
{
    private readonly IIncentiveRepository _incentives;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteIncentiveCommandHandler(IIncentiveRepository incentives, IUnitOfWork unitOfWork)
    {
        _incentives = incentives;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteIncentiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var incentive = await _incentives.GetByIdAsync(command.IncentiveId, cancellationToken).ConfigureAwait(false);
        if (incentive is null)
        {
            return EmployeeErrors.IncentiveNotFound;
        }

        // Yumshoq o'chirish — `AuditableInterceptor` `Remove`ni avtomatik
        // `DeletedAt` belgisiga aylantiradi (`ISoftDeletable`).
        _incentives.Remove(incentive);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
