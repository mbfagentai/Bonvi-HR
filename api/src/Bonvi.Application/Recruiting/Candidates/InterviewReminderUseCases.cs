using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// When the day-before reminder of an interview may go out, in Tashkent time.
/// </summary>
/// <remarks>
/// The bot polls all day, so the hours are held here: nobody should get this at night, and a bot
/// that was down in the morning still catches up by evening. Owner's request, 2026-11-05.
/// </remarks>
public static class InterviewReminderWindow
{
    /// <summary>First hour of sending, Tashkent time.</summary>
    public const int FromHour = 10;

    /// <summary>The hour sending stops, Tashkent time.</summary>
    public const int ToHour = 20;

    /// <summary>
    /// The window for this moment: interviews falling on tomorrow, invited before today.
    /// <c>null</c> outside the sending hours.
    /// </summary>
    public static (DateTimeOffset InterviewFrom, DateTimeOffset InterviewTo, DateTimeOffset InvitedBefore)? At(
        DateTimeOffset now)
    {
        var local = now.ToTashkent();

        if (local.Hour is < FromHour or >= ToHour)
        {
            return null;
        }

        var today = new DateTimeOffset(local.Date, BonviTime.TashkentOffset);

        return (today.AddDays(1).ToUniversalTime(), today.AddDays(2).ToUniversalTime(), today.ToUniversalTime());
    }
}

/// <summary>Candidates awaiting the day-before reminder, for the bot.</summary>
public sealed record ListPendingInterviewRemindersQuery(int Limit = 20);

public sealed class ListPendingInterviewRemindersQueryHandler
{
    private const int MaxLimit = 50;

    private readonly IRecruitingReadModel _readModel;
    private readonly IClock _clock;

    public ListPendingInterviewRemindersQueryHandler(IRecruitingReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<InterviewNoticeDto>>> HandleAsync(
        ListPendingInterviewRemindersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (InterviewReminderWindow.At(_clock.UtcNow) is not { } window)
        {
            return Result.Success<IReadOnlyList<InterviewNoticeDto>>([]);
        }

        var rows = await _readModel
            .ListPendingInterviewRemindersAsync(
                window.InterviewFrom,
                window.InterviewTo,
                window.InvitedBefore,
                Math.Clamp(query.Limit, 1, MaxLimit),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(rows);
    }
}

/// <summary>Records that the bot tried the reminder. One attempt, as with the invitation.</summary>
public sealed record MarkInterviewReminderSentCommand(Guid CandidateId, bool Delivered = true);

public sealed class MarkInterviewReminderSentCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkInterviewReminderSentCommandHandler(
        ICandidateRepository candidates,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        MarkInterviewReminderSentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetByIdAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        candidate.MarkInterviewReminderSent(_clock.UtcNow, command.Delivered);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
