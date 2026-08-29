using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.Vacancies;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Vakansiya ochish/tahrirlash so'rovi tanasi.</summary>
/// <remarks>
/// <b>Lavozim, bo'lim va yuridik shaxs maydonlari YO'Q.</b> Vakansiya —
/// e'lon, kompaniya tuzilmasidan unda hech narsa saqlanmaydi (batafsil
/// sabab <c>Vacancy</c> domen izohida). Uchalasi taklif QABUL
/// QILINGANDA so'raladi: <c>POST /api/recruiting/offers/{id}/accept</c>.
/// </remarks>
/// <param name="VacancyTitleId">
/// Vakansiya nomi — «vacancy-titles» ma'lumotnomasidan. Majburiy va
/// yagona bog'lanish.
/// </param>
/// <param name="Openings">Nechta odam kerak (1–100). Sukut — 1.</param>
/// <param name="OpenedAt">Ochilgan sana. Bo'sh — bugun (tahrirlashda o'zgarmaydi).</param>
/// <param name="ResponsibleEmployeeId">Mas'ul xodim (rekruter).</param>
/// <param name="Note">Izoh: shoshilinchlik, talablar.</param>
public sealed record SaveVacancyRequest(
    Guid VacancyTitleId,
    int Openings = 1,
    DateOnly? OpenedAt = null,
    Guid? ResponsibleEmployeeId = null,
    string? Note = null);

/// <summary>Vakansiyani to'xtatish so'rovi tanasi.</summary>
public sealed record PauseVacancyRequest(string? Reason = null);

/// <summary>Vakansiyani yopish so'rovi tanasi.</summary>
/// <param name="ClosedAt">Yopilish sanasi. Bo'sh — bugun.</param>
/// <param name="Reason">Nima uchun yopildi — izohga yoziladi.</param>
public sealed record CloseVacancyRequest(DateOnly? ClosedAt = null, string? Reason = null);

/// <summary>
/// Vakansiyalar — rekrutingning boshlanish nuqtasi (2-bosqich).
/// </summary>
/// <remarks>
/// Vakansiyaning ko'rinadigan nomi O'ZINING ma'lumotnomasidan keladi
/// («vacancy-titles»), kompaniya lavozimlaridan emas — nega ular ataylab
/// ikki xil tushuncha ekani <c>VacancyTitle</c> izohida.
/// </remarks>
public static class VacancyEndpoints
{
    public static IEndpointRouteBuilder MapVacancyEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/vacancies").WithTags("Recruiting");

        group.MapGet("/", async (
                string? status,
                Guid? vacancyTitleId,
                int? page,
                int? pageSize,
                string? sort,
                ListVacanciesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListVacanciesQuery(status, vacancyTitleId, page, pageSize, sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListVacancies")
            .WithSummary("Vakansiyalar — ro'yxat")
            .WithDescription(
                "SERVER tomonda sahifalanadi, filtrlanadi va saralanadi. " +
                "«status» — Open | Paused | Closed (bo'sh — hammasi, noma'lum qiymat " +
                "«vakansiya.holat_notogri» 400). «vacancyTitleId» — e'lon nomi bo'yicha " +
                "filtr; lavozim/bo'lim/firma filtrlari YO'Q, chunki vakansiyada bunday " +
                "maydonlar yo'q. «sort» — openedAt (sukut, yangidan eskiga) yoki title; " +
                "teskari tartib «-openedAt» yoki «openedAt:desc». " +
                "«pageSize» eng ko'pi 200. Har bir qatorda «candidateCount» va " +
                "«hiredCount» bor — «2/3 to'ldirildi» ko'rinishi uchun.")
            .Produces<PagedResult<VacancyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetVacancyQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetVacancyQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("GetVacancy")
            .WithSummary("Vakansiya — bitta yozuv")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                SaveVacancyRequest request,
                OpenVacancyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new OpenVacancyCommand(
                            request.VacancyTitleId,
                            request.Openings,
                            request.OpenedAt,
                            request.ResponsibleEmployeeId,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("OpenVacancy")
            .WithSummary("Vakansiya — ochish")
            .WithDescription(
                "«vacancyTitleId» MAJBURIY va u «/api/catalog/vacancy-titles» " +
                "ma'lumotnomasidan olinadi — bu KOMPANIYA LAVOZIMI EMAS, e'londa " +
                "turadigan qisqa nom. Lavozim, bo'lim va firma bu yerda umuman " +
                "so'ralmaydi: ular «/offers/{id}/accept» da tanlanadi. " +
                "Xatolar: «vakansiya.nom_yoq» (400), «vakansiya.nom_topilmadi» va " +
                "«vakansiya.masul_topilmadi» (404); «vakansiya.orin_soni» (400).")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                SaveVacancyRequest request,
                UpdateVacancyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateVacancyCommand(
                            id,
                            request.VacancyTitleId,
                            request.Openings,
                            request.OpenedAt,
                            request.ResponsibleEmployeeId,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("UpdateVacancy")
            .WithSummary("Vakansiya — tahrirlash")
            .WithDescription(
                "Vakansiya nomi ham o'zgaradi (vakansiya — reja, fakt emas). " +
                "HOLAT bu yerdan o'zgarmaydi: «/pause», «/resume», «/close» bor. " +
                "«openedAt» bo'sh bo'lsa — eski sana saqlanadi.")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/pause", async (
                Guid id,
                PauseVacancyRequest? request,
                ChangeVacancyStateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new PauseVacancyCommand(id, request?.Reason), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("PauseVacancy")
            .WithSummary("Vakansiya — vaqtincha to'xtatish")
            .WithDescription(
                "To'xtatilgan vakansiyaga YANGI nomzod kiritilmaydi " +
                "(«vakansiya.qabul_qilmaydi», 409) — to'xtatishning ma'nosi shu. " +
                "Yopilgan vakansiyani to'xtatib bo'lmaydi: «vakansiya.yopiq» (409).")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/resume", async (
                Guid id,
                ChangeVacancyStateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ResumeVacancyCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ResumeVacancy")
            .WithSummary("Vakansiya — qayta ochish")
            .WithDescription(
                "To'xtatilgan VA yopilgan vakansiya ham qayta ochiladi: olingan odam " +
                "bir haftada ketib qolishi odatiy hol va yangi vakansiya ochish " +
                "nomzodlar tarixini ikkiga bo'lib yuborardi. «closedAt» tozalanadi.")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/close", async (
                Guid id,
                CloseVacancyRequest? request,
                ChangeVacancyStateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CloseVacancyCommand(id, request?.ClosedAt, request?.Reason),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("CloseVacancy")
            .WithSummary("Vakansiya — yopish")
            .WithDescription(
                "«closedAt» bo'sh — bugun. O'rin to'lganda vakansiya O'ZI yopilmaydi: " +
                "bu HR ning qarori (ro'yxatdagi «hiredCount» unga asos beradi). " +
                "Xatolar: «vakansiya.allaqachon_yopiq» (409), «vakansiya.yopish_sanasi» (400).")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteVacancyCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteVacancyCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeleteVacancy")
            .WithSummary("Vakansiya — yozuvni o'chirish")
            .WithDescription(
                "Yumshoq o'chirish: yozuv bazada qoladi va unga bog'langan nomzodlar " +
                "yo'qolmaydi. Xato kiritilgan vakansiya uchun; ish tugagan vakansiya " +
                "esa YOPILADI, o'chirilmaydi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
