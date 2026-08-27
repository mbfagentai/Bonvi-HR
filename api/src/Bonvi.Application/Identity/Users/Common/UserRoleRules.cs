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
///     Super administrator — hammani; bosh rahbar — faqat HR menejerlarni.
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
    /// Foydalanuvchining rol KODI. Rol berilmagan yoki topilmagan bo'lsa — <c>null</c>.
    /// </summary>
    public static string? CodeOf(User user, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        if (user.RoleId is not { } roleId)
        {
            return null;
        }

        return roles.FirstOrDefault(role => role.Id == roleId)?.Code;
    }

    /// <summary>
    /// So'rovchi shu foydalanuvchiga (uning JORIY roli bo'yicha) tega oladimi.
    /// </summary>
    public static Result EnsureCanActOn(User acting, User target, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(acting);
        ArgumentNullException.ThrowIfNull(target);

        return Allowed(CodeOf(acting, roles), CodeOf(target, roles));
    }

    /// <summary>
    /// So'rovchi shu ROLNI bera oladimi (yaratishda va rol almashtirishda).
    /// </summary>
    public static Result EnsureCanAssign(User acting, Role role, IReadOnlyList<Role> roles)
    {
        ArgumentNullException.ThrowIfNull(acting);
        ArgumentNullException.ThrowIfNull(role);

        return Allowed(CodeOf(acting, roles), role.Code);
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
    /// «Kim kimga tega oladi» matritsasi — qoidaning YAGONA nusxasi.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     <b>super_admin</b> — hammaga: bosh rahbarga ham, HR menejerlarga
    ///     ham. U tizim egasi.
    ///   </item>
    ///   <item>
    ///     <b>main_boss</b> — FAQAT HR menejerlarga. Super administratorga
    ///     ham, boshqa bosh rahbarga ham (ya'ni o'ziga ham, shu ekrandan)
    ///     tegolmaydi. O'z profilini u «Sozlamalar» oynasidan tahrirlaydi —
    ///     bu butunlay boshqa yo'l va unga bu qoida taalluqli emas.
    ///   </item>
    ///   <item>
    ///     <b>Qolganlar</b> (HR menejer, rolsiz hisob) — hech kimga. Ular bu
    ///     endpoint'larga <c>users.manage</c> yo'qligi sababli baribir
    ///     yetmaydi; bu yerdagi rad etish — ikkinchi qatlam.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>Rolsiz NISHON ham himoyalangan.</b> <c>targetRole</c> <c>null</c>
    /// bo'lsa (eski yoki buzilgan yozuv) unga faqat super administrator
    /// tegadi: noma'lum holatda eng ehtiyotkor javob shu.
    /// </para>
    /// </remarks>
    private static Result Allowed(string? actingRole, string? targetRole)
    {
        if (string.Equals(actingRole, SystemRoles.SuperAdmin, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        if (string.Equals(actingRole, SystemRoles.MainBoss, StringComparison.Ordinal) &&
            string.Equals(targetRole, SystemRoles.HrManager, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        return UserErrors.RoleOutOfReach;
    }
}
