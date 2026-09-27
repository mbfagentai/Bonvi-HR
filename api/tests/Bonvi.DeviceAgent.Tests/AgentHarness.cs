using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bonvi.DeviceAgent.Bonvi;
using Bonvi.DeviceAgent.Hikvision;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bonvi.DeviceAgent.Tests;

/// <summary>Qo'lda suriladigan soat — agentning «hozir»i.</summary>
internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
/// Agentni TARMOQSIZ ishga tushiradigan sinov stendi.
/// </summary>
/// <remarks>
/// Agent HAQIQIY <see cref="HikvisionClient"/> va <see cref="BonviAgentClient"/>
/// bilan yasaladi, ularning ostida esa soxta terminal va soxta server
/// turadi. Holat fayli vaqtinchalik jildda — <see cref="Restart"/> agentni
/// «qayta ishga tushirib», kursorni fayldan qayta o'qiydi.
/// </remarks>
internal sealed class AgentHarness : IDisposable
{
    private readonly string _directory;
    private readonly HttpClient _terminalHttp;
    private readonly HttpClient _bonviHttp;
    private readonly IOptions<AgentOptions> _options;

    public AgentHarness(Action<AgentOptions>? configure = null)
    {
        // Toshkentda ertalab 08:00
        Clock = new ManualClock(new DateTimeOffset(2026, 9, 24, 3, 0, 0, TimeSpan.Zero));
        Terminal = new FakeTerminal(Clock);
        Bonvi = new FakeBonvi(Clock);

        _directory = Path.Combine(Path.GetTempPath(), "bonvi-agent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        var options = new AgentOptions
        {
            ApiBaseUrl = "http://bonvi.test",
            DeviceToken = "token",
            DeviceHost = "terminal.test",
            DevicePassword = "parol",
            StatePath = Path.Combine(_directory, "agent-state.json"),
        };

        configure?.Invoke(options);
        _options = Options.Create(options);

        _terminalHttp = new HttpClient(Terminal, disposeHandler: false) { BaseAddress = new Uri("http://terminal.test/") };
        _bonviHttp = new HttpClient(Bonvi, disposeHandler: false) { BaseAddress = new Uri("http://bonvi.test/") };

        Device = new HikvisionClient(_terminalHttp, _options);
        BonviClient = new BonviAgentClient(_bonviHttp);
        Worker = NewWorker();
    }

    public ManualClock Clock { get; }

    public FakeTerminal Terminal { get; }

    public FakeBonvi Bonvi { get; }

    public HikvisionClient Device { get; }

    public BonviAgentClient BonviClient { get; private set; }

    public AgentWorker Worker { get; private set; }

    public string StatePath => _options.Value.StatePath;

    /// <summary>Agent qayta ishga tushdi — kursor holat faylidan o'qiladi.</summary>
    public void Restart()
    {
        Worker.Dispose();
        BonviClient = new BonviAgentClient(_bonviHttp);
        Worker = NewWorker();
    }

    /// <summary>Hodisalar halqasining bitta aylanishi.</summary>
    public Task ForwardAsync() => Worker.ForwardEventsAsync(TestContext.Current.CancellationToken);

    /// <summary>Ro'yxat halqasidagi soat o'lchovi.</summary>
    public Task MeasureClockAsync() => Worker.ReportClockAsync(TestContext.Current.CancellationToken);

    /// <summary>Ro'yxat halqasining bitta aylanishi — solishtirish.</summary>
    public Task ReconcileAsync() => Worker.ReconcileAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Agentning halqasi kabi: <paramref name="cycles"/> marta, har biri
    /// orasida <paramref name="step"/> — va har aylanishda terminaldan bitta o'tish.
    /// </summary>
    public async Task PassesWithCyclesAsync(int cycles, TimeSpan step, bool swallowFailures = false)
    {
        for (var index = 0; index < cycles; index++)
        {
            Clock.Advance(step);
            Terminal.Pass("900000001");

            try
            {
                await ForwardAsync();
            }
            catch (HttpRequestException) when (swallowFailures)
            {
                // Halqa xatoni yutadi va keyingi aylanishda qayta uradi.
            }
        }
    }

    public void Dispose()
    {
        Worker.Dispose();
        _terminalHttp.Dispose();
        _bonviHttp.Dispose();
        Terminal.Dispose();
        Bonvi.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Vaqtinchalik jild — qolsa ham zarari yo'q.
        }
    }

