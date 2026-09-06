using Bonvi.Domain.Abstractions;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// Haqiqiy tizim vaqti. Testlarda bu sinf soxta (fake) vaqt bilan almashtiriladi.
/// </summary>
/// <remarks>
/// Siljishning O'ZI bu yerda emas, <see cref="BonviTime"/> da — sabab
/// o'sha yerda. Ilgari u shu sinfda <c>internal</c> bo'lib turgan va
/// Application qatlami unga yeta olmagani uchun ikkinchi nusxa paydo
/// bo'lgan edi.
/// </remarks>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateTimeOffset.UtcNow.TashkentDate();
}
