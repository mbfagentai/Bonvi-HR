using Bonvi.Api.Extensions;
using Bonvi.Application.Catalog.Commands;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Catalog.Departments;
using Bonvi.Application.Catalog.Dtos;
using Bonvi.Application.Catalog.Positions;
using Bonvi.Application.Catalog.Queries;
using Bonvi.Application.Catalog.Usage;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;

namespace Bonvi.Api.Endpoints;

/// <summary>Yozuvlar tartibini o'zgartirish so'rovi tanasi.</summary>
/// <param name="OrderedIds">Yangi ketma-ketlikdagi identifikatorlar.</param>
public sealed record ReorderRequest(IReadOnlyList<Guid> OrderedIds);

/// <summary>Lavozimga vazifa qo'shish so'rovi tanasi.</summary>
/// <param name="Text">Vazifa matni.</param>
public sealed record AddDutyRequest(string? Text);

/// <summary>Bo'limni ko'chirish so'rovi tanasi.</summary>
/// <param name="NewParentId">Yangi ota bo'lim. <c>null</c> — eng yuqori daraja.</param>
public sealed record MoveDepartmentRequest(Guid? NewParentId);

/// <summary>Bo'lim boshlig'ini almashtirish tanasi.</summary>
/// <param name="EmployeeId">Yangi boshliq. <c>null</c> — boshliqni olib tashlash.</param>
public sealed record SetDepartmentHeadRequest(Guid? EmployeeId);

/// <summary>Faol emas qilinadigan yozuvdagi xodimlar o'tadigan joy (docs/SPEC-malumotnoma-faolsizlantirish.md §C).</summary>
public sealed record ReassignReferenceRequest(
    Guid TargetId,
    DateOnly? EffectiveDate,
    string? OrderNo,
    DateOnly? OrderDate);

