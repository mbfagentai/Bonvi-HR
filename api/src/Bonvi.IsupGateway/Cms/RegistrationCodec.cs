using System.Text;
using System.Text.Json;
using Bonvi.IsupGateway.Native;

namespace Bonvi.IsupGateway.Cms;

/// <summary>Terminal ro'yxatdan o'tishda o'zi haqida aytgani.</summary>
internal sealed record RegistrationInfo(
    string DeviceId,
    string Serial,
    string Firmware,
    string RemoteAddress,
    string ProtocolVersion,
    byte[] DeviceIdBytes,
    byte[] SessionKey);

/// <summary>
/// SDK qo'ng'irog'ining buferlarini o'qish va yozish — native chaqiruvsiz,
/// shu sababli sinovda oddiy xotira bilan tekshiriladi.
/// </summary>
/// <remarks>
/// ⚠️ Har yozish bufer sig'imi (<c>dwInLen</c>) ichida va doimo NUL bilan
/// tugaydi: SDK satrni C uslubida o'qiydi, chegaradan oshgan bayt esa
/// SDK'ning boshqa ma'lumotini buzadi.
/// </remarks>
internal static unsafe class RegistrationCodec
{
    /// <summary>Chiqish buferidan <c>NET_EHOME_DEV_REG_INFO_V12</c> ni o'qiydi.</summary>
    /// <remarks>
    /// <paramref name="length"/> berilgan bo'lsa, undan ortiq o'qilmaydi (qolgani nol).
    /// Berilmasa (<c>0</c>) — Hikvision namunasidagidek to'liq tuzilma o'qiladi.
    /// </remarks>
    public static RegistrationInfo Read(IntPtr buffer, uint length)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(buffer, IntPtr.Zero);

        var size = (uint)sizeof(NetEhomeDevRegInfoV12);
        var copy = length == 0 ? size : Math.Min(length, size);

        // Mahalliy o'zgaruvchi — stekda, ko'chmaydi: `fixed` buferlarga
        // to'g'ridan-to'g'ri ko'rsatkich bilan murojaat qilish mumkin.
        NetEhomeDevRegInfoV12 info = default;
        Buffer.MemoryCopy((void*)buffer, &info, size, copy);

        var reg = &info.struRegInfo;

        var fullSerial = Text(info.byDeviceFullSerial, 64);
        var shortSerial = Text(reg->sDeviceSerial, 12);

