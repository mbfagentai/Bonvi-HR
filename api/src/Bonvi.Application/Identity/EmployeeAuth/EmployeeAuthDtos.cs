namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>
/// Xodim ilovasiga muvaffaqiyatli kirish natijasi.
/// </summary>
/// <remarks>
/// <para>
/// <c>AuthResultDto</c> dan ATAYLAB alohida va undagi <c>User</c> maydoniga
/// mos keladigan hech narsa yo'q: xodimda rol ham, ruxsat ham yo'q, o'z
/// ma'lumotini esa u <c>GET /api/me</c> dan oladi. Kirish javobiga profil
/// qo'shilsa, u ikkita joyda ikki xil eskirardi.
/// </para>
/// <para>
/// <see cref="MustChangePassword"/> kirish javobida turadi, chunki ilova
/// undan KEYIN qaysi ekranni ochishini shu yerda hal qiladi — birinchi
/// so'rovni yuborib, 403 olib, keyin yo'naltirish ortiqcha aylanish
/// bo'lardi.
/// </para>
/// </remarks>
/// <param name="AccessToken">Har so'rovda <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiresAt">Access token muddati.</param>
/// <param name="RefreshToken">Muddati tugaganda yangi juftlik olish uchun.</param>
/// <param name="MustChangePassword">Bir martalik parolni almashtirish MAJBURIYmi.</param>
public sealed record EmployeeAuthResultDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    bool MustChangePassword);
