using Bonvi.Api.Extensions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.Offers;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Ish taklifini yuborish so'rovi tanasi.</summary>
/// <param name="CandidateId">Nomzod. Vakansiya undan olinadi.</param>
/// <param name="SalaryAmount">Taklif qilinayotgan oylik ish haqi. Majburiy.</param>
/// <param name="SalaryCurrency">Valyuta kodi. Bo'sh — UZS.</param>
/// <param name="StartDate">Ish boshlash sanasi. Bo'sh — yuborilgan sana.</param>
/// <param name="SentAt">Yuborilgan sana. Bo'sh — bugun.</param>
/// <param name="ExpiresAt">Javob muddati. Bo'sh — muddatsiz.</param>
/// <param name="Note">Ichki izoh.</param>
public sealed record SendJobOfferRequest(
    Guid CandidateId,
    decimal SalaryAmount,
    string? SalaryCurrency = null,
    DateOnly? StartDate = null,
    DateOnly? SentAt = null,
    DateOnly? ExpiresAt = null,
    string? Note = null);

/// <summary>Taklif shartlarini tuzatish so'rovi tanasi.</summary>
public sealed record UpdateJobOfferRequest(
    decimal SalaryAmount,
    string? SalaryCurrency = null,
    DateOnly? StartDate = null,
    DateOnly? ExpiresAt = null,
    string? Note = null);

/// <summary>Taklifga javob (rad etish / muddati o'tdi) so'rovi tanasi.</summary>
public sealed record RespondJobOfferRequest(DateOnly? At = null, string? Note = null);

/// <summary>
/// Taklif qabul qilindi — nomzod xodimga aylanadi.
/// </summary>
/// <remarks>
/// Shaxsiy ma'lumot NOMZODDAN ko'chadi va bu yerda qayta so'ralmaydi.
/// JSHSHIR va pasport esa aynan shu yerda: nomzod hujjatlarini ishga qabul
/// kuni keltiradi.
/// </remarks>
/// <param name="RespondedAt">Javob sanasi. Bo'sh — bugun.</param>
/// <param name="ResponseNote">Javob izohi.</param>
/// <param name="HireDate">Ishga qabul sanasi. Bo'sh — taklifdagi ish boshlash sanasi.</param>
/// <param name="OrderNo">Ishga qabul buyrug'ining raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Pinfl">JSHSHIR (14 xona).</param>
/// <param name="Passport">Pasport seriyasi va raqami.</param>
/// <param name="EmployeeId">Mavjud xodim kartasi — QAYTA ishga olishda.</param>
/// <param name="Rate">Stavka: 1.0 — to'liq, 0.5 — o'rindoshlik.</param>
/// <param name="IsPrimary">Asosiy ish joyimi.</param>
/// <param name="ResponsibleEmployeeId">Biriktirilgan xodim (mentor).</param>
/// <param name="ProbationDays">Sinov muddati (kun). Bo'sh — lavozimdagi qiymat yoki 90.</param>
public sealed record AcceptJobOfferRequest(
    DateOnly? RespondedAt = null,
    string? ResponseNote = null,
    DateOnly? HireDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Pinfl = null,
    string? Passport = null,
    Guid? EmployeeId = null,
    decimal? Rate = null,
    bool? IsPrimary = null,
    Guid? ResponsibleEmployeeId = null,
    int? ProbationDays = null);

/// <summary>
/// Ish takliflari — voronkaning oxirgi rasmiy qadami (2-bosqich).
/// </summary>
public static class JobOfferEndpoints
{
    public static IEndpointRouteBuilder MapJobOfferEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/offers").WithTags("Recruiting");

