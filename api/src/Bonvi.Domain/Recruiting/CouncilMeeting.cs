using Bonvi.Domain.Common;

namespace Bonvi.Domain.Recruiting;

/// <summary>What the hiring council decided about one candidate.</summary>
public enum CouncilDecisionKind
{
    Accept = 1,
    Conditional = 2,
    Reserve = 3,
    Reject = 4,
}

/// <summary>Outcome of the practical test, where one was held.</summary>
public enum CouncilPracticalResult
{
    Passed = 1,
    Failed = 2,
    NotRequired = 3,
}

/// <summary>
/// One sitting of the hiring council: the minutes' number, who attended and what HR did afterwards.
/// One per calendar day. Members and follow-up dates are JSON because they are only ever read and
/// written whole, and printed.
/// </summary>
public sealed class CouncilMeeting : AggregateRoot, IAuditable
{
    private CouncilMeeting(Guid id, DateOnly meetingDate, int number, string members, string followUp)
        : base(id)
    {
        MeetingDate = meetingDate;
        Number = number;
        Members = members;
        FollowUp = followUp;
    }

    private CouncilMeeting() { }

    public DateOnly MeetingDate { get; private set; }

    /// <summary>The number printed on the minutes, assigned when the sitting is first saved.</summary>
    public int Number { get; private set; }

    /// <summary>JSON array of members with their attendance.</summary>
    public string Members { get; private set; } = "[]";

    /// <summary>JSON object with the dates of the follow-up steps.</summary>
    public string FollowUp { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CouncilMeeting Create(DateOnly meetingDate, int number, string members, string followUp)
        => new(NewId(), meetingDate, number, members, followUp);

    public void Update(string members, string followUp)
    {
        Members = members;
        FollowUp = followUp;
    }
}

/// <summary>One candidate's row in the council's minutes.</summary>
public sealed class CouncilDecision : AggregateRoot, IAuditable
{
    public const int MaxConditionLength = 1000;
    public const int MaxConclusionLength = 4000;

    private CouncilDecision(Guid id, Guid meetingId, Guid candidateId)
        : base(id)
    {
        MeetingId = meetingId;
        CandidateId = candidateId;
    }

    private CouncilDecision() { }

    public Guid MeetingId { get; private set; }

    public Guid CandidateId { get; private set; }

    public CouncilDecisionKind Decision { get; private set; }

    public CouncilPracticalResult? Practical { get; private set; }

    /// <summary>The pay offered. Shown only to holders of <c>salary.view</c>.</summary>
    public decimal? OfferedSalary { get; private set; }

    public DateOnly? StartDate { get; private set; }

    /// <summary>Special condition: probation, mentor, liability contract.</summary>
    public string? Condition { get; private set; }

    /// <summary>The council's words about this candidate.</summary>
    public string? Conclusion { get; private set; }

    /// <summary>
    /// The candidate's «Кенгаш» stage entry carrying this decision, so the candidate's card shows
    /// it. Empty when the candidate had already left the funnel and had no entry for that day.
    /// </summary>
    public Guid? StageEntryId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CouncilDecision Create(Guid meetingId, Guid candidateId)
        => new(NewId(), meetingId, candidateId);

    public void Update(
        CouncilDecisionKind decision,
        CouncilPracticalResult? practical,
        decimal? offeredSalary,
        DateOnly? startDate,
        string? condition,
        string? conclusion,
        Guid? stageEntryId)
    {
        Decision = decision;
        Practical = practical;
        OfferedSalary = offeredSalary;
        StartDate = startDate;
        Condition = condition;
        Conclusion = conclusion;
        StageEntryId = stageEntryId;
    }
}
