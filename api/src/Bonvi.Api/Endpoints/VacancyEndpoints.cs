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
/// sabab <c>Vacancy</c> domen izohida). Uchalasi nomzod ISHGA
/// OLINAYOTGANDA so'raladi:
/// <c>POST /api/recruiting/candidates/{id}/hire</c>.
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
                bool? excludeGeneralPool,
                ListVacanciesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListVacanciesQuery(
                            status,
                            vacancyTitleId,
                            page,
                            pageSize,
                            sort,
                            excludeGeneralPool ?? false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListVacancies")
            .WithSummary("Vakansiyalar — ro'yxat")
            .WithDescription(
                "SERVER tomonda sahifalanadi, filtrlanadi va saralanadi. " +
                "«status» — Open | Closed (bo'sh — hammasi, noma'lum qiymat " +
                "«vakansiya.holat_notogri» 400; «Paused» ENDI YO'Q). " +
                "«vacancyTitleId» — e'lon nomi bo'yicha " +
                "filtr; lavozim/bo'lim/firma filtrlari YO'Q, chunki vakansiyada bunday " +
                "maydonlar yo'q. «sort» — openedAt (sukut, yangidan eskiga) yoki title; " +
                "teskari tartib «-openedAt» yoki «openedAt:desc». " +
                "«pageSize» eng ko'pi 200. Har bir qatorda «candidateCount» va " +
                "«hiredCount» bor — «2/3 to'ldirildi» ko'rinishi uchun. " +
                "«excludeGeneralPool=true» — «Umumiy anketa» vakansiyasini ro'yxatdan " +
                "chiqarib tashlaydi; buni FAQAT bot so'raydi, chunki u nomzodga " +
                "tanlov ko'rsatadi. HR uchun bu oddiy vakansiya va sukut bo'yicha " +
                "ro'yxatda turadi. Sozlama bo'sh bo'lsa filtr hech narsa qilmaydi.")
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
                "HOLAT bu yerdan o'zgarmaydi: «/close» va «/reopen» bor. " +
                "«openedAt» bo'sh bo'lsa — eski sana saqlanadi.")
            .Produces<VacancyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        /*
          «/pause» O'CHIRILDI, «/resume» esa «/reopen» bo'ldi.

          Pauza va yopish AYNI natijani berardi — ikkalasida ham vakansiya
          yangi nomzod qabul qilmasdi. Farq faqat HR ning niyatida edi
          («vaqtincha» yoki «tugadi») va undan hech qanday xatti-harakat
          kelib chiqmasdi (batafsil sabab `VacancyStatus` domen izohida).
          «resume» nomi esa to'xtatishdan qolgan qoldiq edi: davom
          ettiriladigan narsa yo'q, faqat qayta ochiladigan.

          Eski manzillar ATAYLAB qoldirilmadi: ular 404 qaytaradi.
          Ishlaydigan, lekin boshqa narsa qiladigan endpoint («pause»
          aslida yopadi) mijozni jimgina aldardi.
        */
        group.MapPost("/{id:guid}/reopen", async (
                Guid id,
                ChangeVacancyStateCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ReopenVacancyCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ReopenVacancy")
            .WithSummary("Vakansiya — qayta ochish")
            .WithDescription(
                "Yopilgan vakansiya qayta ochiladi: olingan odam bir haftada ketib " +
                "qolishi odatiy hol va yangi vakansiya ochish nomzodlar tarixini " +
                "ikkiga bo'lib yuborardi. «closedAt» tozalanadi. Allaqachon ochiq " +
                "vakansiyada 409 QAYTMAYDI — amal hech narsa yozmaydi, ya'ni " +
                "takrorlanishi zararsiz (sababi `Vacancy.Reopen` izohida).")
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
                "Yopish QAYTARILADI («/reopen») va nomzodlarga hamda ularning " +
                "bosqichlariga TEGMAYDI — u faqat yangi nomzod kiritishni to'sadi. " +
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
                "FAQAT NOMZODI YO'Q vakansiya o'chiriladi: nomzodi bori — TARIX va " +
                "uni o'chirish nomzodlarning yozuvini yetim qoldirardi " +
                "(«vakansiya.nomzodlari_bor», 409; batafsil sabab " +
                "`DeleteVacancyCommandHandler` izohida). Ish tugagan vakansiya " +
                "YOPILADI, o'chirilmaydi. Yumshoq o'chirish: yozuv bazada qoladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
