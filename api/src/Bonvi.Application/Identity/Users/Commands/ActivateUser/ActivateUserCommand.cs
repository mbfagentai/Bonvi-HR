using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Commands.ActivateUser;

/// <summary>Foydalanuvchi hisobini qayta faollashtirish so'rovi.</summary>
/// <param name="Id">Faollashtiriladigan hisob.</param>
public sealed record ActivateUserCommand(Guid Id);

/// <summary>
/// Faolsizlantirilgan hisobni qaytaradi.
/// </summary>
/// <remarks>
/// <para>
/// Bloklash hisoblagichi ham tozalanadi (<see cref="User.Activate"/>) —
/// xato urinishlar tarixi faollashtirishda ahamiyatini yo'qotadi.
/// </para>
/// <para>
/// <b>Nega bu yerda ham ikkita tekshiruv bor (ilgari bittasi ham yo'q edi).</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>Kim kimga tega oladi.</b> Bosh rahbar faolsizlantirilgan super
///     administratorni qaytara olsa, o'zidan kuchliroq hisobni tiklab
///     qo'ygan bo'lardi — ya'ni «faqat HR menejerlarga tegadi» qoidasini
///     shu yo'l bilan chetlab o'tish mumkin bo'lardi.
///   </item>
///   <item>
///     <b>«Atigi bitta».</b> Faollashtirish — hisoblar sonini OSHIRADIGAN
///     yagona amal. Eski bosh rahbar faolsizlantirilib, o'rniga yangisi
///     qo'yilgan bo'lsa, eskisini qaytarish tizimda IKKITA faol bosh
///     rahbar hosil qilardi. Shu sababli qoida yaratish va tahrirlashdagi
///     kabi bu yerda ham tekshiriladi.
///   </item>
/// </list>
/// <para>
/// Allaqachon faol hisobni faollashtirish esa hech narsani o'zgartirmaydi
/// va tekshiruvlardan ham o'tadi: «atigi bitta» hisobning O'ZINI
/// (<c>exceptUserId</c>) sanamaydi — amal IDEMPOTENT qoladi.
/// </para>
/// </remarks>
public sealed class ActivateUserCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateUserCommandHandler(
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
        ActivateUserCommand command,
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

        // ---------- «Atigi bitta» ----------
        // Ro'yxat faqat tekshiruv haqiqatan kerak bo'lganda so'raladi: HR
        // menejerni faollashtirishda (eng ko'p uchraydigan holat) ortiqcha
        // so'rov ketmaydi.
        var targetRole = target.RoleId is { } roleId
            ? roles.FirstOrDefault(role => role.Id == roleId)
            : null;

        if (targetRole is not null && SystemRoles.SingleHolder.Contains(targetRole.Code))
        {
            var allUsers = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var single = UserRoleRules.EnsureSingleHolder(targetRole, allUsers, exceptUserId: target.Id);
            if (single.IsFailure)
            {
                return single.Error;
            }
        }

        target.Activate();

        _auditLogger.Record(AuditAction.Update, "User", target.Id.ToString(), newValues: "{\"isActive\":true}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
