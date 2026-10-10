using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.IsupGateway.Faces;
using Bonvi.IsupGateway.Tunnel;

namespace Bonvi.IsupGateway.Tests;

/// <summary>
/// Soxta terminal — ISUP tunnelining NARIGI tomoni.
/// </summary>
/// <remarks>
/// <para>
/// HTTP emas: tunnel kabi faqat usul, yo'l va matn tanasini oladi va faqat
/// tanani qaytaradi (HTTP holati yo'q). Xato ham tanada —
/// <c>ResponseStatus</c> bilan, haqiqiy terminaldagidek.
/// </para>
/// <para>
/// Yuz yozishda terminal rasmni <c>faceURL</c> dan O'ZI yuklab oladi — bu
/// yerda <see cref="FacePictureStore"/> dan, aynan shlyuz bergan havola
/// bo'yicha. Havola noto'g'ri yoki eskirgan bo'lsa — rad.
/// </para>
/// </remarks>
internal sealed class FakeIsupTerminal(TimeProvider clock, FacePictureStore faces, string faceUrlBase) : IIsapiTunnel
{
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:sszzz";

    private readonly List<(long Serial, DateTimeOffset Time, string PersonNo)> _log = [];
    private long _nextSerial = 1;

    public bool Online { get; set; } = true;

    /// <summary>SDK chaqiruvi muvaffaqiyatsiz (masalan muddat tugadi).</summary>
    public bool SdkFails { get; set; }

    public string Serial { get; set; } = "DS-K1T341AMF20231019V030700ENJ80796745";

    public List<(string Method, string Path, string Body)> Calls { get; } = [];

    public List<string> Persons { get; } = [];

    /// <summary>Qidiruvda ko'rinmaydigan, lekin mavjud raqamlar (poyga holati).</summary>
    public HashSet<string> HiddenFromSearch { get; } = new(StringComparer.Ordinal);

    public HashSet<string> WithFace { get; } = new(StringComparer.Ordinal);

    public HashSet<string> RejectFaceFor { get; } = new(StringComparer.Ordinal);

    /// <summary>Terminal yuklab olgan rasmlar: raqam → baytlar.</summary>
    public Dictionary<string, byte[]> Downloaded { get; } = new(StringComparer.Ordinal);

