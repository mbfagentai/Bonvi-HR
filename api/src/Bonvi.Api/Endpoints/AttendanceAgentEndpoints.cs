using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Photos;
using Bonvi.Domain.Attendance;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// LAN ichidagi agent uchun endpointlar — qurilmaga kimni yozish kerakligi
/// va nima yozilgani (SPEC-agent §D4).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Qabul yo'li bilan BIR XIL ochiqlik:</b> JWT yo'q, himoya qurilma
/// tokenida (<see cref="AttendanceDeviceAuthenticator"/>). Sabab ham bir xil
/// — agent odam emas va tizimga kira olmaydi. Token faqat SARLAVHADA
/// (<c>X-Bonvi-Device-Token</c>): yo'ldagi shakl Hikvision uchun yon
/// yo'l edi, agent esa bizniki va sarlavha qo'ya oladi, ya'ni token logga
/// tushmaydi.
/// </para>
/// <para>
/// ⭐ <b>Har qanday ulanishni AGENT boshlaydi.</b> Ishlab chiqarish serveri
/// (VPS) ofis tarmog'idagi terminalga yeta olmaydi, shu sababli server
/// hech qachon agentga «hozir yoz» deb aytmaydi: agent o'zi so'raydi. Ya'ni
/// hech qayerda kiruvchi port ochilmaydi.
/// </para>
/// <para>
/// Imzo rejimidagi qurilmada <c>X-Bonvi-Signature</c> shu yerda ham
/// ishlaydi: <c>GET</c> uchun imzo BO'SH tana ustida hisoblanadi (o'qish
/// so'rovini takrorlash hech narsa o'zgartirmaydi), <c>POST</c> uchun —
/// xom tana ustida, qabul yo'lidagidek.
/// </para>
/// </remarks>
public static class AttendanceAgentEndpoints
{
    private const string TokenHeader = "X-Bonvi-Device-Token";
    private const string SignatureHeader = "X-Bonvi-Signature";

    /// <summary>Hisobot tanasining chegarasi — 4000 qator × ~200 bayt dan kengroq.</summary>
    private const int MaxReportBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Hisobotni o'qish sozlamalari.
    /// </summary>
    /// <remarks>
    /// Tana XOM baytlar bo'lib o'qiladi (imzo shular ustida), shu sababli
    /// uni model bog'lovchisi emas, shu sozlama bilan <c>JsonSerializer</c>
    /// o'giradi — <c>Program.cs</c> dagi HTTP sozlamasining aynan o'zi:
    /// camelCase va satrli enum.
    /// </remarks>
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IEndpointRouteBuilder MapAttendanceAgentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/attendance/agent").WithTags("Attendance");

