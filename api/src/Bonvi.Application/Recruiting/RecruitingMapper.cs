using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting;

/// <summary>
/// Rekruting agregatlarini DTO'ga o'giradi — ma'lumotnoma NOMLARI bilan.
/// </summary>
/// <remarks>
/// <para>
/// Bitta mapper uchala agregatga xizmat qiladi (vakansiya, nomzod,
/// taklif), chunki ular bir xil manbadan nom oladi: lavozim, firma va
/// bo'lim ma'lumotnomasi hamda vakansiya. Uchta alohida mapper bu
/// bog'liqliklarni uch marta takrorlardi va nomlar bir kun uch xil
/// ko'rinishda chiqib qolardi.
/// </para>
/// <para>
/// <b>Bosqich nomlari BITTA so'rovda olinadi.</b> Voronkada 6-10 ta bosqich
/// bor va nomzodning tarixida ular takrorlanadi. Har bir yozuv uchun
/// alohida so'rov yuborish 10 qatorlik tarix uchun 10 ta ortiqcha
/// murojaat bo'lardi. Faolsizlantirilganlar ham olinadi — eski yozuvda
/// unga havola qolgan va nomsiz qator «ma'lumot yo'qolgan» degan
/// taassurot berardi.
/// </para>
/// </remarks>
public sealed class RecruitingMapper
{
    private readonly IPipelineStageRepository _stages;
    private readonly IVacancyRepository _vacancies;
    private readonly IJobOfferRepository _offers;
    private readonly ICandidateRepository _candidates;
    private readonly IEmploymentRepository _employments;
    private readonly IEmployeeRepository _employees;
    private readonly CatalogNameResolver _names;

    public RecruitingMapper(
        IPipelineStageRepository stages,
        IVacancyRepository vacancies,
        IJobOfferRepository offers,
        ICandidateRepository candidates,
        IEmploymentRepository employments,
        IEmployeeRepository employees,
        CatalogNameResolver names)
    {
        _stages = stages;
        _vacancies = vacancies;
        _offers = offers;
        _candidates = candidates;
        _employments = employments;
        _employees = employees;
        _names = names;
    }

    /// <summary>Bosqich — ma'lumotnoma yozuvi, qo'shimcha so'rovsiz.</summary>
    public static PipelineStageDto ToDto(PipelineStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        return new PipelineStageDto(stage.Id, stage.Name, stage.IsActive, stage.Note, stage.SortOrder);
    }

    public async Task<VacancyDto> ToDtoAsync(Vacancy vacancy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vacancy);

        var position = await _names.PositionAsync(vacancy.PositionId, cancellationToken).ConfigureAwait(false);
        var legalEntity = await _names.LegalEntityAsync(vacancy.LegalEntityId, cancellationToken).ConfigureAwait(false);
        var department = await _names.DepartmentAsync(vacancy.DepartmentId, cancellationToken).ConfigureAwait(false);

        var responsible = vacancy.ResponsibleEmployeeId is { } responsibleId
            ? await _employees.GetByIdAsync(responsibleId, cancellationToken).ConfigureAwait(false)
            : null;

        // Sanoqlar bitta so'rovda: «3 ta nomzod, 1 tasi olindi» — vakansiya
        // kartasining asosiy raqami va usiz HR ro'yxatga qarab qaror
        // qabul qila olmaydi.
        var (total, hired) = await _candidates
            .CountByVacancyAsync(vacancy.Id, cancellationToken)
            .ConfigureAwait(false);

