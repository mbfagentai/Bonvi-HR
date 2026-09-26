using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Commands.RefreshSession;

/// <summary>Sessiyani yangilash so'rovi.</summary>
/// <param name="RefreshToken">Kirishda berilgan yangilash tokeni.</param>
/// <param name="IpAddress">So'rov kelgan IP.</param>
public sealed record RefreshSessionCommand(string RefreshToken, string? IpAddress);

/// <summary>
/// Access token muddati tugaganda yangi juftlik beradi.
/// </summary>
/// <remarks>
/// <para><b>Token aylantirish (rotation) va o'g'irlikni aniqlash:</b></para>
/// <para>
/// Har bir yangilashda eski token darhol bekor qilinadi va yangisi beriladi.
/// Agar allaqachon bekor qilingan token qayta ishlatilsa — bu ikki narsani
/// anglatadi: yo token o'g'irlangan, yo nusxasi boshqa qurilmada qolgan.
/// Ikkala holatda ham xavfsiz yo'l bitta: <b>o'sha foydalanuvchining barcha
/// sessiyalari yopiladi</b>. Haqiqiy foydalanuvchi qaytadan kiradi, o'g'ri esa
/// hech narsa qila olmaydi.
/// </para>
/// <para>
/// Bu OWASP tavsiya qiladigan «refresh token rotation with reuse detection»
/// naqshi. Usiz o'g'irlangan token 7 kun davomida amal qilaveradi.
/// </para>
/// </remarks>
public sealed class RefreshSessionCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RefreshSessionCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ITokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AuthResultDto>> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return InvalidToken();
        }

        var now = _clock.UtcNow;
        var hash = _tokens.HashRefreshToken(command.RefreshToken);

        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return InvalidToken();
        }

        // ⚠️ Qayta ishlatish aniqlandi — barcha sessiyalarni yopamiz
        if (stored.RevokedAt is not null)
        {
            await _refreshTokens
                .RevokeAllForUserAsync(stored.UserId, now, command.IpAddress, cancellationToken)
                .ConfigureAwait(false);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Error.Forbidden(
                "auth.token_qayta_ishlatildi",
                "Xavfsizlik sababli barcha sessiyalar yopildi. Iltimos, qaytadan kiring.");
        }

        if (!stored.IsActiveAt(now))
        {
            return InvalidToken();
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return Error.Forbidden("auth.hisob_faol_emas", "Hisob faol emas.");
        }

        var roleCodes = await _users.GetRoleCodesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var roleNames = await _users.GetRoleNamesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var permissions = await _users.GetPermissionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var pair = _tokens.CreateTokens(user, roleCodes, permissions);

        // Eski tokenni yangisiga bog'lab bekor qilamiz — zanjir hosil bo'ladi.
        // Zanjir o'g'irlikni tekshirishda qo'l keladi.
        stored.Revoke(now, command.IpAddress, pair.RefreshTokenHash);

        _refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            pair.RefreshTokenHash,
            pair.RefreshTokenExpiresAt,
            command.IpAddress));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AuthResultDto(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            new CurrentUserDto(
                user.Id,
                user.Login.Value,
                user.FullName.Full,
                user.FullName.ShortForm,
                user.EmployeeId,
                user.PhotoKey,
                roleCodes,
                [.. permissions],
                roleNames));
    }

    private static Error InvalidToken()
        => Error.Forbidden("auth.token_yaroqsiz", "Sessiya muddati tugadi. Iltimos, qaytadan kiring.");
}
