using System.Diagnostics.CodeAnalysis;
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
public sealed class VacancyRequestRepository : Repository<VacancyRequest>, IVacancyRequestRepository
{
    public VacancyRequestRepository(AppDbContext context) : base(context) { }
}

public sealed class VacancyRepository : Repository<Vacancy>, IVacancyRepository
{
    public VacancyRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ToLower()</c> SQL'dagi <c>lower()</c> ga tarjima qilinadi —
    /// analizator qoidalari nega o'chirilgani
    /// <c>ReferenceRepository.NameEquals</c> izohida.
    /// </remarks>
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "EF Core StringComparison variantini SQL'ga tarjima qila olmaydi.")]
    public Task<bool> TitleTakenAsync(
        string title,
        Guid? exceptId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var normalized = (Vacancy.NormalizeTitle(title) ?? string.Empty).ToLowerInvariant();

        var query = Set
            .AsNoTracking()
            .Where(vacancy => vacancy.Title.ToLower() == normalized);

        if (openOnly)
        {
            query = query.Where(vacancy => vacancy.Status == VacancyStatus.Open);
        }

        // Tahrirlashda yozuvning O'ZI takror hisoblanmasligi kerak
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
    /// <para>
    /// Tartib <c>Include</c> ichida ATAYLAB beriladi: aks holda PostgreSQL
    /// qatorlarni ixtiyoriy tartibda qaytaradi va voronka tarixi har
    /// ochilganda boshqacha ko'rinardi. So'rov
    /// <c>(candidate_id, recorded_at)</c> indeksiga to'liq tushadi.
    /// </para>
    /// <para>
    /// Anketa ham shu yerda ulanadi — bittadan ko'p bo'lmaydi (1:1) va u
    /// nomzod kartasi bilan birga ko'rsatiladi. Alohida so'rov bo'lganida
    /// har bir karta ochilishi ikki murojaat bo'lardi, kechiktirilgan
    /// yuklash (lazy) esa bu loyihada umuman yoqilmagan: anketa jimgina
    /// <c>null</c> bo'lib qolardi.
    /// </para>
    /// <para>
    /// ⚠️ <c>AsSplitQuery()</c> — bezak emas, ZARURAT. Uchta to'plam bitta
    /// so'rovga qo'shilsa PostgreSQL dekart ko'paytmasini qaytaradi (eng
    /// yomon holatda 10 × 10 × 10 qator, ya'ni bitta nomzod uchun mingta
    /// qator) va EF Core buni ogohlantirish bilan qayd qiladi. Bo'lingan
    /// so'rov o'rniga to'rtta arzon so'rov ketadi va har biri o'z
    /// indeksiga tushadi.
    /// </para>
    /// <para>
    /// ⚠️ Fayl va izohda <c>ThenBy(Id)</c> — TENGLIKNI uzish uchun.
    /// <c>CreatedAt</c> <c>DateTimeOffset.UtcNow</c> dan olinadi
    /// (<c>CandidateStageEntry</c> va <c>EmploymentEvent</c> dagi bilan
    /// bir xil), ya'ni bitta tick ichida yozilgan ikki qatorning vaqti
    /// AYNAN teng bo'lishi mumkin — o'shanda PostgreSQL ularni har
    /// o'qishda boshqa tartibda qaytaradi va pasport varaqlari joy
    /// almashardi, ya'ni yuqoridagi <c>OrderBy</c> aynan o'zi oldini
    /// olishi kerak bo'lgan holat qaytardi. <c>Id</c> tiebreaker sifatida
    /// tasodifiy emas: <c>Entity.NewId()</c> — UUID v7, u ham vaqt
    /// bo'yicha o'sadi, ya'ni teng vaqtda ham YOZILISH ketma-ketligini
    /// saqlaydi.
    /// </para>
    /// </remarks>
    public Task<Candidate?> GetWithStagesAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(candidate => candidate.StageEntries.OrderBy(entry => entry.RecordedAt).ThenBy(entry => entry.Id))
            .Include(candidate => candidate.Files.OrderBy(file => file.CreatedAt).ThenBy(file => file.Id))
            .Include(candidate => candidate.Notes.OrderBy(note => note.CreatedAt).ThenBy(note => note.Id))
            .Include(candidate => candidate.Questionnaire)
            .AsSplitQuery()
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

/// <summary><see cref="ICandidateFormRepository"/> — PostgreSQL.</summary>
public sealed class CandidateFormRepository : Repository<CandidateForm>, ICandidateFormRepository
{
    public CandidateFormRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>nextval</c> tranzaksiyadan tashqarida — raqamda «teshik» qolishi zararsiz
    /// (<c>DevicePersonNumbers</c> izohida batafsil).
    /// </remarks>
    public async Task<int> NextNumberAsync(CancellationToken cancellationToken = default)
    {
        var values = await Context.Database
            .SqlQuery<int>($"SELECT nextval('candidate_form_number_seq')::int AS \"Value\"")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return values[0];
    }

    /// <inheritdoc />
    public Task<CandidateForm?> FindBySubmissionAsync(Guid clientSubmissionId, CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .FirstOrDefaultAsync(form => form.ClientSubmissionId == clientSubmissionId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateForm>> ListByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(form => form.CandidateId == candidateId)
            .OrderByDescending(form => form.SubmittedAt)
            .ThenByDescending(form => form.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
