using System.ComponentModel.DataAnnotations;

namespace Bonvi.Infrastructure.Identity;

/// <summary>JWT sozlamalari (<c>appsettings.json</c> dagi «Jwt» bo'limi).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Tokenni kim bergan.</summary>
    [Required]
    public string Issuer { get; set; } = "bonvi-hr";

    /// <summary>Token kim uchun.</summary>
    [Required]
    public string Audience { get; set; } = "bonvi-hr-web";

    /// <summary>
    /// Imzolash kaliti. Kamida 32 belgi (HMAC-SHA256 uchun 256 bit).
    /// </summary>
    /// <remarks>
    /// Production'da bu qiymat muhit o'zgaruvchisidan keladi va
    /// hech qachon repozitoriyga tushmaydi.
    /// </remarks>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey kamida 32 belgidan iborat bo'lishi kerak.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Access token muddati. Ataylab qisqa: uni serverda bekor qilib
    /// bo'lmaydi, shu sababli o'g'irlangan token tez eskirishi kerak.
    /// </summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token muddati (kun).</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;
}
