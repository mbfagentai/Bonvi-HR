using System.Security.Claims;
using Bonvi.Api.Extensions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Mcp;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.Mcp;

/// <summary>«AI ulanish» bo'limining holati.</summary>
/// <param name="State">
/// <c>NotConfigured</c> | <c>Pending</c> | <c>Faulty</c> | <c>Off</c> | <c>On</c>.
/// </param>
/// <param name="Reason">Nega ishlamayapti (sirsiz). <c>On</c>/<c>Off</c> da <c>null</c>.</param>
/// <param name="Enabled">Egasining o'chirgichi.</param>
/// <param name="ServerUrl">AI ilovasiga kiritiladigan manzil (<c>…/mcp</c>).</param>
public sealed record McpStatusDto(string State, string? Reason, bool Enabled, string ServerUrl);

public sealed record McpEnabledRequest(bool Enabled);

/// <summary>
/// Bitta ulanish — ro'yxat qatori. <c>Status</c>: <c>active</c> | <c>revoked</c> | <c>expired</c>.
/// </summary>
public sealed record McpConnectionDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string ClientName,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    string Status);

/// <summary>Jurnal qatori.</summary>
public sealed record McpAuditDto(
    long Id,
    DateTimeOffset At,
    string UserName,
    string ClientName,
    string Tool,
    string Outcome,
    int? RowCount,
    int DurationMs);

/// <summary>
/// «Tizim sozlamalari» → «AI ulanish (MCP)» bo'limi (<c>docs/SPEC-mcp.md</c> §8).
/// Hammasi <c>settings.manage</c> ostida — bu bo'lim sozlamalar sahifasining qismi.
/// </summary>
/// <remarks>
/// <para>
/// Ulanishni «uzish» yozuvni O'CHIRMAYDI: u ro'yxatda kulrang bo'lib qoladi
/// (API kalitlaridagi bilan bir xil sabab — «kim qachon ulangan edi» degan
/// savol xavfsizlik tekshiruvida birinchi so'raladi).
/// </para>
/// </remarks>
public static class McpAdminEndpoints
{
    public const int MaxAuditRows = 200;

    private static readonly Error ConnectionNotFound = Error.NotFound("mcp.ulanish_topilmadi", "Ulanish topilmadi.");

    public static IEndpointRouteBuilder MapMcpAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/mcp").WithTags("Mcp");

