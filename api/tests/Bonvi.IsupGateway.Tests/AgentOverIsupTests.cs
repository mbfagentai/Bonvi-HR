using Bonvi.DeviceAgent;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.DeviceAgent.Hikvision;
using Bonvi.IsupGateway.Faces;
using Bonvi.IsupGateway.Tunnel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bonvi.IsupGateway.Tests;

/// <summary>
/// ⭐ AGENTNING O'ZI ISUP tunneli orqali — shlyuzdagi bilan aynan bir xil yig'ilgan.
/// </summary>
/// <remarks>
/// <see cref="Agents.DeviceAgentsHostedService"/> agentni qanday yig'sa, bu yerda
/// ham shunday: haqiqiy <see cref="AgentWorker"/>, <see cref="HikvisionClient"/>,
/// <see cref="BonviAgentClient"/>, ostida <see cref="IsupPassthroughHandler"/>.
/// Farqi faqat tunnelning narigi tomonida — soxta terminal.
/// </remarks>
public sealed class AgentOverIsupTests : IDisposable
{
    private const string FaceBase = "https://hr.bonvi.test/isup-face/";

    private static readonly byte[] Photo = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bonvi-isup-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 10, 3, 0, 0, TimeSpan.Zero));
    private readonly FacePictureStore _faces;
    private readonly FakeIsupTerminal _terminal;
    private readonly FakeBonviApi _bonvi = new();
    private readonly HttpClient _deviceHttp;
    private readonly HttpClient _bonviHttp;
    private readonly AgentWorker _worker;

    public AgentOverIsupTests()
    {
        Directory.CreateDirectory(_directory);

        _faces = new FacePictureStore(_clock);
        _terminal = new FakeIsupTerminal(_clock, _faces, FaceBase);

        var settings = new IsupOptions { FaceUrlBase = FaceBase };

        var agentOptions = Options.Create(new AgentOptions
        {
            ApiBaseUrl = "http://api.test",
            DeviceToken = "token",
            DeviceHost = "isup://zavod-1",
            DeviceUser = "isup",
            DevicePassword = "isup",
            SnapshotChannel = string.Empty,
            StatePath = Path.Combine(_directory, "zavod-1.json"),
        });

        _deviceHttp = new HttpClient(new IsupPassthroughHandler("zavod-1", _terminal, _faces, settings))
        {
            BaseAddress = new Uri("http://isup.invalid/"),
        };

        _bonviHttp = new HttpClient(_bonvi, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };

        _worker = new AgentWorker(
            new HikvisionClient(_deviceHttp, agentOptions),
            new BonviAgentClient(_bonviHttp),
            agentOptions,
            NullLogger<AgentWorker>.Instance,
            _clock);
    }

    [Fact]
    public async Task Royxatdagi_xodim_tunnel_orqali_yuzi_bilan_terminalga_yoziladi()
    {
        var employee = Guid.NewGuid();
        Roster((employee, "900000001"));

        await _worker.ReconcileAsync(TestContext.Current.CancellationToken);

        _terminal.Persons.ShouldContain("900000001");
        _terminal.WithFace.ShouldContain("900000001");
        _terminal.Downloaded["900000001"].ShouldBe(Photo);
        _faces.Count.ShouldBe(0, "rasm havolasi yozishdan keyin qolmaydi");

        var report = _bonvi.ReportItems.ShouldHaveSingleItem();
        report.GetProperty("employeeId").GetGuid().ShouldBe(employee);
        report.GetProperty("ok").GetBoolean().ShouldBeTrue();

        // Token terminal seriyasiga bog'lanadi — tunnel orqali o'qilgan seriya sarlavhada.
        _bonvi.Serials.ShouldContain(_terminal.Serial);
    }

    [Fact]
    public async Task Terminal_yuzni_rad_etsa_qatiy_rad_deb_hisobot_beriladi()
    {
        var employee = Guid.NewGuid();
        Roster((employee, "900000002"));
        _terminal.RejectFaceFor.Add("900000002");

        await _worker.ReconcileAsync(TestContext.Current.CancellationToken);

        var report = _bonvi.ReportItems.ShouldHaveSingleItem();
        report.GetProperty("ok").GetBoolean().ShouldBeFalse();
        report.GetProperty("rejected").GetBoolean().ShouldBeTrue();
        report.GetProperty("error").GetString()!.ShouldContain("face quality too low");
    }

    [Fact]
    public async Task Terminal_ulanmagan_bolsa_hech_narsa_yozilmaydi_va_xato_vaqtinchalik()
    {
        Roster((Guid.NewGuid(), "900000003"));
        _terminal.Online = false;

        var failure = await Should.ThrowAsync<HikvisionException>(
            () => _worker.ReconcileAsync(TestContext.Current.CancellationToken));

        failure.StatusCode.ShouldBe(503);
        _bonvi.ReportItems.ShouldBeEmpty("rad etilgan deb belgilanmaydi — terminal qaytgach qayta uriniladi");
    }

    [Fact]
    public async Task Otishlar_tunnel_orqali_serverga_yetadi()
    {
        _terminal.Pass("900000001");
        _clock.Advance(TimeSpan.FromSeconds(3));
        _terminal.Pass("900000002");
        _clock.Advance(TimeSpan.FromSeconds(3));

        await _worker.ForwardEventsAsync(TestContext.Current.CancellationToken);

        _bonvi.Stored.Values.OrderBy(v => v, StringComparer.Ordinal).ShouldBe(["900000001", "900000002"]);
    }

    public void Dispose()
    {
        _worker.Dispose();
        _deviceHttp.Dispose();
        _bonviHttp.Dispose();
        _bonvi.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Vaqtinchalik jild.
        }
    }

    private void Roster(params (Guid EmployeeId, string PersonNo)[] entries)
    {
        foreach (var (employeeId, _) in entries)
        {
            _bonvi.Photos[employeeId] = Photo;
        }

        _bonvi.Roster = new Dictionary<string, object?>
        {
            ["deviceId"] = Guid.NewGuid(),
            ["deviceName"] = "zavod-1",
            ["generatedAt"] = DateTimeOffset.UnixEpoch,
            ["entries"] = entries.Select(entry => new Dictionary<string, object?>
            {
                ["employeeId"] = entry.EmployeeId,
                ["personNo"] = entry.PersonNo,
                ["fullName"] = "Sinov " + entry.PersonNo,
                ["photoVersion"] = "employees/x.jpg",
                ["version"] = "employees/x.jpg:0:abcd1234",
                ["force"] = false,
                ["hold"] = false,
            }).ToList(),
            ["removeNumbers"] = Array.Empty<string>(),
            ["allowMassRemoval"] = false,
        };
    }
}