        return new RegistrationInfo(
            DeviceId: Text(reg->byDeviceID, IsupNative.MaxDeviceIdLength),
            Serial: fullSerial.Length > 0 ? fullSerial : shortSerial,
            Firmware: Text(reg->byFirmwareVersion, 24),
            RemoteAddress: Address(&reg->struDevAdd),
            ProtocolVersion: Text(reg->byDevProtocolVersion, 6),
            DeviceIdBytes: new ReadOnlySpan<byte>(reg->byDeviceID, IsupNative.MaxDeviceIdLength).ToArray(),
            SessionKey: new ReadOnlySpan<byte>(reg->bySessionKey, IsupNative.MasterKeyLength).ToArray());
    }

    /// <summary>ISUP 5.0 kalitini kirish buferiga yozadi (<c>ENUM_DEV_AUTH</c>).</summary>
    /// <returns><c>false</c> — bufer yo'q yoki sig'masa (terminal rad etiladi).</returns>
    public static bool WriteKey(IntPtr buffer, uint capacity, string key)
        => WriteText(buffer, capacity, Encoding.ASCII.GetBytes(key));

    /// <summary>Satrni (UTF-8) kirish buferiga yozadi.</summary>
    public static bool WriteText(IntPtr buffer, uint capacity, string text)
        => WriteText(buffer, capacity, Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// Qayta yo'naltirish (DAS) so'roviga javob: terminal shu manzilga ulanishda davom etadi.
    /// </summary>
    /// <remarks>
    /// Hikvision namunasidagi shakl, lekin TO'G'RI JSON (namunada
    /// <c>"UdpPort":</c> qiymatsiz qolgan).
    /// </remarks>
    public static string DasReply(string address, string? domain, int port)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("Type", "DAS");
            json.WriteStartObject("DasInfo");
            json.WriteString("Address", address);
            json.WriteString("Domain", domain ?? string.Empty);
            json.WriteString("ServerID", string.Empty);
            json.WriteNumber("Port", port);
            json.WriteNumber("UdpPort", port);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// <c>ENUM_DEV_ON</c> javobidagi yurak urishi sozlamasini o'rnatadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SDK buferni o'z sukut qiymatlari bilan TO'LDIRIB beradi — shu sababli
    /// yangi tuzilma yozilmaydi: o'qiladi, faqat yurak urishi o'zgartiriladi,
    /// qaytarib yoziladi. Signal, NTP va rasm serverlari TEGILMAYDI —
    /// shlyuz terminalga hech qanday sozlama yubormaydi (rasm serveri turi
    /// 0 = Tomcat, ya'ni oddiy HTTP yuklab olish, aynan bizga keragi).
    /// </para>
    /// <para>
    /// Ikkala qiymat ham <c>0</c> bo'lsa (SDK sukuti) — umuman yozilmaydi.
    /// Bufer tuzilmadan kichik bo'lsa — ham.
    /// </para>
    /// </remarks>
    /// <returns>Bufer o'zgartirildimi.</returns>
    public static bool ApplyKeepAlive(IntPtr buffer, uint capacity, int keepAliveSeconds, int timeoutCount)
    {
        if (buffer == IntPtr.Zero || (keepAliveSeconds <= 0 && timeoutCount <= 0))
        {
            return false;
        }

        var size = (uint)sizeof(NetEhomeServerInfoV50);

        if (capacity != 0 && capacity < size)
        {
            return false;
        }

        var info = (NetEhomeServerInfoV50*)buffer;

        if (keepAliveSeconds > 0)
        {
            info->dwKeepAliveSec = (uint)keepAliveSeconds;
        }

        if (timeoutCount > 0)
        {
            info->dwTimeOutCount = (uint)timeoutCount;
        }

        return true;
    }

    /// <summary>IP manzilni <c>NET_EHOME_IPADDRESS</c> ga yozadi (bufer tozalanadi, NUL qoladi).</summary>
    public static void WriteAddress(NetEhomeIpAddress* address, string ip, int port)
    {
        var bytes = Encoding.ASCII.GetBytes(ip);
        var span = new Span<byte>(address->szIP, 128);

        span.Clear();
        bytes.AsSpan(0, Math.Min(bytes.Length, 127)).CopyTo(span);
        address->wPort = (ushort)port;
    }

    private static bool WriteText(IntPtr buffer, uint capacity, byte[] bytes)
    {
        if (buffer == IntPtr.Zero || bytes.Length == 0)
        {
            return false;
        }

        // Sig'im noma'lum (0) bo'lsa — matn + NUL. Hikvision namunasi ham shunday yozadi.
        var room = capacity == 0 ? bytes.Length + 1 : (int)Math.Min(capacity, int.MaxValue);

        if (bytes.Length >= room)
        {
            return false;
        }

        var target = new Span<byte>((void*)buffer, bytes.Length + 1);
        bytes.CopyTo(target);
        target[bytes.Length] = 0;

        return true;
    }

    private static string Address(NetEhomeIpAddress* address)
    {
        var ip = Text(address->szIP, 128);
        return address->wPort > 0 ? $"{ip}:{address->wPort}" : ip;
    }

    private static string Text(byte* source, int length)
    {
        var span = new ReadOnlySpan<byte>(source, length);
        var end = span.IndexOf((byte)0);

        if (end >= 0)
        {
            span = span[..end];
        }

        return Encoding.UTF8.GetString(span).Trim();
    }
}
