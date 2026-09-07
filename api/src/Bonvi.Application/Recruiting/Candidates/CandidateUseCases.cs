using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Settings;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Recruiting.Candidates;

/// <summary>
/// Nomzodlar ro'yxati — rekrutingning asosiy ekrani.
/// </summary>
/// <param name="Search">F.I.Sh. yoki telefon bo'yicha qidiruv.</param>
/// <param name="VacancyId">Vakansiya bo'yicha filtr.</param>
/// <param name="StageId">Bosqich bo'yicha filtr — voronka ustunini chizish uchun.</param>
/// <param name="Status">
/// <c>InProgress</c> | <c>Rejected</c> | <c>Hired</c> | <c>Withdrawn</c> |
/// <c>Reserve</c>.
/// <para>
/// Bo'sh bo'lsa — ISHGA OLINGANLARDAN tashqari hammasi: ular endi xodim
/// va nomzodlar ro'yxatida shovqin (batafsil sabab
/// <c>CandidateSearchFilter.HideHired</c> izohida). Ularni ko'rish uchun
/// <c>Hired</c> ni ATAYLAB tanlash kerak.
/// </para>
/// <para>
/// Voronka taxtasi <c>InProgress</c> bilan so'raydi: voronkadan chiqqan
/// nomzodlar u yerda faol bo'lib turmasligi kerak. <c>Reserve</c> esa
/// sukut ro'yxatda KO'RINADI — rezervning butun ma'nosi «kerak bo'lganda
/// shu yerdan chaqiramiz» degani.
/// </para>
/// </param>
/// <param name="Source">
/// <c>Manual</c> | <c>TelegramBot</c> | <c>Excel</c>. «Bot qancha nomzod
/// keltirdi» degan savolning javobi shu filtr orqali olinadi.
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// <c>createdAt</c> (sukut, yangidan eskiga), <c>name</c>, <c>stage</c>.
/// Teskari tartib: oldiga <c>-</c> yoki oxiriga <c>:desc</c>.
/// </param>
/// <param name="IncludeArchive">
/// Excel'dan ko'chirilgan kengash arxivi ham qo'shilsinmi.
/// <para>
/// Sukut bo'yicha <c>false</c>: 3110 ta tarixiy yozuv kundalik ro'yxatni
/// ko'mib yuborardi. <c>Source</c> ataylab tanlangan bo'lsa bu bayroq
/// e'tiborga olinmaydi — HR «Excel arxivi» ni tanlagan ekan, aynan
/// o'shani ko'rmoqchi.
/// </para>
/// </param>
public sealed record SearchCandidatesQuery(
    string? Search = null,
    Guid? VacancyId = null,
    Guid? StageId = null,
    string? Status = null,
    string? Source = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null,
    bool IncludeArchive = false);

public sealed record GetCandidateQuery(Guid CandidateId);

