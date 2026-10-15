using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Search.Dtos;

/// <summary>
/// Global qidiruv natijasi — yuqori paneldagi qidiruv maydonining javobi.
/// </summary>
/// <remarks>
/// <para>
/// Har bir guruh (<see cref="Employees"/>, <see cref="Candidates"/>,
/// <see cref="Vacancies"/>) ruxsatga qarab <c>null</c> bo'lishi mumkin:
/// <c>null</c> — «bu bo'limni ko'rishga ruxsat yo'q», bo'sh ro'yxat esa
/// «qidirildi, hech narsa topilmadi». Ekran ikkisini farqlaydi — birinchisida
/// guruh umuman chizilmaydi (dashboard'dagi <c>recruiting</c> bloki bilan
/// bir xil naqsh).
/// </para>
/// <para>
/// Guruhda faqat birinchi bir nechta yozuv qaytadi, <c>Total</c> esa
/// topilganlarning HAMMASI — «yana 25 ta» degan yozuv uchun.
/// </para>
/// </remarks>
public sealed record GlobalSearchDto(
    string Query,
    GlobalSearchEmployeesDto? Employees,
    GlobalSearchCandidatesDto? Candidates,
    GlobalSearchVacanciesDto? Vacancies);

/// <summary>Xodimlar guruhi — barcha holatlar, ishlayotganlar birinchi.</summary>
public sealed record GlobalSearchEmployeesDto(
    int Total,
    IReadOnlyList<GlobalSearchEmployeeDto> Items);

/// <summary>Bitta topilgan xodim.</summary>
/// <remarks>
/// Holat va lavozim xodimning ASOSIY (bo'lmasa eng oxirgi) mehnat
/// munosabatidan — xodimlar ro'yxatidagi bilan aynan bir xil qoida.
/// Munosabati yo'q xodimda <c>Status</c> <c>null</c>.
/// </remarks>
public sealed record GlobalSearchEmployeeDto(
    Guid Id,
    string FullName,
    string? PhotoKey,
    EmploymentStatus? Status,
    string? Position,
    string? Department,
    IReadOnlyList<EmployeeFlagType> Flags);

/// <summary>Nomzodlar guruhi — barcha holatlar va Excel arxivi, eng yangisi birinchi.</summary>
public sealed record GlobalSearchCandidatesDto(
    int Total,
    IReadOnlyList<GlobalSearchCandidateDto> Items);

/// <summary>Bitta topilgan ariza (nomzod).</summary>
/// <remarks>
/// Bitta odam bir necha marta ariza topshirgan bo'lsa, har bir ariza
/// alohida qator — har birining o'z vakansiyasi va holati bor.
/// </remarks>
public sealed record GlobalSearchCandidateDto(
    Guid Id,
    string FullName,
    bool HasPhoto,
    CandidateStatus Status,
    string? Vacancy,
    string? Stage,
    DateTimeOffset CreatedAt,
    Guid? HiredEmployeeId);

/// <summary>Vakansiyalar guruhi — ochiq va yopiqlar, eng yangisi birinchi.</summary>
public sealed record GlobalSearchVacanciesDto(
    int Total,
    IReadOnlyList<GlobalSearchVacancyDto> Items);

/// <summary>Bitta topilgan vakansiya.</summary>
public sealed record GlobalSearchVacancyDto(
    Guid Id,
    string Title,
    VacancyStatus Status,
    int Openings,
    int CandidateCount);
