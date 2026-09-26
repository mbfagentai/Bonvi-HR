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
/// Bo'sh bo'lsa — ISHGA OLINGANLAR, RAD ETILGANLAR va O'ZI VOZ
/// KECHGANLARDAN tashqari hammasi. Birinchisi endi xodim, qolgan ikkitasi
/// voronkadan chiqqan: uchalasi ham «hozir kim ish ustida?» degan savolga
/// shovqin (batafsil sabablar <c>CandidateSearchFilter.HideHired</c>,
/// <c>HideRejected</c> va <c>HideWithdrawn</c> izohlarida). Ularni ko'rish
/// uchun holatni ATAYLAB tanlash kerak; rad etilganlar va voz
/// kechganlarning umumiy bo'limi esa <see cref="Rejected"/> bayrog'i bilan
/// ochiladi.
/// </para>
/// <para>
/// Voronka taxtasi <c>InProgress</c> bilan so'raydi: voronkadan chiqqan
/// nomzodlar u yerda faol bo'lib turmasligi kerak. <c>Reserve</c> ham
/// sukut ro'yxatdan chiqariladi (2026-09-26) — rezervdagilar «Rezerv»
/// bo'limida, u <c>status=Reserve</c> bilan so'raydi
/// (<c>CandidateSearchFilter.HideReserve</c>).
/// </para>
/// </param>
/// <param name="Source">
/// <c>Manual</c> | <c>TelegramBot</c> | <c>Excel</c>. «Bot qancha nomzod
/// keltirdi» degan savolning javobi shu filtr orqali olinadi.
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// <c>createdAt</c> (sukut, yangidan eskiga), <c>name</c>, <c>stage</c>,
/// <c>nextContact</c>. Teskari tartib: oldiga <c>-</c> yoki oxiriga
/// <c>:desc</c>.
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
/// <param name="ContactDue">
/// FILTR: keyingi aloqa sanasi qo'yilgan VA u BUGUN yoki undan oldin
/// bo'lgan nomzodlar — «bugun kim bilan gaplashish kerak».
/// <para>
/// «Bugun» ni server hisoblaydi (Toshkent kalendari,
/// <see cref="SearchCandidatesQueryHandler"/> dagi <c>IClock</c>) —
/// brauzer sanasi boshqa mintaqada bo'lsa ro'yxat bir kunga surilib
/// ketardi.
/// </para>
/// </param>
/// <param name="Rejected">
/// BO'LIM: «Rad etilganlar» — <c>Rejected</c> VA <c>Withdrawn</c> birga.
/// <para>
/// ⚠️ Bu filtr EMAS, BO'LIM: bo'sh natija «hech kim rad etilmagan»
/// degani, «qidiruv topilmadi» degani emas. Bayroq holat tanlovi bilan
/// birga ham ishlaydi (<c>rejected=true&amp;status=Withdrawn</c> — faqat
/// voz kechganlar). Nega ko'p qiymatli holat filtri
/// (<c>?status=Rejected,Withdrawn</c>) YARAMADI —
/// <c>CandidateSearchFilter.HideRejected</c> izohida: bitta
/// <c>&lt;select&gt;</c> ikkita qiymatni ifodalay olmaydi va butun manzil
/// sxemasi ko'p qiymatli bo'lib ketardi.
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
    bool IncludeArchive = false,
    bool ContactDue = false,
    bool Rejected = false);

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
      marta shunday bo'lgan: o'shanda ish taklifini qabul qilish
      so'rovi butun hujjatni yiqitgan edi (2026-08-29).
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
/// <param name="CandidateId">Nomzod.</param>
/// <param name="VacancyId">Yangi vakansiya.</param>
/// <param name="Note">
/// Amal izohi — bo'sh bo'lmasa <c>candidate_notes</c> ga
/// <see cref="CandidateNoteKind.Reassign"/> turi bilan yoziladi.
/// <para>
/// Bu nomzodning <c>Note</c> maydoni EMAS: u odamning uzoq muddatli
/// tavsifi va tahrirlash formasidan qayta yozilib ketardi (batafsil
/// <see cref="CandidateNote"/> izohida).
/// </para>
/// </param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi. Berilmasa saqlangan sana O'ZGARMAYDI — bu amalda
/// «tozalash» yo'q (jadval <c>Candidate.Close</c> izohida).
/// </param>
public sealed record ReassignCandidateCommand(
    Guid CandidateId,
    Guid VacancyId,
    string? Note = null,
    DateOnly? NextContactDate = null);

