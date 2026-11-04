using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Api.Extensions;
using Bonvi.Application.Blanks.Steps;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

public sealed record ApproveBlankStepBody(
    int ExpectedVersion,
    string? Decision,
    JsonElement? DecisionExtra,
    string? Comment,
    JsonElement? Answers);

public sealed record BlankStepCommentBody(int ExpectedVersion, string? Comment);

/// <summary>Blanka qadamlari (SPEC-blankalar §7 #12–#16).</summary>
/// <remarks>
/// #12 va #14 da endpoint darajasida faqat «blanks.view»: o'z shaxs qadamini (xodimning o'zi, boshliq) imzolash
/// «blanks.approve» talab qilmaydi (§6.2) — qolganini handler qadam bo'yicha tekshiradi («blanka.qadam_ruxsat_yoq» 403).
/// </remarks>
public static class BlankStepEndpoints
{
    /// <summary>#13 so'rov chegarasi: imzo (≤ 300 KB) + payload — 1 MB (§7).</summary>
    private const long MaxInPersonRequestBytes = 1024 * 1024;

    private const string StepRoute = "/submissions/{id:guid}/steps/{stepCode}";

    public static RouteGroupBuilder MapBlankStepEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost(StepRoute + "/approve", async (
                Guid id,
                string stepCode,
                ApproveBlankStepBody body,
                ApproveBlankStepCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler
                    .HandleAsync(
                        new ApproveBlankStepCommand(
                            id,
                            stepCode,
                            new BlankStepActInput(
                                body.ExpectedVersion,
                                body.Decision,
                                Node(body.DecisionExtra),
                                body.Comment,
                                Node(body.Answers))),
                        cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("ApproveBlankStep")
            .WithSummary("Blankalar — qadamni elektron tasdiqlash")
            .WithDescription("Lavozim roli: «blanks.approve» (+ rolga biriktirilgan bo'lsa shu foydalanuvchi). Shaxs roli: "
                + "foydalanuvchining xodimi = qadam xodimi. «answers» — qadamning o'z bo'limlari (to'liq almashadi), "
                + "majburiylari tekshiriladi. Oxirgi guruh imzolansa blanka Approved.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost(StepRoute + "/sign-in-person", async (
                Guid id,
                string stepCode,
                [FromForm] string? payload,
                IFormFile? signature,
                SignBlankStepInPersonCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var signatureStream = signature?.OpenReadStream();

                return (await handler
                    .HandleAsync(
                        new SignBlankStepInPersonCommand(id, stepCode, payload, signatureStream, signature?.Length ?? 0),
                        cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult();
            })
            .RequireAuthorization(Permissions.BlanksFill)

            // Token Authorization sarlavhasida — cookie yo'q, CSRF himoyasi ortiqcha (fayl endpointlari kabi)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxInPersonRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxInPersonRequestBytes)
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("SignBlankStepInPerson")
            .WithSummary("Blankalar — qadamni HR qurilmasida qo'lda imzolash")
            .WithDescription("multipart: «payload» (JSON: expectedVersion, decision?, decisionExtra?, comment?, answers?, "
                + "signerName?) + «signature» (PNG ≤ 300 KB; oq fonda ≤ 1200×600 qayta chiziladi). Guvohda signerName majburiy.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost(StepRoute + "/return", async (
                Guid id,
                string stepCode,
                BlankStepCommentBody body,
                ReturnBlankStepCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler
                    .HandleAsync(new ReturnBlankStepCommand(id, stepCode, body.ExpectedVersion, body.Comment), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("ReturnBlankStep")
            .WithSummary("Blankalar — qoralamaga qaytarish")
            .WithDescription("Qadamda «canReturn» bo'lishi kerak, izoh majburiy. Blanka Draft ga qaytadi, raqam saqlanadi, "
                + "qadamlar keyingi yuborishda qaytadan tuziladi.")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost(StepRoute + "/reject", async (
                Guid id,
                string stepCode,
                BlankStepCommentBody body,
                RejectBlankStepCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler
                    .HandleAsync(new RejectBlankStepCommand(id, stepCode, body.ExpectedVersion, body.Comment), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.BlanksApprove)
            .WithName("RejectBlankStep")
            .WithSummary("Blankalar — rad etish")
            .WithDescription("Qadamda «canReject» bo'lishi kerak, izoh majburiy. Blanka Rejected (yakuniy).")
            .Produces<BlankSubmissionDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet(StepRoute + "/signature", async (
                Guid id,
                string stepCode,
                GetBlankStepSignatureQueryHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(id, stepCode, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    return result.ToHttpResult();
                }

                http.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.Stream(result.Value.Content, "image/png");
            })
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("GetBlankStepSignature")
            .WithSummary("Blankalar — qadamning qo'lda chizilgan imzosi")
            .WithDescription("Faqat qo'lda imzolangan qadamda bor; aks holda «blanka.fayl_topilmadi» 404.")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static JsonNode? Node(JsonElement? element)
        => element is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } value
            ? JsonNode.Parse(value.GetRawText())
            : null;
}
