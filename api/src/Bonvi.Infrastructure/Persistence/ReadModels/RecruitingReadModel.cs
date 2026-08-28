using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IRecruitingReadModel"/> ning EF Core orqali amalga oshirilishi —
/// nomzodlar va vakansiyalar ro'yxatlari uchun proyeksiya so'rovlari.
/// </summary>
/// <remarks>
/// <para>
/// Fayl <c>Persistence/ReadModels</c> da, repozitoriylar yonida emas:
/// bu yerdagi so'rovlar agregat qaytarmaydi va hech qachon saqlanmaydi
/// (<see cref="EmployeeReadModel"/> izohidagi bilan bir xil sabab).
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash BAZADA.</b> Materializatsiyadan keyin
/// birorta ham <c>Where</c> yoki <c>OrderBy</c> yo'q — faqat ko'rsatish
/// uchun kerak bo'lgan nomlar qo'shiladi.
/// </para>
/// </remarks>
public sealed class RecruitingReadModel : IRecruitingReadModel
{
    private readonly AppDbContext _context;

    public RecruitingReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// So'rov uchta bo'lakdan iborat: nomzodlar filtrlanadi, vakansiya va
    /// bosqich NOMLARI chapdan birlashtiriladi, so'ng saralash va sahifalash
    /// qo'llanadi. Nomlar chapdan qo'shiladi — o'chirilgan vakansiyaga yoki
    /// faolsizlantirilgan bosqichga bog'langan nomzod ro'yxatdan TUSHIB
    /// QOLMASLIGI kerak (ichki birlashtirish aynan shuni qilardi va HR
    /// nomzodni «yo'qolgan» deb hisoblardi).
    /// </para>
    /// <para>
    /// «Qaysi xodimga aylandi» havolasi alohida, FAQAT shu sahifadagi
    /// nomzodlar uchun so'raladi — <c>IncentiveReadModel.TypeNamesAsync</c>
    /// dagi bilan bir xil qoida: u faqat ko'rsatish uchun, unga filtr ham,
    /// saralash ham qo'llanmaydi.
    /// </para>
    /// </remarks>
    public async Task<CandidatePage> SearchCandidatesAsync(
        CandidateSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var candidates = _context.Candidates.AsNoTracking();

        if (filter.VacancyId is { } vacancyId)
        {
            candidates = candidates.Where(candidate => candidate.VacancyId == vacancyId);
        }

        if (filter.StageId is { } stageId)
        {
            candidates = candidates.Where(candidate => candidate.CurrentStageId == stageId);
        }

        if (filter.Status is { } status)
        {
            candidates = candidates.Where(candidate => candidate.Status == status);
        }

        if (filter.Source is { } source)
        {
            candidates = candidates.Where(candidate => candidate.Source == source);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Qidiruv qoidasi bitta joyda — `CandidateSearchQuery`. Uning
            // ichidagi yig'ish (қ/ғ/ҳ/ў/ё va telefon ajratgichlari) xodimlar
            // ro'yxatidagi bilan AYNAN bir xil manbadan keladi.
            var matching = CandidateSearchQuery.MatchingIds(_context, filter.Search);

            candidates = candidates.Where(candidate => matching.Contains(candidate.Id));
        }

        var joined =
            from candidate in candidates
            join vacancy in _context.Vacancies.AsNoTracking()
                on candidate.VacancyId equals vacancy.Id into vacancyGroup
            from vacancy in vacancyGroup.DefaultIfEmpty()
            join position in _context.Positions.AsNoTracking()
                on vacancy.PositionId equals position.Id into positionGroup
            from position in positionGroup.DefaultIfEmpty()
            join stage in _context.PipelineStages.AsNoTracking()
                on candidate.CurrentStageId equals stage.Id into stageGroup
            from stage in stageGroup.DefaultIfEmpty()
            select new
            {
                Candidate = candidate,
                VacancyName = position != null ? position.Name : null,
                StageName = stage != null ? stage.Name : null,
                StageOrder = stage != null ? stage.SortOrder : int.MaxValue,
            };

        var total = await joined.CountAsync(cancellationToken).ConfigureAwait(false);

        // Saralash shu yerda, alohida metodda emas: kalitlar anonim tipning
        // maydonlari va ularni metod chegarasidan o'tkazib bo'lmaydi
        // (`IncentiveReadModel` dagi bilan bir xil sabab).
        var ordered = filter.Sort switch
        {
            CandidateSortField.Name => filter.Descending
                ? joined
                    .OrderByDescending(row => row.Candidate.FullName.LastName)
                    .ThenByDescending(row => row.Candidate.FullName.FirstName)
                : joined
                    .OrderBy(row => row.Candidate.FullName.LastName)
                    .ThenBy(row => row.Candidate.FullName.FirstName),

            CandidateSortField.Stage => filter.Descending
                ? joined.OrderByDescending(row => row.StageOrder)
                : joined.OrderBy(row => row.StageOrder),

            _ => filter.Descending
                ? joined.OrderByDescending(row => row.Candidate.CreatedAt)
                : joined.OrderBy(row => row.Candidate.CreatedAt),
        };

        var page = await ordered
            // Oxirgi mezon sahifalash uchun MAJBURIY: bir kunda o'nlab
            // anketa tushishi mumkin va teng qiymatlarda PostgreSQL tartibni
            // kafolatlamaydi — bitta yozuv ikkinchi sahifada takrorlanib,
            // boshqasi umuman tushib qolardi. Yo'nalish DOIM kamayish:
            // `Id` — v7 GUID, ya'ni «oxirgi kiritilgani tepada».
            .ThenByDescending(row => row.Candidate.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hires = await HiredEmployeeIdsAsync(
                [.. page.Where(row => row.Candidate.Status == CandidateStatus.Hired).Select(row => row.Candidate.Id)],
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<CandidateRow> items =
        [
            .. page.Select(row => new CandidateRow(
                row.Candidate.Id,
                row.Candidate.FullName.LastName,
                row.Candidate.FullName.FirstName,
                row.Candidate.FullName.MiddleName,
                row.Candidate.Phone == null ? null : row.Candidate.Phone.Value,
                row.Candidate.BirthDate,
                row.Candidate.Speciality,
                row.Candidate.VacancyId,
                row.VacancyName,
                row.Candidate.CurrentStageId,
                row.StageName,
                row.Candidate.Status,
                row.Candidate.Source,
                row.Candidate.ResumeFileKey != null,
                row.Candidate.CreatedAt,
                hires.GetValueOrDefault(row.Candidate.Id))),
        ];

        return new CandidatePage(items, total);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Nomzodlar sanog'i bog'liq so'rov (correlated subquery) bilan
    /// olinadi: vakansiyalar soni o'nlab, nomzodlar esa yuzlab bo'ladi va
    /// ularni birlashtirib guruhlash butun nomzodlar jadvalini har bir
    /// sahifa uchun o'qishga majbur qilardi. Ikkala sanoq ham
    /// <c>(vacancy_id, status)</c> indeksiga tushadi.
    /// </remarks>
    public async Task<VacancyPage> ListVacanciesAsync(
        VacancyListFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var vacancies = _context.Vacancies.AsNoTracking();

        if (filter.Status is { } status)
        {
            vacancies = vacancies.Where(vacancy => vacancy.Status == status);
        }

        if (filter.PositionId is { } positionId)
        {
            vacancies = vacancies.Where(vacancy => vacancy.PositionId == positionId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            // ⚠️ Bo'lim filtri AYNAN mos keladigan bo'lim bo'yicha, butun
            // shox bo'yicha EMAS — xodimlar ro'yxatidagidan farqli. Sabab:
            // vakansiya ANIQ bir bo'limga ochiladi va «7-Departamentdagi
            // vakansiyalar» degan savol amalda «uning ichidagi 22 ta sexning
            // hammasidan» degani emas. Kerak bo'lganda shox bo'yicha filtr
            // qo'shiladi, lekin u ATAYLAB hozir kiritilmadi.
            vacancies = vacancies.Where(vacancy => vacancy.DepartmentId == departmentId);
        }

        if (filter.LegalEntityId is { } legalEntityId)
        {
            vacancies = vacancies.Where(vacancy => vacancy.LegalEntityId == legalEntityId);
        }

        var joined =
            from vacancy in vacancies
            join position in _context.Positions.AsNoTracking()
                on vacancy.PositionId equals position.Id into positionGroup
            from position in positionGroup.DefaultIfEmpty()
            join legalEntity in _context.LegalEntities.AsNoTracking()
                on vacancy.LegalEntityId equals legalEntity.Id into legalEntityGroup
            from legalEntity in legalEntityGroup.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking()
                on vacancy.DepartmentId equals department.Id into departmentGroup
            from department in departmentGroup.DefaultIfEmpty()
            join responsible in _context.Employees.AsNoTracking()
                on vacancy.ResponsibleEmployeeId equals responsible.Id into responsibleGroup
            from responsible in responsibleGroup.DefaultIfEmpty()
            select new
            {
                Vacancy = vacancy,
                PositionName = position != null ? position.Name : null,
                LegalEntityName = legalEntity != null ? legalEntity.Name : null,
                DepartmentName = department != null ? department.Name : null,
                Responsible = responsible,
                CandidateCount = _context.Candidates.Count(c => c.VacancyId == vacancy.Id),
                HiredCount = _context.Candidates
                    .Count(c => c.VacancyId == vacancy.Id && c.Status == CandidateStatus.Hired),
            };

        var total = await joined.CountAsync(cancellationToken).ConfigureAwait(false);

        var ordered = filter.Sort switch
        {
            VacancySortField.Position => filter.Descending
                ? joined.OrderByDescending(row => row.PositionName)
                : joined.OrderBy(row => row.PositionName),

            _ => filter.Descending
                ? joined.OrderByDescending(row => row.Vacancy.OpenedAt)
                : joined.OrderBy(row => row.Vacancy.OpenedAt),
        };

        var page = await ordered
            .ThenByDescending(row => row.Vacancy.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<VacancyRow> items =
        [
            .. page.Select(row => new VacancyRow(
                row.Vacancy.Id,
                row.Vacancy.PositionId,
                row.PositionName,
                row.Vacancy.LegalEntityId,
                row.LegalEntityName,
                row.Vacancy.DepartmentId,
                row.DepartmentName,
                row.Vacancy.Openings,
                row.Vacancy.Status,
                row.Vacancy.OpenedAt,
                row.Vacancy.ClosedAt,
                row.Vacancy.ResponsibleEmployeeId,
                row.Responsible == null ? null : row.Responsible.FullName.Full,
                row.Vacancy.Note,
                row.CandidateCount,
                row.HiredCount)),
        ];

        return new VacancyPage(items, total);
    }

    /// <summary>
    /// Nomzod → u aylangan XODIM identifikatori.
    /// </summary>
    /// <remarks>
    /// Bog'lanish ishga qabul HODISASIDA saqlanadi
    /// (<c>EmploymentEvent.CandidateId</c>), shu sababli so'rov hodisadan
    /// munosabatga, undan xodimga boradi. Faqat SHU SAHIFADAGI ishga
    /// olingan nomzodlar uchun — odatda bir necha yozuv.
    /// </remarks>
    private async Task<Dictionary<Guid, Guid>> HiredEmployeeIdsAsync(
        List<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var rows = await (
                from item in _context.EmploymentEvents.AsNoTracking()
                join employment in _context.Employments.AsNoTracking()
                    on item.EmploymentId equals employment.Id
                where item.Type == EmploymentEventType.Hire
                      && item.CandidateId != null
                      && candidateIds.Contains(item.CandidateId.Value)
                select new { CandidateId = item.CandidateId!.Value, employment.EmployeeId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = new Dictionary<Guid, Guid>();
        foreach (var row in rows)
        {
            // Bir nomzoddan ikkinchi marta ishga olish TO'SILGAN, lekin
            // eski ma'lumotda takror uchrasa ham so'rov yiqilmasligi kerak:
            // birinchi topilgani olinadi.
            map.TryAdd(row.CandidateId, row.EmployeeId);
        }

        return map;
    }
}
