using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Commands.ActivateUser;

/// <summary>Foydalanuvchi hisobini qayta faollashtirish so'rovi.</summary>
/// <param name="Id">Faollashtiriladigan hisob.</param>
public sealed record ActivateUserCommand(Guid Id);

/// <summary>
/// Faolsizlantirilgan hisobni qaytaradi.
/// </summary>
/// <remarks>
/// Bu yo'nalishda xavfsizlik cheklovi kerak emas: faollashtirish
/// birortasini bloklamaydi, faqat kirish qaytadan ochiladi. Bloklash
/// hisoblagichi ham tozalanadi (<see cref="Domain.Identity.User.Activate"/>) —
/// xato urinishlar tarixi faollashtirishda ahamiyatini yo'qotadi.
/// </remarks>
public sealed class ActivateUserCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateUserCommandHandler(IUserRepository users, IAuditLogger auditLogger, IUnitOfWork unitOfWork)
    {
        _users = users;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ActivateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var target = await _users.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UserErrors.NotFound;
        }

        target.Activate();

        _auditLogger.Record(AuditAction.Update, "User", target.Id.ToString(), newValues: "{\"isActive\":true}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
