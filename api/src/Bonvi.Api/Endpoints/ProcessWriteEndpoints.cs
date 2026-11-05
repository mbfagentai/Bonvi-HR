using System.Text.Json;
using Bonvi.Api.Extensions;
using Bonvi.Application.Processes.Commands;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Processes;

namespace Bonvi.Api.Endpoints;

/// <summary>Manual start body.</summary>
/// <param name="ProcessCode">Only <c>dismissal</c> in Phase 1.</param>
/// <param name="EmploymentId">Whose dismissal.</param>
/// <param name="LastWorkDay">Required, <c>yyyy-MM-dd</c>.</param>
/// <param name="Basis">Free text, at most 1000 characters.</param>
public sealed record StartProcessCaseBody(string? ProcessCode, Guid? EmploymentId, DateOnly? LastWorkDay, string? Basis);

/// <summary>«Bajarildi» body.</summary>
/// <param name="Version">The case <c>version</c> the screen shows.</param>
/// <param name="Inputs">Keyed by input id. Text, choice — string; date — <c>yyyy-MM-dd</c>; int — number; checklist — object item → <c>done</c> | <c>na</c>. Marks on data-backed items are ignored.</param>
/// <param name="MissingReason">Required data is not in the system and a <c>processes.manage</c> holder closes anyway: why. At most 1000 characters; ignored when nothing is missing.</param>
public sealed record CompleteProcessStepBody(int? Version, JsonElement? Inputs, string? MissingReason);

/// <summary>«Kerak emas» body.</summary>
/// <param name="Version">The case <c>version</c> the screen shows.</param>
/// <param name="Reason">Required when only the legacy rule allows the skip.</param>
public sealed record SkipProcessStepBody(int? Version, string? Reason);

/// <summary>Undo body.</summary>
/// <param name="Version">The case <c>version</c> the screen shows.</param>
public sealed record UndoProcessStepBody(int? Version);

/// <summary>Cancel and reopen body.</summary>
/// <param name="Version">The case <c>version</c> the screen shows.</param>
/// <param name="Reason">Required, at most 1000 characters.</param>
public sealed record ProcessCaseReasonBody(int? Version, string? Reason);

/// <summary>Stage bindings body.</summary>
/// <param name="Bindings">The codes to change; a code left out keeps its binding.</param>
public sealed record SetProcessStageBindingsBody(IReadOnlyList<SetProcessStageBindingItem>? Bindings);

/// <summary>Calendar exception body.</summary>
/// <param name="Date">The date; an existing row of that date is changed.</param>
/// <param name="Kind"><c>Holiday</c> or <c>WorkingDay</c>.</param>
/// <param name="Title">Required, at most 200 characters.</param>
public sealed record SaveProcessHolidayBody(DateOnly? Date, ProcessHolidayKind? Kind, string? Title);

/// <summary>«Mening ishlarim» writes (docs/SPEC-mening-ishlarim.md §10 #9–#13, #16–#18). Reads are in <see cref="ProcessEndpoints"/>.</summary>
public static class ProcessWriteEndpoints
{
    public static IEndpointRouteBuilder MapProcessWriteEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/processes").WithTags("Processes");

        MapCaseWrites(group);
        MapStepWrites(group);
        MapSettings(group);

