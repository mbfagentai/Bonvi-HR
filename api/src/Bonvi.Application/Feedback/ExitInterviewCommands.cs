using Bonvi.Application.Feedback.Common;
using Bonvi.Application.Feedback.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;

namespace Bonvi.Application.Feedback;

/// <summary>Ishdan ketuvchi xodim bilan suhbatni yozib qo'yadi.</summary>
public sealed record RecordExitInterviewCommand(
    Guid EmploymentId,
    DateOnly InterviewDate,
    string? Reason,
    RecommendationAnswer? WouldRecommend = null,
    string? Impressions = null,
    string? ConditionsToReturn = null);

/// <summary>Kiritish xatosini tuzatadi — mehnat munosabati (EmploymentId) O'ZGARMAYDI.</summary>
public sealed record UpdateExitInterviewCommand(
    Guid ExitInterviewId,
    DateOnly InterviewDate,
    string? Reason,
    RecommendationAnswer? WouldRecommend = null,
    string? Impressions = null,
    string? ConditionsToReturn = null);

public sealed record DeleteExitInterviewCommand(Guid ExitInterviewId);

public sealed class RecordExitInterviewCommandHandler
{
    private readonly IExitInterviewRepository _interviews;
    private readonly IEmploymentRepository _employments;
    private readonly ExitInterviewMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public RecordExitInterviewCommandHandler(
        IExitInterviewRepository interviews,
        IEmploymentRepository employments,
        ExitInterviewMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _interviews = interviews;
        _employments = employments;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExitInterviewDto>> HandleAsync(
        RecordExitInterviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employment = await _employments
            .GetByIdAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return FeedbackErrors.EmploymentNotFound;
        }

        // Faqat YAKUNLANGAN munosabat uchun — sabab entity izohida
        // (atamaning o'zi «exit» degani).
        if (employment.Status != EmploymentStatus.Terminated)
        {
            return FeedbackErrors.EmploymentNotTerminated;
        }

        // Bittadan ortiq yozuv bo'lmasligi — bitta munosabat bir marta
        // tugaydi, demak unga bitta suhbat yetarli.
        var existing = await _interviews
            .GetByEmploymentAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return FeedbackErrors.AlreadyRecorded;
        }

        var recorded = ExitInterview.Record(
            command.EmploymentId,
            command.InterviewDate,
            command.Reason,
            command.WouldRecommend,
            command.Impressions,
            command.ConditionsToReturn);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        _interviews.Add(recorded.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(recorded.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateExitInterviewCommandHandler
{
    private readonly IExitInterviewRepository _interviews;
    private readonly ExitInterviewMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateExitInterviewCommandHandler(
        IExitInterviewRepository interviews,
        ExitInterviewMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _interviews = interviews;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ExitInterviewDto>> HandleAsync(
        UpdateExitInterviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var interview = await _interviews
            .GetByIdAsync(command.ExitInterviewId, cancellationToken)
            .ConfigureAwait(false);

        if (interview is null)
        {
            return FeedbackErrors.NotFound;
        }

        var updated = interview.Update(
            command.InterviewDate,
            command.Reason,
            command.WouldRecommend,
            command.Impressions,
            command.ConditionsToReturn);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(interview, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DeleteExitInterviewCommandHandler
{
    private readonly IExitInterviewRepository _interviews;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteExitInterviewCommandHandler(IExitInterviewRepository interviews, IUnitOfWork unitOfWork)
    {
        _interviews = interviews;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteExitInterviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var interview = await _interviews
            .GetByIdAsync(command.ExitInterviewId, cancellationToken)
            .ConfigureAwait(false);

        if (interview is null)
        {
            return FeedbackErrors.NotFound;
        }

        // Yumshoq o'chirish — AuditableInterceptor Remove'ni DeletedAt
        // belgisiga aylantiradi.
        _interviews.Remove(interview);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
