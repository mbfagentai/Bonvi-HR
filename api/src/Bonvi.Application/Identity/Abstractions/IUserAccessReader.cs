namespace Bonvi.Application.Identity.Abstractions;

/// <summary>
/// Hisobning JORIY kirish huquqi: rol kodlari va ruxsatlari — bazadagi holat bo'yicha.
/// </summary>
/// <param name="RoleCodes">Rol kodlari (amalda bitta).</param>
/// <param name="Permissions">Rol(lar) orqali yig'ilgan ruxsatlar.</param>
public sealed record UserAccess(IReadOnlyList<string> RoleCodes, IReadOnlySet<string> Permissions);

/// <summary>
/// Har bir autentifikatsiyalangan so'rovda tokendagi rol va ruxsatlarni
/// bazadagi JORIY holat bilan almashtirish uchun o'quvchi port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kerak.</b> Ruxsatlar access token ichida yuradi va token
/// 15 daqiqa yashaydi. Ilgari rolni tahrirlash yoki hisobni faolsizlantirish
/// allaqachon berilgan tokenga shu muddat tugagunicha yetib bormasdi.
/// Egasining qarori: rolga kiritilgan har bir o'zgarish (ruxsat berish ham,
/// olish ham) deploy va qayta kirishsiz, KEYINGI so'rovdayoq kuchga kirsin.
/// </para>
/// <para>
/// <b>Kesh — amalga oshirishning ichida.</b> Natija qisqa muddat
/// (≤ 30 soniya) xotirada saqlanadi va rol yoki hisob o'zgargan zahoti
/// eskiradi — shunda har so'rov bazaga bormaydi, eski natija esa
/// qolib ketmaydi. Chaqiruvchi bu haqda hech narsa bilmaydi.
/// </para>
/// </remarks>
public interface IUserAccessReader
{
    /// <summary>
    /// Hisobning joriy rol va ruxsatlari.
    /// </summary>
    /// <returns>
    /// Hisob yo'q, o'chirilgan yoki faolsiz bo'lsa — <c>null</c>: bunday
    /// tokenga autentifikatsiya berilmaydi.
    /// </returns>
    Task<UserAccess?> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
