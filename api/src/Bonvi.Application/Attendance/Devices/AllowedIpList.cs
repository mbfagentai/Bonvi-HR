using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Bonvi.Application.Attendance.Devices;

/// <summary>
/// Qurilmaning IP ro'yxati (<c>allowed_ips</c>) to'g'ri yozilganmi — SAQLASHDAN oldin.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega saqlashda tekshiriladi.</b> Qabul yo'li
/// (<c>AttendanceDeviceAuthenticator.SourceAllowed</c>) o'qib bo'lmaydigan
/// elementni «hech narsani qamramaydi» deb oladi — bu u yerda to'g'ri
/// tomonga adashish. Lekin ro'yxatdagi YAGONA element xato yozilsa, qurilma
/// jimgina qulflanadi: terminal yuborishda davom etadi, hammasi 401, va HR
/// buni faqat tabel bo'shab qolganda sezadi. Shu sababli xato shu yerda,
/// operator formani yopmasidan oldin aytiladi.
/// </para>
/// <para>
/// ⚠️ Qoida qabul yo'lidagi o'qish bilan AYNAN bir xil bo'lishi SHART:
/// manzil <see cref="IPAddress.TryParse(string?, out IPAddress?)"/> bilan,
/// prefiks — ishorasiz butun son, 0 dan manzil bitlari sonigacha
/// (IPv4 — 32, IPv6 — 128). Bu yerda qabul qilingan har bir yozuvni qabul
/// yo'li ham tushunadi; birortasi o'zgarsa, ikkinchisi ham o'zgaradi.
/// </para>
/// <para>
/// Host bitlari to'ldirilgan yozuv (<c>10.0.0.5/8</c>) RUXSAT etiladi —
/// odam aynan shunday yozadi va qabul yo'li niqobni o'zi qo'llaydi.
/// </para>
/// </remarks>
public static class AllowedIpList
{
    /// <summary>Ro'yxat bo'sh yoki har bir elementi o'qiladimi.</summary>
    /// <param name="allowedIps">Vergul bilan ajratilgan manzillar yoki CIDR lar.</param>
    /// <returns>Bo'sh ro'yxat — to'g'ri (cheklov yo'q degani).</returns>
    public static bool IsValid(string? allowedIps)
    {
        if (string.IsNullOrWhiteSpace(allowedIps))
        {
            return true;
        }

        var entries = allowedIps.Split(',', StringSplitOptions.TrimEntries);

        // Bo'sh element («10.0.0.1,,10.0.0.2» yoki oxirgi vergul) ham xato: qabul
        // yo'li uni tashlab yuboradi, lekin bu deyarli har doim o'chib qolgan manzil.
        return Array.TrueForAll(entries, IsValidEntry);
    }

    private static bool IsValidEntry(string entry)
    {
        if (entry.Length == 0)
        {
            return false;
        }

        var slash = entry.IndexOf('/', StringComparison.Ordinal);
        var addressPart = slash < 0 ? entry : entry[..slash];

        if (!IPAddress.TryParse(addressPart, out var address))
        {
            return false;
        }

        if (slash < 0)
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bits = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

        return int.TryParse(entry.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var prefix)
            && prefix <= bits;
    }
}
