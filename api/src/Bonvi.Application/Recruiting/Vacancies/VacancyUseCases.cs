using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Vacancies;

/// <summary>
/// Vakansiyalar ro'yxati.
/// </summary>
/// <param name="Status">
/// Holat: <c>Open</c> | <c>Closed</c>. Bo'sh — hammasi. <c>Paused</c>
/// endi YO'Q (sababi <c>VacancyStatus</c> izohida) — eski havolalardagi
/// <c>?status=Paused</c> «vakansiya.holat_notogri» bilan 400 qaytadi va
/// bu ataylab: jimgina butun ro'yxatni ko'rsatish HR ga filtr ishlayapti
/// degan yolg'on taassurot berardi.
/// Satr, sanoq emas — <c>SearchEmployeesQuery.Status</c> dagi bilan bir xil
/// sabab: minimal API sanoqni registrga sezgir bog'laydi va bo'sh qiymatni
/// 400 bilan rad etadi, holbuki «filtr tozalandi» oddiy hol.
/// </param>
/// <param name="VacancyTitleId">
/// Vakansiya nomi bo'yicha filtr. Lavozim, bo'lim va firma filtrlari
/// OLIB TASHLANDI — vakansiyada bunday maydonlar yo'q (sababi
/// <c>Vacancy</c> izohida).
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// Saralash: <c>openedAt</c> (sukut, yangidan eskiga) yoki <c>title</c>.
/// Teskari tartib uchun oldiga <c>-</c> yoki oxiriga <c>:desc</c>.
/// </param>
/// <param name="ExcludeGeneralPool">
/// «Umumiy anketa» vakansiyasini ro'yxatdan chiqarib tashlash.
/// <para>
/// ⭐ Buni FAQAT bot so'raydi: nomzodga vakansiyalar TANLOV bo'lib
/// ko'rsatiladi, «Umumiy anketa» esa tanlanadigan ish o'rni emas —
/// u vakansiyasiz kelgan anketa tushadigan joy
/// (<c>SystemSettingKeys.GeneralPoolVacancyId</c>).
/// </para>
/// <para>
/// <b>Nega HR uchun sukut bo'yicha O'CHIQ.</b> HR uchun u eng oddiy
/// vakansiya: voronka, bosqichlar va rad etish unda ham ishlaydi,
/// nomzodni unga qo'lda kiritish ham, undan boshqasiga ko'chirish ham
/// mumkin bo'lishi kerak. Filtrni har doim yoqib qo'yish esa aynan shu
/// ekranlarni jimgina ishlamaydigan qilib qo'yardi — HR nomzodni
/// «Umumiy anketa» ga kirita olmay qolardi va sababini ko'rmasdi.
/// </para>
/// </param>
public sealed record ListVacanciesQuery(
    string? Status = null,
    Guid? VacancyTitleId = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null,
    bool ExcludeGeneralPool = false);

public sealed record GetVacancyQuery(Guid VacancyId);

/// <summary>Yangi vakansiya ochish.</summary>
/// <remarks>
/// Buyruqda kompaniya tuzilmasidan hech narsa YO'Q: e'lon ochish uchun
/// faqat uning nomi kerak. Firma, bo'lim va lavozim — ishga qabul
/// paytidagi qaror (<c>AcceptJobOfferCommand</c>), e'lonning xossasi emas.
/// </remarks>
public sealed record OpenVacancyCommand(
    Guid VacancyTitleId,
    int Openings = 1,
    DateOnly? OpenedAt = null,
    Guid? ResponsibleEmployeeId = null,
    string? Note = null);

/// <summary>Vakansiya tafsilotlarini tuzatish. Holat BU YERDA o'zgarmaydi.</summary>
public sealed record UpdateVacancyCommand(
    Guid VacancyId,
    Guid VacancyTitleId,
    int Openings = 1,
    DateOnly? OpenedAt = null,
    Guid? ResponsibleEmployeeId = null,
    string? Note = null);

/// <summary>Vakansiyani yopish.</summary>
public sealed record CloseVacancyCommand(Guid VacancyId, DateOnly? ClosedAt = null, string? Reason = null);

