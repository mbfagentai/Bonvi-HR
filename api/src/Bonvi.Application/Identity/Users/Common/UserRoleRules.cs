using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// Rol modelining BARCHA biznes qoidalari — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerdagi uchala qoida ham koddan o'zidan KO'RINMAYDI: ular
/// foydalanuvchining qarori. Shu sababli har biri alohida izohlangan.
/// </para>
/// <list type="number">
///   <item>
///     <b>Kim kimni boshqara oladi</b> (<see cref="EnsureCanActOn"/>).
///     Super administrator — hammani; qolganlar — ruxsatlari O'ZINIKIDAN
///     oshmaydigan boshqa rol egalarini (qoida <see cref="Allowed"/> da).
///   </item>
///   <item>
///     <b>Atigi bitta</b> (<see cref="EnsureSingleHolder"/>). Ikkinchi super
///     administrator yoki ikkinchi bosh rahbar bo'lishi mumkin emas.
///   </item>
///   <item>
///     <b>Oxirgi rahbar</b> (<see cref="EnsureManagerRemains"/>). Tizimda
///     foydalanuvchi boshqaruviga qodir kamida bitta faol hisob qolishi shart.
///   </item>
/// </list>
/// <para>
/// <b>Nega hammasi bitta faylda.</b> Bir xil qoida oltita amalda kerak
/// (yaratish, tahrirlash, faolsizlantirish, faollashtirish, parol tiklash,
/// rasm). Har birida qo'lda yozilsa, bir kun kelib biri unutilardi — va
/// aynan o'sha yo'l orqali bosh rahbar super administratorga tegib qolardi.
/// </para>
/// <para>
/// <b>Nega barcha metodlar sof (repozitoriysiz).</b> Chaqiruvchi rollar va
/// foydalanuvchilar ro'yxatini allaqachon yuklab olgan bo'ladi (uchala rol
/// va bir nechta hisob — butun tizimda shuncha). Qoidalar bazani o'zi
/// so'ramasa, ularni testda ham, boshqa handler'da ham qayta ishlatish oson
/// va ortiqcha so'rov ketmaydi.
/// </para>
/// </remarks>
internal static class UserRoleRules
{
    /// <summary>
    /// Foydalanuvchining roli. Rol berilmagan yoki topilmagan bo'lsa — <c>null</c>.
    /// </summary>
    public static Role? RoleOf(User user, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        if (user.RoleId is not { } roleId)
        {
            return null;
        }

        return roles.FirstOrDefault(role => role.Id == roleId);
    }

    /// <summary>
    /// Foydalanuvchining rol KODI. Rol berilmagan yoki topilmagan bo'lsa — <c>null</c>.
    /// </summary>
    public static string? CodeOf(User user, IReadOnlyList<Role> roles) => RoleOf(user, roles)?.Code;

    /// <summary>
    /// So'rovchi shu foydalanuvchiga (uning JORIY roli bo'yicha) tega oladimi.
    /// </summary>
    public static Result EnsureCanActOn(User acting, User target, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(acting);
        ArgumentNullException.ThrowIfNull(target);

        return Allowed(RoleOf(acting, roles), RoleOf(target, roles));
    }

    /// <summary>
    /// So'rovchi shu ROLNI bera oladimi (yaratishda va rol almashtirishda).
    /// </summary>
    public static Result EnsureCanAssign(User acting, Role role, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(acting);
        ArgumentNullException.ThrowIfNull(role);

        return Allowed(RoleOf(acting, roles), role);
    }

