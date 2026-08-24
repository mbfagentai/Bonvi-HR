using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Flags;

/// <summary>Xodimga belgi qo'yadi.</summary>
/// <remarks>
/// Qora ro'yxat uchun sabab MAJBURIY — bu qoida domenda
/// (<c>belgi.sabab_bosh</c>). Excel'da sababsiz yozuvlar bor edi va bir
/// yildan keyin hech kim nima uchun ekanini bilmasdi.
/// </remarks>
public sealed record RaiseFlagCommand(
    Guid EmployeeId,
    EmployeeFlagType Type,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Reason = null,
    Guid? ResponsibleEmployeeId = null);

/// <summary>Belgini yopadi (masalan nazorat muddati tugadi).</summary>
public sealed record CloseFlagCommand(Guid FlagId, DateOnly? ClosedAt = null, string? Note = null);

public sealed class RaiseFlagCommandHandler
{
    private readonly IEmployeeFlagRepository _flags;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RaiseFlagCommandHandler(
        IEmployeeFlagRepository flags,
        IEmployeeRepository employees,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _flags = flags;
        _employees = employees;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeFlagDto>> HandleAsync(
        RaiseFlagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var exists = await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var today = _clock.Today;

        var raised = EmployeeFlag.Raise(
            command.EmployeeId,
            command.Type,
            command.StartDate ?? today,
            command.Reason,
            command.EndDate,
            command.ResponsibleEmployeeId);

        if (raised.IsFailure)
        {
            return raised.Error;
        }

        _flags.Add(raised.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(raised.Value, today));
    }
}

public sealed class CloseFlagCommandHandler
{
    private readonly IEmployeeFlagRepository _flags;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CloseFlagCommandHandler(IEmployeeFlagRepository flags, IClock clock, IUnitOfWork unitOfWork)
    {
        _flags = flags;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeFlagDto>> HandleAsync(
        CloseFlagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var flag = await _flags.GetByIdAsync(command.FlagId, cancellationToken).ConfigureAwait(false);
        if (flag is null)
        {
            return EmployeeErrors.FlagNotFound;
        }

        var today = _clock.Today;

        var closed = flag.Close(command.ClosedAt ?? today, command.Note);
        if (closed.IsFailure)
        {
            return closed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(flag, today));
    }
}
