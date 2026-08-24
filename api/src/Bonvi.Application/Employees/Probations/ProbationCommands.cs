using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Probations;

/// <summary>Sinov muddatini yakunlaydi.</summary>
/// <param name="ProbationId">Sinov muddati identifikatori.</param>
/// <param name="Status">Natija: o'tdi / o'tmadi / erta ketdi / bekor qilindi.</param>
/// <param name="ResolvedAt">Yakunlangan sana. Bo'sh bo'lsa — bugun.</param>
/// <param name="Conclusion">Xulosa matni.</param>
/// <param name="ReviewerEmployeeId">Xulosa bergan mas'ul.</param>
public sealed record ResolveProbationCommand(
    Guid ProbationId,
    ProbationStatus Status,
    DateOnly? ResolvedAt = null,
    string? Conclusion = null,
    Guid? ReviewerEmployeeId = null);

/// <summary>Sinov muddatini uzaytiradi.</summary>
public sealed record ExtendProbationCommand(Guid ProbationId, DateOnly NewEndDate);

/// <summary>
/// Sinov muddatini yakunlaydi va natijani mehnat munosabatiga o'tkazadi.
/// </summary>
/// <remarks>
/// Sinovdan o'tgan xodimning munosabati <c>Probation</c> dan <c>Active</c> ga
/// o'tishi KERAK — aks holda ro'yxatda u abadiy «sinovda» bo'lib qolardi va
/// aynan shu Excel'dagi 411 ta «Тугатилган» yozuvining sababi edi.
/// </remarks>
public sealed class ResolveProbationCommandHandler
{
    private readonly IProbationRepository _probations;
    private readonly IEmploymentRepository _employments;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveProbationCommandHandler(
        IProbationRepository probations,
        IEmploymentRepository employments,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _probations = probations;
        _employments = employments;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProbationDto>> HandleAsync(
        ResolveProbationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var probation = await _probations
            .GetByIdAsync(command.ProbationId, cancellationToken)
            .ConfigureAwait(false);

        if (probation is null)
        {
            return EmployeeErrors.ProbationNotFound;
        }

        var at = command.ResolvedAt ?? _clock.Today;

        var resolved = probation.Resolve(command.Status, at, command.Conclusion, command.ReviewerEmployeeId);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        if (command.Status == ProbationStatus.Passed)
        {
            var employment = await _employments
                .GetWithEventsAsync(probation.EmploymentId, cancellationToken)
                .ConfigureAwait(false);

            // Munosabat topilmasa ham sinov natijasi saqlanadi: yozuv
            // yo'qolgandan ko'ra, nomukammal bog'lanish yaxshiroq.
            if (employment is { Status: EmploymentStatus.Probation })
            {
                var completed = employment.CompleteProbation(at, command.Conclusion);
                if (completed.IsFailure)
                {
                    return completed.Error;
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_mapper.ToDto(probation));
    }
}

/// <summary>Sinov muddatini uzaytiradi (umumiy chegara — 180 kun).</summary>
public sealed class ExtendProbationCommandHandler
{
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendProbationCommandHandler(
        IProbationRepository probations,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _probations = probations;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProbationDto>> HandleAsync(
        ExtendProbationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var probation = await _probations
            .GetByIdAsync(command.ProbationId, cancellationToken)
            .ConfigureAwait(false);

        if (probation is null)
        {
            return EmployeeErrors.ProbationNotFound;
        }

        var extended = probation.Extend(command.NewEndDate);
        if (extended.IsFailure)
        {
            return extended.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_mapper.ToDto(probation));
    }
}
