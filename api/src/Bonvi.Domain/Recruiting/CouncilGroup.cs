using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Recruiting;

/// <summary>A person allowed to give conclusions from the Mini App. Group membership grants nothing.</summary>
public sealed class CouncilMember : AggregateRoot, IAuditable
{
    public const int MaxNameLength = 200;

    private CouncilMember(Guid id, string fullName, PhoneNumber phone)
        : base(id)
    {
        FullName = fullName;
        Phone = phone;
        IsActive = true;
    }

    private CouncilMember() { }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Matched against the contact a member shares with the bot.</summary>
    public PhoneNumber Phone { get; private set; } = null!;

    /// <summary>Set once the member shared their contact; identifies them in the Mini App from then on.</summary>
    public long? TelegramUserId { get; private set; }

    public DateTimeOffset? LinkedAt { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CouncilMember Create(string fullName, PhoneNumber phone) => new(NewId(), fullName, phone);

    /// <summary>Another number is another person's Telegram, so a changed phone drops the link.</summary>
    public void Update(string fullName, PhoneNumber phone, bool isActive)
    {
        FullName = fullName;
        IsActive = isActive;

        if (Phone != phone)
        {
            Phone = phone;
            Unlink();
        }
    }

    public void Link(long telegramUserId, DateTimeOffset at)
    {
        TelegramUserId = telegramUserId;
        LinkedAt = at;
    }

    public void Unlink()
    {
        TelegramUserId = null;
        LinkedAt = null;
    }
}

/// <summary>
/// One conclusion by one member on one candidate. Never changed: writing again adds a row, so the
/// earlier words stay beside the new ones. The final decision is <see cref="CouncilDecision"/>.
/// </summary>
public sealed class CouncilOpinion : AggregateRoot, IAuditable
{
    public const int MaxCommentLength = 4000;

    private CouncilOpinion(Guid id, Guid candidateId, Guid memberId, DateOnly meetingDate, CouncilDecisionKind decision, string? comment)
        : base(id)
    {
        CandidateId = candidateId;
        MemberId = memberId;
        MeetingDate = meetingDate;
        Decision = decision;
        Comment = comment;
    }

    private CouncilOpinion() { }

    public Guid CandidateId { get; private set; }

    public Guid MemberId { get; private set; }

    /// <summary>The invitation's day when written, which is the sitting the conclusion belongs to.</summary>
    public DateOnly MeetingDate { get; private set; }

    public CouncilDecisionKind Decision { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CouncilOpinion Create(Guid candidateId, Guid memberId, DateOnly meetingDate, CouncilDecisionKind decision, string? comment)
        => new(NewId(), candidateId, memberId, meetingDate, decision, comment);
}

/// <summary>The group's message for one council day, kept so later changes edit it instead of posting again.</summary>
public sealed class CouncilDigest : AggregateRoot, IAuditable
{
    public const int MaxSignatureLength = 64;

    private CouncilDigest(Guid id, DateOnly meetingDate, long chatId, long messageId, string signature)
        : base(id)
    {
        MeetingDate = meetingDate;
        ChatId = chatId;
        MessageId = messageId;
        Signature = signature;
    }

    private CouncilDigest() { }

    public DateOnly MeetingDate { get; private set; }

    public long ChatId { get; private set; }

    public long MessageId { get; private set; }

    /// <summary>Hash of the list the message shows: a different list means the message needs editing.</summary>
    public string Signature { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CouncilDigest Create(DateOnly meetingDate, long chatId, long messageId, string signature)
        => new(NewId(), meetingDate, chatId, messageId, signature);

    public void Update(long chatId, long messageId, string signature)
    {
        ChatId = chatId;
        MessageId = messageId;
        Signature = signature;
    }
}
