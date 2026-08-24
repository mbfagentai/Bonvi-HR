using System.Security.Claims;
using Bonvi.Application.Abstractions;

namespace Bonvi.Api.Common;

/// <summary>
/// Joriy foydalanuvchini HTTP so'rovidagi JWT dan o'qiydi.
/// </summary>
/// <remarks>
/// Bu sinf Api qatlamida turadi, chunki u <c>HttpContext</c> ga bog'liq —
/// bu veb texnologiyasi. Application qatlami faqat <see cref="ICurrentUser"/>
/// interfeysini biladi va HTTP haqida hech narsa bilmaydi.
/// </remarks>
public sealed class CurrentUser : ICurrentUser
{
    /// <summary>JWT ichida ruxsatlar shu nom bilan uzatiladi.</summary>
    public const string PermissionClaimType = "perm";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(PermissionClaimType, permission) ?? false;
}