    private AgentWorker NewWorker()
        => new(Device, BonviClient, _options, NullLogger<AgentWorker>.Instance, Clock);
}

/// <summary>
/// Soxta terminal — ISAPI ning agent ishlatadigan qismi.
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy firmware'ning ikki odatini ataylab takrorlaydi: sahifaga
/// <c>maxResults</c> dan KAM beradi (<see cref="PageCap"/>, sukut 30) va
/// o'z soati bilan yuradi (<see cref="ClockOffset"/>) — hodisa vaqti ham,
/// qidiruv oralig'i ham shu soat bo'yicha.
/// </para>
/// <para>
/// Odamlar jadvali ham bor: yozish (<c>Record</c>/<c>Modify</c>), o'chirish,
/// yuz yozish — har biri sanaladi va kerak bo'lsa rad etiladi.
/// </para>
/// </remarks>
internal sealed class FakeTerminal(ManualClock clock) : HttpMessageHandler
{
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:sszzz";

    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly List<LogEntry> _log = [];
    private long _nextSerial = 1;

    /// <summary>Terminal soati minus haqiqiy vaqt.</summary>
    public TimeSpan ClockOffset { get; set; }

    /// <summary>Bir sahifada eng ko'p nechta yozuv beradi.</summary>
    public int PageCap { get; set; } = 30;

    /// <summary><c>responseStatusStrg</c> va <c>totalMatches</c> ni beradimi (eski firmware bermaydi).</summary>
    public bool ReportsSearchStatus { get; set; } = true;

    /// <summary>Firmware <c>timeReverseOrder</c> ni e'tiborsiz qoldirib, yangidan eskiga beradi.</summary>
    public bool Descending { get; set; }

    /// <summary>Shu tartibdagi hodisa so'rovidan boshlab terminal javob bermaydi.</summary>
    public int? FailFromEventRequest { get; set; }

    /// <summary>Terminal parolni rad etadi — har so'rovga 401.</summary>
    public bool Unauthorized { get; set; }

    /// <summary>Terminal umuman javob bermaydi (tarmoq xatosi).</summary>
    public bool Offline { get; set; }

    /// <summary>Terminalning seriya raqami — <c>/ISAPI/System/deviceInfo</c>.</summary>
    public string Serial { get; set; } = "DS-K1T341AMF20210915V030700ENG12345678";

    public int EventRequests { get; private set; }

    /// <summary>Oxirgi hodisa so'rovining tanasi — so'ralgan tartibni tekshirish uchun.</summary>
    public string? LastEventQuery { get; private set; }

    /// <summary>Terminaldagi odamlar (raqamlar).</summary>
    public List<string> Persons { get; } = [];

    /// <summary>Yuzi YO'Q odamlar — qolganlarida bittadan yuz bor.</summary>
    public HashSet<string> WithoutFace { get; } = new(StringComparer.Ordinal);

    /// <summary>Yuzini terminal rad etadigan raqamlar («face quality too low»).</summary>
    public HashSet<string> RejectFaceFor { get; } = new(StringComparer.Ordinal);

    /// <summary>O'chirilishini terminal rad etadigan raqamlar.</summary>
    public HashSet<string> RejectDeleteFor { get; } = new(StringComparer.Ordinal);

    public List<string> PersonWrites { get; } = [];

    public List<string> FaceWrites { get; } = [];

    public List<string> Deletes { get; } = [];

    /// <summary>Terminalning «hozir»i — soniyagacha, Toshkent siljishi bilan.</summary>
    public DateTimeOffset Now
    {
        get
        {
            var now = (clock.GetUtcNow() + ClockOffset).ToOffset(Tashkent);
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        }
    }

