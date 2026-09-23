using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.WorkSites;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Bonvi.Api.Endpoints;

/// <summary>Obyektni yaratish va tahrirlash tanasi — ikkalasi bir xil.</summary>
/// <remarks>
/// <para>
/// ⚠️ <c>Guid X = default</c> yo'q (OpenAPI tuzog'i, boshqa tanalardagi
/// bilan bir xil sabab).
/// </para>
/// <para>
/// <see cref="Shape"/> qaysi maydonlar o'qilishini hal qiladi: doirada —
/// <see cref="Latitude"/>, <see cref="Longitude"/>, <see cref="RadiusMeters"/>
/// (uchalasi majburiy); ko'pburchakda — <see cref="Polygon"/> (majburiy) va
/// <see cref="EdgeBufferMeters"/>. Ko'pburchakda markazni server
/// hisoblaydi — yuborilgan kenglik/uzunlik e'tiborga olinmaydi.
/// </para>
/// </remarks>
/// <param name="Name">Nom. Majburiy va takrorlanmas.</param>
/// <param name="Shape">«Circle» yoki «Polygon». Majburiy.</param>
/// <param name="Address">Erkin matnli manzil, ≤ 300 belgi.</param>
/// <param name="Latitude">Doira markazining kengligi, −90…90.</param>
/// <param name="Longitude">Doira markazining uzunligi, −180…180.</param>
/// <param name="RadiusMeters">Doira radiusi, 50…5000.</param>
/// <param name="Polygon">Ko'pburchak uchlari — ochiq halqa, 3…50 ta.</param>
/// <param name="EdgeBufferMeters">
/// Ko'pburchak chet zaxirasi, 0…500. Berilmasa: yaratishda 50, tahrirda — hozirgi qiymati.
/// </param>
/// <param name="Note">Izoh.</param>
public sealed record WorkSiteRequest(
    string? Name,
    WorkSiteShape Shape,
    string? Address = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? RadiusMeters = null,
    IReadOnlyList<WorkSiteVertexDto>? Polygon = null,
    int? EdgeBufferMeters = null,
    string? Note = null)
{
    /// <summary>Tanani ilova qatlami buyrug'iga o'giradi — yaratish va tahrir uchun bitta nusxa.</summary>
    public SaveWorkSiteCommand ToCommand()
        => new(Name, Shape, Address, Latitude, Longitude, RadiusMeters, Polygon, EdgeBufferMeters, Note);
}

/// <summary>
/// Obyektlar ma'lumotnomasi — <c>/api/attendance/work-sites</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>O'QISH <c>attendance.view</c> YOKI <c>catalog.view</c>, YOZISH
/// <c>attendance.manage</c>.</b> Bu loyihadagi yagona «bo'lingan ruxsatli»
/// ma'lumotnoma va shu sababli sababi shu yerda yozilgan.
/// </para>
/// <para>
/// O'qish ikki ruxsatdan BIRINI talab qiladi (<see cref="ReadPolicy"/>,
/// SPEC-joylashuvlar §C): «Verifikatsiya → Joylashuvlar» ekrani
/// <c>attendance.view</c> bilan ochiladi, ma'lumotnoma tanlash
/// ro'yxatlari esa <c>catalog.view</c> bilan ishlaydi. Ikkalasi ham
/// shunchaki nomlar va chegaralar ro'yxatini ko'radi.
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
/// Shakl (doira yoki ko'pburchak) — SPEC-joylashuvlar §A–C. Ko'pburchak
/// tekshiruvi domenda (<c>WorkSite.CreatePolygon</c>), shu sababli bu yerda
/// faqat tana buyruqqa o'giriladi.
/// </para>
/// </remarks>
public static class WorkSiteEndpoints
{
    /// <summary>
    /// O'qish siyosati: <c>attendance.view</c> YOKI <c>catalog.view</c>.
    /// </summary>
    /// <remarks>
    /// Nomli siyosat emas, shu yerda quriladi: «ikki ruxsatdan biri» faqat
    /// shu guruhda kerak va umumiy ro'yxatga (<c>AddBonviAuthorization</c>)
    /// qo'shilsa, u yerda hech narsaga tegishli bo'lmagan nom bo'lib
    /// turardi. Sxema ko'rsatilmagan — HR sukut sxemasi, ruxsat
    /// siyosatlaridagi bilan bir xil; xodim tokeni bu yerdan o'tmaydi.
    /// </remarks>
    private static readonly AuthorizationPolicy ReadPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(CurrentUser.PermissionClaimType, Permissions.AttendanceView, Permissions.CatalogView)
        .Build();

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
            .RequireAuthorization(ReadPolicy)
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
            .RequireAuthorization(ReadPolicy)
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
                    .HandleAsync(new CreateWorkSiteCommand(request.ToCommand()), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("CreateWorkSite")
            .WithSummary("Obyektlar — yangi obyekt")
            .WithDescription(
                "«shape» qaysi maydonlar o'qilishini hal qiladi: «Circle» — latitude, longitude, " +
                "radiusMeters (majburiy); «Polygon» — polygon (3…50 uch, majburiy) va edgeBufferMeters " +
                "(0…500, sukut 50); ko'pburchak markazini server hisoblaydi. " +
                "Xatolar (400): «malumotnoma.nom_bosh», «obyekt.shakl_notogri», «davomat.obyekt_kenglik», " +
                "«davomat.obyekt_uzunlik», «davomat.obyekt_radiusi», «obyekt.polygon_nuqtalar_soni», " +
                "«obyekt.polygon_koordinata», «obyekt.polygon_kesishadi», «obyekt.polygon_zaxira», " +
                "«obyekt.polygon_katta», «obyekt.manzil_uzun»; «malumotnoma.nom_band» (409).")
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
                    .HandleAsync(new UpdateWorkSiteCommand(id, request.ToCommand()), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("UpdateWorkSite")
            .WithSummary("Obyektlar — tahrirlash")
            .WithDescription(
                "Tana va xatolar yaratishdagi bilan bir xil; shaklni almashtirish mumkin. " +
                "Ko'pburchakda edgeBufferMeters berilmasa hozirgi qiymati qoladi. " +
                "Radius, zaxira yoki shaklni o'zgartirish TARIXNI qayta yozmaydi: har bir qabul " +
                "qilingan belgi o'sha lahzadagi masofa, ruxsat va shaklni o'zida saqlaydi.")
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