/// <summary>
/// Botda to'ldirilgan anketa — nomzodni ro'yxatga olish so'rovining
/// IXTIYORIY bloki.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Blok butunligicha ixtiyoriy va bu deploy tartibini belgilaydi.</b>
/// Prod'da avval API yangilanadi, keyin bot: oraliqda ESKI bot anketasiz
/// so'rov yuboradi va u AYNAN avvalgidek ishlashi shart. Shu sababli
/// bu yerda hech qanday majburiy maydon paydo bo'lmaydi — «anketa yo'q»
/// xato emas, odatiy hol.
/// </para>
/// <para>
/// <b>Nega alohida record, buyruqqa o'n uchta parametr qo'shilmadi.</b>
/// <c>RegisterCandidateCommand</c> allaqachon o'n uch parametrli; yana
/// shuncha qo'shilsa, chaqiruv joyida qaysi qiymat qayerga tushayotganini
/// ko'z bilan tekshirib bo'lmasdi. Blok esa butunligicha bor yoki yo'q —
/// va shakl aynan shu qoidani ko'rsatib turadi.
/// </para>
/// <para>
/// <b>Tekshiruv bu yerda YO'Q</b>: sanoqlar va uzunliklar
/// <see cref="CandidateQuestionnaire.Create"/> da tekshiriladi — qoida
/// bitta joyda tursin (mavjud <c>FullName</c>/<c>PhoneNumber</c> bilan
/// bir xil bo'linish).
/// </para>
/// </remarks>
/// <param name="Education">Ma'lumot darajasi.</param>
/// <param name="MaritalStatus">Oilaviy holati.</param>
/// <param name="RussianLevel">Rus tilini bilish darajasi.</param>
/// <param name="ExpectedSalary">Kutilayotgan maosh oralig'i.</param>
/// <param name="ReferralSource">Ish haqida qayerdan bilgan.</param>
/// <param name="OfferAcceptedAt">
/// Ommaviy oferta bilan rozilik VAQTI. <c>null</c> — oferta havolasi
/// sozlanmagan, ya'ni nomzoddan rozilik SO'RALMAGAN (sababi domenda).
/// </param>
/// <param name="WorkExperience">Ish tajribasi — erkin matn, o'tkazib yuborsa bo'ladi.</param>
/// <param name="OtherLanguages">Boshqa tillar — erkin matn.</param>
/// <param name="OfficeSkills">Office dasturlari — erkin matn.</param>
/// <param name="LastSalary">Oxirgi maosh — erkin matn.</param>
/// <param name="ExtraNote">Nomzodning o'zi haqida qo'shimchasi.</param>
/// <param name="HasDriverLicense">Haydovchilik guvohnomasi bormi.</param>
/// <param name="DriverLicenseCategories">
/// Guvohnoma toifasi (B, BC, BCE, BCED). Guvohnoma yo'q bo'lsa qiymat
/// jimgina tashlanadi — sababi domen izohida.
/// </param>
public sealed record CandidateQuestionnaireInput(
    EducationLevel Education,
    MaritalStatus MaritalStatus,
    LanguageLevel RussianLevel,
    SalaryExpectation ExpectedSalary,
    ReferralSource ReferralSource,

    /*
      ⚠️ `DateTimeOffset?` va `= null`, `= default` EMAS. So'rov
      rekordidagi qiymat turiga (`Guid`, `DateTimeOffset`) sukut qiymat
      yozilsa, .NET 10 OpenAPI generatori butun `/openapi/v1.json` ni
      500 qiladi — ya'ni `./bonvi gen` umuman ishlamay qoladi. Bir
      marta shunday bo'lgan (`AcceptJobOfferRequest`, 2026-08-29).
    */
    DateTimeOffset? OfferAcceptedAt = null,
    string? WorkExperience = null,
    string? OtherLanguages = null,
    string? OfficeSkills = null,
    string? LastSalary = null,
    string? ExtraNote = null,
    bool HasDriverLicense = false,
    string? DriverLicenseCategories = null);

/// <summary>
/// Yangi nomzodni voronkaga kiritish.
/// </summary>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="VacancyId">
/// Qaysi vakansiyaga. Bo'sh bo'lsa — «Umumiy anketa» xizmat vakansiyasi
/// (izohi <see cref="RegisterCandidateCommandHandler"/> da).
/// </param>
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
/// <param name="Questionnaire">
/// Botda to'ldirilgan anketa. Bo'sh bo'lsa nomzod anketasiz yaratiladi —
/// HR formasi va ESKI bot aynan shunday yuboradi.
/// </param>
public sealed record RegisterCandidateCommand(
    string? LastName,
    string? FirstName,
    Guid? VacancyId = null,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? Address = null,
    string? Speciality = null,
    string? Note = null,
    Guid? StageId = null,
    CandidateSource Source = CandidateSource.Manual,
    long? TelegramChatId = null,
    string? TelegramLanguage = null,
    CandidateQuestionnaireInput? Questionnaire = null);

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

/// <summary>
/// Nomzodni rezervga o'tkazish — «hozir olinmadi, lekin kerak bo'lsa
/// chaqiramiz».
/// </summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="At">Rezervga o'tkazilgan sana. Bo'sh — bugun.</param>
/// <param name="Reason">
/// Sabab: «o'rin yo'q», «kelasi mavsumga», «boshqa do'konga mos». Bu matn
/// nomzod qaytarilganda YO'QOLADI (sababi
/// <c>Candidate.ReturnFromReserve</c> izohida), shu sababli u qarorning
/// izohi bo'lib qoladi, uzoq muddatli tavsif emas.
/// </param>
public sealed record ReserveCandidateCommand(Guid CandidateId, DateOnly? At = null, string? Reason = null);

