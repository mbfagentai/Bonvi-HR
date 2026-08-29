using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
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
    IReadOnlyList<EmployeeFlagType> Flags,
    IReadOnlyList<EmployeeDirectionDto> Directions);

/// <summary>Yo'nalish — ro'yxatdagi nishon uchun.</summary>
public sealed record EmployeeDirectionDto(Guid Id, string Name);

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
    string? MehnatUzUrl,
    // Kalitning O'ZI qaytariladi, rasm emas — `PhotoKey` bilan aynan bir xil
    // sabab: skrinshot alohida endpoint bilan olinadi va kalit ikki ish
    // qiladi (bor-yo'qligini bildiradi va mijoz keshini eskirtiradi).
    string? MehnatUzScreenshotKey,
    IReadOnlyList<Guid> DirectionIds,
    bool NeedsReview,
    string? ReviewNote);

/// <summary>Yuklangan mehnat.uz skrinshotining saqlash kaliti.</summary>
/// <remarks>
/// <see cref="EmployeePhotoDto"/> bilan bir xil naqsh: baytlar emas, KALIT
/// qaytadi — interfeys rasmni odatdagi <c>GET</c> orqali oladi va kalit
/// o'zgargani kesh uchun signal bo'ladi.
/// </remarks>
public sealed record MehnatUzScreenshotDto(string ScreenshotKey);

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

/// <summary>
/// Xodimning nomzodlik davridagi bitta bosqich xulosasi.
/// </summary>
/// <remarks>
/// <para>
/// Bu — rekruting <c>CandidateStageEntryDto</c> sining xodim kartasidagi
/// ko'rinishi va u ATAYLAB alohida tip. Ikki sabab: bu yerga nomzod
/// identifikatori qo'shiladi (bitta odamda ikkita nomzodlik bo'lishi
/// mumkin — ketib qaytib kelgan holat) va «kim yozdi» NOMI bilan keladi,
/// chunki xodim kartasini ochgan HR o'sha foydalanuvchini
/// identifikatordan tanimaydi. Rekruting DTO'sini qayta ishlatish esa
/// xodimlar modulini rekrutingning ko'rsatish shakliga bog'lab qo'yardi.
/// </para>
/// <para>
/// Yozuvni bu yerdan O'ZGARTIRIB BO'LMAYDI — sababi
/// <c>GetEmployeeStagesQuery</c> izohida.
/// </para>
/// </remarks>
/// <param name="Id">Bosqich yozuvi.</param>
/// <param name="CandidateId">Qaysi nomzod yozuvidan olingan.</param>
/// <param name="StageId">Bosqich.</param>
/// <param name="Stage">Bosqich nomi. <c>null</c> — ma'lumotnomadan topilmadi.</param>
/// <param name="Date">Bosqich o'tkazilgan sana (kengash kuni, suhbat kuni).</param>
/// <param name="Outcome">Kutilmoqda / o'tdi / o'tmadi.</param>
/// <param name="Conclusion">Xulosa matni.</param>
/// <param name="RecordedBy">Kim yozdi — tizim foydalanuvchisi.</param>
/// <param name="RecordedByName">Yozgan foydalanuvchining F.I.Sh.</param>
/// <param name="RecordedAt">Qachon yozildi.</param>
public sealed record EmployeeStageEntryDto(
    Guid Id,
    Guid CandidateId,
    Guid StageId,
    string? Stage,
    DateOnly Date,
    StageOutcome Outcome,
    string? Conclusion,
    Guid? RecordedBy,
    string? RecordedByName,
    DateTimeOffset RecordedAt);

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
