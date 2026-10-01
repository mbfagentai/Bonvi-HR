using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Bonvi.Api.Mcp;

/// <summary>MCP veb sozlamalari (<c>Mcp</c> bo'limi, <c>docs/SPEC-mcp.md</c> §9).</summary>
public sealed class McpOptions
{
    /// <summary>
    /// AI mijozi foydalanuvchini qaytara oladigan ANIQ manzillar — Claude va
    /// ChatGPT'ning e'lon qilingan OAuth qaytish manzillari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega xost emas, aniq manzil.</b> Ro'yxatdan o'tish (DCR) anonim, ya'ni
    /// yo'lni hujumchi tanlaydi va PKCE tasdiqlovchisi ham unda. Faqat xost
    /// tekshirilsa, shu domenlardagi BIRORTA ochiq yo'naltirish yoki so'rov
    /// satrini oshkor qiladigan sahifa kodni o'g'irlashga yetardi — xavfsizlik
    /// uchinchi tomon saytidagi har bir yo'lga bog'liq bo'lib qolardi
    /// (xavfsizlik tekshiruvi, 2026-10-01).
    /// </para>
    /// <para>
    /// Provayder manzilni o'zgartirsa ulanish aniq xato bilan to'xtaydi;
    /// yangisi <c>Mcp:AllowedRedirectUris</c> bilan qo'shiladi (deploy'siz).
    /// </para>
    /// </remarks>
    public static readonly string[] DefaultRedirectUris =
    [
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
        "https://chatgpt.com/connector_platform_oauth_redirect",
    ];

    /// <summary>
    /// <c>Mcp:PublicBaseUrl</c>, masalan <c>https://hr.bonvi.uz</c>. Development va
    /// Testing'dan tashqarida SHART: OAuth emitentining manzili <c>Host</c>
    /// sarlavhasiga bog'liq bo'lib qolmasin.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    public IReadOnlySet<string> AllowedRedirectUris { get; init; } =
        new HashSet<string>(DefaultRedirectUris, StringComparer.Ordinal);

    /// <summary>
    /// Kompyuterdagi dasturlarning loopback (<c>http://127.0.0.1:…</c>) qaytishi.
    /// Sukut: Development va Testing'da ochiq, production'da YOPIQ
    /// (<c>Mcp:AllowLoopbackRedirects</c>). Rahbariyat claude.ai va ChatGPT
    /// orqali ulanadi; loopback esa sizib chiqqan parol bilan istalgan mahalliy
    /// vosita ulanishiga yo'l ochardi.
    /// </summary>
    public bool AllowLoopbackRedirects { get; init; }

    public static McpOptions From(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection("Mcp:AllowedRedirectUris");
        var uris = section.GetChildren().Select(child => child.Value).ToList();

        if (uris.Count == 0 && !string.IsNullOrWhiteSpace(section.Value))
        {
            uris = [.. section.Value.Split(',')];
        }

        var set = uris
            .Select(uri => (uri ?? string.Empty).Trim())
            .Where(uri => uri.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var loopbackDefault = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        return new McpOptions
        {
            PublicBaseUrl = configuration["Mcp:PublicBaseUrl"]?.Trim().TrimEnd('/') is { Length: > 0 } url ? url : null,
            AllowedRedirectUris = set.Count > 0 ? set : new HashSet<string>(DefaultRedirectUris, StringComparer.Ordinal),
            AllowLoopbackRedirects = configuration.GetValue("Mcp:AllowLoopbackRedirects", loopbackDefault),
        };
    }

    public static bool IsLoopback(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IdnHost is "localhost" or "127.0.0.1" or "[::1]" or "::1";
    }

    /// <summary>
    /// Kod (yoki xato) yuborilishi mumkin bo'lgan qaytish manzili: aniq kanonik
    /// ko'rinish, ruxsat ro'yxatidagi ANIQ https manzil yoki (ruxsat bo'lsa)
    /// loopback'ga http, fragment va foydalanuvchi ma'lumotisiz, ≤ 512 belgi.
    /// Maxsus sxemalar (<c>cursor://</c>) rad etiladi.
    /// </summary>
    public bool IsAllowedRedirectUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > McpLimits.MaxUriLength)
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(value, uri.AbsoluteUri, StringComparison.Ordinal))
        {
            return false;
        }

        if (uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            return AllowLoopbackRedirects && IsLoopback(uri);
        }

        return uri.Scheme == Uri.UriSchemeHttps && AllowedRedirectUris.Contains(value);
    }

    /// <summary>
    /// Kirish/rozilik sahifasi CSP'sidagi <c>form-action</c>: o'zi va ruxsat
    /// etilgan qaytish manzillarining manbalari — brauzer bu qoidani formadan
    /// keyingi yo'naltirishga ham qo'llaydi.
    /// </summary>
    public string FormActionSources()
    {
        var origins = AllowedRedirectUris
            .Select(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.GetLeftPart(UriPartial.Authority) : null)
            .Where(origin => origin is not null)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        var sources = string.Join(' ', origins);

        if (AllowLoopbackRedirects)
        {
            sources += " http://localhost:* http://127.0.0.1:* http://[::1]:*";
        }

        return "'self' " + sources;
    }

    /// <summary>Rozilik sahifasi kalit qayerga ketishini qanday aytadi.</summary>
    public static string DestinationLabel(string redirectUri) =>
        Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            ? IsLoopback(uri) ? "kompyuteringizdagi dastur" : uri.IdnHost
            : "?";
}

