using System.Security.Claims;
using Bonvi.Application.Abstractions;
using Bonvi.Infrastructure.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bonvi.Api.Common;

/// <summary>
/// Joriy xodimni HTTP so'rovidagi JWT dan o'qiydi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CurrentUser"/> bilan yonma-yon turadi va ikkalasi bir-birini
/// KO'RMAYDI: xodim tokenida <c>NameIdentifier</c> da'vosi umuman yo'q
/// (<c>EmployeeJwtTokenService</c> izohi), ya'ni <c>ICurrentUser.UserId</c>
/// xodim so'rovida <c>null</c> bo'lib qoladi. Bu ataylab — audit jurnali
/// mavjud bo'lmagan foydalanuvchiga ishora qilmasligi kerak.
/// </para>
/// <para>
/// ⚠️ Da'volar HECH QANDAY tekshiruvsiz o'qiladi va shunday bo'lishi ham
/// kerak: token imzosi, muddati va AUDITORIYASI bu yergacha yetib kelishdan
/// oldin tekshirilgan (<c>AddBonviAuthentication</c>). Bu sinf faqat
/// o'qiydi.
/// </para>
/// </remarks>
public sealed class CurrentEmployee : ICurrentEmployee
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentEmployee(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid? AccountId => Read(JwtRegisteredClaimNames.Sub);

    /// <inheritdoc />
    public Guid? EmployeeId => Read(EmployeeJwtTokenService.EmployeeClaim);

    private Guid? Read(string claimType)
    {
        var raw = Principal?.FindFirstValue(claimType);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
