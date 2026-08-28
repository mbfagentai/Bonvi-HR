using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Common;
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
/// <param name="RoleCode">Biriktiriladigan rol kodi (masalan <c>"hr_manager"</c>).</param>
public sealed record CreateUserCommand(
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? Password,
    string? RoleCode);

/// <summary>
/// Yangi hisob yaratadi va unga YAGONA rol biriktiradi.
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
/// <para>
/// <b>Ikkita rol qoidasi</b> (<see cref="UserRoleRules"/>): so'rovchi shu
/// rolni umuman BERA oladimi (bosh rahbar faqat HR menejer yarata oladi) va
/// bu rolning egasi allaqachon bormi («atigi bitta»). Ikkalasi ham
/// <c>users.manage</c> ruxsatidan MUSTAQIL: ruxsat ikkala rahbarda ham bor.
/// </para>
/// </remarks>
public sealed class CreateUserCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IPasswordHasher hasher,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
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

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

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

        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var roleResult = RoleResolver.Resolve(roles, command.RoleCode);
        if (roleResult.IsFailure)
        {
            return roleResult.Error;
        }

        var role = roleResult.Value;

        // ---------- Kim kimga rol bera oladi ----------
        var allowed = UserRoleRules.EnsureCanAssign(acting.Value, role, roles);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        var taken = await _users.LoginExistsAsync(loginResult.Value, exceptUserId: null, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return UserErrors.LoginTaken;
        }

        // ---------- «Atigi bitta» ----------
        // Ro'yxat faqat shu qoida kerak bo'lganda so'raladi: HR menejer
        // yaratishda (eng ko'p uchraydigan holat) qo'shimcha so'rov ketmaydi.
        if (SystemRoles.SingleHolder.Contains(role.Code))
        {
            var allUsers = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var single = UserRoleRules.EnsureSingleHolder(role, allUsers, exceptUserId: null);
            if (single.IsFailure)
            {
                return single.Error;
            }
        }

        var userResult = User.Create(loginResult.Value, _hasher.Hash(command.Password!), nameResult.Value);
        if (userResult.IsFailure)
        {
            return userResult.Error;
        }

        var user = userResult.Value;
        user.SetRole(role.Id);

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
                role = role.Code,
            }));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UserSummaryDtoFactory.Create(user, roles);
    }
}
