using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>Xodim ilovasidan chiqish — shu qurilmaning sessiyasini yopadi.</summary>
/// <param name="RefreshToken">Chiqayotgan qurilmaning yangilash tokeni.</param>
/// <param name="IpAddress">So'rov kelgan manzil.</param>
public sealed record EmployeeLogoutCommand(string? RefreshToken, string? IpAddress);

/// <summary>
/// <see cref="EmployeeLogoutCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega kerak.</b> Ilgari «Chiqish» faqat telefonning xotirasini
/// tozalardi — serverdagi sessiya esa yetti kun tirik qolardi. Token nusxasi
/// qolgan bo'lsa (zaxira nusxa, boshqa tab, o'g'irlangan telefon), u
/// «chiqib ketgan» odam nomidan belgilashda davom etardi.
/// </para>
/// <para>
/// FAQAT shu token yopiladi, hisobning boshqa sessiyalari emas: odam bitta
/// telefondan chiqdi, ikkinchisidagi sessiyasi — uning o'z qarori. Hammasini
/// yopish yo'li — parolni almashtirish.
/// </para>
/// <para>
/// ⚠️ <b>Javob har doim bir xil</b> (204): noma'lum, muddati o'tgan yoki
/// allaqachon yopilgan token ham «muvaffaqiyat». Farqli javob tokenning
/// holatini tekshiradigan oracle bo'lardi, mijozga esa baribir bitta natija
/// kerak — u tizimdan chiqdi.
/// </para>
/// <para>
/// Endpoint anonim (<c>AllowAnonymous</c>): access token muddati tugagan
/// bo'lishi mumkin va shunda ham chiqish ishlashi kerak. Ishonchnomaning
/// o'zi — tanadagi yangilash tokeni.
/// </para>
/// </remarks>
public sealed class EmployeeLogoutCommandHandler
{
    private readonly IEmployeeRefreshTokenRepository _refreshTokens;
    private readonly IEmployeeTokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public EmployeeLogoutCommandHandler(
        IEmployeeRefreshTokenRepository refreshTokens,
        IEmployeeTokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _refreshTokens = refreshTokens;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        EmployeeLogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        var stored = await _refreshTokens
            .GetByHashAsync(_tokens.HashRefreshToken(command.RefreshToken), cancellationToken)
            .ConfigureAwait(false);

        if (stored is null || !stored.IsActiveAt(now))
        {
            return Result.Success();
        }

        stored.Revoke(now, command.IpAddress);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
