using Bonvi.Api.Extensions;
using Bonvi.Application.Feedback;
using Bonvi.Application.Feedback.Dtos;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Qayta aloqa yozish so'rovi tanasi.</summary>
/// <param name="EmploymentId">Ishdan bo'shatilgan mehnat munosabati.</param>
/// <param name="InterviewDate">Suhbat o'tkazilgan sana.</param>
/// <param name="Reason">Ishdan ketish sababi — xodimning o'z so'zi bilan. Majburiy.</param>
/// <param name="WouldRecommend">Kompaniyani tavsiya qilarmidi: Yes | No | DontKnow.</param>
/// <param name="Impressions">Kompaniya haqida umumiy taassurot.</param>
/// <param name="ConditionsToReturn">Qanday shartda qayta ishlashga rozi bo'lardi.</param>
public sealed record RecordExitInterviewRequest(
    Guid EmploymentId,
    DateOnly InterviewDate,
    string? Reason,
    RecommendationAnswer? WouldRecommend = null,
    string? Impressions = null,
    string? ConditionsToReturn = null);

/// <summary>Qayta aloqa yozuvini tuzatish so'rovi tanasi — <c>EmploymentId</c> o'zgarmaydi.</summary>
public sealed record UpdateExitInterviewRequest(
    DateOnly InterviewDate,
    string? Reason,
    RecommendationAnswer? WouldRecommend = null,
    string? Impressions = null,
    string? ConditionsToReturn = null);

/// <summary>
/// Qayta aloqa (exit interview) endpoint'lari — M10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Faqat ICHKI FORMA.</b> Rejadagi «ommaviy havola» (Google Form o'rniga
/// xodim o'zi to'ldiradigan tashqi sahifa) ATAYLAB QURILMAGAN: autentifikatsiyasiz
/// endpoint alohida xavfsizlik yuzasi (havola tokeni, muddati, bekor qilish,
/// tezlik cheklovi, spam himoyasi) va uni shu ichki forma ishlab, sinovdan
/// o'tgandan keyin alohida, ongli ravishda qo'shish kerak.
/// </para>
/// <para>
/// Ruxsatlar ALOHIDA guruh (<see cref="Permissions.FeedbackView"/> /
/// <see cref="Permissions.FeedbackManage"/>), <c>employment.*</c> emas —
/// sabab <see cref="Permissions.FeedbackView"/> izohida.
/// </para>
/// </remarks>
public static class ExitInterviewEndpoints
{
    public static IEndpointRouteBuilder MapExitInterviewEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/exit-interviews").WithTags("ExitInterviews");

        group.MapGet("/", async (
                DateOnly? from,
                DateOnly? to,
                string? search,
                ListExitInterviewsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListExitInterviewsQuery(from, to, search), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.FeedbackView)
            .WithName("ListExitInterviews")
            .WithSummary("Qayta aloqa — jurnal (91.16 o'rniga)")
            .WithDescription(
                "Barcha suhbatlar, suhbat sanasi bo'yicha kamayish tartibida. " +
                "«from»/«to» — ikki tomondan yopiq oraliq. " +
                "«search» — XODIM bo'yicha (F.I.Sh., JSHSHIR, pasport, telefon), xodimlar " +
                "ro'yxatidagi bilan aynan bir xil qoida; suhbat MATNI qidiruvga kirmaydi.")
            .Produces<IReadOnlyList<ExitInterviewDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/by-employment/{employmentId:guid}", async (
                Guid employmentId,
                GetExitInterviewByEmploymentQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetExitInterviewByEmploymentQuery(employmentId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.FeedbackView)
            .WithName("GetExitInterviewByEmployment")
            .WithSummary("Qayta aloqa — bitta mehnat munosabati bo'yicha")
            .WithDescription(
                "Xodim kartasidagi mehnat tarixi tabida ko'rsatish uchun. Yozuv yo'q bo'lsa — 404 " +
                "(ko'p munosabatda suhbat umuman bo'lmasligi mumkin — bu kutilgan holat, frontend " +
                "buni xatosiz ko'rsatishi kerak).")
            .Produces<ExitInterviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                RecordExitInterviewRequest request,
                RecordExitInterviewCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RecordExitInterviewCommand(
                            request.EmploymentId,
                            request.InterviewDate,
                            request.Reason,
                            request.WouldRecommend,
                            request.Impressions,
                            request.ConditionsToReturn),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.FeedbackManage)
            .WithName("RecordExitInterview")
            .WithSummary("Qayta aloqa — suhbatni yozish")
            .WithDescription(
                "Faqat YAKUNLANGAN (Terminated) mehnat munosabati uchun: " +
                "«qayta_aloqa.mehnat_tugamagan» (409). Bitta munosabatga bittadan ortiq " +
                "yozuv bo'lmaydi: «qayta_aloqa.allaqachon_bor» (409). Sababsiz yaratilmaydi: " +
                "«qayta_aloqa.sabab_bosh» (400).")
            .Produces<ExitInterviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateExitInterviewRequest request,
                UpdateExitInterviewCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateExitInterviewCommand(
                            id,
                            request.InterviewDate,
                            request.Reason,
                            request.WouldRecommend,
                            request.Impressions,
                            request.ConditionsToReturn),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.FeedbackManage)
            .WithName("UpdateExitInterview")
            .WithSummary("Qayta aloqa — kiritish xatosini tuzatish")
            .WithDescription(
                "Mehnat munosabati (EmploymentId) o'zgartirilmaydi — noto'g'ri munosabatga " +
                "yozilgan bo'lsa, yozuv o'chirilib yangisi kiritiladi.")
            .Produces<ExitInterviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteExitInterviewCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteExitInterviewCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.FeedbackManage)
            .WithName("DeleteExitInterview")
            .WithSummary("Qayta aloqa — yozuvni o'chirish")
            .WithDescription("Yumshoq o'chirish: yozuv bazada qoladi, faqat ro'yxatlarda ko'rinmay qoladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
