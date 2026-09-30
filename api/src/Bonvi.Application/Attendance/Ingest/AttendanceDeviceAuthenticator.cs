using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>
/// So'rovni yuborgan qurilmani aniqlaydi va tasdiqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Tartib ataylab shunday:</b> token → imzo → IP → faollik. Birinchi
/// uchtasining har qanday nosozligi BIR XIL javob beradi
/// (<see cref="IngestErrors.Unauthorized"/>), haqiqiy sabab faqat logga
/// tushadi. Faollik OXIRIDA tekshiriladi: imzo rejimidagi qurilmada token
/// o'zi to'liq tasdiq emas, va «qurilma o'chirilgan» degan javobni imzosi
/// yo'q chaqiruvchi olmasligi kerak.
/// </para>
/// <para>
/// Qurilma hodisa tanasidagi <c>deviceName</c>, <c>ipAddress</c>,
/// <c>macAddress</c> bo'yicha ANIQLANMAYDI — ular hujumchi yozadigan
/// satrlar. Faqat token.
/// </para>
/// <para>
/// Muvaffaqiyatsiz urinish bazaga HECH NARSA yozmaydi (hatto
/// <c>last_seen_at</c> ham): noto'g'ri token bilan kelgan so'rov qaysi
/// qurilmaga tegishli ekani noma'lum, imzosi yo'q so'rov esa qurilmaning
/// kuzatuv ustunlarini yozishga haqli emas.
/// </para>
/// </remarks>
public sealed class AttendanceDeviceAuthenticator
{
    /// <summary>Imzo sarlavhasidagi versiya belgisi.</summary>
    /// <remarks>
    /// Versiya imzo formatiga kiritilgan: algoritm almashgan kuni eski va yangi
    /// qurilmalar bir vaqtda ishlashi uchun (<c>v2=</c> qo'shiladi, <c>v1=</c>
    /// o'chmaydi).
    /// </remarks>
    public const string SignaturePrefix = "v1=";

    private const int SignatureHexLength = 64;

    private readonly IAttendanceDeviceRepository _devices;
    private readonly ISecretProtector _secrets;
    private readonly IAttendanceIngestJournal _journal;

    public AttendanceDeviceAuthenticator(
        IAttendanceDeviceRepository devices,
        ISecretProtector secrets,
        IAttendanceIngestJournal journal)
    {
        _devices = devices;
        _secrets = secrets;
        _journal = journal;
    }

    /// <summary>Qurilmani tasdiqlaydi.</summary>
    /// <param name="token">Yo'ldagi yoki sarlavhadagi token.</param>
    /// <param name="body">So'rov tanasining XOM baytlari — imzo shular ustida.</param>
    /// <param name="signature"><c>X-Bonvi-Signature</c> sarlavhasi.</param>
    /// <param name="sourceIp">So'rov manzili (<c>UseForwardedHeaders</c> dan keyin).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Kuzatiladigan (tracked) qurilma yozuvi yoki xato.</returns>
    public async Task<Result<AttendanceDevice>> AuthenticateAsync(
        string? token,
        byte[] body,
        string? signature,
        string? sourceIp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var presented = token?.Trim();
        if (string.IsNullOrEmpty(presented))
        {
            return Refuse(IngestAuthFailure.MissingToken, null, sourceIp);
        }

        var prefix = AttendanceDeviceToken.PrefixOf(presented);

        var device = await _devices
            .FindByTokenHashAsync(AttendanceDeviceToken.Hash(presented), cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
        {
            return Refuse(IngestAuthFailure.UnknownToken, prefix, sourceIp);
        }

        if (device.AuthMode == DeviceAuthMode.TokenAndHmac)
        {
            var secret = device.HmacSecret is { } stored ? _secrets.Unprotect(stored) : null;
            if (secret is null)
            {
                return Refuse(IngestAuthFailure.SecretUnreadable, prefix, sourceIp);
            }

            if (!SignatureMatches(secret, body, signature))
            {
                return Refuse(IngestAuthFailure.BadSignature, prefix, sourceIp);
            }
        }

        if (!SourceAllowed(device.AllowedIps, sourceIp))
        {
            return Refuse(IngestAuthFailure.SourceNotAllowed, prefix, sourceIp);
        }

        if (!device.IsActive)
        {
            _journal.DeviceInactive(device.Id);
            return IngestErrors.DeviceInactive;
        }

        return device;
    }

    /// <summary>
    /// Qurilmaning IP ro'yxati manzilni qamrab oladimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bo'sh ro'yxat ⇒ cheklov YO'Q: terminallar bugun o'zgaruvchan uy
    /// internetida turibdi (<c>AttendanceDevice.AllowedIps</c> izohi).
    /// </para>
    /// <para>
    /// Ro'yxat bor-u, manzil yo'q yoki o'qilmaydi ⇒ RAD. Yozuvdagi o'qib
    /// bo'lmaydigan element hech narsani qamramaydi — ya'ni xato yozilgan
    /// ro'yxat eshikni OCHMAYDI, yopadi. Bu to'g'ri tomonga adashish.
    /// </para>
    /// <para>
    /// <c>System.Net.IPNetwork</c> ATAYLAB ishlatilmagan: u
    /// <c>10.0.0.5/8</c> kabi host bitlari to'ldirilgan yozuvni rad etadi,
    /// odam esa aynan shunday yozadi («mana shu qurilmaning manzili/24»).
    /// Bu yerda niqob qo'lda qo'llanadi va ikkala yozuv ham ishlaydi.
    /// </para>
    /// </remarks>
    /// <param name="allowedIps">Vergul bilan ajratilgan CIDR yoki yakka manzillar.</param>
    /// <param name="sourceIp">So'rov manzili.</param>
    /// <returns>Ruxsat etilganmi.</returns>
    public static bool SourceAllowed(string? allowedIps, string? sourceIp)
    {
        if (string.IsNullOrWhiteSpace(allowedIps))
        {
            return true;
        }

        if (!IPAddress.TryParse(sourceIp, out var source))
        {
            return false;
        }

        if (source.IsIPv4MappedToIPv6)
        {
            source = source.MapToIPv4();
        }

        foreach (var entry in allowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Covers(entry, source))
            {
                return true;
            }
        }

        return false;
    }

