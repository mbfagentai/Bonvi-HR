using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Identity.Users.Commands.UpdateUser;

/// <summary>Foydalanuvchi ma'lumotini tahrirlash so'rovi.</summary>
/// <param name="Id">Tahrirlanadigan foydalanuvchi.</param>
/// <param name="Login">Yangi telefon raqami.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="RoleCodes">Yangi to'liq rol ro'yxati — eskisi ALMASHTIRILADI, qo'shilmaydi.</param>
public sealed record UpdateUserCommand(
    Guid Id,
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    IReadOnlyList<string>? RoleCodes);

/// <summary>
/// Ism, telefon va rollarni yangilaydi. Parol BU YERDA O'ZGARTIRILMAYDI —
/// buning uchun alohida «parolni tiklash» amali bor.
/// </summary>
/// <remarks>
/// <para><b>Ikki xavfsizlik himoyasi:</b></para>
/// <list type="number">
///   <item>
///     <b>O'zini o'zi adminlikdan chetlashtirish.</b> Rol ro'yxatidan admin
///     rolini olib tashlash <see cref="User.RemoveRole(Guid, Guid, Guid)"/>
///     orqali amalga oshiriladi — bu domen metodi shu hisobning aynan o'zi
///     ekanini tekshiradi va sof identifikator taqqoslashi bilan ishlaydi.
///   </item>
///   <item>
///     <b>Oxirgi administrator.</b> Admin roli olib tashlanayotgan bo'lsa,
///     tizimda BOSHQA faol admin qolishi tekshiriladi. Bu tekshiruv domen
///     darajasida bo'lishi MUMKIN EMAS: <see cref="User"/> agregati boshqa
///     foydalanuvchilar haqida hech narsa bilmaydi (agregatlar bir-biridan
///     mustaqil) — shu sababli bu yerda, repozitoriy orqali bajariladi.
///   </item>
/// </list>
/// </remarks>
public sealed class UpdateUserCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserSummaryDto>> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var target = await _users.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UserErrors.NotFound;
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

        var loginTaken = await _users
            .LoginExistsAsync(loginResult.Value, exceptUserId: target.Id, cancellationToken)
            .ConfigureAwait(false);

        if (loginTaken)
        {
            return UserErrors.LoginTaken;
        }

        var rolesResult = await RoleResolver.ResolveAsync(_roles, command.RoleCodes, cancellationToken)
            .ConfigureAwait(false);

        if (rolesResult.IsFailure)
        {
            return rolesResult.Error;
        }

        var newRoles = rolesResult.Value;
        var allRoles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var roleById = allRoles.ToDictionary(role => role.Id);
        var adminRoleId = allRoles.FirstOrDefault(role => role.Code == SystemRoles.Admin)?.Id ?? Guid.Empty;

        var oldRoleIds = target.RoleIds.ToHashSet();
        var newRoleIds = newRoles.Select(role => role.Id).ToHashSet();
        var removingIds = oldRoleIds.Except(newRoleIds).ToList();
        var addingIds = newRoleIds.Except(oldRoleIds).ToList();

        // ---------- Oxirgi administratorni himoya qilish ----------
        if (removingIds.Contains(adminRoleId))
        {
            var allUsers = await _users.ListAsync(cancellationToken).ConfigureAwait(false);
            var anotherActiveAdmin = allUsers.Any(user =>
                user.Id != target.Id && user.IsActive && user.RoleIds.Contains(adminRoleId));

            if (!anotherActiveAdmin)
            {
                return UserErrors.LastAdminProtected;
            }
        }

        var oldSnapshot = SnapshotJson(target, oldRoleIds, roleById);

        // ---------- O'zini o'zi adminlikdan chetlashtirishdan himoya (domen) ----------
        // Olib tashlash QO'SHISHDAN OLDIN: agar bu yerda rad etilsa, hisobga
        // hech qanday o'zgarish tegmagan bo'lishi kerak (keyingi qo'shish
        // qadamlari hali bajarilmagan).
        foreach (var id in removingIds)
        {
            var removed = target.RemoveRole(id, acting.Value.Id, adminRoleId);
            if (removed.IsFailure)
            {
                return removed.Error;
            }
        }

        target.Rename(nameResult.Value);
        target.ChangeLogin(loginResult.Value);

        foreach (var id in addingIds)
        {
            target.AssignRole(id);
        }

        _auditLogger.Record(
            AuditAction.Update,
            "User",
            target.Id.ToString(),
            oldValues: oldSnapshot,
            newValues: SnapshotJson(target, newRoleIds, roleById));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UserSummaryDtoFactory.Create(target, newRoles);
    }

    private static string SnapshotJson(
        User user,
        IReadOnlySet<Guid> roleIds,
        Dictionary<Guid, Role> roleById)
    {
        var roleCodes = roleIds
            .Select(id => roleById.TryGetValue(id, out var role) ? role.Code : id.ToString())
            .OrderBy(code => code, StringComparer.Ordinal);

        return JsonSerializer.Serialize(new
        {
            login = user.Login.Value,
            fullName = user.FullName.Full,
            roles = roleCodes,
        });
    }
}
