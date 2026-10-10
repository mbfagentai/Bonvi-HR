using System.Net;
using System.Text.Json;
using Bonvi.DeviceAgent;
using Bonvi.DeviceAgent.Hikvision;
using Bonvi.IsupGateway.Faces;
using Bonvi.IsupGateway.Tunnel;
using Microsoft.Extensions.Options;

namespace Bonvi.IsupGateway.Tests;

/// <summary>
/// Agentning HAQIQIY terminal mijozi (<see cref="HikvisionClient"/>) ISUP transporti ustida.
/// </summary>
public sealed class IsupPassthroughHandlerTests : IDisposable
{
    private const string FaceBase = "https://hr.bonvi.test/isup-face/";

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x10, 0x20, 0x30];

    private readonly FacePictureStore _faces = new(TimeProvider.System);
    private readonly FakeIsupTerminal _terminal;
    private readonly HttpClient _http;
    private readonly HikvisionClient _client;

    public IsupPassthroughHandlerTests()
    {
        _terminal = new FakeIsupTerminal(TimeProvider.System, _faces, FaceBase);

        var options = new IsupOptions { FaceUrlBase = FaceBase };

        _http = new HttpClient(new IsupPassthroughHandler("zavod-1", _terminal, _faces, options))
        {
            BaseAddress = new Uri("http://isup.invalid/"),
        };

        _client = new HikvisionClient(_http, Options.Create(new AgentOptions
        {
            ApiBaseUrl = "http://api.test",
            DeviceToken = "token",
            DeviceHost = "isup://zavod-1",
            DevicePassword = "isup",
            StatePath = "/tmp/yoq.json",
        }));
    }

    [Fact]
    public async Task Seriya_GET_bilan_tanasiz_oqiladi()
    {
        var serial = await _client.ReadSerialAsync(TestContext.Current.CancellationToken);

        serial.ShouldBe(_terminal.Serial);

        var call = _terminal.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("GET");
        call.Path.ShouldBe("/ISAPI/System/deviceInfo");
        call.Body.ShouldBeEmpty();
    }

    [Fact]
    public async Task JSON_tana_va_sorov_parametrlari_ozgarishsiz_otadi()
    {
        _terminal.Persons.AddRange(["900000001", "900000002"]);

        var people = await _client.ListPersonsAsync(TestContext.Current.CancellationToken);

        people.Select(p => p.EmployeeNo).ShouldBe(["900000001", "900000002"]);

        var call = _terminal.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("POST");
        call.Path.ShouldBe("/ISAPI/AccessControl/UserInfo/Search?format=json");

        using var json = JsonDocument.Parse(call.Body);
        json.RootElement.TryGetProperty("UserInfoSearchCond", out _).ShouldBeTrue("PascalCase o'ram saqlanadi");
    }

    [Fact]
    public async Task Raqam_band_bolsa_Modify_ga_otadi()
    {
        // Ro'yxat o'qilgandan keyin odam boshqa yo'l bilan qo'shilgan (poyga).
        _terminal.Persons.Add("900000001");
        _terminal.HiddenFromSearch.Add("900000001");

        await _client.WritePersonAsync("900000001", "Sinov Xodim", exists: false, TestContext.Current.CancellationToken);

        _terminal.Calls.Select(c => c.Path).ShouldBe(
        [
            "/ISAPI/AccessControl/UserInfo/Record?format=json",
            "/ISAPI/AccessControl/UserInfo/Modify?format=json",
        ]);
    }

    [Fact]
    public async Task Yuz_multipart_dan_faceURL_li_JSON_ga_aylanadi_va_havola_ochadi()
    {
        await _client.WriteFaceAsync("900000001", Jpeg, replace: false, TestContext.Current.CancellationToken);

        var call = _terminal.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("POST");
        call.Path.ShouldBe("/ISAPI/Intelligent/FDLib/FaceDataRecord?format=json");

        using var json = JsonDocument.Parse(call.Body);
        json.RootElement.GetProperty("FPID").GetString().ShouldBe("900000001");
        json.RootElement.GetProperty("FDID").GetString().ShouldBe("1");
        json.RootElement.GetProperty("faceLibType").GetString().ShouldBe("blackFD");
        json.RootElement.GetProperty("faceURL").GetString()!.ShouldStartWith(FaceBase);

        // Terminal aynan agent bergan rasmni yuklab oldi...
        _terminal.Downloaded["900000001"].ShouldBe(Jpeg);

        // ...va havola so'rov tugashi bilan o'chdi.
        _faces.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Yuzni_almashtirish_FDSetUp_ga_faceURL_bilan()
    {
        await _client.WriteFaceAsync("900000002", Jpeg, replace: true, TestContext.Current.CancellationToken);

        var call = _terminal.Calls.ShouldHaveSingleItem();
        call.Method.ShouldBe("PUT");
        call.Path.ShouldBe("/ISAPI/Intelligent/FDLib/FDSetUp?format=json&FDID=1&FPID=900000002");
        call.Body.ShouldContain("\"faceURL\":\"" + FaceBase);
        _terminal.Downloaded.ShouldContainKey("900000002");
    }

    [Fact]
    public async Task Terminal_rad_etsa_400_va_matni_saqlanadi()
    {
        _terminal.RejectFaceFor.Add("900000003");

        var failure = await Should.ThrowAsync<HikvisionException>(
            () => _client.WriteFaceAsync("900000003", Jpeg, replace: false, TestContext.Current.CancellationToken));

        failure.StatusCode.ShouldBe(400);
        failure.Body.ShouldContain("face quality too low");
        _faces.Count.ShouldBe(0, "rad etilganda ham havola o'chadi");
    }

    [Fact]
    public async Task Ulanmagan_terminal_503_vaqtinchalik()
    {
        _terminal.Online = false;

        var failure = await Should.ThrowAsync<HikvisionException>(
            () => _client.ReadSerialAsync(TestContext.Current.CancellationToken));

        failure.StatusCode.ShouldBe(503);
    }

    [Fact]
    public async Task SDK_xatosi_504_vaqtinchalik()
    {
        _terminal.SdkFails = true;

        var failure = await Should.ThrowAsync<HikvisionException>(
            () => _client.ReadClockAsync(TestContext.Current.CancellationToken));

        failure.StatusCode.ShouldBe(504);
    }

    [Fact]
    public async Task Hech_qachon_401_yoki_403_qaytmaydi()
    {
        // 401/403 agentni 10 daqiqaga kutishga solardi (AuthBackoff).
        foreach (var setup in new Action[] { () => _terminal.Online = false, () => { _terminal.Online = true; _terminal.SdkFails = true; } })
        {
            setup();

            using var response = await _http.GetAsync(new Uri("/ISAPI/System/deviceInfo", UriKind.Relative), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
            response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Jonli_kadr_tunnelga_bormaydi()
    {
        var picture = await _client.SnapshotAsync("101", TestContext.Current.CancellationToken);

        picture.ShouldBeNull();
        _terminal.Calls.ShouldBeEmpty();
    }

    public void Dispose() => _http.Dispose();
}
