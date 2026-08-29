using Bonvi.Domain.Abstractions;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// Haqiqiy tizim vaqti. Testlarda bu sinf soxta (fake) vaqt bilan almashtiriladi.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>Toshkent vaqt mintaqasi (UTC+5, yozgi vaqt yo'q).</summary>
    /// <remarks>
    /// <c>internal</c>, chunki bir joyda undan tashqarida ham kerak
    /// bo'ladi: <c>NotificationReadModel</c> «nomzod qaysi KUNI
    /// qo'shilgan» degan savolga javob berish uchun <c>timestamptz</c>
    /// ustunini shu siljish bilan kunga aylantiradi. Ikkinchi nusxa
    /// yozilsa, ular bir kun ajralib ketardi va yarim tunda hisoblangan
    /// «bugun» ikki xil chiqardi.
    /// </remarks>
    internal static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TashkentOffset).Date);
}