        group.MapGet("/roster", async (
                [FromHeader(Name = TokenHeader)] string? token,
                [FromHeader(Name = SignatureHeader)] string? signature,
                HttpContext context,
                GetAgentRosterQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAgentRosterQuery(token, signature, AgentIp(context)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Respond(natija.IsFailure ? natija.Error : null) ?? natija.ToHttpResult();
            })
            .AsAgentRoute()
            .WithName("GetAttendanceAgentRoster")
            .WithSummary("Davomat — agent uchun ro'yxat")
            .WithDescription(
                "Qurilmada BO'LISHI KERAK bo'lgan xodimlar: bugunni qamrovchi mehnat munosabati bor va " +
                "rasmi bor. Rasmsiz xodim ro'yxatga TUSHMAYDI. «personNo» — server ajratgan shaxs raqami " +
                "(9 bilan boshlanadigan 9 belgi), «version» — hisobotda qaytariladigan belgi, «force» — " +
                "qurilmada bor bo'lsa ham qayta yozish kerak. Noto'g'ri token — «davomat.ingest_ruxsat_yoq» (401).")
            .Produces<AgentRosterDto>(StatusCodes.Status200OK);

        group.MapGet("/photo/{employeeId:guid}", async (
                Guid employeeId,
                [FromHeader(Name = TokenHeader)] string? token,
                [FromHeader(Name = SignatureHeader)] string? signature,
                HttpContext context,
                AttendanceDeviceAuthenticator authenticator,
                GetEmployeePhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var device = await authenticator
                    .AuthenticateAsync(token, [], signature, AgentIp(context), cancellationToken)
                    .ConfigureAwait(false);

                if (device.IsFailure)
                {
                    return Respond(device.Error) ?? device.ToHttpResult();
                }

                var natija = await handler
                    .HandleAsync(
                        new GetEmployeePhotoQuery(employeeId, PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT — xato yo'lidagina `ToHttpResult()`.
                return natija.IsFailure ? natija.ToHttpResult() : PhotoHttp.Write(context, natija.Value);
            })
            .AsAgentRoute()
            .WithName("GetAttendanceAgentPhoto")
            .WithSummary("Davomat — agent uchun xodim rasmi")
            .WithDescription(
                "Rasm baytlari; «ETag» + «If-None-Match» → 304, ya'ni o'zgarmagan rasm ikkinchi marta " +
                "yuklanmaydi. Rasm yo'q — «xodim.rasm_topilmadi» (404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified);

        group.MapPost("/report", async (
                [FromHeader(Name = TokenHeader)] string? token,
                [FromHeader(Name = SignatureHeader)] string? signature,
                HttpContext context,
                ReportAgentEnrollmentsCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<AgentReportItemDto>? items;
                try
                {
                    items = JsonSerializer.Deserialize<IReadOnlyList<AgentReportItemDto>>(body, ReportJson);
                }
                catch (JsonException)
                {
                    items = null;
                }

                var natija = await handler
                    .HandleAsync(
                        new ReportAgentEnrollmentsCommand(token, body, signature, AgentIp(context), items ?? []),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Respond(natija.IsFailure ? natija.Error : null) ?? natija.ToHttpResult();
            })
            .AsAgentRoute()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxReportBytes))
            .WithName("ReportAttendanceAgentEnrollments")
            .WithSummary("Davomat — agent hisoboti")
            .WithDescription(
                "«[{ employeeId, action: Upsert|Remove, ok, version, error }]». Holatni FAQAT shu " +
                "endpoint o'zgartiradi: server hech qachon «yozilgan bo'lsa kerak» deb taxmin qilmaydi. " +
                "Noma'lum xodim jimgina tashlanadi («ignored»). Chegaradan ko'p qator — " +
                "«davomat.agent_hisobot_uzun» (400).")
            .Produces<AgentReportResultDto>(StatusCodes.Status200OK);

        group.MapPost("/clock", async (
                [FromHeader(Name = TokenHeader)] string? token,
                [FromHeader(Name = SignatureHeader)] string? signature,
                HttpContext context,
                ReportDeviceClockCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync(context.Request, cancellationToken).ConfigureAwait(false);

                DeviceClockBody? clock;
                try
                {
                    clock = JsonSerializer.Deserialize<DeviceClockBody>(body, ReportJson);
                }
                catch (JsonException)
                {
                    clock = null;
                }

                if (clock?.DeviceTime is not { } deviceTime)
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "davomat.qurilma_vaqti_notogri",
                        Detail = "Qurilma vaqti ko'rsatilmagan.",
                    });
                }

                var natija = await handler
                    .HandleAsync(
                        new ReportDeviceClockCommand(token, body, signature, AgentIp(context), deviceTime),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Respond(natija.IsFailure ? natija.Error : null) ?? natija.ToHttpResult();
            })
            .AsAgentRoute()
            .DisableAntiforgery()
            .WithName("ReportAttendanceDeviceClock")
            .WithSummary("Davomat — qurilma soati")
            .WithDescription(
                "«{ deviceTime }» — terminalning O'Z joriy vaqti, siljishi bilan. Server uni shu " +
                "lahzadagi o'z vaqti bilan solishtiradi va farqni qurilma kartasiga yozadi. " +
                "Bu o'lchov hodisalar navbatiga bog'liq EMAS: agent bir necha daqiqa o'chib tursa " +
                "ham soat og'ishi ko'rsatkichi to'g'ri qoladi. Ishonarsiz qiymat — " +
                "«davomat.qurilma_vaqti_notogri» (400).")
            .Produces<DeviceClockDto>(StatusCodes.Status200OK);

        return app;
    }

    /// <summary>Agent yuborgan qurilma vaqti.</summary>
    /// <param name="DeviceTime">Terminalning joriy vaqti.</param>
    private sealed record DeviceClockBody(DateTimeOffset? DeviceTime);

    /// <summary>To'rtala marshrutning umumiy sozlamalari — qabul yo'lidagidek bitta joyda.</summary>
    private static RouteHandlerBuilder AsAgentRoute(this RouteHandlerBuilder builder)
        => builder
            // Ochiqlik ATAYLAB — sinf izohiga qarang. Himoya qurilma tokenida.
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Ingest)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <summary>
    /// Tasdiqlanmagan so'rovga 401 beradi.
    /// </summary>
    /// <remarks>
    /// Umumiy <c>ToHttpResult</c> buni qila olmaydi: <c>IngestErrors.Unauthorized</c>
    /// domen xato turlarida yo'q (sababi o'sha xatoning izohida). Shakl qabul
    /// yo'lidagi bilan bir xil.
    /// </remarks>
    private static IResult? Respond(Domain.Common.Error? error)
        => error is { } value && value.Code == IngestErrors.Unauthorized.Code
            ? Results.Problem(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = value.Code,
                Detail = value.Message,
            })
            : null;

    private static string? AgentIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;

        return address is { IsIPv4MappedToIPv6: true }
            ? address.MapToIPv4().ToString()
            : address?.ToString();
    }

    /// <summary>Tanani to'liq o'qiydi — imzo XOM baytlar ustida tekshiriladi.</summary>
    private static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