/// <summary>
/// Bosqichni yozish — voronkaning yagona harakati.
/// </summary>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="StageId">Bosqich.</param>
/// <param name="Date">Bosqich sanasi. Bo'sh — bugun.</param>
/// <param name="Conclusion">Xulosa matni — kengash xulosasi ham SHU maydon.</param>
/// <param name="NextContactDate">Keyingi aloqa sanasi — berilsa QO'YILADI.</param>
/// <param name="ClearNextContact">
/// <c>true</c> — saqlangan sana TOZALANADI (<paramref name="NextContactDate"/>
/// e'tiborga olinmaydi).
/// <para>
/// ⭐ <b>Nega ikkita maydon, «null — tozalash» emas.</b> Bu buyruqni ikkita
/// mijoz chaqiradi: yangi oyna (formasi bor) va voronkadagi SUDRASH
/// (formasi yo'q — u faqat bosqichni yuboradi). «null — tozalash»
/// shartnomasida kartani sudrab qo'yish HR besh daqiqa oldin qo'ygan
/// sanani JIMGINA o'chirib tashlardi. Ikkita maydon xunukroq, lekin
/// ANIQ — loyihada aniqlik jimlikdan doim ustun
/// (<c>IncludeArchive</c>, <c>ExcludedVacancyId</c> bilan bir xil qaror).
/// </para>
/// <para>
/// ⚠️ Tur bu yerda oddiy <c>bool</c>, HTTP so'rovi rekordida esa
/// <c>bool?</c>. Sabab: OpenAPI generatori faqat so'rov rekordlarini
/// ko'radi va qiymat turiga sukut qiymat yozilsa butun hujjatni 500
/// qiladi (2026-08-29). Bu yerda bunday xavf yo'q, endpoint esa
/// <c>?? false</c> bilan o'giradi.
/// </para>
/// </param>
public sealed record RecordCandidateStageCommand(
    Guid CandidateId,
    Guid StageId,
    DateOnly? Date = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool ClearNextContact = false);

