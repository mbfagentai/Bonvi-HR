using System.Runtime.InteropServices;

namespace Bonvi.IsupGateway.Native;

/*
  Hikvision HCISUPSDK — CMS (ro'yxatdan o'tish) moduli, Linux x64.

  ⚠️ BU FAYLDAGI TUZILMALAR `HCISUPCMS.h` / `HCISUPPublic.h` DAN BAYTMA-BAYT
  KO'CHIRILGAN. Bitta maydon tushib qolsa yoki joyi almashsa, SDK xotiraning
  boshqa joyini o'qiydi/yozadi — xato chiqmaydi, terminal shunchaki
  ro'yxatdan o'tmaydi yoki jarayon yiqiladi. Shu sababli:
    • hamma tuzilma blittable (`fixed` buferlar), marshalling taxmini YO'Q;
    • o'lchamlar `IsupNativeLayoutTests` da tekshiriladi (x64: 132, 184,
      524, 912, 1488, 96, 272, 132 bayt).

  Linux tiplari (HCISUPPublic.h): DWORD = uint, LONG = int (32 bit!),
  BOOL = int, WORD = ushort. Chaqiruv konvensiyasi — cdecl.
*/

/// <summary><c>NET_EHOME_IPADDRESS</c> — 132 bayt.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeIpAddress
{
    public fixed byte szIP[128];
    public ushort wPort;
    public fixed byte byRes[2];
}

/// <summary><c>NET_EHOME_CMS_LISTEN_PARAM</c> — 184 bayt.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeCmsListenParam
{
    public NetEhomeIpAddress struAddress;

    /// <summary><c>DEVICE_REGISTER_CB</c> — 8 baytga tekislanadi (136-bayt).</summary>
    public delegate* unmanaged[Cdecl]<int, uint, IntPtr, uint, IntPtr, uint, IntPtr, int> fnCB;

    public IntPtr pUserData;
    public uint dwKeepAliveSec;
    public uint dwTimeOutCount;
    public fixed byte byRes[24];
}

/// <summary><c>NET_EHOME_DEV_REG_INFO</c> — 524 bayt.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeDevRegInfo
{
    public uint dwSize;
    public uint dwNetUnitType;
    public fixed byte byDeviceID[IsupNative.MaxDeviceIdLength];
    public fixed byte byFirmwareVersion[24];
    public NetEhomeIpAddress struDevAdd;
    public uint dwDevType;
    public uint dwManufacture;
    public fixed byte byPassWord[32];
    public fixed byte sDeviceSerial[12];
    public byte byReliableTransmission;
    public byte byWebSocketTransmission;
    public byte bySupportRedirect;
    public fixed byte byDevProtocolVersion[6];
    public fixed byte bySessionKey[IsupNative.MasterKeyLength];
    public byte byMarketType;
    public byte byRes1;
    public byte bySupport;
    public fixed byte byRes[24];
}

/// <summary><c>NET_EHOME_DEV_REG_INFO_V12</c> — 912 bayt. Ro'yxat qo'ng'irog'ining chiqish buferi.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeDevRegInfoV12
{
    public NetEhomeDevRegInfo struRegInfo;
    public NetEhomeIpAddress struRegAddr;
    public fixed byte sDevName[64];
    public fixed byte byDeviceFullSerial[64];
    public fixed byte byRes[128];
}

/// <summary><c>NET_EHOME_BLACKLIST_SEVER</c> — 292 bayt.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeBlacklistServer
{
    public NetEhomeIpAddress struAdd;
    public fixed byte byServerName[32];
    public fixed byte byUserName[32];
    public fixed byte byPassWord[32];
    public fixed byte byRes[64];
}

/// <summary>
/// <c>NET_EHOME_SERVER_INFO_V50</c> — 1488 bayt. <c>ENUM_DEV_ON</c> da terminalga
/// qaytariladigan javob (yurak urishi, signal/NTP/rasm serveri).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeServerInfoV50
{
    public uint dwSize;
    public uint dwKeepAliveSec;
    public uint dwTimeOutCount;
    public NetEhomeIpAddress struTCPAlarmSever;
    public NetEhomeIpAddress struUDPAlarmSever;
    public uint dwAlarmServerType;
    public NetEhomeIpAddress struNTPSever;
    public uint dwNTPInterval;
    public NetEhomeIpAddress struPictureSever;
    public uint dwPicServerType;
    public NetEhomeBlacklistServer struBlackListServer;
    public NetEhomeIpAddress struRedirectSever;
    public fixed byte byClouldAccessKey[64];
    public fixed byte byClouldSecretKey[64];
    public byte byClouldHttps;
    public fixed byte byRes1[3];
    public uint dwAlarmKeepAliveSec;
    public uint dwAlarmTimeOutCount;
    public uint dwClouldPoolId;
    public fixed byte byRes[368];
}

