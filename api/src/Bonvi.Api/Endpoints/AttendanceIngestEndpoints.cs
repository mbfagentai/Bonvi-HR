using System.Buffers;
using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Attendance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// Turniket va yuz terminallari hodisalarini qabul qilish — davomat moduli (3-bosqich).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>TIZIMDAGI YAGONA ANONIM YOZISH ENDPOINTI.</b> Umumiy fallback
/// siyosati har bir endpointdan autentifikatsiya talab qiladi
/// (<c>AuthenticationExtensions</c>), bu yerda esa JWT umuman yo'q — terminal
/// odam emas va tizimga kira olmaydi. Shu sababli ochiqlik ATAYLAB va
/// oshkora e'lon qilinadi (<c>.AllowAnonymous()</c>), himoyani esa qurilma
/// tokeni beradi: SHA-256 xeshi bo'yicha qidiriladi, xohlasa imzo va IP
/// ro'yxati qo'shiladi (<see cref="AttendanceDeviceAuthenticator"/>).
/// </para>
/// <para>
/// <b>Ikki marshrut, bitta ishlovchi.</b> Hikvision «HTTP Listening» faqat
/// URL sozlay oladi — sarlavha ham, imzo ham yo'q — shu sababli token
/// YO'LDA keladi. Vositachi agent va kelajakdagi adapterlar sarlavhali
/// shaklni ishlatadi: u logga umuman tushmaydi.
/// </para>
/// <para>
/// ⭐ <b>Yo'ldagi token logga tushmaydi</b> — uni <see cref="IngestTokenScrubber"/>
/// barcha chiqishlardan oldin <c>/api/attendance/ingest/***</c> ga
/// almashtiradi. Endpoint OpenAPI'dan YASHIRILMAGAN: shartnoma ko'rinib
/// turishi kerak, yashirish esa xavfsizlik bermaydi va <c>./bonvi gen</c> ni
/// buzardi (SPEC §3.3).
/// </para>
/// <para>
/// <b>Javobda kim o'tgani yo'q</b> — faqat <c>{ ok, stored, duplicates }</c>
/// (<see cref="AttendanceIngestResultDto"/> izohi).
/// </para>
/// </remarks>
public static class AttendanceIngestEndpoints
{
    /// <summary>Tokenli sarlavha — vositachi agent va adapterlar uchun.</summary>
    public const string TokenHeader = "X-Bonvi-Device-Token";

    /// <summary>HMAC imzosi sarlavhasi — <c>v1=&lt;hex&gt;</c>.</summary>
    public const string SignatureHeader = "X-Bonvi-Signature";

    private const string Description =
        "Qurilma hodisalarini qabul qiladi. JWT YO'Q — qurilma o'z tokeni bilan tasdiqlanadi. " +
        "Tana qurilma turiga qarab o'qiladi (tanaga qarab emas): Hikvision — «multipart/form-data» " +
        "(«event_log» qismida hodisa JSON'i, keyingi qismda yuz surati) yoki hodisaning o'zi JSON; " +
        "Generic — «application/json»: { \"events\": [ { personId, occurredAt (ISO 8601), direction, " +
        "method, eventId, photoBase64 } ] } yoki o'ramisiz bitta hodisa, eng ko'pi 200 ta. " +
        "Tana 2 MB gacha, surat 1 MB gacha (JPEG/PNG; yaroqsiz surat tashlanadi, o'tish baribir yoziladi). " +
        "Takror yuborish zararsiz: 200 va «duplicates». Autentifikatsiyaning HAR QANDAY nosozligi — " +
        "bir xil «davomat.ingest_ruxsat_yoq» (401); o'chirilgan qurilma — «davomat.qurilma_faolsiz» (403); " +
        "o'qib bo'lmagan tana — «davomat.hodisa_oqilmadi», 200 dan ko'p hodisa — «davomat.hodisa_kop», " +
        "30 kundan uzoq vaqt — «davomat.vaqt_yaroqsiz», shaxs raqamisiz o'tish — «davomat.shaxs_raqami_bosh» " +
        "(hammasi 400). Imzo rejimidagi qurilmada «X-Bonvi-Signature: v1=<hex HMAC-SHA256(xom tana, sir)>» shart.";

