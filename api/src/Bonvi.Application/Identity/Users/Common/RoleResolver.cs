using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// Rol KODINI (<c>"super_admin"</c>, <c>"hr_manager"</c>) <see cref="Role"/>
/// obyektiga aylantiradi — foydalanuvchi yaratish va tahrirlashda umumiy.
/// </summary>
/// <remarks>
/// <para>
/// Frontend rolni KOD bilan yuboradi (xuddi <c>CurrentUserDto.Roles</c> kabi
/// qaytariladigan ko'rinish), identifikator bilan emas — kod barqaror va
/// odam o'qiy oladigan qiymat, ID esa faqat bazaning ichki tafsiloti.
/// </para>
/// <para>
/// <b>Nega ro'yxat emas, BITTA kod.</b> Foydalanuvchida aynan bitta rol
/// bo'ladi (<see cref="User.SetRole"/> izohiga qarang), shu sababli
/// shartnomaning o'zi ham bitta qiymat qabul qiladi: «ikkita rol yuborildi»
/// degan holat umuman yuzaga kelmaydi va uni tekshirish ham kerak emas.
/// </para>
/// <para>
/// <b>Nega repozitoriy emas, tayyor ro'yxat.</b> Chaqiruvchining o'ziga
/// barcha rollar baribir kerak (kim kimga tega oladi — <see cref="UserRoleRules"/>).
/// Tizimda uchta rol bor, ular bitta so'rovda olinadi va shundan keyin
/// hamma narsa xotirada hal bo'ladi.
/// </para>
/// </remarks>
internal static class RoleResolver
{
    public static Result<Role> Resolve(IReadOnlyList<Role> roles, string? code)
    {
        ArgumentNullException.ThrowIfNull(roles);

        if (string.IsNullOrWhiteSpace(code))
        {
            return UserErrors.NoRole;
        }

        var trimmed = code.Trim();
        var role = roles.FirstOrDefault(item =>
            string.Equals(item.Code, trimmed, StringComparison.OrdinalIgnoreCase));

        return role is null ? UserErrors.RoleNotFound(trimmed) : Result.Success(role);
    }
}