    /// <summary>
    /// «Atigi bitta» qoidasi: shu rolda boshqa FAOL hisob bo'lmasligi kerak.
    /// </summary>
    /// <param name="role">Beriladigan rol.</param>
    /// <param name="allUsers">Tizimdagi barcha hisoblar (faol va faolsiz).</param>
    /// <param name="exceptUserId">
    /// Tahrirlanayotgan hisobning o'zi — u «boshqa egasi» sifatida
    /// sanalmasligi kerak, aks holda rolini o'zgartirmagan holda ismini
    /// tahrirlash ham rad etilardi.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Nega faqat FAOL hisoblar sanaladi.</b> HR haqiqatida odam
    /// almashadi: eski bosh rahbar ishdan ketsa, hisobi faolsizlantiriladi
    /// va o'rniga yangisi qo'yiladi. Faolsizlarni ham sanash tizimni
    /// «bu lavozim abadiy band» holatiga qulflab qo'yardi — chiqish yo'li
    /// esa faqat bazaga qo'lda aralashish bo'lardi. Ayni sabab
    /// <see cref="SystemRoles.SingleHolder"/> izohida ham yozilgan.
    /// </para>
    /// <para>
    /// <b>Almashuv qanday ketadi.</b> Bosh rahbar uchun oddiy: super
    /// administrator eskisini faolsizlantiradi, keyin yangisini qo'shadi.
    /// Super administratorning O'ZI uchun esa boshqa yo'l bor —
    /// hisobning login va ismini tahrirlash: unga tega oladigan yagona
    /// odam o'zi va o'zini faolsizlantira olmaydi (bu ataylab: aks holda
    /// tizim egasiz qolardi).
    /// </para>
    /// </remarks>
    public static Result EnsureSingleHolder(
        Role role,
        IReadOnlyList<User> allUsers,
        Guid? exceptUserId)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(allUsers);

        if (!SystemRoles.SingleHolder.Contains(role.Code))
        {
            return Result.Success();
        }

        var band = allUsers.Any(user =>
            user.IsActive &&
            user.Id != exceptUserId &&
            user.RoleIds.Contains(role.Id));

        if (!band)
        {
            return Result.Success();
        }

