using Bonvi.Api.Extensions;
using Bonvi.Application.Processes.Gates;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Case gate override body.</summary>
/// <param name="Reason">Why the gate is overridden, at least 10 characters.</param>
public sealed record GrantGateOverrideBody(string? Reason);

/// <summary>Direct-hire override body.</summary>
/// <param name="ForUserId">The operator allowed to hire without a candidate.</param>
/// <param name="Reason">Why, at least 10 characters.</param>
public sealed record GrantDirectHireOverrideBody(Guid ForUserId, string? Reason);

/// <summary>Gate overrides of the process engine (docs/SPEC-mening-ishlarim.md §8.4, §10 rows 14–15).</summary>
public static class ProcessGateOverrideEndpoints
{
    public static IEndpointRouteBuilder MapProcessGateOverrideEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/processes").WithTags("Processes");

        group.MapPost("/cases/{id:guid}/gates/{code}/override", async (
                Guid id,
                string code,
                GrantGateOverrideBody body,
                GrantCaseGateOverrideCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GrantCaseGateOverrideCommand(id, code, body.Reason), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ProcessesOverride)
            .WithName("GrantProcessGateOverride")
            .WithSummary("Exception to a blocking gate of a case")
            .WithDescription(
                "Valid 24 hours, single use. Besides the permission the caller must hold the hr1 assignment " +
                "(while nobody holds it, «processes.manage»): 403 «jarayon.ruxsat_yoq». " +
                "Permission: «processes.override».")
            .Produces<ProcessGateOverrideDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/gate-overrides/direct-hire", async (
                GrantDirectHireOverrideBody body,
                GrantDirectHireOverrideCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GrantDirectHireOverrideCommand(body.ForUserId, body.Reason), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.ProcessesOverride)
            .WithName("GrantDirectHireOverride")
            .WithSummary("Allow one operator one hire without a candidate")
            .WithDescription(
                "Valid 24 hours, single use, only for the named operator. Same hr1 rule as the case override. " +
                "Permission: «processes.override».")
            .Produces<ProcessGateOverrideDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/gate-overrides", async (
                DateOnly? from,
                ListGateOverridesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var rows = await handler
                    .HandleAsync(new ListGateOverridesQuery(from), cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(rows);
            })
            .RequireAuthorization(Permissions.ProcessesView)
            .WithName("ListProcessGateOverrides")
            .WithSummary("The overrides log, newest first")
            .WithDescription(
                "Grants since «from» (Tashkent date), by default the last 30 days. " +
                "Permission: «processes.view».")
            .Produces<IReadOnlyList<ProcessGateOverrideDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
