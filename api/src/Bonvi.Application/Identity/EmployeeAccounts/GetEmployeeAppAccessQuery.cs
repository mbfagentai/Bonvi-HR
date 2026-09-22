using Bonvi.Application.Attendance.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>Xodim kartasidagi «Ilova» bo'limi.</summary>
/// <param name="EmployeeId">Xodim.</param>
public sealed record GetEmployeeAppAccessQuery(Guid EmployeeId);

/// <summary>
/// <see cref="GetEmployeeAppAccessQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Hisobi yo'q xodim ham TO'LIQ javob qaytaradi (<c>account: null</c>),
/// 404 emas: ekran «hisob yo'q — ochish» holatini ko'rsatishi kerak va
/// buning uchun «telefondan belgilash» bayrog'i ham baribir kerak bo'ladi.
/// </remarks>
public sealed class GetEmployeeAppAccessQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeAccountRepository _accounts;

    public GetEmployeeAppAccessQueryHandler(
        IEmployeeRepository employees,
        IEmployeeAccountRepository accounts)
    {
        _employees = employees;
        _accounts = accounts;
    }

    public async Task<Result<EmployeeAppAccessDto>> HandleAsync(
        GetEmployeeAppAccessQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employee = await _employees.GetByIdAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var account = await _accounts.FindByEmployeeAsync(employee.Id, cancellationToken).ConfigureAwait(false);

        return new EmployeeAppAccessDto(
            employee.Id,
            employee.MobileCheckInAllowed,
            account is null ? null : EmployeeAccountMapper.ToDto(account));
    }
}
