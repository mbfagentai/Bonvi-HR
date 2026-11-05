using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Completeness;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>«Hujjatlari chala»: which current employees still lack what, for HR to fill in.</summary>
public static class EmployeeCompletenessEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeCompletenessEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/employees/completeness").WithTags("Employees");

        group.MapGet("/", async (
                string? search,
                Guid? departmentId,
                string? missing,
                int? page,
                int? pageSize,
                GetEmployeeCompletenessQueryHandler handler,
                CancellationToken cancellationToken) =>
                (await handler
                    .HandleAsync(new GetEmployeeCompletenessQuery(search, departmentId, missing, page, pageSize), cancellationToken)
                    .ConfigureAwait(false))
                .ToHttpResult())
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetEmployeeCompleteness")
            .WithSummary("Current employees with something missing")
            .WithDescription(
                "Only employees still working, and only those lacking at least one kind: most gaps first, then the " +
                "newest hire. «missing» is one kind code from /summary; an unknown one is «xodim.hujjat_turi_notogri» " +
                "(400). «salary» is a kind only with «salary.view». Says whether PINFL and passport exist, never their values.")
            .Produces<PagedResult<EmployeeCompletenessItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/summary", async (
                string? search,
                Guid? departmentId,
                GetEmployeeCompletenessSummaryQueryHandler handler,
                CancellationToken cancellationToken) =>
                (await handler
                    .HandleAsync(new GetEmployeeCompletenessSummaryQuery(search, departmentId), cancellationToken)
                    .ConfigureAwait(false))
                .ToHttpResult())
            .RequireAuthorization(Permissions.EmployeesView)
            .WithName("GetEmployeeCompletenessSummary")
            .WithSummary("How many current employees lack each kind")
            .WithDescription(
                "Every kind checked for this viewer, including those at zero: the fixed ones, «salary» with " +
                "«salary.view», and one «doc:{id}» per active required document type.")
            .Produces<EmployeeCompletenessSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
