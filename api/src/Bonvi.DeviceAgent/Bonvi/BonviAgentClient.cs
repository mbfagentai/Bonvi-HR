using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bonvi.DeviceAgent.Bonvi;

/// <summary>
/// Bonvi API bilan gaplashadigan yupqa mijoz — qurilma tokeni bilan.
/// </summary>
/// <remarks>
/// <para>
/// JWT, login va parol YO'Q (<c>AgentOptions.DeviceToken</c> izohi):
/// hamma chaqiruv <c>X-Bonvi-Device-Token</c> sarlavhasi bilan ketadi va
/// server qaysi qurilma gapirayotganini shundan biladi.
/// </para>
/// <para>
/// Terminal seriyasi (<see cref="TerminalSerial"/>) ham HAR so'rovda —
/// <c>X-Bonvi-Terminal-Serial</c>. Server tokenni birinchi ko'rgan seriyaga
/// bog'laydi va boshqasi bilan kelgan ro'yxat/hisobotni rad etadi: nusxalangan
/// agent sozlamasi boshqa terminalning odamlarini o'chira olmasin.
/// </para>
/// </remarks>
internal sealed class BonviAgentClient
{
    /// <summary>Terminal seriyasining sarlavhasi — serverdagi nom bilan bir xil.</summary>
    public const string TerminalSerialHeader = "X-Bonvi-Terminal-Serial";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    private string? _terminalSerial;

    public BonviAgentClient(HttpClient http) => _http = http;

    /// <summary>
    /// Terminalning seriyasi — ro'yxat halqasi har aylanishda o'qib qo'yadi.
    /// </summary>
    /// <remarks>
    /// Ikki halqa ikki oqimda: biri yozadi, ikkalasi o'qiydi. Satrga havola
    /// yozish atomar, <see cref="Volatile"/> esa eskirgan qiymatni ushlab
    /// qolmaslikni kafolatlaydi. Hali o'qilmagan bo'lsa <c>null</c> va sarlavha
    /// qo'yilmaydi — server bog'lanmagan qurilmada buni qabul qiladi.
    /// </remarks>
    public string? TerminalSerial
    {
        get => Volatile.Read(ref _terminalSerial);
        set => Volatile.Write(ref _terminalSerial, value);
    }

    /// <summary>Qurilmada bo'lishi kerak bo'lgan xodimlar.</summary>
    public async Task<RosterResponse> RosterAsync(CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, "api/attendance/agent/roster");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var roster = await response.Content
            .ReadFromJsonAsync<RosterResponse>(Json, cancellationToken)
            .ConfigureAwait(false);

        return roster ?? new RosterResponse(Guid.Empty, string.Empty, DateTimeOffset.MinValue, [], null, false);
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
        using var request = Request(HttpMethod.Get, $"api/attendance/agent/photo/{employeeId}");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Qurilmada nima bo'lganini serverga aytadi.</summary>
    public async Task ReportAsync(IReadOnlyList<ReportItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        await PostAsync("api/attendance/agent/report", items, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Solishtirish natijasi — ushlab qolingan o'chirishlar va begona odamlar.</summary>
    public Task StatusAsync(int heldRemovals, int foreignPersons, CancellationToken cancellationToken)
        => PostAsync("api/attendance/agent/status", new { heldRemovals, foreignPersons }, cancellationToken);

    /// <summary>Terminalning joriy vaqtini yuboradi — soat og'ishi shu yerdan hisoblanadi.</summary>
    public Task ReportClockAsync(DateTimeOffset deviceTime, CancellationToken cancellationToken)
        => PostAsync("api/attendance/agent/clock", new { deviceTime }, cancellationToken);

    /// <summary>O'tish hodisalarini qabul yo'liga yuboradi.</summary>
    /// <remarks>
    /// Marshrut agentniki EMAS, umumiy qabul yo'li: takrorni tashlash,
    /// surat va moslik qoidalari o'sha yerda bir marta yozilgan.
    /// </remarks>
    public async Task<IngestResult> IngestAsync(IReadOnlyList<IngestEvent> events, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "api/attendance/ingest");
        request.Content = JsonContent.Create(new { events }, options: Json);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<IngestResult>(Json, cancellationToken)
            .ConfigureAwait(false);

        return result ?? new IngestResult(false, 0, 0);
    }

    private async Task PostAsync<TBody>(string path, TBody body, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body, options: Json);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>So'rov — terminal seriyasi (bo'lsa) bilan.</summary>
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (TerminalSerial is { } serial)
        {
            request.Headers.TryAddWithoutValidation(TerminalSerialHeader, serial);
        }

