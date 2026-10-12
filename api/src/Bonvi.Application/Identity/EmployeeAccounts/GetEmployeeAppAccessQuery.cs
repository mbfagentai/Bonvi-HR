using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>Xodim kartasidagi «Ilova» bo'limi.</summary>
/// <param name="EmployeeId">Xodim.</param>
public sealed record GetEmployeeAppAccessQuery(Guid EmployeeId);

/// <summary>HR xodimning parolini ko'radi.</summary>
/// <param name="EmployeeId">Xodim.</param>
public sealed record RevealEmployeePasswordQuery(Guid EmployeeId);

/// <summary>
/// «Ilova» bo'limining javobini yig'adi — uchala HR endpointi uchun bitta nusxa.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Hisob shu yerda KAFOLATLANADI</b> (ensure-on-read): yangi qabul
/// qilingan odamning hisobi fon vazifasini kutmasdan, HR bo'limni ochgan
/// lahzada paydo bo'ladi.
/// </para>
/// <para>
/// Login — profildagi asosiy telefon. U bo'sh yoki boshqa o'chirilmagan
/// xodimda ham bo'lsa, kirish RAD etiladi va sababi shu javobda aytiladi:
/// kirish endpointi buni oshkor qila olmaydi (umumiy javob), HR esa
/// ko'rishi va profilda tuzatishi kerak.
/// </para>
/// </remarks>
public sealed class EmployeeAppAccessBuilder
{
    /// <summary>Takror raqamli nechta boshqa xodim ko'rsatiladi.</summary>
    public const int MaxSharedShown = 5;

    private readonly IEmployeeAccountRepository _accounts;
    private readonly EmployeeAccountProvisioner _provisioner;
    private readonly ISecretProtector _protector;

    public EmployeeAppAccessBuilder(
        IEmployeeAccountRepository accounts,
        EmployeeAccountProvisioner provisioner,
        ISecretProtector protector)
    {
        _accounts = accounts;
        _provisioner = provisioner;
        _protector = protector;
    }

    /// <summary>Hisobni kafolatlaydi va bo'lim holatini yig'adi.</summary>
    /// <param name="employee">O'chirilmagan xodim.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<EmployeeAppAccessDto> BuildAsync(Employee employee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var account = await _provisioner.EnsureAsync(employee.Id, cancellationToken).ConfigureAwait(false);

        return await BuildAsync(employee, account, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tayyor hisob bilan bo'lim holatini yig'adi.</summary>
    /// <param name="employee">Xodim.</param>
    /// <param name="account">Uning hisobi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<EmployeeAppAccessDto> BuildAsync(
        Employee employee,
        EmployeeAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(account);

        var issue = EmployeeLoginIssue.None;
        IReadOnlyList<EmployeePhoneHolderDto> shared = [];

        if (employee.Phone is null)
        {
            issue = EmployeeLoginIssue.NoPhone;
        }
        else
        {
            // +1: xodimning o'zi ham ro'yxatda — boshqalardan MaxSharedShown
            // tasi ko'rinishi uchun.
            var holders = await _accounts
                .PhoneHoldersAsync(employee.Phone, MaxSharedShown + 1, cancellationToken)
                .ConfigureAwait(false);

            shared =
            [
                .. holders
                    .Where(holder => holder.Id != employee.Id)
                    .Take(MaxSharedShown)
                    .Select(holder => new EmployeePhoneHolderDto(holder.Id, holder.FullName.Full)),
            ];

            if (shared.Count > 0)
            {
                issue = EmployeeLoginIssue.SharedPhone;
            }
        }

        return new EmployeeAppAccessDto(
            employee.Id,
            employee.MobileCheckInAllowed,
            employee.Phone?.Value,
            issue,
            shared,
            new EmployeeAccountDto(
                account.Id,
                account.PasswordSetAt,
                Reveal(account) is not null,
                account.LastLoginAt,
                account.PasswordHandedOutAt));
    }

    /// <summary>Saqlangan parolni ochadi; ocha olmasa <c>null</c>.</summary>
    /// <param name="account">Hisob.</param>
    public string? Reveal(EmployeeAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return account.PasswordProtected is { } stored ? _protector.Unprotect(stored) : null;
    }
}

/// <summary>
/// <see cref="GetEmployeeAppAccessQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// ⚠️ GET, lekin YOZADI — hisob yo'q bo'lsa ochadi (sinf
/// <see cref="EmployeeAppAccessBuilder"/> izohi). Bu ataylab va xavfsiz:
/// amal idempotent, ikkinchi chaqiruv hech narsa yozmaydi.
/// </remarks>
public sealed class GetEmployeeAppAccessQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly EmployeeAppAccessBuilder _builder;

    public GetEmployeeAppAccessQueryHandler(IEmployeeRepository employees, EmployeeAppAccessBuilder builder)
    {
        _employees = employees;
        _builder = builder;
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

        return await _builder.BuildAsync(employee, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// <see cref="RevealEmployeePasswordQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// Egasining qarori (2026-09-27): parol HR ga HAR DOIM ko'rinadi. Shunga
/// qaramay u bo'lim javobida EMAS, alohida so'rovda — ekran uni faqat ko'z
/// tugmasi bosilganda so'raydi, ya'ni ochiq parol keshda, sahifa holatida
/// va brauzer tarixida bekorga yotmaydi.
/// </para>
/// <para>
/// ⭐ Har bir ko'rish AUDITGA yoziladi («kim, qachon, kimning parolini
/// ko'rdi») — parolning o'zi hech qachon.
/// </para>
/// <para>
/// ⭐ Birinchi ko'rish hisobni OCHADI (<see cref="EmployeeAccount.MarkPasswordHandedOut"/>):
/// HR parolni bilib oldi, demak uni xodimga bera oladi. Ungacha avtomatik
/// yasalgan parol bilan kirib bo'lmaydi — hech kim bilmaydigan parolni
/// internetdan taxmin qilishning ma'nosi qolmasin.
/// </para>
/// </remarks>
public sealed class RevealEmployeePasswordQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly EmployeeAccountProvisioner _provisioner;
    private readonly EmployeeAppAccessBuilder _builder;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RevealEmployeePasswordQueryHandler(
        IEmployeeRepository employees,
        EmployeeAccountProvisioner provisioner,
        EmployeeAppAccessBuilder builder,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _employees = employees;
        _provisioner = provisioner;
        _builder = builder;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmployeePasswordDto>> HandleAsync(
        RevealEmployeePasswordQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employee = await _employees.GetByIdAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var account = await _provisioner.EnsureAsync(employee.Id, cancellationToken).ConfigureAwait(false);

        if (_builder.Reveal(account) is not { } password)
        {
            return EmployeeAccountErrors.PasswordUnavailable;
        }

        account.MarkPasswordHandedOut(_clock.UtcNow);

        _auditLogger.Record(
            AuditAction.Export,
            "EmployeeAccount",
            account.Id.ToString(),
            newValues: "{\"action\":\"password_viewed\"}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EmployeePasswordDto(password);
    }
}
