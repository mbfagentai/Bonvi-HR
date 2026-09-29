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
/// <param name="Search">
/// Nom bo'yicha qidiruv — qismiy moslik, registr va «қ/к» kabi
/// harflar farqi hisobga olinmaydi (xodimlar ro'yxatidagi qoida).
/// Ilgari o'rnida «vakansiya nomi» ma'lumotnomasi bo'yicha filtr
/// turardi; ma'lumotnoma olib tashlangach (sababi <c>Vacancy</c>
/// izohida) nomni yozib izlash qoldi.
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
    string? Search = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null,
    bool ExcludeGeneralPool = false);

public sealed record GetVacancyQuery(Guid VacancyId);

/// <summary>Yangi vakansiya ochish.</summary>
/// <remarks>
/// Buyruqda kompaniya tuzilmasidan hech narsa YO'Q: e'lon ochish uchun
/// faqat uning nomi kerak. Firma, bo'lim va lavozim — ishga qabul
/// paytidagi qaror (<c>HireCandidateCommand</c>), e'lonning xossasi emas.
/// </remarks>
public sealed record OpenVacancyCommand(
    string? Title,
    int Openings = 1,
    DateOnly? OpenedAt = null,
    string? Note = null);

/// <summary>Vakansiya tafsilotlarini tuzatish. Holat BU YERDA o'zgarmaydi.</summary>
public sealed record UpdateVacancyCommand(
    Guid VacancyId,
    string? Title,
    int Openings = 1,
    DateOnly? OpenedAt = null,
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
                    string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
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
                row.Title,
                row.Openings,
                row.Status,
                row.OpenedAt,
                row.ClosedAt,
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
/// Nomning bandligi SHU YERDA tekshiriladi, domenda emas: domen bazani
/// ko'rmaydi. Yopilgan vakansiya ham nomni band qiladi — HR uni qayta
/// ochishi kerak, yangisini emas (sababi
/// <see cref="IVacancyRepository.TitleTakenAsync"/> izohida).
/// </remarks>
public sealed class OpenVacancyCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public OpenVacancyCommandHandler(
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

    public async Task<Result<VacancyDto>> HandleAsync(
        OpenVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Avval domen: bo'sh nom va o'rin soni uning qoidalari va xabarlari
        // o'sha yerda yozilgan. Bandlik esa faqat yaroqli nom uchun so'raladi.
        var opened = Vacancy.Open(
            command.Title,
            command.OpenedAt ?? _clock.Today,
            command.Openings,
            command.Note);

        if (opened.IsFailure)
        {
            return opened.Error;
        }

        if (await _vacancies
                .TitleTakenAsync(opened.Value.Title, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
        {
            return RecruitingErrors.VacancyTitleTaken;
        }

        _vacancies.Add(opened.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(opened.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateVacancyCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVacancyCommandHandler(
        IVacancyRepository vacancies,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _vacancies = vacancies;
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

        // Bandlik yozuvga TEGISHDAN OLDIN: rad etilgan nom kuzatilayotgan
        // obyektda qolib ketmasligi kerak. Bo'sh nomni esa domen rad etadi
        // (`vakansiya.nom_yoq`) — uning xabari o'sha yerda.
        //
        // FAQAT nom o'zgarganda: ko'chirishdan qolgan bir xil nomli
        // vakansiyalar bor (ilgari bitta nomga bir nechta vakansiya
        // ochilardi). Har saqlashda tekshirilsa, HR ularning o'rin sonini
        // ham, izohini ham o'zgartira olmay qolardi.
        var title = Vacancy.NormalizeTitle(command.Title);
        var renamed = title is not null
            && !string.Equals(title, vacancy.Title, StringComparison.OrdinalIgnoreCase);

        if (renamed
            && await _vacancies
                .TitleTakenAsync(title!, vacancy.Id, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
        {
            return RecruitingErrors.VacancyTitleTaken;
        }

        var updated = vacancy.Update(
            command.Title,
            command.OpenedAt ?? vacancy.OpenedAt,
            command.Openings,
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

    public async Task<Result<VacancyDto>> HandleAsync(
        ReopenVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Faqat OCHIQlar orasidan: ko'chirishdan qolgan bir xil nomli
        // yopiq vakansiyalar bir-birini to'smasin, lekin ikkitasi bir
        // vaqtda ochiq turmasin — bot nomzodga ikkita bir xil tugma
        // ko'rsatardi (izohi `IVacancyRepository.TitleTakenAsync` da).
        //
        // Allaqachon ochiq vakansiya tekshirilmaydi: qayta ochish unda hech
        // narsa yozmaydi va zararsiz bo'lishi va'da qilingan (`Vacancy.Reopen`
        // izohi) — eskirgan ro'yxatdan kelgan takroriy bosish 409 olmasin.
        var vacancy = await _vacancies.GetByIdAsync(command.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is { Status: VacancyStatus.Closed }
            && await _vacancies
                .TitleTakenAsync(vacancy.Title, vacancy.Id, openOnly: true, cancellationToken)
                .ConfigureAwait(false))
        {
            return RecruitingErrors.VacancyTitleTaken;
        }

        return await ApplyAsync(command.VacancyId, vacancy => vacancy.Reopen(), cancellationToken)
            .ConfigureAwait(false);
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
/// (<c>OpenVacancyCommandHandler</c> dagi nom bandligi bilan bir xil
/// bo'linish).
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
