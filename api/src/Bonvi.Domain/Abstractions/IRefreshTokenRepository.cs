using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Abstractions;

/// <summary>Yangilash tokenlari uchun saqlash porti.</summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>Xesh bo'yicha topadi (ochiq token hech qachon saqlanmaydi).</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Foydalanuvchining barcha faol tokenlarini bekor qiladi.
    /// </summary>
    /// <remarks>
    /// Ikki holatda chaqiriladi: chiqishda va <b>token qayta ishlatilganda</b>.
    /// Ikkinchisi o'g'irlik belgisi — barcha sessiyalar yopiladi.
    /// </remarks>
    Task<int> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset at,
        string? byIp,
        CancellationToken cancellationToken = default);

    /// <summary>Muddati tugagan tokenlarni tozalaydi (fon vazifasi uchun).</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}
