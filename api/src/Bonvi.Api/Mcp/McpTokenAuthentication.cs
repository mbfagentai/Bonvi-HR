using System.Security.Claims;
using System.Text.Encodings.Web;
using Bonvi.Api.Common;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Mcp;
using Bonvi.Infrastructure.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bonvi.Api.Mcp;

public sealed class McpTokenAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// <c>/mcp</c> uchun Bearer autentifikatsiyasi — bizning shaffof bo'lmagan OAuth
/// kirish tokenlarimiz bilan (<c>docs/SPEC-mcp.md</c> §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>YANGILIK — har bir so'rovda</b>, veb-paneldagi JWT bilan bir xil qoida
/// (<c>AuthenticationExtensions.ApplyCurrentAccessAsync</c>): token xeshi
/// topiladi, muddati o'tmagan, ulanish bekor qilinmagan va 90 kundan yosh,
/// foydalanuvchi faol (<see cref="IUserAccessReader"/> — vebdagi o'sha 30
/// soniyalik kesh) va uning rolida HALI <see cref="Permissions.McpConnect"/>
/// bor. Ya'ni rolidan ruxsatni olish yoki hisobni faolsizlantirish ulanishni
/// keyingi chaqiruvdayoq o'ldiradi — hech bir token ruxsatdan uzoq yashamaydi.
/// </para>
/// <para>
/// Foydalanuvchining ruxsatlari veb-tokendagi kabi <c>perm</c> da'volariga
/// aylanadi va har bir vosita aynan shularni tekshiradi.
/// </para>
/// <para>
/// 401 javobida <c>WWW-Authenticate: Bearer resource_metadata="…"</c> (RFC 9728)
/// bor — standart MCP mijozi shundan bizning avtorizatsiya serverimizni topadi
/// va OAuth oqimini boshlaydi.
/// </para>
/// </remarks>
public sealed class McpTokenAuthenticationHandler(
    IOptionsMonitor<McpTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    McpDatabase database,
    IUserAccessReader accessReader)
    : AuthenticationHandler<McpTokenAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "McpToken";

    private bool _tokenPresented;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();

        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        _tokenPresented = true;

        if (!token.StartsWith(McpSecrets.AccessPrefix, StringComparison.Ordinal) || token.Length > 200)
        {
            return AuthenticateResult.Fail("token yaroqsiz");
        }

        if (!database.IsConfigured)
        {
            return AuthenticateResult.Fail("MCP sozlanmagan");
        }

        var hash = McpSecrets.Hash(token);
        var now = DateTimeOffset.UtcNow;
        var cancellationToken = Context.RequestAborted;

        await using var store = database.CreateStoreContext();

        var row = await (
                from item in store.McpTokens
                join grant in store.McpGrants on item.GrantId equals grant.Id
                join client in store.McpClients on grant.ClientId equals client.Id
                where item.TokenHash == hash && item.Kind == McpToken.Access
                select new { item.ExpiresAt, Grant = grant, ClientName = client.Name })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null || row.ExpiresAt <= now || !row.Grant.IsAliveAt(now, McpLimits.GrantLifetime))
        {
            return AuthenticateResult.Fail("token yaroqsiz");
        }

        var access = await accessReader.ReadAsync(row.Grant.UserId, cancellationToken).ConfigureAwait(false);

        if (access is null || !access.Permissions.Contains(Permissions.McpConnect))
        {
            return AuthenticateResult.Fail("foydalanuvchiga endi ruxsat yo'q");
        }

        var fullName = await store.Users
            .AsNoTracking()
            .Where(user => user.Id == row.Grant.UserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Oxirgi ishlatilgan vaqt — daqiqada ko'pi bilan bir marta: har
        // chaqiruvga bitta UPDATE ortiqcha (API kalitlaridagi bilan bir xil).
        if (row.Grant.LastUsedAt is null || now - row.Grant.LastUsedAt > TimeSpan.FromMinutes(1))
        {
            await store.McpGrants
                .Where(grant => grant.Id == row.Grant.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(grant => grant.LastUsedAt, now), cancellationToken)
                .ConfigureAwait(false);
        }

        var identity = new ClaimsIdentity(Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, row.Grant.UserId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, fullName?.Full ?? string.Empty));

        foreach (var role in access.RoleCodes)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in access.Permissions)
        {
            identity.AddClaim(new Claim(CurrentUser.PermissionClaimType, permission));
        }

        identity.AddClaim(new Claim(McpClaims.GrantId, row.Grant.Id.ToString()));
        identity.AddClaim(new Claim(McpClaims.ClientId, row.Grant.ClientId));
        identity.AddClaim(new Claim(McpClaims.ClientName, row.ClientName));

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        var header = $"Bearer resource_metadata=\"{McpUrls.ResourceMetadata(Request)}\", scope=\"{McpOAuthEndpoints.Scope}\"";

        if (_tokenPresented)
        {
            header += ", error=\"invalid_token\"";
        }

        Response.Headers.WWWAuthenticate = header;
        return Task.CompletedTask;
    }
}
