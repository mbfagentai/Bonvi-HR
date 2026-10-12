using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Mcp;
using Bonvi.Domain.Abstractions;
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
/// <b>Uch himoya, uch boshqa sabab</b> —
/// <see cref="Bonvi.Application.Identity.Users.Commands.UpdateUser.UpdateUserCommandHandler"/>
/// dagi bilan bir xil mantiq:
/// </para>
/// <list type="number">
///   <item>
///     <b>Kim kimga tega oladi</b> (<see cref="UserRoleRules.EnsureCanActOn"/>).
///     Bosh rahbar super administratorni ham, o'zini ham bu ekrandan
///     faolsizlantira olmaydi.
///   </item>
///   <item>
///     <b>Oxirgi rahbar.</b> Boshqa foydalanuvchilarni bilish kerak
///     bo'lgani uchun bu yerda, repozitoriy orqali tekshiriladi.
///   </item>
///   <item>
///     <b>O'zini o'zi bloklash.</b> <see cref="User.Deactivate(Guid)"/> — sof
///     domen qoidasi, faqat identifikatorlar taqqoslanadi.
///   </item>
/// </list>
/// <para>
/// <b>Nega faolsizlantirish «atigi bitta» ni tekshirmaydi.</b> U hisoblar
/// sonini faqat KAMAYTIRADI: bo'shatilgan lavozimga yangisini qo'yish esa
/// aynan shu yo'l bilan mumkin bo'ladi (<see cref="UserRoleRules.EnsureSingleHolder"/>
/// izohiga qarang — u faqat FAOL egalarni sanaydi).
/// </para>
/// </remarks>
public sealed class DeactivateUserCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMcpGrantRevoker _mcpGrants;

    public DeactivateUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IUnitOfWork unitOfWork,
        IMcpGrantRevoker mcpGrants)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _mcpGrants = mcpGrants;
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

        // ---------- Kim kimga tega oladi ----------
        var allowed = UserRoleRules.EnsureCanActOn(acting.Value, target, roles);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        // ---------- Oxirgi rahbarni himoya qilish ----------
        // Faqat FAOL hisob uchun: allaqachon faolsiz hisobni qayta
        // faolsizlantirish rahbariyat sonini kamaytirmaydi.
        if (target.IsActive)
        {
            var allUsers = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var remains = UserRoleRules.EnsureManagerRemains(target, allUsers, roles);
            if (remains.IsFailure)
            {
                return remains.Error;
            }
        }

        // ---------- O'zini o'zi bloklashdan himoya (domen) ----------
        var deactivated = target.Deactivate(acting.Value.Id);
        if (deactivated.IsFailure)
        {
            return deactivated.Error;
        }

        // AI (MCP) ulanishlari ham — aks holda hisob qayta faollashtirilganda
        // eski ulanish yangi roziliksiz «tirilib» qolardi (`IMcpGrantRevoker` izohi).
        await _mcpGrants
            .RevokeAllForUserAsync(target.Id, "system:deactivated", cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
