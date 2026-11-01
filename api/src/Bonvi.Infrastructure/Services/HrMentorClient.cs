using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// HRMentor ning HTTP mijozi (SPEC-blankalar-hrmentor §3) — typed <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kalit jurnalga tushmasligi uchun</b> <c>HttpClient</c> ning o'z so'rov jurnali o'chiq
/// (<c>RemoveAllLoggers</c>, ro'yxatga olishda), bu sinf esa hech narsa yozmaydi — xatolar
/// KOD bo'lib qaytadi (<see cref="HrMentorErrors"/>).
/// </para>
/// <para>
/// <b>Yo'naltirish (3xx) kuzatilmaydi</b> (ro'yxatga olishda <c>AllowAutoRedirect = false</c>):
/// kalit boshqa manzilga yoki <c>http</c> ga ketib qolmasin. 3xx — «javob tushunarsiz».
/// </para>
/// <para>
/// <b>Vaqt:</b> JSON so'rovlari <see cref="JsonTimeout"/>, fayl yuklash
/// <see cref="FileTimeout"/> (<c>HttpClient.Timeout</c> shu kattasiga qo'yiladi). JSON javobi
/// <see cref="MaxJsonBytes"/> dan katta bo'lsa o'qilmaydi.
/// </para>
/// </remarks>
public sealed class HrMentorClient : IHrMentorClient
{
    /// <summary><c>IHttpClientFactory</c> dagi nomi.</summary>
    public const string HttpClientName = "hrmentor";

    /// <summary>JSON javobining eng katta hajmi — 2 MB.</summary>
    public const int MaxJsonBytes = 2 * 1024 * 1024;

    /// <summary>JSON so'rovining vaqti.</summary>
    public static readonly TimeSpan JsonTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Fayl yuklashning vaqti (<c>HttpClient.Timeout</c>).</summary>
    public static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(120);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public HrMentorClient(HttpClient http) => _http = http;

    public async Task<Result> PushCatalogAsync(
        HrMentorConnection connection,
        string formCode,
        string catalogJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Put, connection, $"digital-forms/{Uri.EscapeDataString(formCode)}/catalog");
        request.Content = new StringContent(catalogJson, Encoding.UTF8, "application/json");

