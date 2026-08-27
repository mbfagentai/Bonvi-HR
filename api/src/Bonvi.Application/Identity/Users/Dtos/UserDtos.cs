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
/// <param name="PhotoKey">
/// Hisob rasmining saqlash kaliti; rasm yo'q bo'lsa <c>null</c>.
/// Rasmning O'ZI emas, kaliti beriladi — xuddi <c>CurrentUserDto</c> va
/// xodimlar ro'yxatidagi kabi: mijoz shu kalitni kesh belgisi sifatida
/// ishlatadi va suratni alohida so'rov bilan oladi. Kalitsiz ro'yxat
/// har bir qator uchun keraksiz 404 so'rov yuborardi.
/// </param>
/// <param name="RoleCode">
/// Rol kodi (masalan <c>"hr_manager"</c>). Rol berilmagan yozuvda —
/// <c>null</c>; amalda bunday hisob yo'q, lekin eski ma'lumot uchun
/// shartnoma buni ko'tara oladi.
/// </param>
/// <param name="RoleName">
/// Rol nomi — bazadagi (o'zbekcha) matn. Interfeys uni KO'RSATISHDA
/// ishlatmaydi: 4 tilli tarjima <c>roles.&lt;code&gt;</c> kaliti orqali
/// olinadi. Baribir qaytariladi — API'ni brauzerdan yoki Swagger'dan
/// ko'rayotgan odam kodning nima ekanini darhol tushunishi uchun.
/// </param>
public sealed record UserSummaryDto(
    Guid Id,
    string Login,
    string FullName,
    string ShortName,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil,
    string? PhotoKey,
    string? RoleCode,
    string? RoleName);

/// <summary>Yuklangan foydalanuvchi rasmining saqlash kaliti.</summary>
/// <param name="PhotoKey">Yangi kalit — mijoz uni kesh belgisi sifatida ishlatadi.</param>
public sealed record UserPhotoDto(string PhotoKey);
