namespace Bonvi.Domain.Mcp;

/// <summary>
/// AI mijozi (Claude, ChatGPT…) — OAuth dinamik ro'yxatdan o'tish (RFC 7591)
/// bilan yaratiladi (<c>docs/SPEC-mcp.md</c> §5).
/// </summary>
/// <remarks>
/// Faqat OCHIQ mijoz: sir berilmaydi, kodni PKCE (S256) qo'riqlaydi. Mijoz
/// o'zi hech narsaga huquq bermaydi — huquq faqat foydalanuvchi ruxsat bergan
/// <see cref="McpGrant"/> da tug'iladi.
/// </remarks>
public sealed class McpClient
{
    public const int MaxIdLength = 64;
    public const int MaxNameLength = 60;
    public const int MaxRedirectUris = 5;
    public const int MaxUriLength = 512;

    private readonly List<string> _redirectUris = [];

    private McpClient()
    {
        Id = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Ochiq identifikator, masalan <c>mcp_…</c>.</summary>
    public string Id { get; private set; }

    /// <summary>Mijoz o'zi aytgan nom — TOZALANGAN (boshqaruv belgilarisiz).</summary>
    public string Name { get; private set; }

    /// <summary>Ro'yxatdan o'tgan qaytish manzillari (aniq ko'rinishda).</summary>
    public IReadOnlyList<string> RedirectUris => _redirectUris.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public static McpClient Register(string id, string name, IEnumerable<string> redirectUris, DateTimeOffset now)
    {
        var client = new McpClient
        {
            Id = id,
            Name = name,
            CreatedAt = now,
        };

        client._redirectUris.AddRange(redirectUris);
        return client;
    }
}

/// <summary>
/// Bitta «ulanish»: foydalanuvchi mijozga ruxsat berdi. Barcha tokenlar shu
/// yozuvga osilgan — uni bekor qilish kirish va yangilash tokenlarini birdaniga
/// o'ldiradi.
/// </summary>
/// <remarks>
/// <c>users</c> ga FK ATAYLAB yo'q: o'chirilgan yoki faolsizlantirilgan
/// foydalanuvchi shunchaki autentifikatsiyadan o'tmay qoladi, ulanish yozuvi
/// esa «kim qachon ulangan edi» degan savol uchun qoladi.
/// </remarks>
public sealed class McpGrant
{
    public const int MaxRevokedByLength = 64;

    private McpGrant()
    {
        ClientId = string.Empty;
        Resource = string.Empty;
    }

    public Guid Id { get; private set; }

    public string ClientId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>RFC 8707 resursi — bizning <c>/mcp</c> manzilimiz.</summary>
    public string Resource { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Kim bekor qildi: foydalanuvchi identifikatori yoki <c>system:…</c>
    /// (kod/token qayta ishlatilganda server o'zi bekor qiladi).
    /// </summary>
    public string? RevokedBy { get; private set; }

    public static McpGrant Create(string clientId, Guid userId, string resource, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        ClientId = clientId,
        UserId = userId,
        Resource = resource,
        CreatedAt = now,
    };

    public bool IsAliveAt(DateTimeOffset now, TimeSpan lifetime) =>
        RevokedAt is null && CreatedAt + lifetime > now;

    public void Revoke(string by, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedBy = by.Length <= MaxRevokedByLength ? by : by[..MaxRevokedByLength];
    }
}

/// <summary>Bir martalik avtorizatsiya kodi. Bazada faqat SHA-256 xeshi.</summary>
public sealed class McpAuthCode
{
    private McpAuthCode()
    {
        CodeHash = string.Empty;
        ClientId = string.Empty;
        RedirectUri = string.Empty;
        CodeChallenge = string.Empty;
        Resource = string.Empty;
    }

    public Guid Id { get; private set; }

    public string CodeHash { get; private set; }

    public string ClientId { get; private set; }

    public Guid UserId { get; private set; }

    public string RedirectUri { get; private set; }

    /// <summary>PKCE S256 chaqirig'i (tasdiqlovchining SHA-256 base64url).</summary>
    public string CodeChallenge { get; private set; }

