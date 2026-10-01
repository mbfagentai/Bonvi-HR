using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Mcp;
using Bonvi.Domain.Mcp;
using Bonvi.Infrastructure.Mcp;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.Mcp;

/// <summary>
/// AI ulanish uchun OAuth 2.1 avtorizatsiya serveri — MCP avtorizatsiya
/// spetsifikatsiyasi talab qilgan oqim (<c>docs/SPEC-mcp.md</c> §5).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>/.well-known/oauth-protected-resource[/mcp]</c> — RFC 9728;</item>
///   <item><c>/.well-known/oauth-authorization-server[/mcp]</c> — RFC 8414;</item>
///   <item><c>POST /oauth/register</c> — RFC 7591 (faqat ochiq mijozlar);</item>
///   <item><c>GET/POST /oauth/authorize</c> — BIZNING kirish va rozilik sahifamiz, faqat PKCE S256;</item>
///   <item><c>POST /oauth/token</c> — <c>authorization_code</c> (+PKCE) va aylanuvchi <c>refresh_token</c>;</item>
///   <item><c>POST /oauth/revoke</c> — RFC 7009.</item>
/// </list>
/// <para>
/// Parol AI ilovasiga BERILMAYDI — u faqat shu sahifada, bizning serverda
/// tekshiriladi. Kod faqat <c>mcp.connect</c> ruxsati bor hisobga beriladi.
/// Kodlar va tokenlar bazada faqat xesh bo'lib turadi. MCP yopiq bo'lsa har bir
/// manzil 404 qaytaradi.
/// </para>
/// <para>
/// <c>mcp_*</c> jadvallariga yozuv <see cref="McpDatabase.CreateStoreContext"/>
/// orqali (sababi o'sha metod izohida).
/// </para>
/// </remarks>
public static partial class McpOAuthEndpoints
{
    /// <summary>Server beradigan yagona doira.</summary>
    public const string Scope = "hr.read";

    public const string RegisterRateLimitPolicy = "mcp-register";
    public const string TokenRateLimitPolicy = "mcp-token";

    /// <summary>
    /// Kirish/rozilik formasi — IP bo'yicha daqiqasiga 10 ta. Veb-kirishning
    /// <c>login</c> siyosatidan ALOHIDA: bitta ofis IP'si ortidagi HR'larning
    /// kirishi va rahbarning AI ulashi bir-birining chegarasini yemasin. Parol
    /// tanlashga qarshi asosiy to'siq baribir hisob qulfi (5 xato → 15 daqiqa,
    /// veb bilan UMUMIY hisoblagich).
    /// </summary>
    public const string AuthorizeRateLimitPolicy = "mcp-authorize";

