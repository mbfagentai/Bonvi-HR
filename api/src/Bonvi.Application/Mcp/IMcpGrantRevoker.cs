namespace Bonvi.Application.Mcp;

/// <summary>
/// Foydalanuvchining barcha AI (MCP) ulanishlarini bekor qilish
/// (<c>docs/SPEC-mcp.md</c> §5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kerak.</b> Parol sizib chiqqanda odatiy chora — parolni tiklash
/// yoki «barcha sessiyalardan chiqish». Ular faqat veb-sessiyalarni
/// (<c>refresh_tokens</c>) yopardi; sizib chiqqan parol bilan ochilgan AI
/// ulanishi esa 90 kungacha ishlayverardi. Endi bu amallar AI ulanishlarini
/// ham yopadi. Hisob faolsizlantirilganda ham — aks holda u qayta
/// faollashtirilganda eski ulanish yangi roziliksiz «tirilib» qolardi.
/// </para>
/// <para>
/// Yozuvlar O'CHIRILMAYDI — «Tizim sozlamalari» dagi ro'yxatda «uzilgan»
/// bo'lib qoladi.
/// </para>
/// </remarks>
public interface IMcpGrantRevoker
{
    /// <returns>Bekor qilingan ulanishlar soni.</returns>
    Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
