using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>HR xodimga ilova hisobini ochadi.</summary>
/// <param name="EmployeeId">Kimga.</param>
/// <param name="Login">
/// Telefon raqami. Bo'sh qoldirilsa — xodim kartasidagi asosiy raqam.
/// </param>
public sealed record IssueEmployeeAccountCommand(Guid EmployeeId, string? Login);

/// <summary>HR parolni tiklaydi — yangi bir martalik parol.</summary>
/// <param name="EmployeeId">Kimning hisobi.</param>
public sealed record ResetEmployeeAccountPasswordCommand(Guid EmployeeId);

/// <summary>HR hisobni yopadi yoki qaytaradi.</summary>
/// <param name="EmployeeId">Kimning hisobi.</param>
/// <param name="Active"><c>true</c> — qaytarish, <c>false</c> — yopish.</param>
public sealed record SetEmployeeAccountActiveCommand(Guid EmployeeId, bool Active);

/// <summary>«Telefondan belgilash» o'chirgichi.</summary>
/// <param name="EmployeeId">Kimga.</param>
/// <param name="Allowed">Ruxsat berilsinmi.</param>
public sealed record SetMobileCheckInAllowedCommand(Guid EmployeeId, bool Allowed);

/// <summary>
/// <see cref="IssueEmployeeAccountCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// Bir martalik parolni SERVER yasaydi, faqat uning xeshini saqlaydi va
/// ochiq holini javobda BIR MARTA qaytaradi — qurilma qabul tokeni bilan
/// aynan bir xil marosim, ya'ni HR uchun yangi narsa emas.
/// </para>
/// <para>
/// ⚠️ Login — xodim telefonining NUSXASI. Oldindan to'ldiriladi, keyin
/// mustaqil yashaydi (sababi <see cref="EmployeeAccount.Login"/> izohida).
/// <c>employees.phone</c> unikal EMAS, shu sababli ikkinchi bir xil raqamli
/// odam <c>xodim.login_band</c> oladi va HR ga aynan nima qilish kerakligi
/// aytiladi.
/// </para>
/// </remarks>
public sealed class IssueEmployeeAccountCommandHandler
{
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRepository _employees;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public IssueEmployeeAccountCommandHandler(
        IEmployeeAccountRepository accounts,
        IEmployeeRepository employees,
        IPasswordHasher passwordHasher,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _accounts = accounts;
        _employees = employees;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmployeeAccountCreatedDto>> HandleAsync(
        IssueEmployeeAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employee = await _employees.GetByIdAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        // Raqam berilmasa xodim kartasidagisi olinadi. Ikkalasi ham bo'sh
        // bo'lsa — format xatosi (`telefon.bosh`), va bu to'g'ri xabar:
        // HR avval xodim kartasiga raqam yozishi kerak.
        var login = PhoneNumber.Create(command.Login ?? employee.Phone?.Value);
        if (login.IsFailure)
        {
            return login.Error;
        }

        if (await _accounts.FindByEmployeeAsync(employee.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return EmployeeAccountErrors.AlreadyExists;
        }

        if (await _accounts.LoginExistsAsync(login.Value, null, cancellationToken).ConfigureAwait(false))
        {
            return EmployeeAccountErrors.LoginTaken;
        }

        var password = OneTimePassword.Generate();
        var now = _clock.UtcNow;

        var issued = EmployeeAccount.Issue(
            employee.Id,
            login.Value,
            _passwordHasher.Hash(password),
            now);

        if (issued.IsFailure)
        {
            return issued.Error;
        }

        _accounts.Add(issued.Value);

        // Parol ham, uning xeshi ham audit yozuviga HECH QACHON tushmaydi —
        // faqat faktning o'zi (`ResetUserPasswordCommandHandler` qoidasi).
        _auditLogger.Record(
            AuditAction.Create,
            "EmployeeAccount",
            issued.Value.Id.ToString(),
            newValues: "{\"action\":\"issued\"}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EmployeeAccountCreatedDto(EmployeeAccountMapper.ToDto(issued.Value), password);
    }
}

/// <summary>
/// <see cref="ResetEmployeeAccountPasswordCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// Yangi bir martalik parol yasaladi va hisobning BARCHA sessiyalari
/// yopiladi. Sabab <c>ResetUserPasswordCommandHandler</c> dagi bilan bir
/// xil: parol odatda unutilgani yoki sizib chiqqani uchun tiklanadi, eski
/// yangilash tokeni esa yetti kun amal qiladi — u bekor qilinmasa,
/// «tikladim» degan gap yolg'on bo'lardi.
/// </para>
/// <para>
/// ⚠️ <c>last_login_at</c> TEGILMAYDI: «bu odam umuman kirganmi» degan
/// savol parol tiklanganidan keyin ham javobini saqlashi kerak.
/// </para>
/// </remarks>
public sealed class ResetEmployeeAccountPasswordCommandHandler
{
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ResetEmployeeAccountPasswordCommandHandler(
        IEmployeeAccountRepository accounts,
        IEmployeeRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmployeeAccountCreatedDto>> HandleAsync(
        ResetEmployeeAccountPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accounts.FindByEmployeeAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return EmployeeAccountErrors.NotFound;
        }

        var password = OneTimePassword.Generate();
        var now = _clock.UtcNow;

        account.ResetPassword(_passwordHasher.Hash(password), now);

        await _refreshTokens
            .RevokeAllForAccountAsync(account.Id, now, byIp: null, cancellationToken)
            .ConfigureAwait(false);

        _auditLogger.Record(
            AuditAction.Update,
            "EmployeeAccount",
            account.Id.ToString(),
            newValues: "{\"action\":\"password_reset\"}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EmployeeAccountCreatedDto(EmployeeAccountMapper.ToDto(account), password);
    }
}

/// <summary>
/// <see cref="SetEmployeeAccountActiveCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Yopish sessiyalarni ham yopadi: aks holda hisob «faol emas» deb
/// turgani holda ochiq ilova yana yetti kun ishlayverardi. Qaytarish
/// parolni TIKLAMAYDI — odamning eski paroli o'z kuchida qoladi.
/// </remarks>
public sealed class SetEmployeeAccountActiveCommandHandler
{
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SetEmployeeAccountActiveCommandHandler(
        IEmployeeAccountRepository accounts,
        IEmployeeRefreshTokenRepository refreshTokens,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmployeeAccountDto>> HandleAsync(
        SetEmployeeAccountActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accounts.FindByEmployeeAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return EmployeeAccountErrors.NotFound;
        }

        if (command.Active)
        {
            account.Activate();
        }
        else
        {
            account.Deactivate();

            await _refreshTokens
                .RevokeAllForAccountAsync(account.Id, _clock.UtcNow, byIp: null, cancellationToken)
                .ConfigureAwait(false);
        }

        _auditLogger.Record(
            AuditAction.Update,
            "EmployeeAccount",
            account.Id.ToString(),
            newValues: command.Active ? "{\"isActive\":true}" : "{\"isActive\":false}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return EmployeeAccountMapper.ToDto(account);
    }
}

/// <summary>
/// <see cref="SetMobileCheckInAllowedCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Hisobga TEGMAYDI va hisob bor-yo'qligini ham tekshirmaydi: ruxsat
/// odamning ISHIGA tegishli, loginiga emas, ya'ni hisobi hali ochilmagan
/// xodimga ham oldindan berilishi mumkin (sababi
/// <c>Employee.MobileCheckInAllowed</c> izohida).
/// </remarks>
public sealed class SetMobileCheckInAllowedCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public SetMobileCheckInAllowedCommandHandler(
        IEmployeeRepository employees,
        IEmployeeAccountRepository accounts,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _accounts = accounts;
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

        var account = await _accounts.FindByEmployeeAsync(employee.Id, cancellationToken).ConfigureAwait(false);

        return new EmployeeAppAccessDto(
            employee.Id,
            employee.MobileCheckInAllowed,
            account is null ? null : EmployeeAccountMapper.ToDto(account));
    }
}

/// <summary>Hisobni tashqi ko'rinishga o'giradi — yagona nusxa.</summary>
internal static class EmployeeAccountMapper
{
    public static EmployeeAccountDto ToDto(EmployeeAccount account)
        => new(
            account.Id,
            account.EmployeeId,
            account.Login.Value,
            account.MustChangePassword,
            account.PasswordSetAt,
            account.IsActive,
            account.LastLoginAt,
            account.LockedUntil);
}
