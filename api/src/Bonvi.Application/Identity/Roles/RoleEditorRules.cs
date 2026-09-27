using System.Globalization;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Roles;

/// <summary>
/// Rollar muharririning qoidalari va javob yig'uvchisi — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// Yaratish, tahrirlash va o'chirish bir xil savollarni beradi: «so'rovchida
/// qanday ruxsatlar bor», «u shu rolni egallab turibdimi», «nom band emasmi».
/// Har handler'da qo'lda yozilsa, bir kun biri unutilardi — xuddi
/// <see cref="Users.Common.UserRoleRules"/> dagi kabi sabab.
/// </para>
/// <para>
/// Metodlar sof (repozitoriysiz): chaqiruvchi rollar va hisoblar ro'yxatini
/// allaqachon yuklagan bo'ladi (tizimda bir nechta rol va bir nechta hisob).
/// </para>
/// </remarks>
internal static class RoleEditorRules
{
    /// <summary>Egasi yaratgan rol kodining old qo'shimchasi.</summary>
    private const string CustomCodePrefix = "custom_";

    /// <summary>
    /// So'rovchining JORIY ruxsatlari — uning rollari orqali, bazadan.
    /// </summary>
    /// <remarks>
    /// Tokendagi claim'lar emas, bazadagi rol: «o'zingizda yo'q ruxsatni
    /// bera olmaysiz» qoidasi aynan hozirgi holatga qarab tekshirilishi
    /// kerak. Eski yozuvda bir nechta rol bo'lsa (<see cref="User.RoleIds"/>
    /// izohi), ular birlashtiriladi — xuddi kirishdagi kabi.
    /// <para>
    /// Kodda endi yo'q eski kodlar (masalan <c>employment.manage</c>) bu
    /// to'plamga TUSHMAYDI — nishon rolida ham ular hisobga olinmaydi
    /// (<see cref="EnsureTargetWithinOwn"/>). Bir tomonda qolib, ikkinchisida
    /// tashlansa, «teng huquqli» solishtiruvi noto'g'ri natija berardi.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> PermissionsOf(User user, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles.Where(role => user.RoleIds.Contains(role.Id)))
        {
            result.UnionWith(role.Permissions.Where(Domain.Identity.Permissions.Exists));
        }