/// <summary>Chegaralar — bir joyda (<c>docs/SPEC-mcp.md</c> §5).</summary>
public static class McpLimits
{
    public const int MaxUriLength = 512;
    public const int MaxState = 512;

    /// <summary>Bir vaqtda mavjud bo'lishi mumkin bo'lgan HALI ULANMAGAN mijozlar soni.</summary>
    public const int MaxPendingRegistrations = 50;

    /// <summary>Ulanmagan (rozilik olinmagan) mijoz qancha turadi.</summary>
    public static readonly TimeSpan PendingClientLifetime = TimeSpan.FromHours(1);

    /// <summary>Kutilayotgan kirish/rozilik so'rovlari keshining chegarasi.</summary>
    public const int MaxPendingRequests = 2000;

    public static readonly TimeSpan AccessLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Ulanishning mutlaq umri — undan keyin yangilash rad etiladi, qayta kirish kerak.</summary>
    public static readonly TimeSpan GrantLifetime = TimeSpan.FromDays(90);

    /// <summary>Kirish sahifasining umri (ochilgandan parol kiritilguncha).</summary>
    public static readonly TimeSpan LoginPageLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Rozilik sahifasining umri (kirgandan «Ruxsat berish» gacha).</summary>
    public static readonly TimeSpan ConsentPageLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Bir vaqtdagi <c>/mcp</c> chaqiruvlar: ulanish va butun jarayon bo'yicha.</summary>
    public const int ConcurrentPerGrant = 2;
    public const int ConcurrentGlobal = 6;

    /// <summary>Autentifikatsiyadan OLDINGI IP (/64) byudjeti — soxta tokenlar oqimiga qarshi.</summary>
    public const int PreAuthPerMinute = 300;
}

/// <summary>Ochiq manzillar.</summary>
public static class McpUrls
{
    public static string BaseUrl(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = request.HttpContext.RequestServices.GetRequiredService<McpOptions>();
        return options.PublicBaseUrl ?? $"{request.Scheme}://{request.Host}";
    }

    public static string Resource(HttpRequest request) => BaseUrl(request) + "/mcp";

    public static string ResourceMetadata(HttpRequest request) =>
        BaseUrl(request) + "/.well-known/oauth-protected-resource/mcp";
}

/// <summary>Shaffof bo'lmagan sirlar: yaratish, xeshlash, PKCE.</summary>
public static class McpSecrets
{
    public const string AccessPrefix = "bhr_at_";
    public const string RefreshPrefix = "bhr_rt_";
    public const string CodePrefix = "bhr_ac_";

