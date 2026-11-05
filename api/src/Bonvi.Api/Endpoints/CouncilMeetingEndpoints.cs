using Bonvi.Api.Extensions;
using Bonvi.Application.Recruiting.Council;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Api.Endpoints;

/// <summary>Attendance and follow-up dates of a sitting.</summary>
/// <param name="Members">Members with attendance.</param>
/// <param name="FollowUp">Follow-up dates.</param>
public sealed record SaveCouncilMeetingRequest(
    IReadOnlyList<CouncilMemberDto>? Members,
    CouncilFollowUpDto? FollowUp);

/// <summary>One candidate's decision.</summary>
/// <param name="Decision">Required.</param>
/// <param name="Practical">Practical test.</param>
/// <param name="OfferedSalary">Ignored without <c>salary.view</c>.</param>
/// <param name="StartDate">Start date.</param>
/// <param name="Condition">Special condition.</param>
/// <param name="Conclusion">The council's words.</param>
public sealed record SaveCouncilDecisionRequest(
    CouncilDecisionKind? Decision,
    CouncilPracticalResult? Practical,
    decimal? OfferedSalary,
    DateOnly? StartDate,
    string? Condition,
    string? Conclusion);

/// <summary>The hiring council's minutes.</summary>
public static class CouncilMeetingEndpoints
{
    public static IEndpointRouteBuilder MapCouncilMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/recruiting/council-meetings").WithTags("Recruiting");

        group.MapGet("/", async (ListCouncilDaysQueryHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new ListCouncilDaysQuery(), cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("ListCouncilDays")
            .WithSummary("Council days from the last 60 days on: invited and decided counts")
            .Produces<IReadOnlyList<CouncilDayDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{date}", async (
                DateOnly date,
                GetCouncilMeetingQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetCouncilMeetingQuery(date), cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingView)
            .WithName("GetCouncilMeeting")
            .WithSummary("One sitting: members, candidates invited that day, saved decisions")
            .WithDescription("«offeredSalary» is null without salary.view.")
            .Produces<CouncilMeetingDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{date}", async (
                DateOnly date,
                SaveCouncilMeetingRequest request,
                SaveCouncilMeetingCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(new SaveCouncilMeetingCommand(date, request.Members, request.FollowUp), cancellationToken)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("SaveCouncilMeeting")
            .WithSummary("Saves attendance and follow-up dates")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{date}/candidates/{candidateId:guid}", async (
                DateOnly date,
                Guid candidateId,
                SaveCouncilDecisionRequest request,
                SaveCouncilDecisionCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .HandleAsync(
                        new SaveCouncilDecisionCommand(
                            date,
                            candidateId,
                            request.Decision,
                            request.Practical,
                            request.OfferedSalary,
                            request.StartDate,
                            request.Condition,
                            request.Conclusion),
                        cancellationToken)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Permissions.RecruitingManage)
            .WithName("SaveCouncilDecision")
            .WithSummary("Saves one candidate's decision and writes it to the «Кенгаш» stage entry")
            .WithDescription("The candidate's status doesn't change. Editing updates the same stage entry.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
