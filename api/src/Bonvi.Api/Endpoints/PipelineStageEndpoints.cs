using Bonvi.Api.Extensions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.Stages;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Voronka bosqichini yaratish/tahrirlash so'rovi tanasi.</summary>
/// <param name="Name">Bosqich nomi. Majburiy va takrorlanmaydi.</param>
/// <param name="Note">Ixtiyoriy izoh.</param>
public sealed record SavePipelineStageRequest(string? Name, string? Note = null);

/// <summary>Voronkani qayta tartiblash so'rovi tanasi.</summary>
/// <param name="OrderedIds">Yangi ketma-ketlikdagi BARCHA bosqichlar.</param>
public sealed record ReorderPipelineStagesRequest(IReadOnlyList<Guid> OrderedIds);

/// <summary>Bosqichni faolsizlantirish so'rovi tanasi.</summary>
/// <param name="Reason">Nima uchun — izohga yoziladi.</param>
public sealed record DeactivatePipelineStageRequest(string? Reason = null);

/// <summary>
/// Voronka bosqichlari — rekruting ma'lumotnomasi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>/api/catalog</c> ostida emas.</b> Bosqich texnik jihatdan
/// ma'lumotnoma, lekin uning ikkita xususiyati umumiy ma'lumotnoma
/// mexanizmiga sig'maydi: ro'yxat ALIFBO emas, VORONKA tartibida qaytadi
/// va uni sudrab qayta tartiblash mumkin. Umumiy <c>/api/catalog/{tur}</c>
/// shtampiga qo'shilsa, frontenddagi bitta generik sahifa ularni alifboda
/// chizib, voronkani buzardi.
/// </para>
/// <para>
/// Ruxsat ham shu sababdan <see cref="Permissions.RecruitingManage"/>,
/// <see cref="Permissions.CatalogManage"/> emas: bosqichlar ro'yxati —
/// HR ning TANLOV JARAYONI ta'rifi, ma'lumotnomaga yangi lavozim qo'shish
/// esa butunlay boshqa amal.
/// </para>
/// </remarks>
public static class PipelineStageEndpoints
{
    public static IEndpointRouteBuilder MapPipelineStageEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/stages").WithTags("Recruiting");

        group.MapGet("/", async (
                bool? onlyActive,
                ListPipelineStagesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListPipelineStagesQuery(onlyActive ?? false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListPipelineStages")
            .WithSummary("Voronka bosqichlari — ro'yxat")
            .WithDescription(
                "DOIM voronka tartibida («sortOrder» bo'yicha, alifbo EMAS). " +
                "«onlyActive=true» — faqat faol bosqichlar (nomzodni o'tkazish uchun " +
                "tanlash ro'yxati); sukut bo'yicha hammasi qaytariladi.")
            .Produces<IReadOnlyList<PipelineStageDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                SavePipelineStageRequest request,
                CreatePipelineStageCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CreatePipelineStageCommand(request.Name, request.Note), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("CreatePipelineStage")
            .WithSummary("Voronka bosqichi — yangi bosqich")
            .WithDescription(
                "Bosqich voronkaning OXIRIGA qo'shiladi — tartib so'ralmaydi. " +
                "O'rtaga qo'yish uchun qo'shib bo'lib «/reorder» ishlatiladi. " +
                "Xatolar: «bosqich.nom_bosh» (400), «bosqich.nom_band» (409).")
            .Produces<PipelineStageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                SavePipelineStageRequest request,
                UpdatePipelineStageCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdatePipelineStageCommand(id, request.Name, request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("UpdatePipelineStage")
            .WithSummary("Voronka bosqichi — tahrirlash")
            .WithDescription("Nom va izoh o'zgaradi. Tartib bu yerdan O'ZGARMAYDI — «/reorder» orqali.")
            .Produces<PipelineStageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/reorder", async (
                ReorderPipelineStagesRequest request,
                ReorderPipelineStagesCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ReorderPipelineStagesCommand(request.OrderedIds), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ReorderPipelineStages")
            .WithSummary("Voronka bosqichlari — tartibni o'zgartirish")
            .WithDescription(
                "«orderedIds» — YAKUNIY ketma-ketlik va unda BARCHA bosqichlar " +
                "(faolsizlantirilganlari ham) bo'lishi shart, takrorsiz. " +
                "Aks holda «bosqich.tartib_royxati» (400). Tartib 0 dan qayta " +
                "raqamlanadi. Javob — yangi tartibdagi to'liq ro'yxat.")
            .Produces<IReadOnlyList<PipelineStageDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                DeactivatePipelineStageRequest? request,
                SetPipelineStageActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SetPipelineStageActiveCommand(id, IsActive: false, request?.Reason),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeactivatePipelineStage")
            .WithSummary("Voronka bosqichi — faolsizlantirish")
            .WithDescription(
                "Bosqich O'CHIRILMAYDI: unda turgan nomzodlar joyida qoladi va " +
                "bosqich nomi ularning kartasida ko'rinib turadi. Faqat YANGI " +
                "o'tkazish rad etiladi («bosqich.faol_emas», 409). Bosqichda " +
                "nomzod turgani amalni TO'SMAYDI.")
            .Produces<PipelineStageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                SetPipelineStageActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetPipelineStageActiveCommand(id, IsActive: true), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ActivatePipelineStage")
            .WithSummary("Voronka bosqichi — qayta yoqish")
            .Produces<PipelineStageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