    public static IEndpointRouteBuilder MapAttendanceIngestEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/attendance/ingest").WithTags("Attendance");

        group.MapPost("/{token}", (
                string token,
                [FromHeader(Name = SignatureHeader)] string? signature,
                HttpContext context,
                IngestAttendanceEventCommandHandler handler,
                CancellationToken cancellationToken)
                => IngestAsync(token, signature, context, handler, cancellationToken))
            .AsDeviceIngest()
            .WithName("IngestAttendanceByPath")
            .WithSummary("Davomat — qurilma hodisasi (token yo'lda)")
            .WithDescription(
                "«token» — qurilmaning qabul tokeni; u qurilma kartasida yaratilganda BIR MARTA " +
                "ko'rsatiladi. Hikvision «HTTP Listening» uchun: u faqat URL sozlay oladi. " +
                "Yo'l logda «/api/attendance/ingest/***» bo'lib yoziladi. " + Description);

        group.MapPost("/", (
                [FromHeader(Name = TokenHeader)] string? token,
                [FromHeader(Name = SignatureHeader)] string? signature,
                HttpContext context,
                IngestAttendanceEventCommandHandler handler,
                CancellationToken cancellationToken)
                => IngestAsync(token, signature, context, handler, cancellationToken))
            .AsDeviceIngest()
            .WithName("IngestAttendanceByHeader")
            .WithSummary("Davomat — qurilma hodisasi (token sarlavhada)")
            .WithDescription(
                "Token «X-Bonvi-Device-Token» sarlavhasida — vositachi agent va adapterlar uchun. " +
                Description);