/// <summary>
/// Tarixdagi BITTA bosqich yozuvini tuzatish.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida buyruq, <see cref="RecordCandidateStageCommand"/> ga
/// «yozuv ID» qo'shilmadi.</b> Ikkisi ikki xil amal va ularning
/// tekshiruvlari ham har xil: yozish voronkani SURADI (bosqich faol
/// bo'lishi shart, nomzod jarayonda bo'lishi shart), tuzatish esa faqat
/// mavjud qatorni to'g'rilaydi (bosqich ham, nomzod holati ham
/// o'zgarmaydi). Bitta buyruqqa ixtiyoriy ID qo'shilsa, handler ichida
/// ikkita butunlay boshqa yo'l paydo bo'lardi va har bir tekshiruv
/// «bu qaysi holat edi?» degan shart bilan o'ralardi.
/// </para>
/// <para>
/// <b>Bosqich bu yerda YO'Q va bo'lishi ham mumkin emas</b> — sabab
/// <c>CandidateStageEntry.Edit</c> izohida: yozuv «qaysi bosqich» degan
/// fakt bilan tug'iladi.
/// </para>
/// </remarks>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="EntryId">Qaysi yozuv tuzatiladi.</param>
/// <param name="Date">Bosqich sanasi. Bo'sh — bugun.</param>
/// <param name="Conclusion">Xulosa matni; bo'sh bo'lishi mumkin.</param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi — <see cref="RecordCandidateStageCommand"/> dagi
/// bilan AYNAN bir xil uch holatli qoida. Tuzatish oynasi ham shu maydonni
/// so'raydi: «gaplashdim, besh kundan keyin qayta qo'ng'iroq» degan qaror
/// ko'pincha aynan natija yozilayotgan lahzada tug'iladi.
/// </param>
/// <param name="ClearNextContact">Sanani tozalash.</param>
public sealed record EditCandidateStageEntryCommand(
    Guid CandidateId,
    Guid EntryId,
    DateOnly? Date = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool ClearNextContact = false);

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
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi — berilsa qo'yiladi, berilmasa MAVJUDI joyida
/// qoladi.
/// <para>
/// ⭐ Rezerv — yakunlar orasida YAGONA joy: rad etish va voz kechish
/// sanani tozalaydi, rezerv esa saqlaydi. Sabab
/// <c>Candidate.Close</c> izohida: rezerv chiqish emas, KECHIKTIRISH
/// («o'rin bo'shasa chaqiramiz»), ya'ni qo'ng'iroq rejasi aynan o'sha
/// yerda o'rinli.
/// </para>
/// </param>
public sealed record ReserveCandidateCommand(
    Guid CandidateId,
    DateOnly? At = null,
    string? Reason = null,
    DateOnly? NextContactDate = null);

/// <summary>Voronkadan chiqqan nomzodni qaytarish.</summary>
/// <remarks>
/// Uchta manbadan: rezervdan, RAD ETILGANDAN va o'zi VOZ KECHGANDAN
/// (foydalanuvchi talabi, 2026-09-16). Qoidalar domenda —
/// <c>Candidate.ReturnToFunnel</c>.
/// </remarks>
/// <param name="CandidateId">Nomzod.</param>
/// <param name="Note">
/// Qaytarish sababi — <c>candidate_notes</c> ga
/// <see cref="CandidateNoteKind.ReturnToFunnel"/> turi bilan yoziladi va
/// nomzodning lentasida abadiy qoladi.
/// <para>
/// ⚠️ Rad etilgan va voz kechgan nomzodda MAJBURIY («qarorni bekor
/// qilyapmiz — nega?»), rezervdan qaytarishda ixtiyoriy. Tekshiruv
/// domenda.
/// </para>
/// <para>
/// U <c>ClosingNote</c> GA yozilmaydi va yozilishi ham MUMKIN emas:
/// qaytarish o'sha tranzaksiyada <c>ClosingNote</c> ni tozalaydi, ya'ni
/// izoh yozilgan zahoti o'chib ketardi (<see cref="CandidateNote"/> izohi).
/// </para>
/// </param>
/// <param name="NextContactDate">
/// Keyingi aloqa sanasi. Berilmasa mavjudi TEGILMAYDI — rezervda qo'yilgan
/// qo'ng'iroq rejasi nomzod voronkaga qaytgach ham o'z kuchida qoladi.
/// </param>
public sealed record ReturnCandidateToFunnelCommand(
    Guid CandidateId,
    string? Note = null,
    DateOnly? NextContactDate = null);

