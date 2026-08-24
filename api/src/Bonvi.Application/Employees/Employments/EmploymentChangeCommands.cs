using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Employments;

/// <summary>
/// Lavozim yoki bo'lim o'zgarishi.
/// </summary>
/// <remarks>
/// Bo'sh qoldirilgan maydon «o'zgarmadi» degani: forma avvalgi qiymatlarni
/// oldindan to'ldiradi va HR faqat o'zgargan qatorga tegadi.
/// </remarks>
public sealed record TransferEmployeeCommand(
    Guid EmploymentId,
    DateOnly? EffectiveDate = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null);

/// <summary>Qo'shimcha vazifa (sovmestitelstvo emas — mavjud ish doirasida).</summary>
public sealed record AddAdditionalDutyCommand(
    Guid EmploymentId,
    string? Duty,
    DateOnly? EffectiveDate = null,
    decimal? ExtraSalaryAmount = null,
    string? SalaryCurrency = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>Oklad o'zgarishi.</summary>
public sealed record ChangeSalaryCommand(
    Guid EmploymentId,
    decimal? Amount,
    string? Currency = null,
    DateOnly? EffectiveDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>Lavozim/bo'lim o'zgarishini qayd etadi.</summary>
public sealed class TransferEmployeeCommandHandler
{
    private readonly IEmploymentRepository _employments;
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public TransferEmployeeCommandHandler(
        IEmploymentRepository employments,
        IPositionRepository positions,
        IDepartmentRepository departments,
        IProbationRepository probations,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _employments = employments;
        _positions = positions;
        _departments = departments;
        _probations = probations;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmploymentChangeDto>> HandleAsync(
        TransferEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employment = await _employments
            .GetWithEventsAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return EmployeeErrors.EmploymentNotFound;
        }

        // Ma'lumotnoma yozuvi bor-yo'qligi OLDIN tekshiriladi: aks holda
        // xato tashqi kalit cheklovidan kelib chiqib, 500 bo'lardi.
        if (command.DepartmentId is { } departmentId
            && !await _departments.ExistsAsync(departmentId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.DepartmentNotFound;
        }

        if (command.PositionId is { } positionId
            && !await _positions.ExistsAsync(positionId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.PositionNotFound;
        }

        Money? salary = null;
        if (command.SalaryAmount is { } amount)
        {
            var money = Money.Create(amount, command.SalaryCurrency ?? Money.Uzs);
            if (money.IsFailure)
            {
                return money.Error;
            }

            salary = money.Value;
        }

        var transferred = employment.Transfer(
            command.EffectiveDate ?? _clock.Today,
            command.DepartmentId,
            command.PositionId,
            salary,
            command.OrderNo,
            command.OrderDate,
            command.Note);

        if (transferred.IsFailure)
        {
            return transferred.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await ResultAsync(employment, _probations, _mapper, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Javobni yig'adi — uchala o'zgarish amali uchun umumiy.</summary>
    internal static async Task<Result<EmploymentChangeDto>> ResultAsync(
        Employment employment,
        IProbationRepository probations,
        EmployeeMapper mapper,
        IReadOnlyList<HrWarning> warnings,
        CancellationToken cancellationToken)
    {
        var probation = await probations
            .GetByEmploymentAsync(employment.Id, cancellationToken)
            .ConfigureAwait(false);

        var dto = await mapper.ToDtoAsync(employment, probation, cancellationToken).ConfigureAwait(false);

        return Result.Success(new EmploymentChangeDto(dto, warnings));
    }
}

/// <summary>Qo'shimcha vazifani qayd etadi.</summary>
public sealed class AddAdditionalDutyCommandHandler
{
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AddAdditionalDutyCommandHandler(
        IEmploymentRepository employments,
        IProbationRepository probations,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _employments = employments;
        _probations = probations;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmploymentChangeDto>> HandleAsync(
        AddAdditionalDutyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employment = await _employments
            .GetWithEventsAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return EmployeeErrors.EmploymentNotFound;
        }

        Money? extra = null;
        if (command.ExtraSalaryAmount is { } amount)
        {
            var money = Money.Create(amount, command.SalaryCurrency ?? Money.Uzs);
            if (money.IsFailure)
            {
                return money.Error;
            }

            extra = money.Value;
        }

        var added = employment.AddAdditionalDuty(
            command.EffectiveDate ?? _clock.Today,
            command.Duty ?? string.Empty,
            extra,
            command.OrderNo,
            command.OrderDate);

        if (added.IsFailure)
        {
            return added.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await TransferEmployeeCommandHandler
            .ResultAsync(employment, _probations, _mapper, [], cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Oklad o'zgarishini qayd etadi.</summary>
/// <remarks>
/// Amal <c>employment.manage</c> ruxsati bilan bajariladi, lekin natijadagi
/// oklad maydoni <c>salary.view</c> bo'lmasa ko'rinmaydi: «o'zgartira olish»
/// va «ko'ra olish» — ikki xil huquq.
/// </remarks>
public sealed class ChangeSalaryCommandHandler
{
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeSalaryCommandHandler(
        IEmploymentRepository employments,
        IProbationRepository probations,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _employments = employments;
        _probations = probations;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmploymentChangeDto>> HandleAsync(
        ChangeSalaryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employment = await _employments
            .GetWithEventsAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return EmployeeErrors.EmploymentNotFound;
        }

        Money? salary = null;
        if (command.Amount is { } amount)
        {
            var money = Money.Create(amount, command.Currency ?? Money.Uzs);
            if (money.IsFailure)
            {
                return money.Error;
            }

            salary = money.Value;
        }

        var changed = employment.SetSalary(
            salary,
            command.EffectiveDate ?? _clock.Today,
            command.OrderNo,
            command.OrderDate);

        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await TransferEmployeeCommandHandler
            .ResultAsync(employment, _probations, _mapper, [], cancellationToken)
            .ConfigureAwait(false);
    }
}