    public string Resource { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>Kod almashtirilganda yaratilgan ulanish — kod qayta kelsa u bekor qilinadi.</summary>
    public Guid? GrantId { get; private set; }

    public static McpAuthCode Issue(
        string codeHash,
        string clientId,
        Guid userId,
        string redirectUri,
        string codeChallenge,
        string resource,
        DateTimeOffset now,
        TimeSpan lifetime) => new()
        {
            Id = Guid.CreateVersion7(),
            CodeHash = codeHash,
            ClientId = clientId,
            UserId = userId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            Resource = resource,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };

    public void AttachGrant(Guid grantId) => GrantId = grantId;
}

/// <summary>Kirish yoki yangilash tokeni. Bazada faqat SHA-256 xeshi.</summary>
public sealed class McpToken
{
    public const string Access = "access";
    public const string Refresh = "refresh";

    private McpToken()
    {
        Kind = Access;
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid GrantId { get; private set; }

    /// <summary><see cref="Access"/> yoki <see cref="Refresh"/>.</summary>
    public string Kind { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Almashtirilgan yangilash tokeni. Qayta kelsa — ulanish bekor qilinadi.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static McpToken Issue(Guid grantId, string kind, string tokenHash, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        Id = Guid.CreateVersion7(),
        GrantId = grantId,
        Kind = kind,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = now + lifetime,
    };
}

/// <summary>
/// Jurnal: bitta MCP vositasi chaqiruvi. Ilova uni hech qachon o'zgartirmaydi
/// va o'chirmaydi — faqat 365 kundan eski yozuvlar kuniga bir marta tozalanadi.
/// </summary>
/// <remarks>
/// Foydalanuvchi va mijoz nomlari — o'sha paytdagi NUSXA: keyin ism
/// o'zgarsa yoki hisob o'chirilsa ham jurnal o'qiladigan bo'lib qoladi.
/// </remarks>
public sealed class McpAuditEntry
{
    public const int MaxNameLength = 200;
    public const int MaxToolLength = 100;
    public const int MaxArgumentsLength = 500;
    public const int MaxIpLength = 64;
    public const int MaxUserAgentLength = 300;

    public const string Ok = "ok";
    public const string Denied = "denied";
    public const string Error = "error";

    private McpAuditEntry()
    {
        UserName = string.Empty;
        ClientId = string.Empty;
        ClientName = string.Empty;
        Tool = string.Empty;
        Arguments = string.Empty;
        Outcome = Ok;
    }

    public long Id { get; private set; }

    public DateTimeOffset At { get; private set; }

    public Guid? UserId { get; private set; }

    public string UserName { get; private set; }

    public string ClientId { get; private set; }

    public string ClientName { get; private set; }

    public Guid? GrantId { get; private set; }

    public string Tool { get; private set; }

    /// <summary>Qisqartirilgan argumentlar (vositalar sir qabul qilmaydi).</summary>
    public string Arguments { get; private set; }

    /// <summary><see cref="Ok"/> | <see cref="Denied"/> | <see cref="Error"/>.</summary>
    public string Outcome { get; private set; }

    public int? RowCount { get; private set; }

    public int DurationMs { get; private set; }

    public string? Ip { get; private set; }

    public string? UserAgent { get; private set; }

    public static McpAuditEntry Record(
        DateTimeOffset at,
        Guid? userId,
        string userName,
        string clientId,
        string clientName,
        Guid? grantId,
        string tool,
        string arguments,
        string outcome,
        int? rowCount,
        int durationMs,
        string? ip,
        string? userAgent) => new()
        {
            At = at,
            UserId = userId,
            UserName = Cut(userName, MaxNameLength),
            ClientId = Cut(clientId, McpClient.MaxIdLength),
            ClientName = Cut(clientName, MaxNameLength),
            GrantId = grantId,
            Tool = Cut(tool, MaxToolLength),
            Arguments = Cut(arguments, MaxArgumentsLength),
            Outcome = outcome,
            RowCount = rowCount,
            DurationMs = durationMs,
            Ip = ip is null ? null : Cut(ip, MaxIpLength),
            UserAgent = userAgent is null ? null : Cut(userAgent, MaxUserAgentLength),
        };

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
