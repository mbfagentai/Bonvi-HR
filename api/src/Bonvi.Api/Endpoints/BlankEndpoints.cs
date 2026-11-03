using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Api.Extensions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Shablonni yoqish/o'chirish tanasi.</summary>
/// <param name="IsActive">Yangi blanka ochilishi mumkinmi.</param>
public sealed record SetBlankTemplateActiveBody(bool IsActive);

/// <summary>Rolga biriktirilganlar tanasi.</summary>
/// <param name="UserIds">Foydalanuvchilar (≤ 20); bo'sh — har qanday «blanks.approve» egasi.</param>
public sealed record SetBlankRoleUsersBody(IReadOnlyList<Guid>? UserIds);

/// <summary>Yangi blanka tanasi.</summary>
/// <param name="TemplateCode">Shablon kodi (<c>HR-F06</c>).</param>
/// <param name="EmployeeId">Sub'yekt xodim.</param>
/// <param name="CandidateId">Sub'yekt nomzod.</param>
/// <param name="ClientRequestId">Takroriy yaratishdan himoya.</param>
public sealed record CreateBlankSubmissionBody(string? TemplateCode, Guid? EmployeeId, Guid? CandidateId, Guid? ClientRequestId);

/// <summary>Javoblarni saqlash tanasi.</summary>
/// <param name="ExpectedVersion">Mijoz ko'rgan <c>version</c>.</param>
/// <param name="Answers">Javoblar obyekti — tahrirlanadigan bo'limlar to'liq.</param>
public sealed record SaveBlankAnswersBody(int ExpectedVersion, JsonElement? Answers);

/// <summary>Yuborish tanasi.</summary>
/// <param name="ExpectedVersion">Mijoz ko'rgan <c>version</c>.</param>
public sealed record SubmitBlankSubmissionBody(int ExpectedVersion);

/// <summary>Bekor qilish tanasi.</summary>
/// <param name="ExpectedVersion">Mijoz ko'rgan <c>version</c>.</param>
/// <param name="Reason">Sabab — majburiy.</param>
public sealed record CancelBlankSubmissionBody(int ExpectedVersion, string? Reason);

/// <summary>
/// «Blankalar» — HR qog'oz blankalari onlayn (docs/SPEC-blankalar.md §7). Bu fayl — yig'uvchi:
/// shablonlar, rollar va blankalar (#1–#11) shu yerda; qadamlar (#12–#16) <see cref="BlankStepEndpoints"/>,
/// fayllar va PDF (#17–#20) <see cref="BlankFileEndpoints"/> da.
/// </summary>
public static class BlankEndpoints
{
    public static IEndpointRouteBuilder MapBlankEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/blanks").WithTags("Blanks").RequireAuthorization();

        MapTemplates(group);
        MapRoles(group);
        MapSubmissions(group);

        group.MapBlankStepEndpoints();
        group.MapBlankFileEndpoints();

