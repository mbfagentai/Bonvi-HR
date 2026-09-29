using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting;

/// <summary>
/// Rekruting agregatlarini DTO'ga o'giradi — ma'lumotnoma NOMLARI bilan.
/// </summary>
/// <remarks>
/// <para>
/// Bitta mapper ikkala agregatga ham xizmat qiladi (vakansiya va
/// nomzod), chunki ular bir xil manbadan nom oladi: bosqichlar va
/// vakansiyaning o'zi (nom vakansiyada yashaydi — sababi <c>Vacancy</c>
/// izohida). Ikkita alohida
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

    private readonly SystemSettingsReader _settings;

    public RecruitingMapper(
        IPipelineStageRepository stages,
        IVacancyRepository vacancies,
        ICandidateRepository candidates,
        IEmploymentRepository employments,
        SystemSettingsReader settings)
    {
        _stages = stages;
        _vacancies = vacancies;
        _candidates = candidates;
        _employments = employments;
        _settings = settings;
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

    /// <summary>Biriktirilgan fayl — qo'shimcha so'rovsiz.</summary>
    /// <remarks>
    /// Alohida (va <c>static</c>) metod, chunki uni IKKI joy ishlatadi:
    /// nomzod kartasi (<see cref="ToDtoAsync(Candidate, CancellationToken)"/>)
    /// va yuklash javobi (<c>UploadCandidateFileCommandHandler</c> — u butun
    /// kartani emas, YARATILGAN faylni qaytaradi). Ikki nusxa yozilsa, bir
    /// kun bittasida maydon qo'shilib, ikkinchisida qolib ketardi.
    ///
    /// Saqlash KALITI qaytarilmaydi — sababi <see cref="CandidateFileDto"/>
    /// izohida.
    /// </remarks>
    public static CandidateFileDto ToDto(CandidateFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new CandidateFileDto(
            file.Id,
            file.FileName,
            file.ContentType,
            file.SizeBytes,
            file.CreatedAt);
    }

    public async Task<VacancyDto> ToDtoAsync(Vacancy vacancy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vacancy);

        // Sanoqlar bitta so'rovda: «3 ta nomzod, 1 tasi olindi» — vakansiya
        // kartasining asosiy raqami va usiz HR ro'yxatga qarab qaror
        // qabul qila olmaydi.
        var (total, hired) = await _candidates
            .CountByVacancyAsync(vacancy.Id, cancellationToken)
            .ConfigureAwait(false);

        return new VacancyDto(
            vacancy.Id,
            vacancy.Title,
            vacancy.Openings,
            vacancy.Status,
            vacancy.OpenedAt,
            vacancy.ClosedAt,
            vacancy.Note,
            total,
            hired,
            await GeneralPoolVacancy.IdAsync(_settings, cancellationToken).ConfigureAwait(false) == vacancy.Id);
    }

    /// <summary>Nomzod kartasi — bosqichlar tarixi, fayllari va izohlari bilan.</summary>
    /// <remarks>
    /// <para>
    /// <paramref name="candidate"/> bosqichlari, FAYLLARI, IZOHLARI va
    /// anketasi bilan yuklangan bo'lishi kerak
    /// (<c>ICandidateRepository.GetWithStagesAsync</c>). Aks holda ular bo'sh,
    /// anketa esa <c>null</c> chiqadi va bu HECH QANDAY xato bermaydi — shu
    /// sababli chaqiruvchilar ataylab bitta metoddan foydalanadi.
    /// </para>
    /// <para>
    /// Fayllar va izohlar uchun YANGI bog'liqlik kerak emas: ular
    /// ma'lumotnoma nomini ko'tarmaydi (fayl turi ham, izoh yorlig'i ham
    /// bazada yo'q — birinchisi umuman mavjud emas, ikkinchisi sanoq va
    /// tarjimasi mijozda). Shu sababli bu yerda faqat proyeksiya bor,
    /// so'rov yo'q.
    /// </para>
    /// <para>
    /// Tartib repozitoriydan KELGANIDEK qoldiriladi (ikkalasi ham
    /// <c>CreatedAt</c> bo'yicha o'sish tartibida): bu yerda qayta saralash
    /// ikkinchi haqiqat manbaini yaratardi va bir kun ikkisi ajralib
    /// ketardi.
    /// </para>
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
                entry.Conclusion,
                entry.RecordedBy,
                entry.RecordedAt,
                entry.UpdatedAt)),
        ];

        IReadOnlyList<CandidateFileDto> files = [.. candidate.Files.Select(ToDto)];

        IReadOnlyList<CandidateNoteDto> notes =
        [
            .. candidate.Notes.Select(note => new CandidateNoteDto(
                note.Id,
                note.Kind,
                note.Text,
                note.CreatedBy,
                note.CreatedAt)),
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
            candidate.NextContactDate,
            candidate.PhotoKey is not null,
            employment?.EmployeeId,
            employment?.Id,
            candidate.InterviewAt,
            candidate.InterviewNoticeSentAt,
            stages,
            files,
            notes,
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
    /// (sababi <see cref="Vacancy"/> izohida) — ya'ni nomzod
    /// kartasida «bu odam nimaga kelgan» degan savolga endi e'londa
    /// turgan matnning O'ZI javob beradi, shtat jadvalidagi rasmiy nom
    /// emas.
    /// </remarks>
    private async Task<string?> VacancyNameAsync(Guid vacancyId, CancellationToken cancellationToken)
    {
        var vacancy = await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false);

        return vacancy?.Title;
    }
}
