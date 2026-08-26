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

    /// <summary>Rol tanlanmagan — kamida bittasi majburiy.</summary>
    public static Error NoRoles { get; } =
        Error.Validation("foydalanuvchi.rol_bosh", "Kamida bitta rol tanlanishi kerak.");

    /// <summary>
    /// Oxirgi faol administratorni faolsizlantirish yoki undan admin rolini
    /// olib tashlashga urinish.
    /// </summary>
    /// <remarks>
    /// <see cref="ErrorType.Conflict"/> (409): kiritilgan qiymatning o'zi
    /// to'g'ri, faqat tizimning JORIY holati (boshqa faol admin yo'qligi)
    /// bilan to'qnashadi — xuddi <c>CatalogErrors.DepartmentCycle</c> kabi.
    /// </remarks>
    public static Error LastAdminProtected { get; } =
        Error.Conflict(
            "foydalanuvchi.oxirgi_administrator",
            "Tizimda kamida bitta faol administrator qolishi shart.");

    /// <summary>Ko'rsatilgan kodli rol mavjud emas.</summary>
    public static Error RoleNotFound(string code)
        => Error.NotFound("foydalanuvchi.rol_topilmadi", $"«{code}» — bunday rol mavjud emas.");
}