public sealed class SearchCandidatesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Eng katta sahifa hajmi.</summary>
    public const int MaxPageSize = 200;

    private readonly IRecruitingReadModel _readModel;

    /// <summary>
    /// «Bugun» ni aynan SHU qatlam biladi.
    /// </summary>
    /// <remarks>
    /// <c>contactDue</c> filtri o'qish modeliga BAYROQ emas, SANA bo'lib
    /// tushadi: <c>RecruitingReadModel</c> faqat <c>AppDbContext</c> ni
    /// oladi va uning soati yo'q. Soatni o'sha yerga ulash uni «faqat
    /// so'rovni biladi» qoidasidan chiqarardi — butun loyihada «bugun»
    /// use-case qatlamining qarori (izohi
    /// <c>CandidateSearchFilter.ContactDueOn</c> da).
    /// </remarks>
    private readonly IClock _clock;

    public SearchCandidatesQueryHandler(IRecruitingReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

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
                /*
                  ⚠️ Filtr POZITSIYA bo'yicha yasaladi. Yangi parametr
                  rekordning OXIRIGA qo'shiladi, aks holda bu yerdagi
                  argumentlar jimgina bir pog'ona surilib ketardi
                  (`CandidateSearchFilter` izohi).
                */
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
                    query.IncludeArchive,

                    // Bayroq SANAGA bu yerda aylanadi — o'qish modelining
                    // soati yo'q (izohi `_clock` maydonida).
                    query.ContactDue ? _clock.Today : null,
                    query.Rejected),
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
            row.HiredEmployeeId,
            row.NextContactDate);
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

/// <summary>
/// Nomzodni boshqa vakansiyaga o'tkazadi.
/// </summary>
/// <remarks>
/// <para>
/// Amal bosqichni O'ZGARTIRMAYDI, ya'ni izoh uchun bosqich yozuvi yo'q —
/// shu sababli u <c>candidate_notes</c> ga tushadi
/// (<see cref="CandidateNoteKind.Reassign"/>). Nega mavjud maydonlarning
/// bittasi ham yaramaganini <see cref="CandidateNote"/> izohi tushuntiradi.
/// </para>
/// <para>
/// Tartib: avval o'tkazish (u nomzod JARAYONDA ekanini tekshiradi), keyin
/// izoh, keyin aloqa sanasi, eng oxirida saqlash. Teskarisi bo'lsa, rad
/// etilgan nomzodga izoh yozilib, o'tkazishning o'zi xato bilan
/// qaytardi — va izoh o'sha tranzaksiyada bekor bo'lsa ham, kod
/// «bo'lmaydigan ish bajarildi» degan holatdan o'tib ketardi.
/// </para>
/// </remarks>
public sealed class ReassignCandidateCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly IVacancyRepository _vacancies;
    private readonly RecruitingMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ReassignCandidateCommandHandler(
        ICandidateRepository candidates,
        IVacancyRepository vacancies,
        RecruitingMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _vacancies = vacancies;
        _mapper = mapper;
        _currentUser = currentUser;
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

        /*
          ⚠️ Bo'shlik tekshiruvi CHAQIRUVCHIDA — domen `AddNote` bo'sh matn
          uchun istisno tashlaydi, xato kodi qaytarmaydi (sababi
          `CandidateNote.Create` izohida: kod foydalanuvchiga ko'rsatiladi
          va to'rt tilga tarjima talab qiladi, bu holat esa
          dasturchining xatosi). Izoh esa HAMMA joyda ixtiyoriy: «izoh
          yozilmadi» — yozuvsiz ko'rinadi.
        */
        if (!string.IsNullOrWhiteSpace(command.Note))
        {
            candidate.AddNote(CandidateNoteKind.Reassign, command.Note, _currentUser.UserId);
        }

        // Sana berilsa QO'YILADI, berilmasa tegilmaydi: bu amalda
        // «tozalash» yo'q (jadval `Candidate.Close` izohida).
        if (command.NextContactDate is { } nextContact)
        {
            candidate.SetNextContactDate(nextContact);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Bosqich natijasini yozadi.
/// </summary>
/// <remarks>
/// <para>
/// Bosqichning MAVJUDLIGI va FAOLLIGI shu yerda tekshiriladi: domen
/// ma'lumotnomani ko'rmaydi. Faolsizlantirilgan bosqichga o'tkazish rad
/// etiladi — bu faolsizlantirishning yagona amaliy ma'nosi
/// (<c>RecruitingErrors.StageInactive</c> izohiga qarang).
/// </para>
/// <para>
/// Keyingi aloqa sanasi bosqich YOZILGANDAN KEYIN va saqlashdan OLDIN
/// qo'yiladi (<see cref="ApplyNextContact"/>): bosqich rad etilsa,
/// o'zgargan sana ortidan qolib ketmasligi kerak.
/// </para>
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
            command.Conclusion,
            _currentUser.UserId);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        ApplyNextContact(candidate, command.NextContactDate, command.ClearNextContact);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Keyingi aloqa sanasining UCH HOLATLI qoidasi: qo'yish, tozalash yoki
    /// tegmaslik.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qoida bitta joyda turadi va uni ikkita amal ishlatadi — bosqich
    /// yozish va suhbatga chaqirish
    /// (<see cref="InviteCandidateToInterviewCommandHandler"/>). Ikki
    /// nusxa yozilsa, ular bir kun ajralib ketardi va farqi faqat
    /// «sudrab o'tkazishda sana yo'qoldi» degan shikoyatda ko'rinardi.
    /// Bir xil naqsh <see cref="RegisterCandidateCommandHandler.ParsePhone"/>
    /// da allaqachon bor: <c>internal static</c>, faylning ichida qoladi.
    /// </para>
    /// <para>
    /// Tartib muhim: <paramref name="clearNextContact"/> BIRINCHI
    /// tekshiriladi, ya'ni ikkalasi ham berilgan (mos kelmaydigan) so'rovda
    /// tozalash ustun keladi. Xato qaytarilmaydi — mijoz ikkalasini birga
    /// yubormaydi va bunday so'rov uchun alohida xato kodi to'rt tilga
    /// tarjima talab qilardi.
    /// </para>
    /// </remarks>
    internal static void ApplyNextContact(Candidate candidate, DateOnly? nextContactDate, bool clearNextContact)
    {
        if (clearNextContact)
        {
            candidate.SetNextContactDate(null);
            return;
        }

        if (nextContactDate is { } date)
        {
            candidate.SetNextContactDate(date);
        }

        // Ikkalasi ham bo'sh — saqlangan sana O'ZGARMAYDI. Aynan shu tarmoq
        // voronkadagi sudrash uchun: u oynasiz ishlaydi va HR qo'ygan sanani
        // jimgina o'chirib yuborishi mumkin emas.
    }
}