    public static string New(string prefix) => prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Kichik harfli hex SHA-256 — bazada sirning o'rniga shu turadi.</summary>
    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>RFC 7636 §4.1: 43–128 belgi, [A-Z a-z 0-9 - . _ ~].</summary>
    public static bool IsValidVerifier(string? verifier) =>
        verifier is { Length: >= 43 and <= 128 }
        && verifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~');

    /// <summary>S256 tekshiruvi — doimiy vaqtda.</summary>
    public static bool VerifyPkce(string verifier, string challenge)
    {
        var computed = Encoding.ASCII.GetBytes(Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        var expected = Encoding.ASCII.GetBytes(challenge);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    /// <summary>Chaqiriq — base64url(SHA-256), ya'ni aynan 43 belgi.</summary>
    public static bool IsValidChallenge(string? challenge) =>
        challenge is { Length: 43 } && challenge.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

/// <summary>Tashqaridan kelgan matnni ko'rsatishga yaroqli qilish.</summary>
public static class McpText
{
    /// <summary>
    /// Boshqaruv va format belgilarini (bidi, nol kenglikdagi birlashtirgich,
    /// yangi qator — «Bonvi rasmiy» kabi soxta nom yasashning xomashyosi) olib
    /// tashlaydi, bo'shliqlarni siqadi, uzunlikni kesadi.
    /// </summary>
    public static string Sanitize(string? value, int max)
    {
        var builder = new StringBuilder();

        foreach (var ch in (value ?? string.Empty).Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(ch))
            {
                builder.Append(' ');
                continue;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned)
            {
                continue;
            }

            builder.Append(ch);
        }

        var text = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= max ? text : text[..max];
    }
}

/// <summary>Tarmoq yordamchilari.</summary>
public static class McpNet
{
    /// <summary>
    /// Tezlik cheklovining kaliti: IPv6 /64 bo'yicha (bitta abonent butun /64
    /// oladi — manzil bo'yicha bo'linma osongina aylanib o'tiladi), IPv4 o'zi.
    /// </summary>
    public static string Partition(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ip = context.Connection.RemoteIpAddress;
        if (ip is null)
        {
            return "nomalum";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        return ip.AddressFamily == AddressFamily.InterNetworkV6
            ? Convert.ToHexString(ip.GetAddressBytes(), 0, 8) + "/64"
            : ip.ToString();
    }
}

/// <summary>MCP tokeni bilan kelgan so'rovga qo'shiladigan da'volar.</summary>
public static class McpClaims
{
    public const string GrantId = "mcp_grant";
    public const string ClientId = "mcp_client";
    public const string ClientName = "mcp_client_name";
}

/// <summary>
/// Kirish va rozilik sahifalari orasidagi kutilayotgan so'rovlar — ALOHIDA va
/// hajmi CHEKLANGAN kesh.
/// </summary>
/// <remarks>
/// Anonim <c>GET /oauth/authorize</c> har chaqiruvda yozuv yaratadi. Umumiy
/// <c>IMemoryCache</c> ning chegarasi yo'q — bir nechta manzildan oqim
/// 2 GB serverni xotirasiz qoldirib, butun HR tizimini yiqitishi mumkin edi
/// (xavfsizlik tekshiruvi, 2026-10-01). Bu yerda 2000 yozuvdan oshsa yangi
/// yozuv QO'SHILMAYDI (sahifa «muddati o'tdi» deydi) — boshqa hech narsa
/// zarar ko'rmaydi.
/// </remarks>
public sealed class McpPendingRequests : IDisposable
{
    private readonly Microsoft.Extensions.Caching.Memory.MemoryCache _cache = new(
        new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions { SizeLimit = McpLimits.MaxPendingRequests });

    public void Set(string key, object value, TimeSpan lifetime) =>
        Microsoft.Extensions.Caching.Memory.CacheExtensions.Set(
            _cache,
            key,
            value,
            new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = lifetime });

    /// <summary>Bir martalik: o'qilgan yozuv darhol o'chiriladi.</summary>
    public object? Take(string key)
    {
        if (!_cache.TryGetValue(key, out var value))
        {
            return null;
        }

        _cache.Remove(key);
        return value;
    }

    public void Dispose() => _cache.Dispose();
}