/// <summary><c>NET_EHOME_DEV_SESSIONKEY</c> — 272 bayt.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeDevSessionKey
{
    public fixed byte sDeviceID[IsupNative.MaxDeviceIdLength];
    public fixed byte sSessionKey[IsupNative.MasterKeyLength];
}

/// <summary><c>NET_EHOME_PTXML_PARAM</c> — 96 bayt. ISAPI tunnel so'rovi.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomePtxmlParam
{
    public IntPtr pRequestUrl;
    public uint dwRequestUrlLen;
    public IntPtr pCondBuffer;
    public uint dwCondSize;
    public IntPtr pInBuffer;
    public uint dwInSize;
    public IntPtr pOutBuffer;
    public uint dwOutSize;
    public uint dwReturnedXMLLen;
    public uint dwRecvTimeOut;
    public uint dwHandle;
    public fixed byte byRes[24];
}

/// <summary><c>NET_EHOME_LOCAL_ACCESS_SECURITY</c> — 132 bayt.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NetEhomeLocalAccessSecurity
{
    public uint dwSize;

    /// <summary>0 — har qanday versiya, 1 — faqat 4.0 dan pasti, 2 — faqat himoyalangan (4.0+).</summary>
    public byte byAccessSecurity;

    public fixed byte byRes[127];
}

/// <summary>CMS modulining native chaqiruvlari.</summary>
internal static unsafe partial class IsupNative
{
    /// <summary>Kutubxona nomi — <see cref="NativeLibraryResolver"/> uni aniq yo'lga bog'laydi.</summary>
    public const string Library = "HCISUPCMS";

    public const int MaxDeviceIdLength = 256;

    public const int MasterKeyLength = 16;

    // NET_EHOME_REGISTER_TYPE
    public const uint DevOn = 0;
    public const uint DevOff = 1;
    public const uint DevAddressChanged = 2;
    public const uint DevAuth = 3;
    public const uint DevSessionKey = 4;
    public const uint DevDasRequest = 5;
    public const uint DevSessionKeyRequest = 6;
    public const uint DevReRegister = 7;
    public const uint DevPing = 8;
    public const uint DevKeyError = 9;
    public const uint DevSessionKeyError = 10;

    // NET_EHOME_CMS_INIT_CFG_TYPE
    public const int InitCfgLibcryptoPath = 0;
    public const int InitCfgLibsslPath = 1;

    // NET_EHOME_LOCAL_CFG_TYPE
    public const int LocalCfgAccessSecurity = 0;
    public const int LocalCfgComPath = 5;

    [LibraryImport(Library)]
    public static partial int NET_ECMS_Init();

    [LibraryImport(Library)]
    public static partial int NET_ECMS_Fini();

    [LibraryImport(Library)]
    public static partial int NET_ECMS_SetSDKInitCfg(int enumType, IntPtr lpInBuff);

    [LibraryImport(Library)]
    public static partial int NET_ECMS_SetSDKLocalCfg(int enumType, IntPtr lpInBuff);

    [LibraryImport(Library)]
    public static partial uint NET_ECMS_GetLastError();

    [LibraryImport(Library)]
    public static partial uint NET_ECMS_GetBuildVersion();

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int NET_ECMS_SetLogToFile(uint iLogLevel, string strLogDir, int bAutoDel);

    [LibraryImport(Library)]
    public static partial int NET_ECMS_StartListen(ref NetEhomeCmsListenParam lpCmsListenPara);

    [LibraryImport(Library)]
    public static partial int NET_ECMS_StopListen(int iHandle);

    [LibraryImport(Library)]
    public static partial int NET_ECMS_ForceLogout(int lUserID);

    [LibraryImport(Library)]
    public static partial int NET_ECMS_SetDeviceSessionKey(ref NetEhomeDevSessionKey pDeviceKey);

    [LibraryImport(Library)]
    public static partial int NET_ECMS_ISAPIPassThrough(int lUserID, ref NetEhomePtxmlParam lpParam);
}
