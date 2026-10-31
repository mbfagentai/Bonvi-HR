using System.Security.Claims;
using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Me;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.PublicApi.Team;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.VacancyRequests;

namespace Bonvi.Api.Endpoints;

/// <summary>Rahbar botidan vakansiya so'rovi tanasi (egasi, 2026-10-31).</summary>
/// <param name="DepartmentId">Kalit qamrovidagi bo'lim; bo'sh — kalitning bo'limi.</param>
/// <param name="Title">Lavozim.</param>
/// <param name="Openings">Nechta odam.</param>
/// <param name="NeededBy">Qachondan kerak.</param>
/// <param name="Note">Sabab va talablar.</param>
public sealed record PublicTeamVacancyRequestBody(
    Guid? DepartmentId,
    string? Title,
    int Openings = 1,
    DateOnly? NeededBy = null,
    string? Note = null);

/// <summary>
/// ⭐ Bo'lim davomati API'si — <c>/api/public/v1/team</c>, bo'lim rahbarining boti uchun
/// (egasi, 2026-10-30).
/// </summary>
/// <remarks>
/// <para>
/// Faqat <c>DepartmentAttendance</c> doirasidagi kalit kiradi (<see cref="Policy"/>). Guruh
/// ommaviy yuzadan ALOHIDA: u yerning siyosati Basic/FullRead ni talab qiladi va endpoint
/// darajasidagi siyosat uni bo'shata olmaydi (siyosatlar qo'shiladi). Teskarisi ham o'z-o'zidan:
/// bo'lim kaliti <c>/employees</c> va <c>/full</c> da 403 oladi.
/// </para>
/// <para>
/// Javoblar xodim ilovasidagi «Jamoa» tabi bilan AYNAN bir xil shaklda (o'sha DTO'lar) — bitta
/// hujjat ikkala iste'molchiga yetadi. Qamrov — kalitning bo'limi va uning ichidagilari, har
/// so'rovda bazadan.
/// </para>
/// </remarks>
public static class PublicTeamEndpoints
{
    /// <summary>Bo'lim kaliti siyosati.</summary>
    public const string Policy = "tashqi-bolim-davomati";

    public static IEndpointRouteBuilder MapPublicTeamEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(PublicApiEndpoints.Prefix + "/team")
            .WithTags("Public")
            .WithGroupName(PublicApiEndpoints.DocumentName)
            .RequireAuthorization(Policy)
            .RequireRateLimiting(RateLimitPolicies.PublicApi);