/// <summary>Yopilgan vakansiyani qayta ochish.</summary>
/// <remarks>
/// Ilgari <c>ResumeVacancyCommand</c> («davom ettirish») deb atalardi va
/// nom to'xtatilgan holatdan qolgan edi. To'xtatish yo'q, ya'ni «davom
/// ettiriladigan» narsa ham yo'q: bu buyruq faqat YOPIQNI qayta ochadi va
/// nomi shuni aytishi kerak.
/// </remarks>
public sealed record ReopenVacancyCommand(Guid VacancyId);

/// <summary>Vakansiyani yumshoq o'chirish (xato kiritilgan yozuv).</summary>
/// <remarks>
/// Faqat NOMZODI YO'Q vakansiya o'chiriladi — sababi
/// <c>DeleteVacancyCommandHandler</c> izohida.
/// </remarks>
public sealed record DeleteVacancyCommand(Guid VacancyId);

public sealed class ListVacanciesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi — xodimlar ro'yxatidagi bilan bir xil.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Eng katta sahifa hajmi — bitta so'rov bilan butun bazani tortishga qarshi.</summary>
    public const int MaxPageSize = 200;

    private readonly IRecruitingReadModel _readModel;
    private readonly SystemSettingsReader _settings;

    public ListVacanciesQueryHandler(IRecruitingReadModel readModel, SystemSettingsReader settings)
    {
        _readModel = readModel;
        _settings = settings;
    }

    public async Task<Result<PagedResult<VacancyDto>>> HandleAsync(
        ListVacanciesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var status = RecruitingQueryParsing.ParseVacancyStatus(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var (sort, descending) = RecruitingQueryParsing.ParseVacancySort(query.Sort);

        var excluded = query.ExcludeGeneralPool
            ? await GeneralPoolVacancyIdAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var found = await _readModel
            .ListVacanciesAsync(
                new VacancyListFilter(
                    status.Value,
                    query.VacancyTitleId,
                    page,
                    pageSize,
                    sort,
                    descending,
                    excluded),
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<VacancyDto> items =
        [
            .. found.Items.Select(row => new VacancyDto(
                row.Id,
                row.VacancyTitleId,
                row.VacancyTitleName,
                row.Openings,
                row.Status,
                row.OpenedAt,
                row.ClosedAt,
                row.ResponsibleEmployeeId,
                row.ResponsibleName,
                row.Note,
                row.CandidateCount,
                row.HiredCount)),
        ];

        var pageCount = (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<VacancyDto>(items, page, pageSize, found.Total, pageCount));
    }

    /// <summary>
    /// «Umumiy anketa» vakansiyasi. Sozlanmagan bo'lsa — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Bu yerdan XATO chiqmaydi va chiqmasligi ham kerak.</b>
    /// Sozlama bo'sh bo'lishi (urug'lantirish hali o'tmagan) yoki
    /// buzilgan bo'lishi (bazaga qo'lda aralashilgan) mumkin — ikkala
    /// holatda ham ro'yxat AVVALGIDEK to'liq qaytadi. Botning yagona
    /// ish yo'li shu so'rovdan o'tadi: filtr tufayli yiqilgan ro'yxat
    /// «Umumiy anketa» ko'rinib qolganidan ko'ra ancha qimmatga
    /// tushardi.
    /// </remarks>
    private async Task<Guid?> GeneralPoolVacancyIdAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        return Guid.TryParse(settings.Hidden(SystemSettingKeys.GeneralPoolVacancyId), out var id)
            ? id
            : null;
    }
}

