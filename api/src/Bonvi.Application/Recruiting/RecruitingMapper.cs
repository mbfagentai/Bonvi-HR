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
/// Bitta mapper ikkala agregatga ham xizmat qiladi (vakansiya va
/// nomzod), chunki ular bir xil manbadan nom oladi: vakansiya nomlari
/// ma'lumotnomasi, bosqichlar va vakansiyaning o'zi. Ikkita alohida
/// mapper bu bog'liqliklarni ikki marta takrorlardi va nomlar bir kun
/// ikki xil ko'rinishda chiqib qolardi.
/// </para>
/// <para>
/// <b>Lavozim/bo'lim/firma nomlari bu yerda ENDI KERAK EMAS.</b>
/// Vakansiyada bunday havolalar qolmadi (sababi <c>Vacancy</c> izohida),
/// shu sababli <c>CatalogNameResolver</c> ga bog'liqlik ham olib
/// tashlandi: mapper faqat o'ziga haqiqatan kerak bo'lgan narsani
/// so'raydi.
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
    private readonly ICandidateRepository _candidates;
    private readonly IEmploymentRepository _employments;
    private readonly IEmployeeRepository _employees;
    private readonly IReferenceRepository<VacancyTitle> _titles;

    public RecruitingMapper(
        IPipelineStageRepository stages,
        IVacancyRepository vacancies,
        ICandidateRepository candidates,
        IEmploymentRepository employments,
        IEmployeeRepository employees,
        IReferenceRepository<VacancyTitle> titles)
    {
        _stages = stages;
        _vacancies = vacancies;
        _candidates = candidates;
        _employments = employments;
        _employees = employees;
        _titles = titles;
    }

    /// <summary>Bosqich — ma'lumotnoma yozuvi, qo'shimcha so'rovsiz.</summary>
    public static PipelineStageDto ToDto(PipelineStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        return new PipelineStageDto(
            stage.Id,
            stage.Name,
            stage.IsActive,
            stage.Note,
            stage.SortOrder,
            stage.InvitesToInterview);
    }

    public async Task<VacancyDto> ToDtoAsync(Vacancy vacancy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vacancy);

        var title = await TitleNameAsync(vacancy.VacancyTitleId, cancellationToken).ConfigureAwait(false);

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
            vacancy.VacancyTitleId,
            title,
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

    /// <summary>Nomzod kartasi — bosqichlar tarixi bilan.</summary>
    /// <remarks>
    /// <paramref name="candidate"/> bosqichlari VA anketasi bilan yuklangan
    /// bo'lishi kerak (<c>ICandidateRepository.GetWithStagesAsync</c>). Aks
    /// holda tarix bo'sh, anketa esa <c>null</c> chiqadi va bu HECH QANDAY
    /// xato bermaydi — shu sababli chaqiruvchilar ataylab bitta metoddan
    /// foydalanadi.
    /// </remarks>
    public async Task<CandidateDto> ToDtoAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var vacancyName = await VacancyNameAsync(candidate.VacancyId, cancellationToken).ConfigureAwait(false);

        var stageNames = await StageNamesAsync(cancellationToken).ConfigureAwait(false);

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
            candidate.PhotoKey is not null,
            employment?.EmployeeId,
            employment?.Id,
            candidate.InterviewAt,
            candidate.InterviewNoticeSentAt,
            stages,
            ToDto(candidate.Questionnaire));
    }

    /// <summary>Anketa — qo'shimcha so'rovsiz, nomzod bilan birga yuklangan.</summary>
    /// <remarks>
    /// Anketasi yo'q nomzodda <c>null</c> qaytadi va interfeys bo'limni
    /// umuman chizmaydi. Bo'sh qiymatli blok qaytarish YARAMAYDI: u
    /// «anketa bor, lekin javoblar yo'q» degan ma'no berardi, holbuki
    /// anketaning O'ZI yo'q (HR qo'lda kiritgan nomzod).
    /// </remarks>
    private static CandidateQuestionnaireDto? ToDto(CandidateQuestionnaire? questionnaire)
        => questionnaire is null
            ? null
            : new CandidateQuestionnaireDto(
                questionnaire.Education,
                questionnaire.WorkExperience,
                questionnaire.MaritalStatus,
                questionnaire.RussianLevel,
                questionnaire.OtherLanguages,
                questionnaire.OfficeSkills,
                questionnaire.LastSalary,
                questionnaire.ExtraNote,
                questionnaire.HasDriverLicense,
                questionnaire.DriverLicenseCategories,
                questionnaire.ExpectedSalary,
                questionnaire.ReferralSource,
                questionnaire.OfferAcceptedAt);

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
    /// Vakansiyaning ko'rinadigan nomi — uning E'LON NOMI.
    /// </summary>
    /// <remarks>
    /// Ilgari bu yerda LAVOZIM nomi qaytardi, chunki vakansiyaning o'z
    /// nomi yo'q edi. Endi u bor va u aynan shu maqsad uchun yaratilgan
    /// (sababi <see cref="VacancyTitle"/> izohida) — ya'ni nomzod
    /// kartasida «bu odam nimaga kelgan» degan savolga endi e'londa
    /// turgan matnning O'ZI javob beradi, shtat jadvalidagi rasmiy nom
    /// emas.
    /// </remarks>
    private async Task<string?> VacancyNameAsync(Guid vacancyId, CancellationToken cancellationToken)
    {
        var vacancy = await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false);

        return vacancy is null
            ? null
            : await TitleNameAsync(vacancy.VacancyTitleId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Vakansiya nomi ma'lumotnomasidan matnni oladi.</summary>
    /// <remarks>
    /// Faolsizlantirilgan yozuv ham topiladi (repozitoriy faollikka
    /// qaramaydi): eski vakansiyada unga havola qolgan va nomsiz qator
    /// «ma'lumot yo'qolgan» degan taassurot berardi — bosqich nomlari
    /// bilan aynan bir xil qoida.
    /// </remarks>
    private async Task<string?> TitleNameAsync(Guid titleId, CancellationToken cancellationToken)
    {
        var title = await _titles.GetByIdAsync(titleId, cancellationToken).ConfigureAwait(false);

        return title?.Name;
    }
}
