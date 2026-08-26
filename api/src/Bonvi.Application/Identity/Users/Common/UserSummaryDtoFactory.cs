using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// <see cref="User"/> + uning rollarini <see cref="UserSummaryDto"/> ga o'giradi.
/// </summary>
/// <remarks>
/// Ro'yxat, yaratish va tahrirlash javoblarining HAMMASI bir xil ko'rinishni
/// qaytaradi — shu sababli o'girish bitta joyda, uch marta takrorlanmaydi.
/// </remarks>
internal static class UserSummaryDtoFactory
{
    public static UserSummaryDto Create(User user, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var assigned = roles
            .Where(role => user.RoleIds.Contains(role.Id))
            .OrderBy(role => role.Name, StringComparer.CurrentCulture)
            .ToList();

        return new UserSummaryDto(
            user.Id,
            user.Login.Value,
            user.FullName.Full,
            user.FullName.ShortForm,
            user.IsActive,
            user.LastLoginAt,
            user.LockedUntil,
            [.. assigned.Select(role => role.Code)],
            [.. assigned.Select(role => role.Name)]);
    }
}
