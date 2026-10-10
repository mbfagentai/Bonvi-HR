using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Bonvi.IsupGateway.Cms;
using Bonvi.IsupGateway.Native;

namespace Bonvi.IsupGateway.Tests;

/// <summary>SDK qo'ng'irog'i buferlarini o'qish/yozish — oddiy xotirada.</summary>
public sealed unsafe class RegistrationCodecTests
{
    [Fact]
    public void Royxat_buferidan_terminal_malumotlari_oqiladi()
    {
        var info = default(NetEhomeDevRegInfoV12);
        Put(info.struRegInfo.byDeviceID, "zavod-1");
        Put(info.struRegInfo.byFirmwareVersion, "V3.7.0 build 231019");
        Put(info.struRegInfo.sDeviceSerial, "J80796745");
        Put(info.struRegInfo.byDevProtocolVersion, "5.0");
        Put(info.byDeviceFullSerial, "DS-K1T341AMF20231019V030700ENJ80796745");
        Put(info.struRegInfo.struDevAdd.szIP, "10.0.0.7");
        info.struRegInfo.struDevAdd.wPort = 34567;

        for (var i = 0; i < IsupNative.MasterKeyLength; i++)
        {
            info.struRegInfo.bySessionKey[i] = (byte)(i + 1);
        }

        var read = RegistrationCodec.Read((IntPtr)(&info), (uint)sizeof(NetEhomeDevRegInfoV12));

        read.DeviceId.ShouldBe("zavod-1");
        read.Firmware.ShouldBe("V3.7.0 build 231019");
        read.Serial.ShouldBe("DS-K1T341AMF20231019V030700ENJ80796745");
        read.RemoteAddress.ShouldBe("10.0.0.7:34567");
        read.ProtocolVersion.ShouldBe("5.0");
        read.SessionKey.ShouldBe([.. Enumerable.Range(1, 16).Select(i => (byte)i)]);
        read.DeviceIdBytes.Length.ShouldBe(IsupNative.MaxDeviceIdLength);
    }

    [Fact]
    public void Toliq_seriya_yoq_bolsa_qisqasi_olinadi()
    {
        var info = default(NetEhomeDevRegInfoV12);
        Put(info.struRegInfo.byDeviceID, "ofis-1");
        Put(info.struRegInfo.sDeviceSerial, "J80796745");

        RegistrationCodec.Read((IntPtr)(&info), 0).Serial.ShouldBe("J80796745");
    }

    [Fact]
    public void Qisqa_buferdan_ortigi_oqilmaydi()
    {
        // Faqat V12 ning birinchi qismi (V10) berilgan — qolgani (to'liq seriya) nol.
        var info = default(NetEhomeDevRegInfoV12);
        Put(info.struRegInfo.byDeviceID, "ofis-1");
        Put(info.struRegInfo.sDeviceSerial, "J80796745");
        Put(info.byDeviceFullSerial, "SHU-OQILMASLIGI-KERAK");

        var read = RegistrationCodec.Read((IntPtr)(&info), (uint)sizeof(NetEhomeDevRegInfo));

        read.Serial.ShouldBe("J80796745");
    }

    [Fact]
    public void Kalit_NUL_bilan_yoziladi()
    {
        var buffer = stackalloc byte[32];
        new Span<byte>(buffer, 32).Fill(0xAA);

        RegistrationCodec.WriteKey((IntPtr)buffer, 32, "Kalit-1234567890").ShouldBeTrue();

        Encoding.ASCII.GetString(buffer, 16).ShouldBe("Kalit-1234567890");
        buffer[16].ShouldBe((byte)0);
        buffer[17].ShouldBe((byte)0xAA, "sig'imdan tashqari bayt tegilmaydi");
    }

    [Fact]
    public void Kalit_sigmasa_yozilmaydi()
    {
        var buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0xAA);

        // 16 belgi + NUL = 17 > 16
        RegistrationCodec.WriteKey((IntPtr)buffer, 16, "Kalit-1234567890").ShouldBeFalse();

        new Span<byte>(buffer, 16).ToArray().ShouldAllBe(b => b == 0xAA);
    }

    [Fact]
    public void Bufer_yoq_bolsa_yozilmaydi()
        => RegistrationCodec.WriteKey(IntPtr.Zero, 64, "Kalit-12345").ShouldBeFalse();

    [Fact]
    public void DAS_javobi_togri_JSON()
    {
        var reply = RegistrationCodec.DasReply("189.74.98.57", "hr.bonvi.uz", 7660);

        using var json = JsonDocument.Parse(reply);
        json.RootElement.GetProperty("Type").GetString().ShouldBe("DAS");

        var das = json.RootElement.GetProperty("DasInfo");
        das.GetProperty("Address").GetString().ShouldBe("189.74.98.57");
        das.GetProperty("Domain").GetString().ShouldBe("hr.bonvi.uz");
        das.GetProperty("Port").GetInt32().ShouldBe(7660);
        das.GetProperty("UdpPort").GetInt32().ShouldBe(7660);
    }

    [Fact]
    public void Yurak_urishi_faqat_shu_maydonlarni_ozgartiradi()
    {
        var info = default(NetEhomeServerInfoV50);
        info.dwKeepAliveSec = 15;
        info.dwTimeOutCount = 6;
        info.dwPicServerType = 0;
        Put(info.struPictureSever.szIP, "");

        var before = Bytes(&info);

        RegistrationCodec.ApplyKeepAlive((IntPtr)(&info), (uint)sizeof(NetEhomeServerInfoV50), 30, 4).ShouldBeTrue();

        info.dwKeepAliveSec.ShouldBe(30u);
        info.dwTimeOutCount.ShouldBe(4u);

        // Qolgan hamma bayt (signal, NTP, rasm serveri...) o'zgarmagan.
        var after = Bytes(&info);
        after.AsSpan(12).SequenceEqual(before.AsSpan(12)).ShouldBeTrue();
    }

    [Fact]
    public void Yurak_urishi_sozlanmagan_bolsa_bufer_tegilmaydi()
    {
        var info = default(NetEhomeServerInfoV50);
        info.dwKeepAliveSec = 15;

        RegistrationCodec.ApplyKeepAlive((IntPtr)(&info), 0, 0, 0).ShouldBeFalse();
        info.dwKeepAliveSec.ShouldBe(15u);
    }

    [Fact]
    public void Kichik_buferga_yurak_urishi_yozilmaydi()
    {
        var info = default(NetEhomeServerInfoV50);

        RegistrationCodec.ApplyKeepAlive((IntPtr)(&info), 100, 30, 4).ShouldBeFalse();
        info.dwKeepAliveSec.ShouldBe(0u);
    }

    [Fact]
    public void Manzil_yozilganda_bufer_tozalanadi()
    {
        var address = default(NetEhomeIpAddress);
        Put(address.szIP, "255.255.255.255-eski-uzun-qiymat");

        RegistrationCodec.WriteAddress(&address, "0.0.0.0", 7660);

        Marshal.PtrToStringAnsi((IntPtr)address.szIP).ShouldBe("0.0.0.0");
        address.wPort.ShouldBe((ushort)7660);
    }

    private static void Put(byte* target, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        bytes.CopyTo(new Span<byte>(target, bytes.Length));
        target[bytes.Length] = 0;
    }

    private static byte[] Bytes(NetEhomeServerInfoV50* info)
        => new ReadOnlySpan<byte>(info, sizeof(NetEhomeServerInfoV50)).ToArray();
}