        return app;
    }

    /// <summary>
    /// Ikkala marshrutning umumiy xavfsizlik sozlamalari — bitta joyda.
    /// </summary>
    /// <remarks>
    /// Har bir marshrutda qo'lda yozilsa, ikkinchisida bittasi (masalan tana
    /// chegarasi yoki tezlik siyosati) unutilishi mumkin edi — va bu aynan
    /// anonim endpointda bo'lardi.
    /// </remarks>
    private static RouteHandlerBuilder AsDeviceIngest(this RouteHandlerBuilder builder)
        => builder
            // Ochiqlik ATAYLAB — sinf izohiga qarang. Himoya qurilma tokenida.
            .AllowAnonymous()

            // CSRF himoyasi bu yerda ma'nosiz: sessiya cookie'si yo'q, ya'ni
            // soxtalashtiriladigan narsa ham yo'q. Belgi qoldirilsa, multipart
            // so'rov antiforgery tokeni yo'qligi uchun rad etilardi.
            .DisableAntiforgery()

            // Umumiy cheklov bu yo'lga qo'llanmaydi (`RateLimitingOptions.ExcludedPaths`)
            // — o'rniga IP bo'yicha kengroq, o'z siyosati.
            .RequireRateLimiting(RateLimitPolicies.Ingest)

            // 2 MB dan katta tana Kestrel tomonidan o'qilmaydi va ishlovchiga
            // yetib kelmaydi (413).
            .WithMetadata(new RequestSizeLimitAttribute(AttendanceRules.MaxIngestBodyBytes))
            .Produces<AttendanceIngestResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> IngestAsync(
        string? token,
        string? signature,
        HttpContext context,
        IngestAttendanceEventCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var request = context.Request;

        var body = await ReadBodyAsync(request, cancellationToken).ConfigureAwait(false);
        var parts = await SplitMultipartAsync(body, request.ContentType, cancellationToken).ConfigureAwait(false);

        var natija = await handler
            .HandleAsync(
                new IngestAttendanceEventCommand(
                    token,
                    new AttendanceIngestPayload(body, request.ContentType, parts),
                    signature,
                    SourceIp(context)),
                cancellationToken)
            .ConfigureAwait(false);

        // 401 ni umumiy `ToHttpResult` bera olmaydi (domen xato turlarida
        // yo'q — sababi `IngestErrors.Unauthorized` izohida). Tana esa AYNAN
        // o'sha shaklda: `title` — kod, `detail` — matn.
        if (natija.IsFailure && natija.Error.Code == IngestErrors.Unauthorized.Code)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = natija.Error.Code,
                Detail = natija.Error.Message,
            });
        }

        return natija.ToHttpResult();
    }

    /// <summary>
    /// Tanani to'liq o'qiydi — chegaradan oshsa 413.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tana BAYT bo'lib o'qiladi, forma yoki JSON bog'lovchisi orqali emas:
    /// imzo XOM baytlar ustida tekshiriladi, tahlilchi esa autentifikatsiyadan
    /// KEYIN tanlanadi — ya'ni tanani «qanday o'qish» hali noma'lum.
    /// </para>
    /// <para>
    /// Chegara bu yerda ham tekshiriladi, garchi Kestrel uni endpoint
    /// metama'lumotidan allaqachon qo'llasa ham: Kestrel'siz server
    /// (sinov serveri, kelajakdagi boshqa xost) uni qo'llamaydi va 2 MB
    /// qoidasi xostga bog'liq bo'lib qolmasligi kerak. Istisno turi
    /// Kestrel'nikidek — javobni <c>GlobalExceptionHandler</c> ikkala holatda
    /// ham bir xil beradi.
    /// </para>
    /// </remarks>
    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var limit = AttendanceRules.MaxIngestBodyBytes;

        if (request.ContentLength > limit)
        {
            throw TooLarge();
        }

        using var buffer = new MemoryStream((int)Math.Min(request.ContentLength ?? 16_384, limit));
        var chunk = ArrayPool<byte>.Shared.Rent(81_920);

        try
        {
            int read;
            while ((read = await request.Body.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > limit)
                {
                    throw TooLarge();
                }

                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        return buffer.ToArray();
    }

    private static BadHttpRequestException TooLarge()
        => new("So'rov tanasi ruxsat etilgan hajmdan katta.", StatusCodes.Status413PayloadTooLarge);

    /// <summary>
    /// Multipart tanani qismlarga ajratadi.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ajratib bo'lmasa — bo'sh ro'yxat, 400 EMAS. Javob bu yerda
    /// qaytarilsa, u autentifikatsiyadan OLDIN chiqib ketardi va tokensiz
    /// buzuq so'rov 401 o'rniga 400 olardi. Bo'sh ro'yxatni tahlilchi token
    /// tekshirilgandan keyin «o'qib bo'lmadi» deb qaytaradi.
    /// </remarks>
    private static async Task<IReadOnlyList<IngestFilePart>> SplitMultipartAsync(
        byte[] body,
        string? contentType,
        CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var media)
            || !media.MediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var boundary = HeaderUtilities.RemoveQuotes(media.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            return [];
        }

        var parts = new List<IngestFilePart>();

        try
        {
            using var stream = new MemoryStream(body, writable: false);
            var reader = new MultipartReader(boundary, stream) { BodyLengthLimit = AttendanceRules.MaxIngestBodyBytes };

            while (parts.Count < IngestLimits.MaxMultipartParts
                && await reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false) is { } section)
            {
                var disposition = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var parsed)
                    ? parsed
                    : null;

                using var content = new MemoryStream();
                await section.Body.CopyToAsync(content, cancellationToken).ConfigureAwait(false);

                parts.Add(new IngestFilePart(
                    HeaderUtilities.RemoveQuotes(disposition?.Name ?? default).Value,
                    HeaderUtilities.RemoveQuotes(disposition?.FileName ?? default).Value,
                    section.ContentType,
                    content.ToArray()));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return [];
        }

        return parts;
    }

    /// <summary>
    /// So'rov manzili — IPv4 ko'rinishida, agar u IPv6 ichiga o'ralgan bo'lsa.
    /// </summary>
    /// <remarks>
    /// <c>UseForwardedHeaders</c> konveyerda birinchi turadi, ya'ni Caddy
    /// ortida bu proksi emas, haqiqiy mijoz. <c>::ffff:1.2.3.4</c> ochiladi:
    /// HR qurilma kartasida uni ko'radi, IP ro'yxati esa IPv4 bilan yoziladi.
    /// </remarks>
    private static string? SourceIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;

        return address is { IsIPv4MappedToIPv6: true }
            ? address.MapToIPv4().ToString()
            : address?.ToString();
    }
}

