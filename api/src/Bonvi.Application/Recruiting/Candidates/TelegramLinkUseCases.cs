using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Links to the bot that give a candidate a Telegram chat.
/// </summary>
/// <remarks>
/// A Telegram bot cannot write to someone who never opened it; neither a phone number nor a
/// username is an address (of 84 invited by 2026-11-05, 31 got nothing for this reason). The chat
/// has to come from the candidate, so the online form ends with this link and HR can hand it out.
/// </remarks>
public static partial class TelegramLinks
{
    /// <summary>A new one-time token. Hex fits Telegram's start payload alphabet.</summary>
    public static string NewToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower(CultureInfo.InvariantCulture);

    /// <summary>The link, or <c>null</c> until the bot has reported its username.</summary>
    public static string? Url(string? botUsername, string? token)
        => string.IsNullOrWhiteSpace(botUsername) || string.IsNullOrEmpty(token)
            ? null
            : $"https://t.me/{botUsername}?start={token}";

    /// <summary>Whether this is a syntactically valid Telegram username (bots included).</summary>
    public static bool IsUsername(string? value)
        => value is not null && UsernamePattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9_]{5,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}

/// <summary>The bot received <c>/start token</c>.</summary>
public sealed record LinkCandidateTelegramCommand(string? Token, long ChatId);

/// <summary>Result of a token link; the language lets the bot answer the way the candidate wrote.</summary>
public sealed record TelegramLinkResultDto(bool Linked, string? Language);

public sealed class LinkCandidateTelegramCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IUnitOfWork _unitOfWork;

    public LinkCandidateTelegramCommandHandler(ICandidateRepository candidates, IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TelegramLinkResultDto>> HandleAsync(
        LinkCandidateTelegramCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = string.IsNullOrWhiteSpace(command.Token)
            ? null
            : await _candidates.FindByTelegramLinkTokenAsync(command.Token, cancellationToken).ConfigureAwait(false);

        if (candidate is null)
        {
            // An old or foreign link: not an error, the bot simply starts as usual.
            return new TelegramLinkResultDto(false, null);
        }

        candidate.SetTelegram(command.ChatId, null);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TelegramLinkResultDto(true, candidate.TelegramLanguage);
    }
}

/// <summary>
/// Someone opened the bot on their own: their Telegram username, or the phone Telegram verified,
/// may belong to a candidate who applied elsewhere.
/// </summary>
public sealed record MatchCandidateTelegramCommand(long ChatId, string? Username, string? Phone);

public sealed class MatchCandidateTelegramCommandHandler
{
    /// <summary>One person rarely holds more than a few open applications.</summary>
    private const int MaxMatches = 5;

    private readonly ICandidateRepository _candidates;
    private readonly IUnitOfWork _unitOfWork;

    public MatchCandidateTelegramCommandHandler(ICandidateRepository candidates, IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> HandleAsync(
        MatchCandidateTelegramCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var username = TelegramLinks.IsUsername(command.Username) ? command.Username : null;
        var digits = command.Phone is null ? null : new string([.. command.Phone.Where(char.IsAsciiDigit)]);

        var matches = await _candidates
            .ListTelegramLinkableAsync(digits, username, MaxMatches, cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in matches)
        {
            candidate.SetTelegram(command.ChatId, null);
        }

        if (matches.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return matches.Count;
    }
}

/// <summary>HR asks for the candidate's bot link, to pass on by hand.</summary>
public sealed record IssueCandidateTelegramLinkCommand(Guid CandidateId);

/// <summary>The bot link.</summary>
public sealed record CandidateTelegramLinkDto(string Url);

public sealed class IssueCandidateTelegramLinkCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly SystemSettingsReader _settings;
    private readonly IUnitOfWork _unitOfWork;

    public IssueCandidateTelegramLinkCommandHandler(
        ICandidateRepository candidates,
        SystemSettingsReader settings,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateTelegramLinkDto>> HandleAsync(
        IssueCandidateTelegramLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates.GetByIdAsync(command.CandidateId, cancellationToken).ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var botUsername = settings.Hidden(SystemSettingKeys.BotUsername);

        if (string.IsNullOrWhiteSpace(botUsername))
        {
            return RecruitingErrors.BotUsernameUnknown;
        }

        if (candidate.IssueTelegramLink(TelegramLinks.NewToken()) is not { } token)
        {
            return RecruitingErrors.TelegramAlreadyLinked;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CandidateTelegramLinkDto(TelegramLinks.Url(botUsername, token)!);
    }
}
