using Bonvi.Api.Extensions;
using Bonvi.Application.Common.Documents;
using Bonvi.Application.Employees.Documents;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>Hujjat yozuvini tuzatish so'rovi tanasi — fayl va xodim O'ZGARMAYDI.</summary>
public sealed record UpdateEmployeeDocumentRequest(Guid DocumentTypeId, string? Note);

/// <summary>
/// Xodim hujjatlari endpoint'lari (S8/M9).
/// </summary>
/// <remarks>
/// <para>
/// Ruxsatlar rasm bilan bir xil: <see cref="Permissions.EmployeesView"/> /
/// <see cref="Permissions.EmployeesEdit"/>, ALOHIDA ruxsat guruhi EMAS —
/// <see cref="IncentiveEndpoints"/>dan farqli. Sabab: hujjat kartaning bir
/// qismi (xodim rasmiga aynan o'xshaydi — fayl biriktirilgan-biriktirilmagani),
/// pul yoki intizomiy chora kabi alohida nozik toifa emas. Ikkita rolning
/// (5.1-bo'lim) barchasida ham bu ikki ruxsat birga keladi, ya'ni amalda
/// farq yo'q — yangi ruxsat qo'shish faqat keraksiz murakkablik bo'lardi.
/// </para>
/// </remarks>
public static class EmployeeDocumentEndpoints
{
    /// <summary>
    /// So'rov chegarasi fayl chegarasidan (<see cref="DocumentLimits.MaxBytes"/>)
    /// sal katta: multipart konvertida forma maydonlari (tur, izoh) ham joy egallaydi.
    /// </summary>
    private const long MaxDocumentRequestBytes = DocumentLimits.MaxBytes + (64 * 1024);

    public static IEndpointRouteBuilder MapEmployeeDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/employees/{employeeId:guid}/documents").WithTags("EmployeeDocuments");

        group.MapGet("/", async (
                Guid employeeId,
                Guid? documentTypeId,
                GetEmployeeDocumentsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeDocumentsQuery(employeeId, documentTypeId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetEmployeeDocuments")
            .WithSummary("Xodim — biriktirilgan fayllar ro'yxati")
            .WithDescription(
                "Yuklangan sana bo'yicha kamayish tartibida. «documentTypeId» — faqat shu " +
                "turdagilar (tur bo'yicha guruh); bo'sh bo'lsa — hammasi.")
            .Produces<IReadOnlyList<EmployeeDocumentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                Guid employeeId,
                IFormFile file,
                [FromForm] Guid documentTypeId,
                [FromForm] string? note,
                UploadEmployeeDocumentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadEmployeeDocumentCommand(
                            employeeId, documentTypeId, content, file.FileName, file.Length, note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)

            // CSRF himoyasi bu yerda ORTIQCHA — sababi `EmployeeEndpoints.MapPhoto`
            // dagi bilan bir xil: JWT sarlavhada, sessiya cookie'da emas.
            .DisableAntiforgery()

            // Cheklov marshrut darajasida: chegaradan katta tana Kestrel
            // tomonidan umuman o'qilmaydi.
            .WithMetadata(new RequestSizeLimitAttribute(MaxDocumentRequestBytes))
            .WithName("UploadEmployeeDocument")
            .WithSummary("Xodim — fayl biriktirish")
            .WithDescription(
                "«multipart/form-data»: «file» (fayl), «documentTypeId» (hujjat turi — " +
                "«document-types» ma'lumotnomasidan), ixtiyoriy «note». Fayl turi MAZMUNIGA " +
                "qarab aniqlanadi (kengaytma va e'lon qilingan Content-Type'ga ishonilmaydi): " +
                "faqat PDF, JPEG, PNG yoki WebP, eng ko'pi 20 MB. Xatolar: «hujjat.katta», " +
                "«hujjat.yaroqsiz» (ikkalasi 400), «hujjat.turi_topilmadi» (404).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<EmployeeDocumentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{documentId:guid}/file", async (
                // `employeeId` marshrutda bor, ya'ni OpenAPI hujjatida ham parametr
                // sifatida turishi SHART — aks holda hujjat yaroqsiz bo'ladi va
                // `./bonvi gen` (Orval) uni o'qishdan bosh tortadi. Qiymat bu yerda
                // ishlatilmaydi: yozuv `documentId` bo'yicha topiladi.
                Guid employeeId,
                Guid documentId,
                GetEmployeeDocumentFileQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeDocumentFileQuery(documentId), cancellationToken)
                    .ConfigureAwait(false);

                // Yagona endpoint bo'lib, JSON emas — BAYT qaytaradi (xodim
                // rasmidagi `MapPhoto` bilan bir xil sabab).
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : Results.File(natija.Value.Content, natija.Value.ContentType, natija.Value.FileName);
            })
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("DownloadEmployeeDocument")
            .WithSummary("Xodim — fayl yuklab olish")
            .WithDescription(
                "Fayl bayti. «Content-Disposition» sarlavhasida ASL fayl nomi qaytadi. " +
                "Topilmasa: «hujjat.topilmadi» (404).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{documentId:guid}", async (
                // `employeeId` marshrutda bor, ya'ni OpenAPI hujjatida ham parametr
                // sifatida turishi SHART — aks holda hujjat yaroqsiz bo'ladi va
                // `./bonvi gen` (Orval) uni o'qishdan bosh tortadi. Qiymat bu yerda
                // ishlatilmaydi: yozuv `documentId` bo'yicha topiladi.
                Guid employeeId,
                Guid documentId,
                UpdateEmployeeDocumentRequest request,
                UpdateEmployeeDocumentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateEmployeeDocumentCommand(documentId, request.DocumentTypeId, request.Note),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)
            .WithName("UpdateEmployeeDocument")
            .WithSummary("Xodim — hujjat turi va izohini tuzatish")
            .WithDescription(
                "Faqat tur va izoh o'zgaradi — fayl O'ZGARMAYDI: noto'g'ri fayl yuklangan " +
                "bo'lsa, bu tuzatish emas, boshqa fakt — yozuv o'chirilib, yangisi yuklanadi.")
            .Produces<EmployeeDocumentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{documentId:guid}", async (
                // `employeeId` marshrutda bor, ya'ni OpenAPI hujjatida ham parametr
                // sifatida turishi SHART — aks holda hujjat yaroqsiz bo'ladi va
                // `./bonvi gen` (Orval) uni o'qishdan bosh tortadi. Qiymat bu yerda
                // ishlatilmaydi: yozuv `documentId` bo'yicha topiladi.
                Guid employeeId,
                Guid documentId,
                DeleteEmployeeDocumentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteEmployeeDocumentCommand(documentId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.EmployeesEdit)
            .WithName("DeleteEmployeeDocument")
            .WithSummary("Xodim — hujjatni o'chirish")
            .WithDescription(
                "Yumshoq o'chirish: yozuv bazada qoladi. Fayl ATAYLAB o'chirilmaydi — " +
                "sababi «EmployeeDocument» va «DeleteEmployeeDocumentCommandHandler» izohida.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
