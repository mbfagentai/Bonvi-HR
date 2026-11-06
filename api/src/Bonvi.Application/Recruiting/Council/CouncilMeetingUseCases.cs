using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Council;

/// <summary>One council member on the minutes.</summary>
/// <param name="FullName">Name as printed.</param>
/// <param name="Role">Role on the council, free text.</param>
/// <param name="Present">Attended this sitting.</param>
public sealed record CouncilMemberDto(string FullName, string? Role, bool Present);

/// <summary>Dates of the follow-up steps HR does after the sitting.</summary>
/// <param name="ResultsDeliveredOn">Candidates told the result.</param>
/// <param name="OffersGivenOn">Accepted candidates got the offer and the document list.</param>
/// <param name="DocumentsHandedOn">Documents handed over for the order.</param>
/// <param name="RegistryOn">Entered into the registry.</param>
/// <param name="ReserveOn">Reserve candidates entered into the reserve base.</param>
public sealed record CouncilFollowUpDto(
    DateOnly? ResultsDeliveredOn,
    DateOnly? OffersGivenOn,
    DateOnly? DocumentsHandedOn,
    DateOnly? RegistryOn,
    DateOnly? ReserveOn);

/// <summary>One candidate on a sitting's list, invited that day or already decided.</summary>
/// <param name="CandidateId">Candidate.</param>
/// <param name="FullName">Candidate's name.</param>
/// <param name="Position">Vacancy title.</param>
/// <param name="FormNumber">Latest online form's number, when there is one.</param>
/// <param name="IsActive">Still in the funnel.</param>
/// <param name="Decided">A decision is saved.</param>
/// <param name="Decision">Saved decision.</param>
/// <param name="Practical">Practical test.</param>
/// <param name="OfferedSalary">Null without <c>salary.view</c>.</param>
/// <param name="StartDate">Start date.</param>
/// <param name="Condition">Special condition.</param>
/// <param name="Conclusion">Saved words, or the day's «Кенгаш» entry written earlier.</param>
/// <param name="Opinions">The members' own conclusions for this day, latest first.</param>
public sealed record CouncilCandidateDto(
    Guid CandidateId,
    string FullName,
    string Position,
    int? FormNumber,
    bool IsActive,
    bool Decided,
    CouncilDecisionKind? Decision,
    CouncilPracticalResult? Practical,
    decimal? OfferedSalary,
    DateOnly? StartDate,
    string? Condition,
    string? Conclusion,
    IReadOnlyList<CouncilOpinionDto>? Opinions = null);

/// <summary>One sitting with its list.</summary>
/// <param name="Date">Sitting's day.</param>
/// <param name="Number">Minutes' number; null until first saved.</param>
/// <param name="Members">Members; the previous sitting's names, unticked, until first saved.</param>
/// <param name="FollowUp">Follow-up dates.</param>
/// <param name="CanSeeSalary">Viewer holds <c>salary.view</c>.</param>
/// <param name="Candidates">The list, by name.</param>
public sealed record CouncilMeetingDto(
    DateOnly Date,
    int? Number,
    IReadOnlyList<CouncilMemberDto> Members,
    CouncilFollowUpDto FollowUp,
    bool CanSeeSalary,
    IReadOnlyList<CouncilCandidateDto> Candidates);

/// <summary>A day that has invitations or a saved sitting.</summary>
/// <param name="Date">The day.</param>
/// <param name="Invited">Candidates invited for it.</param>
/// <param name="Decided">Decisions saved for it.</param>
public sealed record CouncilDayDto(DateOnly Date, int Invited, int Decided);

/// <summary>Reads a sitting.</summary>
public sealed record GetCouncilMeetingQuery(DateOnly Date);

/// <summary>Lists recent and coming council days.</summary>
public sealed record ListCouncilDaysQuery;

/// <summary>Saves attendance and follow-up dates.</summary>
public sealed record SaveCouncilMeetingCommand(
    DateOnly Date,
    IReadOnlyList<CouncilMemberDto>? Members,
    CouncilFollowUpDto? FollowUp);

/// <summary>Saves one candidate's decision.</summary>
public sealed record SaveCouncilDecisionCommand(
    DateOnly Date,
    Guid CandidateId,
    CouncilDecisionKind? Decision,
    CouncilPracticalResult? Practical,
    decimal? OfferedSalary,
    DateOnly? StartDate,
    string? Condition,
    string? Conclusion);

internal static class CouncilJson
{
    private const int MaxMembers = 30;
    private const int MaxNameLength = 200;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(IReadOnlyList<CouncilMemberDto> members)
        => JsonSerializer.Serialize(members, Options);

