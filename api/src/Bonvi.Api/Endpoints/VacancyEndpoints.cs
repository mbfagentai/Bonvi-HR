using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.Vacancies;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Vakansiya ochish/tahrirlash so'rovi tanasi.</summary>
/// <param name="PositionId">Lavozim — «positions» ma'lumotnomasidan. Majburiy.</param>
/// <param name="LegalEntityId">Yuridik shaxs — «legal-entities» dan. Majburiy.</param>
/// <param name="DepartmentId">Bo'lim — «departments» dan. Ixtiyoriy.</param>
/// <param name="Openings">Nechta odam kerak (1–100). Sukut — 1.</param>
/// <param name="OpenedAt">Ochilgan sana. Bo'sh — bugun (tahrirlashda o'zgarmaydi).</param>
/// <param name="ResponsibleEmployeeId">Mas'ul xodim (rekruter).</param>
/// <param name="Note">Izoh: shoshilinchlik, talablar.</param>
public sealed record SaveVacancyRequest(
    Guid PositionId,
    Guid LegalEntityId,
    Guid? DepartmentId = null,
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
/// Vakansiyaning ko'rinadigan nomi — uning LAVOZIMI: alohida sarlavha
/// maydoni ataylab yo'q (sababi <c>Vacancy</c> izohida).
/// </remarks>
public static class VacancyEndpoints
{
    public static IEndpointRouteBuilder MapVacancyEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/vacancies").WithTags("Recruiting");

        group.MapGet("/", async (
                string? status,
                Guid? positionId,
                Guid? departmentId,
                Guid? legalEntityId,
                int? page,
                int? pageSize,
                string? sort,
                ListVacanciesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListVacanciesQuery(
                            status, positionId, departmentId, legalEntityId, page, pageSize, sort),
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
                "«vakansiya.holat_notogri» 400). «sort» — openedAt (sukut, yangidan " +
                "eskiga) yoki position; teskari tartib «-openedAt» yoki «openedAt:desc». " +
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
                            request.PositionId,
                            request.LegalEntityId,
                            request.DepartmentId,
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
                "Lavozim va yuridik shaxs MAJBURIY va ular MAVJUD ma'lumotnomalardan " +
                "olinadi: ishga qabulda aynan shu identifikatorlar ishlatiladi. " +
                "Xatolar: «vakansiya.lavozim_topilmadi», «vakansiya.yuridik_shaxs_topilmadi», " +
                "«vakansiya.bolim_topilmadi», «vakansiya.masul_topilmadi» (404); " +
                "«vakansiya.orin_soni» (400).")
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
                            request.PositionId,
                            request.LegalEntityId,
                            request.DepartmentId,
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
                "Lavozim va firma ham o'zgaradi (vakansiya — reja, fakt emas). " +
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
