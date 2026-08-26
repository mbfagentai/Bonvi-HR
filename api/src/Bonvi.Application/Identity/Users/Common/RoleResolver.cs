using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// Rol kodlarini (<c>"admin"</c>, <c>"hr_manager"</c>) <see cref="Role"/>
/// obyektlariga aylantiradi — foydalanuvchi yaratish va tahrirlashda umumiy.
/// </summary>
/// <remarks>
/// Frontend rolni KOD bilan yuboradi (xuddi <c>CurrentUserDto.Roles</c> kabi
/// qaytariladigan ko'rinish), identifikator bilan emas — kod barqaror va
/// odam o'qiy oladigan qiymat, ID esa faqat bazaning ichki tafsiloti.
/// </remarks>
internal static class RoleResolver
{
    public static async Task<Result<IReadOnlyList<Role>>> ResolveAsync(
        IRoleRepository roles,
        IReadOnlyList<string>? codes,
        CancellationToken cancellationToken)
    {
        if (codes is null || codes.Count == 0)
        {
            return UserErrors.NoRoles;
        }

        var distinctCodes = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctCodes.Count == 0)
        {
            return UserErrors.NoRoles;
        }

        var resolved = new List<Role>(distinctCodes.Count);

        foreach (var code in distinctCodes)
        {
            var role = await roles.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
            if (role is null)
            {
                return UserErrors.RoleNotFound(code);
            }

            resolved.Add(role);
        }

        return Result.Success<IReadOnlyList<Role>>(resolved);
    }
}
