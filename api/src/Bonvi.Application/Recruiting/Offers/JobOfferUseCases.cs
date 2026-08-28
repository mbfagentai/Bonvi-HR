using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Offers;

/// <summary>
/// Nomzodga ish taklifini yuborish.
/// </summary>
/// <remarks>
/// Vakansiya so'ralmaydi — u NOMZODNIKI. Alohida so'ralsa, taklif
/// nomzodning vakansiyasidan boshqa o'ringa berilishi va ishga qabul
/// butunlay kutilmagan lavozimga tushishi mumkin edi.
/// </remarks>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="SalaryAmount">Taklif qilingan oylik ish haqi — MAJBURIY.</param>
/// <param name="SalaryCurrency">Valyuta. Bo'sh — UZS.</param>
/// <param name="StartDate">Ish boshlash sanasi — ishga qabul sanasi bo'ladi.</param>
/// <param name="SentAt">Yuborilgan sana. Bo'sh — bugun.</param>
/// <param name="ExpiresAt">Javob muddati. Bo'sh — muddatsiz.</param>
/// <param name="Note">Ichki izoh.</param>
public sealed record SendJobOfferCommand(
    Guid CandidateId,
    decimal SalaryAmount,
    string? SalaryCurrency = null,
    DateOnly? StartDate = null,
    DateOnly? SentAt = null,
    DateOnly? ExpiresAt = null,
    string? Note = null);

/// <summary>Taklif shartlarini tuzatish — faqat javob kelmaguncha.</summary>
public sealed record UpdateJobOfferCommand(
    Guid OfferId,
    decimal SalaryAmount,
    string? SalaryCurrency = null,
    DateOnly? StartDate = null,
    DateOnly? ExpiresAt = null,
    string? Note = null);

/// <summary>Nomzod taklifni rad etdi.</summary>
public sealed record DeclineJobOfferCommand(Guid OfferId, DateOnly? At = null, string? Note = null);

/// <summary>Taklif muddati o'tdi — javob kelmadi.</summary>
public sealed record ExpireJobOfferCommand(Guid OfferId, DateOnly? At = null, string? Note = null);

/// <summary>Nomzodning barcha takliflari.</summary>
public sealed record ListCandidateOffersQuery(Guid CandidateId);

public sealed class ListCandidateOffersQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IJobOfferRepository _offers;
    private readonly RecruitingMapper _mapper;

    public ListCandidateOffersQueryHandler(
        ICandidateRepository candidates,
        IJobOfferRepository offers,
        RecruitingMapper mapper)
    {
        _candidates = candidates;
        _offers = offers;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<JobOfferDto>>> HandleAsync(
        ListCandidateOffersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _candidates.ExistsAsync(query.CandidateId, cancellationToken).ConfigureAwait(false))
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var offers = await _offers.GetByCandidateAsync(query.CandidateId, cancellationToken).ConfigureAwait(false);

        var items = new List<JobOfferDto>(offers.Count);
        foreach (var offer in offers)
        {
            items.Add(await _mapper.ToDtoAsync(offer, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<JobOfferDto>>(items);
    }
}

/// <summary>
/// Ish taklifini yuboradi.
/// </summary>
/// <remarks>
/// <b>Bir vaqtda faqat BITTA faol taklif.</b> Ikkita ochiq taklif ikki xil
/// oklad va'da qilingan degani va qaysi biri kuchda ekani noaniq bo'lardi —
/// eng yomoni, ikkalasi ham qabul qilinib, ikkita xodim kartasi
/// yaratilishi mumkin edi. Yangi shart taklif qilish uchun avval eskisiga
/// javob yozish kerak (rad etildi / muddati o'tdi).
/// </remarks>
public sealed class SendJobOfferCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IJobOfferRepository _offers;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SendJobOfferCommandHandler(
        ICandidateRepository candidates,
        IJobOfferRepository offers,
        RecruitingMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _offers = offers;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<JobOfferDto>> HandleAsync(
        SendJobOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates.GetByIdAsync(command.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        if (!candidate.IsActive)
        {
            // Rad etilgan yoki allaqachon ishga olingan nomzodga taklif
            // yuborish ma'nosiz. Xato kodi domendan keladi — nomzodning
            // holati haqidagi qoida bitta joyda turishi kerak.
            return Error.Conflict(
                "nomzod.jarayonda_emas",
                $"Nomzod voronkadan chiqqan (holati: {candidate.Status}), taklif yuborib bo'lmaydi.");
        }

        var pending = await _offers
            .GetPendingByCandidateAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (pending is not null)
        {
            return RecruitingErrors.OfferAlreadyPending;
        }

        var salary = Money.Create(command.SalaryAmount, command.SalaryCurrency ?? Money.Uzs);
        if (salary.IsFailure)
        {
            return salary.Error;
        }

        var sentAt = command.SentAt ?? _clock.Today;

        var sent = JobOffer.Send(
            candidate.Id,
            candidate.VacancyId,
            salary.Value,
            command.StartDate ?? sentAt,
            sentAt,
            command.ExpiresAt,
            command.Note);

        if (sent.IsFailure)
        {
            return sent.Error;
        }

        _offers.Add(sent.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(sent.Value, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Taklifni tuzatish va unga javob yozish (rad etildi / muddati o'tdi).
/// </summary>
/// <remarks>
/// Uchala amal bir xil qadamlardan iborat (yukla → domen metodi → saqla).
/// <b>«Qabul qilindi» BU YERDA YO'Q</b>: u nomzodni xodimga aylantiradi va
/// butunlay boshqa hajmdagi amal — <c>AcceptJobOfferCommandHandler</c>.
/// </remarks>
public sealed class ChangeJobOfferCommandHandler
{
    private readonly IJobOfferRepository _offers;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeJobOfferCommandHandler(
        IJobOfferRepository offers,
        RecruitingMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _offers = offers;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<JobOfferDto>> HandleAsync(
        UpdateJobOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var salary = Money.Create(command.SalaryAmount, command.SalaryCurrency ?? Money.Uzs);

        return salary.IsFailure
            ? Task.FromResult(Result.Failure<JobOfferDto>(salary.Error))
            : ApplyAsync(
                command.OfferId,
                offer => offer.Update(
                    salary.Value,
                    command.StartDate ?? offer.StartDate,
                    command.ExpiresAt,
                    command.Note),
                cancellationToken);
    }

    public Task<Result<JobOfferDto>> HandleAsync(
        DeclineJobOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyAsync(
            command.OfferId,
            offer => offer.Decline(command.At ?? _clock.Today, command.Note),
            cancellationToken);
    }

    public Task<Result<JobOfferDto>> HandleAsync(
        ExpireJobOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyAsync(
            command.OfferId,
            offer => offer.MarkExpired(command.At ?? _clock.Today, command.Note),
            cancellationToken);
    }

    private async Task<Result<JobOfferDto>> ApplyAsync(
        Guid offerId,
        Func<JobOffer, Result> change,
        CancellationToken cancellationToken)
    {
        var offer = await _offers.GetByIdAsync(offerId, cancellationToken).ConfigureAwait(false);
        if (offer is null)
        {
            return RecruitingErrors.OfferNotFound;
        }

        var changed = change(offer);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(offer, cancellationToken).ConfigureAwait(false);
    }
}
