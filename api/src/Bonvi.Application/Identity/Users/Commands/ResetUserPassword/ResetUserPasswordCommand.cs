using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Mcp;
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
/// <b>Nega ruxsatning O'ZI yetarli emas.</b> Ilgari <c>users.manage</c>
/// faqat administratorda bor edi va boshqa tekshiruv kerak emasdi. Uch
/// rolli modelda bu ruxsat bosh rahbarda ham bor — parolni tiklash esa
/// hisobni EGALLAB OLISH bilan barobar (yangi parolni kiritgan odam biladi,
/// eski sessiyalar esa bekor qilinadi). Ya'ni tekshiruvsiz bosh rahbar
/// super administratorning parolini almashtirib, uning o'rniga kirib
/// olardi. Shu sababli bu yerda ham <see cref="UserRoleRules.EnsureCanActOn"/>
/// chaqiriladi — «kim kimga tega oladi» qoidasining o'sha yagona nusxasi.
/// </para>
/// </remarks>
public sealed class ResetUserPasswordCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMcpGrantRevoker _mcpGrants;
    private readonly IClock _clock;

    public ResetUserPasswordCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IMcpGrantRevoker mcpGrants,
        IClock clock)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _mcpGrants = mcpGrants;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        ResetUserPasswordCommand command,
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

        // ---------- Kim kimga tega oladi ----------
        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var allowed = UserRoleRules.EnsureCanActOn(acting.Value, target, roles);
        if (allowed.IsFailure)
        {
            return allowed.Error;
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

        // AI (MCP) ulanishlari ham — eski parol bilan ochilgan ulanish parol
        // almashgandan keyin ham ishlab turmasin (`IMcpGrantRevoker` izohi).
        await _mcpGrants
            .RevokeAllForUserAsync(target.Id, "system:password-reset", cancellationToken)
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
