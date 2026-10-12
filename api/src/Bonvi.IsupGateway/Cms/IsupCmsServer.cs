using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Bonvi.IsupGateway.Native;
using Bonvi.IsupGateway.Tunnel;
using Microsoft.Extensions.Options;

namespace Bonvi.IsupGateway.Cms;

/// <summary>
/// ISUP 5.0 ro'yxatdan o'tish serveri (CMS) va ISAPI tunneli.
/// </summary>
/// <remarks>
/// <para>
/// Terminal <see cref="IsupOptions.ListenPort"/> ga o'zi ulanadi. Qadamlar
/// (SDK qo'ng'irog'i, <c>HCISUPCMS.h</c>): <c>ENUM_DEV_AUTH</c> — shu
/// terminalning kalitini qaytaramiz; <c>ENUM_DEV_SESSIONKEY</c> — sessiya
/// kalitini SDK'ga o'rnatamiz; <c>ENUM_DEV_ON</c> — terminal ulandi.
/// Ro'yxatda yo'q identifikator ikkala bosqichda ham RAD etiladi.
/// </para>
/// <para>
/// ⚠️ Qo'ng'iroq SDK'ning o'z oqimida keladi va undan istisno CHIQMASLIGI
/// shart (native chegarani kesib o'tgan istisno jarayonni yiqitadi) — shu
/// sababli <see cref="OnRegister"/> hamma narsani ushlaydi.
/// </para>
/// </remarks>
internal sealed partial class IsupCmsServer : IIsapiTunnel, IDisposable
{
    /// <summary>Tunnel javobi uchun bufer. Eng katta javob (50 ta hodisa sahifasi) ~30 KB.</summary>
    internal const int ResponseCapacity = 1024 * 1024;

    private readonly IsupOptions _options;
    private readonly DeviceRegistry _registry;
    private readonly ILogger<IsupCmsServer> _logger;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, IsupDeviceOptions> _devices;

    /// <summary>
    /// Bitta terminalga bir vaqtda bitta tunnel so'rovi. Agentning ikki halqasi
    /// (hodisalar va ro'yxat) parallel ishlaydi; SDK bitta sessiyada parallel
    /// so'rovni qo'llashi kafolatlanmagan.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    /// <summary>SDK init yo'llari — SDK ularni nusxalashi kafolatlanmagan, jarayon oxirigacha yashaydi.</summary>
    private readonly List<IntPtr> _pinnedStrings = [];

    private GCHandle _self;
    private int _listenHandle = -1;
    private bool _initialized;

    public IsupCmsServer(
        IOptions<IsupOptions> options,
        DeviceRegistry registry,
        ILogger<IsupCmsServer> logger,
        TimeProvider time)
    {
        _options = options.Value;
        _registry = registry;
        _logger = logger;
        _time = time;
        _devices = _options.Devices.ToDictionary(d => d.DeviceId, StringComparer.Ordinal);
    }