        return app;
    }

    private static void MapCaseWrites(RouteGroupBuilder group)
    {
        group.MapPost("/cases", async (StartProcessCaseBody body, StartProcessCaseCommandHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(new StartProcessCaseCommand(body.ProcessCode, body.EmploymentId, body.LastWorkDay, body.Basis), cancellationToken)
                    .ConfigureAwait(false);

                return result.IsSuccess
                    ? result.ToCreatedResult($"/api/processes/cases/{result.Value.Id}")
                    : result.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ProcessesWork)
            .WithName("StartProcessCase")
            .WithSummary("Start a dismissal case by hand")
            .WithDescription(
                "Only «dismissal» in Phase 1. 404 unknown employment; 409 jarayon.allaqachon_ochilgan if it has a case " +
                "of any status, jarayon.holat_notogri if the employment has ended or the engine is dormant, jarayon.band " +
                "while a pass runs (try again). Permission: «processes.work».")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/cases/{id:guid}/cancel", async (Guid id, ProcessCaseReasonBody body, CancelProcessCaseCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new CancelProcessCaseCommand(id, body.Version, body.Reason), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("CancelProcessCase")
            .WithSummary("Cancel a running case")
            .WithDescription("Reason required. 409 jarayon.versiya on a stale version. Permission: «processes.manage».")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/cases/{id:guid}/reopen", async (Guid id, ProcessCaseReasonBody body, ReopenProcessCaseCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ReopenProcessCaseCommand(id, body.Version, body.Reason), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("ReopenProcessCase")
            .WithSummary("Take a finished case back to Active")
            .WithDescription(
                "Reason required. 409 jarayon.yakun_kuchda when what ended the case still holds: the next pass would end " +
                "it again, so nothing is changed. 409 jarayon.band while a pass runs (try again). " +
                "Permission: «processes.manage».")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/reconcile", async (RunProcessReconcileCommandHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("RunProcessReconcile")
            .WithSummary("Run an engine pass now (diagnostics)")
            .WithDescription("status: Finished | Busy (another pass holds the lock) | Dormant. Permission: «processes.manage».")
            .Produces<ProcessReconcileResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void MapStepWrites(RouteGroupBuilder group)
    {
        group.MapPost("/cases/{id:guid}/steps/{code}/done", async (Guid id, string code, CompleteProcessStepBody body, CompleteProcessStepCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new CompleteProcessStepCommand(id, code, body.Version, body.Inputs, body.MissingReason), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesWork)
            .WithName("CompleteProcessStep")
            .WithSummary("«Bajarildi» — close a manual step")
            .WithDescription(
                "Inputs are checked against the step's manual.inputs (400 jarayon.kiritish_notogri). 403 if the viewer " +
                "neither holds the step nor has «processes.manage». 409 jarayon.qadam_holati_notogri if the step is not " +
                "Active, the blocking gate's code if it is Blocked, jarayon.versiya on a stale version. " +
                "Data-backed checklist items (manual.inputs[].autoItems) are settled by the server, marks sent for them are " +
                "ignored: a required one with no data is 409 jarayon.malumot_yetishmaydi, whose «missing» lists {input, item, " +
                "label}, unless missingReason is given, which needs «processes.manage» (403) and is audited. " +
                "Permission: «processes.work».")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/cases/{id:guid}/steps/{code}/skip", async (Guid id, string code, SkipProcessStepBody body, SkipProcessStepCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SkipProcessStepCommand(id, code, body.Version, body.Reason), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesWork)
            .WithName("SkipProcessStep")
            .WithSummary("«Kerak emas» — skip an open step")
            .WithDescription(
                "Only a skippable step, or legacySkippable in a legacy case (then the reason is required). " +
                "Permission: «processes.work».")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/cases/{id:guid}/steps/{code}/undo", async (Guid id, string code, UndoProcessStepBody body, UndoProcessStepCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UndoProcessStepCommand(id, code, body.Version), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesWork)
            .WithName("UndoProcessStep")
            .WithSummary("Undo a step a person closed")
            .WithDescription(
                "One's own within 24 hours; with «processes.manage» anyone's, any time. A fact-closed step cannot be " +
                "undone (409 jarayon.qadam_holati_notogri). Permission: «processes.work».")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapSettings(RouteGroupBuilder group)
    {
        group.MapGet("/settings/stage-bindings", async (ListProcessStageBindingsQueryHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("ListProcessStageBindings")
            .WithSummary("Which pipeline stage means «contacted», «invited», «council», «practical test»")
            .WithDescription("All four codes, unbound ones with a null stage. Permission: «processes.manage».")
            .Produces<IReadOnlyList<ProcessStageBindingDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/settings/stage-bindings", async (SetProcessStageBindingsBody body, SetProcessStageBindingsCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SetProcessStageBindingsCommand(body.Bindings), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("SetProcessStageBindings")
            .WithSummary("Bind or unbind stages")
            .WithDescription(
                "A code left out keeps its binding; a null stage unbinds. An unknown code or stage refuses the whole " +
                "request (400). Answers with all four codes. Permission: «processes.manage».")
            .Produces<IReadOnlyList<ProcessStageBindingDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/holidays", async (int? year, ListProcessHolidaysQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListProcessHolidaysQuery(year), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("ListProcessHolidays")
            .WithSummary("Working-day calendar exceptions, by date")
            .WithDescription("«year» narrows to one year. Permission: «processes.view».")
            .Produces<IReadOnlyList<ProcessHolidayDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/holidays", async (SaveProcessHolidayBody body, SaveProcessHolidayCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SaveProcessHolidayCommand(body.Date, body.Kind, body.Title), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("SaveProcessHoliday")
            .WithSummary("Add a calendar exception, or change the one on that date")
            .WithDescription("Due dates follow on the next pass. Permission: «processes.manage».")
            .Produces<ProcessHolidayDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/holidays/{date}", async (DateOnly date, DeleteProcessHolidayCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(date, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesManage)
            .WithName("DeleteProcessHoliday")
            .WithSummary("Remove a calendar exception")
            .WithDescription("Route date is yyyy-MM-dd. Permission: «processes.manage».")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