        return string.Equals(role.Code, SystemRoles.SuperAdmin, StringComparison.Ordinal)
            ? UserErrors.SuperAdminExists
            : UserErrors.MainBossExists;
    }

    /// <summary>
    /// «Oxirgi rahbar» himoyasi: foydalanuvchi boshqaruviga qodir kamida
    /// bitta FAOL hisob qolishi shart.
    /// </summary>
    /// <param name="target">Faolsizlantirilayotgan yoki roli tushirilayotgan hisob.</param>
    /// <param name="allUsers">Tizimdagi barcha hisoblar.</param>
    /// <param name="roles">Barcha rollar.</param>
    /// <remarks>
    /// <para>
    /// Bu tekshiruv domen darajasida bo'lishi MUMKIN EMAS:
    /// <see cref="User"/> agregati boshqa foydalanuvchilar haqida hech
    /// narsa bilmaydi (agregatlar bir-biridan mustaqil).
    /// </para>
    /// <para>
    /// Yangi modelda u amalda deyarli ishga tushmaydi (super administratorga
    /// faqat o'zi tegadi, o'zini esa faolsizlantira olmaydi), lekin ATAYLAB
    /// qoldirildi: qoidalar kelajakda yumshasa ham, tizim boshqaruvsiz
    /// qolmasligi kerak. Narxi — bir marta xotirada sanash.
    /// </para>
    /// </remarks>
    public static Result EnsureManagerRemains(
        User target,
        IReadOnlyList<User> allUsers,
        IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(allUsers);
        ArgumentNullException.ThrowIfNull(roles);

        var rahbarRolIds = roles
            .Where(role => SystemRoles.FullAccess.Contains(role.Code))
            .Select(role => role.Id)
            .ToHashSet();

        if (rahbarRolIds.Count == 0 || !target.RoleIds.Any(rahbarRolIds.Contains))
        {
            return Result.Success();
        }

        var boshqaRahbar = allUsers.Any(user =>
            user.Id != target.Id &&
            user.IsActive &&
            user.RoleIds.Any(rahbarRolIds.Contains));

        return boshqaRahbar ? Result.Success() : UserErrors.LastAdminProtected;
    }

    /// <summary>
    /// «Kim kimga tega oladi» — qoidaning YAGONA nusxasi (frontend ham
    /// aynan shu funksiyani <c>GET /api/roles</c> dagi ruxsatlar bilan takrorlaydi).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ilgari bu rol KODLARI matritsasi edi (bosh rahbar → faqat HR menejer).
    /// Rollarni endi egasi o'zi yaratadi, ya'ni kodlar ro'yxati oldindan
    /// ma'lum emas — qoida RUXSATLARNI solishtirishga o'tdi:
    /// </para>
    /// <list type="number">
    ///   <item>So'rovchining roli yo'q — rad.</item>
    ///   <item>So'rovchi <b>super_admin</b> — ruxsat: u tizim egasi.</item>
    ///   <item>
    ///     Nishon roli himoyalangan (<b>super_admin</b> yoki bot roli) — rad.
    ///     Bot roli shartnomada alohida sanalmagan, lekin u ham
    ///     himoyalangan: bot hisobining parolini tiklash yoki uni
    ///     faolsizlantirish anketa botini to'xtatib qo'yadi, bu esa
    ///     bugungidek faqat super administratorning qarori bo'lib qoladi.
    ///   </item>
    ///   <item>
    ///     Nishon roli so'rovchining O'Z roli — rad. Teng huquqli
    ///     hamkasbini boshqarish (masalan ikkinchi bosh rahbarni
    ///     faolsizlantirish) bugun ham mumkin emas edi.
    ///   </item>
    ///   <item>
    ///     Nishon rolining ruxsatlari so'rovchinikining QISM TO'PLAMI —
    ///     ruxsat; aks holda rad. O'zidan kuchliroq hisobga tegish (yoki
    ///     odamga o'zidan kuchliroq rol berish) huquqni «qarzga olish» bo'lardi.
    ///   </item>
    /// </list>
    /// <para>
    /// Bu bugungi xatti-harakatni aynan takrorlaydi (bosh rahbar HR
    /// menejerlarni boshqaradi, boshqa bosh rahbarni emas) va uni egasi
    /// yaratgan rollarga kengaytiradi.
    /// </para>
    /// <para>
    /// <b>Rolsiz nishon — bo'sh ruxsatlar to'plami</b>: unga
    /// <c>users.manage</c> li har kim tega oladi (shartnoma qarori).
    /// Ilgari faqat super administrator tegardi; bunday hisob amalda yo'q.
    /// </para>
    /// </remarks>
    private static Result Allowed(Role? acting, Role? target)
    {
        if (acting is null)
        {
            return UserErrors.RoleOutOfReach;
        }

        if (string.Equals(acting.Code, SystemRoles.SuperAdmin, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        if (target is null)
        {
            return Result.Success();
        }

        if (target.IsProtected || target.Id == acting.Id)
        {
            return UserErrors.RoleOutOfReach;
        }

        // Kodda endi yo'q eski kodlar (masalan `employment.manage`) ikki
        // tomonda ham hisobga olinmaydi — ular hech narsa bermaydi. Rollar
        // muharriri ham shunday solishtiradi (`RoleEditorRules.EnsureTargetWithinOwn`);
        // aks holda bitta qolib ketgan eski kod bosh rahbarni HR hisoblaridan
        // jimgina uzib qo'yardi.
        var actingPermissions = acting.Permissions
            .Where(Permissions.Exists)
            .ToHashSet(StringComparer.Ordinal);
        var targetPermissions = target.Permissions
            .Where(Permissions.Exists)
            .ToHashSet(StringComparer.Ordinal);

        if (!targetPermissions.IsSubsetOf(actingPermissions))
        {
            return UserErrors.RoleOutOfReach;
        }

        // Foydalanuvchilarni boshqaradigan TENG huquqli boshqa rol — «hamkasb»:
        // ular bir-birining parolini tiklab, hisobini egallab olmasligi uchun
        // bunday rol QAT'IY pastda bo'lishi shart (xavfsizlik tekshiruvi, 2026-09-26).
        if (targetPermissions.Contains(Permissions.UsersManage) &&
            actingPermissions.SetEquals(targetPermissions))
        {
            return UserErrors.RoleOutOfReach;
        }

        return Result.Success();
    }
}