        group.MapGet("/", async (
                Guid candidateId,
                ListCandidateOffersQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListCandidateOffersQuery(candidateId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListCandidateOffers")
            .WithSummary("Ish takliflari — nomzod bo'yicha")
            .WithDescription(
                "«candidateId» MAJBURIY: taklif doim aniq bir nomzodga tegishli va " +
                "«barcha takliflar» degan ro'yxat HR ga hech qanday savolga javob " +
                "bermaydi. Yangidan eskiga — muzokara tarixi shu tartibda o'qiladi. " +
                "Nomzod kartasida ham («GET /candidates/{id}» → «offers») shu ro'yxat bor.")
            .Produces<IReadOnlyList<JobOfferDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                SendJobOfferRequest request,
                SendJobOfferCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new SendJobOfferCommand(
                            request.CandidateId,
                            request.SalaryAmount,
                            request.SalaryCurrency,
                            request.StartDate,
                            request.SentAt,
                            request.ExpiresAt,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("SendJobOffer")
            .WithSummary("Ish taklifi — yuborish")
            .WithDescription(
                "Vakansiya so'ralmaydi — u NOMZODNIKI. Oklad MAJBURIY: summasiz " +
                "«taklif» og'zaki suhbat va u bosqich xulosasi bilan yoziladi. " +
                "Nomzodda javob kutayotgan taklif bo'lsa — «taklif.faol_mavjud» (409): " +
                "yangi shart taklif qilish uchun avval eskisiga javob yozing. " +
                "Boshqa xatolar: «nomzod.jarayonda_emas» (409), " +
                "«taklif.boshlash_sanasi» / «taklif.muddat_sanasi» / «pul.*» (400).")
            .Produces<JobOfferDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateJobOfferRequest request,
                ChangeJobOfferCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateJobOfferCommand(
                            id,
                            request.SalaryAmount,
                            request.SalaryCurrency,
                            request.StartDate,
                            request.ExpiresAt,
                            request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("UpdateJobOffer")
            .WithSummary("Ish taklifi — shartlarni tuzatish")
            .WithDescription(
                "Faqat javob kelmaguncha: javob berilgan taklif QULFLANADI " +
                "(«taklif.javob_berilgan», 409) — aks holda qabul qilingan taklif " +
                "okladi keyin o'zgartirilib, xodim kartasidagi summa bilan jimgina " +
                "farq qilib qolardi. Yangi shart — yangi taklif.")
            .Produces<JobOfferDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/decline", async (
                Guid id,
                RespondJobOfferRequest? request,
                ChangeJobOfferCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeclineJobOfferCommand(id, request?.At, request?.Note), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("DeclineJobOffer")
            .WithSummary("Ish taklifi — nomzod rad etdi")
            .WithDescription(
                "Nomzod voronkada QOLADI: rad javob muzokaraning oxiri emas va HR " +
                "yangi shartlar bilan ikkinchi taklif yuborishi mumkin. Nomzodni " +
                "butunlay yopish uchun «/candidates/{id}/withdraw» ishlatiladi.")
            .Produces<JobOfferDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/expire", async (
                Guid id,
                RespondJobOfferRequest? request,
                ChangeJobOfferCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ExpireJobOfferCommand(id, request?.At, request?.Note), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("ExpireJobOffer")
            .WithSummary("Ish taklifi — muddati o'tdi")
            .WithDescription(
                "Tizim buni O'ZI qilmaydi, hatto «expiresAt» o'tgan bo'lsa ham: " +
                "muddat o'tgan kuni nomzod qo'ng'iroq qilib rozi bo'lishi mumkin va " +
                "avtomatik yopilgan taklif buni qabul qila olmasdi.")
            .Produces<JobOfferDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/accept", async (
                Guid id,
                AcceptJobOfferRequest? request,
                AcceptJobOfferCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var body = request ?? new AcceptJobOfferRequest();

                var natija = await handler
                    .HandleAsync(
                        new AcceptJobOfferCommand(
                            id,
                            body.RespondedAt,
                            body.ResponseNote,
                            body.HireDate,
                            body.OrderNo,
                            body.OrderDate,
                            body.Pinfl,
                            body.Passport,
                            body.EmployeeId,
                            body.Rate,
                            body.IsPrimary,
                            body.ResponsibleEmployeeId,
                            body.ProbationDays),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("AcceptJobOffer")
            .WithSummary("Ish taklifi — qabul qilindi (nomzod XODIMGA aylanadi)")
            .WithDescription(
                "Voronkaning oxirgi qadami. BITTA tranzaksiyada: taklif «qabul qilindi» " +
                "bo'ladi, nomzod «ishga olingan» bo'lib voronkadan chiqadi, 1-bosqichning " +
                "ishga qabul oqimi ishga tushadi — xodim kartasi, mehnat munosabati, " +
                "ishga qabul buyrug'i va MAJBURIY sinov muddati. " +
                "Lavozim/bo'lim/firma VAKANSIYADAN, oklad va ish boshlash sanasi " +
                "TAKLIFDAN, F.I.Sh./telefon/tug'ilgan sana/manzil/mutaxassislik " +
                "NOMZODDAN ko'chadi — ularni qayta kiritish kerak emas. " +
                "«probationDays» 0 yoki manfiy bo'lsa «sinov.majburiy» (400): " +
                "sinovsiz qabul YO'Q. Nomzod allaqachon ishga olingan bo'lsa — " +
                "«nomzod.allaqachon_ishga_olingan» (409); taklifga javob berilgan " +
                "bo'lsa — «taklif.javob_berilgan» (409); qora ro'yxatdagi odam uchun " +
                "«xodim.qora_royxatda» (409). " +
                "Javob: «offer» + «candidate» + «hire» (ishga qabulning odatiy natijasi, " +
                "lavozim vazifalari va ogohlantirishlar bilan).")
            .Produces<CandidateHireResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