    private Error Refuse(IngestAuthFailure reason, string? tokenPrefix, string? sourceIp)
    {
        _journal.AuthenticationFailed(reason, tokenPrefix, sourceIp);
        return IngestErrors.Unauthorized;
    }

    /// <summary>
    /// <c>v1=&lt;hex HMAC-SHA256(tana, sir)&gt;</c> mos keladimi.
    /// </summary>
    /// <remarks>
    /// Solishtirish <see cref="CryptographicOperations.FixedTimeEquals"/>
    /// bilan: oddiy <c>SequenceEqual</c> birinchi farqli baytda to'xtaydi va
    /// javob vaqti imzoni bayt-bayt tanlash imkonini beradi.
    /// </remarks>
    private static bool SignatureMatches(string secret, byte[] body, string? signature)
    {
        var header = signature?.Trim();
        if (header is null
            || !header.StartsWith(SignaturePrefix, StringComparison.Ordinal)
            || header.Length != SignaturePrefix.Length + SignatureHexLength)
        {
            return false;
        }

        byte[] presented;
        try
        {
            presented = Convert.FromHexString(header.AsSpan(SignaturePrefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);

        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    /// <summary>Bitta yozuv (<c>10.0.0.0/8</c>, <c>10.0.0.5</c>, <c>2001:db8::/32</c>) manzilni qamraydimi.</summary>
    private static bool Covers(string entry, IPAddress source)
    {
        var slash = entry.IndexOf('/', StringComparison.Ordinal);
        var addressPart = slash < 0 ? entry : entry[..slash];

        if (!IPAddress.TryParse(addressPart, out var network))
        {
            return false;
        }

        if (network.IsIPv4MappedToIPv6)
        {
            network = network.MapToIPv4();
        }

        if (network.AddressFamily != source.AddressFamily)
        {
            return false;
        }

        var bits = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefixLength = bits;

        if (slash >= 0
            && (!int.TryParse(entry.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out prefixLength) || prefixLength < 0 || prefixLength > bits))
        {
            return false;
        }

        var networkBytes = network.GetAddressBytes();
        var sourceBytes = source.GetAddressBytes();

        var fullBytes = prefixLength / 8;
        if (!networkBytes.AsSpan(0, fullBytes).SequenceEqual(sourceBytes.AsSpan(0, fullBytes)))
        {
            return false;
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[fullBytes] & mask) == (sourceBytes[fullBytes] & mask);
    }
}
