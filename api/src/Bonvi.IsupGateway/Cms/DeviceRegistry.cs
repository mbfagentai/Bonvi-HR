using System.Collections.Concurrent;

namespace Bonvi.IsupGateway.Cms;

/// <summary>Hozir ulangan terminalning sessiyasi.</summary>
/// <param name="DeviceId">ISUP identifikatori (sozlamadagi).</param>
/// <param name="LoginId">SDK bergan sessiya raqami — tunnel shu bilan chaqiriladi.</param>
/// <param name="Serial">Terminalning to'liq seriyasi (ro'yxatdan o'tishda aytgani).</param>
/// <param name="Firmware">Proshivka versiyasi.</param>
/// <param name="RemoteAddress">Terminal qayerdan ulangani (NAT ortidagi manzil).</param>
/// <param name="OnlineSince">Ulangan vaqti.</param>
internal sealed record DeviceSession(
    string DeviceId,
    int LoginId,
    string Serial,
    string Firmware,
    string RemoteAddress,
    DateTimeOffset OnlineSince);

/// <summary>
/// Qaysi terminal hozir ulangan — <c>DeviceId → LoginId</c>.
/// </summary>
/// <remarks>
/// SDK qo'ng'iroqlari o'z oqimida keladi, tunnel so'rovlari esa agent
/// halqalaridan — shu sababli lug'at bir vaqtda o'qish/yozishga chidamli.
/// Terminal qayta ulansa, yangi <c>LoginId</c> eskisini almashtiradi;
/// eski sessiyaning kechikib kelgan «uzildi» xabari yangisini O'CHIRMAYDI.
/// </remarks>
internal sealed class DeviceRegistry
{
    private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new(StringComparer.Ordinal);

    public void Online(DeviceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.DeviceId] = session;
    }

    /// <summary>Sessiya raqami bo'yicha uzadi; qaysi terminal uzilganini qaytaradi.</summary>
    public string? OfflineByLogin(int loginId)
    {
        foreach (var (deviceId, session) in _sessions)
        {
            // Faqat AYNAN shu sessiya: kalit bilan birga qiymat ham solishtiriladi.
            if (session.LoginId == loginId
                && ((ICollection<KeyValuePair<string, DeviceSession>>)_sessions).Remove(new(deviceId, session)))
            {
                return deviceId;
            }
        }

        return null;
    }

    public bool TryGet(string deviceId, out DeviceSession session)
        => _sessions.TryGetValue(deviceId, out session!);

    public IReadOnlyList<DeviceSession> Snapshot() => [.. _sessions.Values.OrderBy(s => s.DeviceId, StringComparer.Ordinal)];
}
