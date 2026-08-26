using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Identity.Users.Commands.CreateUser;

/// <summary>Yangi foydalanuvchi yaratish so'rovi.</summary>
/// <param name="Login">Telefon raqami: 9 xona yoki +998 bilan.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="Password">
/// Boshlang'ich parol — ADMINISTRATOR o'zi kiritadi va yangi foydalanuvchiga
/// shaxsan (og'zaki yoki xizmat orqali) yetkazadi. Tizim uni hech qayerga
/// yubormaydi va javobda qaytarmaydi.
/// </param>
/// <param name="RoleCodes">Biriktiriladigan rol kodlari (masalan <c>["hr_manager"]</c>).</param>
public sealed record CreateUserCommand(
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? Password,
    IReadOnlyList<string>? RoleCodes);

/// <summary>
/// Yangi hisob yaratadi va unga bitta yoki bir nechta rol biriktiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Parolni kim o'ylab topadi.</b> Tizimda o'zi parol yuboradigan kanal
/// (SMS, email) yo'q — HR bo'limi bir-birini shaxsan biladi va yangi hisob
/// kamdan-kam ochiladi (yiliga bir necha marta). Shu sababli administrator
/// parolni o'zi tanlab kiritadi va uni yangi foydalanuvchiga to'g'ridan-to'g'ri
/// aytadi. Tizim tomonidan tasodifiy parol generatsiya qilib, uni javobda
/// bir martalik ko'rsatish — qo'shimcha murakkablik, lekin bu yerdagi
/// yetkazish muammosini hal qilmaydi (baribir og'zaki aytish kerak bo'ladi).
/// </para>
/// <para>
/// <b>Birinchi kirishda parolni almashtirish talab qilinmaydi.</b> Bu odatiy
/// korporativ talab, lekin bu yerda ortiqcha: hisoblar soni — bir nechta,
/// hammasi administratorga ma'lum. Talab qo'shilsa, yangi holat maydoni,
/// alohida oqim va test kerak bo'lardi — foyda esa deyarli sezilmaydi.
/// </para>
/// </remarks>
public sealed class CreateUserCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        IUserRepository users,
        IRoleRepository roles,
        IPasswordHasher hasher,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _roles = roles;
        _hasher = hasher;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserSummaryDto>> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loginResult = PhoneNumber.Create(command.Login);
        if (loginResult.IsFailure)
        {
            return loginResult.Error;
        }

        var nameResult = FullName.Create(command.LastName, command.FirstName, command.MiddleName);
        if (nameResult.IsFailure)
        {
            return nameResult.Error;
        }

        var passwordCheck = PasswordPolicy.Validate(command.Password);
        if (passwordCheck.IsFailure)
        {
            return passwordCheck.Error;
        }

        var rolesResult = await RoleResolver.ResolveAsync(_roles, command.RoleCodes, cancellationToken)
            .ConfigureAwait(false);

        if (rolesResult.IsFailure)
        {
            return rolesResult.Error;
        }

        var taken = await _users.LoginExistsAsync(loginResult.Value, exceptUserId: null, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return UserErrors.LoginTaken;
        }

        var userResult = User.Create(loginResult.Value, _hasher.Hash(command.Password!), nameResult.Value);
        if (userResult.IsFailure)
        {
            return userResult.Error;
        }

        var user = userResult.Value;
        var roles = rolesResult.Value;

        foreach (var role in roles)
        {
            user.AssignRole(role.Id);
        }

        _users.Add(user);

        _auditLogger.Record(
            AuditAction.Create,
            "User",
            user.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(new
            {
                login = user.Login.Value,
                fullName = user.FullName.Full,
                roles = roles.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal),
            }));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UserSummaryDtoFactory.Create(user, roles);
    }
}
