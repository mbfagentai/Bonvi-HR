using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Commands.ResetUserPassword;

/// <summary>Administrator tomonidan parolni tiklash so'rovi.</summary>
/// <param name="Id">Kimning paroli tiklanmoqda.</param>
/// <param name="NewPassword">
/// Yangi parol — ADMINISTRATOR tanlaydi (bu yerda ham, yaratishdagi kabi:
/// tizimda parolni yetkazadigan kanal yo'q, shu sababli uni og'zaki aytadi).
/// </param>
public sealed record ResetUserPasswordCommand(Guid Id, string? NewPassword);

/// <summary>
/// Parolni almashtiradi va foydalanuvchining BARCHA mavjud sessiyalarini
/// bekor qiladi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega sessiyalar bekor qilinadi.</b> Parol odatda ikki holatda
/// tiklanadi: foydalanuvchi uni unutgan, yoki u sizib chiqqan bo'lishi
/// mumkin degan gumon bor. Ikkala holatda ham eski refresh token(lar)
/// hali 7 kun amal qiladi — agar ular bekor qilinmasa, "parolni tikladim"
/// degan tasalli yolg'on bo'lib chiqadi: eski qurilmada/hujumchida hali
/// ham kirish qolaveradi. Mexanizm allaqachon bor
/// (<see cref="IRefreshTokenRepository.RevokeAllForUserAsync"/>) — xuddi
/// «barcha qurilmalardan chiqish»da ishlatilgani.
/// </para>
/// <para>
/// <b>Nega ruxsat qo'shimcha tekshiruvsiz.</b> Bu amal
/// <c>users.manage</c> ruxsati bilan himoyalangan (faqat admin), boshqa
/// hech kim bu endpoint'ga umuman yeta olmaydi — HR menejeri boshqa HR
/// menejerining parolini tiklay olmaydi, chunki uning bu ruxsati yo'q.
/// </para>
/// </remarks>
public sealed class ResetUserPasswordCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ResetUserPasswordCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var target = await _users.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UserErrors.NotFound;
        }

        var passwordCheck = PasswordPolicy.Validate(command.NewPassword);
        if (passwordCheck.IsFailure)
        {
            return passwordCheck.Error;
        }

        target.ChangePassword(_hasher.Hash(command.NewPassword!));

        await _refreshTokens
            .RevokeAllForUserAsync(target.Id, _clock.UtcNow, byIp: null, cancellationToken)
            .ConfigureAwait(false);

        // Parol yoki uning xeshi audit yozuviga HECH QACHON tushmaydi — faqat fakt qayd etiladi
        _auditLogger.Record(
            AuditAction.Update,
            "User",
            target.Id.ToString(),
            newValues: "{\"action\":\"password_reset\"}");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
