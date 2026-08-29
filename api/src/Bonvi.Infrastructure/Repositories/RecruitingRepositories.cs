using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IPipelineStageRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class PipelineStageRepository : ReferenceRepository<PipelineStage>, IPipelineStageRepository
{
    public PipelineStageRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Asosdagi ro'yxat ALIFBO bo'yicha saralaydi va bu boshqa sakkizta
    /// ma'lumotnoma uchun to'g'ri. Voronkada esa u xato natija berardi:
    /// «Job Offer», «Кенгаш», «Телефон суҳбат» — bu voronka emas, lug'at.
    /// Shu sababli saralash qayta ta'riflanadi.
    /// </para>
    /// <para>
    /// Ikkinchi mezon — nom: ikki bosqichga tasodifan bir xil tartib
    /// tushib qolsa (masalan bazaga qo'lda aralashilsa), ro'yxat baribir
    /// har safar bir xil ko'rinishi kerak.
    /// </para>
    /// </remarks>
    public override async Task<IReadOnlyList<PipelineStage>> ListAsync(
        bool onlyActive = false,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking();

        if (onlyActive)
        {
            query = query.Where(stage => stage.IsActive);
        }

        return await query
            .OrderBy(stage => stage.SortOrder)
            .ThenBy(stage => stage.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Yumshoq o'chirilganlar hisobga OLINMAYDI (global filtr ularni
    /// yashiradi), faolsizlantirilganlar esa olinadi: ular ro'yxatda
    /// turibdi va yangi bosqich ularning ustiga tushib qolmasligi kerak.
    /// Bo'sh jadvalda PostgreSQL <c>NULL</c> qaytaradi — shu sababli
    /// natija ham nullable.
    /// </remarks>
    public async Task<int?> MaxSortOrderAsync(CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .MaxAsync(stage => (int?)stage.SortOrder, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Kuzatiladi (<c>AsNoTracking</c> yo'q): bu metod faqat qayta
    /// tartiblashdan oldin chaqiriladi va yozuvlar darhol o'zgartiriladi.
    /// </remarks>
    public async Task<IReadOnlyList<PipelineStage>> GetManyAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        return await Set
            .Where(stage => ids.Contains(stage.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary><see cref="IVacancyRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class VacancyRepository : Repository<Vacancy>, IVacancyRepository
{
    public VacancyRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<bool> HasOpenAsync(
        Guid vacancyTitleId,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()
            .Where(vacancy =>
                vacancy.VacancyTitleId == vacancyTitleId &&
                vacancy.Status == VacancyStatus.Open);

        if (exceptId is { } id)
        {
            query = query.Where(vacancy => vacancy.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }
}

/// <summary><see cref="ICandidateRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class CandidateRepository : Repository<Candidate>, ICandidateRepository
{
    public CandidateRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// Tartib <c>Include</c> ichida ATAYLAB beriladi: aks holda PostgreSQL
    /// qatorlarni ixtiyoriy tartibda qaytaradi va voronka tarixi har
    /// ochilganda boshqacha ko'rinardi. So'rov
    /// <c>(candidate_id, recorded_at)</c> indeksiga to'liq tushadi.
    /// </remarks>
    public Task<Candidate?> GetWithStagesAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(candidate => candidate.StageEntries.OrderBy(entry => entry.RecordedAt))
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountActiveOnStageAsync(Guid stageId, CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .CountAsync(
                candidate =>
                    candidate.CurrentStageId == stageId &&
                    candidate.Status == CandidateStatus.InProgress,
                cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Ikkala son BITTA so'rovda olinadi: vakansiya kartasi ikkalasini ham
    /// birga ko'rsatadi («3 nomzod, 1 tasi olindi») va ikki alohida
    /// so'rov ro'yxatda har bir qator uchun ikki barobar murojaat bo'lardi.
    /// </remarks>
    public async Task<(int Total, int Hired)> CountByVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default)
    {
        var counts = await Set
            .AsNoTracking()
            .Where(candidate => candidate.VacancyId == vacancyId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Hired = group.Count(candidate => candidate.Status == CandidateStatus.Hired),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return counts is null ? (0, 0) : (counts.Total, counts.Hired);
    }
}

/// <summary><see cref="IJobOfferRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class JobOfferRepository : Repository<JobOffer>, IJobOfferRepository
{
    public JobOfferRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// Eng yangisi birinchi: kartada joriy taklif tepada turishi kerak,
    /// muzokara tarixi esa pastda.
    /// </remarks>
    public async Task<IReadOnlyList<JobOffer>> GetByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(offer => offer.CandidateId == candidateId)
            .OrderByDescending(offer => offer.SentAt)
            .ThenByDescending(offer => offer.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// Kuzatiladi: bu metod javob yozishdan oldin ham chaqiriladi va
    /// topilgan yozuv darhol o'zgartirilishi mumkin.
    /// </remarks>
    public Task<JobOffer?> GetPendingByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
        => Set
            .Where(offer => offer.CandidateId == candidateId && offer.Status == JobOfferStatus.Sent)
            .OrderByDescending(offer => offer.SentAt)
            .FirstOrDefaultAsync(cancellationToken);
}
