using Bonvi.Domain.Abstractions;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// Haqiqiy tizim vaqti. Testlarda bu sinf soxta (fake) vaqt bilan almashtiriladi.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>Toshkent vaqt mintaqasi (UTC+5, yozgi vaqt yo'q).</summary>
    private static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TashkentOffset).Date);
}