    public static string Write(CouncilFollowUpDto followUp)
        => JsonSerializer.Serialize(followUp, Options);

    public static IReadOnlyList<CouncilMemberDto> ReadMembers(string? json)
        => string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<CouncilMemberDto>>(json, Options) ?? [];

    public static CouncilFollowUpDto ReadFollowUp(string? json)
        => (string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CouncilFollowUpDto>(json, Options))
            ?? new CouncilFollowUpDto(null, null, null, null, null);

    /// <summary>Trims the rows and drops those with no name; null when the list breaks a limit.</summary>
    public static IReadOnlyList<CouncilMemberDto>? Clean(IReadOnlyList<CouncilMemberDto>? members)
    {
        var rows = (members ?? [])
            .Where(member => !string.IsNullOrWhiteSpace(member.FullName))
            .Select(member => new CouncilMemberDto(
                member.FullName.Trim(),
                string.IsNullOrWhiteSpace(member.Role) ? null : member.Role.Trim(),
                member.Present))
            .ToList();

        return rows.Count > MaxMembers
            || rows.Exists(member => member.FullName.Length > MaxNameLength || member.Role?.Length > MaxNameLength)
            ? null
            : rows;
    }
}

/// <summary>Builds the sitting's page.</summary>
public sealed class GetCouncilMeetingQueryHandler
{
    private readonly ICouncilMeetingRepository _meetings;
    private readonly IRecruitingReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public GetCouncilMeetingQueryHandler(
        ICouncilMeetingRepository meetings,
        IRecruitingReadModel readModel,
        ICurrentUser currentUser)
    {
        _meetings = meetings;
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<CouncilMeetingDto>> HandleAsync(
        GetCouncilMeetingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var canSeeSalary = _currentUser.CanSeeSalary();
        var meeting = await _meetings.FindByDateAsync(query.Date, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<CouncilMemberDto> members;
        if (meeting is not null)
        {
            members = CouncilJson.ReadMembers(meeting.Members);
        }
        else
        {
            var previous = await _meetings.FindLatestBeforeAsync(query.Date, cancellationToken).ConfigureAwait(false);
            members = CouncilJson.ReadMembers(previous?.Members)
                .Select(member => member with { Present = false })
                .ToList();
        }

        var candidates = await _readModel
            .ListCouncilCandidatesAsync(query.Date, meeting?.Id, cancellationToken)
            .ConfigureAwait(false);

        var opinions = await _readModel.ListCouncilOpinionsAsync(query.Date, cancellationToken).ConfigureAwait(false);
        var byCandidate = opinions.ToLookup(row => row.CandidateId, row => row.Opinion);

        return new CouncilMeetingDto(
            query.Date,
            meeting?.Number,
            members,
            CouncilJson.ReadFollowUp(meeting?.FollowUp),
            canSeeSalary,
            [
                .. candidates.Select(row => row with
                {
                    OfferedSalary = canSeeSalary ? row.OfferedSalary : null,
                    Opinions = [.. byCandidate[row.CandidateId]],
                }),
            ]);
    }
}

/// <summary>Lists the days offered in the day picker.</summary>
public sealed class ListCouncilDaysQueryHandler
{
    private const int DaysBack = 60;

    private readonly IRecruitingReadModel _readModel;
    private readonly IClock _clock;

    public ListCouncilDaysQueryHandler(IRecruitingReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<CouncilDayDto>>> HandleAsync(
        ListCouncilDaysQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var days = await _readModel
            .ListCouncilDaysAsync(_clock.Today.AddDays(-DaysBack), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(days);
    }
}

/// <summary>Saves attendance and follow-up dates, opening the sitting on first save.</summary>
public sealed class SaveCouncilMeetingCommandHandler
{
    private readonly ICouncilMeetingRepository _meetings;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCouncilMeetingCommandHandler(ICouncilMeetingRepository meetings, IUnitOfWork unitOfWork)
    {
        _meetings = meetings;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        SaveCouncilMeetingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var members = CouncilJson.Clean(command.Members);
        if (members is null)
        {
            return RecruitingErrors.CouncilMembersInvalid;
        }

        var followUp = CouncilJson.Write(command.FollowUp ?? new CouncilFollowUpDto(null, null, null, null, null));
        var meeting = await _meetings.FindByDateAsync(command.Date, cancellationToken).ConfigureAwait(false);

        if (meeting is null)
        {
            var number = await _meetings.MaxNumberAsync(cancellationToken).ConfigureAwait(false);
            _meetings.Add(CouncilMeeting.Create(command.Date, number + 1, CouncilJson.Write(members), followUp));
        }
        else
        {
            meeting.Update(CouncilJson.Write(members), followUp);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>
/// Saves one candidate's decision and writes it to the candidate's «Кенгаш» stage entry for that day.
/// </summary>
/// <remarks>
/// The candidate's status isn't changed: refusing or hiring stays HR's own action, as it does for
/// every other stage. A candidate who has left the funnel keeps the row on the minutes; a stage
/// can't be recorded for them, though an entry they already have is corrected.
/// </remarks>
public sealed class SaveCouncilDecisionCommandHandler
{
    private const string CouncilStageName = "Кенгаш";

    private readonly ICouncilMeetingRepository _meetings;
    private readonly ICouncilDecisionRepository _decisions;
    private readonly ICandidateRepository _candidates;
    private readonly IPipelineStageRepository _stages;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCouncilDecisionCommandHandler(
        ICouncilMeetingRepository meetings,
        ICouncilDecisionRepository decisions,
        ICandidateRepository candidates,
        IPipelineStageRepository stages,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _meetings = meetings;
        _decisions = decisions;
        _candidates = candidates;
        _stages = stages;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        SaveCouncilDecisionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Decision is not { } decisionKind || !Enum.IsDefined(decisionKind))
        {
            return RecruitingErrors.CouncilDecisionRequired;
        }

        var condition = Clean(command.Condition);
        var conclusion = Clean(command.Conclusion);
        if (condition?.Length > CouncilDecision.MaxConditionLength
            || conclusion?.Length > CouncilDecision.MaxConclusionLength
            || command.OfferedSalary < 0
            || (command.Practical is { } practical && !Enum.IsDefined(practical)))
        {
            return RecruitingErrors.CouncilDecisionInvalid;
        }

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var stage = await _stages.FindByNameAsync(CouncilStageName, cancellationToken).ConfigureAwait(false);
        if (stage is null)
        {
            return RecruitingErrors.StageNotFound;
        }

        var meeting = await _meetings.FindByDateAsync(command.Date, cancellationToken).ConfigureAwait(false);
        if (meeting is null)
        {
            var previous = await _meetings.FindLatestBeforeAsync(command.Date, cancellationToken).ConfigureAwait(false);
            var members = CouncilJson.ReadMembers(previous?.Members).Select(member => member with { Present = false }).ToList();
            var number = await _meetings.MaxNumberAsync(cancellationToken).ConfigureAwait(false);
            meeting = CouncilMeeting.Create(
                command.Date,
                number + 1,
                CouncilJson.Write(members),
                CouncilJson.Write(new CouncilFollowUpDto(null, null, null, null, null)));
            _meetings.Add(meeting);
        }

        var decision = await _decisions
            .FindAsync(meeting.Id, command.CandidateId, cancellationToken)
            .ConfigureAwait(false);
        if (decision is null)
        {
            decision = CouncilDecision.Create(meeting.Id, command.CandidateId);
            _decisions.Add(decision);
        }

        var text = StageText(decisionKind, condition, conclusion);
        var entryId = decision.StageEntryId
            ?? candidate.StageEntries
                .Where(entry => entry.StageId == stage.Id && entry.Date == command.Date)
                .OrderByDescending(entry => entry.RecordedAt)
                .Select(entry => (Guid?)entry.Id)
                .FirstOrDefault();

        if (entryId is { } existing)
        {
            var edited = candidate.EditStageEntry(existing, command.Date, text);
            if (edited.IsFailure)
            {
                return edited.Error;
            }
        }
        else if (candidate.IsActive)
        {
            if (!stage.IsActive)
            {
                return RecruitingErrors.StageInactive;
            }

            var recorded = candidate.RecordStage(stage.Id, command.Date, text, _currentUser.UserId);
            if (recorded.IsFailure)
            {
                return recorded.Error;
            }

            entryId = recorded.Value.Id;
        }

        // Without salary.view the pay on the row stays as it was: the form never saw it.
        var salary = _currentUser.CanSeeSalary() ? command.OfferedSalary : decision.OfferedSalary;

        decision.Update(decisionKind, command.Practical, salary, command.StartDate, condition, conclusion, entryId);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string StageText(CouncilDecisionKind decision, string? condition, string? conclusion)
    {
        var label = decision switch
        {
            CouncilDecisionKind.Accept => "Qabul",
            CouncilDecisionKind.Conditional => "Shartli qabul — surishtiruvdan keyin",
            CouncilDecisionKind.Reserve => "Zaxira",
            _ => "Rad",
        };

        return string.Join(
            "\n",
            new[] { label, conclusion, condition is null ? null : "Shart: " + condition }.Where(part => part is not null));
    }
}
