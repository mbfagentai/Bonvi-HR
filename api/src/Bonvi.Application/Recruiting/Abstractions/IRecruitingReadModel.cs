using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Abstractions;

/// <summary>
/// Rekruting ro'yxatlari uchun <b>o'qish</b> porti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega repozitoriylar yetmaydi.</b> <c>ICandidateRepository</c> va
/// <c>IVacancyRepository</c> agregatni identifikator bo'yicha yuklaydi —
/// bu yozish tomonining vazifasi. Ro'yxat ekrani esa boshqa narsani
/// so'raydi: vakansiya, bosqich va lavozim NOMLARI bilan birlashtirilgan,
/// filtrlangan, saralangan va sahifalangan jadval. Bu proyeksiyani
/// repozitoriyga qo'shish domenni ko'rsatish modeliga bog'lab qo'yardi
/// (<c>IEmployeeReadModel</c> va <c>IIncentiveReadModel</c> bilan bir xil
/// sabab).
/// </para>
/// <para>
/// <b>Filtr, saralash va sahifalash — BAZADA.</b> Nomzodlar soni xodimlar
/// sonidan tez o'sadi: har bir vakansiyaga o'nlab anketa tushadi va rad
/// etilganlar HECH QACHON o'chirilmaydi (ular kelasi yilgi izlanishning
/// asosiy bazasi). Xotirada filtrlash bir yildan keyin butun jadvalni har
/// sahifa uchun tarmoqdan olib o'tishga aylanardi.
/// </para>
/// </remarks>
public interface IRecruitingReadModel
{
    /// <summary>Nomzodlar ro'yxati — rekrutingning asosiy ekrani.</summary>
    Task<CandidatePage> SearchCandidatesAsync(
        CandidateSearchFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rad etilgan, lekin Telegram xabarini hali olmagan nomzodlar.
    /// </summary>
    /// <remarks>
    /// Faqat botga kerak va faqat uchta shart birga bajarilganda:
    /// nomzod rad etilgan, Telegram suhbati ma'lum va xabar hali
    /// yuborilmagan. Uchalasi ham qisman indeks bilan qoplangan
    /// (<c>RecruitingConfigurations</c>).
    /// </remarks>
    Task<IReadOnlyList<RejectionNoticeDto>> ListPendingRejectionNoticesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suhbatga chaqirilgan, lekin taklif xabarini hali olmagan nomzodlar.
    /// </summary>
    /// <remarks>
    /// Yuqoridagining egizagi, lekin sharti nomzodning HOLATIGA bog'liq
    /// emas: taklif voronkada turgan nomzodga yuboriladi. Uchta shart —
    /// suhbat vaqti qo'yilgan, Telegram suhbati ma'lum va xabar hali
    /// ketmagan.
    /// </remarks>
    Task<IReadOnlyList<InterviewNoticeDto>> ListPendingInterviewNoticesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Vakansiyalar ro'yxati — nomzodlar sanog'i bilan.</summary>
    Task<VacancyPage> ListVacanciesAsync(
        VacancyListFilter filter,
        CancellationToken cancellationToken = default);
}

/// <summary>Nomzodlar ro'yxatini saralash ustunlari.</summary>
/// <remarks>
/// Ataylab enum (<c>EmployeeSortField</c> bilan bir xil sabab): mijozdan
/// kelgan matn to'g'ridan-to'g'ri SQL'ga tushmaydi va noma'lum ustun
/// so'ralganda sukut bo'yicha tartib ishlaydi.
/// </remarks>
public enum CandidateSortField
{
    /// <summary>Kiritilgan vaqt (sukut bo'yicha, yangidan eskiga).</summary>
    CreatedAt = 0,

    /// <summary>Familiya, ism.</summary>
    Name = 1,

    /// <summary>Voronkadagi bosqich tartibi.</summary>
    Stage = 2,
}

/// <summary>Vakansiyalar ro'yxatini saralash ustunlari.</summary>
public enum VacancySortField
{
    /// <summary>Ochilgan sana (sukut bo'yicha, yangidan eskiga).</summary>
    OpenedAt = 0,

