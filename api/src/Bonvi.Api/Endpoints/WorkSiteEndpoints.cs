using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.WorkSites;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Obyektni yaratish va tahrirlash tanasi — ikkalasi bir xil.</summary>
/// <remarks>
/// ⚠️ <c>Guid X = default</c> yo'q (OpenAPI tuzog'i, boshqa tanalardagi
/// bilan bir xil sabab).
/// </remarks>
/// <param name="Name">Nom. Majburiy va takrorlanmas.</param>
/// <param name="Latitude">Kenglik, −90…90.</param>
/// <param name="Longitude">Uzunlik, −180…180.</param>
/// <param name="RadiusMeters">Radius 50…5000; berilmasa 200.</param>
/// <param name="Note">Izoh.</param>
public sealed record WorkSiteRequest(
    string? Name,
    decimal Latitude,
    decimal Longitude,
    int? RadiusMeters = null,
    string? Note = null);

/// <summary>
/// Obyektlar ma'lumotnomasi — <c>/api/attendance/work-sites</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>O'QISH <c>catalog.view</c>, YOZISH <c>attendance.manage</c>.</b>
/// Bu loyihadagi yagona «bo'lingan ruxsatli» ma'lumotnoma va shu sababli
/// sababi shu yerda yozilgan.
/// </para>
/// <para>
/// Ekran «Ma'lumotnomalar → Obyektlar» da turadi, ya'ni uni O'QISH
/// qolgan to'qqizta ma'lumotnoma bilan bir xil ruxsat ostida bo'lishi
/// mantiqiy: bu shunchaki nomlar ro'yxati.
/// </para>
/// <para>
/// Lekin <c>radius_meters</c> — yorliq emas, DAVOMAT BOSHQARUVI.
/// <c>catalog.manage</c> ruxsati bor, <c>attendance.manage</c> yo'q odam
/// har bir to'siqni 5 km ga kengaytirib, telefondan belgilashni butun
/// shaharga ochib yuborardi — va ruxsat modelida bunga hech qanday
/// e'tiroz bo'lmasdi. Shu sababli yozish davomat ruxsati ostida.
/// </para>
/// <para>
/// ⚠️ Shu bo'linish sababli ular UMUMIY ma'lumotnoma shtampidan
/// (<c>CatalogEndpoints.MapSimpleReference</c>) o'tmaydi — u ikkala
/// tomonga ham <c>catalog.*</c> ni qattiq yozib qo'ygan.
/// </para>
/// <para>
/// Xarita komponenti bu to'lqinda YO'Q (SPEC §F): koordinata istalgan
/// xarita ilovasidan nusxalab qo'yiladi, xarita esa alohida qaror.
/// </para>
/// </remarks>
public static class WorkSiteEndpoints
{
    public static IEndpointRouteBuilder MapWorkSiteEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/attendance/work-sites").WithTags("Attendance");

        group.MapGet("/", async (
                bool? onlyActive,
                ListWorkSitesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListWorkSitesQuery(onlyActive ?? false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("ListWorkSites")
            .WithSummary("Obyektlar — ro'yxat")
            .WithDescription(
                "Alifbo tartibida. «onlyActive=true» — faqat belgi qabul qilinadigan obyektlar. " +
                "Sukut bo'yicha faolsizlari ham qaytadi: ularni qayta yoqish mumkin bo'lishi kerak.")
            .Produces<IReadOnlyList<WorkSiteDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetWorkSiteQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetWorkSiteQuery(id), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("GetWorkSite")
            .WithSummary("Obyektlar — bitta yozuv")
            .WithDescription("Topilmasa — «davomat.obyekt_topilmadi» (404).")
            .Produces<WorkSiteDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                WorkSiteRequest request,
                CreateWorkSiteCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateWorkSiteCommand(new SaveWorkSiteCommand(
                            request.Name,
                            request.Latitude,
                            request.Longitude,
                            request.RadiusMeters,
                            request.Note)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("CreateWorkSite")
            .WithSummary("Obyektlar — yangi obyekt")
            .WithDescription(
                "Koordinatani istalgan xarita ilovasidan nusxalab qo'yish mumkin. " +
                "Xatolar: «malumotnoma.nom_bosh», «davomat.obyekt_kenglik», " +
                "«davomat.obyekt_uzunlik», «davomat.obyekt_radiusi» (400); " +
                "«malumotnoma.nom_band» (409).")
            .Produces<WorkSiteDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                WorkSiteRequest request,
                UpdateWorkSiteCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateWorkSiteCommand(id, new SaveWorkSiteCommand(
                            request.Name,
                            request.Latitude,
                            request.Longitude,
                            request.RadiusMeters,
                            request.Note)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("UpdateWorkSite")
            .WithSummary("Obyektlar — tahrirlash")
            .WithDescription(
                "Radiusni o'zgartirish TARIXNI qayta yozmaydi: har bir qabul qilingan belgi " +
                "o'sha lahzadagi radiusni o'zida saqlaydi.")
            .Produces<WorkSiteDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                string? reason,
                SetWorkSiteActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetWorkSiteActiveCommand(id, Active: false, reason), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeactivateWorkSite")
            .WithSummary("Obyektlar — faolsizlantirish")
            .WithDescription(
                "Obyekt O'CHMAYDI: bu yerda qabul qilingan belgilar unga ishora qilib turadi. " +
                "Faolsizlantirilgan obyektda yangi belgi qabul qilinmaydi, tarix esa joyida " +
                "qoladi. «reason» izohga yoziladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                SetWorkSiteActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetWorkSiteActiveCommand(id, Active: true), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ActivateWorkSite")
            .WithSummary("Obyektlar — qayta yoqish")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
