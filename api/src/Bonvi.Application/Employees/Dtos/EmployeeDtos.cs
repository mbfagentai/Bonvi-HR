using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Dtos;

/// <summary>
/// Pul summasi.
/// </summary>
/// <remarks>
/// <c>Formatted</c> serverda tayyorlanadi: «1 500 000 so'm» ko'rinishi
/// hisobotda, ro'yxatda va PDF'da BIR XIL bo'lishi kerak, brauzer
/// sozlamasiga bog'liq bo'lmasligi kerak.
/// </remarks>
public sealed record MoneyDto(decimal Amount, string Currency, string Formatted);

/// <summary>Sahifalangan natija.</summary>
/// <param name="Items">Shu sahifadagi yozuvlar.</param>
/// <param name="Page">Joriy sahifa (1 dan).</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Total">Filtrga mos umumiy yozuvlar soni.</param>
/// <param name="PageCount">Jami sahifalar soni.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total,
    int PageCount);

/// <summary>
/// Xodimlar ro'yxatining bitta qatori.
/// </summary>
/// <remarks>
/// Bu DTO xodim <b>kartasi</b> emas: unda faqat jadvalda ko'rinadigan
/// ustunlar bor. Karta uchun <see cref="EmployeeCardDto"/> ishlatiladi —
/// ro'yxat so'rovi 2000 qatorni tortadi, har biriga to'liq karta
/// yig'ish esa keraksiz yuk.
/// </remarks>
public sealed record EmployeeListItemDto(
    Guid Id,
    string FullName,
    string ShortName,
    DateOnly? BirthDate,
    string? Pinfl,
    string? Passport,
    string? Phone,
    string? PhotoKey,
    bool NeedsReview,
    Guid? EmploymentId,
    Guid? LegalEntityId,
    string? LegalEntity,
    Guid? DepartmentId,
    string? Department,
    Guid? PositionId,
    string? Position,
    EmploymentStatus? Status,
    DateOnly? HireDate,
    decimal? Rate,
    MoneyDto? Salary,
    IReadOnlyList<EmployeeFlagType> Flags);

/// <summary>Xodim — shaxs ma'lumotlari.</summary>
public sealed record EmployeeDto(
    Guid Id,
    string LastName,
    string FirstName,
    string? MiddleName,
    string FullName,
    string ShortName,
    DateOnly? BirthDate,
    Gender? Gender,
    string? Pinfl,
    string? Passport,
    string? Phone,
    string? SecondPhone,
    string? Address,
    string? Speciality,
    string? TelegramUrl,
    string? PhotoKey,
    string? CriminalRecordNote,
    bool? SecurityChecked,
    bool? RegisteredOnMehnatUz,
    IReadOnlyList<Guid> DirectionIds,
    bool NeedsReview,
    string? ReviewNote);

/// <summary>Yuklangan rasmning saqlash kaliti.</summary>
/// <remarks>
/// Rasmning O'ZI qaytarilmaydi: yuklashdan keyin interfeys uni odatdagi
/// <c>GET .../photo</c> orqali oladi va shu yo'l brauzer keshiga tushadi.
/// Kalit esa kartani darhol yangilash uchun kerak — sahifani qayta
/// yuklamasdan rasmning o'zgarganini bilish mumkin bo'ladi.
/// </remarks>
public sealed record EmployeePhotoDto(string PhotoKey);

/// <summary>Xodim belgisi (rezerv / qora ro'yxat / nazorat / qobiliyatli).</summary>
public sealed record EmployeeFlagDto(
    Guid Id,
    Guid EmployeeId,
    EmployeeFlagType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Reason,
    Guid? ResponsibleEmployeeId,
    bool IsActive);

/// <summary>Belgi — xodim nomi bilan (ro'yxat ekrani uchun).</summary>
public sealed record FlaggedEmployeeDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? PhotoKey,
    EmployeeFlagType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Reason,
    Guid? ResponsibleEmployeeId,
    string? ResponsibleName,
    bool IsActive);

/// <summary>
/// Xodim kartasi: shaxs + barcha mehnat munosabatlari + belgilar.
/// </summary>
/// <remarks>
/// Uchala qism bitta javobda qaytariladi, chunki karta ochilganda ular
/// baribir kerak bo'ladi. Uchta alohida so'rov — uchta kutish va
/// bir-biriga mos kelmaydigan holat xavfi.
/// </remarks>
public sealed record EmployeeCardDto(
    EmployeeDto Employee,
    IReadOnlyList<EmploymentDto> Employments,
    IReadOnlyList<EmployeeFlagDto> Flags,
    decimal TotalActiveRate);
