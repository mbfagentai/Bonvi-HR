namespace Bonvi.Application.Identity.Users.Dtos;

/// <summary>
/// Foydalanuvchi boshqaruvi ro'yxati/kartochkasi uchun ko'rinish.
/// </summary>
/// <remarks>
/// Parol va uning xeshi BU YERDA ATAYLAB YO'Q — ular hech qachon, hech
/// qanday javobda qaytmaydi. DTO shu qat'iyatning o'zi bilan kafolat beradi:
/// bu maydon umuman mavjud emas, uni "unutib qoldirib yuborish" mumkin emas.
/// </remarks>
/// <param name="Id">Foydalanuvchi identifikatori.</param>
/// <param name="Login">Telefon raqami (+998…).</param>
/// <param name="FullName">To'liq ism-familiya.</param>
/// <param name="ShortName">Qisqa ko'rinish: «Мамажонов Б.Ф.».</param>
/// <param name="IsActive">Hisob faolmi.</param>
/// <param name="LastLoginAt">Oxirgi muvaffaqiyatli kirish vaqti.</param>
/// <param name="LockedUntil">Ketma-ket xato urinishlar sababli bloklangan bo'lsa — shu vaqtgacha.</param>
/// <param name="RoleCodes">Rol kodlari (masalan <c>["admin"]</c>).</param>
/// <param name="RoleNames">Rol nomlari — interfeysda ko'rsatish uchun.</param>
public sealed record UserSummaryDto(
    Guid Id,
    string Login,
    string FullName,
    string ShortName,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> RoleNames);
