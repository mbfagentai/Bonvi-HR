using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Mcp;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Commands.Logout;

/// <summary>Tizimdan chiqish so'rovi.</summary>
/// <param name="RefreshToken">Bekor qilinadigan token.</param>
/// <param name="AllSessions">Barcha qurilmalardagi sessiyalarni yopishmi.</param>
/// <param name="IpAddress">So'rov kelgan IP.</param>
public sealed record LogoutCommand(string? RefreshToken, bool AllSessions, string? IpAddress);

/// <summary>
/// Sessiyani yopadi.
/// </summary>
/// <remarks>
/// Access token bekor qilinmaydi — u 15 daqiqada o'zi tugaydi. JWT'ning
/// tabiati shunday: uni serverda «o'chirib» bo'lmaydi (agar qora ro'yxat
/// yuritilmasa, bu esa JWT'ning barcha afzalligini yo'qqa chiqaradi).
/// Shu sababli access token muddati ataylab qisqa.
/// </remarks>
public sealed class LogoutCommandHandler
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMcpGrantRevoker _mcpGrants;
    private readonly IAuditLogger _auditLogger;
    private readonly IClock _clock;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokens,
        ITokenService tokens,
        IUnitOfWork unitOfWork,
        IMcpGrantRevoker mcpGrants,
        IAuditLogger auditLogger,
        IClock clock)
    {
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _mcpGrants = mcpGrants;
        _auditLogger = auditLogger;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            // Token bo'lmasa ham muvaffaqiyat qaytariladi: chiqish amali
            // hech qachon xato bermasligi kerak — foydalanuvchi baribir chiqdi
            return Result.Success();
        }

        var now = _clock.UtcNow;
        var hash = _tokens.HashRefreshToken(command.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken).ConfigureAwait(false);

        if (stored is null)
        {
            return Result.Success();
        }

        if (command.AllSessions)
        {
            await _refreshTokens
                .RevokeAllForUserAsync(stored.UserId, now, command.IpAddress, cancellationToken)
                .ConfigureAwait(false);

            // «Barcha sessiyalardan chiqish» — AI ulanishlari ham (`IMcpGrantRevoker` izohi).
            await _mcpGrants
                .RevokeAllForUserAsync(stored.UserId, "system:logout-all", cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            stored.Revoke(now, command.IpAddress);
        }

        // Chiqish so'rovida access token yo'q bo'lishi mumkin — bajaruvchi
        // tokenning egasi (SPEC-rollar-va-audit §7.6). Token topilmagan chiqish
        // (allaqachon chiqqan, eskirgan) yozilmaydi: kim ekani noma'lum.
        _auditLogger.RecordSecurityEvent(
            AuditAction.Logout,
            stored.UserId,
            login: null,
            command.AllSessions ? """{"allSessions":true}""" : """{"allSessions":false}""");

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
