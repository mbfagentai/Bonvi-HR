using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Council;

/// <summary>A member on the panel's roster.</summary>
public sealed record CouncilGroupMemberDto(
    Guid Id,
    string FullName,
    string? Phone,
    bool IsActive,
    bool Linked,
    DateTimeOffset? LinkedAt,
    string? TelegramName,
    string? TelegramUsername,
    bool NameConfirmed);

/// <summary>The panel's view: the bound group, the Mini App's name and the roster.</summary>
public sealed record CouncilGroupDto(
    bool GroupBound,
    string? GroupTitle,
    string? MiniAppName,
    string? BotUsername,
    IReadOnlyList<CouncilGroupMemberDto> Members);

/// <summary>The command to paste into the group.</summary>
public sealed record CouncilBindCodeDto(string Command, DateTimeOffset ExpiresAt);

/// <summary>One candidate on the group's message.</summary>
public sealed record CouncilDigestRowDto(Guid CandidateId, string Name, DateTimeOffset At);

/// <summary>A message the bot should post, or edit when <see cref="MessageId"/> is set.</summary>
public sealed record CouncilDigestDto(
    DateOnly Date,
    bool IsToday,
    long? MessageId,
    string Signature,
    IReadOnlyList<CouncilDigestRowDto> Rows);

/// <summary>What the bot needs each round.</summary>
/// <param name="ChatId">The bound group.</param>
/// <param name="MiniAppName">The Mini App's short name.</param>
/// <param name="BotUsername">The bot's username.</param>
/// <param name="Digest">The message due.</param>
/// <param name="BindOpen">A binding code is live, so a new group may be waiting for it and the bot must not leave.</param>
public sealed record CouncilBotStateDto(long? ChatId, string? MiniAppName, string? BotUsername, CouncilDigestDto? Digest, bool BindOpen);

/// <summary>Outcome of a member sharing their contact.</summary>
public sealed record CouncilLinkResultDto(bool Linked, string? FullName);

/// <summary>A candidate in the Mini App's list.</summary>
public sealed record CouncilAppCandidateRowDto(Guid Id, string FullName, string Position, DateTimeOffset InterviewAt, bool HasMyOpinion);

/// <summary>One of the member's own earlier conclusions.</summary>
public sealed record CouncilMyOpinionDto(CouncilDecisionKind Decision, string? Comment, DateTimeOffset At);

/// <summary>A questionnaire form the Mini App can show.</summary>
public sealed record CouncilAppFormDto(Guid Id, int Number);

/// <summary>What a member reads about a candidate. Stage notes and HR's files are not part of it.</summary>
public sealed record CouncilAppCandidateDto(
    Guid Id,
    string FullName,
    string Position,
    DateOnly? BirthDate,
    string? Phone,
    string? Address,
    string? Speciality,
    DateTimeOffset InterviewAt,
    bool HasPhoto,
    CandidateSource Source,
    CandidateQuestionnaireDto? Questionnaire,
    IReadOnlyList<CouncilAppFormDto> Forms,
    IReadOnlyList<CouncilMyOpinionDto> MyOpinions);

/// <summary>A member's conclusion on the minutes page, one block per member.</summary>
public sealed record CouncilOpinionDto(
    Guid MemberId,
    string MemberName,
    CouncilDecisionKind Decision,
    string? Comment,
    DateTimeOffset At);

/// <summary>Who opened the Mini App.</summary>
public enum CouncilAppAccess
{
    /// <summary>The launch data is not genuine.</summary>
    Invalid = 1,

    /// <summary>A genuine Telegram user who is not in the council's group, or was turned off.</summary>
    Unknown = 2,

    /// <summary>An active member in the group.</summary>
    Member = 3,

    /// <summary>Telegram could not say, and this person has no row yet.</summary>
    Unavailable = 4,
}

/// <summary>What Telegram says about one person in the council's group.</summary>
public enum TelegramGroupStatus
{
    Member = 1,
    NotMember = 2,

    /// <summary>Telegram did not answer or refused the bot.</summary>
    Unavailable = 3,
}

/// <summary>Asks Telegram whether a person is in a group.</summary>
public interface ITelegramGroupMembership
{
    Task<TelegramGroupStatus> CheckAsync(string botToken, long chatId, long userId, CancellationToken cancellationToken = default);
}

/// <summary>The result of checking the launch data.</summary>
public sealed record CouncilAppIdentity(CouncilAppAccess Access, CouncilMember? Member);