    /// <summary>SDK'ni yuklaydi va tinglashni boshlaydi. Xato — istisno (shlyuz ishga tushmaydi).</summary>
    public unsafe void Start()
    {
        var directory = _options.NativeDirectory;

        NativeLibraryResolver.Register(directory);

        // OpenSSL yo'llari — FAQAT Init'dan oldin.
        SetInitPath(IsupNative.InitCfgLibcryptoPath, Path.Combine(directory, "libcrypto.so.1.1"));
        SetInitPath(IsupNative.InitCfgLibsslPath, Path.Combine(directory, "libssl.so.1.1"));

        if (IsupNative.NET_ECMS_Init() == 0)
        {
            throw new InvalidOperationException($"NET_ECMS_Init muvaffaqiyatsiz, SDK xatosi {IsupNative.NET_ECMS_GetLastError()}.");
        }

        _initialized = true;

        // Komponentlar jildi — Hikvision namunasidagidek, '/' bilan.
        SetLocalPath(IsupNative.LocalCfgComPath, Path.Combine(directory, "HCAapSDKCom") + "/");

        // Faqat himoyalangan protokol (ISUP 4.0+ kalit bilan). Eski, ochiq
        // EHome 2.0 ulanishlari umuman qabul qilinmaydi.
        var security = new NetEhomeLocalAccessSecurity
        {
            dwSize = (uint)sizeof(NetEhomeLocalAccessSecurity),
            byAccessSecurity = 2,
        };

        if (IsupNative.NET_ECMS_SetSDKLocalCfg(IsupNative.LocalCfgAccessSecurity, (IntPtr)(&security)) == 0)
        {
            SecurityModeFailed(IsupNative.NET_ECMS_GetLastError());
        }

        if (_options.SdkLogLevel > 0)
        {
            Directory.CreateDirectory(_options.SdkLogDirectory);

            if (IsupNative.NET_ECMS_SetLogToFile((uint)_options.SdkLogLevel, _options.SdkLogDirectory, 1) == 0)
            {
                LocalPathFailed(_options.SdkLogDirectory, IsupNative.NET_ECMS_GetLastError());
            }
        }

        _self = GCHandle.Alloc(this);

        var listen = new NetEhomeCmsListenParam
        {
            fnCB = &OnRegister,
            pUserData = GCHandle.ToIntPtr(_self),
            dwKeepAliveSec = (uint)_options.KeepAliveSeconds,
            dwTimeOutCount = (uint)_options.KeepAliveTimeoutCount,
        };

        RegistrationCodec.WriteAddress(&listen.struAddress, _options.ListenAddress, _options.ListenPort);

        _listenHandle = IsupNative.NET_ECMS_StartListen(ref listen);

        if (_listenHandle < 0)
        {
            throw new InvalidOperationException(
                $"NET_ECMS_StartListen {_options.ListenAddress}:{_options.ListenPort} muvaffaqiyatsiz, SDK xatosi {IsupNative.NET_ECMS_GetLastError()}.");
        }

        var build = IsupNative.NET_ECMS_GetBuildVersion();
        Listening(_options.ListenAddress, _options.ListenPort, build, _devices.Count);
    }

    public async Task<TunnelResponse> SendAsync(
        string deviceId,
        string method,
        string pathAndQuery,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGet(deviceId, out _))
        {
            return TunnelResponse.Offline(deviceId);
        }

        var gate = _gates.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Navbat kutilgan orada terminal qayta ulangan bo'lishi mumkin — yangi sessiya raqami.
            if (!_registry.TryGet(deviceId, out var session))
            {
                return TunnelResponse.Offline(deviceId);
            }

            var payload = body.ToArray();

