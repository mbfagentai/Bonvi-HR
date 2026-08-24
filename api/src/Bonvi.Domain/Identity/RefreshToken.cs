using Bonvi.Domain.Common;

namespace Bonvi.Domain.Identity;

/// <summary>
/// Yangilash tokeni — access token muddati tugaganda yangisini olish uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Xavfsizlik:</b> tokenning o'zi saqlanmaydi, faqat SHA-256 xeshi.
/// Baza sizib chiqsa ham tokenlardan foydalanib bo'lmaydi — xuddi parol kabi.
/// </para>
/// <para>
/// <b>Aylantirish (rotation):</b> har bir yangilashda eski token bekor qilinadi
/// va yangisi beriladi. Bekor qilingan token qayta ishlatilsa — bu o'g'irlik
/// belgisi, o'sha zanjirdagi barcha tokenlar bekor qilinadi.
/// </para>
/// </remarks>
public sealed class RefreshToken : AggregateRoot
{
    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, string? createdByIp)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>EF Core uchun.</summary>
    private RefreshToken() => TokenHash = string.Empty;

    public Guid UserId { get; private set; }

    /// <summary>Tokenning SHA-256 xeshi (hex). Ochiq token saqlanmaydi.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedByIp { get; private set; }

    /// <summary>Bu token o'rniga berilgan yangi tokenning xeshi (aylantirish zanjiri).</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? createdByIp = null)
        => new(NewId(), userId, tokenHash, expiresAt, createdByIp);

    public bool IsActiveAt(DateTimeOffset moment) => RevokedAt is null && ExpiresAt > moment;

    public void Revoke(DateTimeOffset at, string? byIp = null, string? replacedByHash = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = at;
        RevokedByIp = byIp;
        ReplacedByTokenHash = replacedByHash;
    }
}
