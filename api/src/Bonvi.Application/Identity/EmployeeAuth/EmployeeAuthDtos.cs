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
/// «Parolni almashtirish shart» bayrog'i YO'Q (2026-09-27 dan): parolni
/// faqat HR beradi va xodim uni almashtirmaydi.
/// </para>
/// </remarks>
/// <param name="AccessToken">Har so'rovda <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiresAt">Access token muddati.</param>
/// <param name="RefreshToken">Muddati tugaganda yangi juftlik olish uchun.</param>
public sealed record EmployeeAuthResultDto(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken);