        return new VacancyDto(
            vacancy.Id,
            vacancy.PositionId,
            position,
            vacancy.LegalEntityId,
            legalEntity,
            vacancy.DepartmentId,
            department,
            vacancy.Openings,
            vacancy.Status,
            vacancy.OpenedAt,
            vacancy.ClosedAt,
            vacancy.ResponsibleEmployeeId,
            responsible?.FullName.Full,
            vacancy.Note,
            total,
            hired);
    }

    public async Task<JobOfferDto> ToDtoAsync(JobOffer offer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var candidate = await _candidates.GetByIdAsync(offer.CandidateId, cancellationToken).ConfigureAwait(false);
        var vacancyName = await VacancyNameAsync(offer.VacancyId, cancellationToken).ConfigureAwait(false);

        return ToDto(offer, candidate?.FullName.Full, vacancyName);
    }

    /// <summary>Nomzod kartasi — bosqichlar tarixi va takliflari bilan.</summary>
    /// <remarks>
    /// <paramref name="candidate"/> bosqichlari bilan yuklangan bo'lishi
    /// kerak (<c>ICandidateRepository.GetWithStagesAsync</c>). Aks holda
    /// tarix bo'sh chiqadi va bu HECH QANDAY xato bermaydi — shu sababli
    /// chaqiruvchilar ataylab bitta metoddan foydalanadi.
    /// </remarks>
    public async Task<CandidateDto> ToDtoAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var vacancyName = await VacancyNameAsync(candidate.VacancyId, cancellationToken).ConfigureAwait(false);

        var stageNames = await StageNamesAsync(cancellationToken).ConfigureAwait(false);

        var offers = await _offers.GetByCandidateAsync(candidate.Id, cancellationToken).ConfigureAwait(false);

        // «Bu nomzod qaysi xodimga aylandi» — ishga qabul HODISASIDAN
        // (sababi `EmploymentEvent.CandidateId` izohida). Faqat ishga
        // olinganlar uchun so'raladi: qolganlarida javob doim bo'sh
        // bo'lardi va bu ortiqcha so'rov bo'lib qolardi.
        var employment = candidate.Status == CandidateStatus.Hired
            ? await _employments.FindByCandidateAsync(candidate.Id, cancellationToken).ConfigureAwait(false)
            : null;

        IReadOnlyList<CandidateStageEntryDto> stages =
        [
            .. candidate.StageEntries.Select(entry => new CandidateStageEntryDto(
                entry.Id,
                entry.StageId,
                stageNames.GetValueOrDefault(entry.StageId),
                entry.Date,
                entry.Outcome,
                entry.Conclusion,
                entry.RecordedBy,
                entry.RecordedAt)),
        ];

        IReadOnlyList<JobOfferDto> offerDtos =
        [
            .. offers.Select(offer => ToDto(offer, candidate.FullName.Full, vacancyName)),
        ];

        return new CandidateDto(
            candidate.Id,
            candidate.FullName.LastName,
            candidate.FullName.FirstName,
            candidate.FullName.MiddleName,
            candidate.FullName.Full,
            candidate.BirthDate,
            candidate.Phone?.Value,
            candidate.Address,
            candidate.Speciality,
            candidate.VacancyId,
            vacancyName,
            candidate.CurrentStageId,
            candidate.CurrentStageId is { } stageId ? stageNames.GetValueOrDefault(stageId) : null,
            candidate.Status,
            candidate.Source,
            candidate.Note,
            candidate.ClosedAt,
            candidate.ClosingNote,
            candidate.ResumeFileName,
            candidate.ResumeContentType,
            candidate.ResumeSizeBytes,
            employment?.EmployeeId,
            employment?.Id,
            stages,
            offerDtos);
    }

    /// <summary>Bosqich nomlari: identifikator → nom.</summary>
    /// <remarks>
    /// Ro'yxat kichik (voronkada 6-10 bosqich), shu sababli butunlay
    /// o'qiladi va keshlanmaydi: kesh bu yerda hech narsa yutmaydi, lekin
    /// «HR bosqich nomini o'zgartirdi, ekranda eskisi turibdi» degan
    /// tushunarsiz holatni tug'dirardi.
    /// </remarks>
    private async Task<Dictionary<Guid, string>> StageNamesAsync(CancellationToken cancellationToken)
    {
        var stages = await _stages.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);

        return stages.ToDictionary(stage => stage.Id, stage => stage.Name);
    }

    /// <summary>
    /// Vakansiyaning ko'rinadigan nomi — uning LAVOZIMI.
    /// </summary>
    /// <remarks>
    /// Vakansiyada alohida sarlavha yo'q (sababi <see cref="Vacancy"/>
    /// izohida), shu sababli ekranda u lavozim nomi bilan ataladi.
    /// </remarks>
    private async Task<string?> VacancyNameAsync(Guid vacancyId, CancellationToken cancellationToken)
    {
        var vacancy = await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false);

        return vacancy is null
            ? null
            : await _names.PositionAsync(vacancy.PositionId, cancellationToken).ConfigureAwait(false);
    }

    private static JobOfferDto ToDto(JobOffer offer, string? candidateName, string? vacancyName)
        => new(
            offer.Id,
            offer.CandidateId,
            candidateName,
            offer.VacancyId,
            vacancyName,
            new MoneyDto(offer.Salary.Amount, offer.Salary.Currency, offer.Salary.Formatted),
            offer.StartDate,
            offer.SentAt,
            offer.ExpiresAt,
            offer.Status,
            offer.RespondedAt,
            offer.ResponseNote,
            offer.Note);
}