    /// <summary>
    /// Vakansiya nomi (e'lon nomi).
    /// </summary>
    /// <remarks>
    /// Ilgari bu <c>Position</c> edi va lavozim nomi bo'yicha saralardi.
    /// Vakansiyada lavozim qolmagach (sababi <c>Vacancy</c> izohida),
    /// ro'yxatning yagona matnli ustuni e'lon nomi bo'lib qoldi.
    /// </remarks>
    Title = 1,
}

/// <summary>
/// Nomzodlar ro'yxatining filtri.
/// </summary>
/// <param name="Search">
/// F.I.Sh. yoki TELEFON bo'yicha qidiruv. JSHSHIR va pasport ATAYLAB yo'q:
/// nomzodda ular umuman bo'lmaydi — u hali hujjat topshirmagan. Qoidaning
/// qolgan qismi xodimlar ro'yxatidagi bilan aynan bir xil (kirill imlo
/// yig'ilishi, telefon ajratgichlari) va u bitta joydan —
/// <c>SearchFolding</c> dan keladi.
/// </param>
/// <param name="VacancyId">Vakansiya bo'yicha filtr.</param>
/// <param name="StageId">Bosqich bo'yicha filtr — voronka ustunini chizish uchun.</param>
/// <param name="Status">
/// Holat bo'yicha filtr. Bo'sh — ISHGA OLINGANLARDAN tashqari hammasi
/// (sababi <see cref="CandidateSearchFilter.HideHired"/> izohida).
/// </param>
/// <param name="Source">Manba bo'yicha filtr: qo'lda kiritilgan / botdan kelgan.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
/// <param name="IncludeArchive">Excel arxivi ham ko'rsatilsinmi (sukut: yo'q).</param>
public sealed record CandidateSearchFilter(
    string? Search = null,
    Guid? VacancyId = null,
    Guid? StageId = null,
    CandidateStatus? Status = null,
    CandidateSource? Source = null,
    int Page = 1,
    int PageSize = 25,
    CandidateSortField Sort = CandidateSortField.CreatedAt,
    bool Descending = true,
    bool IncludeArchive = false)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Excel arxivi ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// Manba ATAYLAB tanlangan bo'lsa chiqarilmaydi: HR «Excel arxivi»ni
    /// tanlagan ekan, aynan o'shani ko'rmoqchi. Aks holda tanlov jimgina
    /// bo'sh ro'yxat berardi.
    /// </remarks>
    public bool HideArchive => !IncludeArchive && Source is null;

    /// <summary>
    /// Ishga olinganlar ro'yxatdan chiqarilsinmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Sukut bo'yicha CHIQARILADI</b> (foydalanuvchi talabi,
    /// 2026-09-07). Ishga olingan nomzod endi XODIM va uning o'rni
    /// «Xodimlar» bo'limida; nomzodlar ro'yxati esa «hozir kim ish
    /// ustida?» degan savolga javob beradi. «Ishga olingan» belgisi bilan
    /// turgan qatorlar shu savolni ko'mib yuborardi.
    /// </para>
    /// <para>
    /// <b>Yozuv O'CHIRILMAYDI</b> — u vakansiyaning «2/3 to'ldirildi»
    /// hisobiga (<c>VacancyDto.HiredCount</c>), bot samaradorligi
    /// hisobotiga (<c>Source</c>) va xodim kartasidan nomzodga
    /// qaytishga kerak. Faqat SUKUT ro'yxatdan chiqariladi.
    /// </para>
    /// <para>
    /// <b>Holat ataylab tanlangan bo'lsa chiqarilmaydi</b> — <c>HideArchive</c>
    /// bilan aynan bir xil qoida: HR «Ishga olingan» ni tanlagan ekan,
    /// aynan o'shani ko'rmoqchi va bo'sh ro'yxat olishi mumkin emas.
    /// </para>
    /// <para>
    /// ⚠️ Qoida FAQAT <see cref="CandidateStatus.Hired"/> ga tegishli.
    /// Rezervdagi nomzod ro'yxatda KO'RINIB TURADI: uning butun ma'nosi
    /// «kerak bo'lganda shu yerdan chaqiramiz» degani va ro'yxatdan
    /// yashirilsa, u ma'no yo'qolardi.
    /// </para>
    /// </remarks>
    public bool HideHired => Status is null;
}

/// <summary>Bitta sahifa: nomzodlar va umumiy soni.</summary>
public sealed record CandidatePage(IReadOnlyList<CandidateRow> Items, int Total);

