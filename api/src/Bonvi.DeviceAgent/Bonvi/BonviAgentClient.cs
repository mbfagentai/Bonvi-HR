using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bonvi.DeviceAgent.Bonvi;

/// <summary>
/// Bonvi API bilan gaplashadigan yupqa mijoz — qurilma tokeni bilan.
/// </summary>
/// <remarks>
/// JWT, login va parol YO'Q (<c>AgentOptions.DeviceToken</c> izohi):
/// hamma chaqiruv <c>X-Bonvi-Device-Token</c> sarlavhasi bilan ketadi va
/// server qaysi qurilma gapirayotganini shundan biladi.
/// </remarks>
internal sealed class BonviAgentClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public BonviAgentClient(HttpClient http) => _http = http;

    /// <summary>Qurilmada bo'lishi kerak bo'lgan xodimlar.</summary>
    public async Task<RosterResponse> RosterAsync(CancellationToken cancellationToken)
    {
        var roster = await _http
            .GetFromJsonAsync<RosterResponse>("api/attendance/agent/roster", Json, cancellationToken)
            .ConfigureAwait(false);

        return roster ?? new RosterResponse(Guid.Empty, string.Empty, DateTimeOffset.MinValue, []);
    }

    /// <summary>
    /// Xodim rasmi.
    /// </summary>
    /// <remarks>
    /// Rasm yo'q bo'lsa (<c>404</c>) — <c>null</c>, istisno EMAS: bu
    /// kutilgan holat va halqa uni xato sifatida yozmasligi kerak.
    /// </remarks>
    public async Task<byte[]?> PhotoAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await _http
            .GetAsync(new Uri($"api/attendance/agent/photo/{employeeId}", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Qurilmada nima bo'lganini serverga aytadi.</summary>
    public async Task ReportAsync(IReadOnlyList<ReportItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var response = await _http
            .PostAsJsonAsync("api/attendance/agent/report", items, Json, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>Terminalning joriy vaqtini yuboradi — soat og'ishi shu yerdan hisoblanadi.</summary>
    public async Task ReportClockAsync(DateTimeOffset deviceTime, CancellationToken cancellationToken)
    {
        using var response = await _http
            .PostAsJsonAsync("api/attendance/agent/clock", new { deviceTime }, Json, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>O'tish hodisalarini qabul yo'liga yuboradi.</summary>
    /// <remarks>
    /// Marshrut agentniki EMAS, umumiy qabul yo'li: takrorni tashlash,
    /// surat va moslik qoidalari o'sha yerda bir marta yozilgan.
    /// </remarks>
    public async Task<IngestResult> IngestAsync(IReadOnlyList<IngestEvent> events, CancellationToken cancellationToken)
    {
        using var response = await _http
            .PostAsJsonAsync("api/attendance/ingest", new { events }, Json, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<IngestResult>(Json, cancellationToken)
            .ConfigureAwait(false);

        return result ?? new IngestResult(false, 0, 0);
    }
}

/// <summary>Ro'yxatning bitta qatori.</summary>
internal sealed record RosterEntry(
    Guid EmployeeId,
    string PersonNo,
    string FullName,
    string? PhotoVersion,
    string Version,
    bool Force);

internal sealed record RosterResponse(
    Guid DeviceId,
    string DeviceName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RosterEntry> Entries);

/// <summary>Agent qurilmada nima qilgani.</summary>
internal enum AgentEnrollmentAction
{
    Upsert = 1,
    Remove = 2,
}

internal sealed record ReportItem(
    Guid EmployeeId,
    string? PersonNo,
    AgentEnrollmentAction Action,
    bool Ok,
    string? Version,
    string? Error);

internal sealed record IngestEvent(
    string PersonId,
    string OccurredAt,
    string Direction,
    string Method,
    string EventId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PhotoBase64 = null);

internal sealed record IngestResult(bool Ok, int Stored, int Duplicates);
