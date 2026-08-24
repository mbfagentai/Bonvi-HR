using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Commands;

/// <summary>
/// Yangi xodim kartasini yaratadi (mehnat munosabatisiz).
/// </summary>
/// <remarks>
/// Odatda xodim <b>ishga qabul</b> formasi orqali yaratiladi. Bu alohida
/// amal esa boshqa holat uchun: nomzod yoki arxivdan qo'lda kiritilayotgan
/// shaxs — u hali hech qayerda ishlamaydi, lekin kartasi kerak.
/// </remarks>
public sealed class CreateEmployeeCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeCommandHandler(IEmployeeRepository employees, IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeDto>> HandleAsync(
        SaveEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fullName = FullName.Create(command.LastName, command.FirstName, command.MiddleName);
        if (fullName.IsFailure)
        {
            return fullName.Error;
        }

        var created = Employee.Create(fullName.Value, command.BirthDate);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var employee = created.Value;

        var duplicate = await FindPinflConflictAsync(_employees, command.Pinfl, null, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate.IsFailure)
        {
            return duplicate.Error;
        }

        var applied = EmployeeFields.Apply(employee, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        _employees.Add(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(employee));
    }

    /// <summary>
    /// JSHSHIR band emasligini tekshiradi.
    /// </summary>
    /// <remarks>
    /// Bazadagi unikal indeksga tayanib qolmaymiz: u 500 xato beradi va
    /// HR «nima bo'ldi» degan savolga javob topa olmaydi. Bu yerda esa
    /// aniq kod qaytadi va interfeys mavjud kartaga havola ko'rsata oladi.
    /// </remarks>
    internal static async Task<Result> FindPinflConflictAsync(
        IEmployeeRepository employees,
        string? rawPinfl,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var pinfl = EmployeeFields.ParsePinfl(rawPinfl);
        if (pinfl.IsFailure)
        {
            return pinfl.Error;
        }

        if (pinfl.Value is not { } value)
        {
            return Result.Success();
        }

        var taken = await employees
            .PinflExistsAsync(value, exceptId, cancellationToken)
            .ConfigureAwait(false);

        return taken ? EmployeeErrors.PinflTaken : Result.Success();
    }
}

/// <summary>Xodim kartasini tahrirlaydi.</summary>
public sealed class UpdateEmployeeCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmployeeCommandHandler(IEmployeeRepository employees, IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeDto>> HandleAsync(
        Guid id,
        SaveEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employee = await _employees.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var fullName = FullName.Create(command.LastName, command.FirstName, command.MiddleName);
        if (fullName.IsFailure)
        {
            return fullName.Error;
        }

        var conflict = await CreateEmployeeCommandHandler
            .FindPinflConflictAsync(_employees, command.Pinfl, employee.Id, cancellationToken)
            .ConfigureAwait(false);

        if (conflict.IsFailure)
        {
            return conflict.Error;
        }

        var renamed = employee.Rename(fullName.Value);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var applied = EmployeeFields.Apply(employee, command);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        // Qo'lda tahrirlangan yozuv endi tekshirilgan hisoblanadi: import
        // qo'ygan «tuzatish kerak» belgisi shu yerda olib tashlanadi.
        if (employee.NeedsReview)
        {
            employee.MarkReviewed();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(EmployeeMapper.ToDto(employee));
    }
}