        group.MapGet("", async (
                ClaimsPrincipal user,
                GetPublicTeamQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(KeyOf(user), cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .WithName("PublicGetTeam")
            .WithSummary("Bo'lim kaliti — qamrov")
            .WithDescription(
                "Kalit ko'radigan bo'limlar: kalitning bo'limi va uning barcha ichki bo'limlari, daraxt tartibida " +
                "({ departments: [{ id, name, path, level, parentId }] }). Ulanishni tekshirishning eng arzon yo'li. " +
                "Xato: «tashqi.bolim_topilmadi» (404 — bo'lim o'chirilgan).")
            .Produces<TeamScopeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/board", async (
                DateOnly? date,
                Guid? departmentId,
                AttendanceDashboardStatus? status,
                string? search,
                int? page,
                int? pageSize,
                ClaimsPrincipal user,
                GetPublicTeamBoardQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        KeyOf(user),
                        new GetPublicTeamBoardQuery(date, departmentId, status, search, page, pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicGetTeamBoard")
            .WithSummary("Bo'lim kaliti — kunlik holat")
            .WithDescription(
                "Kim keldi, qachon keldi/ketdi, kim kechikdi yoki kelmadi — telefoni bilan. HR davomat panelining " +
                "AYNAN o'sha hisobi. «date» — Toshkent sanasi (sukut: bugun); «departmentId» — qamrovdagi bo'lim " +
                "(ichki bo'limlari bilan); «status» — Arrived | OnTime | Late | Absent | NoSchedule | DayOff | " +
                "OnLeave | Excused | Left | …; «search» — ism yoki telefon; «page», «pageSize» (sukut 50, eng ko'pi 200). " +
                "«counts» — holatlar bo'yicha sanoq. Telefon va «hasPhoto» — faqat xodim BUGUN qamrovda bo'lsa. " +
                "Xatolar: «jamoa.sana_notogri» (400), «tashqi.bolim_topilmadi» (404).")
            .Produces<TeamBoardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/passes", async (
                string? after,
                ClaimsPrincipal user,
                GetPublicTeamPassesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(KeyOf(user), after, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .WithName("PublicGetTeamPasses")
            .WithSummary("Bo'lim kaliti — yangi o'tishlar")
            .WithDescription(
                "Bot uchun oqim: «after» kursoridan keyin serverga kelgan, qamrovdagi xodimlarning o'tishlari, " +
                "kelish tartibida ({ items: [{ id, employeeId, fullName, positionName, departmentName, at, date, " +
                "direction: In|Out, source: Terminal|Mobile }], next, hasMore }). Birinchi so'rov «after»siz — bugun " +
                "(Toshkent) boshidan. Javobdagi «next» ni saqlab, keyingi so'rovda «after» qilib bering; «hasMore» " +
                "true bo'lsa darhol yana so'rang. Kursor KELISH vaqti bo'yicha: agent uzilib qolib keyin yuborgan " +
                "o'tishlar ham keladi («at» eski bo'lishi mumkin). «direction» — hisob-kitobdagi yo'nalish " +
                "(yo'nalishsiz terminalda navbat bilan; 15 soniya ichidagi takror o'qish birinchisining yo'nalishida). " +
                "«id» bo'yicha takrorni ushlang. Xato: «tashqi.kursor_notogri» (400).")
            .Produces<PublicTeamPassesDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/members/{employeeId:guid}/attendance", async (
                Guid employeeId,
                int year,
                int month,
                ClaimsPrincipal user,
                GetPublicTeamMemberAttendanceQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(KeyOf(user), new GetTeamMemberAttendanceQuery(employeeId, year, month), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicGetTeamMemberAttendance")
            .WithSummary("Bo'lim kaliti — xodimning oyi")
            .WithDescription(
                "Xodim (F.I.Sh., lavozim, bo'lim, telefon) va oylik tabel: har kun — holat, kelgan/ketgan vaqt, " +
                "ishlangan, kechikish, erta ketish va o'tishlar ro'yxati; «totals» — oy jami. Faqat bo'limi O'SHA " +
                "KUNI qamrovda bo'lgan kunlar; qolganlari bo'sh («NotEmployed»). Xatolar: «davomat.oy_notogri» (400), " +
                "«tashqi.xodim_topilmadi» (404 — qamrovda emas yoki umuman yo'q).")
            .Produces<TeamMemberAttendanceDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/members/{employeeId:guid}/photo", async (
                Guid employeeId,
                HttpContext context,
                ClaimsPrincipal user,
                GetPublicTeamMemberPhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(KeyOf(user), employeeId, PhotoHttp.ReadKnownVersion(context.Request), cancellationToken)
                    .ConfigureAwait(false);

                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .WithName("PublicGetTeamMemberPhoto")
            .WithSummary("Bo'lim kaliti — xodimning rasmi")
            .WithDescription(
                "Rasm baytlari (image/jpeg), xodim BUGUN qamrovda bo'lishi kerak. «ETag» + «If-None-Match» → 304. " +
                "Xatolar: «tashqi.xodim_topilmadi» (404), «xodim.rasm_yoq» (404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/vacancy-requests", async (
                string? status,
                int? page,
                int? pageSize,
                ClaimsPrincipal user,
                ListPublicTeamVacancyRequestsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(KeyOf(user), new ListTeamVacancyRequestsQuery(status, page, pageSize), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicGetTeamVacancyRequests")
            .WithSummary("Bo'lim kaliti — vakansiya so'rovlari")
            .WithDescription(
                "Qamrovdagi bo'limlarning vakansiya so'rovlari — yangidan eskiga ({ items, page, pageSize, total, " +
                "pageCount }). «status» — Pending | Approved | Rejected | Cancelled (sukut: hammasi); «page», " +
                "«pageSize» (sukut 50, eng ko'pi 200). Rad etilganida «decisionNote» — HR sababi; tasdiqlanganida " +
                "«vacancyTitle», «candidateCount», «hiredCount». Xatolar: «vakansiya_sorovi.holat_notogri» (400), " +
                "«tashqi.bolim_topilmadi» (404).")
            .Produces<PagedResult<VacancyRequestDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/vacancy-requests", async (
                PublicTeamVacancyRequestBody request,
                ClaimsPrincipal user,
                SubmitPublicTeamVacancyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        KeyOf(user),
                        new SubmitTeamVacancyRequestCommand(
                            request.DepartmentId,
                            request.Title,
                            request.Openings,
                            request.NeededBy,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .WithName("PublicCreateTeamVacancyRequest")
            .WithSummary("Bo'lim kaliti — vakansiya so'rash")
            .WithDescription(
                "So'rov HR «Vakansiya so'rovlari» menyusiga «Yangi» holatida tushadi. Tana: { departmentId?, title, " +
                "openings (1–100, sukut 1), neededBy? (YYYY-MM-DD), note? }. «departmentId» bo'sh — kalitning bo'limi. " +
                "So'rovchi — bo'limning boshlig'i (bo'lmasa eng yaqin ustki bo'limniki). Xatolar: " +
                "«vakansiya_sorovi.nom_kerak», «vakansiya_sorovi.nom_uzun», «vakansiya_sorovi.soni_notogri», " +
                "«vakansiya_sorovi.izoh_uzun», «tashqi.boshliq_yoq» (400), «tashqi.bolim_topilmadi» (404).")
            .Produces<VacancyRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/vacancy-requests/{id:guid}/cancel", async (
                Guid id,
                ClaimsPrincipal user,
                CancelPublicTeamVacancyRequestCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(KeyOf(user), id, cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .WithName("PublicCancelTeamVacancyRequest")
            .WithSummary("Bo'lim kaliti — vakansiya so'rovini qaytarib olish")
            .WithDescription(
                "Faqat qaror qilinmagan so'rov. Xatolar: «vakansiya_sorovi.topilmadi» (404 — qamrovda emas yoki yo'q), " +
                "«vakansiya_sorovi.qaror_qilingan» (409).")
            .Produces<VacancyRequestDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// Kalit va uning bo'limi — claim'lardan. Siyosat ikkalasini ham talab qiladi, ya'ni bu yerga
    /// yetib kelgan so'rovda ular bor.
    /// </summary>
    private static PublicTeamKey KeyOf(ClaimsPrincipal user)
        => new(
            Guid.Parse(user.FindFirstValue(ApiKeyClaims.KeyId)!),
            Guid.Parse(user.FindFirstValue(ApiKeyClaims.DepartmentId)!));
}