/// <summary>Rezervdagi nomzodni voronkaga qaytarish.</summary>
public sealed record ReturnCandidateFromReserveCommand(Guid CandidateId);

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
                    descending,
                    query.IncludeArchive),
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
            row.HasPhoto,
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
/// ⭐ <b>Vakansiya berilmasa — «Umumiy anketa».</b> Ochiq ish o'rni
/// bo'lmaganda ham nomzod anketa to'ldira olishi kerak (butun
/// funksiyaning maqsadi shu), <c>Candidate.VacancyId</c> esa majburiy.
/// Xizmat vakansiyasining identifikatori YASHIRIN sozlamada turadi va
/// uni SERVER o'qiydi: bot uni bilmasligi kerak — bilsa, identifikator
/// ikki joyda takrorlanardi va sozlama o'zgarganda bot eskisiga yozib
/// yuraverardi.
/// </para>
/// <para>
/// <b>Dublikat tekshiruvi ATAYLAB YO'Q.</b> Xodimda u JSHSHIR bo'yicha
/// bajariladi, nomzodda esa unday kalit yo'q: bir xil ism-familiya butunlay
/// boshqa odamlar bo'lishi mumkin, telefon esa oilada bitta bo'lishi
/// mumkin. To'sib qo'yish haqiqiy nomzodni kiritishga xalaqit berardi —
/// takrorni HR qidiruv orqali o'zi ko'radi.
/// </para>
/// <para>
/// ⭐ <b>Anketa nomzod bilan BITTA saqlashda yoziladi.</b> U rad etilsa
/// (masalan sanoq qiymati noto'g'ri kelsa) nomzod ham yaratilmaydi:
/// aks holda bazada javoblarsiz, «bo'sh» nomzod qolib ketardi va bot
/// takroriy urinishda ikkinchisini yaratardi. Domen tekshiruvi
/// <c>SaveChangesAsync</c> dan OLDIN bajarilgani uchun buning uchun
/// alohida tranzaksiya boshqaruvi kerak emas.
/// </para>
/// </remarks>
public sealed class RegisterCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IVacancyRepository _vacancies;
    private readonly IPipelineStageRepository _stages;
    private readonly RecruitingMapper _mapper;
    private readonly SystemSettingsReader _settings;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCandidateCommandHandler(
        ICandidateRepository candidates,
        IVacancyRepository vacancies,
        IPipelineStageRepository stages,
        RecruitingMapper mapper,
        SystemSettingsReader settings,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _vacancies = vacancies;
        _stages = stages;
        _mapper = mapper;
        _settings = settings;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        RegisterCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vacancy = await ResolveVacancyAsync(command.VacancyId, cancellationToken).ConfigureAwait(false);
        if (vacancy.IsFailure)
        {
            return vacancy.Error;
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
            vacancy.Value.Id,
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

        if (command.Questionnaire is { } form)
        {
            var attached = AttachQuestionnaire(candidate, form);
            if (attached.IsFailure)
            {
                return attached.Error;
            }
        }

        _candidates.Add(candidate);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Nomzod tushadigan vakansiyani aniqlaydi va uni tekshiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ochiq berilgan vakansiya uchun qoida AVVALGIDEK qoladi: topilmasa
    /// «vakansiya.topilmadi», yopiq bo'lsa «vakansiya.qabul_qilmaydi».
    /// Bu prod uchun muhim — hozirgi bot va HR formasi aynan shu yo'ldan
    /// yuradi va ularning xatolari o'zgarmasligi kerak.
    /// </para>
    /// <para>
    /// Berilmagan bo'lsa «Umumiy anketa» olinadi. Uning uchala nosozligi
    /// (sozlama bo'sh, vakansiya topilmadi, yopilgan) BITTA xatoga
    /// yig'iladi — sababi <see cref="RecruitingErrors.GeneralPoolUnavailable"/>
    /// izohida. «vakansiya.topilmadi» qaytarish yaramaydi: mijoz hech
    /// qanday vakansiya YUBORMAGAN va bunday javob uni o'z so'rovida xato
    /// izlashga majbur qilardi.
    /// </para>
    /// </remarks>
    private async Task<Result<Vacancy>> ResolveVacancyAsync(Guid? requested, CancellationToken cancellationToken)
    {
        if (requested is { } vacancyId)
        {
            var chosen = await _vacancies.GetByIdAsync(vacancyId, cancellationToken).ConfigureAwait(false);

            if (chosen is null)
            {
                return RecruitingErrors.VacancyNotFound;
            }

            return chosen.AcceptsCandidates ? chosen : RecruitingErrors.VacancyNotAccepting;
        }

        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (!Guid.TryParse(settings.Hidden(SystemSettingKeys.GeneralPoolVacancyId), out var poolId))
        {
            return RecruitingErrors.GeneralPoolUnavailable;
        }

        var pool = await _vacancies.GetByIdAsync(poolId, cancellationToken).ConfigureAwait(false);

        return pool is null || !pool.AcceptsCandidates
            ? RecruitingErrors.GeneralPoolUnavailable
            : pool;
    }

    /// <summary>
    /// Anketani yasaydi va nomzodga biriktiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nomzod HALI saqlanmagan, ya'ni xato qaytsa hech narsa yozilmaydi
    /// (handler izohidagi qoida). Identifikator esa allaqachon bor —
    /// u xotirada, <c>Candidate.Register</c> da beriladi.
    /// </para>
    /// <para>
    /// ⭐ <b><c>ToUniversalTime()</c> MAJBURIY.</b> Npgsql
    /// <c>timestamptz</c> ustuniga faqat NOL siljishli qiymat yozadi va
    /// aks holda istisno tashlaydi. Bot esa vaqtni Toshkent siljishi
    /// bilan yuboradi («…+05:00») — normallashtirilmasa butun so'rov
    /// 500 bo'lardi. Lahza o'zgarmaydi, faqat ifodalanishi.
    /// Bir xil qoida <c>DashboardReadModel</c> da ham bor.
    /// </para>
    /// <para>
    /// <c>?.</c> — rozilik vaqti BO'LMASLIGI mumkin: oferta havolasi
    /// sozlanmagan bo'lsa bot bu qadamni umuman chizmaydi va maydonni
    /// yubormaydi (sababi <see cref="CandidateQuestionnaire"/> da).
    /// <c>null</c> shundayligicha o'tadi.
    /// </para>
    /// </remarks>
    private static Result AttachQuestionnaire(Candidate candidate, CandidateQuestionnaireInput form)
    {
        var created = CandidateQuestionnaire.Create(
            candidate.Id,
            form.Education,
            form.MaritalStatus,
            form.RussianLevel,
            form.ExpectedSalary,
            form.ReferralSource,
            form.OfferAcceptedAt?.ToUniversalTime(),
            form.WorkExperience,
            form.OtherLanguages,
            form.OfficeSkills,
            form.LastSalary,
            form.ExtraNote,
            form.HasDriverLicense,
            form.DriverLicenseCategories);

        return created.IsFailure ? created.Error : candidate.AttachQuestionnaire(created.Value);
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
/// Nomzodni voronkadan chiqaradi: rad etish, voz kechish yoki rezerv.
/// </summary>
/// <remarks>
/// <para>
/// Uchala amal bir xil qadamlardan iborat va faqat chaqiriladigan domen
/// metodida farq qiladi — <c>ChangeVacancyStateCommandHandler</c> bilan bir
/// xil sabab. «Ishga olindi» esa BU YERDA YO'Q: u faqat xodim haqiqatan
/// yaratilganda qo'yiladi (<c>Candidate.MarkHired</c> izohiga qarang).
/// </para>
/// <para>
/// Rezervdan QAYTARISH ham bu yerda emas
/// (<see cref="ReturnCandidateFromReserveCommandHandler"/>): u nomzodni
/// voronkadan chiqarmaydi, aksincha — qaytaradi. Uni shu sinfga qo'shish
/// «yopish» degan nomni yolg'onga aylantirardi.
/// </para>
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

    public Task<Result<CandidateDto>> HandleAsync(
        ReserveCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ApplyAsync(
            command.CandidateId,
            candidate => candidate.MoveToReserve(command.At ?? _clock.Today, command.Reason),
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

/// <summary>
/// Rezervdagi nomzodni voronkaga qaytaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida use-case, <see cref="CloseCandidateCommandHandler"/>
/// ning bir qismi emas.</b> U yerdagi uchala amal nomzodni voronkadan
/// CHIQARADI va bitta umumiy qoidaga bo'ysunadi («faqat jarayondagi
/// nomzod yopiladi»). Bu amal esa teskari yo'nalishda ishlaydi va uning
/// sharti ham teskari: nomzod aynan REZERVDA bo'lishi kerak. Ikkalasini
/// bitta sinfga yig'ish «yopish» degan nomni yolg'onga aylantirardi.
/// </para>
/// <para>
/// Bosqich va bosqichlar tarixi saqlanadi — nomzod qayerda to'xtagan
/// bo'lsa, o'sha yerdan davom etadi (sababi
/// <c>Candidate.ReturnFromReserve</c> izohida).
/// </para>
/// </remarks>
public sealed class ReturnCandidateFromReserveCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly RecruitingMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnCandidateFromReserveCommandHandler(
        ICandidateRepository candidates,
        RecruitingMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        ReturnCandidateFromReserveCommand command,
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

        var returned = candidate.ReturnFromReserve();
        if (returned.IsFailure)
        {
            return returned.Error;
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
