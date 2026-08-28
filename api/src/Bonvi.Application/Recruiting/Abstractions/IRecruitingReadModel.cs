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

    /// <summary>Lavozim nomi.</summary>
    Position = 1,
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
/// <param name="Status">Holat bo'yicha filtr. Bo'sh — hammasi (rad etilganlar ham).</param>
/// <param name="Source">Manba bo'yicha filtr: qo'lda kiritilgan / botdan kelgan.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
public sealed record CandidateSearchFilter(
    string? Search = null,
    Guid? VacancyId = null,
    Guid? StageId = null,
    CandidateStatus? Status = null,
    CandidateSource? Source = null,
    int Page = 1,
    int PageSize = 25,
    CandidateSortField Sort = CandidateSortField.CreatedAt,
    bool Descending = true)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
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
/// <param name="VacancyName">Vakansiyaning lavozim nomi.</param>
/// <param name="CurrentStageId">Joriy bosqich.</param>
/// <param name="CurrentStageName">Joriy bosqich nomi.</param>
/// <param name="Status">Voronkadagi holati.</param>
/// <param name="Source">Qayerdan kelgan.</param>
/// <param name="HasResume">Rezyume biriktirilganmi.</param>
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
    DateTimeOffset CreatedAt,
    Guid? HiredEmployeeId);

/// <summary>
/// Vakansiyalar ro'yxatining filtri.
/// </summary>
/// <param name="Status">Holat bo'yicha filtr. Bo'sh — hammasi.</param>
/// <param name="PositionId">Lavozim bo'yicha filtr.</param>
/// <param name="DepartmentId">Bo'lim bo'yicha filtr (AYNAN shu bo'lim, shoxsiz).</param>
/// <param name="LegalEntityId">Yuridik shaxs bo'yicha filtr.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifadagi yozuvlar soni.</param>
/// <param name="Sort">Saralash ustuni.</param>
/// <param name="Descending">Teskari tartib.</param>
public sealed record VacancyListFilter(
    VacancyStatus? Status = null,
    Guid? PositionId = null,
    Guid? DepartmentId = null,
    Guid? LegalEntityId = null,
    int Page = 1,
    int PageSize = 25,
    VacancySortField Sort = VacancySortField.OpenedAt,
    bool Descending = true)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Bitta sahifa: vakansiyalar va umumiy soni.</summary>
public sealed record VacancyPage(IReadOnlyList<VacancyRow> Items, int Total);

/// <summary>Vakansiyalar ro'yxatining bitta xom qatori.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="PositionName">Lavozim nomi.</param>
/// <param name="LegalEntityId">Yuridik shaxs.</param>
/// <param name="LegalEntityName">Yuridik shaxs nomi.</param>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="DepartmentName">Bo'lim nomi.</param>
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
    Guid PositionId,
    string? PositionName,
    Guid LegalEntityId,
    string? LegalEntityName,
    Guid? DepartmentId,
    string? DepartmentName,
    int Openings,
    VacancyStatus Status,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName,
    string? Note,
    int CandidateCount,
    int HiredCount);