/// <summary>
/// <see cref="IAttendanceIngestJournal"/> — <c>ILogger</c> orqali, EventId 5200+.
/// </summary>
/// <remarks>
/// <para>
/// Api qatlamida, chunki yozuvga so'rov usuli va yo'li kerak — bular HTTP
/// tushunchalari (<c>CurrentUser</c> dagi bilan bir xil sabab).
/// </para>
/// <para>
/// ⚠️ Yo'l bu yerda O'ZI ham tozalanadi, garchi <see cref="IngestTokenScrubber"/>
/// butun log oqimini tozalasa ham: bu jurnal yagona ataylab yo'lni yozadigan
/// joy va uning xavfsizligi boshqa sinfning ulanganiga bog'liq bo'lmasligi
/// kerak. Token boshi ham tozalanadi — noma'lum tokenni hujumchi yozadi va
/// unda log satrini buzadigan belgilar bo'lishi mumkin.
/// </para>
/// </remarks>
internal sealed partial class AttendanceIngestJournal : IAttendanceIngestJournal
{
    private readonly ILogger<AttendanceIngestJournal> _logger;
    private readonly IHttpContextAccessor _accessor;

    public AttendanceIngestJournal(ILogger<AttendanceIngestJournal> logger, IHttpContextAccessor accessor)
    {
        _logger = logger;
        _accessor = accessor;
    }

    // Yo'l va token boshi tozalanadi — ya'ni satr yasaladi. Log darajasi
    // o'chiq bo'lsa, bu ish umuman bajarilmasligi kerak (CA1873).
    public void AuthenticationFailed(IngestAuthFailure reason, string? tokenPrefix, string? sourceIp)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            LogAuthenticationFailed(reason, RequestMethod(), RequestPath(), sourceIp ?? "nomalum", SafePrefix(tokenPrefix));
        }
    }

    public void DeviceInactive(Guid deviceId)
        => LogDeviceInactive(deviceId);

    public void PayloadRejected(Guid deviceId, string errorCode)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            LogPayloadRejected(deviceId, errorCode, RequestPath());
        }
    }

    public void ClockRejected(Guid deviceId, DateTimeOffset claimedAt)
        => LogClockRejected(deviceId, claimedAt);

    public void UnknownSubTypeSeen(int majorEventType, int subEventType)
        => LogUnknownSubType(majorEventType, subEventType);

    private string RequestMethod() => _accessor.HttpContext?.Request.Method ?? "-";

    private string RequestPath()
        => IngestTokenScrubber.Scrub(_accessor.HttpContext?.Request.Path.Value ?? "-");

    /// <summary>Faqat base64url belgilari qoladi, qolgani <c>?</c>.</summary>
    private static string SafePrefix(string? prefix)
        => prefix is null
            ? "-"
            : string.Create(prefix.Length, prefix, static (span, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                {
                    var c = source[i];
                    span[i] = char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '?';
                }
            });

    [LoggerMessage(
        EventId = 5200,
        Level = LogLevel.Warning,
        Message = "Qurilma qabuli rad etildi: {Sabab} — {Method} {Path}, manba {Manba}, token boshi {TokenBoshi}")]
    private partial void LogAuthenticationFailed(IngestAuthFailure sabab, string method, string path, string manba, string tokenBoshi);

    [LoggerMessage(
        EventId = 5201,
        Level = LogLevel.Information,
        Message = "Faolsizlantirilgan qurilma hodisa yubormoqda: {QurilmaId}")]
    private partial void LogDeviceInactive(Guid qurilmaId);

    [LoggerMessage(
        EventId = 5202,
        Level = LogLevel.Warning,
        Message = "Qurilma hodisasi rad etildi: {QurilmaId} {Kod} — {Path}")]
    private partial void LogPayloadRejected(Guid qurilmaId, string kod, string path);

    [LoggerMessage(
        EventId = 5203,
        Level = LogLevel.Warning,
        Message = "Qurilma soati ishonchsiz, hodisa rad etildi: {QurilmaId}, qurilma vaqti {QurilmaVaqti}")]
    private partial void LogClockRejected(Guid qurilmaId, DateTimeOffset qurilmaVaqti);

    [LoggerMessage(
        EventId = 5204,
        Level = LogLevel.Information,
        Message = "Hikvision'ning notanish hodisa turi birinchi marta ko'rildi: {AsosiyTur}/{KichikTur}")]
    private partial void LogUnknownSubType(int asosiyTur, int kichikTur);
}