    private const string PendingPrefix = "mcp:oauth:";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };

    private static readonly string[] ScopeList = [Scope];
    private static readonly string[] HeaderOnly = ["header"];
    private static readonly string[] CodeOnly = ["code"];
    private static readonly string[] QueryOnly = ["query"];
    private static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
    private static readonly string[] S256Only = ["S256"];
    private static readonly string[] NoneOnly = ["none"];

    [LoggerMessage(EventId = 4110, Level = LogLevel.Warning, Message = "MCP mijozini ro'yxatga olish rad etildi: kunlik chegara")]
    private static partial void LogRegistrationCap(ILogger logger);

    [LoggerMessage(EventId = 4111, Level = LogLevel.Warning, Message = "MCP: {UserId} foydalanuvchida mcp.connect yo'q — rad etildi")]
    private static partial void LogNoPermission(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4112, Level = LogLevel.Information, Message = "MCP: kod berildi — foydalanuvchi {UserId}, mijoz {ClientId}")]
    private static partial void LogCodeIssued(ILogger logger, Guid userId, string clientId);

    [LoggerMessage(EventId = 4113, Level = LogLevel.Warning, Message = "MCP: kod qayta ishlatildi ({ClientId}) — ulanish bekor qilindi")]
    private static partial void LogCodeReplay(ILogger logger, string clientId);

    [LoggerMessage(EventId = 4114, Level = LogLevel.Information, Message = "MCP: yangi ulanish {GrantId} — foydalanuvchi {UserId}")]
    private static partial void LogGrantCreated(ILogger logger, Guid grantId, Guid userId);

    [LoggerMessage(EventId = 4115, Level = LogLevel.Warning, Message = "MCP: yangilash tokeni qayta ishlatildi — ulanish {GrantId} bekor qilindi")]
    private static partial void LogRefreshReuse(ILogger logger, Guid grantId);

    private sealed record PendingRequest(
        string ClientId,
        string RedirectUri,
        string CodeChallenge,
        string? State,
        string Resource,
        Guid? UserId = null,
        string? UserName = null);

    public static IEndpointRouteBuilder MapMcpOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/.well-known/oauth-protected-resource", ProtectedResourceAsync).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/.well-known/oauth-protected-resource/mcp", ProtectedResourceAsync).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/.well-known/oauth-authorization-server", AuthorizationServerAsync).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/.well-known/oauth-authorization-server/mcp", AuthorizationServerAsync).AllowAnonymous().ExcludeFromDescription();

        /*
          Qolgan `/.well-known/*` (masalan `openid-configuration` — u ATAYLAB
          yo'q) — aniq 404. Usiz u umumiy (fallback) avtorizatsiya siyosatiga
          urilib 401 qaytarardi va kashfiyot qilayotgan AI mijozi buni «kirish
          kerak» deb tushunishi mumkin edi. Aniq marshrutlar bu umumiy
          marshrutdan ustun turadi.
        */
        app.MapGet("/.well-known/{**rest}", () => Results.NotFound()).AllowAnonymous().ExcludeFromDescription();

        app.MapPost("/oauth/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RegisterRateLimitPolicy)
            .ExcludeFromDescription();

        // GET ham cheklanadi: har bir yaroqli GET kesh yozuvini yaratadi.
        app.MapGet("/oauth/authorize", AuthorizeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AuthorizeRateLimitPolicy)
            .ExcludeFromDescription();

        app.MapPost("/oauth/authorize", AuthorizePostAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(AuthorizeRateLimitPolicy)
            .ExcludeFromDescription();

        app.MapPost("/oauth/token", TokenAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(TokenRateLimitPolicy)
            .ExcludeFromDescription();

        app.MapPost("/oauth/revoke", RevokeAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(TokenRateLimitPolicy)
            .ExcludeFromDescription();

        return app;
    }

    // =====================================================================
    //  Metama'lumot
    // =====================================================================

    private static async Task<IResult> ProtectedResourceAsync(HttpContext context, McpRuntime runtime)
    {
        if (!await runtime.IsOnAsync(context.RequestAborted).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        return OAuthJson(context, new Dictionary<string, object>
        {
            ["resource"] = McpUrls.Resource(context.Request),
            ["authorization_servers"] = new[] { McpUrls.BaseUrl(context.Request) },
            ["scopes_supported"] = ScopeList,
            ["bearer_methods_supported"] = HeaderOnly,
            ["resource_name"] = "Bonvi HR (faqat o'qish)",
        });
    }

    private static async Task<IResult> AuthorizationServerAsync(HttpContext context, McpRuntime runtime)
    {
        if (!await runtime.IsOnAsync(context.RequestAborted).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        var b = McpUrls.BaseUrl(context.Request);

        return OAuthJson(context, new Dictionary<string, object>
        {
            ["issuer"] = b,
            ["authorization_endpoint"] = b + "/oauth/authorize",
            ["token_endpoint"] = b + "/oauth/token",
            ["registration_endpoint"] = b + "/oauth/register",
            ["revocation_endpoint"] = b + "/oauth/revoke",
            ["response_types_supported"] = CodeOnly,
            ["response_modes_supported"] = QueryOnly,
            ["grant_types_supported"] = GrantTypes,
            ["code_challenge_methods_supported"] = S256Only,
            ["token_endpoint_auth_methods_supported"] = NoneOnly,
            ["revocation_endpoint_auth_methods_supported"] = NoneOnly,
            ["scopes_supported"] = ScopeList,
            ["authorization_response_iss_parameter_supported"] = true,
        });
    }

    // =====================================================================
    //  Dinamik ro'yxatdan o'tish (RFC 7591)
    // =====================================================================

    private static async Task<IResult> RegisterAsync(
        HttpContext context,
        McpRuntime runtime,
        McpDatabase database,
        McpOptions options,
        ILoggerFactory loggers)
    {
        var cancellationToken = context.RequestAborted;

        if (!await runtime.IsOnAsync(cancellationToken).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        if (!LimitBody(context))
        {
            return TooLarge();
        }

        JsonElement body;

        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            body = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return OAuthError(context, "invalid_client_metadata", "Body must be a JSON object");
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return OAuthError(context, "invalid_client_metadata", "Body must be a JSON object");
        }

        if (!body.TryGetProperty("redirect_uris", out var redirects) || redirects.ValueKind != JsonValueKind.Array)
        {
            return OAuthError(context, "invalid_redirect_uri", "redirect_uris (array) required");
        }

        var uris = new List<string>();

        foreach (var item in redirects.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return OAuthError(context, "invalid_redirect_uri", "redirect_uris must be strings");
            }

            uris.Add(item.GetString()!);
        }

        if (uris.Count == 0 || uris.Count > McpClient.MaxRedirectUris)
        {
            return OAuthError(context, "invalid_redirect_uri", $"1..{McpClient.MaxRedirectUris} redirect_uris required");
        }

        foreach (var uri in uris)
        {
            if (!options.IsAllowedRedirectUri(uri))
            {
                return OAuthError(
                    context,
                    "invalid_redirect_uri",
                    "redirect_uri must be one of: " + string.Join(", ", options.AllowedRedirectUris.Order(StringComparer.Ordinal))
                    + (options.AllowLoopbackRedirects ? ", or http on loopback" : string.Empty)
                    + "; canonical form, no fragment, max " + McpLimits.MaxUriLength + " chars");
            }
        }

        foreach (var (property, allowed) in new[] { ("grant_types", GrantTypes), ("response_types", CodeOnly) })
        {
            if (!body.TryGetProperty(property, out var values))
            {
                continue;
            }

            if (values.ValueKind != JsonValueKind.Array)
            {
                return OAuthError(context, "invalid_client_metadata", property + " must be an array");
            }

            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String || !allowed.Contains(value.GetString()))
                {
                    return OAuthError(context, "invalid_client_metadata", property + ": only " + string.Join(", ", allowed));
                }
            }
        }

        var name = body.TryGetProperty("client_name", out var clientName) && clientName.ValueKind == JsonValueKind.String
            ? McpText.Sanitize(clientName.GetString(), McpClient.MaxNameLength)
            : string.Empty;

        if (name.Length == 0)
        {
            name = "AI mijoz";
        }

        await using var store = database.CreateStoreContext();

        /*
          Autentifikatsiyasiz endpoint: IP cheklovi ustiga UMUMIY chegara —
          lekin faqat hali ULANMAGAN mijozlar sanaladi va ular bir soatdan
          keyin tozalanadi. Ilgari kunlik chegara barcha yangi mijozlarni
          sanardi: bir necha manzildan 50 ta soxta ro'yxat butun kun yangi
          ulanishni to'sib qo'yardi; endi eng ko'pi bilan bir soat.
        */
        await PruneAsync(store, cancellationToken).ConfigureAwait(false);

        if (await store.McpClients
                .CountAsync(client => !store.McpGrants.Any(grant => grant.ClientId == client.Id), cancellationToken)
                .ConfigureAwait(false)
            >= McpLimits.MaxPendingRegistrations)
        {
            LogRegistrationCap(loggers.CreateLogger("Bonvi.Mcp"));
            return OAuthJson(
                context,
                new Dictionary<string, object>
                {
                    ["error"] = "temporarily_unavailable",
                    ["error_description"] = "Registration limit reached, try again later",
                },
                StatusCodes.Status429TooManyRequests);
        }

        var registered = McpClient.Register(
            "mcp_" + McpSecrets.Base64Url(RandomNumberGenerator.GetBytes(18)),
            name,
            uris,
            DateTimeOffset.UtcNow);

        store.McpClients.Add(registered);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OAuthJson(
            context,
            new Dictionary<string, object>
            {
                ["client_id"] = registered.Id,
                ["client_id_issued_at"] = registered.CreatedAt.ToUnixTimeSeconds(),
                ["client_name"] = registered.Name,
                ["redirect_uris"] = registered.RedirectUris,
                ["grant_types"] = GrantTypes,
                ["response_types"] = CodeOnly,

                // Ochiq mijoz — sir yo'q, nima so'ralgan bo'lsa ham (RFC 7591 §3.2.1).
                ["token_endpoint_auth_method"] = "none",
                ["scope"] = Scope,
            },
            StatusCodes.Status201Created);
    }

    // =====================================================================
    //  Avtorizatsiya — kirish sahifasi, rozilik, kod
    // =====================================================================

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        McpRuntime runtime,
        McpDatabase database,
        McpOptions options,
        McpPendingRequests cache)
    {
        var cancellationToken = context.RequestAborted;

        if (!await runtime.IsOnAsync(cancellationToken).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        var query = context.Request.Query;
        string? clientId = query["client_id"];
        string? redirectUri = query["redirect_uri"];
        string? state = query["state"];

        await using var store = database.CreateStoreContext();

        // 1) Avval mijoz va qaytish manzili — ikkalasiga ishonch bo'lmaguncha
        //    hech qayerga YO'NALTIRILMAYDI.
        var client = string.IsNullOrEmpty(clientId)
            ? null
            : await store.McpClients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken).ConfigureAwait(false);

        if (client is null)
        {
            return Page(context, "Xato", ErrorBox("Noma'lum ilova (client_id). AI ilovasida ulanishni qaytadan qo'shing."), 400);
        }

        var matched = MatchRedirectUri(client, redirectUri, options);

        if (matched is null)
        {
            return Page(context, "Xato", ErrorBox("Qaytish manzili ro'yxatdan o'tganiga mos emas yoki ruxsat etilmagan."), 400);
        }

        // 2) Bundan keyingi xatolar mijozga qaytariladi (RFC 6749 §4.1.2.1).
        if (state is { Length: > McpLimits.MaxState })
        {
            return RedirectError(context, options, matched, "invalid_request", "state too long", null);
        }

        if (query["response_type"] != "code")
        {
            return RedirectError(context, options, matched, "unsupported_response_type", "Only response_type=code", state);
        }

        string? challenge = query["code_challenge"];

        if (query["code_challenge_method"] != "S256" || !McpSecrets.IsValidChallenge(challenge))
        {
            return RedirectError(context, options, matched, "invalid_request", "PKCE with code_challenge_method=S256 is required", state);
        }

        var canonicalResource = McpUrls.Resource(context.Request);
        string? resource = query["resource"];

        if (!string.IsNullOrEmpty(resource) && !SameResource(resource, canonicalResource))
        {
            return RedirectError(context, options, matched, "invalid_target", "Unknown resource", state);
        }

        // Doira bitta: nima so'ralmasin, beriladigani `hr.read`.
        var handle = Store(cache, new PendingRequest(client.Id, matched, challenge!, state, canonicalResource), McpLimits.LoginPageLifetime);
        return LoginPage(context, client, handle, null);
    }

    private static async Task<IResult> AuthorizePostAsync(
        HttpContext context,
        McpRuntime runtime,
        McpDatabase database,
        McpOptions options,
        McpPendingRequests cache,
        McpSignInCommandHandler signIn,
        ILoggerFactory loggers)
    {
        var cancellationToken = context.RequestAborted;

        if (!await runtime.IsOnAsync(cancellationToken).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        if (!LimitBody(context))
        {
            return TooLarge();
        }

        if (!context.Request.HasFormContentType)
        {
            return Page(context, "Xato", ErrorBox("Noto'g'ri so'rov."), 400);
        }

        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var step = form["step"].ToString();
        var handle = form["req"].ToString();

        if (Take(cache, handle) is not { } pending)
        {
            return Page(context, "Muddati o'tdi", ErrorBox("Sahifa muddati o'tdi. AI ilovasida ulanishni qaytadan boshlang."), 400);
        }

        await using var store = database.CreateStoreContext();

        var client = await store.McpClients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == pending.ClientId, cancellationToken)
            .ConfigureAwait(false);

        if (client is null || MatchRedirectUri(client, pending.RedirectUri, options) is null)
        {
            return Page(context, "Xato", ErrorBox("Ilova topilmadi."), 400);
        }

        var logger = loggers.CreateLogger("Bonvi.Mcp");

        if (step == "login" && pending.UserId is null)
        {
            var login = form["login"].ToString().Trim();
            var password = form["password"].ToString();

            if (login.Length > 50)
            {
                login = login[..50];
            }

            var result = await signIn
                .HandleAsync(new McpSignInCommand(login, password), cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                // Sahifa yangi tutqich bilan qayta chiziladi — eskisi `Take` bilan
                // o'chirilgan, ya'ni bitta tutqich bilan cheksiz urinib bo'lmaydi.
                var retry = Store(cache, pending, McpLimits.LoginPageLifetime);
                return LoginPage(context, client, retry, result.Error.Message);
            }

            if (!result.Value.CanConnect)
            {
                LogNoPermission(logger, result.Value.UserId);
                return Page(
                    context,
                    "Ruxsat yo'q",
                    ErrorBox("Sizning rolingizda «AI orqali o'qish» ruxsati yo'q. Kerak bo'lsa, tizim egasiga murojaat qiling "
                             + "(Rollar → «AI orqali o'qish (MCP)»)."),
                    403);
            }

            var consent = Store(
                cache,
                pending with { UserId = result.Value.UserId, UserName = result.Value.FullName },
                McpLimits.ConsentPageLifetime);

            return ConsentPage(context, client, pending.RedirectUri, result.Value.FullName, consent);
        }

        if (step == "consent" && pending.UserId is { } userId)
        {
            if (form["decision"] != "allow")
            {
                return RedirectError(context, options, pending.RedirectUri, "access_denied", "The user denied the request", pending.State);
            }

            var code = McpSecrets.New(McpSecrets.CodePrefix);

            store.McpAuthCodes.Add(McpAuthCode.Issue(
                McpSecrets.Hash(code),
                pending.ClientId,
                userId,
                pending.RedirectUri,
                pending.CodeChallenge,
                pending.Resource,
                DateTimeOffset.UtcNow,
                McpLimits.CodeLifetime));

            await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LogCodeIssued(logger, userId, pending.ClientId);

            var parameters = new Dictionary<string, string?>
            {
                ["code"] = code,
                ["iss"] = McpUrls.BaseUrl(context.Request),
            };

            if (pending.State is not null)
            {
                parameters["state"] = pending.State;
            }

            return Results.Redirect(QueryHelpers.AddQueryString(pending.RedirectUri, parameters));
        }

        return Page(context, "Xato", ErrorBox("Noto'g'ri so'rov."), 400);
    }

    // =====================================================================
    //  Token
    // =====================================================================

    private static async Task<IResult> TokenAsync(
        HttpContext context,
        McpRuntime runtime,
        McpDatabase database,
        Bonvi.Application.Identity.Abstractions.IUserAccessReader accessReader,
        ILoggerFactory loggers)
    {
        var cancellationToken = context.RequestAborted;

        if (!await runtime.IsOnAsync(cancellationToken).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        if (!LimitBody(context))
        {
            return TooLarge();
        }

        if (!context.Request.HasFormContentType)
        {
            return OAuthError(context, "invalid_request", "application/x-www-form-urlencoded body required");
        }

        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var grantType = form["grant_type"].ToString();
        var clientId = form["client_id"].ToString();
        var logger = loggers.CreateLogger("Bonvi.Mcp");
        var now = DateTimeOffset.UtcNow;

        await using var store = database.CreateStoreContext();

        if (grantType == "authorization_code")
        {
            var code = form["code"].ToString();
            var verifier = form["code_verifier"].ToString();
            var redirectUri = form["redirect_uri"].ToString();

            if (code.Length == 0 || code.Length > 200)
            {
                return OAuthError(context, "invalid_grant", "code missing");
            }

            if (!McpSecrets.IsValidVerifier(verifier))
            {
                return OAuthError(context, "invalid_grant", "code_verifier missing or malformed");
            }

            var hash = McpSecrets.Hash(code);
            var row = await store.McpAuthCodes.FirstOrDefaultAsync(c => c.CodeHash == hash, cancellationToken).ConfigureAwait(false);

            if (row is null)
            {
                return OAuthError(context, "invalid_grant", "unknown code");
            }

            if (row.ConsumedAt is not null)
            {
                // Qayta kelgan kod — u sizib chiqqan. U yaratgan narsa o'ldiriladi (RFC 6819 §4.4.1.1).
                if (row.GrantId is { } leaked)
                {
                    await RevokeGrantAsync(store, leaked, "system:code-replay", cancellationToken).ConfigureAwait(false);
                }

                LogCodeReplay(logger, row.ClientId);
                return OAuthError(context, "invalid_grant", "code already used");
            }

            if (row.ExpiresAt <= now)
            {
                return OAuthError(context, "invalid_grant", "code expired");
            }

            if (!McpSecrets.VerifyPkce(verifier, row.CodeChallenge))
            {
                return OAuthError(context, "invalid_grant", "PKCE verification failed");
            }

            if (!string.Equals(row.ClientId, clientId, StringComparison.Ordinal))
            {
                return OAuthError(context, "invalid_grant", "client mismatch");
            }

            if (!string.Equals(row.RedirectUri, redirectUri, StringComparison.Ordinal))
            {
                return OAuthError(context, "invalid_grant", "redirect_uri mismatch");
            }

            var resource = form["resource"].ToString();

            if (resource.Length > 0 && !SameResource(resource, row.Resource))
            {
                return OAuthError(context, "invalid_target", "resource mismatch");
            }

            if (!await StillAllowedAsync(accessReader, row.UserId, cancellationToken).ConfigureAwait(false))
            {
                return OAuthError(context, "invalid_grant", "user no longer allowed");
            }

            // Atomar bir martalik: ikkita parallel almashinuvdan faqat bittasi o'tadi.
            var won = await store.McpAuthCodes
                .Where(c => c.Id == row.Id && c.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.ConsumedAt, now), cancellationToken)
                .ConfigureAwait(false);

            if (won != 1)
            {
                return OAuthError(context, "invalid_grant", "code already used");
            }

            var grant = McpGrant.Create(row.ClientId, row.UserId, row.Resource, now);
            store.McpGrants.Add(grant);
            row.AttachGrant(grant.Id);

            var issued = Issue(store, grant, now);
            await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PruneAsync(store, cancellationToken).ConfigureAwait(false);

            LogGrantCreated(logger, grant.Id, grant.UserId);
            return TokenResponse(context, issued);
        }

        if (grantType == "refresh_token")
        {
            var refresh = form["refresh_token"].ToString();

            if (!refresh.StartsWith(McpSecrets.RefreshPrefix, StringComparison.Ordinal) || refresh.Length > 200)
            {
                return OAuthError(context, "invalid_grant", "unknown refresh_token");
            }

            var hash = McpSecrets.Hash(refresh);
            var token = await store.McpTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Kind == McpToken.Refresh, cancellationToken)
                .ConfigureAwait(false);

            if (token is null)
            {
                return OAuthError(context, "invalid_grant", "unknown refresh_token");
            }

            var grant = await store.McpGrants.FirstOrDefaultAsync(g => g.Id == token.GrantId, cancellationToken).ConfigureAwait(false);

            if (grant is null || grant.RevokedAt is not null)
            {
                return OAuthError(context, "invalid_grant", "grant revoked");
            }

            if (!string.Equals(grant.ClientId, clientId, StringComparison.Ordinal))
            {
                return OAuthError(context, "invalid_grant", "client mismatch");
            }

            if (token.ConsumedAt is not null)
            {
                // Aylanishdagi qayta ishlatish — eski token birovning qo'lida.
                await RevokeGrantAsync(store, grant.Id, "system:refresh-reuse", cancellationToken).ConfigureAwait(false);
                LogRefreshReuse(logger, grant.Id);
                return OAuthError(context, "invalid_grant", "refresh_token already used");
            }

            if (token.ExpiresAt <= now)
            {
                return OAuthError(context, "invalid_grant", "refresh_token expired");
            }

            if (!grant.IsAliveAt(now, McpLimits.GrantLifetime))
            {
                return OAuthError(context, "invalid_grant", "connection older than 90 days — sign in again");
            }

            if (!await StillAllowedAsync(accessReader, grant.UserId, cancellationToken).ConfigureAwait(false))
            {
                return OAuthError(context, "invalid_grant", "user no longer allowed");
            }

            var won = await store.McpTokens
                .Where(t => t.Id == token.Id && t.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ConsumedAt, now), cancellationToken)
                .ConfigureAwait(false);

            if (won != 1)
            {
                await RevokeGrantAsync(store, grant.Id, "system:refresh-reuse", cancellationToken).ConfigureAwait(false);
                return OAuthError(context, "invalid_grant", "refresh_token already used");
            }

            var issued = Issue(store, grant, now);
            await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return TokenResponse(context, issued);
        }

        return OAuthError(context, "unsupported_grant_type", "authorization_code or refresh_token");
    }

    /// <summary>RFC 7009. Juftning istalgan tokenini bekor qilish butun ulanishni yopadi. Har doim 200.</summary>
    private static async Task<IResult> RevokeAsync(HttpContext context, McpRuntime runtime, McpDatabase database)
    {
        var cancellationToken = context.RequestAborted;

        if (!await runtime.IsOnAsync(cancellationToken).ConfigureAwait(false))
        {
            return Results.NotFound();
        }

        if (!LimitBody(context))
        {
            return TooLarge();
        }

        if (!context.Request.HasFormContentType)
        {
            return OAuthError(context, "invalid_request", "form body required");
        }

        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var token = form["token"].ToString();

        if (token.Length is > 0 and <= 200)
        {
            await using var store = database.CreateStoreContext();
            var hash = McpSecrets.Hash(token);
            var row = await store.McpTokens.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
                .ConfigureAwait(false);

            // RFC 7009 §2.1: token taqdim etgan mijozga tegishli bo'lishi kerak.
            // Boshqa mijozning tokeni jimgina e'tiborsiz qoldiriladi (javob baribir 200).
            var clientId = form["client_id"].ToString();
            var owner = row is null
                ? null
                : await store.McpGrants.AsNoTracking()
                    .Where(grant => grant.Id == row.GrantId)
                    .Select(grant => grant.ClientId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (row is not null && (clientId.Length == 0 || string.Equals(owner, clientId, StringComparison.Ordinal)))
            {
                await RevokeGrantAsync(store, row.GrantId, "client:revoke", cancellationToken).ConfigureAwait(false);
            }
        }

        return Results.Ok();
    }

    // =====================================================================
    //  Saqlash
    // =====================================================================

    private sealed record IssuedTokens(string AccessToken, string RefreshToken, int ExpiresIn);

    private static IssuedTokens Issue(Bonvi.Infrastructure.Persistence.AppDbContext store, McpGrant grant, DateTimeOffset now)
    {
        var access = McpSecrets.New(McpSecrets.AccessPrefix);
        var refresh = McpSecrets.New(McpSecrets.RefreshPrefix);

        store.McpTokens.Add(McpToken.Issue(grant.Id, McpToken.Access, McpSecrets.Hash(access), now, McpLimits.AccessLifetime));
        store.McpTokens.Add(McpToken.Issue(grant.Id, McpToken.Refresh, McpSecrets.Hash(refresh), now, McpLimits.RefreshLifetime));

        return new IssuedTokens(access, refresh, (int)McpLimits.AccessLifetime.TotalSeconds);
    }

    /// <summary>Ulanishni (va uning hamma tokenini) bekor qiladi.</summary>
    public static async Task RevokeGrantAsync(
        Bonvi.Infrastructure.Persistence.AppDbContext store,
        Guid grantId,
        string by,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);

        var grant = await store.McpGrants.FirstOrDefaultAsync(g => g.Id == grantId, cancellationToken).ConfigureAwait(false);

        if (grant is null || grant.RevokedAt is not null)
        {
            return;
        }

        grant.Revoke(by, DateTimeOffset.UtcNow);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Uy tozalash: muddati bir kundan oshgan kod/tokenlar va bir soatda ulanmagan mijozlar.</summary>
    private static async Task PruneAsync(Bonvi.Infrastructure.Persistence.AppDbContext store, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);

        await store.McpAuthCodes.Where(c => c.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await store.McpTokens.Where(t => t.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var pendingCutoff = DateTimeOffset.UtcNow - McpLimits.PendingClientLifetime;

        await store.McpClients
            .Where(c => c.CreatedAt < pendingCutoff && !store.McpGrants.Any(g => g.ClientId == c.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<bool> StillAllowedAsync(
        Bonvi.Application.Identity.Abstractions.IUserAccessReader accessReader,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await accessReader.ReadAsync(userId, cancellationToken).ConfigureAwait(false);
        return access?.Permissions.Contains(Bonvi.Domain.Identity.Permissions.McpConnect) == true;
    }

    // =====================================================================
    //  Qaytish manzili qoidalari
    // =====================================================================

    /// <summary>
    /// Ro'yxatdan o'tgan manzil bilan aniq moslik; loopback'da port farq qilishi
    /// mumkin (RFC 8252 §7.3). Natija BUGUNGI ruxsat ro'yxatidan ham o'tishi
    /// kerak — xost ro'yxatdan olib tashlangan bo'lsa, eski mijoz hech narsa olmaydi.
    /// </summary>
    private static string? MatchRedirectUri(McpClient client, string? requested, McpOptions options)
    {
        string? match = null;

        if (string.IsNullOrEmpty(requested))
        {
            match = client.RedirectUris.Count == 1 ? client.RedirectUris[0] : null;
        }
        else
        {
            foreach (var registered in client.RedirectUris)
            {
                if (string.Equals(registered, requested, StringComparison.Ordinal))
                {
                    match = requested;
                    break;
                }

                if (Uri.TryCreate(registered, UriKind.Absolute, out var r)
                    && Uri.TryCreate(requested, UriKind.Absolute, out var q)
                    && r.Scheme == Uri.UriSchemeHttp && q.Scheme == Uri.UriSchemeHttp
                    && McpOptions.IsLoopback(r)
                    && r.IdnHost == q.IdnHost && r.AbsolutePath == q.AbsolutePath
                    && r.Query == q.Query && q.Fragment.Length == 0)
                {
                    match = requested;
                    break;
                }
            }
        }

        return match is not null && options.IsAllowedRedirectUri(match) ? match : null;
    }

    private static bool SameResource(string a, string b) =>
        string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    // =====================================================================
    //  Kutilayotgan so'rov — serverda, tasodifiy tutqich ostida
    // =====================================================================

    /*
      Nega Data Protection emas: bu tizimda kalitlar diskka saqlanmaydi va
      konteyner qayta ishga tushganda yangilanadi — imzolangan blob ham
      baribir yaroqsiz bo'lardi. Serverdagi kesh esa sodda va sahifaga hech
      narsa bermaydi: tutqich — 256 bitli tasodifiy satr, uni taxmin qilib
      bo'lmaydi va u BIR MARTALIK (`Take`). Kesh hajmi CHEKLANGAN
      (`McpPendingRequests` izohi).
    */
    private static string Store(McpPendingRequests cache, PendingRequest request, TimeSpan lifetime)
    {
        var handle = McpSecrets.Base64Url(RandomNumberGenerator.GetBytes(32));
        cache.Set(PendingPrefix + handle, request, lifetime);
        return handle;
    }

    private static PendingRequest? Take(McpPendingRequests cache, string handle)
    {
        if (string.IsNullOrEmpty(handle) || handle.Length > 64)
        {
            return null;
        }

        return cache.Take(PendingPrefix + handle) as PendingRequest;
    }

    /// <summary>
    /// Anonim POST tanasining chegarasi. Kestrel sukuti 30 MB — kirish formasi
    /// va ro'yxatdan o'tish JSON'i uchun bir necha KB yetarli.
    /// </summary>
    private const long MaxBodyBytes = 16 * 1024;

    private static bool LimitBody(HttpContext context)
    {
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            return false;
        }

        if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
        {
            feature.MaxRequestBodySize = MaxBodyBytes;
        }

        return true;
    }

    private static IResult TooLarge() => Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    // =====================================================================
    //  Javoblar
    // =====================================================================

    private static IResult RedirectError(
        HttpContext context,
        McpOptions options,
        string redirectUri,
        string error,
        string description,
        string? state)
    {
        // Ruxsat ro'yxatida bo'lmagan xostga xatoni ham yubormaymiz — shu yerda ko'rsatamiz.
        if (!options.IsAllowedRedirectUri(redirectUri))
        {
            return Page(context, "Xato", ErrorBox("So'rov noto'g'ri: " + description), 400);
        }

        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description,
            ["iss"] = McpUrls.BaseUrl(context.Request),
        };

        if (state is not null)
        {
            parameters["state"] = state;
        }

        return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, parameters));
    }

    private static IResult TokenResponse(HttpContext context, IssuedTokens tokens) => OAuthJson(context, new Dictionary<string, object>
    {
        ["access_token"] = tokens.AccessToken,
        ["token_type"] = "Bearer",
        ["expires_in"] = tokens.ExpiresIn,
        ["refresh_token"] = tokens.RefreshToken,
        ["scope"] = Scope,
    });

    private static IResult OAuthError(HttpContext context, string error, string description, int status = 400) =>
        OAuthJson(context, new Dictionary<string, object> { ["error"] = error, ["error_description"] = description }, status);

    private static IResult OAuthJson(HttpContext context, object payload, int status = 200)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        return Results.Content(JsonSerializer.Serialize(payload, Json), "application/json", Encoding.UTF8, status);
    }

    // =====================================================================
    //  HTML — iOS uslubi (tizim shrifti, oq karta, Bonvi ko'ki), o'zbekcha
    // =====================================================================

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static IResult LoginPage(HttpContext context, McpClient client, string handle, string? error) => Page(context, "Kirish", $"""
        <p class="lead">«{H(client.Name)}» Bonvi HR ma'lumotlarini <b>faqat o'qish uchun</b> so'rayapti.
        Davom etish uchun Bonvi HR'dagi telefon raqamingiz va parolingizni kiriting.</p>
        {(error is null ? string.Empty : ErrorBox(error))}
        <form method="post" action="/oauth/authorize" autocomplete="on">
          <input type="hidden" name="step" value="login">
          <input type="hidden" name="req" value="{H(handle)}">
          <label>Telefon raqami<input name="login" inputmode="tel" autocomplete="username" placeholder="90 123 45 67" required autofocus></label>
          <label>Parol<input name="password" type="password" autocomplete="current-password" required></label>
          <button type="submit" class="primary">Kirish</button>
        </form>
        <p class="note">Parolingiz AI ilovasiga berilmaydi — u faqat shu sahifada, Bonvi serverida tekshiriladi.</p>
        """);

    private static IResult ConsentPage(HttpContext context, McpClient client, string redirectUri, string userName, string handle) =>
        Page(context, "Ruxsat", $"""
            <p class="lead"><b>{H(userName)}</b>, «{H(client.Name)}» Bonvi HR ma'lumotlarini <b>faqat o'qish uchun</b> so'rayapti.</p>
            <div class="dest">Ruxsat bersangiz, kalit <b>{H(McpOptions.DestinationLabel(redirectUri))}</b> ga yuboriladi.
              <span>Ilova ID: {H(client.Id)}</span></div>
            <ul class="facts">
              <li>Faqat o'qiydi — hech narsani o'zgartirmaydi va o'chirmaydi.</li>
              <li>Faqat sizga ruxsat berilgan bo'limlarni ko'radi.</li>
              <li>Pasport ma'lumotlari va yuklangan fayllar hech qachon berilmaydi.</li>
              <li>Har bir so'rov jurnalga yoziladi. Ulanishni tizim egasi «Tizim sozlamalari» da istalgan payt uzadi;
                  parolingiz tiklansa yoki «barcha sessiyalardan chiqish» bosilsa, u o'zi uziladi.</li>
              <li>Bu sahifani faqat O'ZINGIZ hozirgina AI ilovasida «Ulash» ni bosgan bo'lsangiz davom ettiring.
                  Ilova nomi («{H(client.Name)}») uni ro'yxatdan o'tkazgan tomonning o'zi yozgan — tekshirilmagan.</li>
            </ul>
            <form method="post" action="/oauth/authorize" class="row">
              <input type="hidden" name="step" value="consent">
              <input type="hidden" name="req" value="{H(handle)}">
              <button type="submit" name="decision" value="deny" class="secondary">Rad etish</button>
              <button type="submit" name="decision" value="allow" class="primary">Ruxsat berish</button>
            </form>
            """);

    private static string ErrorBox(string message) => $"""<div class="error">{H(message)}</div>""";

    private static IResult Page(HttpContext context, string title, string body, int status = 200)
    {
        /*
          O'z CSP'imiz: skript umuman yo'q, ramkaga solish yo'q (kirish
          sahifasida clickjacking). `form-action` — o'zi va ruxsat etilgan
          qaytish manzillarining manbalari: brauzer bu qoidani rozilik
          POST'idan keyingi AI mijoziga YO'NALTIRISHGA ham qo'llaydi, ya'ni
          `'self'` ning o'zi oqimni to'sib qo'yardi.

          COOP `unsafe-none`: AI ilovalari ulanishni ko'pincha popup oynada
          ochadi va qaytgach `window.opener` orqali xabar beradi. Umumiy
          `same-origin` (SecurityHeadersMiddleware) popup'ning ochuvchi bilan
          aloqasini butunlay uzib, ulanishni tugatmay qo'yardi.
        */
        var options = context.RequestServices.GetRequiredService<McpOptions>();
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; style-src 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; "
            + "form-action " + options.FormActionSources();
        context.Response.Headers["Cross-Origin-Opener-Policy"] = "unsafe-none";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XFrameOptions = "DENY";

        var html = new StringBuilder();
        html.Append(CultureInfo.InvariantCulture, $$"""
            <!doctype html>
            <html lang="uz"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>{{H(title)}} — Bonvi HR · AI ulanish</title>
            <style>
              *{box-sizing:border-box}
              body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
                   background:#f2f2f7;color:#1c1c1e;font:15px/1.47 -apple-system,BlinkMacSystemFont,"SF Pro Text","Segoe UI",Roboto,sans-serif;padding:16px}
              .card{width:100%;max-width:400px;background:#fff;border-radius:20px;padding:28px 24px;
                    box-shadow:0 1px 2px rgba(0,0,0,.04),0 8px 30px rgba(0,0,0,.06)}
              .brand{display:flex;align-items:center;gap:12px;margin-bottom:20px}
              .logo{width:40px;height:40px;border-radius:11px;background:#007aff;color:#fff;display:flex;align-items:center;
                    justify-content:center;font-weight:700;font-size:19px}
              h1{font-size:19px;margin:0;font-weight:600}
              .sub{font-size:12px;color:#8e8e93}
              .lead{margin:0 0 18px}
              label{display:block;font-size:13px;color:#3a3a3c;margin-bottom:12px}
              input{display:block;width:100%;margin-top:6px;padding:12px 14px;border:1px solid #d1d1d6;border-radius:12px;font:inherit;background:#fff}
              input:focus{outline:none;border-color:#007aff;box-shadow:0 0 0 3px rgba(0,122,255,.2)}
              button{font:inherit;font-weight:600;border:0;border-radius:12px;padding:13px 16px;cursor:pointer;width:100%}
              .primary{background:#007aff;color:#fff}
              .primary:hover{filter:brightness(.95)}
              .secondary{background:#f2f2f7;color:#1c1c1e}
              .row{display:flex;gap:10px;margin-top:8px}
              .error{background:#fff1f0;color:#c41d1d;border-radius:12px;padding:10px 12px;margin:0 0 14px;font-size:14px}
              .note{font-size:12px;color:#8e8e93;margin:14px 0 0}
              .facts{padding-left:18px;margin:0 0 16px;color:#3a3a3c;font-size:14px}
              .facts li{margin-bottom:6px}
              .dest{background:#f2f7ff;border:1px solid #cfe3ff;border-radius:12px;padding:10px 12px;margin:0 0 14px;font-size:14px}
              .dest span{display:block;font-size:11px;color:#8e8e93;margin-top:4px;word-break:break-all}
            </style></head><body><main class="card">
            <div class="brand"><div class="logo">B</div><div><h1>Bonvi HR</h1><div class="sub">AI ulanish · faqat o'qish</div></div></div>
            """);
        html.Append(body);
        html.Append("</main></body></html>");

        return Results.Content(html.ToString(), "text/html; charset=utf-8", Encoding.UTF8, status);
    }
}
