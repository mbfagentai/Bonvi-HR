using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>Xodim sessiyasini yangilash so'rovi.</summary>
/// <param name="RefreshToken">Kirishda berilgan yangilash tokeni.</param>
/// <param name="IpAddress">So'rov kelgan manzil.</param>
public sealed record EmployeeRefreshCommand(string? RefreshToken, string? IpAddress);

/// <summary>
/// Yangilash tokenini almashtiradi (rotation).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qayta ishlatilgan token — o'g'irlik belgisi.</b> Bekor qilingan
/// token bilan kelgan so'rovda hisobning BARCHA tirik tokenlari yopiladi.
/// Sabab: token faqat bir marta ishlatiladi, ya'ni ikkinchi marta kelishi
/// nusxa borligini bildiradi — va qaysi nusxasi o'g'rining qo'lida ekanini
/// aniqlashning iloji yo'q. Ikkalasini ham yopish yagona xavfsiz javob.
/// <c>RefreshSessionCommandHandler</c> dagi bilan bir xil qoida.
/// </para>
/// <para>
/// ⚠️ Yangilash HISOB va XODIM holatini QAYTA tekshiradi: ishdan ketgan yoki
/// hisobi yopilgan odamda access token muddati tugashi bilan sessiya
/// o'ladi. Usiz «ishdan bo'shatildi» degan qaror yetti kun davomida
/// kuchga kirmasdi.
/// </para>
/// </remarks>
public sealed class EmployeeRefreshCommandHandler
{
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly IEmployeeTokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IEmployeeAuthJournal _journal;

    public EmployeeRefreshCommandHandler(
        IEmployeeRefreshTokenRepository refreshTokens,
        IEmployeeAccountRepository accounts,
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IEmployeeTokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock,
        IEmployeeAuthJournal journal)
    {
        _refreshTokens = refreshTokens;
        _accounts = accounts;
        _employees = employees;
        _employments = employments;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _journal = journal;
    }

    public async Task<Result<EmployeeAuthResultDto>> HandleAsync(
        EmployeeRefreshCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var now = _clock.UtcNow;
        var hash = _tokens.HashRefreshToken(command.RefreshToken);

        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        if (!stored.IsActiveAt(now))
        {
            // Muddati tugagani — oddiy holat. Bekor qilingani esa nusxa
            // borligini bildiradi: o'sha hisobning hamma sessiyasi yopiladi.
            if (stored.RevokedAt is not null)
            {
                _journal.RefreshTokenReused(stored.EmployeeAccountId);
                await _refreshTokens
                    .RevokeAllForAccountAsync(stored.EmployeeAccountId, now, command.IpAddress, cancellationToken)
                    .ConfigureAwait(false);

                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var account = await _accounts.GetByIdAsync(stored.EmployeeAccountId, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.IsActive)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        // INV-1 va INV-2 yangilashda ham amal qiladi — sabab sinf izohida.
        var employee = await _employees.GetByIdAsync(account.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (employee is null
            || !await _employments.IsEmployedOnAsync(employee.Id, now.TashkentDate(), cancellationToken).ConfigureAwait(false))
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var pair = _tokens.CreateTokens(account);

        stored.Revoke(now, command.IpAddress, pair.RefreshTokenHash);

        _refreshTokens.Add(EmployeeRefreshToken.Issue(
            account.Id,
            pair.RefreshTokenHash,
            pair.RefreshTokenExpiresAt,
            now,
            command.IpAddress));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EmployeeAuthResultDto(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            account.MustChangePassword);
    }
}