/// <summary>
/// Tarixdagi bitta bosqich yozuvini tuzatadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bosqich ma'lumotnomasi bu yerda TEKSHIRILMAYDI</b> —
/// <see cref="RecordCandidateStageCommandHandler"/> dan farqli. Sabab:
/// tuzatish bosqichni o'zgartirmaydi, ya'ni tekshiradigan yangi qiymat
/// yo'q. Aks holda faolsizlantirilgan bosqichdagi ESKI yozuvning izohini
/// tuzatib bo'lmasdi — holbuki tarix aynan shu narsani saqlash uchun bor.
/// </para>
/// <para>
/// <b>Nomzodning holati ham tekshirilmaydi</b>: rad etilgan nomzodning
/// tarixidagi xato ham tuzatiladi (sababi <c>Candidate.EditStageEntry</c>
/// izohida).
/// </para>
/// </remarks>
public sealed class EditCandidateStageEntryCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly RecruitingMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EditCandidateStageEntryCommandHandler(
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

    public async Task<Result<CandidateDto>> HandleAsync(
        EditCandidateStageEntryCommand command,
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

        var edited = candidate.EditStageEntry(
            command.EntryId,
            command.Date ?? _clock.Today,
            command.Conclusion);

        if (edited.IsFailure)
        {
            return edited.Error;
        }

        RecordCandidateStageCommandHandler.ApplyNextContact(
            candidate,
            command.NextContactDate,
            command.ClearNextContact);

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
/// (<see cref="ReturnCandidateToFunnelCommandHandler"/>): u nomzodni
/// voronkadan chiqarmaydi, aksincha — qaytaradi. Uni shu sinfga qo'shish
/// «yopish» degan nomni yolg'onga aylantirardi.
/// </para>
/// <para>
/// ⭐ <b>Keyingi aloqa sanasi uchun bu yerda BIRORTA qator yo'q</b> va bu
/// ataylab: <c>Candidate.Close</c> uni O'ZI tozalaydi (rezervdan tashqari
/// hamma yakunda). Ya'ni rad etish va voz kechish qo'shimcha ish
/// bajarmaydi, rezerv esa sanani domenning o'ziga uzatadi
/// (<see cref="ReserveCandidateCommand.NextContactDate"/>). Qoidani bu
/// yerda takrorlash — uni ikki joyda saqlash bo'lardi.
/// </para>
/// <para>
/// Shu sababli <c>RejectCandidateCommand</c> va
/// <c>WithdrawCandidateCommand</c> ga sana maydoni QO'SHILMADI. HTTP
/// so'rovi (<c>CloseCandidateRequest</c>) uchalasi uchun bitta bo'lgani
/// uchun maydon so'rovda bor, lekin bu ikkisida JIMGINA e'tiborsiz
/// qoldiriladi: xato qaytarish hech narsani himoya qilmasdi, faqat
/// mijozni buzardi (sababi so'rov rekordi izohida).
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
            candidate => candidate.MoveToReserve(
                command.At ?? _clock.Today,
                command.Reason,
                command.NextContactDate),
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
/// Voronkadan chiqqan nomzodni qaytaradi — rezervdan, rad etilgandan
/// yoki voz kechgandan.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida use-case, <see cref="CloseCandidateCommandHandler"/>
/// ning bir qismi emas.</b> U yerdagi uchala amal nomzodni voronkadan
/// CHIQARADI va bitta umumiy qoidaga bo'ysunadi («faqat jarayondagi
/// nomzod yopiladi»). Bu amal esa teskari yo'nalishda ishlaydi va uning
/// sharti ham teskari: nomzod voronkadan CHIQQAN bo'lishi kerak.
/// Ikkalasini bitta sinfga yig'ish «yopish» degan nomni yolg'onga
/// aylantirardi.
/// </para>
/// <para>
/// Bosqich va bosqichlar tarixi saqlanadi — nomzod qayerda to'xtagan
/// bo'lsa, o'sha yerdan davom etadi (sababi
/// <c>Candidate.ReturnToFunnel</c> izohida).
/// </para>
/// <para>
/// ⭐ Izohni DOMEN o'zi lentaga yozadi (<c>ReturnToFunnel</c> ichida) —
/// bu yerda emas. Sabab: sabab rad etish va voz kechishdan qaytarishda
/// MAJBURIY, ya'ni tekshiruv va yozuv bitta joyda turishi kerak. Aks
/// holda «tekshirdi, lekin yozmadi» degan tarmoq paydo bo'lardi.
/// </para>
/// </remarks>
public sealed class ReturnCandidateToFunnelCommandHandler
{
    private readonly ICandidateRepository _candidates;
    private readonly RecruitingMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnCandidateToFunnelCommandHandler(
        ICandidateRepository candidates,
        RecruitingMapper mapper,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _mapper = mapper;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CandidateDto>> HandleAsync(
        ReturnCandidateToFunnelCommand command,
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

        // Sabab, kim qaytargani va sana domenning O'ZIGA uzatiladi:
        // qaytarish `ClosedAt`, `ClosingNote` va rad etish xabarining
        // belgisini tozalaydi, sababni lentaga yozadi, aloqa sanasiga
        // esa tegmaydi — yangisi berilgan bo'lsa qo'yadi (izohi
        // `Candidate.ReturnToFunnel` da).
        var returned = candidate.ReturnToFunnel(
            command.Note,
            _currentUser.UserId,
            command.NextContactDate);

        if (returned.IsFailure)
        {
            return returned.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(candidate, cancellationToken).ConfigureAwait(false);
    }
}
