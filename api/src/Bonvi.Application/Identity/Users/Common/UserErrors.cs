using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Common;

/// <summary>
/// Foydalanuvchilarni boshqarishda uchraydigan xatolar.
/// </summary>
/// <remarks>
/// Xato kodlari BIR joyda to'plangan — <see cref="Bonvi.Application.Catalog.Common.CatalogErrors"/>
/// bilan bir xil sabab: frontend ularni tarjima qiladi, shu sababli kod
/// tasodifan o'zgarib ketmasligi kerak.
/// </remarks>
public static class UserErrors
{
    /// <summary>Foydalanuvchi topilmadi.</summary>
    public static Error NotFound { get; } =
        Error.NotFound("foydalanuvchi.topilmadi", "Foydalanuvchi topilmadi.");

    /// <summary>Bu telefon raqami bilan boshqa foydalanuvchi allaqachon bor.</summary>
    public static Error LoginTaken { get; } =
        Error.Conflict("foydalanuvchi.login_band", "Bu telefon raqami bilan foydalanuvchi allaqachon mavjud.");

    /// <summary>Rol tanlanmagan — u majburiy.</summary>
    /// <remarks>
    /// Kod <c>rol_bosh</c> bo'lib qoldi (matn esa yangilandi): u frontend
    /// sxemasida ham, tarjimalarda ham ishlatiladi va uch rolli modelga
    /// o'tishda ma'nosi o'zgarmadi — rol baribir majburiy.
    /// </remarks>
    public static Error NoRole { get; } =
        Error.Validation("foydalanuvchi.rol_bosh", "Rol tanlanishi kerak.");

    /// <summary>
    /// Oxirgi faol rahbarni (foydalanuvchi boshqaruvi ruxsatiga ega hisobni)
    /// faolsizlantirish yoki uni oddiy HR menejeriga aylantirishga urinish.
    /// </summary>
    /// <remarks>
    /// <see cref="ErrorType.Conflict"/> (409): kiritilgan qiymatning o'zi
    /// to'g'ri, faqat tizimning JORIY holati (boshqa faol rahbar yo'qligi)
    /// bilan to'qnashadi — xuddi <c>CatalogErrors.DepartmentCycle</c> kabi.
    /// </remarks>
    public static Error LastAdminProtected { get; } =
        Error.Conflict(
            "foydalanuvchi.oxirgi_administrator",
            "Tizimda kamida bitta faol administrator qolishi shart.");

    /// <summary>Ko'rsatilgan kodli rol mavjud emas.</summary>
    public static Error RoleNotFound(string code)
        => Error.NotFound("foydalanuvchi.rol_topilmadi", $"«{code}» — bunday rol mavjud emas.");

    /// <summary>
    /// So'rovchining bu rol egasiga tegishga huquqi yo'q.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega bu RUXSAT emas, xato.</b> Bosh rahbarda ham, super
    /// administratorda ham <c>users.manage</c> ruxsati BOR — ikkalasi ham
    /// foydalanuvchi boshqaruvi ekraniga kiradi. Farq «kimga tega olishida»,
    /// ya'ni so'rovning MAZMUNIGA bog'liq: shu sababli uni
    /// <c>RequireAuthorization</c> bilan tekshirib bo'lmaydi va u use-case
    /// qatlamida <see cref="Result"/> sifatida qaytadi.
    /// </para>
    /// <para>
    /// <see cref="ErrorType.Forbidden"/> (403): so'rov to'g'ri va tushunarli,
    /// faqat aynan shu odam uni bajara olmaydi.
    /// </para>
    /// </remarks>
    public static Error RoleOutOfReach { get; } =
        Error.Forbidden(
            "foydalanuvchi.rolga_haqqi_yoq",
            "Bu rol egasini boshqarishga huquqingiz yo'q.");

    /// <summary>Tizimda allaqachon faol super administrator bor.</summary>
    /// <remarks>
    /// <see cref="ErrorType.Conflict"/> (409) — <see cref="LastAdminProtected"/>
    /// bilan bir xil sabab: qiymat to'g'ri, tizimning joriy holati qarshi.
    /// </remarks>
    public static Error SuperAdminExists { get; } =
        Error.Conflict(
            "foydalanuvchi.super_admin_mavjud",
            "Tizimda faol super administrator allaqachon bor — ikkinchisi bo'lishi mumkin emas.");

    /// <summary>Tizimda allaqachon faol bosh rahbar bor.</summary>
    public static Error MainBossExists { get; } =
        Error.Conflict(
            "foydalanuvchi.bosh_rahbar_mavjud",
            "Tizimda faol bosh rahbar allaqachon bor — ikkinchisi bo'lishi mumkin emas.");
}