    /// <summary>Yuz bilan o'tish — terminal jurnaliga yoziladi.</summary>
    public void Pass(string personNo, int minor = 75) => _log.Add(new LogEntry(_nextSerial++, Now, personNo, minor));

    /// <summary>Jurnal tozalandi — raqamlar 1 dan qayta boshlanadi.</summary>
    public void ClearLog()
    {
        _log.Clear();
        _nextSerial = 1;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("terminal javob bermadi");
        }

        if (Unauthorized)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(string.Empty) };
        }

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var query = request.RequestUri!.Query;

        return request.RequestUri!.AbsolutePath switch
        {
            "/ISAPI/AccessControl/AcsEvent" => SearchEvents(body),
            "/ISAPI/AccessControl/UserInfo/Search" => SearchPersons(body),
            "/ISAPI/AccessControl/UserInfo/Record" => WritePerson(body, create: true),
            "/ISAPI/AccessControl/UserInfo/Modify" => WritePerson(body, create: false),
            "/ISAPI/AccessControl/UserInfo/Delete" => DeletePerson(body),
            "/ISAPI/Intelligent/FDLib/FaceDataRecord" => WriteFace(FaceNumber(body)),
            "/ISAPI/Intelligent/FDLib/FDSetUp" => WriteFace(QueryValue(query, "FPID")),
            "/ISAPI/System/time" => Text(
                $"<Time><localTime>{Now.ToString(TimeFormat, CultureInfo.InvariantCulture)}</localTime></Time>"),
            "/ISAPI/System/deviceInfo" => Text(
                $"<DeviceInfo><deviceName>MinMoe</deviceName><model>DS-K1T341AMF</model><serialNumber>{Serial}</serialNumber></DeviceInfo>"),
            "/ISAPI/Streaming/channels/101/picture" => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]),
            },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage SearchEvents(string body)
    {
        EventRequests++;
        LastEventQuery = body;

        if (FailFromEventRequest is { } fail && EventRequests >= fail)
        {
            throw new HttpRequestException("terminal javob bermadi");
        }

        using var json = JsonDocument.Parse(body);
        var cond = json.RootElement.GetProperty("AcsEventCond");

        var position = cond.GetProperty("searchResultPosition").GetInt32();
        var max = cond.GetProperty("maxResults").GetInt32();
        var start = DateTimeOffset.Parse(cond.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(cond.GetProperty("endTime").GetString()!, CultureInfo.InvariantCulture);

        var matching = _log.Where(entry => entry.Time >= start && entry.Time <= end);

        var matches = (Descending
                ? matching.OrderByDescending(entry => entry.Time).ThenByDescending(entry => entry.Serial)
                : matching.OrderBy(entry => entry.Time).ThenBy(entry => entry.Serial))
            .ToList();

        var page = matches.Skip(position).Take(Math.Min(max, PageCap)).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["searchID"] = "sinov",
            ["InfoList"] = page.Select(entry => new Dictionary<string, object?>
            {
                ["serialNo"] = entry.Serial,
                ["time"] = entry.Time.ToString(TimeFormat, CultureInfo.InvariantCulture),
                ["employeeNoString"] = entry.PersonNo,
                ["currentVerifyMode"] = "face",
                ["major"] = 5,
                ["minor"] = entry.Minor,
            }).ToList(),
        };

        AddStatus(payload, position + page.Count, matches.Count);

        return Json(new Dictionary<string, object?> { ["AcsEvent"] = payload });
    }

    private HttpResponseMessage SearchPersons(string body)
    {
        using var json = JsonDocument.Parse(body);
        var cond = json.RootElement.GetProperty("UserInfoSearchCond");

        var position = cond.GetProperty("searchResultPosition").GetInt32();
        var max = cond.GetProperty("maxResults").GetInt32();

        var page = Persons.Skip(position).Take(Math.Min(max, PageCap)).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["searchID"] = "sinov",
            ["UserInfo"] = page.Select(number => new Dictionary<string, object?>
            {
                ["employeeNo"] = number,
                ["name"] = "Sinov " + number,
                ["numOfFace"] = WithoutFace.Contains(number) ? 0 : 1,
            }).ToList(),
        };

        AddStatus(payload, position + page.Count, Persons.Count);

        return Json(new Dictionary<string, object?> { ["UserInfoSearch"] = payload });
    }

    private HttpResponseMessage WritePerson(string body, bool create)
    {
        using var json = JsonDocument.Parse(body);
        var number = json.RootElement.GetProperty("UserInfo").GetProperty("employeeNo").GetString()!;

        PersonWrites.Add(number);

        if (create && !Persons.Contains(number))
        {
            Persons.Add(number);
            WithoutFace.Add(number);
        }

        return Ok();
    }

    private HttpResponseMessage DeletePerson(string body)
    {
        using var json = JsonDocument.Parse(body);
        var number = json.RootElement
            .GetProperty("UserInfoDelCond")
            .GetProperty("EmployeeNoList")[0]
            .GetProperty("employeeNo")
            .GetString()!;

        if (RejectDeleteFor.Contains(number))
        {
            return Refuse("deviceBusy");
        }

        Deletes.Add(number);
        Persons.Remove(number);
        WithoutFace.Remove(number);

        return Ok();
    }

    private HttpResponseMessage WriteFace(string? number)
    {
        if (number is null)
        {
            return Refuse("MessageParametersLack");
        }

        if (RejectFaceFor.Contains(number))
        {
            return Refuse("face quality too low");
        }

        FaceWrites.Add(number);
        WithoutFace.Remove(number);

        return Ok();
    }

    /// <summary>Multipart tanadagi <c>FPID</c> — yuz qaysi odamga.</summary>
    private static string? FaceNumber(string body)
    {
        const string marker = "\"FPID\":\"";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = body.IndexOf('"', start);
        return end < 0 ? null : body[start..end];
    }

    private static string? QueryValue(string query, string name)
        => query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0] == name)
            .Select(pair => Uri.UnescapeDataString(pair[1]))
            .FirstOrDefault();

    private void AddStatus(Dictionary<string, object?> payload, int read, int total)
    {
        if (!ReportsSearchStatus)
        {
            return;
        }

        payload["responseStatusStrg"] = read < total ? "MORE" : total == 0 ? "NO MATCH" : "OK";
        payload["totalMatches"] = total;
    }

    private static HttpResponseMessage Ok() => Json(new { statusCode = 1, statusString = "OK" });

    private static HttpResponseMessage Refuse(string reason) => new(HttpStatusCode.BadRequest)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new { statusCode = 6, statusString = "Invalid Content", subStatusCode = reason }),
            Encoding.UTF8),
    };

    private static HttpResponseMessage Json(object value) => Text(JsonSerializer.Serialize(value));

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(text, Encoding.UTF8),
    };

    private sealed record LogEntry(long Serial, DateTimeOffset Time, string? PersonNo, int Minor);
}

