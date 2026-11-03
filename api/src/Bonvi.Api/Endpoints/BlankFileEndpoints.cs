using Bonvi.Api.Extensions;
using Bonvi.Application.Blanks.Files;
using Bonvi.Application.Blanks.Pdf;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Application.Common.Documents;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bonvi.Api.Endpoints;

/// <summary>Blank attachments and PDF (SPEC-blankalar §7 #17–#20).</summary>
public static class BlankFileEndpoints
{
    /// <summary>
    /// Request cap: the largest field limit (<see cref="DocumentLimits.MaxBytes"/>, 20 MB) + 1 MB of
    /// multipart overhead (§7 «cap 21 MB»); the field's own <c>maxSizeMb</c> is checked in the handler.
    /// </summary>
    private const long MaxUploadRequestBytes = DocumentLimits.MaxBytes + (1024 * 1024);

    public static RouteGroupBuilder MapBlankFileEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Permission: «blanks.fill» OR an Open-step actor (§6.2), who may hold only «blanks.view» —
        // so the gate is «view» and the handler decides («blanka.ruxsat_yoq» 403), as in #9.
        group.MapPost("/submissions/{id:guid}/attachments", async (
                Guid id,
                IFormFile file,
                [FromForm] string? fieldPath,
                UploadBlankAttachmentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                return (await handler
                        .HandleAsync(
                            new UploadBlankAttachmentCommand(id, fieldPath, content, file.FileName, file.Length),
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToHttpResult();
            })
            .RequireAuthorization(Permissions.BlanksView)

            // CSRF protection is redundant here: the JWT travels in a header, not a session cookie
            // (same reasoning as EmployeeDocumentEndpoints).
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes))
            .WithName("UploadBlankAttachment")
            .WithSummary("Blankalar — faylni maydonga biriktirish")
            .WithDescription(
                "«multipart/form-data»: «fieldPath» («evidence» yoki «handover[2].doc» — jadval qatori avval saqlangan " +
                "bo'lishi kerak), «file». Tur MAZMUNIGA qarab aniqlanadi; maydonning «accept», «maxFiles», «maxSizeMb» " +
                "chegaralari. Fayl id'si maydon javobiga qo'shiladi va «version» oshadi — blankani qayta o'qing. " +
                "Xatolar: «blanka.fayl_turi», «fayl_eski_format», «fayl_makro», «fayl_katta», «fayl_soni», «fayl_bosh», " +
                "«blanka.notogri» (400); «blanka.maydon_tahrirlanmaydi», «blanka.holat_notogri» (409).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<BlankAttachmentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/submissions/{id:guid}/attachments/{attachmentId:guid}", async (
                Guid id,
                Guid attachmentId,
                [FromQuery] int expectedVersion,
                RemoveBlankAttachmentCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler
                    .HandleAsync(new RemoveBlankAttachmentCommand(id, attachmentId, expectedVersion), cancellationToken)
                    .ConfigureAwait(false))
                .ToHttpResult())
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("RemoveBlankAttachment")
            .WithSummary("Blankalar — faylni olib tashlash")
            .WithDescription(
                "Maydon hozir tahrirlansa: fayl belgilanadi, maydon javobidan chiqadi, diskdan o'chiriladi. " +
                "«expectedVersion» eskirgan — «blanka.ozgargan» 409.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/submissions/{id:guid}/attachments/{attachmentId:guid}", async (
                Guid id,
                Guid attachmentId,
                HttpContext context,
                GetBlankAttachmentFileQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, attachmentId, cancellationToken).ConfigureAwait(false);
                if (natija.IsFailure)
                {
                    return natija.ToHttpResult();
                }

                var file = natija.Value;
                SetFileHeaders(context, file.FileName, file.Inline);
                return Results.Stream(file.Content, file.ContentType);
            })
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("DownloadBlankAttachment")
            .WithSummary("Blankalar — faylni yuklab olish")
            .WithDescription(
                "PDF va rasm — «inline», Word/Excel/PowerPoint — «attachment»; «X-Content-Type-Options: nosniff». " +
                "Topilmasa: «blanka.fayl_topilmadi» (404).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/submissions/{id:guid}/pdf", async (
                Guid id,
                HttpContext context,
                GetBlankPdfQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false);
                if (natija.IsFailure)
                {
                    return natija.ToHttpResult();
                }

                SetFileHeaders(context, natija.Value.FileName, inline: true);
                return Results.Bytes(natija.Value.Content, "application/pdf");
            })
            .RequireAuthorization(Permissions.BlanksView)
            .WithName("GetBlankPdf")
            .WithSummary("Blankalar — PDF")
            .WithDescription("Har qanday holatda joyida chizilgan PDF (§10).")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>Content-Disposition with an RFC 5987 UTF-8 name (Cyrillic file names) + nosniff.</summary>
    private static void SetFileHeaders(HttpContext context, string fileName, bool inline)
    {
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(fileName);
        context.Response.Headers.ContentDisposition = disposition.ToString();
        context.Response.Headers.XContentTypeOptions = "nosniff";
    }
}
