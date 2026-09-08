using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Dtos;
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

        // Ishga olingan nomzod kundalik ro'yxatda KO'RINMAYDI: u endi
        // xodim va uning o'rni «Xodimlar» bo'limida. Yozuv o'chirilmaydi —
        // u vakansiya sanog'iga va bot hisobotiga kerak (batafsil sabab
        // `CandidateSearchFilter.HideHired` izohida). Holat ataylab
        // tanlangan bo'lsa bu qoida ishlamaydi, ya'ni shart yuqoridagi
        // filtr bilan hech qachon ziddiyatga tushmaydi.
        //
        // Sanoq (`total`) ham shu so'rovdan olinadi — qatorlar bilan
        // sanoqning ziddiyatga tushishi shu tufayli MUMKIN EMAS.
        if (filter.HideHired)
        {
            candidates = candidates.Where(candidate => candidate.Status != CandidateStatus.Hired);
        }

        if (filter.Source is { } source)
        {
            candidates = candidates.Where(candidate => candidate.Source == source);
        }

        // Excel'dan ko'chirilgan kengash arxivi (3110 yozuv) kundalik
        // ro'yxatda KO'RINMAYDI. Sabab amaliy: HR ekranni «hozir kim
        // bor?» degan savol bilan ochadi va 2023-yilda rad etilgan
        // odamlar bu savolni ko'mib yuboradi. Arxiv yo'qolmaydi —
        // manba filtridan «Excel arxivi» ni tanlash yetarli.
        if (filter.HideArchive)
        {
            candidates = candidates.Where(candidate => candidate.Source != CandidateSource.Excel);
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
            join title in _context.VacancyTitles.AsNoTracking()
                on vacancy.VacancyTitleId equals title.Id into titleGroup
            from title in titleGroup.DefaultIfEmpty()
            join stage in _context.PipelineStages.AsNoTracking()
                on candidate.CurrentStageId equals stage.Id into stageGroup
            from stage in stageGroup.DefaultIfEmpty()
            select new
            {
                Candidate = candidate,
                // Nomzod qatorida ko'rinadigan «vakansiya» — endi E'LON
                // NOMI, lavozim emas: vakansiyada lavozim qolmadi
                // (sababi `Vacancy` izohida).
                VacancyName = title != null ? title.Name : null,
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
                row.Candidate.PhotoKey != null,
                row.Candidate.CreatedAt,

                /*
                  ⚠️ `GetValueOrDefault` BU YERDA YARAMAYDI: lug'at
                  qiymati `Guid` (nullable emas), ya'ni topilmaganda u
                  `null` emas, `Guid.Empty` qaytaradi. Maydon esa
                  `Guid?` — natijada ishga OLINMAGAN nomzod javobda
                  «00000000-0000-0000-0000-000000000000» bilan chiqardi.
                  Bugun ro'yxat bu maydonni chizmaydi, lekin unga
                  «xodim kartasi» havolasi qo'shilgan kuni u rad
                  etilgan nomzodda ham paydo bo'lib, mavjud bo'lmagan
                  xodimga olib borardi.
                */
                hires.TryGetValue(row.Candidate.Id, out var hiredEmployeeId)
                    ? hiredEmployeeId
                    : null)),
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
    /// <summary>
    /// Rad etilgan, lekin Telegram xabarini hali olmagan nomzodlar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uchta shart birga — aynan shu uchtasi qisman indeks bilan
    /// qoplangan (<c>RecruitingConfigurations</c>). So'rov har necha
    /// o'n soniyada takrorlanadi va odatda BO'SH javob qaytaradi,
    /// shu sababli u arzon bo'lishi shart.
    /// </para>
    /// <para>
    /// Tartib — RAD ETILGAN SANA bo'yicha: eng uzoq kutgan nomzod
    /// birinchi javob oladi. Bir to'da nomzod birga rad etilsa,
    /// ular ham kiritilgan tartibda ketadi.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<RejectionNoticeDto>> ListPendingRejectionNoticesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows =
            from candidate in _context.Candidates.AsNoTracking()
            where candidate.Status == CandidateStatus.Rejected
                && candidate.TelegramChatId != null
                && candidate.RejectionNoticeSentAt == null
            join vacancy in _context.Vacancies.AsNoTracking()
                on candidate.VacancyId equals vacancy.Id into vacancyGroup
            from vacancy in vacancyGroup.DefaultIfEmpty()
            join title in _context.VacancyTitles.AsNoTracking()
                on vacancy.VacancyTitleId equals title.Id into titleGroup
            from title in titleGroup.DefaultIfEmpty()
            orderby candidate.ClosedAt, candidate.CreatedAt
            select new RejectionNoticeDto(
                candidate.Id,
                candidate.TelegramChatId!.Value,
                candidate.TelegramLanguage,
                candidate.FullName.LastName + " " + candidate.FullName.FirstName,

                // Vakansiya o'chirilgan bo'lishi mumkin — xabar baribir
                // ketishi kerak, nomsiz bo'lsa ham.
                title.Name ?? string.Empty);

        return await rows.Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InterviewNoticeDto>> ListPendingInterviewNoticesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        /*
          Nomzodning HOLATI tekshirilmaydi va bu ataylab: taklif voronkada
          turgan nomzodga yuboriladi, lekin HR chaqirgandan keyin uni rad
          etib ulgurgan bo'lsa, xabar baribir ketishi KERAK EMAS. Shu
          sababli yagona shart — nomzod hali yopilmagan (`ClosedAt`
          bo'sh): rad etilgan yoki ishga olingan odamga «sizni suhbatga
          kutamiz» degan xabar eng yomon nosozlik bo'lardi.
        */
        var rows =
            from candidate in _context.Candidates.AsNoTracking()
            where candidate.InterviewAt != null
                && candidate.TelegramChatId != null
                && candidate.InterviewNoticeSentAt == null
                && candidate.ClosedAt == null
            join vacancy in _context.Vacancies.AsNoTracking()
                on candidate.VacancyId equals vacancy.Id into vacancyGroup
            from vacancy in vacancyGroup.DefaultIfEmpty()
            join title in _context.VacancyTitles.AsNoTracking()
                on vacancy.VacancyTitleId equals title.Id into titleGroup
            from title in titleGroup.DefaultIfEmpty()

            // Eng yaqin suhbat birinchi javob oladi: kechikish narxi
            // aynan shu yerda eng yuqori.
            orderby candidate.InterviewAt
            select new InterviewNoticeDto(
                candidate.Id,
                candidate.TelegramChatId!.Value,
                candidate.TelegramLanguage,
                candidate.FullName.LastName + " " + candidate.FullName.FirstName,
                title.Name ?? string.Empty,
                candidate.InterviewAt!.Value);

        return await rows.Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

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

        // Lavozim, bo'lim va firma filtrlari OLIB TASHLANDI: vakansiyada
        // bunday ustunlar endi umuman yo'q (sababi `Vacancy` izohida).
        // Yagona ma'lumotnoma filtri — e'lon nomi.
        if (filter.VacancyTitleId is { } vacancyTitleId)
        {
            vacancies = vacancies.Where(vacancy => vacancy.VacancyTitleId == vacancyTitleId);
        }

        // Chiqarib tashlash SANOQDAN OLDIN: `total` ham, sahifa ham bir
        // xil to'plamdan olinishi kerak. Filtrni xotirada, sahifa
        // olingandan keyin qo'llash «12 tadan 11 tasi ko'rinyapti»
        // degan holatga olib kelardi.
        if (filter.ExcludedVacancyId is { } excludedVacancyId)
        {
            vacancies = vacancies.Where(vacancy => vacancy.Id != excludedVacancyId);
        }

        var joined =
            from vacancy in vacancies
            join title in _context.VacancyTitles.AsNoTracking()
                on vacancy.VacancyTitleId equals title.Id into titleGroup
            from title in titleGroup.DefaultIfEmpty()
            join responsible in _context.Employees.AsNoTracking()
                on vacancy.ResponsibleEmployeeId equals responsible.Id into responsibleGroup
            from responsible in responsibleGroup.DefaultIfEmpty()
            select new
            {
                Vacancy = vacancy,
                TitleName = title != null ? title.Name : null,
                Responsible = responsible,
                CandidateCount = _context.Candidates.Count(c => c.VacancyId == vacancy.Id),
                HiredCount = _context.Candidates
                    .Count(c => c.VacancyId == vacancy.Id && c.Status == CandidateStatus.Hired),
            };

        var total = await joined.CountAsync(cancellationToken).ConfigureAwait(false);

        var ordered = filter.Sort switch
        {
            VacancySortField.Title => filter.Descending
                ? joined.OrderByDescending(row => row.TitleName)
                : joined.OrderBy(row => row.TitleName),

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
                row.Vacancy.VacancyTitleId,
                row.TitleName,
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
