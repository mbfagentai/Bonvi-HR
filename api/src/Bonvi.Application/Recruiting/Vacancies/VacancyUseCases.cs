using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Vacancies;

/// <summary>
/// Vakansiyalar ro'yxati.
/// </summary>
/// <param name="Status">
/// Holat: <c>Open</c> | <c>Paused</c> | <c>Closed</c>. Bo'sh — hammasi.
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
public sealed record ListVacanciesQuery(
    string? Status = null,
    Guid? VacancyTitleId = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

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

/// <summary>Vakansiyani vaqtincha to'xtatish.</summary>
public sealed record PauseVacancyCommand(Guid VacancyId, string? Reason = null);

/// <summary>To'xtatilgan yoki yopilgan vakansiyani qayta ochish.</summary>
public sealed record ResumeVacancyCommand(Guid VacancyId);

/// <summary>Vakansiyani yopish.</summary>
public sealed record CloseVacancyCommand(Guid VacancyId, DateOnly? ClosedAt = null, string? Reason = null);

/// <summary>Vakansiyani yumshoq o'chirish (xato kiritilgan yozuv).</summary>
public sealed record DeleteVacancyCommand(Guid VacancyId);

public sealed class ListVacanciesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi — xodimlar ro'yxatidagi bilan bir xil.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Eng katta sahifa hajmi — bitta so'rov bilan butun bazani tortishga qarshi.</summary>
    public const int MaxPageSize = 200;

    private readonly IRecruitingReadModel _readModel;

    public ListVacanciesQueryHandler(IRecruitingReadModel readModel) => _readModel = readModel;

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

        var found = await _readModel
            .ListVacanciesAsync(
                new VacancyListFilter(
                    status.Value,
                    query.VacancyTitleId,
                    page,
                    pageSize,
                    sort,
                    descending),
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

/// <summary>To'xtatish, qayta ochish va yopish — uchala amal bitta handlerda.</summary>
/// <remarks>
/// Ular bir xil qadamlardan iborat (yukla → domen metodini chaqir → saqla)
/// va faqat chaqiriladigan metodda farq qiladi. Uchta alohida sinf o'sha
/// yigirma qatorni uch marta takrorlardi.
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
        PauseVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ApplyAsync(command.VacancyId, vacancy => vacancy.Pause(command.Reason), cancellationToken);
    }

    public Task<Result<VacancyDto>> HandleAsync(
        ResumeVacancyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ApplyAsync(command.VacancyId, vacancy => vacancy.Resume(), cancellationToken);
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

public sealed class DeleteVacancyCommandHandler
{
    private readonly IVacancyRepository _vacancies;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteVacancyCommandHandler(IVacancyRepository vacancies, IUnitOfWork unitOfWork)
    {
        _vacancies = vacancies;
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

        // Yumshoq o'chirish: nomzodlar unga havola qilib turibdi va ular
        // yo'qolmasligi kerak. O'chirilgan vakansiya ro'yxatlarda
        // ko'rinmaydi, lekin nomzod kartasidagi nom joyida qoladi.
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
