using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bonvi.Infrastructure.Identity;

/// <summary>
/// JWT access token va tasodifiy refresh token yaratadi.
/// </summary>
/// <remarks>
/// <para><b>Access token</b> — imzolangan JWT. Ichida foydalanuvchi id'si,
/// login, rollar va <b>ruxsatlar</b> bor.</para>
/// <para>
/// Nega ruxsatlar token ichida: aks holda har bir so'rovda bazadan
/// ruxsatlarni o'qish kerak bo'lardi. 540 xodim va o'nlab foydalanuvchida
/// bu sezilmaydi, lekin bu — keraksiz so'rov. Token ichida bo'lgani uchun
/// tekshiruv xotirada bajariladi.
/// </para>
/// <para>
/// Narxi: administrator ruxsatni o'zgartirsa, u eng ko'pi bilan 15 daqiqadan
/// keyin (access token muddati) kuchga kiradi. Bu maqbul kelishuv.
/// Darhol kerak bo'lsa — <c>/api/auth/me</c> bazadan o'qiydi.
/// </para>
/// </remarks>
public sealed class JwtTokenService : ITokenService
{
    /// <summary>Ruxsatlar tokenda shu nom bilan yoziladi.</summary>
    public const string PermissionClaim = "perm";

    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _clock = clock;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public TokenPair CreateTokens(
        User user,
        IReadOnlyList<string> roleCodes,
        IReadOnlySet<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleCodes);
        ArgumentNullException.ThrowIfNull(permissions);

        var now = _clock.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>(roleCodes.Count + permissions.Count + 5)
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Login.Value),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        if (user.EmployeeId is { } employeeId)
        {
            claims.Add(new Claim("emp", employeeId.ToString()));
        }

        foreach (var role in roleCodes)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(PermissionClaim, permission));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            SigningCredentials = _credentials,
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);

        var refreshToken = GenerateRefreshToken();

        return new TokenPair(
            accessToken,
            accessExpires,
            refreshToken,
            HashRefreshToken(refreshToken),
            now.AddDays(_options.RefreshTokenDays));
    }

    /// <summary>
    /// Refresh tokenning SHA-256 xeshi (hex).
    /// </summary>
    /// <remarks>
    /// Bazada faqat xesh saqlanadi. Baza sizib chiqsa ham tokenlardan
    /// foydalanib bo'lmaydi — xuddi parol kabi.
    /// Bu yerda tuz (salt) kerak emas: token allaqachon 256 bitli tasodifiy
    /// qiymat, uni lug'at bo'yicha topib bo'lmaydi.
    /// </remarks>
    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 256 bitli kriptografik tasodifiy token.
    /// </summary>
    /// <remarks>
    /// <c>Random</c> emas, <c>RandomNumberGenerator</c> ishlatiladi:
    /// oddiy tasodifiy sonlar generatori oldindan aytib bo'ladigan qiymat
    /// beradi va tokenni taxmin qilish mumkin bo'lib qoladi.
    /// </remarks>
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