        var answer = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
        return answer.IsFailure ? answer.Error : Result.Success();
    }

    public async Task<Result<string?>> GetServedTextsAsync(
        HrMentorConnection connection,
        string formCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Get, connection, $"digital-forms/{Uri.EscapeDataString(formCode)}/texts");
        var answer = await SendJsonAsync(request, cancellationToken, notFoundIsEmpty: true).ConfigureAwait(false);

        return answer.IsFailure ? answer.Error : Result.Success(answer.Value);
    }

    public async Task<Result> PushProfileAsync(
        HrMentorConnection connection,
        string formCode,
        string profileJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Put, connection, $"digital-forms/{Uri.EscapeDataString(formCode)}/profile");
        request.Content = new StringContent(profileJson, Encoding.UTF8, "application/json");

        var answer = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
        return answer.IsFailure ? answer.Error : Result.Success();
    }

    public async Task<Result<string?>> GetPublishedDefinitionAsync(
        HrMentorConnection connection,
        string formCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Get, connection, $"digital-forms/{Uri.EscapeDataString(formCode)}/definition");
        var answer = await SendJsonAsync(request, cancellationToken, notFoundIsEmpty: true).ConfigureAwait(false);

        return answer.IsFailure ? answer.Error : Result.Success(answer.Value);
    }

    public async Task<Result<string?>> GetDraftDefinitionAsync(
        HrMentorConnection connection,
        string formCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Get, connection, $"digital-forms/{Uri.EscapeDataString(formCode)}/draft");
        var answer = await SendJsonAsync(request, cancellationToken, notFoundIsEmpty: true).ConfigureAwait(false);

        return answer.IsFailure ? answer.Error : Result.Success(answer.Value);
    }

    public async Task<Result<IReadOnlyList<HrMentorBlankItem>>> ListBlanksAsync(
        HrMentorConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Get, connection, "blanks");
        var answer = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            return answer.Error;
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<BlankRow>>(answer.Value ?? "null", Json);
            if (rows is null || rows.Any(row => row is null || !row.IsComplete))
            {
                return HrMentorErrors.BadResponse;
            }

            return rows
                .Select(row => new HrMentorBlankItem(
                    row.DocumentId!.Value,
                    row.Code,
                    row.Title!,
                    row.VersionId!.Value,
                    row.VersionNo!.Value,
                    row.PublishedAt,
                    row.FileName!,
                    row.ContentType!,
                    row.SizeBytes!.Value,
                    row.Sha256!))
                .ToList();
        }
        catch (JsonException)
        {
            return HrMentorErrors.BadResponse;
        }
    }

    public async Task<Result<byte[]>> DownloadBlankAsync(
        HrMentorConnection connection,
        Guid documentId,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return await DownloadFileAsync(
                connection,
                $"blanks/{documentId.ToString("D", CultureInfo.InvariantCulture)}/file",
                maxBytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<HrMentorPlatformDocuments>> ListPlatformDocumentsAsync(
        HrMentorConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = Request(HttpMethod.Get, connection, "platform/documents");
        var answer = await SendPlatformJsonAsync(request, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            return answer.Error;
        }

        try
        {
            var body = JsonSerializer.Deserialize<PlatformDocumentsBody>(answer.Value, Json);
            if (body?.Platform is not { IsComplete: true } platform
                || body.Documents is not { } rows
                || rows.Any(row => row is null || !row.IsComplete))
            {
                return HrMentorErrors.BadResponse;
            }

            return new HrMentorPlatformDocuments(
                platform.Id!.Value,
                platform.Name!,
                [.. rows.Select(row => row!.ToItem())]);
        }
        catch (JsonException)
        {
            return HrMentorErrors.BadResponse;
        }
    }

    public async Task<Result<byte[]>> DownloadPlatformDocumentAsync(
        HrMentorConnection connection,
        Guid documentId,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return await DownloadFileAsync(
                connection,
                $"platform/documents/{documentId.ToString("D", CultureInfo.InvariantCulture)}/file",
                maxBytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<HrMentorHoldingsReport>> ReportHoldingsAsync(
        HrMentorConnection connection,
        IReadOnlyList<HrMentorHolding> holdings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(holdings);

        using var request = Request(HttpMethod.Put, connection, "platform/holdings");
        var payload = new HoldingsBody([.. holdings.Select(h => new HoldingRow(h.DocumentId, h.VersionId))]);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        var answer = await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            return answer.Error;
        }

        try
        {
            var report = JsonSerializer.Deserialize<HoldingsReportBody>(answer.Value ?? "null", Json);
            return report is { Recorded: >= 0, Ignored: >= 0 }
                ? new HrMentorHoldingsReport(report.Recorded.Value, report.Ignored.Value)
                : HrMentorErrors.BadResponse;
        }
        catch (JsonException)
        {
            return HrMentorErrors.BadResponse;
        }
    }

    /// <summary>Faylni <paramref name="maxBytes"/> gacha yuklaydi (<see cref="FileTimeout"/>).</summary>
    private async Task<Result<byte[]>> DownloadFileAsync(
        HrMentorConnection connection,
        string path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, connection, path);

        try
        {
            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (StatusError(response.StatusCode) is { } error)
            {
                return error;
            }

            if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
            {
                return HrMentorErrors.BlankTooLarge;
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var bytes = await ReadLimitedAsync(stream, maxBytes, cancellationToken).ConfigureAwait(false);
                return bytes is null ? HrMentorErrors.BlankTooLarge : bytes;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HrMentorErrors.Timeout;
        }
        catch (HttpRequestException)
        {
            return HrMentorErrors.Unreachable;
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, HrMentorConnection connection, string path)
    {
        var request = new HttpRequestMessage(method, new Uri($"{connection.BaseUrl.AbsoluteUri.TrimEnd('/')}/api/v1/{path}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>So'rov → javob matni (≤ 2 MB, 20 s). <paramref name="notFoundIsEmpty"/> — 404 → <c>null</c>.</summary>
    private async Task<Result<string?>> SendJsonAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool notFoundIsEmpty = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(JsonTimeout);

        try
        {
            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);

            if (notFoundIsEmpty && response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result.Success<string?>(null);
            }

            if (StatusError(response.StatusCode) is { } error)
            {
                return error;
            }

            if (response.Content.Headers.ContentLength is > MaxJsonBytes)
            {
                return HrMentorErrors.BadResponse;
            }

            var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var bytes = await ReadLimitedAsync(stream, MaxJsonBytes, deadline.Token).ConfigureAwait(false);
                if (bytes is null)
                {
                    return HrMentorErrors.BadResponse;
                }

                return Result.Success<string?>(Encoding.UTF8.GetString(bytes));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HrMentorErrors.Timeout;
        }
        catch (HttpRequestException)
        {
            return HrMentorErrors.Unreachable;
        }
    }

    /// <summary>
    /// Platforma ro'yxati so'rovi (≤ 2 MB, 20 s). 403 tanasidagi KOD o'qiladi: kalit hali
    /// bog'lanmagan yoki ruxsat yo'q, yoki yo'l yo'q (404) — <see cref="HrMentorErrors.PlatformNotReady"/>
    /// (eski ro'yxatga o'tish belgisi), platforma o'chirilgan — <see cref="HrMentorErrors.PlatformOff"/>.
    /// </summary>
    private async Task<Result<string>> SendPlatformJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(JsonTimeout);

        try
        {
            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return HrMentorErrors.PlatformNotReady;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return await ErrorCodeAsync(response, deadline.Token).ConfigureAwait(false) switch
                {
                    "platforms.key_not_bound" or "app.permission_denied" => HrMentorErrors.PlatformNotReady,
                    "platforms.platform_inactive" => HrMentorErrors.PlatformOff,
                    _ => HrMentorErrors.Forbidden,
                };
            }

            if (StatusError(response.StatusCode) is { } error)
            {
                return error;
            }

            if (response.Content.Headers.ContentLength is > MaxJsonBytes)
            {
                return HrMentorErrors.BadResponse;
            }

            var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var bytes = await ReadLimitedAsync(stream, MaxJsonBytes, deadline.Token).ConfigureAwait(false);
                return bytes is null ? HrMentorErrors.BadResponse : Encoding.UTF8.GetString(bytes);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HrMentorErrors.Timeout;
        }
        catch (HttpRequestException)
        {
            return HrMentorErrors.Unreachable;
        }
    }

    /// <summary>HRMentor xato tanasidagi <c>error.code</c> (≤ 16 KB); o'qib bo'lmasa <c>null</c>.</summary>
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var bytes = await ReadLimitedAsync(stream, 16 * 1024, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ErrorBody>(bytes, Json)?.Error?.Code;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private static Error? StatusError(HttpStatusCode status)
    {
        var code = (int)status;
        return code switch
        {
            >= 200 and < 300 => null,
            401 => HrMentorErrors.Unauthorized,
            403 => HrMentorErrors.Forbidden,
            404 => HrMentorErrors.NotFound,
            >= 400 and < 500 => HrMentorErrors.Rejected,
            >= 500 => HrMentorErrors.ServerError,
            _ => HrMentorErrors.BadResponse,
        };
    }

    /// <summary>Oqimni <paramref name="max"/> baytgacha o'qiydi; oshsa <c>null</c>.</summary>
    private static async Task<byte[]?> ReadLimitedAsync(Stream stream, long max, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > max)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>HRMentor <c>BlankOut</c> (snake_case).</summary>
    private sealed record BlankRow(
        [property: JsonPropertyName("document_id")] Guid? DocumentId,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("version_id")] Guid? VersionId,
        [property: JsonPropertyName("version_no")] int? VersionNo,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("file_name")] string? FileName,
        [property: JsonPropertyName("content_type")] string? ContentType,
        [property: JsonPropertyName("size_bytes")] long? SizeBytes,
        [property: JsonPropertyName("sha256")] string? Sha256)
    {
        public bool IsComplete =>
            DocumentId is not null
            && VersionId is not null
            && VersionNo is not null
            && SizeBytes is >= 0
            && !string.IsNullOrWhiteSpace(Title)
            && !string.IsNullOrWhiteSpace(FileName)
            && !string.IsNullOrWhiteSpace(ContentType)
            && Sha256 is { Length: 64 };

        /// <summary>To'liq qator (<see cref="IsComplete"/>) → blanka.</summary>
        public HrMentorBlankItem ToItem() => new(
            DocumentId!.Value,
            Code,
            Title!,
            VersionId!.Value,
            VersionNo!.Value,
            PublishedAt,
            FileName!,
            ContentType!,
            SizeBytes!.Value,
            Sha256!);
    }

    /// <summary>HRMentor <c>PlatformDocumentsOut</c>: platforma va uning hujjatlari (<see cref="BlankRow"/> shaklida).</summary>
    private sealed record PlatformDocumentsBody(
        [property: JsonPropertyName("platform")] PlatformRefBody? Platform,
        [property: JsonPropertyName("documents")] List<BlankRow?>? Documents);

    private sealed record PlatformRefBody(
        [property: JsonPropertyName("id")] Guid? Id,
        [property: JsonPropertyName("name")] string? Name)
    {
        public bool IsComplete => Id is not null && !string.IsNullOrWhiteSpace(Name);
    }

    /// <summary>HRMentor <c>HoldingsIn</c>.</summary>
    private sealed record HoldingsBody([property: JsonPropertyName("documents")] List<HoldingRow> Documents);

    private sealed record HoldingRow(
        [property: JsonPropertyName("document_id")] Guid DocumentId,
        [property: JsonPropertyName("version_id")] Guid VersionId);

    /// <summary>HRMentor <c>HoldingsReportOut</c>.</summary>
    private sealed record HoldingsReportBody(
        [property: JsonPropertyName("recorded")] int? Recorded,
        [property: JsonPropertyName("ignored")] int? Ignored);

    /// <summary>HRMentor xato tanasi: <c>{"error": {"code", "message", "details"}}</c>.</summary>
    private sealed record ErrorBody([property: JsonPropertyName("error")] ErrorPart? Error);

    private sealed record ErrorPart([property: JsonPropertyName("code")] string? Code);
}
