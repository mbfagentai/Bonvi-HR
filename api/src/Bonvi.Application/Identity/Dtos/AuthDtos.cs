namespace Bonvi.Application.Identity.Dtos;

/// <summary>Tizimga kirgan foydalanuvchi haqidagi ma'lumot.</summary>
/// <param name="Id">Foydalanuvchi identifikatori.</param>
/// <param name="Login">Telefon raqami (+998…).</param>
/// <param name="FullName">To'liq ism-familiya.</param>
/// <param name="ShortName">Qisqa ko'rinish: «Мамажонов Б.Ф.».</param>
/// <param name="EmployeeId">Bog'langan xodim, agar mavjud bo'lsa.</param>
/// <param name="PhotoKey">
/// Hisob rasmining saqlash kaliti; rasm yo'q bo'lsa <c>null</c>.
/// Rasmning O'ZI emas, kaliti beriladi: mijoz shu kalitni kesh belgisi
/// sifatida ishlatadi va suratni alohida so'rov bilan oladi.
/// </param>
/// <param name="Roles">Rol kodlari.</param>
/// <param name="Permissions">Barcha ruxsatlar (rollar orqali yig'ilgan).</param>
public sealed record CurrentUserDto(
    Guid Id,
    string Login,
    string FullName,
    string ShortName,
    Guid? EmployeeId,
    string? PhotoKey,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>Muvaffaqiyatli autentifikatsiya natijasi.</summary>
/// <param name="AccessToken">Har bir so'rovda `Authorization: Bearer` sifatida yuboriladi.</param>
/// <param name="ExpiresAt">Access token muddati.</param>
/// <param name="RefreshToken">Access token tugaganda yangisini olish uchun.</param>
/// <param name="User">Joriy foydalanuvchi.</param>
public sealed record AuthResultDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    CurrentUserDto User);

/// <summary>Yuklangan hisob rasmining saqlash kaliti.</summary>
/// <param name="PhotoKey">Yangi kalit — mijoz uni kesh belgisi sifatida ishlatadi.</param>
public sealed record AccountPhotoDto(string PhotoKey);
