using Bonvi.Api.Extensions;
using Bonvi.Application.Blanks;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// HRMentor blankalari (SPEC-blankalar-hrmentor §6): ro'yxat, fayl va «Hozir yangilash».
/// </summary>
/// <remarks>
/// Ko'rish — <see cref="Permissions.EmployeesView"/>: blankalar butun HR ning qog'ozi, faqat
/// rekruting emas, va yangi ruxsat rollar migratsiyasini talab qilardi. Yangilash —
/// <see cref="Permissions.SettingsManage"/>: u HRMentor ga so'rov yuboradi va ulanish
/// sozlamalari ham o'sha ruxsat ostida.
/// </remarks>
public static class BlankEndpoints
{
    public static IEndpointRouteBuilder MapBlankEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/blanks").WithTags("Blanks");

        group.MapGet("/", async (GetBlanksQueryHandler handler, CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetBlanksQuery(), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetBlanks")
            .WithSummary("Blankalar — HRMentor dan olingan nusxalar")
            .WithDescription(
                "Ulanish holati (oxirgi urinish, oxirgi xatosiz yangilanish, xato kodi), amaldagi " +
                "anketa matnlari reviziyasi va blankalar — kod bo'yicha.")
            .Produces<BlanksDto>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}/file", async (
                Guid id,
                GetBlankFileQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new GetBlankFileQuery(id), cancellationToken).ConfigureAwait(false);

                // JSON emas — BAYT; nom `Content-Disposition: attachment` da (RFC 5987).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, natija.Value.ContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("DownloadBlank")
            .WithSummary("Blanka — fayl yuklab olish")
            .WithDescription("Blankaning joriy versiyasi fayli. Topilmasa: «blanka.topilmadi» (404).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/sync", async (SyncBlanksCommandHandler handler, CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(new SyncBlanksCommand(), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("SyncBlanks")
            .WithSummary("Blankalar — HRMentor dan hozir yangilash")
            .WithDescription(
                "Besh daqiqalik yurishni darhol bajaradi va sahifa javobini qaytaradi. Ulanmagan: " +
                "«hrmentor.not_connected» (400); boshqa yurish 120 soniyada tugamadi: «hrmentor.busy» (409). " +
                "HRMentor xatosi so'rovni yiqitmaydi — u javobdagi «lastError» da.")
            .Produces<BlanksDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
