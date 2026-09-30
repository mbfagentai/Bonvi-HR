using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>HR yangi 6 xonali parol yasaydi.</summary>
/// <param name="EmployeeId">Kimning hisobi.</param>
public sealed record RegenerateEmployeePasswordCommand(Guid EmployeeId);

/// <summary>HR o'zi yozgan parolni qo'yadi.</summary>
/// <param name="EmployeeId">Kimning hisobi.</param>
/// <param name="Password">Yangi parol (6–64 belgi).</param>
public sealed record SetEmployeePasswordCommand(Guid EmployeeId, string? Password);

/// <summary>«Telefondan belgilash» o'chirgichi.</summary>
/// <param name="EmployeeId">Kimga.</param>
/// <param name="Allowed">Ruxsat berilsinmi.</param>
public sealed record SetMobileCheckInAllowedCommand(Guid EmployeeId, bool Allowed);

/// <summary>
/// Parolni HR beradi — <see cref="RegenerateEmployeePasswordCommand"/> va
/// <see cref="SetEmployeePasswordCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Parolni FAQAT HR almashtiradi</b> (egasining qarori, 2026-09-27):
/// xodim uchun parol almashtirish endpointi yo'q. Ikki yo'l bir xil oqibat
/// bilan tugaydi — faqat parolning manbai boshqa: tasodifiy 6 raqam
/// (<see cref="EmployeePassword.Generate"/>) yoki HR yozgani
/// (<see cref="EmployeePassword.Validate"/>).
/// </para>
/// <para>
/// ⭐ <b>Hisobning BARCHA sessiyalari yopiladi.</b> Parol odatda unutilgani
/// yoki birovga ma'lum bo'lib qolgani uchun almashtiriladi; eski yangilash
/// tokeni esa yetti kun amal qiladi — u bekor qilinmasa, «almashtirdim»
/// degan gap yolg'on bo'lardi. <c>last_login_at</c> TEGILMAYDI.
/// </para>
/// <para>
/// Audit yozuviga parol ham, xeshi ham tushmaydi — faqat faktning o'zi.
/// </para>
/// </remarks>
public sealed class EmployeePasswordCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly EmployeeAccountProvisioner _provisioner;
    private readonly EmployeeAppAccessBuilder _builder;
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecretProtector _protector;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public EmployeePasswordCommandHandler(
        IEmployeeRepository employees,
        EmployeeAccountProvisioner provisioner,
        EmployeeAppAccessBuilder builder,
        IEmployeeRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ISecretProtector protector,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _employees = employees;
        _provisioner = provisioner;
        _builder = builder;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _protector = protector;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Yangi tasodifiy 6 xonali parol.</summary>
    /// <param name="command">Buyruq.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public Task<Result<EmployeeAppAccessDto>> HandleAsync(
        RegenerateEmployeePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return AssignAsync(command.EmployeeId, EmployeePassword.Generate(), "password_generated", cancellationToken);
    }

    /// <summary>HR yozgan parol.</summary>
    /// <param name="command">Buyruq.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result<EmployeeAppAccessDto>> HandleAsync(
        SetEmployeePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Shakl AVVAL tekshiriladi — xeshlash qimmat amal va uni 3 belgilik
        // parol uchun bajarishning ma'nosi yo'q.
        var password = EmployeePassword.Validate(command.Password);
        if (password.IsFailure)
        {
            return password.Error;
        }

        return await AssignAsync(command.EmployeeId, password.Value, "password_set", cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<EmployeeAppAccessDto>> AssignAsync(
        Guid employeeId,
        string password,
        string action,
        CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var account = await _provisioner.EnsureAsync(employee.Id, cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;

        account.SetPassword(_passwordHasher.Hash(password), _protector.Protect(password), now);

        await _refreshTokens
            .RevokeAllForAccountAsync(account.Id, now, byIp: null, cancellationToken)
            .ConfigureAwait(false);

        _auditLogger.Record(
            AuditAction.Update,
            "EmployeeAccount",
            account.Id.ToString(),
            newValues: "{\"action\":\"" + action + "\"}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _builder.BuildAsync(employee, account, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// <see cref="SetMobileCheckInAllowedCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Ruxsat XODIMGA beriladi, hisobga emas — odamning ISHIGA tegishli, loginiga
/// emas (sababi <c>Employee.MobileCheckInAllowed</c> izohida). Sukut bo'yicha
/// hammada o'chiq; avtomatik hisob ochish uni yoqmaydi.
/// </remarks>
public sealed class SetMobileCheckInAllowedCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly EmployeeAppAccessBuilder _builder;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public SetMobileCheckInAllowedCommandHandler(
        IEmployeeRepository employees,
        EmployeeAppAccessBuilder builder,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _builder = builder;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmployeeAppAccessDto>> HandleAsync(
        SetMobileCheckInAllowedCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employee = await _employees.GetByIdAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        employee.SetMobileCheckInAllowed(command.Allowed);

        _auditLogger.Record(
            AuditAction.Update,
            "Employee",
            employee.Id.ToString(),
            newValues: command.Allowed
                ? "{\"mobileCheckInAllowed\":true}"
                : "{\"mobileCheckInAllowed\":false}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _builder.BuildAsync(employee, cancellationToken).ConfigureAwait(false);
    }
}