            // Native chaqiruv bloklaydi (javobni kutadi) — thread pool'da, async halqani to'smasdan.
            // Bekor qilish native chaqiruvni to'xtata olmaydi: muddat `dwRecvTimeOut` bilan cheklangan.
            return await Task.Run(() => Invoke(session.LoginId, method, pathAndQuery, payload), CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (_listenHandle >= 0)
        {
            _ = IsupNative.NET_ECMS_StopListen(_listenHandle);
            _listenHandle = -1;
        }

        if (_initialized)
        {
            _ = IsupNative.NET_ECMS_Fini();
            _initialized = false;
        }

        if (_self.IsAllocated)
        {
            _self.Free();
        }

        foreach (var pointer in _pinnedStrings)
        {
            Marshal.FreeHGlobal(pointer);
        }

        _pinnedStrings.Clear();

        foreach (var gate in _gates.Values)
        {
            gate.Dispose();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnRegister(int loginId, uint dataType, IntPtr outBuffer, uint outLength, IntPtr inBuffer, uint inLength, IntPtr user)
    {
        try
        {
            if (user == IntPtr.Zero || GCHandle.FromIntPtr(user).Target is not IsupCmsServer server)
            {
                return 0;
            }

            return server.HandleRegistration(loginId, dataType, outBuffer, outLength, inBuffer, inLength) ? 1 : 0;
        }
        catch
        {
            // Bu yerdan istisno native kodga o'tsa jarayon yiqiladi. Jurnal
            // ham yozilmaydi — logger o'zi istisno bergan bo'lishi mumkin.
            return 0;
        }
    }

    private bool HandleRegistration(int loginId, uint dataType, IntPtr outBuffer, uint outLength, IntPtr inBuffer, uint inLength)
    {
        try
        {
            if (dataType == IsupNative.DevOff)
            {
                var gone = _registry.OfflineByLogin(loginId);

                if (gone is not null)
                {
                    DeviceOffline(gone);
                }

                return true;
            }

            if (outBuffer == IntPtr.Zero)
            {
                return true;
            }

            var info = RegistrationCodec.Read(outBuffer, outLength);
            var known = _devices.TryGetValue(info.DeviceId, out var device);

            switch (dataType)
            {
                case IsupNative.DevAuth:
                    if (!known)
                    {
                        UnknownDevice(info.DeviceId, info.RemoteAddress, info.Serial);
                        return false;
                    }

                    if (!RegistrationCodec.WriteKey(inBuffer, inLength, device!.Key))
                    {
                        KeyBufferTooSmall(info.DeviceId, inLength);
                        return false;
                    }

                    return true;

                case IsupNative.DevSessionKey:
                    return known && InstallSessionKey(info);

                case IsupNative.DevOn:
                    if (!known)
                    {
                        UnknownDevice(info.DeviceId, info.RemoteAddress, info.Serial);
                        return false;
                    }

                    RegistrationCodec.ApplyKeepAlive(inBuffer, inLength, _options.KeepAliveSeconds, _options.KeepAliveTimeoutCount);

                    _registry.Online(new DeviceSession(
                        info.DeviceId,
                        loginId,
                        info.Serial,
                        info.Firmware,
                        info.RemoteAddress,
                        _time.GetUtcNow()));

                    DeviceOnline(info.DeviceId, info.Serial, info.Firmware, info.RemoteAddress, info.ProtocolVersion);
                    return true;

                case IsupNative.DevAddressChanged:
                    if (known)
                    {
                        _registry.Online(new DeviceSession(
                            info.DeviceId, loginId, info.Serial, info.Firmware, info.RemoteAddress, _time.GetUtcNow()));
                    }

                    return true;

                case IsupNative.DevDasRequest:
                    if (known && !string.IsNullOrWhiteSpace(_options.PublicAddress))
                    {
                        RegistrationCodec.WriteText(
                            inBuffer,
                            inLength,
                            RegistrationCodec.DasReply(_options.PublicAddress, _options.PublicDomain, _options.ListenPort));
                    }

                    return true;

                case IsupNative.DevKeyError:
                    KeyMismatch(info.DeviceId, info.RemoteAddress);
                    return true;

                case IsupNative.DevSessionKeyError:
                    SessionKeyError(info.DeviceId);
                    return true;

                default:
                    return true;
            }
        }
        catch (Exception failure)
        {
            CallbackFailed(dataType, failure);
            return false;
        }
    }

    private unsafe bool InstallSessionKey(RegistrationInfo info)
    {
        var key = default(NetEhomeDevSessionKey);

        info.DeviceIdBytes.AsSpan(0, Math.Min(info.DeviceIdBytes.Length, IsupNative.MaxDeviceIdLength))
            .CopyTo(new Span<byte>(key.sDeviceID, IsupNative.MaxDeviceIdLength));
        info.SessionKey.AsSpan(0, Math.Min(info.SessionKey.Length, IsupNative.MasterKeyLength))
            .CopyTo(new Span<byte>(key.sSessionKey, IsupNative.MasterKeyLength));

        if (IsupNative.NET_ECMS_SetDeviceSessionKey(ref key) == 0)
        {
            SessionKeyRejected(info.DeviceId, IsupNative.NET_ECMS_GetLastError());
            return false;
        }

        return true;
    }

    private unsafe TunnelResponse Invoke(int loginId, string method, string pathAndQuery, byte[] body)
    {
        // `Uri.PathAndQuery` allaqachon %-kodlangan, ya'ni ASCII.
        var url = Encoding.ASCII.GetBytes($"{method} {pathAndQuery}");

        var urlBuffer = (byte*)NativeMemory.AllocZeroed((nuint)url.Length + 1);
        var inBuffer = body.Length > 0 ? (byte*)NativeMemory.AllocZeroed((nuint)body.Length + 1) : null;
        var outBuffer = (byte*)NativeMemory.AllocZeroed(ResponseCapacity);

        try
        {
            url.CopyTo(new Span<byte>(urlBuffer, url.Length));
            body.CopyTo(new Span<byte>(inBuffer, body.Length));

            var param = new NetEhomePtxmlParam
            {
                pRequestUrl = (IntPtr)urlBuffer,
                dwRequestUrlLen = (uint)url.Length,
                pInBuffer = (IntPtr)inBuffer,
                dwInSize = (uint)body.Length,
                pOutBuffer = (IntPtr)outBuffer,
                dwOutSize = ResponseCapacity,
                dwRecvTimeOut = (uint)_options.PassthroughTimeoutMs,
            };

            if (IsupNative.NET_ECMS_ISAPIPassThrough(loginId, ref param) == 0)
            {
                return TunnelResponse.Failed($"ISUP tunnel: {method} {RouteOf(pathAndQuery)} — SDK xatosi {IsupNative.NET_ECMS_GetLastError()}");
            }

            if (param.dwReturnedXMLLen > ResponseCapacity)
            {
                return TunnelResponse.Failed($"ISUP tunnel: {method} {RouteOf(pathAndQuery)} — javob {param.dwReturnedXMLLen} bayt, bufer {ResponseCapacity}");
            }

            var length = RegistrationCodec.ResponseLength(
                new ReadOnlySpan<byte>(outBuffer, ResponseCapacity),
                param.dwReturnedXMLLen);

            var text = Encoding.UTF8.GetString(outBuffer, length).TrimEnd('\0');

            return new TunnelResponse(TunnelOutcome.Ok, text);
        }
        finally
        {
            NativeMemory.Free(outBuffer);
            NativeMemory.Free(inBuffer);
            NativeMemory.Free(urlBuffer);
        }
    }

    private void SetInitPath(int type, string path)
    {
        var pointer = Pin(path);

        if (IsupNative.NET_ECMS_SetSDKInitCfg(type, pointer) == 0)
        {
            throw new InvalidOperationException($"NET_ECMS_SetSDKInitCfg({type}) {path} — SDK xatosi {IsupNative.NET_ECMS_GetLastError()}.");
        }
    }

    private void SetLocalPath(int type, string path)
    {
        if (IsupNative.NET_ECMS_SetSDKLocalCfg(type, Pin(path)) == 0)
        {
            LocalPathFailed(path, IsupNative.NET_ECMS_GetLastError());
        }
    }

    /// <summary>Satr nusxasi native xotirada — jarayon oxirigacha.</summary>
    private unsafe IntPtr Pin(string text)
    {
        // Hikvision namunasi 256 baytlik buferni beradi.
        var bytes = Encoding.UTF8.GetBytes(text);
        var pointer = Marshal.AllocHGlobal(Math.Max(256, bytes.Length + 1));

        new Span<byte>((void*)pointer, Math.Max(256, bytes.Length + 1)).Clear();
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        _pinnedStrings.Add(pointer);

        return pointer;
    }

    /// <summary>Jurnal uchun yo'l — so'rov parametrlarisiz (ularda FPID kabi qiymatlar bo'ladi).</summary>
    private static string RouteOf(string pathAndQuery)
    {
        var question = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        return question < 0 ? pathAndQuery : pathAndQuery[..question];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ISUP CMS tinglamoqda: {Address}:{Port} (SDK build {Build}), ruxsat etilgan terminallar: {Devices}")]
    private partial void Listening(string address, int port, uint build, int devices);

    [LoggerMessage(Level = LogLevel.Information, Message = "Terminal ULANDI: {DeviceId}, seriya {Serial}, proshivka {Firmware}, manzil {Address}, protokol {Protocol}")]
    private partial void DeviceOnline(string deviceId, string serial, string firmware, string address, string protocol);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Terminal UZILDI: {DeviceId}")]
    private partial void DeviceOffline(string deviceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Noma'lum terminal rad etildi: ID '{DeviceId}', manzil {Address}, seriya {Serial}")]
    private partial void UnknownDevice(string deviceId, string address, string serial);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Terminal kaliti mos kelmadi: {DeviceId} ({Address}) — terminaldagi «Encryption key» ni sozlama bilan solishtiring")]
    private partial void KeyMismatch(string deviceId, string address);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kalit buferga sig'madi: {DeviceId}, sig'im {Capacity}")]
    private partial void KeyBufferTooSmall(string deviceId, uint capacity);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sessiya kaliti o'rnatilmadi: {DeviceId}, SDK xatosi {Error}")]
    private partial void SessionKeyRejected(string deviceId, uint error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sessiya kaliti almashinuvida xato: {DeviceId}")]
    private partial void SessionKeyError(string deviceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Himoyalangan rejim yoqilmadi, SDK xatosi {Error}")]
    private partial void SecurityModeFailed(uint error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SDK komponent jildi o'rnatilmadi: {Path}, SDK xatosi {Error}")]
    private partial void LocalPathFailed(string path, uint error);

    [LoggerMessage(Level = LogLevel.Error, Message = "ISUP qo'ng'irog'ida xato (tur {DataType})")]
    private partial void CallbackFailed(uint dataType, Exception failure);
}
