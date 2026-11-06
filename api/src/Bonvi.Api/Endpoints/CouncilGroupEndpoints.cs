using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Council;
using Bonvi.Application.Recruiting.Forms;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Api.Endpoints;

public sealed record SaveCouncilGroupMemberRequest(string? FullName, string? Phone, bool IsActive = true);

public sealed record SaveCouncilMiniAppRequest(string? Name);

public sealed record BindCouncilGroupRequest(string? Code, long ChatId, string? Title);

public sealed record MigrateCouncilGroupRequest(long FromChatId, long ToChatId);

public sealed record LinkCouncilMemberRequest(long TelegramUserId, string? Phone);

public sealed record MarkCouncilDigestSentRequest(DateOnly Date, long ChatId, long MessageId, string? Signature);

public sealed record SaveCouncilOpinionRequest(CouncilDecisionKind? Decision, string? Comment);

/// <summary>What the Mini App shows first: is this person known, and where to link if not.</summary>
public sealed record CouncilAppMeResponse(bool Recognized, string? FullName, string? BotUsername);

/// <summary>The council group in Telegram (docs/SPEC-kengash-guruhi.md).</summary>
public static class CouncilGroupEndpoints
{
    private const string InitDataHeader = "X-Telegram-Init-Data";
    private const string MemberItem = "CouncilMember";

    public static IEndpointRouteBuilder MapCouncilGroupEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapRoster(app);
        MapBot(app);
        MapMiniApp(app);

