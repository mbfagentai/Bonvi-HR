using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Recruiting;

/// <summary>
/// A person who gives conclusions from the Mini App. Access comes from being in the council's Telegram
/// group: the row appears on the first open, and an admin writes the real name or turns it off.
/// </summary>
public sealed class CouncilMember : AggregateRoot, IAuditable
{
    public const int MaxNameLength = 200;

    public const int MaxTelegramNameLength = 200;

    private CouncilMember(Guid id, string fullName, PhoneNumber? phone)
        : base(id)
    {
        FullName = fullName;
        Phone = phone;
        IsActive = true;
    }

    private CouncilMember() { }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Only for a member added by hand; one who came from the group has none.</summary>
    public PhoneNumber? Phone { get; private set; }

    /// <summary>The name on the Telegram profile, shown to the admin so they know who this is.</summary>
    public string? TelegramName { get; private set; }

    /// <summary>The Telegram username without «@», when there is one.</summary>
    public string? TelegramUsername { get; private set; }

    /// <summary>An admin wrote or accepted <see cref="FullName"/>; until then it is just the Telegram name.</summary>
    public bool NameConfirmed { get; private set; }

    /// <summary>Set once the member shared their contact; identifies them in the Mini App from then on.</summary>
    public long? TelegramUserId { get; private set; }

    public DateTimeOffset? LinkedAt { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CouncilMember Create(string fullName, PhoneNumber phone)
        => new(NewId(), fullName, phone) { NameConfirmed = true };

    /// <summary>Someone from the council's group opened the Mini App for the first time.</summary>
    public static CouncilMember FromTelegram(long telegramUserId, string telegramName, string? username, DateTimeOffset at)
    {
        var name = Fit(telegramName);
        var member = new CouncilMember(NewId(), name, null) { TelegramName = name, TelegramUsername = Fit(username) };
        member.Link(telegramUserId, at);

        return member;
    }

    /// <summary>A profile can change; the name written by an admin stays.</summary>
    public bool RefreshProfile(string telegramName, string? username)
    {
        var name = Fit(telegramName);
        var user = Fit(username);

        if (TelegramName == name && TelegramUsername == user)
        {
            return false;
        }

        TelegramName = name;
        TelegramUsername = user;

        if (!NameConfirmed)
        {
            FullName = name;
        }

        return true;
    }

    /// <summary>
    /// An admin named this person. Another number is another person's Telegram, so a changed phone
    /// drops the link; no phone leaves the current one alone.
    /// </summary>
    public void Update(string fullName, PhoneNumber? phone, bool isActive)
    {
        FullName = fullName;
        IsActive = isActive;
        NameConfirmed = true;

        if (phone is not null && Phone != phone)
        {
            Phone = phone;
            Unlink();
        }
    }

    private static string Fit(string? value)
    {
        var text = value?.Trim() ?? string.Empty;

        return text.Length > MaxTelegramNameLength ? text[..MaxTelegramNameLength] : text;
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
