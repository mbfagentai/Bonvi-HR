using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Abstractions;

/// <summary>Yaratilgan tokenlar juftligi.</summary>
/// <param name="AccessToken">JWT. Qisqa muddatli (15 daqiqa).</param>
/// <param name="AccessTokenExpiresAt">Access token qachon tugaydi.</param>
/// <param name="RefreshToken">Tasodifiy satr. Uzoq muddatli (7 kun).</param>
/// <param name="RefreshTokenHash">Bazaga saqlanadigan SHA-256 xesh.</param>
/// <param name="RefreshTokenExpiresAt">Refresh token qachon tugaydi.</param>
public sealed record TokenPair(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    string RefreshTokenHash,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Token yaratish porti.</summary>
public interface ITokenService
{
    /// <summary>
    /// Foydalanuvchi uchun token juftligini yaratadi.
    /// Ruxsatlar access token ichiga yoziladi — har bir so'rovda bazaga
    /// murojaat qilmaslik uchun (yuklama chidamliligi).
    /// </summary>
    TokenPair CreateTokens(User user, IReadOnlyList<string> roleCodes, IReadOnlySet<string> permissions);

    /// <summary>Ochiq refresh tokendan uning xeshini hisoblaydi.</summary>
    string HashRefreshToken(string refreshToken);
}
