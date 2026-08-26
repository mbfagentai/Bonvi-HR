using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Commands.DeactivateUser;

/// <summary>Foydalanuvchi hisobini faolsizlantirish so'rovi.</summary>
/// <param name="Id">Faolsizlantiriladigan hisob.</param>
public sealed record DeactivateUserCommand(Guid Id);

/// <summary>
/// Hisobni faolsizlantiradi — kirish darhol yopiladi
/// (<c>LoginCommandHandler</c> <c>IsActive</c> ni tekshiradi).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ikki xavfsizlik himoyasi</b> —
/// <see cref="Bonvi.Application.Identity.Users.Commands.UpdateUser.UpdateUserCommandHandler"/>
/// dagi bilan bir xil mantiq:
/// </para>
/// <list type="number">
///   <item>
///     <b>O'zini o'zi bloklash.</b> <see cref="User.Deactivate(Guid)"/> — sof
///     domen qoidasi, faqat identifikatorlar taqqoslanadi.
///   </item>
///   <item>
///     <b>Oxirgi administrator.</b> Boshqa foydalanuvchilarni bilish kerak
///     bo'lgani uchun bu yerda, repozitoriy orqali tekshiriladi.
///   </item>
/// </list>
/// </remarks>
public sealed class DeactivateUserCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateUserCommandHandler(
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

    public async Task<Result> HandleAsync(
        DeactivateUserCommand command,
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

        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var adminRoleId = roles.FirstOrDefault(role => role.Code == SystemRoles.Admin)?.Id ?? Guid.Empty;

        // ---------- Oxirgi administratorni himoya qilish ----------
        if (target.IsActive && target.RoleIds.Contains(adminRoleId))
        {
            var allUsers = await _users.ListAsync(cancellationToken).ConfigureAwait(false);
            var anotherActiveAdmin = allUsers.Any(user =>
                user.Id != target.Id && user.IsActive && user.RoleIds.Contains(adminRoleId));

            if (!anotherActiveAdmin)
            {
                return UserErrors.LastAdminProtected;
            }
        }

        // ---------- O'zini o'zi bloklashdan himoya (domen) ----------
        var deactivated = target.Deactivate(acting.Value.Id);
        if (deactivated.IsFailure)
        {
            return deactivated.Error;
        }

        _auditLogger.Record(AuditAction.Update, "User", target.Id.ToString(), newValues: "{\"isActive\":false}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