        return request;
    }

    /// <summary>
    /// Muvaffaqiyatsiz javobni <see cref="BonviApiException"/> ga aylantiradi — xato KODI bilan.
    /// </summary>
    /// <remarks>
    /// <c>EnsureSuccessStatusCode</c> tanani tashlab yuboradi, halqaga esa aynan
    /// kod kerak: <c>davomat.terminal_mos_emas</c> (boshqa terminal — uzoq
    /// kutish) va <c>davomat.agent_royxat_band</c> (poyga — tez qayta urinish)
    /// ikkalasi ham 409.
    /// </remarks>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? code = null;

        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemBody>(Json, cancellationToken)
                .ConfigureAwait(false);

            code = problem?.Title;
        }
        catch (JsonException)
        {
            // Tana ProblemDetails emas (masalan proksi sahifasi) — faqat holat.
        }
        catch (NotSupportedException)
        {
            // Tana JSON emas.
        }

        throw new BonviApiException(response.StatusCode, code);
    }

    private sealed record ProblemBody(string? Title);
}

/// <summary>Bonvi API so'rovni rad etdi.</summary>
/// <remarks>
/// <see cref="HttpRequestException"/> dan meros: tarmoq xatosi bilan bir xil
/// ushlanadi, lekin <see cref="Code"/> orqali sababini ham aytadi.
/// </remarks>
internal sealed class BonviApiException(HttpStatusCode status, string? code)
    : HttpRequestException($"Bonvi {(int)status}: {code ?? "-"}", inner: null, status)
{
    /// <summary>Server qaytargan xato kodi (<c>title</c>) — bo'lsa.</summary>
    public string? Code { get; } = code;
}

/// <summary>Ro'yxatning bitta qatori.</summary>
/// <remarks>
/// <c>Hold</c> — qurilma aynan shu belgini rad etgan: belgi o'zgarguncha
/// YOZILMAYDI va O'CHIRILMAYDI ham.
/// </remarks>
internal sealed record RosterEntry(
    Guid EmployeeId,
    string PersonNo,
    string FullName,
    string? PhotoVersion,
    string Version,
    bool Force,
    bool Hold = false);

/// <summary>Kerakli ro'yxat.</summary>
/// <remarks>
/// <c>RemoveNumbers</c> — server ajratgan, ro'yxatda yo'q raqamlar; <c>null</c> —
/// eski server, u holda Bonvi fazasidagi har qanday ortiqcha raqam o'chiriladi
/// (eski qoida). <c>AllowMassRemoval</c> — HR ommaviy o'chirishni tasdiqlagan.
/// </remarks>
internal sealed record RosterResponse(
    Guid DeviceId,
    string DeviceName,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RosterEntry> Entries,
    IReadOnlyList<string>? RemoveNumbers = null,
    bool AllowMassRemoval = false);

/// <summary>Agent qurilmada nima qilgani.</summary>
internal enum AgentEnrollmentAction
{
    Upsert = 1,
    Remove = 2,
}

/// <summary>Hisobotning bitta qatori.</summary>
/// <remarks>
/// <c>Rejected</c> — xato QAT'IY: qurilma yoki rasm qoidasi aynan shu belgini
/// rad etdi va server uni belgi o'zgarguncha qayta bermaydi. Tarmoq xatosida
/// <c>false</c>.
/// </remarks>
internal sealed record ReportItem(
    Guid EmployeeId,
    string? PersonNo,
    AgentEnrollmentAction Action,
    bool Ok,
    string? Version,
    string? Error,
    bool Rejected = false);

/// <summary>Qabul yo'liga ketadigan bitta hodisa.</summary>
/// <remarks>
/// <c>Minor</c> — terminal jurnalidagi kichik tur: server rad etilgan
/// urinishni ikkinchi marta tekshiradi (agent ularni allaqachon tashlaydi).
/// </remarks>
internal sealed record IngestEvent(
    string PersonId,
    string OccurredAt,
    string Direction,
    string Method,
    string EventId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PhotoBase64 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Minor = null);

internal sealed record IngestResult(bool Ok, int Stored, int Duplicates);
