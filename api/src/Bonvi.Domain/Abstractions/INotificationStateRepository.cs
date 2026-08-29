using Bonvi.Domain.Notifications;

namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Bildirishnoma holatlari (o'qildi / o'chirildi) uchun saqlash porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega «foydalanuvchining BARCHA holatlari» degan metod yo'q.</b>
/// Jadval yillar davomida o'sadi, bir so'rovda kerak bo'ladigan holatlar
/// esa har doim CHEKLANGAN: faqat shu safar hisoblangan kalitlar
/// (odatda o'nlab). Butun tarixni yuklab, keyin xotirada kesish
/// ro'yxat ekranlaridagi bilan bir xil xato bo'lardi — bugun sezilmaydi,
/// bir kun jimgina sekinlashadi.
/// </para>
/// </remarks>
public interface INotificationStateRepository : IRepository<NotificationState>
{
    /// <summary>
    /// Foydalanuvchining berilgan kalitlar bo'yicha holatlari.
    /// </summary>
    /// <param name="userId">Kimning holatlari.</param>
    /// <param name="keys">
    /// Qaysi kalitlar bo'yicha. Bo'sh ro'yxat — bo'sh javob (bazaga
    /// murojaat ham qilinmaydi: <c>WHERE key IN ()</c> ma'nosiz so'rov).
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<NotificationState>> GetAsync(
        Guid userId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default);
}
