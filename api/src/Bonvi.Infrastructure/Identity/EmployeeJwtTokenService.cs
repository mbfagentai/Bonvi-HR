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
/// Xodim ilovasi uchun JWT va yangilash tokeni yaratadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Tokenda RUXSAT DA'VOLARI UMUMAN YO'Q</b> — na <c>perm</c>, na
/// <c>role</c>. Bu bezak emas, himoyaning bir qismi: har bir HR endpointi
/// aniq ruxsat kodini talab qiladi, ya'ni da'vosi yo'q token ularning
/// birortasidan ham o'ta olmaydi. Auditoriya tekshiruvi esa undan oldin
/// keladi va tokenni umuman qabul qilmaydi. Ikki mustaqil to'siq.
/// </para>
/// <para>
/// ⭐ <b><c>NameIdentifier</c> da'vosi ham ATAYLAB yozilmaydi.</b> Uni
/// <c>ICurrentUser.UserId</c> o'qiydi — audit jurnali «kim o'zgartirdi»
/// ni shundan oladi. Xodim hisobi esa <c>users</c> jadvalida YO'Q, ya'ni
/// u yerga yozilgan identifikator mavjud bo'lmagan foydalanuvchiga
/// ishora qilardi va jurnal jimgina yolg'on gapira boshlardi.
/// </para>
/// <para>
/// <b><c>sub</c> — HISOBNING identifikatori</b>, xodimniki emas. Hisob
/// qayta berilsa (masalan o'chirilib, yangisi ochilsa), eski tokenlar
/// yangilaridan ajralib turishi kerak. Xodim identifikatori esa
/// <c>emp</c> da'vosida — u qulaylik uchun, qaror qabul qilish uchun
/// emas: hamma ishlovchi uni baribir hisob qatoridan qayta o'qiydi.
/// </para>
/// <para>
/// <b>Imzolash kaliti HR tokeniniki bilan BIR XIL</b> — sababi
/// <see cref="EmployeeJwtOptions"/> izohida.
/// </para>
/// </remarks>
public sealed class EmployeeJwtTokenService : IEmployeeTokenService
{
    /// <summary>Xodim identifikatori tokenda shu nom bilan yuriladi.</summary>
    /// <remarks>
    /// HR tokenidagi bilan bir xil nom (<c>JwtTokenService</c>): ikkala
    /// tokenda ham u «bu token qaysi xodimga tegishli» degan bitta
    /// savolga javob beradi.
    /// </remarks>
    public const string EmployeeClaim = "emp";

    private readonly EmployeeJwtOptions _options;
    private readonly JwtOptions _shared;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public EmployeeJwtTokenService(
        IOptions<EmployeeJwtOptions> options,
        IOptions<JwtOptions> shared,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(shared);

        _options = options.Value;
        _shared = shared.Value;
        _clock = clock;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_shared.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public TokenPair CreateTokens(EmployeeAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var now = _clock.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>(3)
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(EmployeeClaim, account.EmployeeId.ToString()),

            // Har bir token noyob: o'g'irlangan tokenni logdagi boshqasidan
            // ajratish uchun (HR tokenidagi bilan bir xil sabab).
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            // Beruvchi HR tokeniniki bilan bir xil — bu bitta tizim.
            // Ajratuvchi belgi faqat auditoriya.
            Issuer = _shared.Issuer,
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

    /// <inheritdoc />
    /// <remarks>
    /// Tuz (salt) kerak emas: token allaqachon 256 bitli tasodifiy qiymat,
    /// uni lug'at bo'yicha topib bo'lmaydi (<c>JwtTokenService</c> dagi
    /// bilan bir xil mulohaza).
    /// </remarks>
    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes).ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>256 bitli kriptografik tasodifiy token.</summary>
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