        return result;
    }

    /// <summary>So'rovchi super administratormi.</summary>
    public static bool IsSuperAdmin(User user, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        return roles.Any(role =>
            user.RoleIds.Contains(role.Id) &&
            string.Equals(role.Code, SystemRoles.SuperAdmin, StringComparison.Ordinal));
    }

    /// <summary>So'rovchi shu rolni egallab turibdimi.</summary>
    public static bool Holds(User user, Role role)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(role);

        return user.RoleIds.Contains(role.Id);
    }

    /// <summary>
    /// «O'zingizda yo'q ruxsatni bera olmaysiz» — birinchi ortiqcha kod bilan rad etadi.
    /// </summary>
    public static Result EnsureWithinOwn(IReadOnlySet<string> own, IReadOnlyList<string> requested)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(requested);

        var extra = requested.FirstOrDefault(permission => !own.Contains(permission));

        return extra is null ? Result.Success() : RoleErrors.ExceedsOwn(extra);
    }

    /// <summary>
    /// «O'zingizdan YUQORI rolga tega olmaysiz» — rolning JORIY ruxsatlari
    /// ham amaldagi foydalanuvchining ruxsatlari ichida bo'lishi shart.
    /// </summary>
    /// <remarks>
    /// <see cref="EnsureWithinOwn"/> faqat YANGI ro'yxatni tekshiradi. Usiz
    /// tor huquqli rol muharriri kengroq rolni «qisqartirib» (masalan HR
    /// menejerlardan huquqlarni olib) yoki o'chirib yuborardi — interfeys buni
    /// yopardi, server esa yo'q edi (xavfsizlik tekshiruvi, 2026-09-26).
    /// Kodda endi yo'q eski kodlar (masalan <c>employment.manage</c>)
    /// hisobga olinmaydi: ular hech narsa bermaydi.
    /// <para>
    /// <b>Teng huquqli boshqaruvchi rol ham yopiq</b> — huquqlari aynan
    /// so'rovchiniki bilan teng va <c>roles.manage</c> yoki <c>users.manage</c>
    /// bor rol (<see cref="RoleErrors.PeerRole"/>). Bu foydalanuvchilar
    /// tomonidagi qoidaning (<c>UserRoleRules.Allowed</c>) aynan juftligi.
    /// Super administrator bundan ozod: u tizim egasi va barcha huquqli
    /// rolni ham boshqara olishi kerak.
    /// </para>
    /// </remarks>
    public static Result EnsureTargetWithinOwn(IReadOnlySet<string> own, Role target, bool actingIsSuperAdmin)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(target);

        var current = target.Permissions
            .Where(Domain.Identity.Permissions.Exists)
            .ToHashSet(StringComparer.Ordinal);

        var within = EnsureWithinOwn(own, [.. current]);
        if (within.IsFailure || actingIsSuperAdmin)
        {
            return within;
        }

        var managesAccess =
            current.Contains(Domain.Identity.Permissions.RolesManage) ||
            current.Contains(Domain.Identity.Permissions.UsersManage);

        return managesAccess && current.SetEquals(own) ? RoleErrors.PeerRole : Result.Success();
    }

    /// <summary>
    /// Nom boshqa rolda yo'qligini tekshiradi — katta-kichik harfsiz.
    /// </summary>
    /// <param name="roles">Barcha rollar, yashirin bot roli ham.</param>
    /// <param name="name">Tozalangan (trim) nom.</param>
    /// <param name="exceptRoleId">Tahrirlanayotgan rolning o'zi — o'z nomini saqlasa «band» emas.</param>
    public static Result EnsureNameFree(IReadOnlyList<Role> roles, string name, Guid? exceptRoleId)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var taken = roles.Any(role =>
            role.Id != exceptRoleId &&
            string.Equals(role.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

        return taken ? RoleErrors.NameTaken : Result.Success();
    }

    /// <summary>
    /// Yangi rol uchun kod: <c>custom_&lt;8 hex&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kod foydalanuvchiga ko'rsatilmaydi, lekin u rolning barqaror
    /// identifikatori (foydalanuvchi formasi rolni kod bilan yuboradi).
    /// Nomdan yasalmaydi: nom o'zgaradi, kod esa o'zgarmasligi kerak.
    /// </para>
    /// <para>
    /// To'qnashuv ehtimoli amalda nol (bir necha rol, 2³² variant), lekin
    /// <c>roles.code</c> unikal indeks — takrorlansa saqlash 500 bilan
    /// yiqilardi. Shu sababli mavjud kodlar bilan solishtiriladi.
    /// </para>
    /// </remarks>
    public static string NewCode(IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        while (true)
        {
            var code = CustomCodePrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

            if (!roles.Any(role => string.Equals(role.Code, code, StringComparison.Ordinal)))
            {
                return code;
            }
        }
    }

    /// <summary>Rol → javob. Hisoblar soni shu yerda sanaladi.</summary>
    /// <param name="role">Rol.</param>
    /// <param name="users">Barcha hisoblar (faol va faolsiz).</param>
    public static RoleDto ToDto(Role role, IReadOnlyList<User> users)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(users);

        return new RoleDto(
            role.Id,
            role.Code,
            role.Name,
            role.IsProtected,
            [.. role.Permissions.Order(StringComparer.Ordinal)],
            users.Count(user => user.RoleIds.Contains(role.Id)));
    }

    /// <summary>Audit uchun holat nusxasi: kod, nom va ruxsatlar (alifbo tartibida).</summary>
    public static object Snapshot(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new
        {
            code = role.Code,
            name = role.Name,
            permissions = role.Permissions.Order(StringComparer.Ordinal).ToArray(),
        };
    }
}
