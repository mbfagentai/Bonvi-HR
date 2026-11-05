using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>
/// «Mening ishlarim» reads (docs/SPEC-mening-ishlarim.md §10 #1–#8). Writes and gate overrides have endpoint files of
/// their own in the same <c>/api/processes</c> group path.
/// </summary>
public static class ProcessEndpoints
{
    public static IEndpointRouteBuilder MapProcessEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/processes").WithTags("Processes");

        group.MapGet("/my", async (GetMyWorkQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetMyProcessWork")
            .WithSummary("Mening ishlarim — the viewer's tasks and «Tartib» cards")
            .WithDescription(
                "Sections: urgent (due today or late, and Blocked steps the viewer can clear), later, held (the " +
                "viewer's Blocked steps someone else must clear), waiting (Locked within 7 days behind someone " +
                "else's open step). Runs a throttled engine pass first.")
            .Produces<ProcessMyWorkDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/my/count", async (GetMyProcessCountQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetMyProcessCount")
            .WithSummary("Menu badge — urgent + later")
            .WithDescription("Runs no engine pass: the badge is asked for on every page.")
            .Produces<ProcessMyCountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/cases", async (
                string? process,
                string? status,
                Guid? department,
                Guid? employee,
                Guid? candidate,
                string? holderRole,
                bool? late,
                DateOnly? from,
                DateOnly? to,
                string? q,
                int? page,
                int? pageSize,
                ListProcessCasesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(
                        new ListProcessCasesQuery(process, status, department, employee, candidate, holderRole, late, from, to, q, page, pageSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("ListProcessCases")
            .WithSummary("Cases — filtered list, newest first")
            .WithDescription(
                "«status» — Active | Completed | Cancelled (unknown → jarayon.kiritish_notogri 400). «from»/«to» — " +
                "start date, both included. «holderRole» and «late» look at Active and Blocked steps. «q» — " +
                "employee or candidate name, or the request's title. «pageSize» 50 by default, at most 10 000.")
            .Produces<PagedResult<ProcessCaseListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/board", async (string? process, GetProcessBoardQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(process, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetProcessBoard")
            .WithSummary("Board of one process — running cases by column")
            .WithDescription("«process» is required (missing → 400, unknown → 404).")
            .Produces<ProcessBoardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/cases/{id:guid}", async (Guid id, GetProcessCaseQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetProcessCase")
            .WithSummary("One case with all steps and linked blanks")
            .WithDescription("Reconciles this case first. canComplete / canSkip / canUndo say what this viewer may do.")
            .Produces<ProcessCaseDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/cases/{id:guid}/chain", async (Guid id, GetProcessCaseChainQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetProcessCaseChain")
            .WithSummary("The cases one person's hiring reads as, in order")
            .WithDescription(
                "A hire-candidate or hire-formalise case: its hire-request, its hire-candidate, its hire-formalise, " +
                "as far as they exist. Any other case, a hire-request included: just itself. Runs no engine pass.")
            .Produces<ProcessCaseChainDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/late", async (GetProcessLateQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetProcessLate")
            .WithSummary("Kim kechikdi — blocked, late, warnings, overrides of the last 30 days")
            .Produces<ProcessLateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/templates", async (ListProcessTemplatesQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("ListProcessTemplates")
            .WithSummary("Published process definitions")
            .Produces<IReadOnlyList<ProcessTemplateDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/status", async (GetProcessStatusQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("GetProcessEngineStatus")
            .WithSummary("Engine state — dormant or active, gates mode, last pass")
            .Produces<ProcessStatusDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