    public void Pass(string personNo)
    {
        var now = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        _log.Add((_nextSerial++, now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)), personNo));
    }

    public Task<TunnelResponse> SendAsync(
        string deviceId,
        string method,
        string pathAndQuery,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        if (!Online)
        {
            return Task.FromResult(TunnelResponse.Offline(deviceId));
        }

        if (SdkFails)
        {
            return Task.FromResult(TunnelResponse.Failed("ISUP tunnel: SDK xatosi 10"));
        }

        var text = Encoding.UTF8.GetString(body.Span);
        Calls.Add((method, pathAndQuery, text));

        var question = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = question < 0 ? pathAndQuery : pathAndQuery[..question];

        var reply = path switch
        {
            "/ISAPI/System/deviceInfo" =>
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><DeviceInfo><deviceName>Access Controller</deviceName><model>DS-K1T341AMF</model><serialNumber>{Serial}</serialNumber></DeviceInfo>",
            "/ISAPI/System/time" =>
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Time><timeMode>manual</timeMode><localTime>{Now().ToString(TimeFormat, CultureInfo.InvariantCulture)}</localTime></Time>",
            "/ISAPI/AccessControl/UserInfo/Search" => SearchPersons(text),
            "/ISAPI/AccessControl/UserInfo/Record" => WritePerson(text, create: true),
            "/ISAPI/AccessControl/UserInfo/Modify" => WritePerson(text, create: false),
            "/ISAPI/AccessControl/UserInfo/Delete" => DeletePerson(text),
            "/ISAPI/Intelligent/FDLib/FaceDataRecord" => WriteFace(text),
            "/ISAPI/Intelligent/FDLib/FDSetUp" => WriteFace(text),
            "/ISAPI/AccessControl/AcsEvent" => SearchEvents(text),
            _ => Status(4, "Invalid Operation", "notSupport"),
        };

        return Task.FromResult(new TunnelResponse(TunnelOutcome.Ok, reply));
    }

    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }

    private string SearchPersons(string body)
    {
        using var json = JsonDocument.Parse(body);
        var position = json.RootElement.GetProperty("UserInfoSearchCond").GetProperty("searchResultPosition").GetInt32();

        var visible = Persons.Where(p => !HiddenFromSearch.Contains(p)).ToList();
        var page = visible.Skip(position).Take(30).ToList();
        var read = position + page.Count;

        return JsonSerializer.Serialize(new
        {
            UserInfoSearch = new
            {
                searchID = "sinov",
                responseStatusStrg = read < visible.Count ? "MORE" : visible.Count == 0 ? "NO MATCH" : "OK",
                totalMatches = visible.Count,
                UserInfo = page.Select(number => new { employeeNo = number, name = "Sinov " + number, numOfFace = WithFace.Contains(number) ? 1 : 0 }),
            },
        });
    }

    private string WritePerson(string body, bool create)
    {
        using var json = JsonDocument.Parse(body);
        var number = json.RootElement.GetProperty("UserInfo").GetProperty("employeeNo").GetString()!;

        if (create)
        {
            if (Persons.Contains(number))
            {
                return Status(6, "Invalid Content", "employeeNoAlreadyExist");
            }

            Persons.Add(number);
        }

        return Status(1, "OK");
    }

    private string DeletePerson(string body)
    {
        using var json = JsonDocument.Parse(body);
        var number = json.RootElement.GetProperty("UserInfoDelCond").GetProperty("EmployeeNoList")[0].GetProperty("employeeNo").GetString()!;

        Persons.Remove(number);
        WithFace.Remove(number);

        return Status(1, "OK");
    }

    /// <summary>JSON shakli (ISUP): <c>{"faceURL":…,"faceLibType":"blackFD","FDID":"1","FPID":…}</c>.</summary>
    private string WriteFace(string body)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        if (!root.TryGetProperty("FPID", out var fpid) || !root.TryGetProperty("faceURL", out var url))
        {
            return Status(6, "Invalid Content", "MessageParametersLack");
        }

        var number = fpid.GetString()!;

        if (RejectFaceFor.Contains(number))
        {
            return Status(6, "Invalid Content", "face quality too low");
        }

        // Terminal rasmni havoladan O'ZI yuklab oladi.
        var address = url.GetString() ?? string.Empty;

        if (!address.StartsWith(faceUrlBase, StringComparison.Ordinal)
            || !address.EndsWith(".jpg", StringComparison.Ordinal)
            || !faces.TryGet(address[faceUrlBase.Length..^4], out var jpeg))
        {
            return Status(6, "Invalid Content", "downloadPictureFailed");
        }

        Downloaded[number] = jpeg;
        WithFace.Add(number);

        return Status(1, "OK");
    }

    private string SearchEvents(string body)
    {
        using var json = JsonDocument.Parse(body);
        var cond = json.RootElement.GetProperty("AcsEventCond");

        var position = cond.GetProperty("searchResultPosition").GetInt32();
        var start = DateTimeOffset.Parse(cond.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(cond.GetProperty("endTime").GetString()!, CultureInfo.InvariantCulture);

        var matches = _log.Where(e => e.Time >= start && e.Time <= end).OrderBy(e => e.Time).ThenBy(e => e.Serial).ToList();
        var page = matches.Skip(position).Take(30).ToList();
        var read = position + page.Count;

        return JsonSerializer.Serialize(new
        {
            AcsEvent = new
            {
                searchID = "sinov",
                responseStatusStrg = read < matches.Count ? "MORE" : matches.Count == 0 ? "NO MATCH" : "OK",
                totalMatches = matches.Count,
                InfoList = page.Select(e => new
                {
                    serialNo = e.Serial,
                    time = e.Time.ToString(TimeFormat, CultureInfo.InvariantCulture),
                    employeeNoString = e.PersonNo,
                    currentVerifyMode = "face",
                    major = 5,
                    minor = 75,
                }),
            },
        });
    }

    private static string Status(int code, string text, string? sub = null)
        => JsonSerializer.Serialize(new { requestURL = "", statusCode = code, statusString = text, subStatusCode = sub ?? "ok" });
}

/// <summary>Soxta Bonvi API — agent yo'llari (ro'yxat, rasm, hisobot, holat, soat, qabul).</summary>
internal sealed class FakeBonviApi : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public object Roster { get; set; } = new { deviceId = Guid.Empty, deviceName = "sinov", generatedAt = DateTimeOffset.UnixEpoch, entries = Array.Empty<object>() };

    public Dictionary<Guid, byte[]> Photos { get; } = [];

    public List<JsonElement> ReportItems { get; } = [];

    /// <summary>Qabul qilingan o'tishlar: kalit → shaxs raqami.</summary>
    public Dictionary<string, string> Stored { get; } = new(StringComparer.Ordinal);

    /// <summary>So'rovlardagi terminal seriyasi sarlavhasi.</summary>
    public List<string?> Serials { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;

        Serials.Add(request.Headers.TryGetValues(BonviAgentClient.TerminalSerialHeader, out var values) ? values.First() : null);

        if (path.StartsWith("/api/attendance/agent/photo/", StringComparison.Ordinal))
        {
            return Photos.TryGetValue(Guid.Parse(path["/api/attendance/agent/photo/".Length..]), out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        switch (path)
        {
            case "/api/attendance/agent/roster":
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Roster, Web), Encoding.UTF8, "application/json"),
                };

            case "/api/attendance/agent/report":
                using (var json = JsonDocument.Parse(body))
                {
                    ReportItems.AddRange(json.RootElement.EnumerateArray().Select(item => item.Clone()));
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"accepted\":0,\"ignored\":0}", Encoding.UTF8, "application/json"),
                };

            case "/api/attendance/ingest":
                var stored = 0;

                using (var json = JsonDocument.Parse(body))
                {
                    foreach (var item in json.RootElement.GetProperty("events").EnumerateArray())
                    {
                        if (Stored.TryAdd(item.GetProperty("eventId").GetString()!, item.GetProperty("personId").GetString()!))
                        {
                            stored++;
                        }
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { ok = true, stored, duplicates = 0 }), Encoding.UTF8, "application/json"),
                };

            case "/api/attendance/agent/status":
            case "/api/attendance/agent/clock":
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
