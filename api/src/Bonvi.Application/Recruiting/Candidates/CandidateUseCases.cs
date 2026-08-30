using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Nomzodlar ro'yxati — rekrutingning asosiy ekrani.
/// </summary>
/// <param name="Search">F.I.Sh. yoki telefon bo'yicha qidiruv.</param>
/// <param name="VacancyId">Vakansiya bo'yicha filtr.</param>
/// <param name="StageId">Bosqich bo'yicha filtr — voronka ustunini chizish uchun.</param>
/// <param name="Status">
/// <c>InProgress</c> | <c>Rejected</c> | <c>Hired</c> | <c>Withdrawn</c>.
/// Bo'sh — hammasi. Voronka taxtasi <c>InProgress</c> bilan so'raydi:
/// ishga olingan va rad etilganlar u yerda faol bo'lib turmasligi kerak.
/// </param>
/// <param name="Source">
/// <c>Manual</c> | <c>TelegramBot</c>. «Bot qancha nomzod keltirdi» degan
/// savolning javobi shu filtr orqali olinadi.
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// <c>createdAt</c> (sukut, yangidan eskiga), <c>name</c>, <c>stage</c>.
/// Teskari tartib: oldiga <c>-</c> yoki oxiriga <c>:desc</c>.
/// </param>
public sealed record SearchCandidatesQuery(
    string? Search = null,
    Guid? VacancyId = null,
    Guid? StageId = null,
    string? Status = null,
    string? Source = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

public sealed record GetCandidateQuery(Guid CandidateId);

/// <summary>
/// Yangi nomzodni voronkaga kiritish.
/// </summary>
/// <param name="VacancyId">Qaysi vakansiyaga — MAJBURIY.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Phone">Telefon.</param>
/// <param name="Address">Manzil.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="StageId">
/// Boshlang'ich bosqich. Bo'sh bo'lsa — voronkaning BIRINCHI faol bosqichi
/// («Анкета»). Nomzod bosqichsiz kiritilmaydi: u qayerda turgani noma'lum
/// bo'lardi va hech qaysi voronka ustuniga tushmasdi.
/// </param>
/// <param name="Source">
/// Qayerdan kelgan. Sukut — <see cref="CandidateSource.Manual"/>: HR
/// formasi shu qiymatni yubormaydi. Telegram bot (keyingi bosqich) esa
/// <see cref="CandidateSource.TelegramBot"/> ni ochiq beradi.
/// </param>
/// <param name="TelegramChatId">
/// Telegram suhbatining raqami — faqat botdan kelgan nomzodda.
/// Rad etish xabari aynan shu manzilga boradi.
/// </param>
/// <param name="TelegramLanguage">
/// Nomzod botda tanlagan til (<c>uz-Cyrl</c>, <c>uz-Latn</c>, <c>ru</c>).
/// Javob aynan o'sha tilda yoziladi.
/// </param>
public sealed record RegisterCandidateCommand(
    Guid VacancyId,
    string? LastName,
    string? FirstName,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? Address = null,
    string? Speciality = null,
    string? Note = null,
    Guid? StageId = null,
    CandidateSource Source = CandidateSource.Manual,
    long? TelegramChatId = null,
    string? TelegramLanguage = null);

/// <summary>Nomzodning shaxsiy ma'lumotini tuzatish. Vakansiya va manba o'zgarmaydi.</summary>
public sealed record UpdateCandidateCommand(
    Guid CandidateId,
    string? LastName,
    string? FirstName,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? Address = null,
    string? Speciality = null,
    string? Note = null);

/// <summary>Nomzodni boshqa vakansiyaga o'tkazish.</summary>
public sealed record ReassignCandidateCommand(Guid CandidateId, Guid VacancyId);

/// <summary>
/// Bosqich natijasini yozish — voronkaning yagona harakati.
/// </summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="StageId">Bosqich.</param>
/// <param name="Outcome">
/// <c>Pending</c> — bosqich ochildi, natija keyin; <c>Passed</c> / <c>Failed</c> —
/// tayyor natija. Sukut <c>Pending</c>: «keyingi bosqichga o'tkazish» eng
/// tez-tez uchraydigan amal.
/// </param>
/// <param name="Date">Bosqich sanasi. Bo'sh — bugun.</param>
/// <param name="Conclusion">Xulosa matni — kengash xulosasi ham SHU maydon.</param>
public sealed record RecordCandidateStageCommand(
    Guid CandidateId,
    Guid StageId,
    StageOutcome Outcome = StageOutcome.Pending,
    DateOnly? Date = null,
    string? Conclusion = null);

/// <summary>Nomzodni rad etish (korxona qarori).</summary>
public sealed record RejectCandidateCommand(Guid CandidateId, DateOnly? At = null, string? Reason = null);

/// <summary>Nomzod o'zi voz kechdi.</summary>
public sealed record WithdrawCandidateCommand(Guid CandidateId, DateOnly? At = null, string? Reason = null);

/// <summary>Nomzod yozuvini yumshoq o'chirish.</summary>
public sealed record DeleteCandidateCommand(Guid CandidateId);

public sealed class SearchCandidatesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Eng katta sahifa hajmi.</summary>
    public const int MaxPageSize = 200;

    private readonly IRecruitingReadModel _readModel;

    public SearchCandidatesQueryHandler(IRecruitingReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<CandidateListItemDto>>> HandleAsync(
        SearchCandidatesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var status = RecruitingQueryParsing.ParseCandidateStatus(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var source = RecruitingQueryParsing.ParseCandidateSource(query.Source);
        if (source.IsFailure)
        {
            return source.Error;
        }

        var (sort, descending) = RecruitingQueryParsing.ParseCandidateSort(query.Sort);

        // Bo'sh matn `null` ga aylantiriladi: «?search=» — «qidiruvsiz»,
        // «hech narsa topilmasin» emas (barcha ro'yxatlarda bir xil qoida).
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var found = await _readModel
            .SearchCandidatesAsync(
                new CandidateSearchFilter(
                    search,
                    query.VacancyId,
                    query.StageId,
                    status.Value,
                    source.Value,
                    page,
                    pageSize,
                    sort,
                    descending),
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<CandidateListItemDto> items = [.. found.Items.Select(ToDto)];

        var pageCount = (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<CandidateListItemDto>(items, page, pageSize, found.Total, pageCount));
    }

    /// <summary>Xom qatorni ekran qatoriga aylantiradi.</summary>
    /// <remarks>
    /// F.I.Sh. bu yerda yig'iladi — o'qish modeli ustunlarni xom holida
    /// qaytaradi va ko'rsatish shaklini bilmaydi
    /// (<c>SearchEmployeesQueryHandler.ToDto</c> bilan bir xil bo'linish).
    /// </remarks>
    private static CandidateListItemDto ToDto(CandidateRow row)
    {
        var fullName = FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName);

        return new CandidateListItemDto(
            row.Id,
            fullName.Full,
            fullName.ShortForm,
            row.Phone,
            row.BirthDate,
            row.Speciality,
            row.VacancyId,
            row.VacancyName,
            row.CurrentStageId,
            row.CurrentStageName,
            row.Status,
            row.Source,
            row.HasResume,
            row.CreatedAt,
            row.HiredEmployeeId);
    }
}

public sealed class GetCandidateQueryHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly RecruitingMapper _mapper;

    public GetCandidateQueryHandler(ICandidateRepository candidates, RecruitingMapper mapper)
    {
        _candidates = candidates;
        _mapper = mapper;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        GetCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidate = await _candidates
            .GetWithStagesAsync(query.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        return candidate is null
            ? RecruitingErrors.CandidateNotFound
            : await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Nomzodni voronkaga kiritadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vakansiya OCHIQ bo'lishi shart.</b> Yopilgan yoki to'xtatilgan
/// vakansiyaga nomzod kiritish — bu yo'qolgan ma'lumot: HR uni voronkada
/// izlaydi, u esa hech qaysi faol ro'yxatda ko'rinmaydi.
/// </para>
/// <para>
/// <b>Dublikat tekshiruvi ATAYLAB YO'Q.</b> Xodimda u JSHSHIR bo'yicha
/// bajariladi, nomzodda esa unday kalit yo'q: bir xil ism-familiya butunlay
/// boshqa odamlar bo'lishi mumkin, telefon esa oilada bitta bo'lishi
/// mumkin. To'sib qo'yish haqiqiy nomzodni kiritishga xalaqit berardi —
/// takrorni HR qidiruv orqali o'zi ko'radi.
/// </para>
/// </remarks>
public sealed class RegisterCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IVacancyRepository _vacancies;
    private readonly IPipelineStageRepository _stages;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCandidateCommandHandler(
        ICandidateRepository candidates,
        IVacancyRepository vacancies,
        IPipelineStageRepository stages,
        RecruitingMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _vacancies = vacancies;
        _stages = stages;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        RegisterCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vacancy = await _vacancies.GetByIdAsync(command.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is null)
        {
            return RecruitingErrors.VacancyNotFound;
        }

        if (!vacancy.AcceptsCandidates)
        {
            return RecruitingErrors.VacancyNotAccepting;
        }

        var stage = await ResolveStageAsync(command.StageId, cancellationToken).ConfigureAwait(false);
        if (stage.IsFailure)
        {
            return stage.Error;
        }

        var fullName = FullName.Create(command.LastName, command.FirstName, command.MiddleName);
        if (fullName.IsFailure)
        {
            return fullName.Error;
        }

        var phone = ParsePhone(command.Phone);
        if (phone.IsFailure)
        {
            return phone.Error;
        }

        var registered = Candidate.Register(
            fullName.Value,
            command.VacancyId,
            command.Source,
            command.BirthDate,
            phone.Value,
            command.Address,
            command.Speciality,
            command.Note);

        if (registered.IsFailure)
        {
            return registered.Error;
        }

        var candidate = registered.Value;

        /*
          Telegram suhbatining raqami — FAQAT botdan kelgan nomzodda.
          Busiz rad etish xabarini yuborib bo'lmaydi: bot nomzodni
          telefon raqami bilan topa olmaydi, u Telegram'da manzil emas.

          Tekshiruv `Source` bo'yicha EMAS, qiymat bo'yicha: manba
          nomini so'rovda yozib qo'yish oson, chat raqamini esa
          o'ylab topib bo'lmaydi.
        */
        if (command.TelegramChatId is { } chatId)
        {
            candidate.SetTelegram(chatId, command.TelegramLanguage);
        }

        // Birinchi bosqich «kutilmoqda» bo'lib ochiladi: anketa tushdi,
        // natija hali yo'q. Shu tufayli nomzod yaratilgan zahoti voronka
        // taxtasining birinchi ustunida ko'rinadi — aks holda u
        // «bosqichsiz» qolib, hech qaysi ustunga tushmasdi.
        var recorded = candidate.RecordStage(stage.Value.Id, _clock.Today);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        _candidates.Add(candidate);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Boshlang'ich bosqichni aniqlaydi.
    /// </summary>
    /// <remarks>
    /// Ochiq berilgan bosqich tekshiriladi (mavjud va FAOL), berilmagan
    /// bo'lsa voronkaning birinchi faol bosqichi olinadi. Faol bosqich
    /// umuman bo'lmasa — xato: nomzodni «hech qayerga» qo'yib bo'lmaydi.
    /// </remarks>
    private async Task<Result<PipelineStage>> ResolveStageAsync(Guid? stageId, CancellationToken cancellationToken)
    {
        if (stageId is { } requested)
        {
            var stage = await _stages.GetByIdAsync(requested, cancellationToken).ConfigureAwait(false);
            if (stage is null)
            {
                return RecruitingErrors.StageNotFound;
            }

            return stage.IsActive ? stage : RecruitingErrors.StageInactive;
        }

        var active = await _stages.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);
        var first = active.OrderBy(stage => stage.SortOrder).FirstOrDefault();

        return first is null ? RecruitingErrors.NoActiveStages : first;
    }

    internal static Result<PhoneNumber?> ParsePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<PhoneNumber?>(null);
        }

        var parsed = PhoneNumber.Create(value);

        return parsed.IsFailure ? parsed.Error : Result.Success<PhoneNumber?>(parsed.Value);
    }
}

public sealed class UpdateCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCandidateCommandHandler(
        ICandidateRepository candidates,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        UpdateCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var fullName = FullName.Create(command.LastName, command.FirstName, command.MiddleName);
        if (fullName.IsFailure)
        {
            return fullName.Error;
        }

        var phone = RegisterCandidateCommandHandler.ParsePhone(command.Phone);
        if (phone.IsFailure)
        {
            return phone.Error;
        }

        var updated = candidate.Update(
            fullName.Value,
            command.BirthDate,
            phone.Value,
            command.Address,
            command.Speciality,
            command.Note);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ReassignCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IVacancyRepository _vacancies;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ReassignCandidateCommandHandler(
        ICandidateRepository candidates,
        IVacancyRepository vacancies,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _vacancies = vacancies;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        ReassignCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var vacancy = await _vacancies.GetByIdAsync(command.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy is null)
        {
            return RecruitingErrors.VacancyNotFound;
        }

        if (!vacancy.AcceptsCandidates)
        {
            return RecruitingErrors.VacancyNotAccepting;
        }

        var reassigned = candidate.Reassign(command.VacancyId);
        if (reassigned.IsFailure)
        {
            return reassigned.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Bosqich natijasini yozadi.
/// </summary>
/// <remarks>
/// Bosqichning MAVJUDLIGI va FAOLLIGI shu yerda tekshiriladi: domen
/// ma'lumotnomani ko'rmaydi. Faolsizlantirilgan bosqichga o'tkazish rad
/// etiladi — bu faolsizlantirishning yagona amaliy ma'nosi
/// (<c>RecruitingErrors.StageInactive</c> izohiga qarang).
/// </remarks>
public sealed class RecordCandidateStageCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IPipelineStageRepository _stages;
    private readonly RecruitingMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RecordCandidateStageCommandHandler(
        ICandidateRepository candidates,
        IPipelineStageRepository stages,
        RecruitingMapper mapper,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _stages = stages;
        _mapper = mapper;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        RecordCandidateStageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates
            .GetWithStagesAsync(command.CandidateId, cancellationToken)
            .ConfigureAwait(false);

        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var stage = await _stages.GetByIdAsync(command.StageId, cancellationToken).ConfigureAwait(false);
        if (stage is null)
        {
            return RecruitingErrors.StageNotFound;
        }

        if (!stage.IsActive)
        {
            return RecruitingErrors.StageInactive;
        }

        var recorded = candidate.RecordStage(
            command.StageId,
            command.Date ?? _clock.Today,
            command.Outcome,
            command.Conclusion,
            _currentUser.UserId);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Nomzodni voronkadan chiqaradi: rad etish yoki voz kechish.
/// </summary>
/// <remarks>
/// Ikkala amal bir xil qadamlardan iborat va faqat chaqiriladigan domen
/// metodida farq qiladi — <c>ChangeVacancyStateCommandHandler</c> bilan bir
/// xil sabab. «Ishga olindi» esa BU YERDA YO'Q: u faqat taklif qabul
/// qilinganda va xodim haqiqatan yaratilganda qo'yiladi
/// (<c>Candidate.MarkHired</c> izohiga qarang).
/// </remarks>
public sealed class CloseCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCandidateCommandHandler(
        ICandidateRepository candidates,
        RecruitingMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<CandidateDto>> HandleAsync(
        RejectCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyAsync(
            command.CandidateId,
            candidate => candidate.Reject(command.At ?? _clock.Today, command.Reason),
            cancellationToken);
    }

    public Task<Result<CandidateDto>> HandleAsync(
        WithdrawCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyAsync(
            command.CandidateId,
            candidate => candidate.Withdraw(command.At ?? _clock.Today, command.Reason),
            cancellationToken);
    }

    private async Task<Result<CandidateDto>> ApplyAsync(
        Guid candidateId,
        Func<Candidate, Result> change,
        CancellationToken cancellationToken)
    {
        var candidate = await _candidates.GetWithStagesAsync(candidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        var changed = change(candidate);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DeleteCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCandidateCommandHandler(ICandidateRepository candidates, IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var candidate = await _candidates.GetByIdAsync(command.CandidateId, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return RecruitingErrors.CandidateNotFound;
        }

        // Yumshoq o'chirish. Rezyume fayli diskda QOLADI — xodim hujjatidagi
        // bilan bir xil qaror: yozuv bazada turgan ekan, unga ishora
        // qiladigan fayl ham turishi kerak, aks holda tiklangan yozuv
        // buzilgan bo'lib chiqardi.
        _candidates.Remove(candidate);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
