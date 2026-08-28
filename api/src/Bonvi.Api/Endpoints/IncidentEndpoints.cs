using Bonvi.Api.Extensions;
using Bonvi.Application.Safety;
using Bonvi.Application.Safety.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Safety;

namespace Bonvi.Api.Endpoints;

/// <summary>Baxtsiz hodisani yozish so'rovi tanasi.</summary>
/// <param name="EmployeeId">Jabrlangan xodim.</param>
/// <param name="IncidentDate">Hodisa sanasi.</param>
/// <param name="Location">Hodisa joyi. Majburiy.</param>
/// <param name="Description">Hodisa tavsifi. Majburiy.</param>
/// <param name="Severity">Jabrlanish darajasi: Minor | Moderate | Severe | Fatal.</param>
/// <param name="Shift">Smena/vaqt tavsifi.</param>
/// <param name="PositionId">Hodisa vaqtidagi lavozim.</param>
/// <param name="Cause">Sababi.</param>
/// <param name="CorrectiveAction">Ko'rilgan chora-tadbir.</param>
/// <param name="MedicalAidProvided">Tibbiy yordam ko'rsatildimi.</param>
/// <param name="MedicalAidNote">Tibbiy yordam tafsiloti.</param>
/// <param name="ResponsibleEmployeeId">Mas'ul xodim.</param>
public sealed record RecordIncidentRequest(
    Guid EmployeeId,
    DateOnly IncidentDate,
    string? Location,
    string? Description,
    IncidentSeverity Severity,
    string? Shift = null,
    Guid? PositionId = null,
    string? Cause = null,
    string? CorrectiveAction = null,
    bool MedicalAidProvided = false,
    string? MedicalAidNote = null,
    Guid? ResponsibleEmployeeId = null);

/// <summary>Hodisa yozuvini tuzatish so'rovi tanasi — <c>EmployeeId</c> o'zgarmaydi.</summary>
public sealed record UpdateIncidentRequest(
    DateOnly IncidentDate,
    string? Location,
    string? Description,
    IncidentSeverity Severity,
    string? Shift = null,
    Guid? PositionId = null,
    string? Cause = null,
    string? CorrectiveAction = null,
    bool MedicalAidProvided = false,
    string? MedicalAidNote = null,
    Guid? ResponsibleEmployeeId = null);

/// <summary>Komissiya xulosasini yozish so'rovi tanasi.</summary>
/// <param name="Conclusion">Xulosa matni. Majburiy.</param>
/// <param name="ConcludedAt">Xulosa sanasi. Bo'sh — bugun.</param>
public sealed record ConcludeIncidentRequest(string? Conclusion, DateOnly? ConcludedAt = null);

/// <summary>
/// Baxtsiz hodisalar jurnali endpoint'lari — M11.
/// </summary>
/// <remarks>
/// Ruxsatlar ALOHIDA guruh (<see cref="Permissions.SafetyView"/> /
/// <see cref="Permissions.SafetyManage"/>) — sabab
/// <see cref="Permissions.SafetyView"/> izohida.
/// </remarks>
public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/safety/incidents").WithTags("Safety");

        group.MapGet("/", async (
                DateOnly? from,
                DateOnly? to,
                IncidentSeverity? severity,
                string? search,
                ListIncidentsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ListIncidentsQuery(from, to, severity, search), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SafetyView)
            .WithName("ListIncidents")
            .WithSummary("Baxtsiz hodisalar — jurnal")
            .WithDescription(
                "Barcha yozuvlar, hodisa sanasi bo'yicha kamayish tartibida. " +
                "«from»/«to» — ikki tomondan yopiq oraliq. «severity» — Minor | Moderate | " +
                "Severe | Fatal. «search» — JABRLANGAN XODIM bo'yicha (F.I.Sh., JSHSHIR, " +
                "pasport, telefon); hodisa tavsifi va mas'ul shaxs qidiruvga kirmaydi.")
            .Produces<IReadOnlyList<IncidentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                GetIncidentQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetIncidentQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SafetyView)
            .WithName("GetIncident")
            .WithSummary("Baxtsiz hodisa — bitta yozuv")
            .Produces<IncidentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                RecordIncidentRequest request,
                RecordIncidentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RecordIncidentCommand(
                            request.EmployeeId,
                            request.IncidentDate,
                            request.Location,
                            request.Description,
                            request.Severity,
                            request.Shift,
                            request.PositionId,
                            request.Cause,
                            request.CorrectiveAction,
                            request.MedicalAidProvided,
                            request.MedicalAidNote,
                            request.ResponsibleEmployeeId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("RecordIncident")
            .WithSummary("Baxtsiz hodisa — yozish")
            .WithDescription(
                "Komissiya xulosasi bu so'rovda YO'Q — u tekshiruv tugagach, alohida " +
                "«/conclude» orqali qo'shiladi. Joy va tavsif sababsiz yaratilmaydi: " +
                "«baxtsiz_hodisa.joy_bosh» / «baxtsiz_hodisa.tavsif_bosh» (400).")
            .Produces<IncidentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateIncidentRequest request,
                UpdateIncidentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateIncidentCommand(
                            id,
                            request.IncidentDate,
                            request.Location,
                            request.Description,
                            request.Severity,
                            request.Shift,
                            request.PositionId,
                            request.Cause,
                            request.CorrectiveAction,
                            request.MedicalAidProvided,
                            request.MedicalAidNote,
                            request.ResponsibleEmployeeId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("UpdateIncident")
            .WithSummary("Baxtsiz hodisa — kiritish xatosini tuzatish")
            .WithDescription(
                "Xodim (EmployeeId) o'zgartirilmaydi — noto'g'ri xodimga yozilgan bo'lsa, " +
                "yozuv o'chirilib yangisi kiritiladi. Komissiya xulosasi bu yerdan ham " +
                "o'zgartirilmaydi — «/conclude» orqali.")
            .Produces<IncidentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/conclude", async (
                Guid id,
                ConcludeIncidentRequest request,
                ConcludeIncidentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ConcludeIncidentCommand(id, request.Conclusion, request.ConcludedAt),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("ConcludeIncident")
            .WithSummary("Baxtsiz hodisa — komissiya xulosasini yozish")
            .WithDescription(
                "Xulosa sababsiz bo'lmaydi: «baxtsiz_hodisa.xulosa_bosh» (400). " +
                "«ConcludedAt» bo'sh bo'lsa — bugun.")
            .Produces<IncidentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                DeleteIncidentCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteIncidentCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.SafetyManage)
            .WithName("DeleteIncident")
            .WithSummary("Baxtsiz hodisa — yozuvni o'chirish")
            .WithDescription("Yumshoq o'chirish: yozuv bazada qoladi, faqat ro'yxatlarda ko'rinmay qoladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
