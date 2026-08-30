using System.Globalization;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// Xaritadagi nuqta — «kenglik,uzunlik».
/// </summary>
/// <remarks>
/// <para>
/// Nomzod manzilni MATN bilan emas, xaritadagi nuqta bilan tushunadi:
/// Telegram uni ochib navigatorga uzatadi. Shu sababli kontakt
/// matnidan keyin alohida nuqta yuboriladi.
/// </para>
/// <para>
/// <b>Nega bitta matn maydoni, ikkita son emas.</b> HR nuqtani Google
/// Maps'dan NUSXA OLADI va u yerda u aynan shu ko'rinishda —
/// «40.505852, 70.930622». Ikkita alohida maydon HR ni sonni qo'lda
/// bo'lishga majbur qilardi va bu xatoning eng oson turi.
/// </para>
/// </remarks>
/// <param name="Latitude">Kenglik, −90…90.</param>
/// <param name="Longitude">Uzunlik, −180…180.</param>
public readonly record struct MapPoint(double Latitude, double Longitude)
{
    /// <summary>
    /// Matnni nuqtaga ajratadi. Tushunarsiz bo'lsa <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CultureInfo.InvariantCulture"/> SHART: serverning
    /// mahalliy sozlamasida kasr ajratgichi vergul bo'lsa
    /// «40.505852» o'qilmay qolardi va xarita jimgina yo'qolardi.
    /// </para>
    /// <para>
    /// Chegaralar ham tekshiriladi. Sozlamada shablon bor
    /// (<c>SystemSettingKeys.LocationPattern</c>), lekin u faqat
    /// SHAKLNI qaraydi va qiymat `.env` dan ham kelishi mumkin — u
    /// yerda esa hech qanday tekshiruv yo'q. Noto'g'ri nuqta bilan
    /// Telegram butun xabarni rad etardi.
    /// </para>
    /// </remarks>
    public static MapPoint? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(',');

        if (parts.Length != 2
            || !double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            return null;
        }

        return new MapPoint(latitude, longitude);
    }
}