/// <summary>
/// Ma'lumotnomalar endpoint'lari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu fayl qisqa.</b> Sakkizta ma'lumotnomaning yettitadan amali —
/// bu 56 ta endpoint. Ularning har biri qo'lda yozilsa, fayl 2000 qatordan
/// oshardi va bitta o'zgarish (masalan yangi ruxsat) 56 joyga tegishni
/// talab qilardi. Shu sababli marshrutlar generik metodlar bilan
/// «shtamplanadi»: har bir ma'lumotnoma uchun bitta chaqiruv.
/// </para>
/// <para>
/// <b>Yo'l generik EMAS.</b> <c>/api/catalog/{tur}</c> ko'rinishidagi bitta
/// marshrut o'rniga har bir ma'lumotnomaga aniq yo'l beriladi
/// (<c>/api/catalog/legal-entities</c>). Sabab: OpenAPI hujjatida har bir
/// yo'l o'z javob tipini ko'rsatishi kerak — frontend klienti aynan
/// shundan generatsiya qilinadi va <c>tur</c> bo'yicha tarmoqlanuvchi
/// bitta marshrut unga tipni bera olmaydi.
/// </para>
/// </remarks>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        // ---------- Oddiy ma'lumotnomalar: hammasi bir xil ishlaydi ----------
        MapSimpleReference<LegalEntity, LegalEntityDto>(
            group, "legal-entities", "LegalEntities", "LegalEntity", "Yuridik shaxslar");

        MapSimpleReference<Direction, DirectionDto>(
            group, "directions", "Directions", "Direction", "Yo'nalishlar");

        MapReassign<ReassignDirectionCommandHandler>(
            group, "directions", "Direction", "Yo'nalish",
            (handler, command, cancellationToken) => handler.HandleAsync(command, cancellationToken));

        MapSimpleReference<TerminationReason, TerminationReasonDto>(
            group, "termination-reasons", "TerminationReasons", "TerminationReason", "Ishdan ketish sabablari");

        MapSimpleReference<AwardType, AwardTypeDto>(
            group, "award-types", "AwardTypes", "AwardType", "Rag'bat turlari");

        MapSimpleReference<PenaltyType, PenaltyTypeDto>(
            group, "penalty-types", "PenaltyTypes", "PenaltyType", "Jarima turlari");

        MapSimpleReference<DocumentType, DocumentTypeDto>(
            group, "document-types", "DocumentTypes", "DocumentType", "Hujjat turlari");

        // Yo'qlik turlari — davomat so'rovlari shu ro'yxatdan tanlanadi
        // (SPEC-so'rovlar §B1). Mexanikasi yuqoridagilar bilan bir xil, shu
        // sababli u ham shu yerda: modul boshqa bo'lsa ham, ma'lumotnoma
        // ma'lumotnoma bo'lib qoladi va HR uni «Ma'lumotnomalar» bo'limida
        // qidiradi.
        MapSimpleReference<AbsenceType, AbsenceTypeDto>(
            group, "absence-types", "AbsenceTypes", "AbsenceType", "Yo'qlik turlari");

        // «Qayerdan bilgan» — nomzod ish haqida qayerdan bilgani (SPEC-qayerdan-bilgan §3).
        // Yo'qlik turlari bilan bir xil sabab: rekrutingga tegishli bo'lsa ham, ma'lumotnoma
        // HR uni qidiradigan joyda. Tanlash ro'yxati rekruting ruxsati bilan alohida —
        // `/api/recruiting/referral-channels` (bot va rekruter `catalog.view` siz o'qiydi).
        MapSimpleReference<ReferralChannel, ReferralChannelDto>(
            group, "referral-channels", "ReferralChannels", "ReferralChannel", "Qayerdan bilgan");

        // ---------- Daraxt va vazifalar: o'z qoidalari bor ----------
        MapDepartments(group);
        MapPositions(group);

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Oddiy ma'lumotnoma — yettita amal
    // ════════════════════════════════════════════════════════════

    private static void MapSimpleReference<TEntity, TDto>(
        IEndpointRouteBuilder group,
        string path,
        string plural,
        string singular,
        string title)
        where TEntity : ReferenceEntity
    {
        MapReads<TEntity, TDto>(group, path, plural, singular, title);
        MapWrites<TEntity, TDto>(group, path, singular, title);
        MapState<TEntity>(group, path, plural, singular, title);
    }

    /// <summary>Ro'yxat va bitta yozuv.</summary>
    private static void MapReads<TEntity, TDto>(
        IEndpointRouteBuilder group,
        string path,
        string plural,
        string singular,
        string title)
        where TEntity : ReferenceEntity
    {
        group.MapGet("/" + path, async (
                bool? onlyActive,
                ListReferenceQueryHandler<TEntity, TDto> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListReferenceQuery(onlyActive ?? false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("List" + plural)
            .WithSummary(title + " — ro'yxat")
            .WithDescription(
                "Tartib bo'yicha saralangan ro'yxat. «onlyActive=true» — faqat faol yozuvlar " +
                "(tanlash ro'yxatlari uchun). Sukut bo'yicha hammasi qaytariladi.")
            .Produces<IReadOnlyList<TDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/" + path + "/{id:guid}", async (
                Guid id,
                GetReferenceByIdQueryHandler<TEntity, TDto> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetReferenceByIdQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("Get" + singular)
            .WithSummary(title + " — bitta yozuv")
            .WithDescription("Identifikator bo'yicha bitta ma'lumotnoma yozuvi.")
            .Produces<TDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>Yaratish va tahrirlash.</summary>
    /// <remarks>
    /// Yaratishda 201 emas, 200 qaytariladi: <c>Location</c> sarlavhasi uchun
    /// yangi yozuvning identifikatori kerak, u esa generik <c>TDto</c> ichida —
    /// uni tipni bilmasdan olib bo'lmaydi. Mijoz to'liq obyektni javobdan
    /// oladi, ya'ni qo'shimcha so'rov ham kerak emas.
    /// </remarks>
    private static void MapWrites<TEntity, TDto>(
        IEndpointRouteBuilder group,
        string path,
        string singular,
        string title)
        where TEntity : ReferenceEntity
    {
        group.MapPost("/" + path, async (
                SaveReferenceCommand command,
                CreateReferenceCommandHandler<TEntity, TDto> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("Create" + singular)
            .WithSummary(title + " — yangi yozuv")
            .WithDescription(
                "Nom majburiy va takrorlanmasligi kerak («malumotnoma.nom_band»). " +
                "So'rov tanasi barcha ma'lumotnomalar uchun umumiy — turga tegishli " +
                "bo'lmagan maydonlar e'tiborsiz qoldiriladi.")
            .Produces<TDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/" + path + "/{id:guid}", async (
                Guid id,
                SaveReferenceCommand command,
                UpdateReferenceCommandHandler<TEntity, TDto> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("Update" + singular)
            .WithSummary(title + " — tahrirlash")
            .WithDescription("Mavjud yozuvni tahrirlaydi. Nom takrorlanmasligi tekshiriladi.")
            .Produces<TDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>Faollik holati va tartib.</summary>
    private static void MapState<TEntity>(
        IEndpointRouteBuilder group,
        string path,
        string plural,
        string singular,
        string title)
        where TEntity : ReferenceEntity
    {
        group.MapPost("/" + path + "/{id:guid}/deactivate", async (
                Guid id,
                string? reason,
                DeactivateReferenceCommandHandler<TEntity> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeactivateReferenceCommand(id, reason), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("Deactivate" + singular)
            .WithSummary(title + " — faolsizlantirish")
            .WithDescription(
                "Yozuv O'CHIRILMAYDI, faqat yangi formalarda tanlanmaydigan bo'ladi. " +
                "«reason» — nima uchun, izohga yoziladi. Ishlayotgan xodim biriktirilgan bo'lsa — 409 " +
                "(«malumotnoma.faol_xodim_bor»); bo'lim quyi bo'limlari bilan birga faol emas bo'ladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/" + path + "/{id:guid}/usage", async (
                Guid id,
                GetReferenceUsageQueryHandler<TEntity> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetReferenceUsageQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("Get" + singular + "Usage")
            .WithSummary(title + " — kim biriktirilgan")
            .WithDescription(
                "Faol emas qilishdan oldin: yozuvga biriktirilgan, hozir ishlayotgan xodimlar. " +
                "Ular bor ekan, «deactivate» 409 qaytaradi; bo'lim, lavozim va yo'nalishda ular «reassign» bilan o'tkaziladi.")
            .Produces<ReferenceUsageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/" + path + "/{id:guid}/activate", async (
                Guid id,
                ActivateReferenceCommandHandler<TEntity> handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ActivateReferenceCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("Activate" + singular)
            .WithSummary(title + " — qayta yoqish")
            .WithDescription("Faolsizlantirilgan yozuvni qaytaradi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Ishlayotgan xodimlarni boshqa yozuvga o'tkazib, keyin faol emas qilish (egasi, 2026-10-31).
    /// </summary>
    /// <remarks>Qo'shimcha huquq (o'tkazish yoki xodimni tahrirlash) handler ichida tekshiriladi.</remarks>
    private static void MapReassign<THandler>(
        IEndpointRouteBuilder group,
        string path,
        string singular,
        string title,
        Func<THandler, ReassignReferenceCommand, CancellationToken, Task<Result>> handle)
        where THandler : notnull
    {
        group.MapPost("/" + path + "/{id:guid}/reassign", async (
                Guid id,
                ReassignReferenceRequest request,
                THandler handler,
                CancellationToken cancellationToken) =>
            {
                var command = new ReassignReferenceCommand(
                    id, request.TargetId, request.EffectiveDate, request.OrderNo, request.OrderDate);

                var natija = await handle(handler, command, cancellationToken).ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("Reassign" + singular)
            .WithSummary(title + " — xodimlarni o'tkazib, faol emas qilish")
            .WithDescription(
                "Hozir ishlayotgan hamma biriktirilgan xodim «targetId» ga o'tadi (bo'lim va lavozimda — har biriga " +
                "o'tkazish yozuvi), keyin yozuv faol emas bo'ladi. Hammasi bitta saqlashda.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Bo'limlar — daraxt
    // ════════════════════════════════════════════════════════════

    private static void MapDepartments(IEndpointRouteBuilder group)
    {
        // O'qish va holat — umumiy shtamp bilan
        MapReads<Department, DepartmentDto>(group, "departments", "Departments", "Department", "Bo'limlar");
        MapState<Department>(group, "departments", "Departments", "Department", "Bo'limlar");

        MapReassign<ReassignDepartmentCommandHandler>(
            group, "departments", "Department", "Bo'lim",
            (handler, command, cancellationToken) => handler.HandleAsync(command, cancellationToken));

        group.MapGet("/departments/tree", async (
                bool? onlyActive,
                GetDepartmentTreeQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetDepartmentTreeQuery(onlyActive ?? false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("GetDepartmentTree")
            .WithSummary("Bo'limlar daraxti")
            .WithDescription(
                "Butun ierarxiya bitta javobda, ichma-ich. Interfeys uni ochib-yopib ko'rsatadi " +
                "va har bir tugun uchun alohida so'rov yubormaydi.")
            .Produces<IReadOnlyList<DepartmentTreeNodeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/departments", async (
                SaveReferenceCommand command,
                CreateDepartmentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("CreateDepartment")
            .WithSummary("Bo'limlar — yangi bo'lim")
            .WithDescription(
                "«parentId» berilsa, bo'lim shu bo'lim ichida yaratiladi. " +
                "Yozilgandan keyin butun daraxtning darajasi va yo'li qayta hisoblanadi.")
            .Produces<DepartmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/departments/{id:guid}", async (
                Guid id,
                SaveReferenceCommand command,
                UpdateDepartmentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("UpdateDepartment")
            .WithSummary("Bo'limlar — tahrirlash")
            .WithDescription(
                "Ota bo'lim BU YERDA o'zgarmaydi — buning uchun alohida «ko'chirish» amali bor.")
            .Produces<DepartmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/departments/{id:guid}/move", async (
                Guid id,
                MoveDepartmentRequest request,
                MoveDepartmentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new MoveDepartmentCommand(id, request.NewParentId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("MoveDepartment")
            .WithSummary("Bo'limlar — daraxtda ko'chirish")
            .WithDescription(
                "Bo'limni butun avlodlari bilan boshqa ota bo'limga ko'chiradi. " +
                "«newParentId: null» — eng yuqori darajaga chiqaradi. " +
                "Bo'limni o'z avlodiga ko'chirib bo'lmaydi: «bolim.halqa» (409).")
            .Produces<DepartmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/departments/{id:guid}/head", async (
                Guid id,
                SetDepartmentHeadRequest request,
                SetDepartmentHeadCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetDepartmentHeadCommand(id, request.EmployeeId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("SetDepartmentHead")
            .WithSummary("Bo'limlar — boshliqni almashtirish")
            .WithDescription(
                "Faqat boshliq o'zgaradi (xodim kartasidan). «employeeId: null» — boshliqni olib tashlash. " +
                "Bo'limda boshqa boshliq bo'lsa, u almashtiriladi. «catalog.manage» ustiga «attendance.view» ham kerak. " +
                "Xatolar: «auth.ruxsat_yoq» (403), «bolim.topilmadi» (404), «bolim.boshliq_topilmadi» (400).")
            .Produces<DepartmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ════════════════════════════════════════════════════════════
    //  Lavozimlar — vazifalari bilan
    // ════════════════════════════════════════════════════════════

    private static void MapPositions(IEndpointRouteBuilder group)
    {
        MapState<Position>(group, "positions", "Positions", "Position", "Lavozimlar");

        MapReassign<ReassignPositionCommandHandler>(
            group, "positions", "Position", "Lavozim",
            (handler, command, cancellationToken) => handler.HandleAsync(command, cancellationToken));

        group.MapGet("/positions", async (
                bool? onlyActive,
                Guid? departmentId,
                ListPositionsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListPositionsQuery(onlyActive ?? false, departmentId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("ListPositions")
            .WithSummary("Lavozimlar — ro'yxat")
            .WithDescription(
                "Vazifalarsiz ro'yxat. «departmentId» — bo'lim bo'yicha filtr " +
                "(ishga qabul formasida bo'lim tanlangach ishlatiladi).")
            .Produces<IReadOnlyList<PositionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/positions/{id:guid}", async (
                Guid id,
                GetPositionQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetPositionQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("GetPosition")
            .WithSummary("Lavozimlar — bitta lavozim")
            .WithDescription("Lavozim VAZIFALARI BILAN qaytariladi.")
            .Produces<PositionDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/positions", async (
                SaveReferenceCommand command,
                CreatePositionCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("CreatePosition")
            .WithSummary("Lavozimlar — yangi lavozim")
            .WithDescription(
                "«grade» 1–5, «probationDays» 1–180 yoki bo'sh (bo'sh — ishga qabulda 90 kun). " +
                "Vazifalar keyin alohida qo'shiladi.")
            .Produces<PositionDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/positions/{id:guid}", async (
                Guid id,
                SaveReferenceCommand command,
                UpdatePositionCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, command, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("UpdatePosition")
            .WithSummary("Lavozimlar — tahrirlash")
            .WithDescription("Vazifalar bu yerda o'zgarmaydi — ular uchun alohida amallar bor.")
            .Produces<PositionDetailsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        MapPositionDuties(group);
    }

    /// <summary>Lavozim vazifalari — HR'ning «avtomatik yuklanadi» talabi.</summary>
    private static void MapPositionDuties(IEndpointRouteBuilder group)
    {
        group.MapGet("/positions/{id:guid}/duties", async (
                Guid id,
                GetPositionDutiesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetPositionDutiesQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogView)
            .WithName("GetPositionDuties")
            .WithSummary("Lavozim vazifalari")
            .WithDescription(
                "⭐ Ishga qabul formasi uchun: lavozim tanlanganda vazifalar avtomatik yuklanadi. " +
                "Javob ataylab kichik — u har bir tanlashda so'raladi.")
            .Produces<IReadOnlyList<PositionDutyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/positions/{id:guid}/duties", async (
                Guid id,
                AddDutyRequest request,
                AddPositionDutyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new AddPositionDutyCommand(id, request.Text), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("AddPositionDuty")
            .WithSummary("Lavozim vazifasi — qo'shish")
            .WithDescription("Javobda yangilangan TO'LIQ ro'yxat qaytariladi.")
            .Produces<IReadOnlyList<PositionDutyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/positions/{id:guid}/duties/{dutyId:guid}", async (
                Guid id,
                Guid dutyId,
                RemovePositionDutyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RemovePositionDutyCommand(id, dutyId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("RemovePositionDuty")
            .WithSummary("Lavozim vazifasi — o'chirish")
            .WithDescription(
                "Mavjud bo'lmagan vazifa uchun ham muvaffaqiyat qaytariladi: " +
                "natija baribir kerakli holat.")
            .Produces<IReadOnlyList<PositionDutyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/positions/{id:guid}/duties/order", async (
                Guid id,
                ReorderRequest request,
                ReorderPositionDutiesCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ReorderPositionDutiesCommand(id, request.OrderedIds), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.CatalogManage)
            .WithName("ReorderPositionDuties")
            .WithSummary("Lavozim vazifalari — tartibni o'zgartirish")
            .WithDescription("Vazifalar yo'riqnomada muhimlik bo'yicha yoziladi, tartib mazmunli.")
            .Produces<IReadOnlyList<PositionDutyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