/// <summary>Turns the Mini App's signed launch data into a council member.</summary>
public sealed class CouncilMemberIdentifier
{
    private readonly SystemSettingsReader _settings;
    private readonly ICouncilGroupRepository _group;
    private readonly ITelegramGroupMembership _membership;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CouncilMemberIdentifier(
        SystemSettingsReader settings,
        ICouncilGroupRepository group,
        ITelegramGroupMembership membership,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _settings = settings;
        _group = group;
        _membership = membership;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <param name="initData">The launch data.</param>
    /// <param name="register">Give a group member their row. Only the first screen does: it runs once and alone.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<CouncilAppIdentity> IdentifyAsync(string? initData, bool register = false, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var token = settings.Secret(SystemSettingKeys.BotToken);

        if (TelegramInitData.ReadUser(initData, token, _clock.UtcNow) is not { } user)
        {
            return new CouncilAppIdentity(CouncilAppAccess.Invalid, null);
        }

        var member = await _group.FindMemberByTelegramAsync(user.Id, cancellationToken).ConfigureAwait(false);

        if (member is { IsActive: false } || CouncilSettings.ChatId(settings) is not { } chat)
        {
            return new CouncilAppIdentity(CouncilAppAccess.Unknown, null);
        }

        var status = await _membership.CheckAsync(token!, chat, user.Id, cancellationToken).ConfigureAwait(false);

        switch (status)
        {
            case TelegramGroupStatus.NotMember:
                return new CouncilAppIdentity(CouncilAppAccess.Unknown, null);

            // A Telegram hiccup must not stop a sitting for people already known.
            case TelegramGroupStatus.Unavailable:
                return member is null
                    ? new CouncilAppIdentity(CouncilAppAccess.Unavailable, null)
                    : new CouncilAppIdentity(CouncilAppAccess.Member, member);
        }

        if (member is null)
        {
            if (!register)
            {
                return new CouncilAppIdentity(CouncilAppAccess.Unknown, null);
            }

            member = CouncilMember.FromTelegram(user.Id, user.FullName, user.Username, _clock.UtcNow);
            _group.AddMember(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (member.RefreshProfile(user.FullName, user.Username))
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new CouncilAppIdentity(CouncilAppAccess.Member, member);
    }
}

/// <summary>Reads and writes the group's server-side settings.</summary>
internal static class CouncilSettings
{
    public static async Task<Result> WriteAsync(
        ISystemSettingRepository settings,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        var existing = await settings.FindAsync(key, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing.SetServerValue(value);
        }

        var created = SystemSetting.CreateServerValue(key, value);
        if (created.IsFailure)
        {
            return created.Error;
        }

        settings.Add(created.Value);

        return Result.Success();
    }

    public static string? OrNull(string value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>The code and its expiry, when one was issued.</summary>
    public static (string Code, long Expires)? BindCode(SystemSettings settings)
    {
        var stored = settings.Hidden(SystemSettingKeys.BotCouncilBindCode).Split('|');

        return stored.Length == 2 && long.TryParse(stored[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            ? (stored[0], expires)
            : null;
    }

    public static long? ChatId(SystemSettings settings)
        => long.TryParse(settings.Hidden(SystemSettingKeys.BotCouncilChatId), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}

public sealed record GetCouncilGroupQuery;

public sealed class GetCouncilGroupQueryHandler
{
    private readonly ICouncilGroupRepository _group;
    private readonly SystemSettingsReader _settings;

    public GetCouncilGroupQueryHandler(ICouncilGroupRepository group, SystemSettingsReader settings)
    {
        _group = group;
        _settings = settings;
    }

    public async Task<Result<CouncilGroupDto>> HandleAsync(GetCouncilGroupQuery query, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var members = await _group.ListMembersAsync(cancellationToken).ConfigureAwait(false);

        return new CouncilGroupDto(
            CouncilSettings.ChatId(settings) is not null,
            CouncilSettings.OrNull(settings.Hidden(SystemSettingKeys.BotCouncilChatTitle)),
            CouncilSettings.OrNull(settings.Hidden(SystemSettingKeys.BotCouncilMiniAppName)),
            CouncilSettings.OrNull(settings.Hidden(SystemSettingKeys.BotUsername)),
            [.. members.Select(member => new CouncilGroupMemberDto(
                member.Id,
                member.FullName,
                member.Phone?.Value,
                member.IsActive,
                member.TelegramUserId is not null,
                member.LinkedAt,
                member.TelegramName,
                member.TelegramUsername,
                member.NameConfirmed))]);
    }
}

/// <summary>Adds a member, or changes one when <see cref="Id"/> is set.</summary>
public sealed record SaveCouncilMemberCommand(Guid? Id, string? FullName, string? Phone, bool IsActive);

public sealed class SaveCouncilMemberCommandHandler
{
    private readonly ICouncilGroupRepository _group;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCouncilMemberCommandHandler(ICouncilGroupRepository group, IUnitOfWork unitOfWork)
    {
        _group = group;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(SaveCouncilMemberCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = command.FullName?.Trim();

        // A member who came from the group has no phone and needs none; a new row by hand does.
        var noPhone = string.IsNullOrWhiteSpace(command.Phone) && command.Id is not null;
        var phone = noPhone ? null : PhoneNumber.Create(command.Phone);

        if (string.IsNullOrEmpty(name) || name.Length > CouncilMember.MaxNameLength || phone is { IsFailure: true })
        {
            return RecruitingErrors.CouncilMemberInvalid;
        }

        var holder = phone is null ? null : await _group.FindMemberByPhoneAsync(phone.Value, cancellationToken).ConfigureAwait(false);

        if (holder is not null && holder.Id != command.Id)
        {
            return RecruitingErrors.CouncilMemberPhoneTaken;
        }

        CouncilMember member;
        if (command.Id is { } id)
        {
            var found = await _group.FindMemberAsync(id, cancellationToken).ConfigureAwait(false);
            if (found is null)
            {
                return RecruitingErrors.CouncilMemberNotFound;
            }

            member = found;
            member.Update(name, phone?.Value, command.IsActive);
        }
        else
        {
            member = CouncilMember.Create(name, phone!.Value);
            _group.AddMember(member);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return member.Id;
    }
}

public sealed record UnlinkCouncilMemberCommand(Guid Id);

public sealed class UnlinkCouncilMemberCommandHandler
{
    private readonly ICouncilGroupRepository _group;
    private readonly IUnitOfWork _unitOfWork;

    public UnlinkCouncilMemberCommandHandler(ICouncilGroupRepository group, IUnitOfWork unitOfWork)
    {
        _group = group;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(UnlinkCouncilMemberCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await _group.FindMemberAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return RecruitingErrors.CouncilMemberNotFound;
        }

        member.Unlink();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

public sealed record IssueCouncilBindCodeCommand;

/// <summary>Issues a single-use code; only a settings holder sees it, which is what authorises the group.</summary>
public sealed class IssueCouncilBindCodeCommandHandler
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly ISystemSettingRepository _settings;
    private readonly SystemSettingsReader _reader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public IssueCouncilBindCodeCommandHandler(
        ISystemSettingRepository settings,
        SystemSettingsReader reader,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _settings = settings;
        _reader = reader;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CouncilBindCodeDto>> HandleAsync(IssueCouncilBindCodeCommand command, CancellationToken cancellationToken = default)
    {
        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var username = settings.Hidden(SystemSettingKeys.BotUsername);

        if (string.IsNullOrEmpty(username))
        {
            return RecruitingErrors.BotUsernameUnknown;
        }

        var code = RandomNumberGenerator.GetInt32(10_000_000, 100_000_000).ToString(CultureInfo.InvariantCulture);
        var expires = _clock.UtcNow.Add(Lifetime);
        var stored = code + "|" + expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var written = await CouncilSettings.WriteAsync(_settings, SystemSettingKeys.BotCouncilBindCode, stored, cancellationToken).ConfigureAwait(false);
        if (written.IsFailure)
        {
            return written.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // With privacy mode on, a command reaches the bot reliably only when it names the bot.
        return new CouncilBindCodeDto($"/kengash_ulash@{username} {code}", expires);
    }
}

public sealed record SaveCouncilMiniAppCommand(string? Name);

public sealed partial class SaveCouncilMiniAppCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCouncilMiniAppCommandHandler(ISystemSettingRepository settings, IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(SaveCouncilMiniAppCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = command.Name?.Trim() ?? string.Empty;

        if (name.Length > 0 && !MiniAppName().IsMatch(name))
        {
            return RecruitingErrors.CouncilMiniAppNameInvalid;
        }

        var written = await CouncilSettings.WriteAsync(_settings, SystemSettingKeys.BotCouncilMiniAppName, name, cancellationToken).ConfigureAwait(false);
        if (written.IsFailure)
        {
            return written;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    [GeneratedRegex("^[A-Za-z0-9_]{3,30}$")]
    private static partial Regex MiniAppName();
}

/// <summary>The bot saw the binding command in a group.</summary>
public sealed record BindCouncilGroupCommand(string? Code, long ChatId, string? Title);

public sealed class BindCouncilGroupCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly SystemSettingsReader _reader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public BindCouncilGroupCommandHandler(
        ISystemSettingRepository settings,
        SystemSettingsReader reader,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _settings = settings;
        _reader = reader;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(BindCouncilGroupCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (CouncilSettings.BindCode(settings) is not { } stored
            || string.IsNullOrEmpty(command.Code)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored.Code), Encoding.UTF8.GetBytes(command.Code))
            || _clock.UtcNow.ToUnixTimeSeconds() > stored.Expires
            || command.ChatId >= 0)
        {
            return RecruitingErrors.CouncilBindCodeInvalid;
        }

        var title = command.Title?.Trim() ?? string.Empty;
        (string Key, string Value)[] writes =
        [
            (SystemSettingKeys.BotCouncilChatId, command.ChatId.ToString(CultureInfo.InvariantCulture)),
            (SystemSettingKeys.BotCouncilChatTitle, title.Length > 200 ? title[..200] : title),
            (SystemSettingKeys.BotCouncilBindCode, string.Empty),
        ];

        foreach (var (key, value) in writes)
        {
            var written = await CouncilSettings.WriteAsync(_settings, key, value, cancellationToken).ConfigureAwait(false);
            if (written.IsFailure)
            {
                return written;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>A group became a supergroup and has a new id.</summary>
public sealed record MigrateCouncilGroupCommand(long FromChatId, long ToChatId);

public sealed class MigrateCouncilGroupCommandHandler
{
    private readonly ISystemSettingRepository _settings;
    private readonly SystemSettingsReader _reader;
    private readonly IUnitOfWork _unitOfWork;

    public MigrateCouncilGroupCommandHandler(ISystemSettingRepository settings, SystemSettingsReader reader, IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _reader = reader;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(MigrateCouncilGroupCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (CouncilSettings.ChatId(settings) != command.FromChatId || command.ToChatId >= 0)
        {
            return Result.Success();
        }

        var written = await CouncilSettings
            .WriteAsync(_settings, SystemSettingKeys.BotCouncilChatId, command.ToChatId.ToString(CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            return written;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>A member shared their own contact with the bot.</summary>
public sealed record LinkCouncilMemberCommand(long TelegramUserId, string? Phone);

public sealed class LinkCouncilMemberCommandHandler
{
    private readonly ICouncilGroupRepository _group;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LinkCouncilMemberCommandHandler(ICouncilGroupRepository group, IUnitOfWork unitOfWork, IClock clock)
    {
        _group = group;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CouncilLinkResultDto>> HandleAsync(LinkCouncilMemberCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var phone = PhoneNumber.Create(command.Phone);
        if (phone.IsFailure || command.TelegramUserId <= 0)
        {
            return new CouncilLinkResultDto(false, null);
        }

        var member = await _group.FindMemberByPhoneAsync(phone.Value, cancellationToken).ConfigureAwait(false);
        if (member is not { IsActive: true })
        {
            return new CouncilLinkResultDto(false, null);
        }

        // One Telegram account speaks for one member, so an earlier holder loses it first.
        var earlier = await _group.FindMemberByTelegramAsync(command.TelegramUserId, cancellationToken).ConfigureAwait(false);
        if (earlier is not null && earlier.Id != member.Id)
        {
            earlier.Unlink();
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        member.Link(command.TelegramUserId, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CouncilLinkResultDto(true, member.FullName);
    }
}

public sealed record GetCouncilBotStateQuery;

/// <summary>Tells the bot the group and, when due, the message to post or edit.</summary>
public sealed class GetCouncilBotStateQueryHandler
{
    private static readonly TimeOnly PostFrom = new(10, 0);
    private static readonly TimeOnly PostUntil = new(20, 0);

    private readonly SystemSettingsReader _settings;
    private readonly ICouncilGroupRepository _group;
    private readonly IRecruitingReadModel _readModel;
    private readonly IClock _clock;

    public GetCouncilBotStateQueryHandler(
        SystemSettingsReader settings,
        ICouncilGroupRepository group,
        IRecruitingReadModel readModel,
        IClock clock)
    {
        _settings = settings;
        _group = group;
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<CouncilBotStateDto>> HandleAsync(GetCouncilBotStateQuery query, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var chatId = CouncilSettings.ChatId(settings);

        var miniApp = CouncilSettings.OrNull(settings.Hidden(SystemSettingKeys.BotCouncilMiniAppName));
        var digest = chatId is { } chat ? await FindDueAsync(chat, miniApp, cancellationToken).ConfigureAwait(false) : null;

        return new CouncilBotStateDto(
            chatId,
            miniApp,
            CouncilSettings.OrNull(settings.Hidden(SystemSettingKeys.BotUsername)),
            digest,
            CouncilSettings.BindCode(settings) is { } code && _clock.UtcNow.ToUnixTimeSeconds() <= code.Expires);
    }

    private async Task<CouncilDigestDto?> FindDueAsync(long chatId, string? miniApp, CancellationToken cancellationToken)
    {
        var local = _clock.UtcNow.ToOffset(BonviTime.TashkentOffset);
        var today = DateOnly.FromDateTime(local.DateTime);
        var time = TimeOnly.FromDateTime(local.DateTime);
        var mayPost = time >= PostFrom && time < PostUntil;

        foreach (var day in new[] { today, today.AddDays(1) })
        {
            var rows = await CouncilInvitations.ListOpenAsync(_readModel, day, cancellationToken).ConfigureAwait(false);
            var existing = await _group.FindDigestAsync(day, cancellationToken).ConfigureAwait(false);
            var sameChat = existing?.ChatId == chatId;
            var signature = Signature(rows, miniApp);

            var isDue = sameChat
                ? existing!.Signature != signature
                : mayPost && rows.Count > 0;

            if (isDue)
            {
                return new CouncilDigestDto(day, day == today, sameChat ? existing!.MessageId : null, signature, rows);
            }
        }

        return null;
    }

    /// <summary>
    /// By the minute, as the buttons show: Telegram refuses an edit that changes nothing visible. The Mini App's
    /// name is in it because the buttons link there: a new name must reach a message already posted.
    /// </summary>
    private static string Signature(IReadOnlyList<CouncilDigestRowDto> rows, string? miniApp)
    {
        var text = (miniApp ?? string.Empty) + '\n' + string.Join('\n', rows.Select(row => row.CandidateId.ToString("N") + (row.At.ToUnixTimeSeconds() / 60).ToString(CultureInfo.InvariantCulture)));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..CouncilDigest.MaxSignatureLength];
    }
}

/// <summary>The people invited to a council day.</summary>
internal static class CouncilInvitations
{
    public static async Task<IReadOnlyList<CouncilDigestRowDto>> ListOpenAsync(
        IRecruitingReadModel readModel,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();
        var rows = await readModel.ListCouncilInvitedAsync(from, from.AddDays(1), null, cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new CouncilDigestRowDto(row.Id, row.ShortName, row.InterviewAt))];
    }
}

/// <summary>The bot posted or edited a message.</summary>
public sealed record MarkCouncilDigestSentCommand(DateOnly Date, long ChatId, long MessageId, string? Signature);

public sealed class MarkCouncilDigestSentCommandHandler
{
    private readonly ICouncilGroupRepository _group;
    private readonly IUnitOfWork _unitOfWork;

    public MarkCouncilDigestSentCommandHandler(ICouncilGroupRepository group, IUnitOfWork unitOfWork)
    {
        _group = group;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(MarkCouncilDigestSentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var signature = command.Signature ?? string.Empty;
        var digest = await _group.FindDigestAsync(command.Date, cancellationToken).ConfigureAwait(false);

        if (digest is null)
        {
            _group.AddDigest(CouncilDigest.Create(command.Date, command.ChatId, command.MessageId, signature));
        }
        else
        {
            digest.Update(command.ChatId, command.MessageId, signature);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Which invitations a member can reach.</summary>
internal static class CouncilAppWindow
{
    public const int ListDaysBack = 14;
    public const int AccessDaysBack = 60;

    public static bool IsReachable(DateTimeOffset? interviewAt, DateTimeOffset now)
        => interviewAt is { } at && at >= now.AddDays(-AccessDaysBack);
}

public sealed record ListCouncilAppCandidatesQuery(Guid MemberId);

public sealed class ListCouncilAppCandidatesQueryHandler
{
    private readonly IRecruitingReadModel _readModel;
    private readonly IClock _clock;

    public ListCouncilAppCandidatesQueryHandler(IRecruitingReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<CouncilAppCandidateRowDto>>> HandleAsync(ListCouncilAppCandidatesQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _readModel
            .ListCouncilInvitedAsync(_clock.UtcNow.AddDays(-CouncilAppWindow.ListDaysBack), null, query.MemberId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<CouncilAppCandidateRowDto> items =
        [
            .. rows.Select(row => new CouncilAppCandidateRowDto(row.Id, row.FullName, row.Position, row.InterviewAt, row.HasMyOpinion)),
        ];

        return Result.Success(items);
    }
}

public sealed record GetCouncilAppCandidateQuery(Guid CandidateId, Guid MemberId);

public sealed class GetCouncilAppCandidateQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly ICandidateFormRepository _forms;
    private readonly IVacancyRepository _vacancies;
    private readonly IRecruitingReadModel _readModel;
    private readonly IClock _clock;

    public GetCouncilAppCandidateQueryHandler(
        ICandidateRepository candidates,
        ICandidateFormRepository forms,
        IVacancyRepository vacancies,
        IRecruitingReadModel readModel,
        IClock clock)
    {
        _candidates = candidates;
        _forms = forms;
        _vacancies = vacancies;
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<CouncilAppCandidateDto>> HandleAsync(GetCouncilAppCandidateQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidate = await _candidates.GetWithStagesAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);

        if (candidate is null || !CouncilAppWindow.IsReachable(candidate.InterviewAt, _clock.UtcNow))
        {
            return RecruitingErrors.CouncilCandidateNotFound;
        }

        var vacancy = candidate.VacancyId is { } vacancyId
            ? await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false)
            : null;
        var forms = await _forms.ListByCandidateAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
        var opinions = await _readModel.ListMyCouncilOpinionsAsync(candidate.Id, query.MemberId, cancellationToken).ConfigureAwait(false);

        return new CouncilAppCandidateDto(
            candidate.Id,
            candidate.FullName.ToString(),
            vacancy?.Title ?? string.Empty,
            candidate.BirthDate,
            candidate.Phone?.Value,
            candidate.Address,
            candidate.Speciality,
            candidate.InterviewAt!.Value,
            candidate.PhotoKey is not null,
            candidate.Source,
            RecruitingMapper.ToDto(candidate.Questionnaire),
            [.. forms.Select(form => new CouncilAppFormDto(form.Id, form.Number))],
            opinions);
    }
}

/// <summary>A member may read this candidate's photo and form, the same rule as the card.</summary>
public sealed record CheckCouncilAppCandidateQuery(Guid CandidateId);

/// <summary>A member writes a conclusion. It is always a new row (SPEC-kengash-guruhi §6).</summary>
public sealed record SaveCouncilOpinionCommand(Guid CandidateId, Guid MemberId, CouncilDecisionKind? Decision, string? Comment);

public sealed class SaveCouncilOpinionCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly ICouncilGroupRepository _group;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SaveCouncilOpinionCommandHandler(
        ICandidateRepository candidates,
        ICouncilGroupRepository group,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _candidates = candidates;
        _group = group;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>The check behind the member's photo and form routes.</summary>
    public async Task<Result> CheckReachableAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await _candidates.GetByIdAsync(candidateId, cancellationToken).ConfigureAwait(false);

        return candidate is not null && CouncilAppWindow.IsReachable(candidate.InterviewAt, _clock.UtcNow)
            ? Result.Success()
            : RecruitingErrors.CouncilCandidateNotFound;
    }

    public async Task<Result> HandleAsync(SaveCouncilOpinionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();

        if (command.Decision is not { } decision
            || !Enum.IsDefined(decision)
            || comment?.Length > CouncilOpinion.MaxCommentLength
            || (decision == CouncilDecisionKind.Conditional && comment is null))
        {
            return RecruitingErrors.CouncilOpinionInvalid;
        }

        var candidate = await _candidates.GetByIdAsync(command.CandidateId, cancellationToken).ConfigureAwait(false);

        if (candidate is null || !CouncilAppWindow.IsReachable(candidate.InterviewAt, _clock.UtcNow))
        {
            return RecruitingErrors.CouncilCandidateNotFound;
        }

        var day = DateOnly.FromDateTime(candidate.InterviewAt!.Value.ToOffset(BonviTime.TashkentOffset).DateTime);

        _group.AddOpinion(CouncilOpinion.Create(candidate.Id, command.MemberId, day, decision, comment));
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