        group.MapGet("/status", async (HttpContext context, McpRuntime runtime) =>
            {
                var status = await runtime.GetStatusAsync(context.RequestAborted).ConfigureAwait(false);
                return Results.Ok(await ToDtoAsync(context, status).ConfigureAwait(false));
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("GetMcpStatus")
            .WithSummary("AI ulanish — holat")
            .WithDescription(
                "«state»: NotConfigured (serverda parol yoki ochiq manzil yo'q), Pending (tekshiruv hali tugamagan), " +
                "Faulty (o'qish rolini tayyorlash yoki o'z-o'zini tekshirish yiqildi — «reason» da sabab), " +
                "Off (egasi o'chirgan, sukut), On (ishlayapti). «serverUrl» — AI ilovasiga kiritiladigan manzil.")
            .Produces<McpStatusDto>();

        group.MapPost("/check", async (HttpContext context, McpRuntime runtime) =>
            {
                await runtime.CheckAsync(context.RequestAborted).ConfigureAwait(false);
                var status = await runtime.GetStatusAsync(context.RequestAborted).ConfigureAwait(false);
                return Results.Ok(await ToDtoAsync(context, status).ConfigureAwait(false));
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("CheckMcp")
            .WithSummary("AI ulanish — qayta tekshirish")
            .WithDescription("O'qish rolini qayta tayyorlaydi va o'z-o'zini tekshirishni qayta o'tkazadi. Javob — yangi holat.")
            .Produces<McpStatusDto>();

        group.MapPut("/enabled", async (
                McpEnabledRequest request,
                HttpContext context,
                McpRuntime runtime,
                ISystemSettingRepository settings,
                IUnitOfWork unitOfWork) =>
            {
                var cancellationToken = context.RequestAborted;
                var value = request.Enabled ? "true" : "false";
                var row = await settings.FindAsync(SystemSettingKeys.McpEnabled, cancellationToken).ConfigureAwait(false);

                if (row is null)
                {
                    var created = SystemSetting.CreateServerValue(SystemSettingKeys.McpEnabled, value);

                    if (created.IsFailure)
                    {
                        return Result.Failure(created.Error).ToHttpResult();
                    }

                    settings.Add(created.Value);
                }
                else
                {
                    var applied = row.SetServerValue(value);

                    if (applied.IsFailure)
                    {
                        return Result.Failure(applied.Error).ToHttpResult();
                    }
                }

                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                runtime.SetEnabled(request.Enabled);

                var status = await runtime.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                return Results.Ok(await ToDtoAsync(context, status).ConfigureAwait(false));
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("SetMcpEnabled")
            .WithSummary("AI ulanish — yoqish yoki o'chirish")
            .WithDescription(
                "«{ enabled }». O'chirilganda mavjud ulanishlar ro'yxatda qoladi, lekin har bir chaqiruv 503 oladi; " +
                "qayta yoqilganda ular yana ishlaydi. Server sozlanmagan bo'lsa ham qiymat saqlanadi — holat " +
                "«NotConfigured»/«Faulty» bo'lib qoladi.")
            .Produces<McpStatusDto>();

        /*
          Ro'yxat, uzish va jurnal `IsConfigured` ga QARAMAYDI: ular ASOSIY
          ulanish bilan o'qiladi. Parol keyinchalik olib tashlansa ham egasi
          kim ulangan edi — ko'rishi va uzishi kerak; aks holda ekran «hech kim
          ulanmagan» deb YOLG'ON aytardi.
        */
        group.MapGet("/connections", async (HttpContext context, McpDatabase database) =>
            {
                await using var store = database.CreateStoreContext();
                var now = DateTimeOffset.UtcNow;

                var rows = await (
                        from grant in store.McpGrants
                        join client in store.McpClients on grant.ClientId equals client.Id
                        join user in store.Users.IgnoreQueryFilters() on grant.UserId equals user.Id into users
                        from user in users.DefaultIfEmpty()
                        orderby grant.RevokedAt != null, grant.CreatedAt descending
                        select new
                        {
                            grant.Id,
                            grant.UserId,
                            UserName = user == null ? null : user.FullName,
                            ClientName = client.Name,
                            grant.CreatedAt,
                            grant.LastUsedAt,
                            grant.RevokedAt,
                        })
                    .AsNoTracking()
                    .Take(500)
                    .ToListAsync(context.RequestAborted)
                    .ConfigureAwait(false);

                return Results.Ok(rows.Select(row => new McpConnectionDto(
                    row.Id,
                    row.UserId,
                    row.UserName?.Full ?? "O'chirilgan foydalanuvchi",
                    row.ClientName,
                    row.CreatedAt,
                    row.LastUsedAt,
                    row.RevokedAt,
                    row.RevokedAt is not null ? "revoked"
                        : row.CreatedAt + McpLimits.GrantLifetime <= now ? "expired"
                        : "active")).ToList());
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("ListMcpConnections")
            .WithSummary("AI ulanish — ulangan hisoblar")
            .WithDescription(
                "Har bir ulanish: kim, qaysi AI ilovasi, qachon ulangan, oxirgi ishlatilgan vaqti (daqiqa aniqligida), " +
                "holati — active | revoked | expired (90 kun). Tartib: avval kuchdagilar, ichida yangisidan.")
            .Produces<IReadOnlyList<McpConnectionDto>>();

        group.MapPost("/connections/{id:guid}/revoke", async (Guid id, HttpContext context, McpDatabase database) =>
            {
                await using var store = database.CreateStoreContext();

                var exists = await store.McpGrants.AnyAsync(grant => grant.Id == id, context.RequestAborted).ConfigureAwait(false);

                if (!exists)
                {
                    return Result.Failure(ConnectionNotFound).ToHttpResult();
                }

                var by = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
                await McpOAuthEndpoints.RevokeGrantAsync(store, id, by, context.RequestAborted).ConfigureAwait(false);

                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("RevokeMcpConnection")
            .WithSummary("AI ulanish — uzish")
            .WithDescription(
                "Ulanish DARHOL ishlamay qoladi (token har chaqiruvda bazadan tekshiriladi). Yozuv o'chirilmaydi. " +
                "IDEMPOTENT: allaqachon uzilgan ulanishda ham 204. Noma'lum: «mcp.ulanish_topilmadi» (404).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/audit", async (int? take, HttpContext context, McpDatabase database) =>
            {
                await using var store = database.CreateStoreContext();

                var rows = await store.McpAudit
                    .AsNoTracking()
                    .OrderByDescending(entry => entry.At)
                    .ThenByDescending(entry => entry.Id)
                    .Take(Math.Clamp(take ?? 100, 1, MaxAuditRows))
                    .Select(entry => new McpAuditDto(
                        entry.Id,
                        entry.At,
                        entry.UserName,
                        entry.ClientName,
                        entry.Tool,
                        entry.Outcome,
                        entry.RowCount,
                        entry.DurationMs))
                    .ToListAsync(context.RequestAborted)
                    .ConfigureAwait(false);

                return Results.Ok(rows);
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("ListMcpAudit")
            .WithSummary("AI ulanish — so'nggi so'rovlar")
            .WithDescription(
                "AI qaysi vositani qachon, kim nomidan chaqirgani. «outcome»: ok | denied (foydalanuvchida bo'lim " +
                "ruxsati yo'q) | error. «take» — 1…200, sukut 100. Argumentlar bu javobda YO'Q.")
            .Produces<IReadOnlyList<McpAuditDto>>();

        return app;
    }

    private static async Task<McpStatusDto> ToDtoAsync(HttpContext context, McpStatus status)
    {
        // `On` — o'chirgich yoqiq; boshqa holatlarda (o'chiq/sozlanmagan/nosoz)
        // egasining tanlovi alohida o'qiladi: u ekranda saqlanib turishi kerak.
        var enabled = status.State is McpState.On || await ReadSwitchAsync(context).ConfigureAwait(false);

        return new McpStatusDto(status.State.ToString(), status.Reason, enabled, McpUrls.Resource(context.Request));
    }

    private static async Task<bool> ReadSwitchAsync(HttpContext context)
    {
        var database = context.RequestServices.GetRequiredService<McpDatabase>();

        await using var store = database.CreateStoreContext();

        var value = await store.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == SystemSettingKeys.McpEnabled)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(context.RequestAborted)
            .ConfigureAwait(false);

        return string.Equals(value, "true", StringComparison.Ordinal);
    }
}
