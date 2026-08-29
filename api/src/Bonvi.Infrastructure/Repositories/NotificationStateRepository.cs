using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Notifications;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="INotificationStateRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// <b>Nega <c>AsNoTracking()</c> YO'Q</b> (o'qish modellaridan farqli).
/// Bu port ikki tomonlama ishlatiladi: ro'yxat uni faqat o'qish uchun
/// chaqiradi, «o'qildi» va «o'chirish» amallari esa qaytgan yozuvni
/// O'ZGARTIRADI va saqlaydi. Kuzatuvsiz qaytgan obyektdagi o'zgarish
/// <c>SaveChanges</c> ga umuman yetib bormasdi — amal muvaffaqiyatli
/// tugagandek ko'rinib, bazada hech narsa o'zgarmasdi.
/// </remarks>
public sealed class NotificationStateRepository : Repository<NotificationState>, INotificationStateRepository
{
    public NotificationStateRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<NotificationState>> GetAsync(
        Guid userId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            // Bo'sh ro'yxatda bazaga umuman murojaat qilinmaydi:
            // `WHERE key = ANY('{}')` hech qachon qator qaytarmaydi, lekin
            // baribir so'rov bo'lib ketardi.
            return [];
        }

        // Kalitlar massiv bo'lib beriladi — Npgsql buni `key = ANY(@p)`
        // ga tarjima qiladi, ya'ni kalitlar soni o'zgarganda so'rov matni
        // O'ZGARMAYDI va tayyorlangan reja qayta ishlatiladi. `Contains`
        // ni ro'yxat bilan chaqirish har xil uzunlikda yangi so'rov
        // yasardi.
        var array = keys as string[] ?? [.. keys];

        return await Set
            .Where(state => state.UserId == userId && array.Contains(state.Key))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
