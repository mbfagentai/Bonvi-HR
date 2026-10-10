using Bonvi.DeviceAgent;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.DeviceAgent.Hikvision;
using Bonvi.IsupGateway.Faces;
using Bonvi.IsupGateway.Tunnel;
using Microsoft.Extensions.Options;

namespace Bonvi.IsupGateway.Agents;

/// <summary>
/// Har bir sozlangan terminal uchun bittadan <see cref="AgentWorker"/> — bitta jarayonda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Agent kodi O'ZGARMAGAN: LAN'dagi bilan bir xil sinf, faqat
/// <see cref="HikvisionClient"/> ostida ISUP transporti
/// (<see cref="IsupPassthroughHandler"/>). Ro'yxat solishtiruvi, hodisa
/// kursori, ommaviy o'chirish to'sig'i va token↔seriya bog'lanishi — hammasi
/// SPEC-agent'dagidek ishlaydi.
/// </para>
/// <para>
/// Terminal hali ulanmagan bo'lsa agent ishlayveradi: tunnel 503 qaytaradi,
/// agent buni vaqtinchalik xato deb biladi va o'z oralig'i bilan qayta
/// urinadi — terminal ulanishi bilan ish o'zi boshlanadi.
/// </para>
/// <para>
/// Jurnalda har terminalning qatori <c>Terminal</c> doirasi (scope) bilan
/// ajraladi: doira agent ishga tushganda o'rnatiladi va uning halqalariga
/// (async oqim) o'zi o'tadi.
/// </para>
/// </remarks>
internal sealed partial class DeviceAgentsHostedService(
    IOptions<IsupOptions> options,
    IIsapiTunnel tunnel,
    FacePictureStore faces,
    ILoggerFactory loggers,
    TimeProvider time,
    ILogger<DeviceAgentsHostedService> logger) : IHostedService, IDisposable
{
    private static readonly Func<ILogger, string, IDisposable?> TerminalScope =
        LoggerMessage.DefineScope<string>("Terminal {Terminal}");

    private readonly List<AgentInstance> _agents = [];

    /// <summary>Bonvi API ga ulanishlar — barcha agentlar uchun bitta hovuz.</summary>
    private readonly SocketsHttpHandler _apiHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        Directory.CreateDirectory(settings.StateDirectory);

        if (settings.Devices.Count == 0)
        {
            NoDevices();
            return;
        }

        foreach (var device in settings.Devices)
        {
            var instance = Create(settings, device);
            _agents.Add(instance);

            // Shablonli doira: konsolda `=> Terminal zavod-1` bo'lib chiqadi
            // (lug'at berilsa, oddiy formatlovchi tur nomini yozardi).
            using (TerminalScope(logger, device.DeviceId))
            {
                await instance.Worker.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        AgentsStarted(_agents.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_agents.Select(agent => agent.Worker.StopAsync(cancellationToken))).ConfigureAwait(false);
    }

    public void Dispose()
    {
        foreach (var agent in _agents)
        {
            agent.Dispose();
        }

        _agents.Clear();
        _apiHandler.Dispose();
    }

    private AgentInstance Create(IsupOptions settings, IsupDeviceOptions device)
    {
        var agentOptions = Options.Create(new AgentOptions
        {
            ApiBaseUrl = settings.ApiBaseUrl,
            DeviceToken = device.Token,

            // Faqat jurnal uchun — haqiqiy manzil yo'q, transport tunnel.
            DeviceHost = $"isup://{device.DeviceId}",

            // Agent sozlamasida majburiy, lekin ISUP'da ishlatilmaydi:
            // tunnelda digest yo'q, terminal kalit bilan tasdiqlangan.
            DeviceUser = "isup",
            DevicePassword = "isup",

            EventsIntervalSeconds = settings.EventsIntervalSeconds,
            RosterIntervalSeconds = settings.RosterIntervalSeconds,
            ColdStartHours = settings.ColdStartHours,

            // Jonli kadr ikkilik javob — tunnel orqali o'tmaydi.
            SnapshotChannel = string.Empty,

            StatePath = Path.Combine(settings.StateDirectory, $"{device.DeviceId}.json"),
        });

        var deviceHttp = new HttpClient(new IsupPassthroughHandler(device.DeviceId, tunnel, faces, settings), disposeHandler: true)
        {
            // Yo'l nisbiy (`/ISAPI/...`) — manzil faqat Uri yasash uchun.
            BaseAddress = new Uri("http://isup.invalid/"),

            // Tunnel muddati + navbat (ikki halqa bitta terminalni bo'lishadi).
            Timeout = TimeSpan.FromMilliseconds(settings.PassthroughTimeoutMs * 3L),
        };

        var apiBase = settings.ApiBaseUrl.EndsWith('/') ? settings.ApiBaseUrl : settings.ApiBaseUrl + "/";

        var bonviHttp = new HttpClient(_apiHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(apiBase),
            Timeout = TimeSpan.FromSeconds(30),
        };

        // Qurilma tokeni HAR so'rovda — LAN agentidagi bilan bir xil (Program.cs).
        bonviHttp.DefaultRequestHeaders.Add("X-Bonvi-Device-Token", device.Token);

        var worker = new AgentWorker(
            new HikvisionClient(deviceHttp, agentOptions),
            new BonviAgentClient(bonviHttp),
            agentOptions,
            loggers.CreateLogger<AgentWorker>(),
            time);

        return new AgentInstance(worker, deviceHttp, bonviHttp);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Agentlar ishga tushdi: {Count} ta terminal")]
    private partial void AgentsStarted(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Isup:Devices bo'sh — birorta terminal sozlanmagan, shlyuz faqat tinglaydi")]
    private partial void NoDevices();

    private sealed record AgentInstance(AgentWorker Worker, HttpClient DeviceHttp, HttpClient BonviHttp) : IDisposable
    {
        public void Dispose()
        {
            Worker.Dispose();
            DeviceHttp.Dispose();
            BonviHttp.Dispose();
        }
    }
}
