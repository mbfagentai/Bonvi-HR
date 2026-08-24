namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Vaqt manbai. Domen qoidalari <c>DateTime.Now</c> ni TO'G'RIDAN-TO'G'RI chaqirmaydi.
/// </summary>
/// <remarks>
/// Nega kerak: "sinov muddati tugashiga 7 kun qoldi" qoidasini test qilish uchun
/// vaqtni boshqarish kerak. <c>DateTime.Now</c> bilan bunday testni yozib bo'lmaydi.
/// </remarks>
public interface IClock
{
    /// <summary>Hozirgi vaqt (UTC).</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Bugungi sana — Toshkent vaqti bo'yicha (UTC+5).</summary>
    DateOnly Today { get; }
}