public sealed class GetVacancyQueryHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly RecruitingMapper _mapper;

    public GetVacancyQueryHandler(IVacancyRepository vacancies, RecruitingMapper mapper)
    {
        _vacancies = vacancies;
        _mapper = mapper;
    }

    public async Task<Result<VacancyDto>> HandleAsync(
        GetVacancyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vacancy = await _vacancies.GetByIdAsync(query.VacancyId, cancellationToken).ConfigureAwait(false);

        return vacancy is null
            ? RecruitingErrors.VacancyNotFound
            : await _mapper.ToDtoAsync(vacancy, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Vakansiya ochadi.
/// </summary>
/// <remarks>
/// Ma'lumotnoma havolalari SHU YERDA tekshiriladi, domenda emas: domen
/// bazani ko'rmaydi. Tekshiruvsiz qoldirilsa, mavjud bo'lmagan nom bilan
/// vakansiya ochilib, ro'yxatda nomsiz qator paydo bo'lardi.
/// </remarks>
public sealed class OpenVacancyCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly VacancyReferenceGuard _references;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public OpenVacancyCommandHandler(
        IVacancyRepository vacancies,
        VacancyReferenceGuard references,
        RecruitingMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _vacancies = vacancies;
        _references = references;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VacancyDto>> HandleAsync(
        OpenVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var references = await _references
            .CheckAsync(command.VacancyTitleId, command.ResponsibleEmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (references.IsFailure)
        {
            return references.Error;
        }

        var opened = Vacancy.Open(
            command.VacancyTitleId,
            command.OpenedAt ?? _clock.Today,
            command.Openings,
            command.ResponsibleEmployeeId,
            command.Note);

        if (opened.IsFailure)
        {
            return opened.Error;
        }

        _vacancies.Add(opened.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(opened.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateVacancyCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly VacancyReferenceGuard _references;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVacancyCommandHandler(
        IVacancyRepository vacancies,
        VacancyReferenceGuard references,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _vacancies = vacancies;
        _references = references;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VacancyDto>> HandleAsync(
        UpdateVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vacancy = await _vacancies.GetByIdAsync(command.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is null)
        {
            return RecruitingErrors.VacancyNotFound;
        }

        var references = await _references
            .CheckAsync(command.VacancyTitleId, command.ResponsibleEmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (references.IsFailure)
        {
            return references.Error;
        }

        var updated = vacancy.Update(
            command.VacancyTitleId,
            command.OpenedAt ?? vacancy.OpenedAt,
            command.Openings,
            command.ResponsibleEmployeeId,
            command.Note);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(vacancy, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Yopish va qayta ochish — ikkala amal bitta handlerda.</summary>
/// <remarks>
/// Ular bir xil qadamlardan iborat (yukla → domen metodini chaqir → saqla)
/// va faqat chaqiriladigan metodda farq qiladi. Ikkita alohida sinf o'sha
/// yigirma qatorni ikki marta takrorlardi.
/// </remarks>
public sealed class ChangeVacancyStateCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeVacancyStateCommandHandler(
        IVacancyRepository vacancies,
        RecruitingMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _vacancies = vacancies;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<VacancyDto>> HandleAsync(
        ReopenVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ApplyAsync(command.VacancyId, vacancy => vacancy.Reopen(), cancellationToken);
    }

    public Task<Result<VacancyDto>> HandleAsync(
        CloseVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyAsync(
            command.VacancyId,
            vacancy => vacancy.Close(command.ClosedAt ?? _clock.Today, command.Reason),
            cancellationToken);
    }

    private async Task<Result<VacancyDto>> ApplyAsync(
        Guid vacancyId,
        Func<Vacancy, Result> change,
        CancellationToken cancellationToken)
    {
        var vacancy = await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is null)
        {
            return RecruitingErrors.VacancyNotFound;
        }

        var changed = change(vacancy);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(vacancy, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Vakansiyani o'chiradi — FAQAT nomzodi yo'q bo'lsa.
/// </summary>
/// <remarks>
/// <para>
/// <b>NEGA nomzodi bor vakansiya o'chirilmaydi.</b> Bunday vakansiya —
/// TARIX. Nomzodning yozuvi «bu odam nimaga kelgan» degan savolga javob
/// beradi (<c>Vacancy</c> domen izohi) va vakansiya ro'yxatdan g'oyib
/// bo'lsa, o'sha javob yetim qolardi: nomzod kartasida nom hali ham
/// ko'rinardi, lekin unga o'tib bo'lmasdi, uni filtrlab bo'lmasdi va
/// «bu odam qayerga ariza bergan edi?» degan savol javobsiz qolardi.
/// </para>
/// <para>
/// <b>NEGA nomzodi YO'Q vakansiya esa o'chiriladi.</b> U ko'pincha
/// shunchaki xato yozuv — imlo xatosi bilan ochilgani, sinov uchun
/// ochilgani, ikki marta ochilgani. Uni abadiy «Yopiq» ro'yxatida
/// saqlash hech qanday savolga javob bermaydi, faqat shovqin qo'shadi.
/// </para>
/// <para>
/// <b>NEGA tekshiruv BU YERDA, domenda emas.</b> «Nechta nomzod bor» —
/// boshqa agregatdagi fakt, domen esa bazani ko'rmaydi
/// (<c>VacancyReferenceGuard</c> dagi bilan bir xil bo'linish).
/// </para>
/// <para>
/// <b>NEGA faqat frontend'da yashirish yetarli emas.</b> Tugmani
/// chizmaslik — qulaylik, himoya emas: <c>DELETE</c> so'rovini qo'lda
/// ham, eskirgan ro'yxatdan ham yuborish mumkin (nomzod ro'yxat
/// yuklangandan keyin qo'shilgan bo'lishi mumkin). Qat'iy javob shu
/// yerda beriladi.
/// </para>
/// </remarks>
public sealed class DeleteVacancyCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly ICandidateRepository _candidates;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteVacancyCommandHandler(
        IVacancyRepository vacancies,
        ICandidateRepository candidates,
        IUnitOfWork unitOfWork)
    {
        _vacancies = vacancies;
        _candidates = candidates;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vacancy = await _vacancies.GetByIdAsync(command.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is null)
        {
            return RecruitingErrors.VacancyNotFound;
        }

        // Sanoq allaqachon mavjud metoddan olinadi (ro'yxatdagi
        // «candidateCount» ham shundan keladi) — bu yerda yangi so'rov
        // yozish ikkita «nomzodlar soni» ta'rifini hosil qilardi.
        var (total, _) = await _candidates
            .CountByVacancyAsync(command.VacancyId, cancellationToken)
            .ConfigureAwait(false);

        if (total > 0)
        {
            return RecruitingErrors.VacancyHasCandidates;
        }

        // Yumshoq o'chirish (yozuv bazada qoladi) — global filtr uni
        // ro'yxatlardan yashiradi. Nomzodlari yo'q, ya'ni bu yerda
        // yetim qoladigan havola ham yo'q.
        _vacancies.Remove(vacancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>
/// Vakansiyaning ma'lumotnoma havolalarini tekshiradi.
/// </summary>
/// <remarks>
/// Ochish va tahrirlash AYNAN bir xil tekshiruvni talab qiladi. Ikki joyda
/// yozilsa, biri o'zgarganda ikkinchisi eskirib qolardi — loyihadagi
/// <c>flags</c>/<c>activeFlags</c> xatosining aynan o'zi.
/// </remarks>
public sealed class VacancyReferenceGuard
{
    private readonly IReferenceRepository<VacancyTitle> _titles;
    private readonly IEmployeeRepository _employees;

    public VacancyReferenceGuard(IReferenceRepository<VacancyTitle> titles, IEmployeeRepository employees)
    {
        _titles = titles;
        _employees = employees;
    }

    /// <summary>Vakansiya nomi va mas'ul xodim haqiqatan mavjudmi.</summary>
    /// <remarks>
    /// Ro'yxat ilgari to'rtta edi (lavozim, firma, bo'lim, mas'ul) va
    /// endi ikkita: vakansiyada kompaniya tuzilmasidan hech narsa
    /// qolmadi (sababi <c>Vacancy</c> izohida). Qolgan uchtasi
    /// yo'qolmadi — ular ishga qabul oqimida tekshiriladi
    /// (<c>AcceptJobOfferCommandHandler</c> va uning chaqiradigan
    /// <c>HireEmployeeCommandHandler</c> i).
    /// </remarks>
    public async Task<Result> CheckAsync(
        Guid vacancyTitleId,
        Guid? responsibleEmployeeId,
        CancellationToken cancellationToken = default)
    {
        // Bo'sh identifikator BU YERDA tekshirilmaydi: «nom ko'rsatilishi
        // shart» — domenning qoidasi va uning xabari (`vakansiya.nom_yoq`)
        // domenda yozilgan. Agar bu yerda ham tekshirilsa, foydalanuvchi
        // bo'sh maydon uchun «nom topilmadi» degan chalg'ituvchi javob
        // olardi va bitta qoida ikki joyda yashab qolardi (`RecruitingErrors`
        // izohidagi qat'iy bo'linish).
        if (vacancyTitleId != Guid.Empty
            && !await _titles.ExistsAsync(vacancyTitleId, cancellationToken).ConfigureAwait(false))
        {
            return RecruitingErrors.VacancyTitleNotFound;
        }

        if (responsibleEmployeeId is { } responsible
            && !await _employees.ExistsAsync(responsible, cancellationToken).ConfigureAwait(false))
        {
            return RecruitingErrors.ResponsibleNotFound;
        }

        return Result.Success();
    }
}