        return app;
    }

    private static void MapRoster(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recruiting/council-group")
            .WithTags("Recruiting")
            .RequireAuthorization(Permissions.SettingsManage);

        group.MapGet("/", async (GetCouncilGroupQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCouncilGroupQuery(), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetCouncilGroup")
            .WithSummary("The bound Telegram group, the Mini App's name and the council's roster")
            .Produces<CouncilGroupDto>(StatusCodes.Status200OK);

        group.MapPost("/members", async (SaveCouncilGroupMemberRequest request, SaveCouncilMemberCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SaveCouncilMemberCommand(null, request.FullName, request.Phone, true), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("AddCouncilGroupMember")
            .WithSummary("Adds a council member; the phone is what Telegram holds")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/members/{id:guid}", async (Guid id, SaveCouncilGroupMemberRequest request, SaveCouncilMemberCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SaveCouncilMemberCommand(id, request.FullName, request.Phone, request.IsActive), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("UpdateCouncilGroupMember")
            .WithSummary("Changes a member; a changed phone drops the Telegram link, inactive closes access")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/members/{id:guid}/unlink", async (Guid id, UnlinkCouncilMemberCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new UnlinkCouncilMemberCommand(id), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("UnlinkCouncilGroupMember")
            .WithSummary("Forgets the member's Telegram account")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/bind-code", async (IssueCouncilBindCodeCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new IssueCouncilBindCodeCommand(), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("IssueCouncilBindCode")
            .WithSummary("A single-use command, valid 30 minutes, that binds the group it is pasted into")
            .Produces<CouncilBindCodeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/mini-app", async (SaveCouncilMiniAppRequest request, SaveCouncilMiniAppCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SaveCouncilMiniAppCommand(request.Name), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("SaveCouncilMiniApp")
            .WithSummary("The Mini App's short name from BotFather; empty sends buttons through the private chat")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapBot(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recruiting/council-bot")
            .WithTags("Recruiting")
            .RequireAuthorization(AuthenticationExtensions.BotServicePolicy);

        group.MapGet("/state", async (GetCouncilBotStateQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCouncilBotStateQuery(), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetCouncilBotState")
            .WithSummary("The bound group and the message due, bot only")
            .Produces<CouncilBotStateDto>(StatusCodes.Status200OK);

        group.MapPost("/bind", async (BindCouncilGroupRequest request, BindCouncilGroupCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new BindCouncilGroupCommand(request.Code, request.ChatId, request.Title), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("BindCouncilGroup")
            .WithSummary("The group where the binding command was written, bot only")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/migrate", async (MigrateCouncilGroupRequest request, MigrateCouncilGroupCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new MigrateCouncilGroupCommand(request.FromChatId, request.ToChatId), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("MigrateCouncilGroup")
            .WithSummary("Telegram turned a group into a supergroup, which changes its id, bot only. Another group's move changes nothing")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/link", async (LinkCouncilMemberRequest request, LinkCouncilMemberCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new LinkCouncilMemberCommand(request.TelegramUserId, request.Phone), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("LinkCouncilMember")
            .WithSummary("A person shared their own contact, bot only. «linked: false» when the phone is not on the roster")
            .Produces<CouncilLinkResultDto>(StatusCodes.Status200OK);

        group.MapPost("/digest-sent", async (MarkCouncilDigestSentRequest request, MarkCouncilDigestSentCommandHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new MarkCouncilDigestSentCommand(request.Date, request.ChatId, request.MessageId, request.Signature), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("MarkCouncilDigestSent")
            .WithSummary("The bot posted or edited the day's message, bot only")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static void MapMiniApp(IEndpointRouteBuilder app)
    {
        var open = app.MapGroup("/api/council-app")
            .WithTags("Council Mini App")

            // No JWT here: identity is the launch data Telegram signed, checked on every request.
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.CouncilApp);

        open.MapGet("/me", async (
                HttpContext context,
                CouncilMemberIdentifier identifier,
                GetCouncilGroupQueryHandler group,
                CancellationToken cancellationToken) =>
            {
                var identity = await identifier.IdentifyAsync(context.Request.Headers[InitDataHeader], cancellationToken).ConfigureAwait(false);

                if (identity.Access == CouncilAppAccess.Invalid)
                {
                    return Results.Unauthorized();
                }

                var settings = await group.HandleAsync(new GetCouncilGroupQuery(), cancellationToken).ConfigureAwait(false);

                return Results.Ok(new CouncilAppMeResponse(identity.Member is not null, identity.Member?.FullName, settings.Value.BotUsername));
            })
            .WithName("GetCouncilAppMe")
            .WithSummary("Is the person who opened the Mini App a linked member")
            .Produces<CouncilAppMeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        var member = open.MapGroup(string.Empty).AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            var identifier = context.RequestServices.GetRequiredService<CouncilMemberIdentifier>();
            var identity = await identifier.IdentifyAsync(context.Request.Headers[InitDataHeader], context.RequestAborted).ConfigureAwait(false);

            switch (identity.Access)
            {
                case CouncilAppAccess.Invalid:
                    return Results.Unauthorized();
                case CouncilAppAccess.Unknown:
                    return Results.Forbid();
                default:
                    context.Items[MemberItem] = identity.Member!.Id;
                    context.Items[HttpAuditActorSource.CouncilMemberNameItem] = identity.Member.FullName;
                    return await next(invocation).ConfigureAwait(false);
            }
        });

        member.MapGet("/candidates", async (HttpContext context, ListCouncilAppCandidatesQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new ListCouncilAppCandidatesQuery(MemberId(context)), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("ListCouncilAppCandidates")
            .WithSummary("Open candidates invited from 14 days ago on")
            .Produces<IReadOnlyList<CouncilAppCandidateRowDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        member.MapGet("/candidates/{id:guid}", async (Guid id, HttpContext context, GetCouncilAppCandidateQueryHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new GetCouncilAppCandidateQuery(id, MemberId(context)), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("GetCouncilAppCandidate")
            .WithSummary("A candidate's questionnaire and the member's own earlier conclusions, read only")
            .Produces<CouncilAppCandidateDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        member.MapGet("/candidates/{id:guid}/photo", async (
                Guid id,
                HttpContext context,
                SaveCouncilOpinionCommandHandler access,
                GetCandidatePhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var reachable = await access.CheckReachableAsync(id, cancellationToken).ConfigureAwait(false);
                if (reachable.IsFailure)
                {
                    return (IResult)reachable.ToHttpResult();
                }

                var photo = await handler
                    .HandleAsync(new GetCandidatePhotoQuery(id, PhotoHttp.ReadKnownVersion(context.Request)), cancellationToken)
                    .ConfigureAwait(false);

                return photo.IsFailure ? photo.ToHttpResult() : PhotoHttp.Write(context, photo.Value);
            })
            .WithName("GetCouncilAppCandidatePhoto")
            .WithSummary("A candidate's photo")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .ProducesProblem(StatusCodes.Status404NotFound);

        member.MapGet("/candidates/{id:guid}/forms/{formId:guid}/pdf", async (
                Guid id,
                Guid formId,
                SaveCouncilOpinionCommandHandler access,
                GetCandidateFormPdfQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var reachable = await access.CheckReachableAsync(id, cancellationToken).ConfigureAwait(false);
                if (reachable.IsFailure)
                {
                    return (IResult)reachable.ToHttpResult();
                }

                var pdf = await handler.HandleAsync(new GetCandidateFormPdfQuery(id, formId), cancellationToken).ConfigureAwait(false);

                return pdf.IsFailure ? pdf.ToHttpResult() : Results.File(pdf.Value.Content, "application/pdf", pdf.Value.FileName);
            })
            .WithName("GetCouncilAppCandidateFormPdf")
            .WithSummary("The candidate's questionnaire as the paper form")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

        member.MapPost("/candidates/{id:guid}/opinions", async (
                Guid id,
                SaveCouncilOpinionRequest request,
                HttpContext context,
                SaveCouncilOpinionCommandHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(new SaveCouncilOpinionCommand(id, MemberId(context), request.Decision, request.Comment), cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .WithName("SaveCouncilOpinion")
            .WithSummary("A member's conclusion; every write is kept, there is no time limit")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static Guid MemberId(HttpContext context) => (Guid)context.Items[MemberItem]!;
}