        return app;
    }

    private static void MapTemplates(RouteGroupBuilder group)
    {
        group.MapGet("/templates", async (ListBlankTemplatesQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("ListBlankTemplates")
            .WithSummary("Blankalar — shablonlar")
            .WithDescription("Har shablon joriy e'lon qilingan versiyasi bilan (yo'q bo'lsa «version» bo'sh). Tartib: sortOrder, kod.")
            .Produces<IReadOnlyList<BlankTemplateDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/template-versions/{id:guid}", async (
                Guid id,
                GetBlankTemplateVersionQueryHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("GetBlankTemplateVersion")
            .WithSummary("Blankalar — shablon versiyasi (ta'rif bilan)")
            .WithDescription("Eski (Retired) versiya ham o'qiladi — eski blankalar o'z versiyasi bilan ko'rsatiladi.")
            .Produces<BlankTemplateVersionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/templates/{id:guid}", async (
                Guid id,
                SetBlankTemplateActiveBody body,
                SetBlankTemplateActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body.IsActive, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksManage)
            .WithName("SetBlankTemplateActive")
            .WithSummary("Blankalar — shablonni yoqish / o'chirish")
            .WithDescription("O'chirilgan shablonda yangi blanka ochilmaydi («blanka.shablon_faol_emas» 409), mavjudlari ishlayveradi.")
            .Produces<BlankTemplateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapRoles(RouteGroupBuilder group)
    {
        group.MapGet("/roles", async (ListBlankRolesQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("ListBlankRoles")
            .WithSummary("Blankalar — ofis rollari va biriktirilganlar")
            .WithDescription("Hamma ofis rollari (hr1 … lawyer). Bo'sh ro'yxat — rol qadamini har qanday «blanks.approve» egasi tasdiqlaydi.")
            .Produces<IReadOnlyList<BlankRoleDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/roles/{roleCode}", async (
                string roleCode,
                SetBlankRoleUsersBody body,
                SetBlankRoleUsersCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SetBlankRoleUsersCommand(roleCode, body.UserIds), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksManage)
            .WithName("SetBlankRoleUsers")
            .WithSummary("Blankalar — rolga biriktirilganlarni almashtirish")
            .WithDescription("To'plam to'liq almashadi (≤ 20). Noma'lum rol — «blanka.rol_notogri» 400; yo'q foydalanuvchi — «blanka.foydalanuvchi_topilmadi» 404.")
            .Produces<BlankRoleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapSubmissions(RouteGroupBuilder group)
    {
        group.MapGet("/submissions", async (
                string? templateCode,
                string? status,
                Guid? employeeId,
                Guid? candidateId,
                Guid? departmentId,
                DateOnly? from,
                DateOnly? to,
                string? search,
                bool? waitingForMe,
                bool? incomplete,
                bool? overdue,
                int? page,
                int? pageSize,
                ListBlankSubmissionsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListBlankSubmissionsQuery(
                            templateCode,
                            status,
                            employeeId,
                            candidateId,
                            departmentId,
                            from,
                            to,
                            search,
                            waitingForMe ?? false,
                            incomplete ?? false,
                            overdue ?? false,
                            page,
                            pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("ListBlankSubmissions")
            .WithSummary("Blankalar — ro'yxat")
            .WithDescription(
                "Yangidan eskiga. «status» — Draft | InApproval | Approved | Rejected | Cancelled. «departmentId» — " +
                "quyi bo'limlar bilan. «from»/«to» — yaratilgan sana (Toshkent kuni, ikkalasi ham kiradi). «search» — " +
                "raqam yoki sub'yekt ismi. «waitingForMe» — chaqiruvchi elektron bajara oladigan ochiq qadamlar; " +
                "«incomplete» — to'ldirilmagan majburiy maydon bor; «overdue» — muddati o'tgan va yakunlanmagan. " +
                "«pageSize» sukut 50, eng ko'pi 10 000.")
            .Produces<PagedResult<BlankSubmissionListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/submissions", async (
                CreateBlankSubmissionBody body,
                CreateBlankSubmissionCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new CreateBlankSubmissionCommand(body.TemplateCode, body.EmployeeId, body.CandidateId, body.ClientRequestId),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (natija.IsFailure || !natija.Value.Created)
                {
                    return natija.IsFailure ? natija.ToHttpResult() : Results.Ok(natija.Value.Submission);
                }

                return Results.Created($"/api/blanks/submissions/{natija.Value.Submission.Id}", natija.Value.Submission);
            })
            .RequireAuthorization(Permissions.BlanksFill)
            .WithName("CreateBlankSubmission")
            .WithSummary("Blankalar — yangi blanka (qoralama)")
            .WithDescription(
                "Javoblar tizim ma'lumotidan oldindan to'ldiriladi (§4.6). Bir xil «clientRequestId» bilan takroriy " +
                "so'rov — o'sha blanka, 200. Sub'yekt shablon turiga mos kelmasa — «blanka.subyekt_notogri» 400.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status201Created)
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/submissions/{id:guid}", async (
                Guid id,
                GetBlankSubmissionQueryHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("GetBlankSubmission")
            .WithSummary("Blankalar — blanka")
            .WithDescription("Javoblar, computed, «missing», qadamlar, fayllar va «can» — chaqiruvchi hozir nima qila oladi.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Ruxsat: «blanks.fill» YOKI ochiq qadam ijrochisi (§6.2) — ikkinchisi faqat «blanks.view» bilan
        // ham bo'lishi mumkin, shuning uchun eshikda «view», qolgani handlerda («blanka.ruxsat_yoq» 403).
        group.MapPut("/submissions/{id:guid}/answers", async (
                Guid id,
                SaveBlankAnswersBody body,
                SaveBlankAnswersCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                JsonNode? answers = body.Answers is { ValueKind: not JsonValueKind.Null } element
                    ? JsonNode.Parse(element.GetRawText())
                    : null;

                return (await handler
                        .HandleAsync(new SaveBlankAnswersCommand(id, body.ExpectedVersion, answers), cancellationToken)
                        .ConfigureAwait(false))
                    .ToHttpResult();
            })
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("SaveBlankAnswers")
            .WithSummary("Blankalar — javoblarni saqlash")
            .WithDescription(
                "Hozir tahrirlanadigan bo'limlar to'liq almashadi, boshqalari e'tiborsiz (o'zgargan bo'lsa — " +
                "«blanka.maydon_tahrirlanmaydi» 409). Faqat tur va chegara tekshiruvi; majburiylik — yuborishda. " +
                "«expectedVersion» eskirgan — «blanka.ozgargan» 409.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/submissions/{id:guid}/submit", async (
                Guid id,
                SubmitBlankSubmissionBody body,
                SubmitBlankSubmissionCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SubmitBlankSubmissionCommand(id, body.ExpectedVersion), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksFill)
            .WithName("SubmitBlankSubmission")
            .WithSummary("Blankalar — yuborish")
            .WithDescription(
                "Qoralama bo'limlarining majburiy maydonlari tekshiriladi («blanka.majburiy» 400, maydon yo'li bilan), " +
                "birinchi yuborishda raqam beriladi, qadamlar ta'rifdan yaratiladi. Qadam yo'q — darrov Approved.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/submissions/{id:guid}/cancel", async (
                Guid id,
                CancelBlankSubmissionBody body,
                CancelBlankSubmissionCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new CancelBlankSubmissionCommand(id, body.ExpectedVersion, body.Reason), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksFill)
            .WithName("CancelBlankSubmission")
            .WithSummary("Blankalar — bekor qilish")
            .WithDescription(
                "Sabab majburiy. O'z qoralamasi — «blanks.fill»; boshqaning qoralamasi va kelishuvdagisi — " +
                "«blanks.manage» (aks holda «blanka.ruxsat_yoq» 403).")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
