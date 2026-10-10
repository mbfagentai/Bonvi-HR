using System.Runtime.CompilerServices;
using Bonvi.IsupGateway.Native;

namespace Bonvi.IsupGateway.Tests;

/// <summary>
/// SDK tuzilmalari <c>HCISUPCMS.h</c> / <c>HCISUPPublic.h</c> bilan baytma-bayt mosmi (Linux x64).
/// </summary>
/// <remarks>
/// Kutilgan qiymatlar header'dan qo'lda hisoblangan (C tekislash qoidalari:
/// DWORD — 4, ko'rsatkich — 8, WORD — 2). Bitta maydon tushib qolsa yoki
/// joyi siljisa, SDK boshqa xotirani o'qiydi — xato bermasdan. Shu test o'sha
/// xatoni tirik terminalga yetmasdan ushlaydi.
/// </remarks>
public sealed unsafe class IsupNativeLayoutTests
{
    [Fact]
    public void IP_manzil_132_bayt()
    {
        Unsafe.SizeOf<NetEhomeIpAddress>().ShouldBe(132);

        var value = default(NetEhomeIpAddress);
        Offset(&value, &value.wPort).ShouldBe(128);
    }

    [Fact]
    public void Tinglash_parametri_184_bayt_qongiroq_8_baytga_tekislangan()
    {
        Unsafe.SizeOf<NetEhomeCmsListenParam>().ShouldBe(184);

        var value = default(NetEhomeCmsListenParam);
        Offset(&value, &value.fnCB).ShouldBe(136);
        Offset(&value, &value.pUserData).ShouldBe(144);
        Offset(&value, &value.dwKeepAliveSec).ShouldBe(152);
        Offset(&value, &value.dwTimeOutCount).ShouldBe(156);
        Offset(&value, value.byRes).ShouldBe(160);
    }

    [Fact]
    public void Royxat_malumoti_524_bayt()
    {
        Unsafe.SizeOf<NetEhomeDevRegInfo>().ShouldBe(524);

        var value = default(NetEhomeDevRegInfo);
        Offset(&value, value.byDeviceID).ShouldBe(8);
        Offset(&value, value.byFirmwareVersion).ShouldBe(264);
        Offset(&value, &value.struDevAdd).ShouldBe(288);
        Offset(&value, &value.dwDevType).ShouldBe(420);
        Offset(&value, value.byPassWord).ShouldBe(428);
        Offset(&value, value.sDeviceSerial).ShouldBe(460);
        Offset(&value, &value.byReliableTransmission).ShouldBe(472);
        Offset(&value, value.byDevProtocolVersion).ShouldBe(475);
        Offset(&value, value.bySessionKey).ShouldBe(481);
        Offset(&value, &value.byMarketType).ShouldBe(497);
        Offset(&value, value.byRes).ShouldBe(500);
    }

    [Fact]
    public void Royxat_malumoti_V12_912_bayt()
    {
        Unsafe.SizeOf<NetEhomeDevRegInfoV12>().ShouldBe(912);

        var value = default(NetEhomeDevRegInfoV12);
        Offset(&value, &value.struRegAddr).ShouldBe(524);
        Offset(&value, value.sDevName).ShouldBe(656);
        Offset(&value, value.byDeviceFullSerial).ShouldBe(720);
        Offset(&value, value.byRes).ShouldBe(784);
    }

    [Fact]
    public void Server_javobi_V50_1488_bayt()
    {
        Unsafe.SizeOf<NetEhomeBlacklistServer>().ShouldBe(292);
        Unsafe.SizeOf<NetEhomeServerInfoV50>().ShouldBe(1488);

        var value = default(NetEhomeServerInfoV50);
        Offset(&value, &value.dwKeepAliveSec).ShouldBe(4);
        Offset(&value, &value.dwTimeOutCount).ShouldBe(8);
        Offset(&value, &value.struTCPAlarmSever).ShouldBe(12);
        Offset(&value, &value.struUDPAlarmSever).ShouldBe(144);
        Offset(&value, &value.dwAlarmServerType).ShouldBe(276);
        Offset(&value, &value.struNTPSever).ShouldBe(280);
        Offset(&value, &value.dwNTPInterval).ShouldBe(412);
        Offset(&value, &value.struPictureSever).ShouldBe(416);
        Offset(&value, &value.dwPicServerType).ShouldBe(548);
        Offset(&value, &value.struBlackListServer).ShouldBe(552);
        Offset(&value, &value.struRedirectSever).ShouldBe(844);
        Offset(&value, value.byClouldAccessKey).ShouldBe(976);
        Offset(&value, value.byClouldSecretKey).ShouldBe(1040);
        Offset(&value, &value.byClouldHttps).ShouldBe(1104);
        Offset(&value, &value.dwAlarmKeepAliveSec).ShouldBe(1108);
        Offset(&value, &value.dwClouldPoolId).ShouldBe(1116);
        Offset(&value, value.byRes).ShouldBe(1120);
    }

    [Fact]
    public void Sessiya_kaliti_272_bayt()
    {
        Unsafe.SizeOf<NetEhomeDevSessionKey>().ShouldBe(272);

        var value = default(NetEhomeDevSessionKey);
        Offset(&value, value.sSessionKey).ShouldBe(256);
    }

    [Fact]
    public void Tunnel_parametri_96_bayt()
    {
        Unsafe.SizeOf<NetEhomePtxmlParam>().ShouldBe(96);

        var value = default(NetEhomePtxmlParam);
        Offset(&value, &value.dwRequestUrlLen).ShouldBe(8);
        Offset(&value, &value.pCondBuffer).ShouldBe(16);
        Offset(&value, &value.pInBuffer).ShouldBe(32);
        Offset(&value, &value.dwInSize).ShouldBe(40);
        Offset(&value, &value.pOutBuffer).ShouldBe(48);
        Offset(&value, &value.dwOutSize).ShouldBe(56);
        Offset(&value, &value.dwReturnedXMLLen).ShouldBe(60);
        Offset(&value, &value.dwRecvTimeOut).ShouldBe(64);
        Offset(&value, &value.dwHandle).ShouldBe(68);
        Offset(&value, value.byRes).ShouldBe(72);
    }

    [Fact]
    public void Kirish_xavfsizligi_132_bayt()
    {
        Unsafe.SizeOf<NetEhomeLocalAccessSecurity>().ShouldBe(132);

        var value = default(NetEhomeLocalAccessSecurity);
        Offset(&value, &value.byAccessSecurity).ShouldBe(4);
    }

    private static long Offset(void* start, void* field) => (byte*)field - (byte*)start;
}
