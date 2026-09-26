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

    public AgentHarness()
    {
        // Toshkentda ertalab 08:00
        Clock = new ManualClock(new DateTimeOffset(2026, 9, 24, 3, 0, 0, TimeSpan.Zero));
        Terminal = new FakeTerminal(Clock);
        Bonvi = new FakeBonvi(Clock);

        _directory = Path.Combine(Path.GetTempPath(), "bonvi-agent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _options = Options.Create(new AgentOptions
        {
            ApiBaseUrl = "http://bonvi.test",
            DeviceToken = "token",
            DeviceHost = "terminal.test",
            DevicePassword = "parol",
            StatePath = Path.Combine(_directory, "agent-state.json"),
        });

        _terminalHttp = new HttpClient(Terminal, disposeHandler: false) { BaseAddress = new Uri("http://terminal.test/") };
        _bonviHttp = new HttpClient(Bonvi, disposeHandler: false) { BaseAddress = new Uri("http://bonvi.test/") };

        Device = new HikvisionClient(_terminalHttp, _options);
        Worker = NewWorker();
    }

    public ManualClock Clock { get; }

    public FakeTerminal Terminal { get; }

    public FakeBonvi Bonvi { get; }

    public HikvisionClient Device { get; }

    public AgentWorker Worker { get; private set; }

    public string StatePath => _options.Value.StatePath;

    /// <summary>Agent qayta ishga tushdi — kursor holat faylidan o'qiladi.</summary>
    public void Restart()
    {
        Worker.Dispose();
        Worker = NewWorker();
    }

    /// <summary>Hodisalar halqasining bitta aylanishi.</summary>
    public Task ForwardAsync() => Worker.ForwardEventsAsync(TestContext.Current.CancellationToken);

    /// <summary>Ro'yxat halqasidagi soat o'lchovi.</summary>
    public Task MeasureClockAsync() => Worker.ReportClockAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Agentning halqasi kabi: <paramref name="cycles"/> marta, har biri
    /// orasida <paramref name="step"/> — va har aylanishda terminaldan bitta o'tish.
    /// </summary>
    public async Task PassesWithCyclesAsync(int cycles, TimeSpan step)
    {
        for (var index = 0; index < cycles; index++)
        {
            Clock.Advance(step);
            Terminal.Pass("900000001");
            await ForwardAsync();
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
        => new(Device, new BonviAgentClient(_bonviHttp), _options, NullLogger<AgentWorker>.Instance, Clock);
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

    /// <summary>Shu tartibdagi hodisa so'rovidan boshlab terminal javob bermaydi.</summary>
    public int? FailFromEventRequest { get; set; }

    public int EventRequests { get; private set; }

    public List<string> Persons { get; } = [];

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
    public void Pass(string personNo) => _log.Add(new LogEntry(_nextSerial++, Now, personNo));

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
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return request.RequestUri!.AbsolutePath switch
        {
            "/ISAPI/AccessControl/AcsEvent" => SearchEvents(body),
            "/ISAPI/AccessControl/UserInfo/Search" => SearchPersons(body),
            "/ISAPI/System/time" => Text(
                $"<Time><localTime>{Now.ToString(TimeFormat, CultureInfo.InvariantCulture)}</localTime></Time>"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private HttpResponseMessage SearchEvents(string body)
    {
        EventRequests++;

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

        var matches = _log
            .Where(entry => entry.Time >= start && entry.Time <= end)
            .OrderBy(entry => entry.Time)
            .ThenBy(entry => entry.Serial)
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
                ["numOfFace"] = 1,
            }).ToList(),
        };

        AddStatus(payload, position + page.Count, Persons.Count);

        return Json(new Dictionary<string, object?> { ["UserInfoSearch"] = payload });
    }

    private void AddStatus(Dictionary<string, object?> payload, int read, int total)
    {
        if (!ReportsSearchStatus)
        {
            return;
        }

        payload["responseStatusStrg"] = read < total ? "MORE" : total == 0 ? "NO MATCH" : "OK";
        payload["totalMatches"] = total;
    }

    private static HttpResponseMessage Json(object value) => Text(JsonSerializer.Serialize(value));

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(text, Encoding.UTF8),
    };

    private sealed record LogEntry(long Serial, DateTimeOffset Time, string? PersonNo);
}

/// <summary>
/// Soxta Bonvi API — qabul yo'li va soat hisoboti.
/// </summary>
/// <remarks>
/// Serverning ikki qoidasini takrorlaydi: takror kalit (<c>eventId</c>)
/// jimgina tashlanadi, vaqti 30 kundan uzoq hodisa esa BUTUN so'rovni
/// 400 bilan yiqitadi (<c>AttendanceRules.MaxClockSkewDays</c>).
/// </remarks>
internal sealed class FakeBonvi(ManualClock clock) : HttpMessageHandler
{
    /// <summary>Internet yo'q — har so'rov tarmoq xatosi bilan tugaydi.</summary>
    public bool Offline { get; set; }

    /// <summary>Qabul qilingan o'tishlar: kalit → shaxs raqami.</summary>
    public Dictionary<string, string> Stored { get; } = new(StringComparer.Ordinal);

    public int Duplicates { get; private set; }

    public int RejectedRequests { get; private set; }

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

        return request.RequestUri!.AbsolutePath switch
        {
            "/api/attendance/ingest" => Ingest(body),
            "/api/attendance/agent/clock" => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
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

            if (Stored.TryAdd(key, item.GetProperty("personId").GetString()!))
            {
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
