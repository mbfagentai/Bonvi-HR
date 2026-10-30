using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.VacancyRequests;
using Bonvi.Application.Reports.Queries;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Yangi vakansiya so'rovi tanasi.</summary>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="RequestedByEmployeeId">So'ragan xodim; bo'sh — bo'lim boshlig'i.</param>
/// <param name="Title">Lavozim.</param>
/// <param name="Openings">Nechta odam.</param>
/// <param name="NeededBy">Qachondan kerak.</param>
/// <param name="Note">Sabab va talablar.</param>
public sealed record CreateVacancyRequestBody(
    Guid DepartmentId,
    Guid? RequestedByEmployeeId,
    string? Title,
    int Openings = 1,
    DateOnly? NeededBy = null,
    string? Note = null);

/// <summary>Rad etish tanasi.</summary>
/// <param name="Reason">Sabab — majburiy.</param>
public sealed record RejectVacancyRequestBody(string? Reason);

/// <summary>
/// «Vakansiya so'rovlari» menyusi — bo'lim boshliqlarining yangi vakansiya so'rovlari
/// (docs/SPEC-vakansiya-sorovi.md).
/// </summary>
public static class VacancyRequestEndpoints
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapVacancyRequestEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/vacancy-requests").WithTags("Recruiting");

        group.MapGet("/", async (
                string? status,
                string? search,
                DateOnly? from,
                DateOnly? to,
                int? page,
                int? pageSize,
                ListVacancyRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListVacancyRequestsQuery(status, search, from, to, page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListVacancyRequests")
            .WithSummary("Vakansiya so'rovlari — ro'yxat")
            .WithDescription(
                "Yangidan eskiga. «status» — Pending | Approved | Rejected | Cancelled (bo'sh — hammasi, " +
                "noma'lum qiymat «vakansiya_sorovi.holat_notogri» 400). «from»/«to» — yuborilgan sana " +
                "(Toshkent kuni, ikkalasi ham kiradi). «search» — lavozim, bo'lim nomi yoki so'ragan " +
                "xodim. «pageSize» eng ko'pi 10 000.")
            .Produces<PagedResult<VacancyRequestDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/export", async (
                string? status,
                string? search,
                DateOnly? from,
                DateOnly? to,
                ExportVacancyRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListVacancyRequestsQuery(status, search, from, to), cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, XlsxContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.RecruitingView, Permissions.ReportsExport)
            .RequireRateLimiting(RateLimitPolicies.Export)
            .WithName("ExportVacancyRequestsToExcel")
            .WithSummary("Vakansiya so'rovlari — Excel eksport")
            .WithDescription(
                "Filtrlar «GET /api/recruiting/vacancy-requests» bilan AYNAN bir xil, sahifasiz. Eng ko'pi " +
                "5000 qator («eksport.juda_kop» 400). Ruxsat: «recruiting.view» VA «reports.export». Har bir " +
                "eksport audit jurnaliga yoziladi (qidiruv matnisiz).")
            .Produces(StatusCodes.Status200OK, contentType: XlsxContentType)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/", async (
                CreateVacancyRequestBody body,
                CreateVacancyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateVacancyRequestCommand(
                            body.DepartmentId,
                            body.RequestedByEmployeeId,
                            body.Title,
                            body.Openings,
                            body.NeededBy,
                            body.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("CreateVacancyRequest")
            .WithSummary("Vakansiya so'rovi — kiritish")
            .WithDescription(
                "Bo'lim boshlig'ining so'rovi menyuda kiritiladi. «requestedByEmployeeId» bo'sh bo'lsa — bo'lim " +
                "boshlig'i; boshliqsiz bo'limda «vakansiya_sorovi.boshliq_yoq» 400. «openings» 1..100. " +
                "Ruxsat «recruiting.view»: menyuni ko'rgan har kim so'rovni yozib qo'ya oladi, QAROR esa " +
                "«recruiting.manage» da.")
            .Produces<VacancyRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelVacancyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new CancelVacancyRequestCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("CancelVacancyRequest")
            .WithSummary("Vakansiya so'rovi — bekor qilish")
            .WithDescription("Faqat «Pending» — aks holda «vakansiya_sorovi.qaror_qilingan» 409.")
            .Produces<VacancyRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/approve", async (
                Guid id,
                SaveVacancyRequest body,
                ApproveVacancyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ApproveVacancyRequestCommand(id, body.Title, body.Openings, body.OpenedAt, body.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ApproveVacancyRequest")
            .WithSummary("Vakansiya so'rovi — vakansiya ochish")
            .WithDescription(
                "Tana «POST /api/recruiting/vacancies» bilan bir xil — vakansiya o'sha qoidalar bilan OCHILADI " +
                "(nom band bo'lsa «vakansiya.nom_band» 409) va so'rov «Approved» bo'lib unga bog'lanadi, bitta " +
                "saqlashda. Qaror qilingan so'rov — «vakansiya_sorovi.qaror_qilingan» 409.")
            .Produces<VacancyRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", async (
                Guid id,
                RejectVacancyRequestBody body,
                RejectVacancyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RejectVacancyRequestCommand(id, body.Reason), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("RejectVacancyRequest")
            .WithSummary("Vakansiya so'rovi — rad etish")
            .WithDescription("«reason» majburiy («vakansiya_sorovi.sabab_kerak» 400).")
            .Produces<VacancyRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