/// <summary>
/// Soxta Bonvi API — qabul yo'li, soat, ro'yxat, rasm, hisobot va holat.
/// </summary>
/// <remarks>
/// Serverning ikki qoidasini takrorlaydi: takror kalit (<c>eventId</c>)
/// jimgina tashlanadi, vaqti 30 kundan uzoq hodisa esa BUTUN so'rovni
/// 400 bilan yiqitadi (<c>AttendanceRules.MaxClockSkewDays</c>).
/// </remarks>
internal sealed class FakeBonvi(ManualClock clock) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Internet yo'q — har so'rov tarmoq xatosi bilan tugaydi.</summary>
    public bool Offline { get; set; }

    /// <summary>Qabul qilingan o'tishlar: kalit → shaxs raqami.</summary>
    public Dictionary<string, string> Stored { get; } = new(StringComparer.Ordinal);

    /// <summary>Qabul qilingan o'tishlarning surati bor-yo'qligi: kalit → surat bormi.</summary>
    public Dictionary<string, bool> StoredWithPhoto { get; } = new(StringComparer.Ordinal);

    /// <summary>Qabul yo'liga kelgan <c>minor</c> qiymatlari.</summary>
    public List<int?> Minors { get; } = [];

    public int Duplicates { get; private set; }

    public int RejectedRequests { get; private set; }

    /// <summary>Agent so'raganda beriladigan ro'yxat (JSON sifatida).</summary>
    public object Roster { get; set; } = new { deviceId = Guid.Empty, deviceName = "sinov", generatedAt = DateTimeOffset.UnixEpoch, entries = Array.Empty<object>() };

    /// <summary>Xodim rasmlari; yo'q bo'lsa 404.</summary>
    public Dictionary<Guid, byte[]> Photos { get; } = [];

    /// <summary>Shu xodimlarning rasmi so'ralganda server 503 qaytaradi.</summary>
    public HashSet<Guid> PhotoFailures { get; } = [];

    public int PhotoRequests { get; private set; }

    /// <summary>Kelgan hisobot so'rovlari — har biri o'z qatorlari bilan.</summary>
    public List<List<JsonElement>> Reports { get; } = [];

    /// <summary>Kelgan holat hisobotlari (<c>heldRemovals</c>, <c>foreignPersons</c>).</summary>
    public List<(int Held, int Foreign)> Statuses { get; } = [];

    /// <summary>Har so'rovdagi terminal seriyasi sarlavhasi: yo'l → qiymat.</summary>
    public List<(string Path, string? Serial)> Serials { get; } = [];

    public IEnumerable<JsonElement> ReportItems => Reports.SelectMany(items => items);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("internet yo'q");
        }

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var path = request.RequestUri!.AbsolutePath;

        Serials.Add((path, request.Headers.TryGetValues(BonviAgentClient.TerminalSerialHeader, out var values) ? values.First() : null));

        if (path.StartsWith("/api/attendance/agent/photo/", StringComparison.Ordinal))
        {
            return Photo(Guid.Parse(path["/api/attendance/agent/photo/".Length..]));
        }

        return path switch
        {
            "/api/attendance/ingest" => Ingest(body),
            "/api/attendance/agent/clock" => new HttpResponseMessage(HttpStatusCode.NoContent),
            "/api/attendance/agent/roster" => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(Roster, Web), Encoding.UTF8, "application/json"),
            },
            "/api/attendance/agent/report" => Report(body),
            "/api/attendance/agent/status" => Status(body),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage Photo(Guid employeeId)
    {
        PhotoRequests++;

        if (PhotoFailures.Contains(employeeId))
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        return Photos.TryGetValue(employeeId, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage Report(string body)
    {
        using var json = JsonDocument.Parse(body);
        Reports.Add([.. json.RootElement.EnumerateArray().Select(item => item.Clone())]);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"accepted\":0,\"ignored\":0}", Encoding.UTF8, "application/json"),
        };
    }

    private HttpResponseMessage Status(string body)
    {
        using var json = JsonDocument.Parse(body);
        Statuses.Add((
            json.RootElement.GetProperty("heldRemovals").GetInt32(),
            json.RootElement.GetProperty("foreignPersons").GetInt32()));

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private HttpResponseMessage Ingest(string body)
    {
        using var json = JsonDocument.Parse(body);
        var events = json.RootElement.GetProperty("events").EnumerateArray().ToList();

        var now = clock.GetUtcNow();

        foreach (var item in events)
        {
            var occurred = DateTimeOffset.Parse(item.GetProperty("occurredAt").GetString()!, CultureInfo.InvariantCulture);

            if (Math.Abs((now - occurred).TotalDays) > 30)
            {
                RejectedRequests++;
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
        }

        var stored = 0;
        var duplicates = 0;

        foreach (var item in events)
        {
            var key = item.GetProperty("eventId").GetString()!;

            Minors.Add(item.TryGetProperty("minor", out var minor) ? minor.GetInt32() : null);

            if (Stored.TryAdd(key, item.GetProperty("personId").GetString()!))
            {
                StoredWithPhoto[key] = item.TryGetProperty("photoBase64", out _);
                stored++;
            }
            else
            {
                duplicates++;
            }
        }

        Duplicates += duplicates;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { ok = true, stored, duplicates }),
                Encoding.UTF8,
                "application/json"),
        };
    }
}