/// <summary>
/// Nomzodlar ro'yxatining bitta xom qatori.
/// </summary>
/// <remarks>
/// Qiymat obyektlari (F.I.Sh., telefon) XOM holida qaytadi: o'qish modeli
/// ko'rsatish modelini bilmasligi kerak, ularni DTO'ga yig'ish Application
/// qatlamining ishi (<c>IncentiveListRow</c> dagi bilan bir xil qoida).
/// </remarks>
/// <param name="Id">Identifikator.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="Phone">Telefon (kanonik satr).</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="VacancyId">Vakansiya.</param>
/// <param name="VacancyName">Vakansiyaning nomi (e'lon nomi).</param>
/// <param name="CurrentStageId">Joriy bosqich.</param>
/// <param name="CurrentStageName">Joriy bosqich nomi.</param>
/// <param name="Status">Voronkadagi holati.</param>
/// <param name="Source">Qayerdan kelgan.</param>
/// <param name="HasResume">Rezyume biriktirilganmi.</param>
/// <param name="HasPhoto">Surati bormi — ro'yxat qatorida rasm ko'rsatiladi.</param>
/// <param name="CreatedAt">Kiritilgan vaqt.</param>
/// <param name="HiredEmployeeId">Ishga olingan bo'lsa — xodim kartasi.</param>
public sealed record CandidateRow(
    Guid Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    string? Phone,
    DateOnly? BirthDate,
    string? Speciality,
    Guid VacancyId,
    string? VacancyName,
    Guid? CurrentStageId,
    string? CurrentStageName,
    CandidateStatus Status,
    CandidateSource Source,
    bool HasResume,
    bool HasPhoto,
    DateTimeOffset CreatedAt,
    Guid? HiredEmployeeId);

/// <summary>
/// Vakansiyalar ro'yxatining filtri.
/// </summary>
/// <param name="Status">Holat bo'yicha filtr. Bo'sh — hammasi.</param>
/// <param name="VacancyTitleId">
/// Vakansiya nomi bo'yicha filtr — «shu e'lon bo'yicha nima bo'lyapti».
///
/// Lavozim, bo'lim va firma filtrlari OLIB TASHLANDI: vakansiyada
/// bunday maydonlar endi umuman yo'q (sababi <c>Vacancy</c> izohida).
/// Ular «kim ishga olindi» degan savolga tegishli va o'sha savolga
/// xodimlar ro'yxati javob beradi.
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
/// <param name="ExcludedVacancyId">
/// Ro'yxatdan CHIQARIB tashlanadigan bitta vakansiya. <c>null</c> —
/// hech kim chiqarilmaydi.
///
/// Nega umumiy «id emas» filtri, «umumiy anketani yashir» emas:
/// o'qish modeli qaysi vakansiya nima uchun xizmat qilishini
/// BILMASLIGI kerak — bu use-case qatlamining bilimi. Bu yerda faqat
/// «shu qatorni qo'shma» degan mexanik shart qoladi.
/// </param>
public sealed record VacancyListFilter(
    VacancyStatus? Status = null,
    Guid? VacancyTitleId = null,
    int Page = 1,
    int PageSize = 25,
    VacancySortField Sort = VacancySortField.OpenedAt,
    bool Descending = true,
    Guid? ExcludedVacancyId = null)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Bitta sahifa: vakansiyalar va umumiy soni.</summary>
public sealed record VacancyPage(IReadOnlyList<VacancyRow> Items, int Total);

/// <summary>Vakansiyalar ro'yxatining bitta xom qatori.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="VacancyTitleId">Vakansiya nomi (ma'lumotnoma yozuvi).</param>
/// <param name="VacancyTitleName">Vakansiya nomining matni.</param>
/// <param name="Openings">Nechta odam kerak.</param>
/// <param name="Status">Holati.</param>
/// <param name="OpenedAt">Ochilgan sana.</param>
/// <param name="ClosedAt">Yopilgan sana.</param>
/// <param name="ResponsibleEmployeeId">Mas'ul xodim.</param>
/// <param name="ResponsibleName">Mas'ul xodimning ismi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="CandidateCount">Nomzodlar soni.</param>
/// <param name="HiredCount">Ishga olinganlar soni.</param>
public sealed record VacancyRow(
    Guid Id,
    Guid VacancyTitleId,
    string? VacancyTitleName,
    int Openings,
    VacancyStatus Status,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName,
    string? Note,
    int CandidateCount,
    int HiredCount);
