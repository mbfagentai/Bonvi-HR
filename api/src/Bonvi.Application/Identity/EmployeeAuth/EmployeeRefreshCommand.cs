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
/// ⭐ <b>Istisno — javobi yo'qolgan so'rov</b> (<see cref="EmployeeRefreshReplayCache"/>).
/// Hozirgina almashtirilgan token <see cref="EmployeeRefreshReplayCache.Grace"/>
/// ichida, o'rniga berilgani hali ishlatilmagan holda qayta kelsa — bu
/// o'g'irlik emas, yo'qolgan javob: mijoz AYNAN O'SHA juftlikni qayta oladi.
/// Aks holda metro yoki liftdagi har bir uzilish xodimni hamma qurilmada
/// tizimdan chiqarib yuborardi.
/// </para>
/// <para>
/// ⚠️ Yangilash XODIM holatini QAYTA tekshiradi: ishdan ketgan yoki
/// o'chirilgan odamda sessiya o'ladi. Usiz «ishdan bo'shatildi» degan qaror
/// yetti kun davomida kuchga kirmasdi (access token esa undan oldin —
/// <c>/api/me/*</c> to'sig'ida — to'xtaydi).
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
    private readonly EmployeeRefreshReplayCache _replay;

    public EmployeeRefreshCommandHandler(
        IEmployeeRefreshTokenRepository refreshTokens,
        IEmployeeAccountRepository accounts,
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IEmployeeTokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock,
        IEmployeeAuthJournal journal,
        EmployeeRefreshReplayCache replay)
    {
        _refreshTokens = refreshTokens;
        _accounts = accounts;
        _employees = employees;
        _employments = employments;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _journal = journal;
        _replay = replay;
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
            // Hozirgina almashtirilgan token — javobi yo'lda yo'qolgan bo'lishi
            // mumkin. O'rinbosar hali ishlatilmagan bo'lsa, xuddi o'sha juftlik
            // qaytadi (sinf izohi); aks holda quyidagi o'g'irlik qoidasi.
            if (await ReplayAsync(stored, hash, now, cancellationToken).ConfigureAwait(false) is { } replayed)
            {
                return replayed;
            }

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
        if (account is null)
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

        var result = new EmployeeAuthResultDto(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken);

        // Saqlangandan KEYIN: saqlash yiqilsa, eslab qolinadigan javob yo'q.
        _replay.Remember(hash, pair.RefreshTokenHash, result, now);

        return result;
    }

    /// <summary>
    /// Almashtirilgan token uchun eslab qolingan juftlik — faqat oyna ichida va
    /// o'rinbosar hali tirik bo'lsa.
    /// </summary>
    /// <remarks>
    /// O'rinbosar BAZADAN tekshiriladi: u allaqachon ishlatilgan (o'zi ham
    /// almashtirilgan) yoki chiqishda bekor qilingan bo'lsa, eski tokenning
    /// qaytishi endi yo'qolgan javob emas — o'g'irlik qoidasi ishlaydi.
    /// </remarks>
    private async Task<EmployeeAuthResultDto?> ReplayAsync(
        EmployeeRefreshToken stored,
        string hash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (stored is not { RevokedAt: { } revokedAt, ReplacedByTokenHash: { } successorHash }
            || now - revokedAt > EmployeeRefreshReplayCache.Grace)
        {
            return null;
        }

        if (_replay.Replay(hash, successorHash, now) is not { } cached)
        {
            return null;
        }

        var successor = await _refreshTokens.GetByHashAsync(successorHash, cancellationToken).ConfigureAwait(false);

        return successor is not null && successor.IsActiveAt(now) ? cached : null;
    }
}
