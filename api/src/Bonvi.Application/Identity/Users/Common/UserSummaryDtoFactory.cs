using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// <see cref="User"/> + uning rolini <see cref="UserSummaryDto"/> ga o'giradi.
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

        // Rol topilmasligi mumkin (rolsiz eski yozuv) — bunda maydonlar
        // `null` bo'lib qaytadi, javob esa baribir yaroqli qoladi.
        var role = user.RoleId is { } roleId
            ? roles.FirstOrDefault(item => item.Id == roleId)
            : null;

        return new UserSummaryDto(
            user.Id,
            user.Login.Value,
            user.FullName.Full,
            user.FullName.ShortForm,
            user.IsActive,
            user.LastLoginAt,
            user.LockedUntil,
            user.PhotoKey,
            role?.Code,
            role?.Name);
    }
}
