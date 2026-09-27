using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>Xodim o'z parolini almashtiradi.</summary>
/// <param name="CurrentPassword">Joriy parol (HR bergan bir martalik yoki eskisi).</param>
/// <param name="NewPassword">Yangi parol.</param>
/// <param name="IpAddress">So'rov kelgan manzil.</param>
public sealed record ChangeEmployeePasswordCommand(
    string? CurrentPassword,
    string? NewPassword,
    string? IpAddress);

/// <summary>
/// <see cref="ChangeEmployeePasswordCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Joriy parol SO'RALADI</b>, garchi so'rov allaqachon tasdiqlangan
/// tokendan kelayotgan bo'lsa ham. Sabab: telefon qo'ldan ketishi mumkin
/// va ochiq qolgan ilova parolni almashtirishning eng oson yo'li bo'lib
/// qolardi — o'shanda haqiqiy egasi o'z hisobidan chiqib qolardi.
/// </para>
/// <para>
/// ⭐ <b>Almashgandan keyin BARCHA sessiyalar yopiladi</b>, shu jumladan
/// so'rovni yuborgani ham. Parol ikki holatda almashtiriladi: unutilgan yoki
/// birovga ma'lum bo'lib qolgan. Ikkalasida ham eski yangilash tokenlari
/// yetti kun amal qilishda davom etsa, «parolni almashtirdim» degan tasalli
/// yolg'on bo'lardi. Ilova javobdagi yangi juftlik bilan davom etadi.
/// </para>
/// <para>
/// ⚠️ Bu — <c>must_change_password</c> bayrog'i qo'yilganda XODIM CHAQIRA
/// OLADIGAN YAGONA endpoint: <c>/api/me/*</c> guruhidagi
/// <see cref="EmployeePasswordGate"/> filtri qolganlariga
/// <c>xodim.parol_almashtirilsin</c> (403) beradi. Shu sababli u bayroqni
/// tekshirmaydi: tekshirsa, birinchi kirishdagi majburiy almashtirish umuman
/// ishlamas edi.
/// </para>
/// <para>
/// ⭐ <b>Noto'g'ri joriy parol SANALADI</b>
/// (<see cref="EmployeeAccount.RecordFailedLogin"/>): 5 xatodan keyin
/// almashtirish 15 daqiqaga bloklanadi (<c>xodim.bloklangan</c>). Aks
/// holda ochiq qolgan telefondagi sessiya joriy parolni cheksiz terib
/// topish kanali bo'lardi — topilgan parol bilan esa egasining boshqa
/// barcha sessiyalari yopiladi. Yo'lning o'zi ham qat'iy tezlik cheklovi
/// ostida (<c>RateLimitingOptions.EmployeePasswordPaths</c>).
/// </para>
/// </remarks>
public sealed class ChangeEmployeePasswordCommandHandler
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmployeeTokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ChangeEmployeePasswordCommandHandler(
        ICurrentEmployee currentEmployee,
        IEmployeeAccountRepository accounts,
        IEmployeeRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IEmployeeTokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<EmployeeAuthResultDto>> HandleAsync(
        ChangeEmployeePasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_currentEmployee.AccountId is not { } accountId)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.IsActive)
        {
            return EmployeeAccountErrors.AccountInactive;
        }

        var now = _clock.UtcNow;

        // Blok parol tekshiruvidan OLDIN: aks holda blok davomida ham to'g'ri
        // parolni topish mumkin bo'lardi (javob farqi — oracle).
        if (account.IsLockedAt(now))
        {
            var minutes = (int)Math.Ceiling((account.LockedUntil!.Value - now).TotalMinutes);
            return EmployeeAccountErrors.LockedOut(minutes);
        }

        // Yangi parolning shakli AVVAL tekshiriladi — xeshlash qimmat amal
        // va uni 3 belgilik parol uchun bajarishning ma'nosi yo'q.
        var policy = PasswordPolicy.Validate(command.NewPassword);
        if (policy.IsFailure)
        {
            return policy.Error;
        }

        var (succeeded, _) = _passwordHasher.Verify(account.PasswordHash, command.CurrentPassword ?? string.Empty);
        if (!succeeded)
        {
            // Sanaladi — sinf izohi. Beshinchisida almashtirish bloklanadi.
            account.RecordFailedLogin(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return EmployeeAccountErrors.WrongPassword;
        }

        account.ChangePassword(_passwordHasher.Hash(command.NewPassword!), now);

        await _refreshTokens
            .RevokeAllForAccountAsync(account.Id, now, command.IpAddress, cancellationToken)
            .ConfigureAwait(false);

        var pair = _tokens.CreateTokens(account);

        _refreshTokens.Add(Domain.Identity.EmployeeRefreshToken.Issue(
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
            MustChangePassword: false);
    }
}
